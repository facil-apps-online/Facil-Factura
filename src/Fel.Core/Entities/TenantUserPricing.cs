using System;

namespace Fel.Core.Entities
{
    // Tarifa mensual estándar por usuario (Client activo) que Superadmin le cobra a un
    // Tenant en modo TenantBillingMode.PerUser.
    public class TenantUserPricing
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        public decimal PricePerUser { get; set; }
        public string Currency { get; set; } = "COP";
        public DateTime UpdatedAt { get; set; }
    }
}
