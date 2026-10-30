using System;

namespace Fel.Core.Entities;

// Movimiento del saldo prepagado de certificados de un tenant. El saldo disponible es la suma de Amount (con signo): los abonos y ajustes los
// registra el superadmin; la reserva (negativa) se crea al solicitar un certificado de producción y la devolución (positiva) si Viafirma lo
// rechaza o la solicitud falla. Cuando el certificado se emite no hay movimiento: la reserva ya descontó el valor y su cargo pasa a causado.
public class CertificateBalanceMovement
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public CertificateBalanceMovementType Type { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "COP";
    public Guid? CertificateChargeId { get; set; }
    public CertificateCharge? CertificateCharge { get; set; }
    // Abonos: referencia del pago (número de transferencia, consignación...). Reservas y devoluciones: la solicitud.
    public string Reference { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    // Fecha del pago (abonos) o del evento.
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
