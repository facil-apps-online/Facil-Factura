using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Fel.Core.Security;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Fel.Infrastructure.Security
{
    // Revoca tokens: el JWT no se guarda en el servidor, así que para poder invalidarlo lleva el sello de seguridad del usuario, y en cada
    // petición se compara con el sello actual. Cambiar la contraseña, restablecerla o "cerrar sesión en todos los dispositivos" cambia el
    // sello; un usuario desactivado también deja de pasar. Un token sin sello (de antes de existir este control) no vale.
    //
    // El sello actual se guarda en memoria 30 segundos para no consultar la base en cada petición. Al cambiar el sello, la misma API lo
    // actualiza de inmediato en la caché (Refresh); en cualquier otro caso, incluida una desactivación o un cambio hecho desde otra API,
    // la revocación puede tardar hasta esos 30 segundos.
    public sealed class SessionStampValidator
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

        private sealed record Entry(Guid Stamp, bool Active);

        private readonly FelDbContext _dbContext;
        private readonly IMemoryCache _cache;

        public SessionStampValidator(FelDbContext dbContext, IMemoryCache cache)
        {
            _dbContext = dbContext;
            _cache = cache;
        }

        private static string Key(PortalKind kind, Guid id) => $"session-stamp:{kind}:{id}";

        // De quién es el token: cada portal lleva un claim distinto.
        private static (PortalKind Kind, Guid Id)? Identify(ClaimsPrincipal principal)
        {
            Guid? Id(string type) => Guid.TryParse(principal.FindFirst(type)?.Value, out var id) ? id : null;

            if (principal.HasClaim(c => c.Type == "DeveloperId")) return Id("DeveloperId") is Guid d ? (PortalKind.Developer, d) : null;
            if (principal.HasClaim(c => c.Type == "ClientId")) return Id(ClaimTypes.NameIdentifier) is Guid c ? (PortalKind.Client, c) : null;
            if (principal.HasClaim(c => c.Type == "TenantId")) return Id(ClaimTypes.NameIdentifier) is Guid t ? (PortalKind.Tenant, t) : null;
            if (principal.IsInRole("Superadmin")) return Id(ClaimTypes.NameIdentifier) is Guid s ? (PortalKind.Superadmin, s) : null;
            return null;
        }

        public static PortalKind? KindOf(ClaimsPrincipal principal) => Identify(principal)?.Kind;

        public async Task<bool> IsValidAsync(ClaimsPrincipal principal)
        {
            var who = Identify(principal);
            if (who == null) return false;
            if (!Guid.TryParse(principal.FindFirst(SessionPolicy.StampClaim)?.Value, out var tokenStamp)) return false;

            var (kind, id) = who.Value;
            var key = Key(kind, id);
            if (!_cache.TryGetValue(key, out Entry? current))
            {
                current = await LoadAsync(kind, id);
                if (current != null) _cache.Set(key, current, CacheTtl);
            }
            return current != null && current.Active && current.Stamp == tokenStamp;
        }

        // Tras cambiar el sello: que esta API vea el nuevo de inmediato. Se escribe el valor nuevo en vez de borrar la entrada, así una
        // petición que llegue entre el cambio y el guardado no vuelve a cachear el sello viejo leyéndolo de la base.
        public void Refresh(PortalKind kind, Guid id, Guid stamp) => _cache.Set(Key(kind, id), new Entry(stamp, true), CacheTtl);

        private async Task<Entry?> LoadAsync(PortalKind kind, Guid id) => kind switch
        {
            PortalKind.Tenant => await _dbContext.TenantUsers.AsNoTracking().Where(u => u.Id == id).Select(u => new Entry(u.SecurityStamp, u.IsActive)).FirstOrDefaultAsync(),
            PortalKind.Client => await _dbContext.ClientUsers.AsNoTracking().Where(u => u.Id == id).Select(u => new Entry(u.SecurityStamp, u.IsActive)).FirstOrDefaultAsync(),
            PortalKind.Developer => await _dbContext.DeveloperUsers.AsNoTracking().Where(u => u.Id == id).Select(u => new Entry(u.SecurityStamp, u.IsActive)).FirstOrDefaultAsync(),
            PortalKind.Superadmin => await _dbContext.SuperadminUsers.AsNoTracking().Where(u => u.Id == id).Select(u => new Entry(u.SecurityStamp, true)).FirstOrDefaultAsync(),
            _ => null
        };
    }
}
