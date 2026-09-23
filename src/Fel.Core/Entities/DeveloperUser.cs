using System;

namespace Fel.Core.Entities
{
    // Usuario del portal de developers, independiente de TenantUser/ClientUser. Dos formas de
    // llegar a existir:
    // 1) Invitado por un Tenant (TenantId con valor): ve las credenciales de prueba de los
    //    Clients de ese Tenant, sin Client propio (ClientId queda null).
    // 2) Registro independiente, sin invitación (TenantId null): se le auto-provisiona su propio
    //    Client de prueba (ClientId con valor) bajo el Tenant "Sandbox" compartido, para que
    //    pueda generar credenciales de prueba y probar la integración sin depender de ningún
    //    Tenant real.
    public class DeveloperUser
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;

        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        public Guid? ClientId { get; set; }
        public Client? Client { get; set; }

        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
    }
}
