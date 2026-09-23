using System;

namespace Fel.Core.Entities
{
    // Un solo token sirve para "olvidé mi contraseña" e invitación (crear la contraseña por
    // primera vez): en ambos casos es un enlace de un solo uso con vencimiento que termina en
    // establecer PasswordHash del usuario referenciado.
    public class PasswordResetToken
    {
        public Guid Id { get; set; }
        public PortalUserType UserType { get; set; }
        public Guid UserId { get; set; }
        public string TokenHash { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public DateTime? UsedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
