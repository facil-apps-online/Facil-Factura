using System;

namespace Fel.Core.Entities;

public class CertificateEvent
{
    public Guid Id { get; set; }
    public Guid CertificateRequestId { get; set; }
    public CertificateRequest CertificateRequest { get; set; } = null!;
    public Guid? CertificateId { get; set; }
    public Certificate? Certificate { get; set; }
    public CertificateEventType EventType { get; set; }
    public CertificateRequestStatus? FromStatus { get; set; }
    public CertificateRequestStatus? ToStatus { get; set; }
    public string ProviderStatus { get; set; } = string.Empty;
    public int? ProviderHttpStatus { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string ActorType { get; set; } = string.Empty;
    public Guid? ActorId { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string MetadataJson { get; set; } = string.Empty;
}
