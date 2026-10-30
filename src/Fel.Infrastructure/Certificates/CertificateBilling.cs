using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Certificates;

public sealed record CertificateBalance(decimal Available, decimal Reserved, decimal Consumed, decimal Deposited);

// Cobro prepagado de certificados: el tenant abona saldo (lo registra el superadmin) y cada certificado de producción se paga de ese saldo.
//
// - Al solicitar se RESERVA el precio vigente: un CertificateCharge pendiente y un movimiento negativo. Se hace antes de llamar a Viafirma,
//   así ninguna solicitud cobrable llega al proveedor sin saldo.
// - Si la solicitud falla al enviarse o Viafirma la rechaza, se DEVUELVE: el cargo queda anulado y un movimiento positivo repone el saldo.
// - Cuando el certificado se emite e instala, el cargo queda CAUSADO (sin movimiento: el valor ya se descontó al reservar).
//
// Sandbox no se cobra, ni a los tenants exentos de cobro (Tenant.IsBillingExempt). El precio es el vigente del perfil para ese tenant si tiene
// uno propio, si no el general; el cargo copia el valor, así un cambio de precio no toca lo ya solicitado.
public static class CertificateBilling
{
    public static bool RequiresPayment(Tenant tenant, CertificateEnvironment environment) =>
        environment == CertificateEnvironment.Production && !tenant.IsBillingExempt;

    public static decimal TaxAmount(CertificatePrice price) => Math.Round(price.NetAmount * price.TaxRate / 100m, 2);

    public static decimal Total(CertificatePrice price) => price.NetAmount + TaxAmount(price);

    public static async Task<CertificatePrice?> ResolvePriceAsync(
        FelDbContext db, Guid tenantId, Guid profileId, CertificateEnvironment environment, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var candidates = await db.CertificatePrices.AsNoTracking()
            .Where(p => p.ProfileId == profileId && p.Environment == environment && p.IsActive
                && p.EffectiveFrom <= now && (p.EffectiveTo == null || p.EffectiveTo > now)
                && (p.TenantId == tenantId || p.TenantId == null))
            .ToListAsync(cancellationToken);
        return candidates
            .OrderByDescending(p => p.TenantId == tenantId)
            .ThenByDescending(p => p.EffectiveFrom)
            .FirstOrDefault();
    }

    public static async Task<CertificateBalance> GetBalanceAsync(FelDbContext db, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var available = await db.CertificateBalanceMovements.Where(m => m.TenantId == tenantId).SumAsync(m => (decimal?)m.Amount, cancellationToken) ?? 0m;
        var deposited = await db.CertificateBalanceMovements
            .Where(m => m.TenantId == tenantId && (m.Type == CertificateBalanceMovementType.Deposit || m.Type == CertificateBalanceMovementType.Adjustment))
            .SumAsync(m => (decimal?)m.Amount, cancellationToken) ?? 0m;
        var reserved = await db.CertificateCharges.Where(c => c.TenantId == tenantId && c.Status == CertificateChargeStatus.Pending)
            .SumAsync(c => (decimal?)c.TotalAmount, cancellationToken) ?? 0m;
        var consumed = await db.CertificateCharges.Where(c => c.TenantId == tenantId && c.Status == CertificateChargeStatus.Accrued)
            .SumAsync(c => (decimal?)c.TotalAmount, cancellationToken) ?? 0m;
        return new CertificateBalance(available, reserved, consumed, deposited);
    }

    // Serializa los movimientos de saldo de un tenant (reservas y abonos) dentro de la transacción en curso, para que dos solicitudes
    // simultáneas no gasten el mismo saldo. Se libera al confirmar o deshacer la transacción.
    public static Task LockTenantBalanceAsync(FelDbContext db, Guid tenantId, CancellationToken cancellationToken = default) =>
        db.Database.ExecuteSqlRawAsync(
            "DECLARE @r int; EXEC @r = sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000; " +
            "IF @r < 0 THROW 50001, 'No fue posible bloquear el saldo de certificados.', 1;",
            new object[] { $"cert-balance:{tenantId:N}" }, cancellationToken);

    // Agrega (sin guardar) el cargo pendiente y el movimiento negativo de la reserva. El llamador ya verificó el saldo con el bloqueo tomado.
    public static CertificateCharge Reserve(FelDbContext db, CertificateRequest request, CertificatePrice price, string profileTitle, string actor)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var charge = new CertificateCharge
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            ClientId = request.ClientId,
            CertificateRequestId = request.Id,
            PriceId = price.Id,
            ChargeType = request.RequestType switch
            {
                CertificateRequestType.Renewal => CertificateChargeType.Renewal,
                CertificateRequestType.Replacement => CertificateChargeType.Replacement,
                _ => CertificateChargeType.Initial
            },
            Status = CertificateChargeStatus.Pending,
            UnitPrice = price.NetAmount,
            TaxAmount = TaxAmount(price),
            TotalAmount = Total(price),
            Currency = price.Currency,
            BillingPeriodStart = monthStart,
            BillingPeriodEnd = monthStart.AddMonths(1).AddTicks(-1),
            OccurredAt = now,
            DescriptionSnapshot = Truncate($"Certificado {profileTitle}", 500),
            CreatedAt = now
        };
        db.CertificateCharges.Add(charge);
        db.CertificateBalanceMovements.Add(new CertificateBalanceMovement
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            Type = CertificateBalanceMovementType.Reserve,
            Amount = -charge.TotalAmount,
            Currency = charge.Currency,
            CertificateChargeId = charge.Id,
            Reference = $"Solicitud {request.Id.ToString()[..8]}",
            Notes = charge.DescriptionSnapshot,
            OccurredAt = now,
            CreatedBy = actor,
            CreatedAt = now
        });
        db.CertificateEvents.Add(new CertificateEvent
        {
            Id = Guid.NewGuid(),
            CertificateRequestId = request.Id,
            EventType = CertificateEventType.ChargeCreated,
            ActorType = actor,
            OccurredAt = now,
            MetadataJson = $"{{\"chargeId\":\"{charge.Id}\",\"total\":{charge.TotalAmount.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}"
        });
        return charge;
    }

    // Devuelve al saldo lo reservado por una solicitud que no llegó a emitirse (sin guardar). Idempotente: solo toca cargos pendientes.
    public static async Task ReleaseAsync(FelDbContext db, Guid requestId, string reason, string actor, CancellationToken cancellationToken = default)
    {
        var charges = await db.CertificateCharges
            .Where(c => c.CertificateRequestId == requestId && c.Status == CertificateChargeStatus.Pending)
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var charge in charges)
        {
            charge.Status = CertificateChargeStatus.Voided;
            db.CertificateBalanceMovements.Add(new CertificateBalanceMovement
            {
                Id = Guid.NewGuid(),
                TenantId = charge.TenantId,
                Type = CertificateBalanceMovementType.Release,
                Amount = charge.TotalAmount,
                Currency = charge.Currency,
                CertificateChargeId = charge.Id,
                Reference = $"Solicitud {requestId.ToString()[..8]}",
                Notes = Truncate($"Devolución: {reason}", 1000),
                OccurredAt = now,
                CreatedBy = actor,
                CreatedAt = now
            });
            db.CertificateEvents.Add(new CertificateEvent
            {
                Id = Guid.NewGuid(),
                CertificateRequestId = requestId,
                EventType = CertificateEventType.ChargeReleased,
                ActorType = actor,
                OccurredAt = now,
                MetadataJson = $"{{\"chargeId\":\"{charge.Id}\"}}"
            });
        }
    }

    // El certificado se emitió: lo reservado queda causado (sin guardar).
    public static async Task ConsumeAsync(FelDbContext db, Guid requestId, CancellationToken cancellationToken = default)
    {
        var charges = await db.CertificateCharges
            .Where(c => c.CertificateRequestId == requestId && c.Status == CertificateChargeStatus.Pending)
            .ToListAsync(cancellationToken);
        foreach (var charge in charges)
        {
            charge.Status = CertificateChargeStatus.Accrued;
            charge.OccurredAt = DateTime.UtcNow;
        }
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
