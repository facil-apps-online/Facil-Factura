using System;
using System.Collections.Generic;

namespace Fel.Core.Interfaces
{
    // Emite y valida los JWT de sesión de los portales propios (Tenant/Client/Developer) — el
    // mismo rol que ya cumplía el token de Superadmin, pero centralizado en vez de repetido en
    // cada proyecto. Antes, estos 3 portales usaban directamente el Id (GUID) de la entidad como
    // "token" sin firmar ni expirar — cualquiera que conociera/filtrara ese GUID podía hacerse
    // pasar por ese tenant/cliente/developer sin contraseña.
    public interface ISessionTokenService
    {
        string GenerateToken(IEnumerable<(string Type, string Value)> claims, TimeSpan validity);
    }
}
