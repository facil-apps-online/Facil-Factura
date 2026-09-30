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

        // El token en crudo, cifrado (no en texto plano) — permite reenviar exactamente el mismo
        // enlace mientras siga vigente, en vez de emitir uno nuevo en cada clic de "reenviar" (que
        // dejaría varios enlaces simultáneamente válidos para el mismo usuario). TokenHash sigue
        // siendo la fuente de verdad para validar/consumir un token; este campo solo existe para
        // poder recuperar el valor original y reenviarlo.
        public string? EncryptedToken { get; set; }

        public DateTime ExpiresAt { get; set; }
        public DateTime? UsedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
