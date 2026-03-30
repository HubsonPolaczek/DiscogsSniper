using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscogsSniper.Models
{
    public class Label
    {
        // To będzie ID z Discogsa (np. 35308 dla Tonpress)
        public int Id { get; set; }

        // Nazwa wytwórni
        public string Name { get; set; }

        // Przełącznik: czy program ma teraz szukać płyt z tej wytwórni?
        public bool IsActive { get; set; } = true;
    }
}