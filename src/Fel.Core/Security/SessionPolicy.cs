using System;
using System.Collections.Generic;
using System.Linq;

namespace Fel.Core.Security
{
    // Qué portal es el dueño de la sesión.
    public enum PortalKind
    {
        Tenant,
        Client,
        Superadmin,
        Developer
    }

    // Política de sesión de los portales. La sesión se cierra por inactividad (cada usuario elige cuánto) y, además, tiene un tope absoluto:
    // por más que se renueve, a las 12 horas hay que volver a escribir la contraseña (NIST SP 800-63B, nivel AAL2). La norma pide 30
    // minutos de inactividad, por eso es el valor por defecto.
    public static class SessionPolicy
    {
        public const int DefaultMinutes = 30;
        public const int SuperadminMaxMinutes = 120;
        public static readonly TimeSpan AbsoluteCap = TimeSpan.FromHours(12);

        // Opciones que puede elegir el usuario: una lista cerrada, no un campo libre.
        public static readonly IReadOnlyList<int> AllowedMinutes = new[] { 15, 30, 60, 120, 240, 480 };

        // Claims propios del token de sesión. "stamp" es el sello de seguridad del usuario (cambia al cambiar la contraseña o al cerrar
        // todas las sesiones, y así mueren los tokens anteriores); "sst" es cuándo empezó la sesión y "cap" cuándo llega al tope.
        public const string StampClaim = "stamp";
        public const string SessionStartClaim = "sst";
        public const string CapClaim = "cap";

        public static IReadOnlyList<int> AllowedFor(PortalKind kind) =>
            kind == PortalKind.Superadmin ? AllowedMinutes.Where(m => m <= SuperadminMaxMinutes).ToArray() : AllowedMinutes;

        public static bool IsAllowed(PortalKind kind, int minutes) => AllowedFor(kind).Contains(minutes);

        // Lo guardado puede quedar fuera de lo permitido (por ejemplo, un valor que ya no cabe en ese portal): se lleva al más cercano válido.
        public static int Effective(PortalKind kind, int stored)
        {
            var allowed = AllowedFor(kind);
            if (allowed.Contains(stored)) return stored;
            return stored > allowed.Max() ? allowed.Max() : DefaultMinutes;
        }
    }
}
