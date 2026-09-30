using System;
using System.Collections.Generic;

namespace Fel.Core.Entities;

public class CertificatePrice
{
    public Guid Id { get; set; }
    public Guid ProviderId { get; set; }
    public CertificateProvider Provider { get; set; } = null!;
    public Guid ProfileId { get; set; }
    public CertificateProfile Profile { get; set; } = null!;
    public CertificateEnvironment Environment { get; set; }
    public Guid? TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public string PriceType { get; set; } = "Certificate";
    public string Currency { get; set; } = "COP";
    public decimal NetAmount { get; set; }
    public decimal TaxRate { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CertificateCharge> Charges { get; set; } = new List<CertificateCharge>();
}
