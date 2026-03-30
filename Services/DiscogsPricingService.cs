using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using DiscogsSniper.Models;

namespace DiscogsSniper.Services
{
    public class DiscogsPricingService
    {
        // WKLEJ TU SWÓJ NOWY TOKEN!!!
        private readonly string _apiToken = "uNWftdtgmrQQVuxpMdYvThYLOMtqTYJplUIHSwFz";

        private readonly HttpClient _httpClient;

        private readonly Dictionary<string, decimal> ExchangeRates = new(StringComparer.OrdinalIgnoreCase)
        {
            { "PLN", 1.00m }, { "EUR", 4.30m }, { "USD", 4.00m }, { "GBP", 5.00m },
            { "CHF", 4.40m }, { "JPY", 0.026m }, { "AUD", 2.60m }, { "CAD", 2.90m }, { "SEK", 0.38m }
        };

        public DiscogsPricingService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "DiscogsSniperPRO/1.0");
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Discogs token={_apiToken}");
        }

        // Dodaliśmy Action<string> log, żeby Mózg mógł pisać prosto do Twojego okienka!
        public async Task<PriceStats> GetPriceStatsAsync(long releaseId, Action<string> log)
        {
            var stats = new PriceStats();

            try
            {
                string url = $"https://api.discogs.com/marketplace/price_suggestions/{releaseId}";
                var response = await _httpClient.GetAsync(url);
                string json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    // Jeśli Discogs nas blokuje lub token jest zły, zobaczymy to w oknie!
                    log($"[API ERROR] Kod: {(int)response.StatusCode}. Odpowiedź Discogs: {json}");
                    return stats;
                }

                using var doc = JsonDocument.Parse(json);

                // Discogs zwraca puste {} jeśli płyta nigdy się nie sprzedawała
                if (doc.RootElement.ValueKind == JsonValueKind.Object && !doc.RootElement.EnumerateObject().Any())
                {
                    log($"[API INFO] Ta płyta to rzadkość! Discogs w ogóle nie ma historii jej sprzedaży (zwrócił puste dane).");
                    return stats;
                }

                // Szukamy wyceny kaskadowo - jak nie ma VG+, to bierzemy inną!
                if (doc.RootElement.TryGetProperty("Very Good Plus (VG+)", out var prop) ||
                    doc.RootElement.TryGetProperty("Near Mint (NM or M-)", out prop) ||
                    doc.RootElement.TryGetProperty("Very Good (VG)", out prop) ||
                    doc.RootElement.TryGetProperty("Mint (M)", out prop))
                {
                    string currency = prop.GetProperty("currency").GetString() ?? "EUR";
                    decimal value = prop.GetProperty("value").GetDecimal();
                    stats.Median = ConvertToPln(currency, value);
                }
                else
                {
                    log($"[API INFO] Discogs nie wycenił tej płyty dla typowych stanów. Surowe dane: {json}");
                }
            }
            catch (Exception ex)
            {
                log($"[API EXCEPTION] Wystąpił błąd w kodzie: {ex.Message}");
            }

            return stats;
        }

        private decimal ConvertToPln(string currency, decimal amount)
        {
            if (ExchangeRates.TryGetValue(currency, out decimal rate)) return amount * rate;
            return amount;
        }
    }
}