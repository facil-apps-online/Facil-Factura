using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
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
                .Where(c => c.IsActive && c.Client.TenantId == tenantId)
                .OrderBy(c => c.ExpirationDate)
                .Select(c => new
                {
                    c.ClientId,
                    ClientName = string.IsNullOrWhiteSpace(c.Client.CommercialName) ? c.Client.CompanyName : c.Client.CommercialName,
                    c.FileName,
                    c.ExpirationDate,
                    c.CreatedAt
                })
                .ToListAsync();

            return Ok(certificates);
        }

        [HttpGet]
        public async Task<IActionResult> GetCertificate(Guid clientId)
        {
            var tenantId = GetCurrentTenantId();
            var clientExists = await _dbContext.Clients.AnyAsync(c => c.Id == clientId && c.TenantId == tenantId);
            if (!clientExists) return Forbid();

            var certificate = await _dbContext.Set<Certificate>()
                .Where(c => c.ClientId == clientId && c.IsActive && c.Environment == CertificateEnvironment.Production)
                .Select(c => new {
                    c.FileName,
                    c.ExpirationDate,
                    c.CreatedAt
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

            return Ok(new { Environment = environment.ToString(), SandboxEnabled = provider.SandboxEnabled, Profiles = profiles, Certificates = certificates, Requests = requests });
        }

        [HttpPost("requests")]
        public async Task<IActionResult> CreateCertificateRequest(Guid clientId, [FromBody] CreateCertificateRequestRequest request, CancellationToken cancellationToken = default)
        {
            var tenantId = GetCurrentTenantId();
            var client = await _dbContext.Clients.SingleOrDefaultAsync(c => c.Id == clientId && c.TenantId == tenantId, cancellationToken);
            if (client == null) return Forbid();
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
            catch (Fel.Infrastructure.Certificates.CertificateProviderException ex)
            {
                _logger.LogWarning("Viafirma rechazÃ³ la solicitud. Environment={Environment} Profile={ProfileId} Code={Code} HttpStatus={HttpStatus}",
                    request.Environment, profile.Id, ex.Code, ex.HttpStatus);
                return StatusCode(StatusCodes.Status502BadGateway, new
                {
                    Provider = provider.Key,
                    Environment = request.Environment.ToString(),
                    Success = false,
                    Code = ex.Code,
                    ProviderHttpStatus = ex.HttpStatus,
                    Message = "Viafirma rechazÃ³ la solicitud. Revisa los datos del perfil.",
                    Details = SummarizeProviderError(ex.ProviderBody)
                });
            }

            string GetRequestField(string key, string fallback = "") =>
                formValues.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : fallback;

            var now = DateTime.UtcNow;
            var certificateRequest = new CertificateRequest
            {
                Id = Guid.NewGuid(), TenantId = tenantId, ClientId = clientId, ProfileId = profile.Id,
                Environment = request.Environment, RequestType = CertificateRequestType.Initial,
                ProviderRequestCode = providerRequest.RequestCode, ProviderPublicId = providerRequest.PublicId,
                ProviderStatus = "CREATED", Status = CertificateRequestStatus.Submitted,
                CsrHash = Convert.ToHexString(SHA256.HashData(Convert.FromBase64String(subject.CsrBase64))).ToLowerInvariant(),
                PublicKeyHash = subject.PublicKeyHash, EncryptedPrivateKey = subject.EncryptedPrivateKey,
                KeyAlgorithm = subject.Algorithm, KeySize = subject.KeySize, TermsUrl = profile.TermsUrl,
                TermsHash = profile.TermsHash, TermsAcceptedAt = now, TermsAcceptedIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                TermsAcceptedUserAgent = Request.Headers.UserAgent.ToString(), SubmittedAt = now, CreatedAt = now, UpdatedAt = now,
                IdempotencyKey = $"tenant:{tenantId}:client:{clientId}:{request.Environment}:{profile.Id}:{now:yyyyMMddHHmmssfff}"
            };
            try
            {
                var kyc = await _provider.GetKycLinkAsync(providerRequest.RequestCode, context, cancellationToken);
                certificateRequest.KycUrl = kyc.ToString();
                certificateRequest.KycUrlFetchedAt = now;
                certificateRequest.Status = CertificateRequestStatus.WaitingForIdentity;
            }
            catch (Exception ex)
            {
                certificateRequest.LastErrorCode = "kyc_link_pending";
                certificateRequest.LastErrorMessage = ex.Message[..Math.Min(ex.Message.Length, 1000)];
            }
            _dbContext.CertificateRequests.Add(certificateRequest);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Ok(new { certificateRequest.Id, Environment = request.Environment.ToString(), Status = certificateRequest.Status.ToString(), certificateRequest.KycUrl });
        }

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

public sealed record CreateCertificateRequestRequest(
    Guid ProfileId,
    CertificateEnvironment Environment,
    bool AcceptTerms,
    Dictionary<string, string>? FormValues);
