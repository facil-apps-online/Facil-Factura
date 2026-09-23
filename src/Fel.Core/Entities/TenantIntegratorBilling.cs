using System;

namespace Fel.Core.Entities
{
    // Override opcional de cómo Superadmin le cobra a un Tenant el uso de UN integrador en
    // particular. Si no existe una fila para un (Tenant, Integrator), se usa el default global del
    // Tenant (Tenant.BillingMode + TenantUserPricing) — igual que hoy, sin este mecanismo. Existe
    // para poder tener, por ejemplo, DIAN directa por documento (vía TariffTier) y Dataico por
    // usuario para el mismo Tenant, o viceversa, sin tocar el resto de integradores.
    public class TenantIntegratorBilling
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        public Guid IntegratorId { get; set; }
        public Integrator? Integrator { get; set; }

        public TenantBillingMode Mode { get; set; } = TenantBillingMode.PerDocument;

        // Solo aplica cuando Mode == PerUser; en PerDocument la tarifa sale de TariffTier
        // (filtrado por este mismo IntegratorId).
        public decimal PricePerUser { get; set; }
    }
}
