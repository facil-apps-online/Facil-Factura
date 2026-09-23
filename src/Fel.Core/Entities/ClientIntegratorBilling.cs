using System;

namespace Fel.Core.Entities
{
    // Override opcional de cómo un Tenant le cobra a su Client el uso de UN integrador en
    // particular. Si no existe una fila para un (Client, Integrator), se usa el default de
    // Client.PricePerDocument (modo PerDocument) — igual que hoy, sin este mecanismo.
    public class ClientIntegratorBilling
    {
        public Guid Id { get; set; }
        public Guid ClientId { get; set; }
        public Client? Client { get; set; }

        public Guid IntegratorId { get; set; }
        public Integrator? Integrator { get; set; }

        public TenantBillingMode Mode { get; set; } = TenantBillingMode.PerDocument;

        public decimal PricePerDocument { get; set; } // Solo aplica en Mode == PerDocument
        public decimal PricePerUser { get; set; }      // Solo aplica en Mode == PerUser
    }
}
