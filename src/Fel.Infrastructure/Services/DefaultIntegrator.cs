using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Services
{
    // Con qué integrador nace un cliente nuevo de un tenant: solo puede ser uno de los que el tenant tiene habilitados. Si tiene la emisión directa
    // (NATIVE) arranca ahí, como siempre; si el tenant solo tiene otro (por ejemplo Dataico), arranca con ese, en vez de quedar en uno que no usa
    // y que además le muestra pantallas que no le corresponden.
    public static class DefaultIntegrator
    {
        public static readonly Guid NativeId = Guid.Parse("00000000-0000-0000-0000-000000000101");

        public static async Task<Guid> ForTenantAsync(FelDbContext db, Guid tenantId)
        {
            var enabled = await db.TenantEnabledIntegrators.AsNoTracking()
                .Where(e => e.TenantId == tenantId && e.Integrator!.IsActive)
                .Select(e => new { e.IntegratorId, e.Integrator!.Code })
                .ToListAsync();

            if (enabled.Count == 0 || enabled.Any(e => e.Code == "NATIVE")) return NativeId;
            return enabled.OrderBy(e => e.Code).First().IntegratorId;
        }
    }
}
