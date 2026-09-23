using System;

namespace Fel.Core.Entities
{
    /// <summary>
    /// Acceso de un usuario del portal de tenants a un tenant concreto.
    /// </summary>
    /// <remarks>
    /// Antes un <see cref="TenantUser"/> pertenecía a un solo tenant a través de su
    /// <c>TenantId</c>, y el alta rechazaba cualquier correo ya registrado. Eso hacía imposible
    /// dos cosas legítimas: que una misma persona administre varios tenants, y que a alguien a
    /// quien se le revocó el acceso en uno se le pueda dar de alta en otro, porque su correo
    /// quedaba ocupado para siempre.
    ///
    /// Con esta tabla la identidad (correo y contraseña, en TenantUser) queda separada de la
    /// pertenencia (una fila por tenant). Revocar es desactivar la asignación, no borrar ni
    /// inhabilitar a la persona; volver a dar acceso es reactivarla.
    ///
    /// <see cref="TenantUser.TenantId"/> se conserva como el tenant por defecto — el que se abre
    /// al iniciar sesión cuando hay varios.
    /// </remarks>
    public class TenantUserAssignment
    {
        public Guid Id { get; set; }

        public Guid TenantUserId { get; set; }
        public TenantUser TenantUser { get; set; } = null!;

        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        /// <summary>
        /// Acceso vigente. Se pone en false al revocar, en vez de borrar la fila, para que quede
        /// el rastro de que esa persona tuvo acceso y cuándo se le quitó.
        /// </summary>
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Cuándo se revocó por última vez. Null si nunca se ha revocado.</summary>
        public DateTime? RevokedAt { get; set; }
    }
}
