using System;
using System.Collections.Generic;

namespace Fel.Core.Entities;

public class CertificateProfile
{
    public Guid Id { get; set; }
    public Guid ProviderId { get; set; }
    public CertificateProvider Provider { get; set; } = null!;
    public CertificateEnvironment Environment { get; set; }
    public string ExternalCode { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PersonType { get; set; } = string.Empty;
    public string ExternalType { get; set; } = string.Empty;
    public string TokenType { get; set; } = string.Empty;
    public int? ProviderValidityDays { get; set; }
    public string TermsUrl { get; set; } = string.Empty;
    public string TermsHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime? LastSyncedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CertificateProfileField> Fields { get; set; } = new List<CertificateProfileField>();
    public ICollection<CertificatePrice> Prices { get; set; } = new List<CertificatePrice>();
}
