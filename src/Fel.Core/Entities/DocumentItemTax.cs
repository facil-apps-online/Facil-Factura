using System;

namespace Fel.Core.Entities
{
    // Impuesto adicional de una línea, distinto del IVA y de las retenciones.
    // Se congela en la factura para que cambios posteriores en el producto no alteren documentos existentes.
    public class DocumentItemTax
    {
        public Guid Id { get; set; }
        public Guid DocumentItemId { get; set; }
        public DocumentItem? DocumentItem { get; set; }

        public string TaxCategory { get; set; } = string.Empty;
        public decimal Rate { get; set; }
        public decimal BaseAmount { get; set; }
        public decimal Amount { get; set; }
    }
}
