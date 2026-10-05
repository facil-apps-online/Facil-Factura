using System;
using System.Collections.Generic;

namespace Fel.Core.Entities
{
    public class Product
    {
        public Guid Id { get; set; }
        public Guid ClientId { get; set; }
        public Client? Client { get; set; }

        public ProductScope Scope { get; set; } = ProductScope.Invoice;

        public string Code { get; set; } = string.Empty; // SKU o Referencia interna
        public string StandardCode { get; set; } = string.Empty; // Ej: UNSPSC (Obligatorio en algunos sectores)
        public string Name { get; set; } = string.Empty;

        public decimal UnitPrice { get; set; }

        // FK al catálogo global (ver UnitOfMeasure.cs) — default = fila semilla "Unidad" (94/EA).
        public Guid UnitOfMeasureId { get; set; } = Fel.Core.Entities.UnitOfMeasure.DefaultUnidadId;
        public UnitOfMeasure? UnitOfMeasure { get; set; }

        public IvaTreatment IvaTreatment { get; set; } = IvaTreatment.Gravado;
        public decimal IvaRate { get; set; } = 19; // Solo aplica cuando IvaTreatment = Gravado

        // Impuestos adicionales al IVA (ej. INC, ICA) — el IVA se maneja aparte vía IvaTreatment/IvaRate
        public ICollection<ProductTax> Taxes { get; set; } = new List<ProductTax>();

        // Ya no tiene ningún efecto: la retención se elige 100% manual por línea de factura (ver
        // InvoicesPage.tsx), no por producto. Se conserva sin usar por si se retoma la resolución
        // automática por concepto de producto más adelante.
        public string? RetentionGroupKey { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
