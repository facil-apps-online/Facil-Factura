using System;

namespace Fel.Core.Entities
{
    public class TenantUser : ISessionAccount
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;
        
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }

        // Sesión del portal: sello de seguridad (ver ISessionAccount) y minutos de inactividad que elige el usuario.
        public Guid SecurityStamp { get; set; } = Guid.NewGuid();
        public int SessionMinutes { get; set; } = Fel.Core.Security.SessionPolicy.DefaultMinutes;
    }
}
