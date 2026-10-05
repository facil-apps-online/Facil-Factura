using System;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Tenant.Controllers
{
    [ApiController]
    [Route("api/tenant/clients/{clientId}/resolutions")]
    public class TenantResolutionsController : ControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly DianResolutionParserService _parserService;
        private readonly IDianSoapClient _dianSoapClient;
        private readonly ICryptoVault _cryptoVault;
        private readonly ResolutionBranchService _resolutionBranches;

        public TenantResolutionsController(FelDbContext dbContext, DianResolutionParserService parserService, IDianSoapClient dianSoapClient, ICryptoVault cryptoVault, ResolutionBranchService resolutionBranches)
        {
            _dbContext = dbContext;
            _parserService = parserService;
            _dianSoapClient = dianSoapClient;
            _cryptoVault = cryptoVault;
            _resolutionBranches = resolutionBranches;
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

        [HttpGet]
        public async Task<IActionResult> GetResolutions(Guid clientId)
        {
            var tenantId = GetCurrentTenantId();
            
            var clientExists = await _dbContext.Clients.AnyAsync(c => c.Id == clientId && c.TenantId == tenantId);
            if (!clientExists) return StatusCode(StatusCodes.Status403Forbidden);

            var resolutions = await _dbContext.Set<Resolution>()
                .Where(r => r.ClientId == clientId && r.IsActive)
                .OrderByDescending(r => r.ValidFrom)
                .Select(r => new {
                    r.Id,
                    r.ResolutionNumber,
                    r.Prefix,
                    r.NumberStart,
                    r.NumberEnd,
                    r.ValidFrom,
                    r.ValidTo,
                    r.TechnicalKey,
                    r.DocumentType,
                    r.NextNumber,
                    r.IsDefault,
                    BranchIds = _dbContext.ResolutionBranches.Where(rb => rb.ResolutionId == r.Id).Select(rb => rb.BranchId).ToList()
                })
                .ToListAsync();

            return Ok(resolutions);
        }

        [HttpPost("parse")]
        public async Task<IActionResult> ParsePdf(Guid clientId, IFormFile file)
        {
            try
            {
                var tenantId = GetCurrentTenantId();
                var clientExists = await _dbContext.Clients.AnyAsync(c => c.Id == clientId && c.TenantId == tenantId);
                if (!clientExists) return StatusCode(StatusCodes.Status403Forbidden);

                if (file == null || file.Length == 0)
                    return BadRequest("No se proporcionó un archivo PDF válido.");

                if (file.ContentType != "application/pdf")
                    return BadRequest("El archivo debe ser un PDF.");

                using var stream = file.OpenReadStream();
                var results = await _parserService.ParsePdfAsync(stream);

                if (results.Count == 1 && !results[0].IsSuccess)
                {
                    return BadRequest(results[0].ErrorMessage);
                }

                return Ok(results);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno procesando el archivo: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateResolution(Guid clientId, [FromBody] CreateResolutionRequest request)
        {
            var tenantId = GetCurrentTenantId();
            var clientExists = await _dbContext.Clients.AnyAsync(c => c.Id == clientId && c.TenantId == tenantId);
            if (!clientExists) return StatusCode(StatusCodes.Status403Forbidden);

            var mainBranchId = await BranchProvisioning.MainBranchIdAsync(_dbContext, clientId);
            var (resolutionBranchIds, branchError) = await _resolutionBranches.ResolveForNewAsync(clientId, request.BranchIds, request.DocumentType, request.Prefix?.Trim() ?? string.Empty, mainBranchId);
            if (branchError != null) return BadRequest(branchError);

            var hasOtherActiveOfType = await _dbContext.Set<Resolution>()
                .AnyAsync(r => r.ClientId == clientId && r.DocumentType == request.DocumentType && r.IsActive);

            var resolution = new Resolution
            {
                Id = Guid.NewGuid(),
                ClientId = clientId,
                ResolutionNumber = request.ResolutionNumber?.Trim() ?? "",
                Prefix = request.Prefix?.Trim() ?? "",
                NumberStart = request.NumberStart,
                NumberEnd = request.NumberEnd,
                ValidFrom = request.ValidFrom,
                ValidTo = request.ValidTo,
                TechnicalKey = request.TechnicalKey ?? "",
                DocumentType = request.DocumentType,
                IsActive = true,
                IsDefault = !hasOtherActiveOfType
            };

            _dbContext.Set<Resolution>().Add(resolution);
            foreach (var branchId in resolutionBranchIds)
                _dbContext.ResolutionBranches.Add(BranchProvisioning.LinkResolution(resolution.Id, branchId));
            await _dbContext.SaveChangesAsync();

            return Ok(new {
                resolution.Id,
                resolution.ResolutionNumber,
                resolution.Prefix,
                resolution.NumberStart,
                resolution.NumberEnd,
                resolution.ValidFrom,
                resolution.ValidTo,
                resolution.DocumentType,
                resolution.IsDefault
            });
        }

        public class UpdateResolutionRequest
        {
            public string ResolutionNumber { get; set; } = string.Empty;
            public string Prefix { get; set; } = string.Empty;
            public long NumberStart { get; set; }
            public long NumberEnd { get; set; }
            public DateTime ValidFrom { get; set; }
            public DateTime ValidTo { get; set; }
            public string TechnicalKey { get; set; } = string.Empty;
        }

        // No incluye DocumentType a propósito: cambiar el tipo de una resolución ya en uso arrastra
        // la numeración y el default por tipo. Si hace falta otro tipo, se crea una resolución nueva.
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateResolution(Guid clientId, Guid id, [FromBody] UpdateResolutionRequest request)
        {
            var tenantId = GetCurrentTenantId();
            var clientExists = await _dbContext.Clients.AnyAsync(c => c.Id == clientId && c.TenantId == tenantId);
            if (!clientExists) return StatusCode(StatusCodes.Status403Forbidden);

            var resolution = await _dbContext.Set<Resolution>().FirstOrDefaultAsync(r => r.Id == id && r.ClientId == clientId);
            if (resolution == null) return NotFound();

            var nextNumber = resolution.NextNumber ?? resolution.NumberStart;
            if (nextNumber < request.NumberStart || nextNumber > request.NumberEnd)
            {
                return BadRequest($"El rango debe seguir incluyendo el próximo número a usar ({nextNumber}).");
            }

            resolution.ResolutionNumber = request.ResolutionNumber?.Trim() ?? "";
            resolution.Prefix = request.Prefix?.Trim() ?? "";
            resolution.NumberStart = request.NumberStart;
            resolution.NumberEnd = request.NumberEnd;
            resolution.ValidFrom = request.ValidFrom;
            resolution.ValidTo = request.ValidTo;
            resolution.TechnicalKey = request.TechnicalKey ?? "";
            await _dbContext.SaveChangesAsync();

            return Ok(new
            {
                resolution.Id,
                resolution.ResolutionNumber,
                resolution.Prefix,
                resolution.NumberStart,
                resolution.NumberEnd,
                resolution.ValidFrom,
                resolution.ValidTo,
                resolution.TechnicalKey,
                resolution.DocumentType,
                resolution.NextNumber,
                resolution.IsDefault
            });
        }

        public class SetNextNumberRequest
        {
            public long NextNumber { get; set; }
        }

        // Permite al tenant fijar manualmente el próximo consecutivo a usar en nombre del cliente —
        // ej. al migrar desde otro sistema donde ya se emitieron facturas hasta cierto número.
        [HttpPut("{id}/next-number")]
        public async Task<IActionResult> SetNextNumber(Guid clientId, Guid id, [FromBody] SetNextNumberRequest request)
        {
            var tenantId = GetCurrentTenantId();
            var clientExists = await _dbContext.Clients.AnyAsync(c => c.Id == clientId && c.TenantId == tenantId);
            if (!clientExists) return StatusCode(StatusCodes.Status403Forbidden);

            var resolution = await _dbContext.Set<Resolution>().FirstOrDefaultAsync(r => r.Id == id && r.ClientId == clientId);
            if (resolution == null) return NotFound();

            if (request.NextNumber < resolution.NumberStart || request.NextNumber > resolution.NumberEnd)
            {
                return BadRequest($"El número debe estar entre {resolution.NumberStart} y {resolution.NumberEnd} (el rango autorizado de esta resolución).");
            }

            resolution.NextNumber = request.NextNumber;
            await _dbContext.SaveChangesAsync();

            return Ok(new { resolution.Id, resolution.NextNumber });
        }

        // Marca esta resolución como la predeterminada de su tipo de documento, en nombre del
        // cliente, desmarcando cualquier otra resolución activa del mismo ClientId+DocumentType.
        [HttpPut("{id}/set-default")]
        public async Task<IActionResult> SetDefault(Guid clientId, Guid id)
        {
            var tenantId = GetCurrentTenantId();
            var clientExists = await _dbContext.Clients.AnyAsync(c => c.Id == clientId && c.TenantId == tenantId);
            if (!clientExists) return StatusCode(StatusCodes.Status403Forbidden);

            var resolution = await _dbContext.Set<Resolution>().FirstOrDefaultAsync(r => r.Id == id && r.ClientId == clientId && r.IsActive);
            if (resolution == null) return NotFound();

            var others = await _dbContext.Set<Resolution>()
                .Where(r => r.ClientId == clientId && r.DocumentType == resolution.DocumentType && r.IsActive && r.Id != id)
                .ToListAsync();

            foreach (var other in others)
            {
                other.IsDefault = false;
            }

            resolution.IsDefault = true;
            await _dbContext.SaveChangesAsync();

            return Ok(new { resolution.Id, resolution.IsDefault });
        }

        // Consulta GetNumberingRange (anexo técnico numeral 7.15) para traer la Clave Técnica real de
        // esta resolución directamente de la DIAN — el PDF de autorización (formulario 1876) nunca la
        // trae, y el portal de producción tampoco la expone en ninguna pantalla; solo este servicio
        // web la entrega. Servicio exclusivo de producción en operación (el de habilitación la
        // entrega el propio catálogo de participantes al registrar el software).
        [HttpPost("{id}/fetch-technical-key")]
        public async Task<IActionResult> FetchTechnicalKey(Guid clientId, Guid id)
        {
            var tenantId = GetCurrentTenantId();
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == clientId && c.TenantId == tenantId);
            if (client == null) return StatusCode(StatusCodes.Status403Forbidden);

            var resolution = await _dbContext.Set<Resolution>().FirstOrDefaultAsync(r => r.Id == id && r.ClientId == clientId);
            if (resolution == null) return NotFound();

            if (string.IsNullOrWhiteSpace(client.SoftwareId))
                return BadRequest("Este cliente no tiene SoftwareId registrado.");

            var certificate = await _dbContext.Certificates.FirstOrDefaultAsync(c => c.ClientId == clientId && c.IsActive);
            if (certificate == null)
                return BadRequest("Este cliente no tiene un certificado digital activo cargado.");

            System.Security.Cryptography.X509Certificates.X509Certificate2 cert;
            try
            {
                cert = _cryptoVault.GetCertificate(certificate.FileName, certificate.EncryptedPassword);
            }
            catch (Exception ex)
            {
                return BadRequest($"No se pudo cargar el certificado: {ex.Message}");
            }

            string soapResponse;
            try
            {
                soapResponse = await _dianSoapClient.GetNumberingRangeAsync(client.TaxId, client.TaxId, client.SoftwareId, cert);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error consultando la DIAN: {ex.Message}");
            }

            XDocument doc;
            try
            {
                doc = XDocument.Parse(soapResponse);
            }
            catch (System.Xml.XmlException)
            {
                return BadRequest("La respuesta de la DIAN no es XML válido.");
            }

            // Coincide por ResolutionNumber tal cual lo devuelve la DIAN — se busca por nombre local
            // (sin acoplarse al namespace exacto), mismo criterio que DianStatusOutcome.
            var rango = doc.Descendants().FirstOrDefault(e =>
                e.Name.LocalName == "NumberRangeResponse" &&
                e.Elements().Any(c => c.Name.LocalName == "ResolutionNumber" && c.Value.Trim() == resolution.ResolutionNumber.Trim()));

            if (rango == null)
            {
                var operationDescription = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "OperationDescription")?.Value;
                return BadRequest($"La DIAN no devolvió ningún rango con el número de resolución {resolution.ResolutionNumber}. {operationDescription}".Trim());
            }

            var technicalKey = rango.Elements().FirstOrDefault(e => e.Name.LocalName == "TechnicalKey")?.Value;
            if (string.IsNullOrWhiteSpace(technicalKey))
                return BadRequest("La DIAN encontró el rango pero no devolvió Clave Técnica.");

            resolution.TechnicalKey = technicalKey;
            await _dbContext.SaveChangesAsync();

            return Ok(new { resolution.Id, resolution.TechnicalKey });
        }

        public class DocumentLegendRequest
        {
            public string? Text { get; set; }
        }

        [HttpGet("{id}/legend")]
        public async Task<IActionResult> GetLegend(Guid clientId, Guid id)
        {
            var tenantId = GetCurrentTenantId();
            if (!await _dbContext.Clients.AnyAsync(c => c.Id == clientId && c.TenantId == tenantId)) return StatusCode(StatusCodes.Status403Forbidden);
            var resolution = await _dbContext.Resolutions.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id && r.ClientId == clientId);
            if (resolution == null) return NotFound();
            var type = Fel.Infrastructure.Services.DocumentLegendType.Normalize(resolution.DocumentType);
            var legend = await _dbContext.DocumentLegendByPrefixes.AsNoTracking().FirstOrDefaultAsync(x => x.ClientId == clientId && x.DocumentType == type && x.Prefix == resolution.Prefix);
            return Ok(new { documentType = type, prefix = resolution.Prefix, text = legend?.Text ?? string.Empty });
        }

        [HttpPut("{id}/legend")]
        public async Task<IActionResult> UpdateLegend(Guid clientId, Guid id, [FromBody] DocumentLegendRequest request)
        {
            var tenantId = GetCurrentTenantId();
            if (!await _dbContext.Clients.AnyAsync(c => c.Id == clientId && c.TenantId == tenantId)) return StatusCode(StatusCodes.Status403Forbidden);
            var resolution = await _dbContext.Resolutions.FirstOrDefaultAsync(r => r.Id == id && r.ClientId == clientId);
            if (resolution == null) return NotFound();
            var type = Fel.Infrastructure.Services.DocumentLegendType.Normalize(resolution.DocumentType);
            var legend = await _dbContext.DocumentLegendByPrefixes.FirstOrDefaultAsync(x => x.ClientId == clientId && x.DocumentType == type && x.Prefix == resolution.Prefix);
            var text = request.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text)) { if (legend != null) _dbContext.DocumentLegendByPrefixes.Remove(legend); }
            else if (legend == null) _dbContext.DocumentLegendByPrefixes.Add(new Fel.Core.Entities.DocumentLegendByPrefix { Id = Guid.NewGuid(), ClientId = clientId, DocumentType = type, Prefix = resolution.Prefix.Trim(), Text = text, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            else { legend.Text = text; legend.UpdatedAt = DateTime.UtcNow; }
            await _dbContext.SaveChangesAsync();
            return Ok(new { documentType = type, prefix = resolution.Prefix, text });
        }

        public class SetResolutionBranchesRequest
        {
            public System.Collections.Generic.List<Guid> BranchIds { get; set; } = new System.Collections.Generic.List<Guid>();
        }

        // Reemplaza las sucursales donde se usa la resolución; debe quedar al menos una.
        [HttpPut("{id}/branches")]
        public async Task<IActionResult> SetBranches(Guid clientId, Guid id, [FromBody] SetResolutionBranchesRequest request)
        {
            var tenantId = GetCurrentTenantId();
            if (!await _dbContext.Clients.AnyAsync(c => c.Id == clientId && c.TenantId == tenantId)) return StatusCode(StatusCodes.Status403Forbidden);

            var (ids, error) = await _resolutionBranches.SetBranchesAsync(clientId, id, request.BranchIds ?? new System.Collections.Generic.List<Guid>());
            if (ids == null) return NotFound();
            if (error != null) return BadRequest(error);
            return Ok(new { branchIds = ids });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteResolution(Guid clientId, Guid id)
        {
            var tenantId = GetCurrentTenantId();
            var clientExists = await _dbContext.Clients.AnyAsync(c => c.Id == clientId && c.TenantId == tenantId);
            if (!clientExists) return StatusCode(StatusCodes.Status403Forbidden);

            var resolution = await _dbContext.Set<Resolution>().FirstOrDefaultAsync(r => r.Id == id && r.ClientId == clientId);
            if (resolution == null) return NotFound();

            resolution.IsActive = false;
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }
    }

    public class CreateResolutionRequest
    {
        public string ResolutionNumber { get; set; } = string.Empty;
        public string Prefix { get; set; } = string.Empty;
        public long NumberStart { get; set; }
        public long NumberEnd { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime ValidTo { get; set; }
        public string TechnicalKey { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        // Sucursales donde se usará. Sin elegir: las de la resolución que reemplaza o, si no hay, la principal.
        public System.Collections.Generic.List<Guid>? BranchIds { get; set; }
    }
}
