using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Services
{
    // Asigna el consecutivo real de una Resolución al momento de publicar un documento — antes ese
    // número se rellenaba con DateTime.UtcNow.Ticks cuando el borrador no lo traía, lo que la DIAN
    // (y Dataico) rechaza porque no corresponde a la numeración autorizada.
    // La resolución llegó a su último número autorizado: no se asigna ni se envía el documento (la
    // DIAN/Dataico lo rechazarían por numeración agotada). Hereda de NotSupportedException para que
    // los controladores que ya traducen esa excepción a 400 (InvoiceController.Publish) devuelvan el
    // mensaje tal cual, sin tocarlos.
    public class ResolutionExhaustedException : NotSupportedException
    {
        public ResolutionExhaustedException(string message) : base(message) { }
    }

    // Consecutivo asignado a una nota y el prefijo propio de su numeración (null = usa el de su resolución).
    public sealed record NoteClaim(long Number, string? Prefix);

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
                    .Select(r => new { r.NextNumber, r.NumberStart, r.NumberEnd, r.Prefix, r.DocumentType })
                    .FirstOrDefaultAsync();

                if (resolution == null)
                {
                    throw new InvalidOperationException("No se encontró la resolución al asignar el consecutivo.");
                }

                var current = resolution.NextNumber ?? resolution.NumberStart;

                // Validar el rango antes de reclamar: un número fuera de lo autorizado no se consume.
                // Nómina (NE) no tiene rango DIAN real (el consecutivo lo lleva libremente el
                // empleador) y NumberEnd = 0 significa "sin tope".
                if (resolution.DocumentType != "NE" && resolution.NumberEnd > 0 && current > resolution.NumberEnd)
                {
                    throw new ResolutionExhaustedException(
                        $"La resolución {resolution.Prefix} llegó a su último número autorizado ({resolution.NumberEnd}). " +
                        "Solicita una nueva resolución a la DIAN o usa otra resolución activa.");
                }

                var next = current + 1;

                var affected = await dbContext.Resolutions
                    .Where(r => r.Id == resolutionId && r.NextNumber == resolution.NextNumber)
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.NextNumber, next));

                if (affected > 0) return current;
            }
        }

        // Consecutivo que le toca a la siguiente emisión, SIN reservarlo: la nómina lo envía a Dataico y solo si el envío es aprobado se
        // consume con CommitNumberAsync. Si Dataico rechaza, no queda nada guardado y el siguiente intento usa el mismo número.
        public static async Task<long> PeekNextNumberAsync(FelDbContext dbContext, Guid resolutionId)
        {
            var resolution = await dbContext.Resolutions.AsNoTracking()
                .Where(r => r.Id == resolutionId)
                .Select(r => new { r.NextNumber, r.NumberStart })
                .FirstOrDefaultAsync();

            if (resolution == null)
            {
                throw new InvalidOperationException("No se encontró la resolución al consultar el consecutivo.");
            }

            return resolution.NextNumber ?? resolution.NumberStart;
        }

        // Consume el número tras una emisión aprobada: el siguiente será number + 1. Solo avanza (nunca retrocede), así que si el
        // contador ya estaba más adelante, o se repite la llamada, no hace daño.
        public static Task<int> CommitNumberAsync(FelDbContext dbContext, Guid resolutionId, long number) =>
            dbContext.Resolutions
                .Where(r => r.Id == resolutionId && (r.NextNumber == null || r.NextNumber <= number))
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.NextNumber, number + 1));

        // Numeración que le corresponde a una nota: la propia de la sucursal si la tiene y, si no, la compartida del Client.
        // Las Notas Crédito/Débito y de Ajuste no tienen rango autorizado propio ante la DIAN, así que su consecutivo es
        // interno y no compite con el de Factura.
        //
        // Las resoluciones del mismo Client con el mismo prefijo (una renovación conserva el prefijo) comparten UN solo contador de notas: ante la DIAN el
        // prefijo y el número de una nota deben ser únicos, así que si cada resolución llevara el suyo la segunda arrancaría en 1 y repetiría números.
        // Entre los contadores de esas resoluciones manda el más adelantado, para no volver a emitir un número ya usado.
        private static IQueryable<NoteNumbering> EffectiveNumbering(FelDbContext dbContext, Guid clientId, Guid? branchId, NoteKind kind, IReadOnlyCollection<Guid>? resolutionIds = null) =>
            dbContext.NoteNumberings.AsNoTracking()
                .Where(n => n.ClientId == clientId && n.Kind == kind
                    && (resolutionIds == null ? n.ResolutionId == null : n.ResolutionId != null && resolutionIds.Contains(n.ResolutionId.Value))
                    && (n.BranchId == branchId || n.BranchId == null))
                .OrderByDescending(n => n.BranchId != null)
                .ThenByDescending(n => n.NextNumber ?? 0);

        // Ids de las resoluciones del Client que comparten el prefijo de la dada (incluida ella); null si no se indica resolución.
        public static async Task<List<Guid>?> SamePrefixResolutionIdsAsync(FelDbContext dbContext, Guid clientId, Guid? resolutionId)
        {
            if (resolutionId == null) return null;
            var prefix = await dbContext.Resolutions.AsNoTracking().Where(r => r.Id == resolutionId).Select(r => r.Prefix).FirstOrDefaultAsync();
            if (prefix == null) return new List<Guid> { resolutionId.Value };
            var ids = await dbContext.Resolutions.AsNoTracking()
                .Where(r => r.ClientId == clientId && r.DocumentType == "FE" && r.Prefix == prefix).Select(r => r.Id).ToListAsync();
            if (!ids.Contains(resolutionId.Value)) ids.Add(resolutionId.Value);
            return ids;
        }

        // Prefijo propio de la numeración de la nota (null = el documento usa el de su resolución).
        public static async Task<string?> GetNotePrefixAsync(FelDbContext dbContext, Guid clientId, Guid? branchId, NoteKind kind, Guid? resolutionId = null)
        {
            var prefix = await EffectiveNumbering(dbContext, clientId, branchId, kind, await SamePrefixResolutionIdsAsync(dbContext, clientId, resolutionId)).Select(n => n.Prefix).FirstOrDefaultAsync();
            return string.IsNullOrWhiteSpace(prefix) ? null : prefix;
        }

        // Mismo patrón optimista que ClaimNextNumberAsync, pero sobre el contador de NoteNumbering.
        public static async Task<NoteClaim> ClaimNextNoteAsync(FelDbContext dbContext, Guid clientId, Guid? branchId, NoteKind kind, Guid? resolutionId = null)
        {
            var resolutionIds = await SamePrefixResolutionIdsAsync(dbContext, clientId, resolutionId);
            while (true)
            {
                var row = await EffectiveNumbering(dbContext, clientId, branchId, kind, resolutionIds)
                    .Select(n => new { n.Id, n.Prefix, n.NextNumber })
                    .FirstOrDefaultAsync();

                if (row == null)
                {
                    await CreateSharedNumberingAsync(dbContext, clientId, kind, resolutionId);
                    continue;
                }

                var current = row.NextNumber ?? 1;
                var next = current + 1;

                var affected = await dbContext.NoteNumberings
                    .Where(n => n.Id == row.Id && n.NextNumber == row.NextNumber)
                    .ExecuteUpdateAsync(s => s.SetProperty(n => n.NextNumber, next));

                if (affected > 0) return new NoteClaim(current, string.IsNullOrWhiteSpace(row.Prefix) ? null : row.Prefix);
            }
        }

        // Por si a un Client le falta su contador compartido. Va por SQL directo para no guardar de paso los cambios
        // pendientes del contexto (el documento que se está publicando); si dos peticiones lo crean a la vez, el índice
        // único deja pasar una y la otra solo vuelve a leer.
        private static async Task CreateSharedNumberingAsync(FelDbContext dbContext, Guid clientId, NoteKind kind, Guid? resolutionId = null)
        {
            var kindValue = (int)kind;
            try
            {
                await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
                    INSERT INTO NoteNumberings (Id, ClientId, BranchId, ResolutionId, Kind, Prefix, NextNumber)
                    SELECT {Guid.NewGuid()}, {clientId}, NULL, {resolutionId}, {kindValue}, NULL, NULL
                    WHERE NOT EXISTS (SELECT 1 FROM NoteNumberings WHERE ClientId = {clientId} AND BranchId IS NULL AND ResolutionId = {resolutionId} AND Kind = {kindValue})");
            }
            catch (Exception)
            {
                // Si falló porque otra petición la creó en el mismo instante, basta con volver a leer; si no, es un error real.
                var exists = await dbContext.NoteNumberings.AsNoTracking()
                    .AnyAsync(n => n.ClientId == clientId && n.BranchId == null && n.ResolutionId == resolutionId && n.Kind == kind);
                if (!exists) throw;
            }
        }
    }
}
