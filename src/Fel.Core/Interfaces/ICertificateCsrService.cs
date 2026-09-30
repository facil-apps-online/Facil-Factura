using System;

namespace Fel.Core.Interfaces;

public interface ICertificateCsrService
{
    GeneratedCertificateRequest Generate(CertificateSubject subject);
}

public sealed record CertificateSubject(
    string Country,
    string State,
    string City,
    string Street,
    string Organization,
    string OrganizationalUnit,
    string SerialNumber,
    string Email,
    string GivenName,
    string Surname,
    string? CommonName = null);

public sealed record GeneratedCertificateRequest(
    string CsrBase64,
    string PublicKeyHash,
    string EncryptedPrivateKey,
    string Algorithm,
    int KeySize);
