using System;

namespace Fel.Core.Entities
{
    public class DocumentRetention
    {
        public Guid Id { get; set; }
        public Guid DocumentItemId { get; set; }
        public DocumentItem? DocumentItem { get; set; }

        public string TaxCategory { get; set; } = "RET_FUENTE"; // Ej: RET_FUENTE, RET_ICA, RET_IVA
        public decimal Rate { get; set; }
        public decimal BaseAmount { get; set; }
        public decimal Amount { get; set; }
    }
}
