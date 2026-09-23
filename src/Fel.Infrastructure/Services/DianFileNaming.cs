using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Services
{
    // Construye el nombre de archivo XML/ZIP exactamente como lo exige el anexo técnico de la DIAN
    // (numerales 6.5.7 y 6.5.8): "{prefijo}{NIT a 10 dígitos}{ppp}{aa}{consecutivo hex a 8 dígitos}".
    // Antes se armaba con el prefijo de la resolución y el número de factura en decimal — un formato
    // que no corresponde al exigido y que la DIAN no logra parsear (falla con una excepción no
    // manejada en su propio procesamiento del batch, sin devolver un error de validación legible).
    public static class DianFileNaming
    {
        // "ppp" para Software Propio, según el numeral 6.5.7 — el único modo de operación que
        // soportamos hoy. Si en el futuro se soporta un Proveedor Tecnológico habría que leer este
        // código de la DIAN en vez de asumirlo fijo.
        private const string SoftwarePropioCode = "000";

        public const string FacturaVenta = "fv";
        public const string NotaCredito = "nc";
        public const string NotaDebito = "nd";

        // Mismo patrón optimista de ResolutionNumbering.ClaimNextNumberAsync — el consecutivo se
        // reinicia a 1 cada 1 de enero (numeral 6.5.7, última nota).
        public static async Task<int> ClaimNextSequenceAsync(FelDbContext dbContext, Guid clientId)
        {
            var currentYear = DateTime.UtcNow.Year;
            while (true)
            {
                var client = await dbContext.Clients.AsNoTracking()
                    .Where(c => c.Id == clientId)
                    .Select(c => new { c.DianFileSequence, c.DianFileSequenceYear })
                    .FirstOrDefaultAsync();

                if (client == null)
                    throw new InvalidOperationException("No se encontró el cliente al asignar el consecutivo de archivo DIAN.");

                var current = client.DianFileSequenceYear == currentYear ? client.DianFileSequence : 0;
                var next = current + 1;

                var affected = await dbContext.Clients
                    .Where(c => c.Id == clientId && c.DianFileSequence == client.DianFileSequence && c.DianFileSequenceYear == client.DianFileSequenceYear)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(c => c.DianFileSequence, next)
                        .SetProperty(c => c.DianFileSequenceYear, currentYear));

                if (affected > 0) return next;
            }
        }

        // taxId: NIT sin DV (tal como se guarda en Client.TaxId/Resolution). sequence: el valor que
        // devuelve ClaimNextSequenceAsync.
        public static string BuildFileName(string docTypePrefix, string taxId, int sequence)
        {
            var nit10 = taxId.PadLeft(10, '0');
            var yy = (DateTime.UtcNow.Year % 100).ToString("D2");
            var hex8 = sequence.ToString("X8");
            return $"{docTypePrefix}{nit10}{SoftwarePropioCode}{yy}{hex8}.xml";
        }
    }
}
