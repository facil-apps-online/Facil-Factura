using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Services
{
    // Asigna el consecutivo real de una Resolución al momento de publicar un documento — antes ese
    // número se rellenaba con DateTime.UtcNow.Ticks cuando el borrador no lo traía, lo que la DIAN
    // (y Dataico) rechaza porque no corresponde a la numeración autorizada.
    public static class ResolutionNumbering
    {
        // ExecuteUpdateAsync filtrado por el valor de NextNumber leído hace un instante: si otra
        // petición ya lo cambió mientras tanto, la actualización afecta 0 filas y se reintenta con
        // el valor fresco — evita que dos publicaciones simultáneas reciban el mismo consecutivo
        // sin necesitar bloqueos ni SQL crudo.
        public static async Task<long> ClaimNextNumberAsync(FelDbContext dbContext, Guid resolutionId)
        {
            while (true)
            {
                var resolution = await dbContext.Resolutions.AsNoTracking()
                    .Where(r => r.Id == resolutionId)
                    .Select(r => new { r.NextNumber, r.NumberStart })
                    .FirstOrDefaultAsync();

                if (resolution == null)
                {
                    throw new InvalidOperationException("No se encontró la resolución al asignar el consecutivo.");
                }

                var current = resolution.NextNumber ?? resolution.NumberStart;
                var next = current + 1;

                var affected = await dbContext.Resolutions
                    .Where(r => r.Id == resolutionId && r.NextNumber == resolution.NextNumber)
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.NextNumber, next));

                if (affected > 0) return current;
            }
        }
    }
}
