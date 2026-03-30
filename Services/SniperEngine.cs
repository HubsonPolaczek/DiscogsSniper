using System;
using System.Threading;
using System.Threading.Tasks;
using DiscogsSniper.Models;

namespace DiscogsSniper.Services
{
    public class SniperEngine
    {
        private readonly DatabaseService _dbService;
        private readonly DiscogsScraperService _scraper;
        private readonly DiscogsPricingService _pricer;

        private CancellationTokenSource? _cts;

        public event EventHandler<Offer>? OnDealFound;
        public event EventHandler<string>? OnLogMessage;

        public SniperEngine()
        {
            _dbService = new DatabaseService();
            _scraper = new DiscogsScraperService();
            _pricer = new DiscogsPricingService();
        }

        // NOWOŚĆ: Dodano parametr maxAgeDays
        public void StartScanning(int minConditionValue, decimal maxTotalPrice, decimal minProfitPercent, int maxAgeDays)
        {
            if (_cts != null && !_cts.IsCancellationRequested) return;

            _cts = new CancellationTokenSource();
            Task.Run(() => ScanLoopAsync(minConditionValue, maxTotalPrice, minProfitPercent, maxAgeDays, _cts.Token));
            Log($"Silnik uruchomiony. Zysk min. {minProfitPercent}%, Max. Wiek: {maxAgeDays} dni...");
        }

        public void StopScanning()
        {
            _cts?.Cancel();
            Log("Zatrzymuję silnik. Dokończę obecną czynność i staję.");
        }

        private async Task ScanLoopAsync(int minCondition, decimal maxPrice, decimal minProfitPercent, int maxAgeDays, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var labels = _dbService.GetLabels();

                foreach (var label in labels)
                {
                    if (token.IsCancellationRequested) break;
                    if (!label.IsActive) continue;

                    Log($"Skanuję wytwórnię: {label.Name}...");

                    var offers = await _scraper.GetLatestOffersForLabelAsync(label.Id);

                    int rejectedBySeen = 0;
                    int rejectedByPrice = 0;
                    int rejectedByCondition = 0;
                    int rejectedByProfit = 0;
                    int rejectedByAge = 0; // Nowy licznik statystyk!

                    foreach (var offer in offers)
                    {
                        if (token.IsCancellationRequested) break;

                        if (_dbService.IsOfferAlreadySeen(offer.ListingId))
                        {
                            rejectedBySeen++;
                            continue;
                        }

                        _dbService.MarkOfferAsSeen(offer.ListingId);

                        // --- FILTR WIEKU (ŚWIEŻOŚCI) ---
                        if (maxAgeDays > 0 && offer.DateListed.HasValue)
                        {
                            var ageInDays = (DateTime.Today - offer.DateListed.Value.Date).TotalDays;
                            if (ageInDays > maxAgeDays)
                            {
                                Log($"[ODRZUT] Za stara! Płyta '{offer.Title}' wystawiona {ageInDays:F0} dni temu.");
                                rejectedByAge++;
                                continue;
                            }
                        }

                        if (maxPrice > 0 && offer.TotalPrice > maxPrice)
                        {
                            rejectedByPrice++;
                            continue;
                        }

                        int conditionValue = GetConditionValue(offer.Condition);
                        if (conditionValue > 0 && conditionValue < minCondition)
                        {
                            rejectedByCondition++;
                            continue;
                        }

                        var stats = await _pricer.GetPriceStatsAsync(offer.ReleaseId, Log);

                        if (stats.Median != null && stats.Median > 0)
                        {
                            decimal targetMaxPrice = stats.Median.Value * (1m - (minProfitPercent / 100m));

                            if (offer.TotalPrice > targetMaxPrice)
                            {
                                Log($"[ODRZUT] Brak zysku. {offer.Title} za {offer.TotalPrice} zł. (Rynkowa: {stats.Median.Value:F2} zł).");
                                rejectedByProfit++;
                                continue;
                            }
                            offer.IsDeal = true; // <--- OZNACZAMY JAKO OKAZJĘ DLA KOLOROWANIA
                            Log($"[🔥 POTWIERDZONA OKAZJA] Płacisz {offer.TotalPrice:F2} zł za płytę wartą rynkowo ok. {stats.Median.Value:F2} zł!");
                        }
                        else
                        {
                            Log($"[UWAGA] Brak rynkowej wyceny dla {offer.Title}. Przepuszczam do Twojej oceny.");
                        }

                        OnDealFound?.Invoke(this, offer);
                        await Task.Delay(3500, token);
                    }

                    if (offers.Count > 0)
                    {
                        Log($"[Podsumowanie {label.Name}] Odrzucono: Starych {rejectedBySeen}, Wiekowych {rejectedByAge}, Za drogich {rejectedByPrice}, Słaby stan {rejectedByCondition}, Niespełniających progu zysku {rejectedByProfit}");
                    }

                    await Task.Delay(5000, token);
                }

                Log("Pętla zakończona. Czekam na nowości przez 60 sekund...");
                await Task.Delay(TimeSpan.FromSeconds(60), token);
            }
        }

        private void Log(string message)
        {
            OnLogMessage?.Invoke(this, message);
        }

        private int GetConditionValue(string condition)
        {
            if (string.IsNullOrEmpty(condition)) return 0;
            if (condition.Contains("Mint (M)", StringComparison.OrdinalIgnoreCase)) return 8;
            if (condition.Contains("Near Mint", StringComparison.OrdinalIgnoreCase) || condition.Contains("NM", StringComparison.OrdinalIgnoreCase)) return 7;
            if (condition.Contains("Very Good Plus", StringComparison.OrdinalIgnoreCase) || condition.Contains("VG+", StringComparison.OrdinalIgnoreCase)) return 6;
            if (condition.Contains("Very Good", StringComparison.OrdinalIgnoreCase) || condition.Contains("VG", StringComparison.OrdinalIgnoreCase)) return 5;
            if (condition.Contains("Good Plus", StringComparison.OrdinalIgnoreCase) || condition.Contains("G+", StringComparison.OrdinalIgnoreCase)) return 4;
            if (condition.Contains("Good", StringComparison.OrdinalIgnoreCase) || condition.Contains("G", StringComparison.OrdinalIgnoreCase)) return 3;
            if (condition.Contains("Fair", StringComparison.OrdinalIgnoreCase) || condition.Contains("F", StringComparison.OrdinalIgnoreCase)) return 2;
            if (condition.Contains("Poor", StringComparison.OrdinalIgnoreCase) || condition.Contains("P", StringComparison.OrdinalIgnoreCase)) return 1;
            return 0;
        }
    }
}