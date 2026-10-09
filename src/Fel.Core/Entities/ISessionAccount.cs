using System;

namespace Fel.Core.Entities
{
    // Lo que tienen en común las cuentas que inician sesión en los portales (usuarios del tenant, del cliente, developers y superadmin) para
    // el perfil y el control de sesión.
    public interface ISessionAccount
    {
        Guid Id { get; }
        // Nombre para mostrar; si está vacío se muestra el correo.
        string Name { get; set; }
        string Email { get; }
        string PasswordHash { get; set; }
        // Sello de seguridad: va en cada token y, si cambia, todos los tokens anteriores dejan de valer.
        Guid SecurityStamp { get; set; }
        // Minutos de inactividad antes de cerrar la sesión (ver SessionPolicy).
        int SessionMinutes { get; set; }
    }
}
