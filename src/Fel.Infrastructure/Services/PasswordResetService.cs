using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Email;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Services
{
    // Un solo mecanismo de token cubre tanto "olvidé mi contraseña" (templateType="password_reset")
    // como invitación a crear la contraseña por primera vez (templateType="invitation"): en ambos
    // casos es un enlace de un solo uso con vencimiento hacia /reset-password?token=...
    public class PasswordResetService
    {
        private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);

        // Fallback cuando el Tenant (o, en el caso de Superadmin, la ausencia de Tenant) no tiene
        // logo propio configurado — nunca se deja el correo sin logo o con una imagen rota.
        private const string DefaultBrandLogoUrl = "https://facil-factura.pro/brand/isotipo-color.png";
        private const string DefaultBrandName = "Facil Factura";

        private readonly FelDbContext _db;
        private readonly IEmailOutbox _emailOutbox;
        private readonly ICryptoService _crypto;

        public PasswordResetService(FelDbContext db, IEmailOutbox emailOutbox, ICryptoService crypto)
        {
            _db = db;
            _emailOutbox = emailOutbox;
            _crypto = crypto;
        }

        // brandLogoUrl/brandName identifican al Tenant dueño de la cuenta (el revendedor cuya
        // marca reconoce el destinatario) — no al Cliente ni al usuario mismo. Si vienen vacíos
        // (Tenant sin logo configurado, o reset de Superadmin sin Tenant) se usa el logo de
        // Facil Factura por defecto.
        public async Task<CoreResult<bool>> RequestAsync(
            PortalUserType userType, Guid userId, string email, string userName,
            string portalBaseUrl, string templateType, Guid? tenantId = null,
            string? brandLogoUrl = null, string? brandName = null, CancellationToken ct = default)
        {
            var resetLink = await GetLinkAsync(userType, userId, portalBaseUrl, ct);
            var (subject, html) = PlatformEmailTemplates.Build(
                templateType, userName, resetLink,
                string.IsNullOrWhiteSpace(brandLogoUrl) ? DefaultBrandLogoUrl : brandLogoUrl,
                string.IsNullOrWhiteSpace(brandName) ? DefaultBrandName : brandName, TokenLifetime);

            // Queda en la bandeja de salida y el worker lo envía por SendPulse. El cuerpo lleva el enlace con el token: se guarda cifrado y se borra al enviarse.
            var row = _emailOutbox.Add(new OutboundEmail
            {
                Kind = EmailKind.Platform,
                TenantId = tenantId,
                RecipientEmail = email,
                Subject = subject,
                BodyHtml = html,
                Sensitive = true
            });
            await _db.SaveChangesAsync(ct);
            await _emailOutbox.NotifyAsync(row);
            return CoreResult<bool>.Ok(true);
        }

        // El enlace de invitación/restablecimiento sin enviarlo por correo, para que quien administra lo copie y lo mande por otro medio (WhatsApp).
        // Reutiliza el enlace vigente y, si ya venció o se usó, genera uno nuevo.
        public async Task<string> GetLinkAsync(PortalUserType userType, Guid userId, string portalBaseUrl, CancellationToken ct = default)
        {
            // Si ya hay un enlace vigente (no vencido, no usado) para este usuario, se reenvía ese
            // mismo — no tiene sentido emitir uno nuevo cada vez que el tenant hace clic en
            // "reenviar": dejaría varios enlaces simultáneamente válidos y, si el destinatario borró
            // el primer correo, el que llega después seguiría siendo el mismo enlace que ya conocía.
            var existing = await _db.PasswordResetTokens
                .Where(t => t.UserType == userType && t.UserId == userId && t.UsedAt == null && t.ExpiresAt > DateTime.UtcNow && t.EncryptedToken != null)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync(ct);

            string rawToken;
            if (existing != null)
            {
                rawToken = _crypto.Decrypt(existing.EncryptedToken!);
            }
            else
            {
                rawToken = GenerateRawToken();
                _db.PasswordResetTokens.Add(new PasswordResetToken
                {
                    Id = Guid.NewGuid(),
                    UserType = userType,
                    UserId = userId,
                    TokenHash = Hash(rawToken),
                    EncryptedToken = _crypto.Encrypt(rawToken),
                    ExpiresAt = DateTime.UtcNow.Add(TokenLifetime),
                    CreatedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync(ct);
            }

            return $"{portalBaseUrl.TrimEnd('/')}/reset-password?token={rawToken}";
        }

        // Devuelve el usuario referenciado si el token es válido, no vencido y no usado, y de paso
        // lo marca como usado (de un solo uso). expectedType evita que un token de Cliente sirva
        // para restablecer, por ejemplo, una cuenta de Tenant.
        public async Task<(PortalUserType UserType, Guid UserId)?> ConsumeAsync(string rawToken, PortalUserType expectedType, CancellationToken ct = default)
        {
            var tokenHash = Hash(rawToken);
            var entry = await _db.PasswordResetTokens
                .Where(t => t.TokenHash == tokenHash && t.UserType == expectedType && t.UsedAt == null && t.ExpiresAt > DateTime.UtcNow)
                .FirstOrDefaultAsync(ct);

            if (entry == null) return null;

            entry.UsedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            return (entry.UserType, entry.UserId);
        }

        private static string GenerateRawToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        private static string Hash(string raw)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToBase64String(bytes);
        }
    }
}
