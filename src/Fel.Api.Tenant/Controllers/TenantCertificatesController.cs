using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Certificates;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace Fel.Api.Tenant.Controllers
{
    [ApiController]
    [Route("api/tenant/clients/{clientId}/certificate")]
    public class TenantCertificatesController : ControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly ICertificateStorageService _storageService;
        private readonly ICryptoVault _cryptoVault;
        private readonly ICertificateProvider _provider;
        private readonly ICertificateProviderContextFactory _contextFactory;
        private readonly ICertificateCsrService _csrService;
        private readonly ILogger<TenantCertificatesController> _logger;

        public TenantCertificatesController(
            FelDbContext dbContext,
            ICertificateStorageService storageService,
            ICryptoVault cryptoVault,
            ICertificateProvider provider,
            ICertificateProviderContextFactory contextFactory,
            ICertificateCsrService csrService,
            ILogger<TenantCertificatesController> logger)
        {
            _dbContext = dbContext;
            _storageService = storageService;
            _cryptoVault = cryptoVault;
            _provider = provider;
            _contextFactory = contextFactory;
            _csrService = csrService;
            _logger = logger;
        }

        private static string? SummarizeProviderError(string? body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            try
            {
                using var document = System.Text.Json.JsonDocument.Parse(body);
                var root = document.RootElement;
                foreach (var propertyName in new[] { "message", "error_description", "description", "detail", "error", "code" })
                {
                    if (root.ValueKind == System.Text.Json.JsonValueKind.Object &&
                        root.TryGetProperty(propertyName, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(value.GetString()))
                    {
                        var summary = value.GetString()!;
                        return summary[..Math.Min(summary.Length, 500)];
                    }
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // No se expone una respuesta arbitraria que pudiera repetir datos del CSR.
            }
            return null;
        }

        private Guid GetCurrentTenantId()
        {
            if (Request.Headers.TryGetValue("x-tenant-id", out var tenantIdStr))
            {
                if (Guid.TryParse(tenantIdStr, out var tenantId))
                    return tenantId;
            }
            throw new UnauthorizedAccessException("x-tenant-id Header is missing");
        }

        // Vista consolidada de todos los certificados de los clientes de este tenant, ordenada
        // por fecha de vencimiento — para que el tenant vea de un vistazo qué se vence pronto.
        [HttpGet]
        [Route("~/api/tenant/certificates")]
        public async Task<IActionResult> GetAllCertificates()
        {
            var tenantId = GetCurrentTenantId();
            var certificates = await _dbContext.Set<Certificate>()
                // El que solo se entrega (no instalado para firmar) no está "activo" pero sí es el vigente del titular.
                .Where(c => (c.IsActive || (c.CertificateRequestId != null && c.Status == CertificateStatus.Current)) && c.Client.TenantId == tenantId)
                .OrderBy(c => c.ExpirationDate)
                .Select(c => new
                {
                    c.Id,
                    c.ClientId,
                    Installed = c.IsActive,
                    ClientName = string.IsNullOrWhiteSpace(c.Client.CommercialName) ? c.Client.CompanyName : c.Client.CommercialName,
                    c.FileName,
                    c.ExpirationDate,
                    c.CreatedAt,
                    Environment = c.Environment.ToString(),
                    // Solo los emitidos por la plataforma se descargan desde el portal.
                    Downloadable = c.CertificateRequestId != null
                })
                .ToListAsync();

            return Ok(certificates);
        }

        // Todas las solicitudes de certificado del tenant (de todos sus clientes o titulares), las más recientes primero, con su cobro.
        [HttpGet]
        [Route("~/api/tenant/certificates/requests")]
        public async Task<IActionResult> GetRequests()
        {
            var tenantId = GetCurrentTenantId();
            var requests = await _dbContext.CertificateRequests.AsNoTracking()
                .Where(r => r.TenantId == tenantId)
                .OrderByDescending(r => r.CreatedAt)
                .Take(500)
                .Select(r => new
                {
                    r.Id,
                    r.ClientId,
                    ClientName = string.IsNullOrWhiteSpace(r.Client.CommercialName) ? r.Client.CompanyName : r.Client.CommercialName,
                    ClientTaxId = r.Client.TaxId,
                    ProfileTitle = r.Profile.Title,
                    Environment = r.Environment.ToString(),
                    Status = r.Status.ToString(),
                    r.ProviderStatus,
                    r.KycUrl,
                    r.LastErrorMessage,
                    r.CreatedAt,
                    r.CompletedAt,
                    Charge = r.Charges.OrderByDescending(c => c.CreatedAt)
                        .Select(c => new { c.TotalAmount, Status = c.Status.ToString() }).FirstOrDefault(),
                    Certificate = _dbContext.Certificates.Where(c => c.CertificateRequestId == r.Id)
                        .Select(c => new { c.Id, c.ExpirationDate }).FirstOrDefault(),
                    // Correo con el .p12 al titular: la contraseña sale solo cuando este quedó enviado.
                    HolderEmail = _dbContext.EmailOutbox.Where(e => e.CertificateRequestId == r.Id && e.Kind == EmailKind.Certificate)
                        .OrderByDescending(e => e.CreatedAt)
                        .Select(e => new { Status = e.Status.ToString(), e.RecipientEmail, e.Channel, e.SentAt, e.LastError }).FirstOrDefault()
                })
                .ToListAsync();
            return Ok(requests);
        }

        // Venta de certificados: los clientes y titulares del tenant que pueden comprar certificados, cada uno con su certificado vigente de
        // producción y su última solicitud.
        [HttpGet]
        [Route("~/api/tenant/certificates/sales")]
        public async Task<IActionResult> GetSales()
        {
            var tenantId = GetCurrentTenantId();
            var modules = await Fel.Infrastructure.Security.TenantModules.GetAsync(_dbContext, tenantId);
            var clients = await _dbContext.Clients.AsNoTracking()
                .Where(c => c.TenantId == tenantId && c.IsActive && !c.IsDeveloperSandbox)
                .Select(c => new
                {
                    c.Id,
                    Name = string.IsNullOrWhiteSpace(c.CommercialName) ? c.CompanyName : c.CommercialName,
                    c.TaxId,
                    c.Email,
                    c.InvoicingEnabled,
                    c.CertificatePurchaseEnabled,
                    IntegratorCode = c.Integrator.Code,
                    Blocked = c.BlockedAt != null,
                    Certificate = _dbContext.Certificates
                        .Where(x => x.ClientId == c.Id && x.Environment == CertificateEnvironment.Production
                            && (x.IsActive || (x.CertificateRequestId != null && x.Status == CertificateStatus.Current)))
                        .OrderByDescending(x => x.IsActive).ThenByDescending(x => x.CreatedAt)
                        .Select(x => new { x.Id, x.ExpirationDate, Installed = x.IsActive, Downloadable = x.CertificateRequestId != null })
                        .FirstOrDefault(),
                    LastRequest = _dbContext.CertificateRequests
                        .Where(r => r.ClientId == c.Id && r.Environment == CertificateEnvironment.Production)
                        .OrderByDescending(r => r.CreatedAt)
                        .Select(r => new { Status = r.Status.ToString(), r.CreatedAt })
                        .FirstOrDefault()
                })
                .ToListAsync();

            // Mismo criterio que CertificatePurchase.IsEnabledAsync, sin una consulta por cliente.
            var sales = clients
                .Where(c => !c.InvoicingEnabled || !modules.Invoicing
                    || (c.CertificatePurchaseEnabled ?? c.IntegratorCode != "DATAICO"))
                .Select(c => new
                {
                    c.Id, c.Name, c.TaxId, c.Email, c.Blocked,
                    Kind = !c.InvoicingEnabled || !modules.Invoicing ? "Titular" : (c.IntegratorCode == "DATAICO" ? "Dataico" : "Nativo"),
                    c.Certificate, c.LastRequest
                })
                .OrderBy(c => c.Name)
                .ToList();
            return Ok(sales);
        }

        // Saldo prepagado de certificados: disponible, reservado (solicitudes en curso), consumido y los movimientos; además el precio vigente
        // de cada perfil de producción para este tenant.
        [HttpGet]
        [Route("~/api/tenant/certificates/balance")]
        public async Task<IActionResult> GetBalance()
        {
            var tenantId = GetCurrentTenantId();
            var tenant = await _dbContext.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenantId);
            var balance = await CertificateBilling.GetBalanceAsync(_dbContext, tenantId);
            var movements = await _dbContext.CertificateBalanceMovements.AsNoTracking()
                .Where(m => m.TenantId == tenantId)
                .OrderByDescending(m => m.OccurredAt).ThenByDescending(m => m.CreatedAt)
                .Take(300)
                .Select(m => new
                {
                    m.Id, Type = m.Type.ToString(), m.Amount, m.Reference, m.Notes, m.OccurredAt,
                    ClientName = m.CertificateCharge == null ? null
                        : (string.IsNullOrWhiteSpace(m.CertificateCharge.Client.CommercialName) ? m.CertificateCharge.Client.CompanyName : m.CertificateCharge.Client.CommercialName)
                })
                .ToListAsync();

            var profiles = await _dbContext.CertificateProfiles.AsNoTracking()
                .Where(p => p.Environment == CertificateEnvironment.Production && p.IsActive && p.Provider.IsActive)
                .OrderBy(p => p.Title)
                .Select(p => new { p.Id, p.Title })
                .ToListAsync();
            var prices = new List<object>();
            foreach (var profile in profiles)
            {
                var price = await CertificateBilling.ResolvePriceAsync(_dbContext, tenantId, profile.Id, CertificateEnvironment.Production);
                prices.Add(new
                {
                    ProfileId = profile.Id, profile.Title,
                    NetAmount = price?.NetAmount, TaxRate = price?.TaxRate, Total = price == null ? (decimal?)null : CertificateBilling.Total(price)
                });
            }

            return Ok(new
            {
                balance.Available, balance.Reserved, balance.Consumed, balance.Deposited,
                Exempt = tenant.IsBillingExempt,
                Movements = movements,
                Prices = prices
            });
        }

        [HttpGet]
        [Route("~/api/tenant/certificates/settings")]
        public async Task<IActionResult> GetSettings()
        {
            var tenantId = GetCurrentTenantId();
            var send = await _dbContext.Tenants.Where(t => t.Id == tenantId).Select(t => t.SendCertificateToHolderByEmail).SingleAsync();
            return Ok(new { sendToHolderByEmail = send });
        }

        [HttpPut]
        [Route("~/api/tenant/certificates/settings")]
        public async Task<IActionResult> SaveSettings([FromBody] CertificateSettingsRequest request)
        {
            var tenantId = GetCurrentTenantId();
            var tenant = await _dbContext.Tenants.SingleAsync(t => t.Id == tenantId);
            tenant.SendCertificateToHolderByEmail = request.SendToHolderByEmail;
            await _dbContext.SaveChangesAsync();
            return Ok(new { sendToHolderByEmail = tenant.SendCertificateToHolderByEmail });
        }

        // Descarga del .p12 emitido por la plataforma (incluye la clave privada). La contraseña se consulta aparte; ambas quedan auditadas.
        // Solo los emitidos por la plataforma: uno cargado a mano ya lo tiene quien lo cargó.
        [HttpGet]
        [Route("~/api/tenant/certificates/{certificateId:guid}/download")]
        public async Task<IActionResult> DownloadCertificate(Guid certificateId)
        {
            var tenantId = GetCurrentTenantId();
            var certificate = await _dbContext.Certificates.Include(c => c.Client)
                .SingleOrDefaultAsync(c => c.Id == certificateId && c.Client.TenantId == tenantId && c.CertificateRequestId != null);
            if (certificate == null) return NotFound();

            var bytes = await _storageService.GetCertificateBytesAsync(certificate.FileName);
            AuditDelivery(certificate, CertificateEventType.DownloadedByTenant);
            await _dbContext.SaveChangesAsync();

            var safeName = new string($"{certificate.Client.CompanyName}-{certificate.Client.TaxId}".Select(ch => char.IsLetterOrDigit(ch) || ch == '-' ? ch : '_').ToArray());
            return File(bytes, "application/x-pkcs12", $"{safeName}.p12");
        }

        [HttpGet]
        [Route("~/api/tenant/certificates/{certificateId:guid}/password")]
        public async Task<IActionResult> GetCertificatePassword(Guid certificateId)
        {
            var tenantId = GetCurrentTenantId();
            var certificate = await _dbContext.Certificates
                .SingleOrDefaultAsync(c => c.Id == certificateId && c.Client.TenantId == tenantId && c.CertificateRequestId != null);
            if (certificate == null) return NotFound();

            var password = _cryptoVault.DecryptPassword(certificate.EncryptedPassword);
            AuditDelivery(certificate, CertificateEventType.PasswordViewedByTenant);
            await _dbContext.SaveChangesAsync();
            return Ok(new { password });
        }

        private void AuditDelivery(Certificate certificate, CertificateEventType eventType)
        {
            _dbContext.CertificateEvents.Add(new CertificateEvent
            {
                Id = Guid.NewGuid(),
                CertificateRequestId = certificate.CertificateRequestId!.Value,
                CertificateId = certificate.Id,
                EventType = eventType,
                ActorType = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "tenant",
                CorrelationId = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                OccurredAt = DateTime.UtcNow
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetCertificate(Guid clientId)
        {
            var tenantId = GetCurrentTenantId();
            var clientExists = await _dbContext.Clients.AnyAsync(c => c.Id == clientId && c.TenantId == tenantId);
            if (!clientExists) return Forbid();

            var certificate = await _dbContext.Set<Certificate>()
                .Where(c => c.ClientId == clientId && c.Environment == CertificateEnvironment.Production
                    && (c.IsActive || (c.CertificateRequestId != null && c.Status == CertificateStatus.Current)))
                .OrderByDescending(c => c.IsActive).ThenByDescending(c => c.CreatedAt)
                .Select(c => new {
                    c.FileName,
                    c.ExpirationDate,
                    c.CreatedAt,
                    Installed = c.IsActive
                })
                .FirstOrDefaultAsync();

            if (certificate == null) return NotFound();
            return Ok(certificate);
        }

        [HttpGet("options")]
        public async Task<IActionResult> GetCertificateOptions(Guid clientId, [FromQuery] CertificateEnvironment environment = CertificateEnvironment.Sandbox)
        {
            var tenantId = GetCurrentTenantId();
            var client = await _dbContext.Clients.AsNoTracking()
                .SingleOrDefaultAsync(c => c.Id == clientId && c.TenantId == tenantId);
            if (client == null) return Forbid();

            var provider = await _dbContext.CertificateProviders
                .AsNoTracking()
                .SingleOrDefaultAsync(p => p.Key == "viafirma" && p.IsActive);
            if (provider == null) return NotFound(new { Message = "El proveedor de certificados no está disponible." });
            if (environment == CertificateEnvironment.Sandbox && !provider.SandboxEnabled)
                return BadRequest(new { Message = "El entorno Sandbox está deshabilitado por Administración." });

            var profiles = await _dbContext.CertificateProfiles
                .AsNoTracking()
                .Where(p => p.ProviderId == provider.Id && p.Environment == environment && p.IsActive
                    && (client.PersonType == "PN"
                        ? (p.ExternalType.Contains("INDIVIDUAL") || p.ExternalCode.Contains("PN"))
                        : (p.ExternalType.Contains("CORPORAT") || p.ExternalCode.Contains("PJ"))))
                .OrderBy(p => p.Title)
                .Select(p => new
                {
                    p.Id, p.Title, p.Description, p.ExternalType, p.ProviderValidityDays, p.TermsUrl,
                    Fields = p.Fields.Where(f => f.IsUsed).OrderBy(f => f.DisplayOrder)
                        .Select(f => new { f.ExternalName, f.Label, f.Type, f.ValidationPattern, f.DefaultValue, f.IsRequired, f.IsEditable })
                })
                .ToListAsync();
            var certificates = await _dbContext.Certificates
                .AsNoTracking()
                .Where(c => c.ClientId == clientId && c.Environment == environment)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new { c.Id, c.FileName, c.ExpirationDate, c.Status, c.IsActive, c.CreatedAt })
                .ToListAsync();
            var requests = await _dbContext.CertificateRequests
                .AsNoTracking()
                .Where(r => r.ClientId == clientId && r.Environment == environment)
                .OrderByDescending(r => r.CreatedAt)
                .Take(10)
                .Select(r => new { r.Id, r.ProfileId, r.Status, r.ProviderStatus, r.KycUrl, r.LastErrorMessage, r.CreatedAt, r.UpdatedAt })
                .ToListAsync();

            // Precio de cada perfil para este tenant y su saldo prepagado: el formulario muestra cuánto cuesta antes de solicitar.
            var tenant = await _dbContext.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenantId);
            var requiresPayment = CertificateBilling.RequiresPayment(tenant, environment);
            var prices = new Dictionary<Guid, decimal?>();
            foreach (var profile in profiles)
            {
                var price = requiresPayment ? await CertificateBilling.ResolvePriceAsync(_dbContext, tenantId, profile.Id, environment) : null;
                prices[profile.Id] = price == null ? null : CertificateBilling.Total(price);
            }
            var balance = requiresPayment ? await CertificateBilling.GetBalanceAsync(_dbContext, tenantId) : null;

            return Ok(new
            {
                Environment = environment.ToString(), SandboxEnabled = provider.SandboxEnabled, Profiles = profiles, Certificates = certificates, Requests = requests,
                RequiresPayment = requiresPayment, Prices = prices, AvailableBalance = balance?.Available,
                PurchaseEnabled = await CertificatePurchase.IsEnabledAsync(_dbContext, client),
                CanInstall = await CertificatePurchase.CanInstallAsync(_dbContext, client)
            });
        }

        [HttpPost("requests")]
        public async Task<IActionResult> CreateCertificateRequest(Guid clientId, [FromBody] CreateCertificateRequestRequest request, CancellationToken cancellationToken = default)
        {
            var tenantId = GetCurrentTenantId();
            var client = await _dbContext.Clients.SingleOrDefaultAsync(c => c.Id == clientId && c.TenantId == tenantId, cancellationToken);
            if (client == null) return Forbid();
            if (!await CertificatePurchase.IsEnabledAsync(_dbContext, client, cancellationToken))
                return StatusCode(403, new { Code = "certificate_purchase_disabled", Message = CertificatePurchase.DisabledMessage });
            if (!request.AcceptTerms) return BadRequest(new { Message = "Debes aceptar los términos del certificado." });

            var provider = await _dbContext.CertificateProviders.SingleOrDefaultAsync(p => p.Key == "viafirma" && p.IsActive, cancellationToken);
            if (provider == null) return BadRequest(new { Message = "El proveedor de certificados no está disponible." });
            if (request.Environment == CertificateEnvironment.Sandbox && !provider.SandboxEnabled)
                return BadRequest(new { Message = "El entorno Sandbox está deshabilitado por Administración." });

            var profile = await _dbContext.CertificateProfiles.SingleOrDefaultAsync(p => p.Id == request.ProfileId && p.ProviderId == provider.Id && p.Environment == request.Environment && p.IsActive, cancellationToken);
            if (profile == null) return BadRequest(new { Message = "El perfil seleccionado no está disponible para ese entorno." });
            var profileFields = await _dbContext.CertificateProfileFields.AsNoTracking()
                .Where(f => f.ProfileId == profile.Id && f.IsUsed).ToListAsync(cancellationToken);
            var formValues = new Dictionary<string, string>(request.FormValues ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
            var isCorporate = client.PersonType != "PN";
            // Estos valores son propiedad del cliente. El formulario web no puede reemplazarlos
            // y tampoco se aceptan alterados desde la petición HTTP.
            formValues["district"] = client.City;
            // Unidad Organizacional del representante legal (ej. "Gerencia General") — no confundir
            // con el departamento geográfico (OrganizationDepartment), que nunca debió mandarse acá:
            // el patrón de Viafirma para este campo no admite comas ni puntos.
            formValues["departament"] = string.IsNullOrWhiteSpace(client.LegalRepresentativeOrganizationalArea)
                ? "FACTURACION ELECTRONICA"
                : client.LegalRepresentativeOrganizationalArea.Trim();
            formValues["state"] = client.OrganizationDepartment ?? client.City;
            formValues["addressCorp"] = client.Address;
            formValues["address"] = client.Address;
            formValues["legalNameCorp"] = client.CompanyName;
            formValues["name"] = isCorporate
                ? string.Join(" ", new[] { client.LegalRepresentativeFirstName, client.LegalRepresentativeOtherNames }.Where(v => !string.IsNullOrWhiteSpace(v)))
                : client.CompanyName;
            formValues["lastName"] = string.Join(" ", new[] { client.LegalRepresentativeFirstLastName, client.LegalRepresentativeSecondLastName }.Where(v => !string.IsNullOrWhiteSpace(v)));
            formValues["identity"] = isCorporate ? client.LegalRepresentativeDocumentNumber ?? string.Empty : client.TaxId;
            formValues["dnAlternativo1"] = client.TaxId;
            formValues["email"] = isCorporate ? client.LegalRepresentativeEmail ?? client.Email : client.Email;
            formValues["countryCode"] = string.IsNullOrWhiteSpace(client.LegalRepresentativeDocumentCountryCode)
                ? "CO"
                : client.LegalRepresentativeDocumentCountryCode.Trim();
            formValues["identityType"] = isCorporate
                ? (client.LegalRepresentativeDocumentType is "CC" or "IDC" ? "IDC" : client.LegalRepresentativeDocumentType ?? "IDC")
                : "IDC";
            formValues["dnAlternativo2"] = client.OrganizationType ?? "RM";
            var validationErrors = profileFields
                .Where(f => f.IsRequired && (!formValues.TryGetValue(f.ExternalName, out var value) || string.IsNullOrWhiteSpace(value)))
                .Select(f => f.Label).ToList();
            if (validationErrors.Count > 0)
                return BadRequest(new { Code = "certificate_profile_fields_required", Message = "Completa los campos obligatorios del perfil.", Fields = validationErrors });
            foreach (var field in profileFields.Where(f => !string.IsNullOrWhiteSpace(f.ValidationPattern)))
            {
                if (!formValues.TryGetValue(field.ExternalName, out var fieldValue) || string.IsNullOrWhiteSpace(fieldValue)) continue;
                try
                {
                    if (!Regex.IsMatch(fieldValue, field.ValidationPattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250)))
                        return BadRequest(new { Code = "certificate_profile_field_invalid", Message = $"Revisa el campo {field.Label}.", Field = field.ExternalName });
                }
                catch (ArgumentException)
                {
                    _logger.LogWarning("Viafirma entregÃ³ una expresiÃ³n invÃ¡lida para {FieldName} en perfil {ProfileId}", field.ExternalName, profile.Id);
                }
                catch (RegexMatchTimeoutException)
                {
                    return BadRequest(new { Code = "certificate_profile_field_validation_timeout", Message = $"No fue posible validar el campo {field.Label}." });
                }
            }
            var pending = await _dbContext.CertificateRequests.AnyAsync(r => r.ClientId == clientId && r.Environment == request.Environment && r.Status != CertificateRequestStatus.Active && r.Status != CertificateRequestStatus.Rejected && r.Status != CertificateRequestStatus.Failed && r.Status != CertificateRequestStatus.Revoked, cancellationToken);
            if (pending) return Conflict(new { Message = "Ya existe una solicitud en curso para este cliente y ambiente." });

            GeneratedCertificateRequest subject;
            try
            {
                var isCorporateProfile = profile.ExternalType.Contains("CORPORAT", StringComparison.OrdinalIgnoreCase) || profile.ExternalCode.Contains("PJ", StringComparison.OrdinalIgnoreCase);
                if (isCorporateProfile != isCorporate)
                    return BadRequest(new { Code = "certificate_profile_person_type_mismatch", Message = "El perfil de certificado no corresponde al tipo de persona del cliente." });
                subject = _csrService.Generate(new CertificateSubject(
                    "CO",
                    GetField("state", client.City),
                    GetField("district", client.City),
                    GetField(isCorporateProfile ? "addressCorp" : "address", client.Address),
                    GetField(isCorporateProfile ? "legalNameCorp" : "name", client.CompanyName),
                    isCorporateProfile ? GetField("departament", "FACTURACION ELECTRONICA") : "",
                    GetField(isCorporateProfile ? "dnAlternativo1" : "identity", client.TaxId),
                    GetField("email", client.Email),
                    GetField("name", client.CompanyName),
                    GetField("lastName", client.CompanyName),
                    isCorporateProfile
                        ? $"{GetField("legalNameCorp", client.CompanyName)} - {GetField("state", client.City)}"
                        : $"{GetField("name", client.CompanyName)} {GetField("lastName", client.CompanyName)} - {GetField("identity", client.TaxId)}"));

                string GetField(string key, string fallback = "") =>
                    formValues.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : fallback;
            }
            catch (Exception ex) when (ex is InvalidOperationException or CryptographicException or ArgumentException)
            {
                _logger.LogError(ex, "No se pudo generar el CSR para el cliente {ClientId} en {Environment}", clientId, request.Environment);
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Provider = provider.Key,
                    Environment = request.Environment.ToString(),
                    Success = false,
                    Code = "certificate_csr_configuration",
                    Message = ex.Message
                });
            }
            CertificateProviderContext context;
            try
            {
                context = await _contextFactory.CreateAsync(provider.Key, request.Environment.ToString(), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo configurar el proveedor de certificados {ProviderKey} en {Environment}", provider.Key, request.Environment);
                return StatusCode(StatusCodes.Status502BadGateway, new
                {
                    Provider = provider.Key,
                    Environment = request.Environment.ToString(),
                    Success = false,
                    Code = "certificate_provider_configuration",
                    Message = "El proveedor de certificados no está configurado para este ambiente. Contacta a Administración."
                });
            }
            string GetRequestField(string key, string fallback = "") =>
                formValues.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : fallback;

            // La solicitud se guarda como borrador antes de llamar a Viafirma: así, si es de producción, el precio queda reservado del
            // saldo prepagado del tenant en la misma transacción y ninguna solicitud cobrable llega al proveedor sin saldo.
            var now = DateTime.UtcNow;
            var actor = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "tenant";
            var certificateRequest = new CertificateRequest
            {
                Id = Guid.NewGuid(), TenantId = tenantId, ClientId = clientId, ProfileId = profile.Id,
                // Se instala para firmar solo si el cliente puede (nativo que factura) y el tenant no pidió solo entregarlo.
                InstallOnPlatform = request.InstallOnPlatform != false && await CertificatePurchase.CanInstallAsync(_dbContext, client, cancellationToken),
                Environment = request.Environment, RequestType = CertificateRequestType.Initial,
                ProviderStatus = string.Empty, Status = CertificateRequestStatus.Draft,
                CsrHash = Convert.ToHexString(SHA256.HashData(Convert.FromBase64String(subject.CsrBase64))).ToLowerInvariant(),
                PublicKeyHash = subject.PublicKeyHash, EncryptedPrivateKey = subject.EncryptedPrivateKey,
                KeyAlgorithm = subject.Algorithm, KeySize = subject.KeySize, TermsUrl = profile.TermsUrl,
                TermsHash = profile.TermsHash, TermsAcceptedAt = now, TermsAcceptedIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                TermsAcceptedUserAgent = Request.Headers.UserAgent.ToString(), CreatedAt = now, UpdatedAt = now,
                IdempotencyKey = $"tenant:{tenantId}:client:{clientId}:{request.Environment}:{profile.Id}:{now:yyyyMMddHHmmssfff}"
            };

            var tenant = await _dbContext.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenantId, cancellationToken);
            CertificatePrice? price = null;
            if (CertificateBilling.RequiresPayment(tenant, request.Environment))
            {
                price = await CertificateBilling.ResolvePriceAsync(_dbContext, tenantId, profile.Id, request.Environment, cancellationToken);
                if (price == null)
                    return BadRequest(new { Code = "certificate_price_unavailable", Message = "No hay un precio vigente para este tipo de certificado. Contacta al administrador." });
            }

            await using (var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken))
            {
                if (price != null)
                {
                    await CertificateBilling.LockTenantBalanceAsync(_dbContext, tenantId, cancellationToken);
                    var balance = await CertificateBilling.GetBalanceAsync(_dbContext, tenantId, cancellationToken);
                    var total = CertificateBilling.Total(price);
                    if (balance.Available < total)
                        return StatusCode(StatusCodes.Status402PaymentRequired, new
                        {
                            Code = "certificate_insufficient_balance",
                            Message = $"Saldo insuficiente: este certificado cuesta {FormatCop(total)} y tu saldo disponible es {FormatCop(balance.Available)}. Abona saldo con tu proveedor para continuar.",
                            Price = total,
                            Available = balance.Available
                        });
                }

                _dbContext.CertificateRequests.Add(certificateRequest);
                _dbContext.CertificateEvents.Add(new CertificateEvent
                {
                    Id = Guid.NewGuid(), CertificateRequestId = certificateRequest.Id, EventType = CertificateEventType.RequestCreated,
                    ToStatus = CertificateRequestStatus.Draft, ActorType = actor, OccurredAt = now
                });
                if (price != null)
                    CertificateBilling.Reserve(_dbContext, certificateRequest, price, profile.Title, actor);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }

            ProviderRequestCreated providerRequest;
            try
            {
                var isCorporateProfile = profile.ExternalType.Contains("CORPORAT", StringComparison.OrdinalIgnoreCase) || profile.ExternalCode.Contains("PJ", StringComparison.OrdinalIgnoreCase);
                providerRequest = await _provider.CreateRequestFromCsrAsync(new CreateProviderRequest(
                    GetRequestField("identityType"), GetRequestField("countryCode"), GetRequestField("identity"),
                    provider.RaCode, profile.ExternalCode, GetRequestField("email", client.Email),
                    isCorporateProfile ? GetRequestField("dnAlternativo2") : string.Empty,
                    subject.CsrBase64), context, cancellationToken);
            }
            catch (Exception ex)
            {
                // No llegó a Viafirma: la solicitud queda fallida y se devuelve lo reservado.
                var providerException = ex as CertificateProviderException;
                certificateRequest.Status = CertificateRequestStatus.Failed;
                certificateRequest.LastErrorCode = providerException?.Code ?? "certificate_provider_unavailable";
                certificateRequest.LastErrorMessage = ex.Message[..Math.Min(ex.Message.Length, 1000)];
                certificateRequest.UpdatedAt = DateTime.UtcNow;
                await CertificateBilling.ReleaseAsync(_dbContext, certificateRequest.Id, "no se pudo enviar a Viafirma", actor, CancellationToken.None);
                await _dbContext.SaveChangesAsync(CancellationToken.None);

                if (providerException == null)
                {
                    _logger.LogError(ex, "No se pudo enviar la solicitud de certificado a Viafirma. Environment={Environment} Profile={ProfileId}", request.Environment, profile.Id);
                    return StatusCode(StatusCodes.Status502BadGateway, new
                    {
                        Provider = provider.Key,
                        Environment = request.Environment.ToString(),
                        Success = false,
                        Code = "certificate_provider_unavailable",
                        Message = "No fue posible comunicarse con Viafirma. Intenta de nuevo en unos minutos; no se descontó saldo."
                    });
                }
                _logger.LogWarning("Viafirma rechazó la solicitud. Environment={Environment} Profile={ProfileId} Code={Code} HttpStatus={HttpStatus}",
                    request.Environment, profile.Id, providerException.Code, providerException.HttpStatus);
                return StatusCode(StatusCodes.Status502BadGateway, new
                {
                    Provider = provider.Key,
                    Environment = request.Environment.ToString(),
                    Success = false,
                    Code = providerException.Code,
                    ProviderHttpStatus = providerException.HttpStatus,
                    Message = "Viafirma rechazó la solicitud. Revisa los datos del perfil; no se descontó saldo.",
                    Details = SummarizeProviderError(providerException.ProviderBody)
                });
            }

            certificateRequest.ProviderRequestCode = providerRequest.RequestCode;
            certificateRequest.ProviderPublicId = providerRequest.PublicId;
            certificateRequest.ProviderStatus = "CREATED";
            certificateRequest.Status = CertificateRequestStatus.Submitted;
            certificateRequest.SubmittedAt = DateTime.UtcNow;
            certificateRequest.UpdatedAt = DateTime.UtcNow;
            try
            {
                var kyc = await _provider.GetKycLinkAsync(providerRequest.RequestCode, context, CancellationToken.None);
                certificateRequest.KycUrl = kyc.ToString();
                certificateRequest.KycUrlFetchedAt = DateTime.UtcNow;
                certificateRequest.Status = CertificateRequestStatus.WaitingForIdentity;
            }
            catch (Exception ex)
            {
                certificateRequest.LastErrorCode = "kyc_link_pending";
                certificateRequest.LastErrorMessage = ex.Message[..Math.Min(ex.Message.Length, 1000)];
            }
            await _dbContext.SaveChangesAsync(CancellationToken.None);
            return Ok(new { certificateRequest.Id, Environment = request.Environment.ToString(), Status = certificateRequest.Status.ToString(), certificateRequest.KycUrl });
        }

        private static string FormatCop(decimal value) =>
            "$" + value.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("es-CO"));

        [HttpPost]
        public async Task<IActionResult> UploadCertificate(Guid clientId, [FromForm] IFormFile file, [FromForm] string password, [FromForm] CertificateEnvironment environment = CertificateEnvironment.Production)
        {
            if (file == null || file.Length == 0) return BadRequest("Debe cargar un archivo válido (.p12 o .pfx).");
            if (string.IsNullOrEmpty(password)) return BadRequest("Debe ingresar la contraseña del certificado.");

            var tenantId = GetCurrentTenantId();
            var clientExists = await _dbContext.Clients.AnyAsync(c => c.Id == clientId && c.TenantId == tenantId);
            if (!clientExists) return Forbid();

            using var stream = file.OpenReadStream();
            var savedPath = await _storageService.SaveCertificateAsync(clientId, stream, file.FileName);
            var encryptedPass = _cryptoVault.EncryptPassword(password);

            DateTime expirationDate;
            try
            {
                var x509 = _cryptoVault.GetCertificate(savedPath, encryptedPass);
                expirationDate = x509.NotAfter;
            }
            catch (Exception)
            {
                _storageService.DeleteCertificate(savedPath);
                return BadRequest("La contraseña es incorrecta o el archivo de certificado no es válido.");
            }

            var oldCert = await _dbContext.Set<Certificate>().FirstOrDefaultAsync(c => c.ClientId == clientId && c.Environment == environment && c.IsActive);
            if (oldCert != null)
            {
                oldCert.IsActive = false;
            }

            var newCert = new Certificate
            {
                Id = Guid.NewGuid(),
                ClientId = clientId,
                Environment = environment,
                FileName = savedPath,
                EncryptedPassword = encryptedPass,
                ExpirationDate = expirationDate,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _dbContext.Set<Certificate>().Add(newCert);
            await _dbContext.SaveChangesAsync();

            return Ok(new {
                FileName = file.FileName,
                ExpirationDate = expirationDate,
                CreatedAt = newCert.CreatedAt
            });
        }
    }
}

public sealed record CertificateSettingsRequest(bool SendToHolderByEmail);

public sealed record CreateCertificateRequestRequest(
    Guid ProfileId,
    CertificateEnvironment Environment,
    bool AcceptTerms,
    Dictionary<string, string>? FormValues,
    // false = solo entregarlo (no instalarlo para firmar ni reemplazar el activo); solo aplica a un cliente nativo que factura.
    bool? InstallOnPlatform = null);
