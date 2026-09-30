using System;
using System.Collections.Generic;

namespace Fel.Core.Entities;

public class CertificateRequest
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public Guid ClientId { get; set; }
    public Client Client { get; set; } = null!;
    public Guid ProfileId { get; set; }
    public CertificateProfile Profile { get; set; } = null!;
    public CertificateEnvironment Environment { get; set; }
    public CertificateRequestType RequestType { get; set; }
    public Guid? PreviousCertificateId { get; set; }
    public Certificate? PreviousCertificate { get; set; }
    public string ProviderRequestCode { get; set; } = string.Empty;
    public string ProviderPublicId { get; set; } = string.Empty;
    public string ProviderStatus { get; set; } = string.Empty;
    public CertificateRequestStatus Status { get; set; } = CertificateRequestStatus.Draft;
    public string AdvancedAccreditedStatus { get; set; } = string.Empty;
    public string AdvancedPaymentStatus { get; set; } = string.Empty;
    public string KycUrl { get; set; } = string.Empty;
    public DateTime? KycUrlFetchedAt { get; set; }
    public DateTime? KycCompletedAt { get; set; }
    public string CsrReference { get; set; } = string.Empty;
    public string CsrHash { get; set; } = string.Empty;
    public string PublicKeyHash { get; set; } = string.Empty;
    public string EncryptedPrivateKey { get; set; } = string.Empty;
    public string KeyAlgorithm { get; set; } = string.Empty;
    public int KeySize { get; set; }
    public string TermsUrl { get; set; } = string.Empty;
    public string TermsHash { get; set; } = string.Empty;
    public DateTime? TermsAcceptedAt { get; set; }
    public Guid? TermsAcceptedByUserId { get; set; }
    public string TermsAcceptedIpAddress { get; set; } = string.Empty;
    public string TermsAcceptedUserAgent { get; set; } = string.Empty;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReadyAt { get; set; }
    public DateTime? DownloadedAt { get; set; }
    public DateTime? InstalledAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? LastProviderSyncAt { get; set; }
    public DateTime? NextProviderSyncAt { get; set; }
    public int RetryCount { get; set; }
    public DateTime? NextRetryAt { get; set; }
    public string LastErrorCode { get; set; } = string.Empty;
    public string LastErrorMessage { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public Guid? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CertificateEvent> Events { get; set; } = new List<CertificateEvent>();
    public ICollection<CertificateCharge> Charges { get; set; } = new List<CertificateCharge>();
}
