using System;

namespace Fel.Core.Entities
{
    public class TenantPricing
    {
        public Guid Id { get; set; }
        
        public Guid TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        public Guid DocumentTypeId { get; set; }
        public DocumentType? DocumentType { get; set; }

        public decimal PricePerDocument { get; set; }
        public string Currency { get; set; } = "COP";
        
        public DateTime UpdatedAt { get; set; }
    }
}
