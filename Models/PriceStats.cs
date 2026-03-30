using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscogsSniper.Models
{
    public class PriceStats
    {
        // Używamy "decimal?" (ze znakiem zapytania), co oznacza, 
        // że wartość może być "nullem" (pusta), jeśli płyta nigdy wcześniej się nie sprzedała.
        public decimal? Lowest { get; set; }
        public decimal? Median { get; set; }
        public decimal? Highest { get; set; }
    }
}