using System.Linq.Expressions;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Services
{
    // Una sola definición de lo que cuenta como documento emitido o rechazado, para los tableros del superadmin, el tenant y el cliente, los planes y el cobro:
    //
    // - Emitido = estado APPROVED. Rechazado = estado REJECTED (se muestra en todos los clientes, pero nunca se cobra). Un borrador no cuenta.
    // - Fecha = la de emisión (ProcessedAt, el momento real del envío), no la de creación del borrador; el mes se mide en hora de Colombia (UTC-5).
    // - No cuenta lo enviado al sandbox (Document.SentToSandbox) ni lo de los sandbox de developers.
    public static class DocumentCounting
    {
        public const int ColombiaOffsetHours = -5;

        // Fecha de emisión del documento. ProcessedAt siempre se llena al emitir o rechazar; CreatedAt es solo el respaldo de un documento sin ese dato.
        public static readonly Expression<Func<Document, DateTime>> EmissionDate = d => d.ProcessedAt ?? d.CreatedAt;

        // Mes de Colombia [día 1 00:00, día 1 del mes siguiente 00:00) expresado en UTC.
        public static (DateTime Start, DateTime End) MonthWindowUtc(int year, int month)
        {
            var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(-ColombiaOffsetHours);
            return (start, start.AddMonths(1));
        }

        // Un día de calendario de Colombia (00:00) expresado en UTC; sirve para la fecha "Cobro desde".
        public static DateTime ColombiaDateToUtc(DateTime date) =>
            DateTime.SpecifyKind(date.Date, DateTimeKind.Utc).AddHours(-ColombiaOffsetHours);

        // Hora de Colombia de un instante UTC (para agrupar por día).
        public static DateTime ToColombia(DateTime utc) => utc.AddHours(ColombiaOffsetHours);

        // Sin sandbox: lo enviado al ambiente de pruebas y lo de los sandbox de developers no cuenta.
        public static IQueryable<Document> Counted(this IQueryable<Document> documents) =>
            documents.Where(d => !d.SentToSandbox && !d.Client.IsDeveloperSandbox);

        public static IQueryable<Document> EmittedBetween(this IQueryable<Document> documents, DateTime startUtc, DateTime endUtc) =>
            documents.Where(d => (d.ProcessedAt ?? d.CreatedAt) >= startUtc && (d.ProcessedAt ?? d.CreatedAt) < endUtc);

        public static IQueryable<Document> Emitted(this IQueryable<Document> documents) => documents.Where(d => d.Status == "APPROVED");
        public static IQueryable<Document> Rejected(this IQueryable<Document> documents) => documents.Where(d => d.Status == "REJECTED");

        // ¿Este documento emitido se cobra? No si el tenant está exento ni si se emitió antes de su fecha de "Cobro desde".
        public static bool IsBillable(DateTime emittedUtc, DateTime? billingStartsAt, bool exempt) =>
            !exempt && (billingStartsAt == null || emittedUtc >= ColombiaDateToUtc(billingStartsAt.Value));

        public sealed record Counts(int Emitted, int Rejected);

        // Emitidos y rechazados por tenant, cliente, sucursal y mes (de Colombia) en un rango de meses, con las reglas de arriba. Se agrupa en memoria porque el mes
        // sale de la hora de Colombia de la fecha de emisión.
        public sealed record MonthCount(Guid TenantId, Guid ClientId, Guid BranchId, int Year, int Month, string Status, int Count);

        public static async Task<List<MonthCount>> MonthlyCountsAsync(IQueryable<Document> documents, int fromYear, int fromMonth, int toYear, int toMonth)
        {
            var start = MonthWindowUtc(fromYear, fromMonth).Start;
            var end = MonthWindowUtc(toYear, toMonth).End;
            var rows = await documents.AsNoTracking().Counted().EmittedBetween(start, end)
                .Where(d => d.Status == "APPROVED" || d.Status == "REJECTED")
                .Select(d => new { d.Client.TenantId, d.ClientId, d.BranchId, d.Status, EmittedAt = d.ProcessedAt ?? d.CreatedAt })
                .ToListAsync();
            return rows
                .Select(r => new { r.TenantId, r.ClientId, r.BranchId, r.Status, Local = ToColombia(r.EmittedAt) })
                .GroupBy(r => new { r.TenantId, r.ClientId, r.BranchId, r.Status, r.Local.Year, r.Local.Month })
                .Select(g => new MonthCount(g.Key.TenantId, g.Key.ClientId, g.Key.BranchId, g.Key.Year, g.Key.Month, g.Key.Status, g.Count()))
                .ToList();
        }

        // Emitidos y rechazados de un mes de Colombia, con las reglas de arriba. tenantId nulo = todos los tenants.
        public static async Task<Counts> CountMonthAsync(FelDbContext db, int year, int month, Guid? tenantId = null, Guid? clientId = null)
        {
            var (start, end) = MonthWindowUtc(year, month);
            var query = db.Documents.AsNoTracking().Counted().EmittedBetween(start, end);
            if (tenantId.HasValue) query = query.Where(d => d.Client.TenantId == tenantId.Value);
            if (clientId.HasValue) query = query.Where(d => d.ClientId == clientId.Value);

            var byStatus = await query.Where(d => d.Status == "APPROVED" || d.Status == "REJECTED")
                .GroupBy(d => d.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync();
            return new Counts(byStatus.FirstOrDefault(x => x.Status == "APPROVED")?.Count ?? 0, byStatus.FirstOrDefault(x => x.Status == "REJECTED")?.Count ?? 0);
        }
    }
}
