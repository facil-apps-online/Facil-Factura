using System;

namespace Fel.Core.Entities
{
    public class ProductTax
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public Product? Product { get; set; }

        public string TaxCategory { get; set; } = "01"; // DIAN/Dataico: 01=IVA, 02=ICA, 03=INC, ...
        public decimal Rate { get; set; }
    }
}
