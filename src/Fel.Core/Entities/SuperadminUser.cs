using System;

namespace Fel.Core.Entities
{
    public class SuperadminUser : ISessionAccount
    {
        public Guid Id { get; set; }
        // Nombre para mostrar (opcional: si está vacío se muestra el correo).
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        // Sesión del portal: sello de seguridad (ver ISessionAccount) y minutos de inactividad que elige el usuario.
        public Guid SecurityStamp { get; set; } = Guid.NewGuid();
        public int SessionMinutes { get; set; } = Fel.Core.Security.SessionPolicy.DefaultMinutes;
    }
}
