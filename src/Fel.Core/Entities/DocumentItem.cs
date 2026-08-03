using System;

namespace Fel.Core.Entities
{
    public class DocumentItem
    {
        public Guid Id { get; set; }
        
        public Guid DocumentId { get; set; }
        public Document Document { get; set; } = null!;

        public Guid? ProductId { get; set; }
        public Product? Product { get; set; }

        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TaxRate { get; set; }
        
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }

        public string SectorExtensionData { get; set; } = "{}";
    }
}
