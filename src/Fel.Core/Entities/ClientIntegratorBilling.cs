using System;

namespace Fel.Core.Entities
{
    // Override opcional de cómo un Tenant le cobra a su Client el uso de UN integrador en
    // particular. Si no existe una fila para un (Branch, Integrator), se usa el default de
    // Branch.PricePerDocument (modo PerDocument).
    public class ClientIntegratorBilling
    {
        public Guid Id { get; set; }
        public Guid ClientId { get; set; }
        public Client? Client { get; set; }

        // La tarifa por integrador es de una sucursal: cada una puede tener la suya.
        public Guid BranchId { get; set; }
        public Branch? Branch { get; set; }

        public Guid IntegratorId { get; set; }
        public Integrator? Integrator { get; set; }

        public TenantBillingMode Mode { get; set; } = TenantBillingMode.PerDocument;

        public decimal PricePerDocument { get; set; } // Solo aplica en Mode == PerDocument
        public decimal PricePerUser { get; set; }      // Solo aplica en Mode == PerUser
    }
}
