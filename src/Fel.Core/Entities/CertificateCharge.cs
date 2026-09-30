using System;

namespace Fel.Core.Entities;

public class CertificateCharge
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public Guid ClientId { get; set; }
    public Client Client { get; set; } = null!;
    public Guid CertificateRequestId { get; set; }
    public CertificateRequest CertificateRequest { get; set; } = null!;
    public Guid PriceId { get; set; }
    public CertificatePrice Price { get; set; } = null!;
    public CertificateChargeType ChargeType { get; set; }
    public CertificateChargeStatus Status { get; set; } = CertificateChargeStatus.Pending;
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "COP";
    public DateTime BillingPeriodStart { get; set; }
    public DateTime BillingPeriodEnd { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string DescriptionSnapshot { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
