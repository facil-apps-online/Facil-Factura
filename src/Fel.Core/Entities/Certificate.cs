using System;
using System.Collections.Generic;

namespace Fel.Core.Entities
{
    public class Certificate
    {
        public Guid Id { get; set; }
        public Guid ClientId { get; set; }
        public Client Client { get; set; } = null!;
        
        public string FileName { get; set; } = string.Empty;
        public string EncryptedPassword { get; set; } = string.Empty;
        public DateTime ExpirationDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
        public CertificateEnvironment Environment { get; set; } = CertificateEnvironment.Production;
        public Guid? ProviderId { get; set; }
        public CertificateProvider? Provider { get; set; }
        public Guid? ProfileId { get; set; }
        public CertificateProfile? Profile { get; set; }
        public Guid? CertificateRequestId { get; set; }
        public CertificateRequest? CertificateRequest { get; set; }
        public string Thumbprint { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public DateTime NotBefore { get; set; }
        public DateTime NotAfter { get; set; }
        public DateTime? ActivationAt { get; set; }
        public CertificateStatus Status { get; set; } = CertificateStatus.Current;
        public DateTime? ActivatedAt { get; set; }
        public DateTime? RetiredAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public bool AutoRenewalEnabled { get; set; }
        public int? RenewalLeadTimeDays { get; set; }

        public ICollection<CertificateRequest> RenewalRequests { get; set; } = new List<CertificateRequest>();
    }
}
