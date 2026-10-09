using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Core.Security;
using Microsoft.Extensions.Caching.Memory;

namespace Fel.Infrastructure.Security
{
    // Reglas de la cuenta y de la sesión de un usuario de portal (perfil, contraseña, duración de la sesión, renovación). No toca la base: el
    // controlador carga la cuenta, llama aquí y guarda. Así las cuatro APIs (tenant, cliente, developers y superadmin) aplican exactamente
    // las mismas reglas.
    public sealed class AccountSessionService
    {
        private const int MaxNameLength = 150;
        public const string WrongCurrentPassword = "La contraseña actual no es correcta.";
        // Contraseñas equivocadas seguidas (con una sesión abierta) antes de cortar todas las sesiones de la cuenta.
        private const int MaxPasswordFailures = 5;
        private static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(15);
        private static readonly string[] OwnClaims = { SessionPolicy.StampClaim, SessionPolicy.SessionStartClaim, SessionPolicy.CapClaim, "exp", "nbf", "iat" };

        private readonly ISessionTokenService _tokens;
        private readonly SessionStampValidator _stamps;
        private readonly IMemoryCache _cache;

        private sealed class Failures { public int Count; }

        public AccountSessionService(ISessionTokenService tokens, SessionStampValidator stamps, IMemoryCache cache)
        {
            _tokens = tokens;
            _stamps = stamps;
            _cache = cache;
        }

        private static string FailuresKey(PortalKind kind, Guid id) => $"password-failures:{kind}:{id}";

        // Anota una contraseña equivocada en una operación que ya exige sesión (confirmar o cambiar la contraseña). Devuelve true si con este
        // fallo se llegó al límite: ahí el controlador corta todas las sesiones, porque quien está probando contraseñas con un token robado
        // no debe poder seguir. El conteo se reinicia a los 15 minutos del primer fallo.
        public bool RegisterPasswordFailure(PortalKind kind, Guid id)
        {
            var failures = _cache.GetOrCreate(FailuresKey(kind, id), entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = FailureWindow;
                return new Failures();
            })!;
            return Interlocked.Increment(ref failures.Count) >= MaxPasswordFailures;
        }

        public void ClearPasswordFailures(PortalKind kind, Guid id) => _cache.Remove(FailuresKey(kind, id));

        public static string DisplayName(ISessionAccount account) =>
            string.IsNullOrWhiteSpace(account.Name) ? account.Email : account.Name.Trim();

        // Cuándo empezó la sesión del token actual (null si el token no lo trae).
        public static DateTime? SessionStart(ClaimsPrincipal principal) =>
            long.TryParse(principal.FindFirst(SessionPolicy.SessionStartClaim)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime
                : null;

        // Los claims del portal (cliente, tenant, rol...) pasan tal cual al token nuevo; los de sesión los recalcula el emisor.
        private static IEnumerable<(string Type, string Value)> CarryOver(ClaimsPrincipal principal) =>
            principal.Claims.Where(c => !OwnClaims.Contains(c.Type)).Select(c => (c.Type, c.Value));

        // Inicio de sesión (o cambio de tenant): sesión nueva, o la misma si ya había una (start).
        public SessionTokenResult Issue(PortalKind kind, ISessionAccount account, IEnumerable<(string Type, string Value)> claims, DateTime? start = null) =>
            _tokens.IssueSession(claims, account.SecurityStamp, SessionPolicy.Effective(kind, account.SessionMinutes), start ?? DateTime.UtcNow);

        // Renueva un token todavía válido. Null si la sesión ya llegó a su tope absoluto: ahí hay que volver a escribir la contraseña.
        public SessionTokenResult? Renew(PortalKind kind, ISessionAccount account, ClaimsPrincipal current)
        {
            var start = SessionStart(current);
            if (start == null || DateTime.UtcNow >= start.Value + SessionPolicy.AbsoluteCap) return null;
            return Issue(kind, account, CarryOver(current), start);
        }

        // Contraseña confirmada: la sesión se reinicia (el tope vuelve a contar desde ahora). El controlador ya verificó la contraseña.
        public SessionTokenResult Reauthenticated(PortalKind kind, ISessionAccount account, ClaimsPrincipal current) =>
            Issue(kind, account, CarryOver(current), DateTime.UtcNow);

        // Cambia el sello: todos los tokens emitidos antes dejan de valer (esta API lo ve de inmediato, sin esperar a que se guarde).
        public void RotateStamp(PortalKind kind, ISessionAccount account)
        {
            account.SecurityStamp = Guid.NewGuid();
            _stamps.Refresh(kind, account.Id, account.SecurityStamp);
            ClearPasswordFailures(kind, account.Id);
        }

        public string? SetSessionMinutes(PortalKind kind, ISessionAccount account, int minutes)
        {
            if (!SessionPolicy.IsAllowed(kind, minutes))
                return $"La duración debe ser una de: {string.Join(", ", SessionPolicy.AllowedFor(kind).Select(m => m + " min"))}.";
            account.SessionMinutes = minutes;
            return null;
        }

        public string? SetName(ISessionAccount account, string? name)
        {
            var clean = name?.Trim() ?? string.Empty;
            if (clean.Length > MaxNameLength) return $"El nombre no puede pasar de {MaxNameLength} caracteres.";
            account.Name = clean;
            return null;
        }

        // Cambia la contraseña (pide la actual, aplica la política de siempre y corta las demás sesiones). Devuelve el error o null.
        public string? ChangePassword(PortalKind kind, ISessionAccount account, string? current, string? next, Func<string, string, bool> verify, Func<string, string> hash)
        {
            if (string.IsNullOrEmpty(current) || !verify(current, account.PasswordHash)) return WrongCurrentPassword;
            var policyError = PasswordPolicy.Validate(next);
            if (policyError != null) return policyError;
            if (verify(next!, account.PasswordHash)) return "La nueva contraseña debe ser distinta de la actual.";

            account.PasswordHash = hash(next!);
            RotateStamp(kind, account);
            return null;
        }
    }
}
