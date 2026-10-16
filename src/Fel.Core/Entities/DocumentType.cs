using System;
using System.Collections.Generic;

namespace Fel.Core.Entities
{
    public class DocumentType
    {
        // Tipo de operación "Mandatos" de la DIAN (tabla 13.1.5.1): la factura declara, línea por línea, el mandante por cuenta de quien se vende.
        public const string MandateOperationType = "11";

        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty; // e.g. "DIAN-FE", "DIAN-NC", "RIPS"
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string GoverningEntity { get; set; } = "DIAN"; // DIAN, MINSALUD, UGP...
        public string DianCode { get; set; } = string.Empty;
        public string? OperationType { get; set; }
        public string? CustomizationId { get; set; }
        public bool IsActive { get; set; } = true;

        public ICollection<TenantPricing> Pricings { get; set; } = new List<TenantPricing>();
        public ICollection<Document> Documents { get; set; } = new List<Document>();
    }
}
