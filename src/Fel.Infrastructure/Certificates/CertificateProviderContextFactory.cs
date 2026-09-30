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
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(provider.DownloadBaseUrl))
            throw new InvalidOperationException($"El proveedor '{provider.Key}' no tiene URLs configuradas para el ambiente '{normalizedEnvironment}'.");

        var encryptedKey = isSandbox ? provider.EncryptedSandboxConsumerKey : provider.EncryptedProductionConsumerKey;
        var encryptedSecret = isSandbox ? provider.EncryptedSandboxConsumerSecret : provider.EncryptedProductionConsumerSecret;
        var consumerKey = !string.IsNullOrWhiteSpace(encryptedKey)
            ? _cryptoVault.DecryptPassword(encryptedKey)
            : GetSecret(provider.ConsumerKeySecretName);
        var consumerSecret = !string.IsNullOrWhiteSpace(encryptedSecret)
            ? _cryptoVault.DecryptPassword(encryptedSecret)
            : GetSecret(provider.ConsumerSecretSecretName);
        return new CertificateProviderContext(provider.Key, normalizedEnvironment, baseUrl, provider.DownloadBaseUrl, provider.RaCode, consumerKey, consumerSecret);
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
