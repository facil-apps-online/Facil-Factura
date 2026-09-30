using System;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CertificateRequest = Fel.Core.Entities.CertificateRequest;

namespace Fel.Infrastructure.Certificates;

public sealed class CertificateAssemblyService : ICertificateAssemblyService
{
    private const string PasswordCharset = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%&*";

    private readonly FelDbContext _dbContext;
    private readonly ICertificateProvider _provider;
    private readonly ICertificateProviderContextFactory _contextFactory;
    private readonly ICertificateStorageService _storageService;
    private readonly ICryptoVault _cryptoVault;
    private readonly ILogger<CertificateAssemblyService> _logger;

    public CertificateAssemblyService(
        FelDbContext dbContext,
        ICertificateProvider provider,
        ICertificateProviderContextFactory contextFactory,
        ICertificateStorageService storageService,
        ICryptoVault cryptoVault,
        ILogger<CertificateAssemblyService> logger)
    {
        _dbContext = dbContext;
        _provider = provider;
        _contextFactory = contextFactory;
        _storageService = storageService;
        _cryptoVault = cryptoVault;
        _logger = logger;
    }

    public async Task<AssembledCertificate> AssembleAndInstallAsync(CertificateRequest request, CancellationToken cancellationToken = default)
    {
        var providerKey = request.Profile.Provider.Key;
        var context = await _contextFactory.CreateAsync(providerKey, request.Environment.ToString(), cancellationToken);

        var p7bBytes = await _provider.DownloadP7bAsync(request.ProviderPublicId, context, cancellationToken);
        request.DownloadedAt ??= DateTime.UtcNow;

        var leafCertificate = ExtractLeafCertificate(p7bBytes, request.PublicKeyHash);

        var pkcs8PrivateKey = Convert.FromBase64String(_cryptoVault.DecryptPassword(request.EncryptedPrivateKey));
        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(pkcs8PrivateKey, out _);

        using var certificateWithKey = leafCertificate.CopyWithPrivateKey(rsa);
        var password = GeneratePassword();
        var pkcs12Bytes = certificateWithKey.Export(X509ContentType.Pfx, password);

        using (var stream = new MemoryStream(pkcs12Bytes))
        {
            var fileName = await _storageService.SaveCertificateAsync(request.ClientId, stream, $"{request.ClientId}.p12");

            var previousCurrent = await _dbContext.Certificates
                .Where(c => c.ClientId == request.ClientId && c.Environment == request.Environment && c.Status == CertificateStatus.Current)
                .ToListAsync(cancellationToken);
            foreach (var previous in previousCurrent)
            {
                previous.Status = CertificateStatus.Retired;
                previous.RetiredAt = DateTime.UtcNow;
                previous.IsActive = false;
            }

            var now = DateTime.UtcNow;
            var certificate = new Certificate
            {
                Id = Guid.NewGuid(),
                ClientId = request.ClientId,
                FileName = fileName,
                EncryptedPassword = _cryptoVault.EncryptPassword(password),
                ExpirationDate = certificateWithKey.NotAfter,
                CreatedAt = now,
                IsActive = true,
                Environment = request.Environment,
                ProviderId = request.Profile.ProviderId,
                ProfileId = request.ProfileId,
                CertificateRequestId = request.Id,
                Thumbprint = certificateWithKey.Thumbprint,
                SerialNumber = certificateWithKey.SerialNumber,
                Subject = certificateWithKey.Subject,
                Issuer = certificateWithKey.Issuer,
                NotBefore = certificateWithKey.NotBefore.ToUniversalTime(),
                NotAfter = certificateWithKey.NotAfter.ToUniversalTime(),
                Status = CertificateStatus.Current,
                ActivatedAt = now
            };
            _dbContext.Certificates.Add(certificate);

            var previousStatus = request.Status;
            request.InstalledAt = now;
            request.CompletedAt = now;
            request.Status = CertificateRequestStatus.Active;

            _dbContext.CertificateEvents.AddRange(
                new CertificateEvent
                {
                    Id = Guid.NewGuid(),
                    CertificateRequestId = request.Id,
                    EventType = CertificateEventType.P7bDownloaded,
                    ActorType = "system",
                    OccurredAt = now
                },
                new CertificateEvent
                {
                    Id = Guid.NewGuid(),
                    CertificateRequestId = request.Id,
                    CertificateId = certificate.Id,
                    EventType = CertificateEventType.Installed,
                    ActorType = "system",
                    OccurredAt = now
                },
                new CertificateEvent
                {
                    Id = Guid.NewGuid(),
                    CertificateRequestId = request.Id,
                    CertificateId = certificate.Id,
                    EventType = CertificateEventType.Activated,
                    FromStatus = previousStatus,
                    ToStatus = CertificateRequestStatus.Active,
                    ActorType = "system",
                    OccurredAt = now
                });

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Certificado instalado para Client {ClientId} ({Environment}). Thumbprint={Thumbprint}",
                request.ClientId, request.Environment, certificate.Thumbprint);

            return new AssembledCertificate(certificate, pkcs12Bytes, password);
        }
    }

    // La llave pública que viaja en el CSR debe coincidir exactamente con la del certificado que
    // Viafirma firmó — si no, estaríamos instalando un certificado que no corresponde a nuestra
    // llave privada. El .p7b puede traer, además del certificado del titular, la cadena de la CA;
    // se identifica el titular por hash de clave pública, no por posición en la colección.
    private static X509Certificate2 ExtractLeafCertificate(byte[] p7bBytes, string expectedPublicKeyHash)
    {
        var signedCms = new SignedCms();
        signedCms.Decode(p7bBytes);

        foreach (var candidate in signedCms.Certificates)
        {
            var publicKeyInfo = candidate.PublicKey.ExportSubjectPublicKeyInfo();
            var hash = Convert.ToHexString(SHA256.HashData(publicKeyInfo)).ToLowerInvariant();
            if (string.Equals(hash, expectedPublicKeyHash, StringComparison.OrdinalIgnoreCase))
                return candidate;
        }

        throw new InvalidOperationException(
            "Ningún certificado del .p7b coincide con la llave pública del CSR original — no se puede instalar.");
    }

    private static string GeneratePassword()
    {
        var bytes = RandomNumberGenerator.GetBytes(16);
        var chars = new char[16];
        for (var i = 0; i < 16; i++)
            chars[i] = PasswordCharset[bytes[i] % PasswordCharset.Length];
        return new string(chars);
    }
}
