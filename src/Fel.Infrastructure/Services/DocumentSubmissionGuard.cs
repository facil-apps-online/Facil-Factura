using System;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;

namespace Fel.Infrastructure.Services;

public enum SubmissionClaimResult
{
    Claimed,
    InProgress,
    Unknown,
    AlreadyCompleted
}

public static class DocumentSubmissionGuard
{
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(10);

    public static async Task<SubmissionClaimResult> TryClaimAsync(FelDbContext db, Document document)
    {
        if (document.Status == "APPROVED" || document.SubmissionState == "COMPLETED")
            return SubmissionClaimResult.AlreadyCompleted;

        var now = DateTime.UtcNow;
        if (document.SubmissionState == "IN_PROGRESS")
        {
            if (document.SubmissionLeaseUntil > now)
                return SubmissionClaimResult.InProgress;

            // No se reintenta automáticamente después de vencer el lease: el proveedor pudo haber
            // aceptado el documento y la respuesta pudo perderse. Primero debe reconciliarse.
            document.SubmissionState = "UNKNOWN";
            document.SubmissionLeaseUntil = null;
            await db.SaveChangesAsync();
            return SubmissionClaimResult.Unknown;
        }

        if (document.SubmissionState == "UNKNOWN")
            return SubmissionClaimResult.Unknown;

        document.SubmissionState = "IN_PROGRESS";
        document.SubmissionOperationId = Guid.NewGuid();
        document.SubmissionAttemptCount++;
        document.SubmissionStartedAt = now;
        document.SubmissionLeaseUntil = now.Add(Lease);
        document.SubmissionLastError = null;
        await db.SaveChangesAsync();
        return SubmissionClaimResult.Claimed;
    }

    // Mensaje a dar cuando no se pudo reclamar el documento (null si se reclamó y se puede enviar).
    public static string? ClaimConflictMessage(SubmissionClaimResult result) => result switch
    {
        SubmissionClaimResult.Claimed => null,
        SubmissionClaimResult.InProgress => "Este documento ya tiene un envío en curso.",
        SubmissionClaimResult.AlreadyCompleted => "Este documento ya fue emitido.",
        _ => "Este documento quedó con un resultado incierto: pudo haber llegado al proveedor. Comunícate con soporte antes de reintentar."
    };

    // Llama al proveedor con el documento ya reclamado. Las validaciones del integrador y la numeración agotada
    // (NotSupportedException y su derivada ResolutionExhaustedException) se lanzan antes de enviar, así que liberan el
    // documento para corregirlo; cualquier otra excepción lo deja incierto, porque pudo haber llegado al proveedor. La
    // excepción sigue su curso en ambos casos.
    public static async Task<T> SendClaimedAsync<T>(FelDbContext db, Document document, Func<Task<T>> send)
    {
        try
        {
            return await send();
        }
        catch (Exception ex)
        {
            if (ex is NotSupportedException)
                MarkFailed(document, ex.Message);
            else
                MarkUnknown(document, ex.Message);
            try { await db.SaveChangesAsync(); }
            catch (Exception) { /* Se conserva la excepción original; el lease vencido lo deja incierto igual. */ }
            throw;
        }
    }

    public static void MarkCompleted(Document document)
    {
        document.SubmissionState = "COMPLETED";
        document.SubmissionLeaseUntil = null;
        document.SubmissionLastError = null;
    }

    public static void MarkFailed(Document document, string? error)
    {
        document.SubmissionState = "FAILED";
        document.SubmissionLeaseUntil = null;
        document.SubmissionLastError = string.IsNullOrWhiteSpace(error) ? null : error[..Math.Min(error.Length, 4000)];
    }

    // Una excepción durante el envío no dice si el proveedor alcanzó a recibir el documento: queda incierto hasta reconciliarlo.
    public static void MarkUnknown(Document document, string? error)
    {
        document.SubmissionState = "UNKNOWN";
        document.SubmissionLeaseUntil = null;
        document.SubmissionLastError = string.IsNullOrWhiteSpace(error) ? null : error[..Math.Min(error.Length, 4000)];
    }
}
