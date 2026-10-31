using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Fel.Infrastructure.Certificates;

public sealed class CertificateProviderContextFactory : ICertificateProviderContextFactory
{
    private readonly FelDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ICryptoVault _cryptoVault;

    public CertificateProviderContextFactory(FelDbContext dbContext, IConfiguration configuration, ICryptoVault cryptoVault)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _cryptoVault = cryptoVault;
    }

    public async Task<CertificateProviderContext> CreateAsync(string providerKey, string environment, CancellationToken cancellationToken = default)
    {
        var provider = await _dbContext.CertificateProviders.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Key == providerKey && p.IsActive, cancellationToken);
        if (provider == null)
            throw new InvalidOperationException($"No existe un proveedor de certificados activo con clave '{providerKey}'.");

        var normalizedEnvironment = environment.Trim();
        var isSandbox = normalizedEnvironment.Equals(nameof(CertificateEnvironment.Sandbox), StringComparison.OrdinalIgnoreCase);
        var baseUrl = isSandbox ? provider.SandboxBaseUrl : provider.ProductionBaseUrl;
        var downloadBaseUrl = DownloadBaseUrlFor(baseUrl, provider.DownloadBaseUrl);
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(downloadBaseUrl))
            throw new InvalidOperationException($"El proveedor '{provider.Key}' no tiene URLs configuradas para el ambiente '{normalizedEnvironment}'.");

        var encryptedKey = isSandbox ? provider.EncryptedSandboxConsumerKey : provider.EncryptedProductionConsumerKey;
        var encryptedSecret = isSandbox ? provider.EncryptedSandboxConsumerSecret : provider.EncryptedProductionConsumerSecret;
        var consumerKey = !string.IsNullOrWhiteSpace(encryptedKey)
            ? _cryptoVault.DecryptPassword(encryptedKey)
            : GetSecret(provider.ConsumerKeySecretName);
        var consumerSecret = !string.IsNullOrWhiteSpace(encryptedSecret)
            ? _cryptoVault.DecryptPassword(encryptedSecret)
            : GetSecret(provider.ConsumerSecretSecretName);
        return new CertificateProviderContext(provider.Key, normalizedEnvironment, baseUrl, downloadBaseUrl, provider.RaCode, consumerKey, consumerSecret);
    }

    // La descarga del certificado vive en el mismo servidor que la API de cada ambiente, sin el sufijo /api/v2 (sandbox.viafirma.com/ra/api/v2 → sandbox.viafirma.com/ra;
    // ecd.viafirma.com/ra/api/v2 → ecd.viafirma.com/ra). La DownloadBaseUrl del proveedor es una sola y apuntaba a Sandbox: con ella, la descarga de un
    // certificado de Producción iba al servidor de pruebas. Solo se usa si la base del ambiente no tiene ese sufijo.
    private static string DownloadBaseUrlFor(string? baseUrl, string? configured)
    {
        const string ApiSuffix = "/api/v2";
        var trimmed = (baseUrl ?? string.Empty).Trim().TrimEnd('/');
        return trimmed.EndsWith(ApiSuffix, StringComparison.OrdinalIgnoreCase) ? trimmed[..^ApiSuffix.Length] : configured ?? string.Empty;
    }

    private string GetSecret(string secretName)
    {
        if (string.IsNullOrWhiteSpace(secretName))
            throw new InvalidOperationException("El proveedor de certificados no tiene configurado el nombre de un secreto.");

        var value = _configuration[$"Secrets:{secretName}"] ?? _configuration[secretName];
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"No se encontró el secreto configurado '{secretName}'.");
        return value;
    }
}
