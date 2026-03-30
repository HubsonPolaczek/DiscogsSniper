using System;

namespace DiscogsSniper.Models
{
    public class Offer
    {
        public long ListingId { get; set; }
        public long ReleaseId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Condition { get; set; } = string.Empty;
        public string SleeveCondition { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal ShippingPrice { get; set; }

        // Suma ceny i wysyłki wyliczana automatycznie
        public decimal TotalPrice => Price + ShippingPrice;

        public string Url { get; set; } = string.Empty;

        // Data znalezienia przez program
        public DateTime DateFound { get; set; } = DateTime.Now;

        // NOWOŚĆ: Prawdziwa data wystawienia płyty przez sprzedawcę
        public DateTime? DateListed { get; set; }

        public bool IsDeal { get; set; } = false; // Czy to potwierdzona okazja?
    }
}