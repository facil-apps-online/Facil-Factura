using System;

namespace Fel.Core.Entities
{
    // Registro de cada vez que un usuario del tenant entra al portal de un cliente en modo soporte (para parametrizar o probar en su nombre).
    // Solo guarda quién entró, a qué cliente y cuándo; lo que haga adentro queda en los datos del cliente como cualquier otra operación.
    public class SupportAccess
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid TenantUserId { get; set; }
        // Copia del correo en el momento: el registro debe seguir diciendo quién fue aunque la cuenta cambie o se elimine.
        public string TenantUserEmail { get; set; } = string.Empty;
        public Guid ClientId { get; set; }
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    }
}
