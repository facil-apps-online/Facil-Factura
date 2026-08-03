using System;

namespace Fel.Core.Entities
{
    public class Product
    {
        public Guid Id { get; set; }
        public Guid ClientId { get; set; }
        public Client Client { get; set; } = null!;

        public string Code { get; set; } = string.Empty; // SKU o Referencia interna
        public string StandardCode { get; set; } = string.Empty; // Ej: UNSPSC (Obligatorio en algunos sectores)
        public string Name { get; set; } = string.Empty;
        
        public decimal UnitPrice { get; set; }
        public decimal TaxRate { get; set; } // Porcentaje de IVA, ej: 19.00
        public string UnitOfMeasure { get; set; } = "94"; // Unidad por defecto según DIAN (94 = Unidad)

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
