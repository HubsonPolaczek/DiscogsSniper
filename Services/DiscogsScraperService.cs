using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.Playwright;
using DiscogsSniper.Models;

namespace DiscogsSniper.Services
{
    public class DiscogsScraperService
    {
        private readonly Dictionary<string, decimal> ExchangeRates = new(StringComparer.OrdinalIgnoreCase)
        {
            { "PLN", 1.00m }, { "EUR", 4.30m }, { "USD", 4.00m }, { "GBP", 5.00m },
            { "CHF", 4.40m }, { "JPY", 0.026m }, { "AUD", 2.60m }, { "CAD", 2.90m }, { "SEK", 0.38m }
        };

        public DiscogsScraperService() { }

        public async Task<List<Offer>> GetLatestOffersForLabelAsync(int labelId)
        {
            var offers = new List<Offer>();
            // W metodzie GetLatestOffersForLabelAsync:
            string url = $"https://www.discogs.com/sell/list?label_id={labelId}&sort=listed%2Cdesc&limit=50&format=Vinyl";

            try
            {
                using var playwright = await Playwright.CreateAsync();

                await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = true,
                    Args = new[] { "--disable-blink-features=AutomationControlled" }
                });

                var context = await browser.NewContextAsync(new BrowserNewContextOptions
                {
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36"
                });

                var page = await context.NewPageAsync();
                page.SetDefaultTimeout(60000);

                await page.SetExtraHTTPHeadersAsync(new Dictionary<string, string>
                {
                    { "Accept-Language", "en-US,en;q=0.9" }
                });

                await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
                await page.WaitForTimeoutAsync(3000);

                try { await page.ClickAsync("text=Accept All", new PageClickOptions { Timeout = 2000 }); } catch { }

                string html = await page.ContentAsync();

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var rows = doc.DocumentNode.SelectNodes("//tr[contains(@class, 'shortcut_navigable')]");

                if (rows == null) return offers;

                foreach (var row in rows)
                {
                    try
                    {
                        var offer = new Offer();

                        string releaseIdStr = row.GetAttributeValue("data-release-id", "");
                        if (long.TryParse(releaseIdStr, out long relId)) offer.ReleaseId = relId;

                        var titleNode = row.SelectSingleNode(".//a[contains(@class, 'item_description_title')]");
                        if (titleNode != null)
                        {
                            offer.Title = titleNode.InnerText.Trim();
                            offer.Url = "https://www.discogs.com" + titleNode.GetAttributeValue("href", "");
                            var match = Regex.Match(offer.Url, @"/item/(\d+)");
                            if (match.Success) offer.ListingId = long.Parse(match.Groups[1].Value);
                        }

                        // WYZNACZANIE DATY WYSTAWIENIA (NOWOŚĆ)
                        var descNode = row.SelectSingleNode(".//td[contains(@class, 'item_description')]");
                        if (descNode != null)
                        {
                            string descText = descNode.InnerText;
                            var dateMatch = Regex.Match(descText, @"Listed:\s*(\d+\s+[A-Za-z]+\s+\d+)");
                            if (dateMatch.Success)
                            {
                                if (DateTime.TryParse(dateMatch.Groups[1].Value, out DateTime parsedDate))
                                    offer.DateListed = parsedDate;
                            }
                            else if (descText.Contains("ago", StringComparison.OrdinalIgnoreCase) ||
                                     descText.Contains("minutes", StringComparison.OrdinalIgnoreCase) ||
                                     descText.Contains("hours", StringComparison.OrdinalIgnoreCase))
                            {
                                // Ktoś wystawił płytę dosłownie przed chwilą (Discogs zamiast daty pisze np. "about 2 hours ago")
                                offer.DateListed = DateTime.Today;
                            }
                        }

                        var mediaNode = row.SelectSingleNode(".//p[contains(@class, 'item_condition')]");
                        if (mediaNode != null) offer.Condition = CleanConditionText(mediaNode.InnerText);

                        var sleeveNode = row.SelectSingleNode(".//*[contains(@class, 'item_sleeve_condition')]");
                        if (sleeveNode != null) offer.SleeveCondition = CleanConditionText(sleeveNode.InnerText);

                        var priceSpan = row.SelectSingleNode(".//td[contains(@class, 'item_price')]//span[contains(@class, 'price')]");
                        var shippingNode = row.SelectSingleNode(".//td[contains(@class, 'item_price')]//span[contains(@class, 'item_shipping')]");

                        offer.Price = 0m;
                        offer.ShippingPrice = 0m;
                        string offerCurrency = "PLN";

                        if (priceSpan != null)
                        {
                            offerCurrency = priceSpan.GetAttributeValue("data-currency", "PLN");
                            string priceValStr = priceSpan.GetAttributeValue("data-pricevalue", "0");

                            if (decimal.TryParse(priceValStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal basePriceRaw))
                            {
                                offer.Price = ConvertToPln(offerCurrency, basePriceRaw);
                            }
                        }

                        if (shippingNode != null)
                        {
                            string shipText = shippingNode.InnerText;
                            if (shipText.Contains("Unavailable", StringComparison.OrdinalIgnoreCase))
                            {
                                offer.ShippingPrice = 9999m;
                            }
                            else
                            {
                                decimal shipPriceRaw = ExtractNumberFromString(shipText);
                                offer.ShippingPrice = ConvertToPln(offerCurrency, shipPriceRaw);
                            }
                        }

                        if (offer.ListingId > 0 && offer.ReleaseId > 0)
                        {
                            offers.Add(offer);
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd pobierania ofert z Discogs: {ex.Message}");
            }

            return offers;
        }

        private decimal ConvertToPln(string currency, decimal amount)
        {
            if (ExchangeRates.TryGetValue(currency, out decimal rate)) return amount * rate;
            return 9999m;
        }

        private string CleanConditionText(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";
            var match = Regex.Match(input, @"(Mint \(M\)|Near Mint \(NM or M-\)|Very Good Plus \(VG\+\)|Very Good \(VG\)|Good Plus \(G\+\)|Good \(G\)|Fair \(F\)|Poor \(P\)|Generic|Not Graded|No Cover)", RegexOptions.IgnoreCase);
            if (match.Success) return match.Groups[1].Value;
            return Regex.Replace(input, @"\s+", " ").Trim();
        }

        private decimal ExtractNumberFromString(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return 0m;
            input = input.Replace(",", "");
            var match = Regex.Match(input, @"\d+(?:\.\d+)?");
            if (match.Success && decimal.TryParse(match.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal result))
            {
                return result;
            }
            return 0m;
        }
    }
}