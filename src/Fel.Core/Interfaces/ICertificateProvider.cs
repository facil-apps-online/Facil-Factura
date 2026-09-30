using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Fel.Core.Interfaces;

public interface ICertificateProvider
{
    Task<IReadOnlyList<ProviderCertificateProfile>> GetProfilesAsync(CertificateProviderContext context, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProviderProfileField>> GetProfileFieldsAsync(string profileCode, CertificateProviderContext context, CancellationToken cancellationToken = default);
    Task<ProviderRequestCreated> CreateRequestFromCsrAsync(CreateProviderRequest command, CertificateProviderContext context, CancellationToken cancellationToken = default);
    Task<ProviderRequestStatus> GetStatusAsync(string requestCode, CertificateProviderContext context, CancellationToken cancellationToken = default);
    Task<ProviderAdvancedStatus> GetAdvancedStatusAsync(string requestCode, CertificateProviderContext context, CancellationToken cancellationToken = default);
    Task<Uri> GetKycLinkAsync(string requestCode, CertificateProviderContext context, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProviderFile>> UploadFilesAsync(string requestCode, IReadOnlyList<ProviderFileUpload> files, CertificateProviderContext context, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProviderFile>> ListFilesAsync(string requestCode, CertificateProviderContext context, CancellationToken cancellationToken = default);
    Task<byte[]> DownloadP7bAsync(string publicId, CertificateProviderContext context, CancellationToken cancellationToken = default);
    Task<string> GetRevocationCodeAsync(string requestCode, CertificateProviderContext context, CancellationToken cancellationToken = default);
    Task RevokeAsync(string revocationCode, CertificateProviderContext context, CancellationToken cancellationToken = default);
    Task RejectAsync(string requestCode, CertificateProviderContext context, CancellationToken cancellationToken = default);
}

public sealed record CertificateProviderContext(
    string ProviderKey,
    string Environment,
    string BaseUrl,
    string DownloadBaseUrl,
    string RaCode,
    string ConsumerKey,
    string ConsumerSecret);

public sealed record ProviderCertificateProfile(
    string ExternalCode,
    string Title,
    string Description,
    string EmailProperty,
    int? ValidityDays,
    string RaCode,
    string ExternalType,
    string TokenType,
    string TermsUrl);

public sealed record ProviderProfileField(
    string Name,
    string Label,
    string Type,
    string ValidationPattern,
    string DefaultValue,
    bool Required,
    bool Editable,
    bool Used,
    int Index);

public sealed record CreateProviderRequest(
    string IdentityType,
    string CountryCode,
    string Identity,
    string RaCode,
    string ProfileCode,
    string CertificateEmail,
    string OrganizationType,
    string CsrBase64);

public sealed record ProviderRequestCreated(string RequestCode, string PublicId);

public sealed record ProviderRequestStatus(string Code);

public sealed record ProviderAdvancedStatus(
    string Status,
    string Accredited,
    string Paid,
    IReadOnlyList<ProviderPublicNote> Notes);

public sealed record ProviderPublicNote(string Note, DateTime? Date);

public sealed record ProviderFile(
    string Id,
    string Name,
    bool UploadedByUser,
    long Size,
    DateTimeOffset? AddedAt);

public sealed record ProviderFileUpload(string Name, byte[] Content);
