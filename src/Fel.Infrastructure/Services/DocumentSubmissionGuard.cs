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
