using System;
using System.Collections.Generic;

namespace Fel.Core.Interfaces
{
    public sealed record SessionTokenResult(string Token, DateTime ExpiresAtUtc);

    // Emite los JWT de sesión de los portales propios (Tenant/Client/Developer/Superadmin), centralizado en vez de repetido en cada
    // proyecto. El token dura lo que el usuario eligió de inactividad (SessionPolicy) y nunca pasa del tope absoluto de la sesión; lleva el
    // sello de seguridad del usuario para poder revocarlo.
    public interface ISessionTokenService
    {
        // sessionStartUtc: cuándo empezó la sesión (el tope de 12 horas se cuenta desde ahí, no desde esta emisión).
        SessionTokenResult IssueSession(IEnumerable<(string Type, string Value)> claims, Guid stamp, int sessionMinutes, DateTime sessionStartUtc);
    }
}
