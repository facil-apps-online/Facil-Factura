using System;

namespace Fel.Core.Entities
{
    // Allowlist de qué integradores puede ver/usar un Tenant. Sin fila aquí, el Tenant no puede
    // seleccionar ese Integrator para ninguno de sus Clients ni sabe que existe (el portal de
    // Tenant filtra su catálogo de integradores por esto). Separado a propósito de
    // TenantIntegratorBilling, que es solo cómo se le cobra — un Tenant puede tener acceso sin
    // override de facturación, y viceversa no debería pasar pero son conceptos distintos.
    public class TenantEnabledIntegrator
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        public Guid IntegratorId { get; set; }
        public Integrator? Integrator { get; set; }
    }
}
