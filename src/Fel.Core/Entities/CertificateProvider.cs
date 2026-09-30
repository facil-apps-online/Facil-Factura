using System;
using System.Collections.Generic;

namespace Fel.Core.Entities;

public class CertificateProvider
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool SandboxEnabled { get; set; }
    public string SandboxBaseUrl { get; set; } = string.Empty;
    public string ProductionBaseUrl { get; set; } = string.Empty;
    public string DownloadBaseUrl { get; set; } = string.Empty;
    public string RaCode { get; set; } = string.Empty;
    public string ConsumerKeySecretName { get; set; } = string.Empty;
    public string ConsumerSecretSecretName { get; set; } = string.Empty;
    public string EncryptedSandboxConsumerKey { get; set; } = string.Empty;
    public string EncryptedSandboxConsumerSecret { get; set; } = string.Empty;
    public string EncryptedProductionConsumerKey { get; set; } = string.Empty;
    public string EncryptedProductionConsumerSecret { get; set; } = string.Empty;
    public int RequestTimeoutSeconds { get; set; } = 30;
    public int MaxRetryAttempts { get; set; } = 5;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CertificateProfile> Profiles { get; set; } = new List<CertificateProfile>();
}
