using System;

namespace Fel.Core.Entities;

public class CertificateProfileField
{
    public Guid Id { get; set; }
    public Guid ProfileId { get; set; }
    public CertificateProfile Profile { get; set; } = null!;
    public string ExternalName { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string ValidationPattern { get; set; } = string.Empty;
    public string DefaultValue { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool IsEditable { get; set; }
    public bool IsUsed { get; set; }
    public int DisplayOrder { get; set; }
    public string DefinitionHash { get; set; } = string.Empty;
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
}
