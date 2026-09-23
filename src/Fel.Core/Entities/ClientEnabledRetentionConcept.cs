using System;

namespace Fel.Core.Entities
{
    // Qué conceptos de retención en la fuente puede elegir un Client al agregar una retención a una
    // línea de factura. Antes se ofrecía el catálogo completo (42 conceptos, incluida ReteIVA) a
    // cualquier Client; ahora cada Client tiene su propio subconjunto habilitado, administrado por
    // el Tenant, sembrado al crear el Client con los conceptos generales más comunes (Compras,
    // Servicios, Honorarios) — no con ReteIVA, que solo aplica a regímenes específicos.
    public class ClientEnabledRetentionConcept
    {
        public Guid Id { get; set; }
        public Guid ClientId { get; set; }
        public Client? Client { get; set; }

        public Guid RetentionConceptId { get; set; }
        public RetentionConcept? RetentionConcept { get; set; }
    }
}
