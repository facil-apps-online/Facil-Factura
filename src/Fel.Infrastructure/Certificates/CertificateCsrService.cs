using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Fel.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Fel.Infrastructure.Certificates;

public sealed class CertificateCsrService : ICertificateCsrService
{
    private readonly ICryptoVault _cryptoVault;
    private readonly IConfiguration _configuration;

    public CertificateCsrService(ICryptoVault cryptoVault, IConfiguration configuration)
    {
        _cryptoVault = cryptoVault;
        _configuration = configuration;
    }

    public GeneratedCertificateRequest Generate(CertificateSubject subject)
    {
        var algorithm = RequiredSetting("Certificates:KeyAlgorithm");
        if (!algorithm.Equals("RSA", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Certificates:KeyAlgorithm debe ser RSA para el perfil PKCS#10 configurado.");

        if (!int.TryParse(_configuration["Certificates:RsaKeySize"], out var keySize) || keySize < 2048)
            throw new InvalidOperationException("Certificates:RsaKeySize debe estar configurado con un tamaño RSA seguro.");

        using var rsa = RSA.Create(keySize);
        var distinguishedName = BuildDistinguishedName(subject);
        var request = new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        var csr = request.CreateSigningRequest();
        var csrPem = PemEncoding.WriteString("CERTIFICATE REQUEST", csr);
        var publicKey = rsa.ExportSubjectPublicKeyInfo();
        var privateKey = rsa.ExportPkcs8PrivateKey();
        return new GeneratedCertificateRequest(
            Convert.ToBase64String(Encoding.ASCII.GetBytes(csrPem)),
            Convert.ToHexString(SHA256.HashData(publicKey)).ToLowerInvariant(),
            _cryptoVault.EncryptPassword(Convert.ToBase64String(privateKey)),
            algorithm.ToUpperInvariant(),
            keySize);
    }

    private string RequiredSetting(string key)
        => !string.IsNullOrWhiteSpace(_configuration[key])
            ? _configuration[key]!
            : throw new InvalidOperationException($"La configuración '{key}' es obligatoria.");

    private static X500DistinguishedName BuildDistinguishedName(CertificateSubject subject)
    {
        var builder = new X500DistinguishedNameBuilder();
        AddIfPresent(subject.Country, builder.AddCountryOrRegion);
        AddIfPresent(subject.State, builder.AddStateOrProvinceName);
        AddIfPresent(subject.City, builder.AddLocalityName);
        AddIfPresent(subject.Street, value => builder.Add("2.5.4.9", value));
        AddIfPresent(subject.Organization, builder.AddOrganizationName);
        AddIfPresent(subject.OrganizationalUnit, builder.AddOrganizationalUnitName);
        AddIfPresent(subject.SerialNumber, value => builder.Add("2.5.4.5", value));
        AddIfPresent(subject.Email, builder.AddEmailAddress);
        AddIfPresent(subject.GivenName, value => builder.Add("2.5.4.42", value));
        AddIfPresent(subject.Surname, value => builder.Add("2.5.4.4", value));
        AddIfPresent(subject.CommonName ?? subject.Organization, builder.AddCommonName);
        return builder.Build();
    }

    private static void AddIfPresent(string? value, Action<string> add)
    {
        if (!string.IsNullOrWhiteSpace(value))
            add(value.Trim());
    }
}
