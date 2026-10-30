using System;
using System.Collections.Generic;
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

        // --- Precios y saldo prepagado de certificados (ver CertificateBilling) ---

        [HttpGet("profiles")]
        public async Task<IActionResult> GetProfiles([FromQuery] CertificateEnvironment environment = CertificateEnvironment.Production, CancellationToken cancellationToken = default)
        {
            var profiles = await _dbContext.CertificateProfiles.AsNoTracking()
                .Where(p => p.Environment == environment && p.IsActive)
                .OrderBy(p => p.Title)
                .Select(p => new { p.Id, p.Title, p.Description, p.ExternalType, p.ProviderValidityDays, Environment = p.Environment.ToString() })
                .ToListAsync(cancellationToken);
            return Ok(profiles);
        }

        // Precios versionados: uno general por perfil (sin tenant) y, si se quiere, uno especial para un tenant. Crear un precio nuevo cierra la
        // vigencia del anterior del mismo perfil y tenant; los cargos ya hechos conservan su valor.
        [HttpGet("prices")]
        public async Task<IActionResult> GetPrices(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var prices = await _dbContext.CertificatePrices.AsNoTracking()
                .OrderBy(p => p.Profile.Title).ThenBy(p => p.TenantId != null).ThenByDescending(p => p.EffectiveFrom)
                .Select(p => new
                {
                    p.Id, p.ProfileId, ProfileTitle = p.Profile.Title, Environment = p.Environment.ToString(),
                    p.TenantId, TenantName = p.Tenant != null ? p.Tenant.CommercialName : null,
                    p.NetAmount, p.TaxRate, p.Currency, p.EffectiveFrom, p.EffectiveTo, p.IsActive,
                    IsCurrent = p.IsActive && p.EffectiveFrom <= now && (p.EffectiveTo == null || p.EffectiveTo > now)
                })
                .ToListAsync(cancellationToken);
            return Ok(prices.Select(p => new
            {
                p.Id, p.ProfileId, p.ProfileTitle, p.Environment, p.TenantId, p.TenantName, p.NetAmount, p.TaxRate, p.Currency,
                Total = p.NetAmount + Math.Round(p.NetAmount * p.TaxRate / 100m, 2),
                p.EffectiveFrom, p.EffectiveTo, p.IsActive, p.IsCurrent
            }));
        }

        [HttpPost("prices")]
        public async Task<IActionResult> CreatePrice([FromBody] CertificatePriceRequest request, CancellationToken cancellationToken = default)
        {
            if (request.NetAmount <= 0) return BadRequest(new { Message = "El precio debe ser mayor a cero." });
            if (request.TaxRate < 0 || request.TaxRate > 100) return BadRequest(new { Message = "El IVA debe estar entre 0 y 100." });

            var profile = await _dbContext.CertificateProfiles.SingleOrDefaultAsync(p => p.Id == request.ProfileId, cancellationToken);
            if (profile == null) return BadRequest(new { Message = "El perfil no existe." });
            if (request.TenantId.HasValue && !await _dbContext.Tenants.AnyAsync(t => t.Id == request.TenantId.Value, cancellationToken))
                return BadRequest(new { Message = "El tenant no existe." });

            var now = DateTime.UtcNow;
            var effectiveFrom = request.EffectiveFrom?.ToUniversalTime() ?? now;
            if (effectiveFrom < now.AddMinutes(-5)) effectiveFrom = now;

            // El precio anterior del mismo perfil y tenant termina donde empieza el nuevo; uno que aún no había empezado se reemplaza.
            var open = await _dbContext.CertificatePrices
                .Where(p => p.ProfileId == profile.Id && p.Environment == profile.Environment && p.TenantId == request.TenantId
                    && p.IsActive && (p.EffectiveTo == null || p.EffectiveTo > effectiveFrom))
                .ToListAsync(cancellationToken);
            foreach (var previous in open)
            {
                if (previous.EffectiveFrom >= effectiveFrom) previous.IsActive = false;
                else previous.EffectiveTo = effectiveFrom;
            }

            Guid.TryParse(User.FindFirst("UserId")?.Value, out var userId);
            var price = new CertificatePrice
            {
                Id = Guid.NewGuid(),
                ProviderId = profile.ProviderId,
                ProfileId = profile.Id,
                Environment = profile.Environment,
                TenantId = request.TenantId,
                NetAmount = Math.Round(request.NetAmount, 2),
                TaxRate = request.TaxRate,
                EffectiveFrom = effectiveFrom,
                IsActive = true,
                CreatedByUserId = userId == Guid.Empty ? null : userId,
                CreatedAt = now
            };
            _dbContext.CertificatePrices.Add(price);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Ok(new { price.Id });
        }

        // Termina la vigencia de un precio (desde ya). Sin precio vigente, ese perfil no se puede solicitar en producción.
        [HttpPost("prices/{id:guid}/close")]
        public async Task<IActionResult> ClosePrice(Guid id, CancellationToken cancellationToken = default)
        {
            var price = await _dbContext.CertificatePrices.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
            if (price == null) return NotFound();
            var now = DateTime.UtcNow;
            if (price.EffectiveFrom >= now) price.IsActive = false;
            else if (price.EffectiveTo == null || price.EffectiveTo > now) price.EffectiveTo = now;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Ok(new { price.Id, price.EffectiveTo, price.IsActive });
        }

        // Saldo de certificados de todos los tenants con el módulo, para ver de un vistazo quién tiene saldo y quién no.
        [HttpGet("balances")]
        public async Task<IActionResult> GetBalances(CancellationToken cancellationToken = default)
        {
            var tenants = await _dbContext.Tenants.AsNoTracking()
                .Where(t => t.CertificatesModuleEnabled)
                .OrderBy(t => t.CommercialName)
                .Select(t => new { t.Id, t.CommercialName, t.IsBillingExempt, t.InvoicingModuleEnabled })
                .ToListAsync(cancellationToken);
            var result = new List<object>();
            foreach (var tenant in tenants)
            {
                var balance = await CertificateBilling.GetBalanceAsync(_dbContext, tenant.Id, cancellationToken);
                result.Add(new
                {
                    TenantId = tenant.Id, TenantName = tenant.CommercialName, tenant.IsBillingExempt,
                    CertificatesOnly = !tenant.InvoicingModuleEnabled,
                    balance.Available, balance.Reserved, balance.Consumed, balance.Deposited
                });
            }
            return Ok(result);
        }

        [HttpGet("balances/{tenantId:guid}")]
        public async Task<IActionResult> GetBalance(Guid tenantId, CancellationToken cancellationToken = default)
        {
            var tenant = await _dbContext.Tenants.AsNoTracking().SingleOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
            if (tenant == null) return NotFound();
            var balance = await CertificateBilling.GetBalanceAsync(_dbContext, tenantId, cancellationToken);
            var movements = await _dbContext.CertificateBalanceMovements.AsNoTracking()
                .Where(m => m.TenantId == tenantId)
                .OrderByDescending(m => m.OccurredAt).ThenByDescending(m => m.CreatedAt)
                .Take(300)
                .Select(m => new
                {
                    m.Id, Type = m.Type.ToString(), m.Amount, m.Reference, m.Notes, m.OccurredAt, m.CreatedBy,
                    ClientName = m.CertificateCharge == null ? null
                        : (string.IsNullOrWhiteSpace(m.CertificateCharge.Client.CommercialName) ? m.CertificateCharge.Client.CompanyName : m.CertificateCharge.Client.CommercialName)
                })
                .ToListAsync(cancellationToken);
            return Ok(new
            {
                balance.Available, balance.Reserved, balance.Consumed, balance.Deposited,
                tenant.IsBillingExempt, Movements = movements
            });
        }

        // Abono (pago recibido del tenant) o ajuste (corrección, positiva o negativa). Un ajuste no puede dejar el saldo disponible en negativo.
        [HttpPost("balances/{tenantId:guid}/movements")]
        public async Task<IActionResult> AddBalanceMovement(Guid tenantId, [FromBody] CertificateBalanceMovementRequest request, CancellationToken cancellationToken = default)
        {
            if (!await _dbContext.Tenants.AnyAsync(t => t.Id == tenantId, cancellationToken)) return NotFound();
            var isDeposit = string.Equals(request.Type, "Deposit", StringComparison.OrdinalIgnoreCase);
            var isAdjustment = string.Equals(request.Type, "Adjustment", StringComparison.OrdinalIgnoreCase);
            if (!isDeposit && !isAdjustment) return BadRequest(new { Message = "El tipo debe ser abono o ajuste." });
            var amount = Math.Round(request.Amount, 2);
            if (isDeposit && amount <= 0) return BadRequest(new { Message = "El abono debe ser mayor a cero." });
            if (isAdjustment && amount == 0) return BadRequest(new { Message = "El ajuste no puede ser cero." });
            if (string.IsNullOrWhiteSpace(request.Reference)) return BadRequest(new { Message = "Indica la referencia del pago o del ajuste." });

            var now = DateTime.UtcNow;
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            await CertificateBilling.LockTenantBalanceAsync(_dbContext, tenantId, cancellationToken);
            if (amount < 0)
            {
                var balance = await CertificateBilling.GetBalanceAsync(_dbContext, tenantId, cancellationToken);
                if (balance.Available + amount < 0)
                    return BadRequest(new { Message = "El ajuste dejaría el saldo disponible en negativo." });
            }
            var movement = new CertificateBalanceMovement
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Type = isDeposit ? CertificateBalanceMovementType.Deposit : CertificateBalanceMovementType.Adjustment,
                Amount = amount,
                Reference = request.Reference.Trim()[..Math.Min(request.Reference.Trim().Length, 200)],
                Notes = (request.Notes ?? string.Empty).Trim()[..Math.Min((request.Notes ?? string.Empty).Trim().Length, 1000)],
                OccurredAt = request.OccurredAt?.ToUniversalTime() ?? now,
                CreatedBy = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "superadmin",
                CreatedAt = now
            };
            _dbContext.CertificateBalanceMovements.Add(movement);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(new { movement.Id });
        }

        private static string BuildKey(string providerKey, CertificateEnvironment environment, string externalCode)
            => $"{providerKey}:{environment}:{externalCode}";

        private static string Hash(string value)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty))).ToLowerInvariant();

        private static bool IsHttps(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
    }

    public sealed class CertificatePriceRequest
    {
        public Guid ProfileId { get; set; }
        // Nulo = precio general; con tenant = precio especial para ese tenant.
        public Guid? TenantId { get; set; }
        public decimal NetAmount { get; set; }
        // Porcentaje (19 = 19 %).
        public decimal TaxRate { get; set; }
        public DateTime? EffectiveFrom { get; set; }
    }

    public sealed class CertificateBalanceMovementRequest
    {
        // "Deposit" (abono) o "Adjustment" (ajuste, con signo).
        public string Type { get; set; } = "Deposit";
        public decimal Amount { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public DateTime? OccurredAt { get; set; }
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
