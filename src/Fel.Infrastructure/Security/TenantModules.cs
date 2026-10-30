using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Security
{
    public sealed record TenantModuleSet(bool Invoicing, bool Certificates)
    {
        // Un tenant que no existe (o aún no se ve en la foto) se trata como los de siempre: con facturación y certificados.
        public static readonly TenantModuleSet Default = new(true, true);
    }

    // Módulos activos de un tenant (ver Tenant.InvoicingModuleEnabled y Tenant.CertificatesModuleEnabled). Igual que TenantBlocks, se guarda
    // una foto de todos los tenants en memoria 30 segundos: quien cambia los módulos la refresca de inmediato en su API (Invalidate) y las demás
    // APIs lo ven en ese lapso.
    //
    // Sin facturación, los clientes del tenant son solo titulares de certificados: no entran al portal de clientes ni usan la API de integración.
    public static class TenantModules
    {
        public const string ClientMessage = "Esta cuenta no tiene facturación electrónica. Comuníquese con su proveedor.";

        private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

        private static readonly SemaphoreSlim Gate = new(1, 1);
        private static Dictionary<Guid, TenantModuleSet>? _snapshot;
        private static DateTime _loadedAtUtc;

        public static void Invalidate() => _snapshot = null;

        public static async Task<TenantModuleSet> GetAsync(FelDbContext dbContext, Guid tenantId)
        {
            var map = await LoadAsync(dbContext);
            return map.TryGetValue(tenantId, out var modules) ? modules : TenantModuleSet.Default;
        }

        private static async Task<Dictionary<Guid, TenantModuleSet>> LoadAsync(FelDbContext dbContext)
        {
            var snapshot = _snapshot;
            if (snapshot != null && DateTime.UtcNow - _loadedAtUtc < Ttl) return snapshot;

            await Gate.WaitAsync();
            try
            {
                snapshot = _snapshot;
                if (snapshot != null && DateTime.UtcNow - _loadedAtUtc < Ttl) return snapshot;

                snapshot = await dbContext.Tenants.AsNoTracking()
                    .Select(t => new { t.Id, t.InvoicingModuleEnabled, t.CertificatesModuleEnabled })
                    .ToDictionaryAsync(t => t.Id, t => new TenantModuleSet(t.InvoicingModuleEnabled, t.CertificatesModuleEnabled));
                _snapshot = snapshot;
                _loadedAtUtc = DateTime.UtcNow;
                return snapshot;
            }
            finally
            {
                Gate.Release();
            }
        }
    }
}
