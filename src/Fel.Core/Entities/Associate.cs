using System;

namespace Fel.Core.Entities
{
    // Comercial/asociado del Tenant (reseller) — quien capta o gestiona un Client (emisor). Es un
    // registro informativo (para saber a quién pertenece cada cliente, ej. para reportes o
    // comisiones), no tiene acceso propio a ningún portal.
    public class Associate
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
