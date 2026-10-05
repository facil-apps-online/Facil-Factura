using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Services
{
    // Lectura y edición de los contadores compartidos de notas del Client (las filas de NoteNumbering sin sucursal), que es lo que
    // muestran y editan el portal y el tenant. Las numeraciones propias de una sucursal se gestionan aparte.
    public static class NoteNumberingService
    {
        public sealed record SharedCounters(
            long NextCreditNoteNumber,
            long NextDebitNoteNumber,
            long NextSupportAdjustmentNumber,
            string SupportAdjustmentPrefix);

        public static async Task<SharedCounters> GetSharedAsync(FelDbContext dbContext, Guid clientId)
        {
            var rows = await dbContext.NoteNumberings.AsNoTracking()
                .Where(n => n.ClientId == clientId && n.BranchId == null)
                .ToListAsync();

            long Next(NoteKind kind) => rows.FirstOrDefault(r => r.Kind == kind)?.NextNumber ?? 1;

            return new SharedCounters(
                Next(NoteKind.CreditNote),
                Next(NoteKind.DebitNote),
                Next(NoteKind.SupportAdjustment),
                rows.FirstOrDefault(r => r.Kind == NoteKind.SupportAdjustment)?.Prefix ?? string.Empty);
        }

        // Aplica los cambios sobre las filas compartidas (las crea si faltan); quien llama guarda con SaveChangesAsync.
        // adjustmentPrefix: null = no cambiar, cadena vacía = quitar el prefijo. Devuelve un mensaje de error si el prefijo ya lo
        // usa la numeración propia de una sucursal (el prefijo es único por Client y tipo).
        public static async Task<string?> UpdateSharedAsync(
            FelDbContext dbContext, Guid clientId, long? nextCredit, long? nextDebit, long? nextAdjustment, string? adjustmentPrefix)
        {
            var rows = await dbContext.NoteNumberings
                .Where(n => n.ClientId == clientId && n.BranchId == null)
                .ToListAsync();

            NoteNumbering Row(NoteKind kind)
            {
                var row = rows.FirstOrDefault(r => r.Kind == kind);
                if (row == null)
                {
                    row = NoteNumbering.Shared(clientId, kind);
                    dbContext.NoteNumberings.Add(row);
                    rows.Add(row);
                }
                return row;
            }

            if (nextCredit.HasValue) Row(NoteKind.CreditNote).NextNumber = nextCredit.Value;
            if (nextDebit.HasValue) Row(NoteKind.DebitNote).NextNumber = nextDebit.Value;
            if (nextAdjustment.HasValue) Row(NoteKind.SupportAdjustment).NextNumber = nextAdjustment.Value;

            if (adjustmentPrefix != null)
            {
                var prefix = adjustmentPrefix.Length == 0 ? null : adjustmentPrefix;
                if (prefix != null && await dbContext.NoteNumberings.AsNoTracking().AnyAsync(n =>
                        n.ClientId == clientId && n.Kind == NoteKind.SupportAdjustment && n.BranchId != null && n.Prefix == prefix))
                {
                    return "Ese prefijo ya lo usa la numeración de una sucursal.";
                }
                Row(NoteKind.SupportAdjustment).Prefix = prefix;
            }

            return null;
        }
    }
}
