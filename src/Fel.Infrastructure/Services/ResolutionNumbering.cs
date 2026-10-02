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
    // La resolución llegó a su último número autorizado: no se asigna ni se envía el documento (la
    // DIAN/Dataico lo rechazarían por numeración agotada). Hereda de NotSupportedException para que
    // los controladores que ya traducen esa excepción a 400 (InvoiceController.Publish) devuelvan el
    // mensaje tal cual, sin tocarlos.
    public class ResolutionExhaustedException : NotSupportedException
    {
        public ResolutionExhaustedException(string message) : base(message) { }
    }

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

        // Mismo patrón optimista que ClaimNextNumberAsync, pero sobre el consecutivo interno de
        // Client (NextCreditNoteNumber/NextDebitNoteNumber) en vez del de una Resolución — las
        // Notas Crédito/Débito no tienen rango autorizado propio ante la DIAN, así que este
        // consecutivo no compite con el de Factura.
        public static async Task<long> ClaimNextCreditNoteNumberAsync(FelDbContext dbContext, Guid clientId)
        {
            while (true)
            {
                var client = await dbContext.Clients.AsNoTracking()
                    .Where(c => c.Id == clientId)
                    .Select(c => new { c.NextCreditNoteNumber })
                    .FirstOrDefaultAsync();

                if (client == null)
                {
                    throw new InvalidOperationException("No se encontró el Client al asignar el consecutivo de la nota crédito.");
                }

                var current = client.NextCreditNoteNumber ?? 1;
                var next = current + 1;

                var affected = await dbContext.Clients
                    .Where(c => c.Id == clientId && c.NextCreditNoteNumber == client.NextCreditNoteNumber)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.NextCreditNoteNumber, next));

                if (affected > 0) return current;
            }
        }

        public static async Task<long> ClaimNextDebitNoteNumberAsync(FelDbContext dbContext, Guid clientId)
        {
            while (true)
            {
                var client = await dbContext.Clients.AsNoTracking()
                    .Where(c => c.Id == clientId)
                    .Select(c => new { c.NextDebitNoteNumber })
                    .FirstOrDefaultAsync();

                if (client == null)
                {
                    throw new InvalidOperationException("No se encontró el Client al asignar el consecutivo de la nota débito.");
                }

                var current = client.NextDebitNoteNumber ?? 1;
                var next = current + 1;

                var affected = await dbContext.Clients
                    .Where(c => c.Id == clientId && c.NextDebitNoteNumber == client.NextDebitNoteNumber)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.NextDebitNoteNumber, next));

                if (affected > 0) return current;
            }
        }

        // Notas de Ajuste del Documento Soporte (DS-AJUSTE): mismo patrón, consecutivo propio del Client.
        public static async Task<long> ClaimNextSupportAdjustmentNumberAsync(FelDbContext dbContext, Guid clientId)
        {
            while (true)
            {
                var client = await dbContext.Clients.AsNoTracking()
                    .Where(c => c.Id == clientId)
                    .Select(c => new { c.NextSupportAdjustmentNumber })
                    .FirstOrDefaultAsync();

                if (client == null)
                {
                    throw new InvalidOperationException("No se encontró el Client al asignar el consecutivo de la nota de ajuste.");
                }

                var current = client.NextSupportAdjustmentNumber ?? 1;
                var next = current + 1;

                var affected = await dbContext.Clients
                    .Where(c => c.Id == clientId && c.NextSupportAdjustmentNumber == client.NextSupportAdjustmentNumber)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.NextSupportAdjustmentNumber, next));

                if (affected > 0) return current;
            }
        }
    }
}
