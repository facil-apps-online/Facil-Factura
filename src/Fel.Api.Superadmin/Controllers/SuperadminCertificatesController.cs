using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Certificates;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fel.Api.Superadmin.Controllers
{
    [ApiController]
    [Route("api/superadmin/certificates")]
    public class SuperadminCertificatesController : ControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly ICertificateProvider _provider;
        private readonly ICertificateProviderContextFactory _contextFactory;
        private readonly ICryptoVault _cryptoVault;
        private readonly ILogger<SuperadminCertificatesController> _logger;

        public SuperadminCertificatesController(
            FelDbContext dbContext,
            ICertificateProvider provider,
            ICertificateProviderContextFactory contextFactory,
            ICryptoVault cryptoVault,
            ILogger<SuperadminCertificatesController> logger)
        {
            _dbContext = dbContext;
            _provider = provider;
            _contextFactory = contextFactory;
            _cryptoVault = cryptoVault;
            _logger = logger;
        }

        // Vista consolidada de todos los certificados de TODOS los tenants, ordenada por fecha de
        // vencimiento — para que Superadmin sepa qué hay que provisionar/dar seguimiento pronto,
        // sin tener que entrar tenant por tenant.
        [HttpGet]
        public async Task<IActionResult> GetAllCertificates()
        {
            var certificates = await _dbContext.Set<Certificate>()
                .Where(c => c.IsActive)
                .OrderBy(c => c.ExpirationDate)
                .Select(c => new
                {
                    c.ClientId,
                    ClientName = string.IsNullOrWhiteSpace(c.Client.CommercialName) ? c.Client.CompanyName : c.Client.CommercialName,
                    TenantId = c.Client.TenantId,
                    TenantName = c.Client.Tenant.CommercialName,
                    c.FileName,
                    c.ExpirationDate,
                    c.CreatedAt
                })
                .ToListAsync();

            return Ok(certificates);
        }

        [HttpGet("providers")]
        public async Task<IActionResult> GetProviders(CancellationToken cancellationToken = default)
        {
            var providers = await _dbContext.CertificateProviders.AsNoTracking()
                .OrderBy(item => item.Name)
                .Select(item => new
                {
                    item.Id,
                    item.Key,
                    item.Name,
                    item.IsActive,
                    item.SandboxEnabled,
                    item.SandboxBaseUrl,
                    item.ProductionBaseUrl,
                    item.DownloadBaseUrl,
                    item.RaCode,
                    SandboxCredentialsConfigured = item.EncryptedSandboxConsumerKey != "" && item.EncryptedSandboxConsumerSecret != "",
                    ProductionCredentialsConfigured = item.EncryptedProductionConsumerKey != "" && item.EncryptedProductionConsumerSecret != "",
                    item.UpdatedAt
                })
                .ToListAsync(cancellationToken);
            return Ok(providers);
        }

        [HttpPut("providers/{providerKey}")]
        public async Task<IActionResult> SaveProvider(
            string providerKey,
            [FromBody] CertificateProviderConfigurationRequest request,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(providerKey) || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.RaCode))
                return BadRequest(new { Message = "La clave, el nombre y el código RA son obligatorios." });
            if (!IsHttps(request.SandboxBaseUrl) || !IsHttps(request.ProductionBaseUrl) || !IsHttps(request.DownloadBaseUrl))
                return BadRequest(new { Message = "Las URLs del proveedor deben usar HTTPS." });

            var provider = await _dbContext.CertificateProviders.SingleOrDefaultAsync(item => item.Key == providerKey, cancellationToken);
            if (provider == null)
            {
                provider = new CertificateProvider { Id = Guid.NewGuid(), Key = providerKey.Trim() };
                _dbContext.CertificateProviders.Add(provider);
            }

            provider.Name = request.Name.Trim();
            provider.IsActive = request.IsActive;
            provider.SandboxEnabled = request.SandboxEnabled;
            provider.SandboxBaseUrl = request.SandboxBaseUrl.Trim().TrimEnd('/');
            provider.ProductionBaseUrl = request.ProductionBaseUrl.Trim().TrimEnd('/');
            provider.DownloadBaseUrl = request.DownloadBaseUrl.Trim().TrimEnd('/');
            provider.RaCode = request.RaCode.Trim();
            provider.RequestTimeoutSeconds = request.RequestTimeoutSeconds is > 0 and <= 300 ? request.RequestTimeoutSeconds : 30;
            provider.MaxRetryAttempts = request.MaxRetryAttempts is >= 0 and <= 20 ? request.MaxRetryAttempts : 5;

            if (!string.IsNullOrWhiteSpace(request.SandboxConsumerKey))
                provider.EncryptedSandboxConsumerKey = _cryptoVault.EncryptPassword(request.SandboxConsumerKey);
            if (!string.IsNullOrWhiteSpace(request.SandboxConsumerSecret))
                provider.EncryptedSandboxConsumerSecret = _cryptoVault.EncryptPassword(request.SandboxConsumerSecret);
            if (!string.IsNullOrWhiteSpace(request.ProductionConsumerKey))
                provider.EncryptedProductionConsumerKey = _cryptoVault.EncryptPassword(request.ProductionConsumerKey);
            if (!string.IsNullOrWhiteSpace(request.ProductionConsumerSecret))
                provider.EncryptedProductionConsumerSecret = _cryptoVault.EncryptPassword(request.ProductionConsumerSecret);

            provider.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Ok(new { provider.Id, provider.Key, provider.Name, provider.UpdatedAt });
        }

        [HttpPost("providers/{providerKey}/test")]
        public async Task<IActionResult> TestProvider(
            string providerKey,
            [FromQuery] CertificateEnvironment environment = CertificateEnvironment.Sandbox,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var context = await _contextFactory.CreateAsync(providerKey, environment.ToString(), cancellationToken);
                var profiles = await _provider.GetProfilesAsync(context, cancellationToken);
                return Ok(new { Provider = providerKey, Environment = environment.ToString(), Success = true, Profiles = profiles.Count });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Provider = providerKey, Environment = environment.ToString(), Success = false, Message = ex.Message });
            }
        }

        [HttpPost("providers/{providerKey}/profiles/sync")]
    public async Task<IActionResult> SyncProfiles(
            string providerKey,
            [FromQuery] CertificateEnvironment environment = CertificateEnvironment.Production,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var provider = await _dbContext.CertificateProviders
                    .SingleOrDefaultAsync(item => item.Key == providerKey && item.IsActive, cancellationToken);
                if (provider == null)
                    return NotFound(new { Message = "No existe un proveedor de certificados activo con esa clave." });

            var context = await _contextFactory.CreateAsync(providerKey, environment.ToString(), cancellationToken);
            var externalProfiles = await _provider.GetProfilesAsync(context, cancellationToken);
            var now = DateTime.UtcNow;
            var synced = 0;

            foreach (var external in externalProfiles)
            {
                var profile = await _dbContext.CertificateProfiles
                    .SingleOrDefaultAsync(item => item.ProviderId == provider.Id
                        && item.Environment == environment
                        && item.ExternalCode == external.ExternalCode, cancellationToken);

                if (profile == null)
                {
                    profile = new CertificateProfile
                    {
                        Id = Guid.NewGuid(),
                        ProviderId = provider.Id,
                        Environment = environment,
                        ExternalCode = external.ExternalCode,
                        CreatedAt = now
                    };
                    _dbContext.CertificateProfiles.Add(profile);
                }

                profile.Key = BuildKey(provider.Key, environment, external.ExternalCode);
                profile.Title = external.Title;
                profile.Description = external.Description;
                profile.PersonType = external.EmailProperty;
                profile.ExternalType = external.ExternalType;
                profile.TokenType = external.TokenType;
                profile.ProviderValidityDays = external.ValidityDays;
                profile.TermsUrl = external.TermsUrl;
                profile.TermsHash = Hash(external.TermsUrl);
                profile.IsActive = true;
                profile.LastSyncedAt = now;
                profile.UpdatedAt = now;

                var fields = await _provider.GetProfileFieldsAsync(external.ExternalCode, context, cancellationToken);
                var receivedNames = fields.Select(field => field.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var persistedFields = await _dbContext.CertificateProfileFields
                    .Where(field => field.ProfileId == profile.Id)
                    .ToListAsync(cancellationToken);

                foreach (var field in persistedFields.Where(field => !receivedNames.Contains(field.ExternalName)))
                    field.IsUsed = false;

                foreach (var externalField in fields)
                {
                    var field = persistedFields.FirstOrDefault(item =>
                        item.ExternalName.Equals(externalField.Name, StringComparison.OrdinalIgnoreCase));
                    if (field == null)
                    {
                        field = new CertificateProfileField
                        {
                            Id = Guid.NewGuid(),
                            ProfileId = profile.Id,
                            ExternalName = externalField.Name
                        };
                        _dbContext.CertificateProfileFields.Add(field);
                    }

                    field.Label = externalField.Label;
                    field.Type = externalField.Type;
                    field.ValidationPattern = externalField.ValidationPattern;
                    field.DefaultValue = externalField.DefaultValue;
                    field.IsRequired = externalField.Required;
                    field.IsEditable = externalField.Editable;
                    field.IsUsed = externalField.Used;
                    field.DisplayOrder = externalField.Index;
                    field.DefinitionHash = Hash(string.Join("|", externalField.Name, externalField.Label,
                        externalField.Type, externalField.ValidationPattern, externalField.DefaultValue,
                        externalField.Required, externalField.Editable, externalField.Used, externalField.Index));
                    field.FetchedAt = now;
                }

                synced++;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
                return Ok(new { Provider = provider.Key, Environment = environment.ToString(), Profiles = synced, SynchronizedAt = now });
            }
            catch (CertificateProviderException ex)
            {
                _logger.LogWarning(ex, "No fue posible sincronizar perfiles del proveedor {ProviderKey} en {Environment}", providerKey, environment);
                return StatusCode(StatusCodes.Status502BadGateway, new
                {
                    Provider = providerKey,
                    Environment = environment.ToString(),
                    Success = false,
                    Code = ex.Code,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado sincronizando perfiles del proveedor {ProviderKey} en {Environment}", providerKey, environment);
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Provider = providerKey,
                    Environment = environment.ToString(),
                    Success = false,
                    Message = "No fue posible sincronizar los perfiles del proveedor."
                });
            }
        }

        private static string BuildKey(string providerKey, CertificateEnvironment environment, string externalCode)
            => $"{providerKey}:{environment}:{externalCode}";

        private static string Hash(string value)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty))).ToLowerInvariant();

        private static bool IsHttps(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
    }

    public sealed class CertificateProviderConfigurationRequest
    {
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public bool SandboxEnabled { get; set; }
        public string SandboxBaseUrl { get; set; } = string.Empty;
        public string ProductionBaseUrl { get; set; } = string.Empty;
        public string DownloadBaseUrl { get; set; } = string.Empty;
        public string RaCode { get; set; } = string.Empty;
        public string SandboxConsumerKey { get; set; } = string.Empty;
        public string SandboxConsumerSecret { get; set; } = string.Empty;
        public string ProductionConsumerKey { get; set; } = string.Empty;
        public string ProductionConsumerSecret { get; set; } = string.Empty;
        public int RequestTimeoutSeconds { get; set; } = 30;
        public int MaxRetryAttempts { get; set; } = 5;
    }
}
