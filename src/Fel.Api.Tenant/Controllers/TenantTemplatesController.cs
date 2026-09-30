using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Tenant.Controllers
{
    [ApiController]
    [Route("api/tenant/templates")]
    public class TenantTemplatesController : ControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly IFacilReportsClient _reportsClient;

        public TenantTemplatesController(FelDbContext dbContext, IFacilReportsClient reportsClient)
        {
            _dbContext = dbContext;
            _reportsClient = reportsClient;
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

        // GET: api/tenant/templates
        [HttpGet]
        public async Task<IActionResult> GetTemplates()
        {
            try
            {
                var tenantId = GetCurrentTenantId();

                // Traer plantillas Globales (Superadmin, publicadas) y Plantillas del Tenant (Todas)
                var templates = await _dbContext.DocumentTemplates
                    .Include(t => t.DocumentType)
                    .Where(t => 
                        (t.TenantId == null && t.ClientId == null && t.Status == TemplateStatus.Published) ||
                        (t.TenantId == tenantId && t.ClientId == null)
                    )
                    .OrderByDescending(t => t.CreatedAt)
                    .Select(t => new
                    {
                        id = t.Id,
                        name = t.Name,
                        repxTemplateKey = t.RepxTemplateKey,
                        status = t.Status.ToString(),
                        versionNumber = t.VersionNumber,
                        documentType = t.DocumentType != null ? t.DocumentType.Name : "N/A",
                        isGlobal = t.TenantId == null,
                        clonedFromId = t.ClonedFromId,
                        mostrarRetenciones = t.MostrarRetenciones
                    })
                    .ToListAsync();

                return Ok(templates);
            }
            catch (Exception ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        // POST: api/tenant/templates/{id}/preview — renderiza el .repx con datos de ejemplo (no
        // reales), para poder ver cómo se ve el diseño sin necesitar un documento emitido.
        [HttpPost("{id:guid}/preview")]
        public async Task<IActionResult> Preview(Guid id)
        {
            try
            {
                var tenantId = GetCurrentTenantId();

                var template = await _dbContext.DocumentTemplates
                    .Include(t => t.DocumentType)
                    .FirstOrDefaultAsync(t => t.Id == id &&
                        ((t.TenantId == null && t.ClientId == null) || (t.TenantId == tenantId && t.ClientId == null)));

                if (template == null) return NotFound("Plantilla no encontrada.");
                if (string.IsNullOrWhiteSpace(template.RepxTemplateKey))
                {
                    return BadRequest("Esta plantilla aún no tiene un archivo .repx asociado.");
                }

                var data = Fel.Infrastructure.Services.TemplatePreviewSampleData.BuildSampleData(template.DocumentType?.Code ?? "", template.MostrarRetenciones);
                var pdfBytes = await _reportsClient.GenerateReportAsync(template.RepxTemplateKey, data);
                if (pdfBytes == null)
                {
                    return BadRequest("No se pudo generar la vista previa. Revisa que Facil Reports esté disponible.");
                }

                return File(pdfBytes, "application/pdf");
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        public class CloneRequest
        {
            public string NewName { get; set; } = string.Empty;
            public string NewRepxTemplateKey { get; set; } = string.Empty;
        }

        // POST: api/tenant/templates/{id}/clone
        [HttpPost("{id:guid}/clone")]
        public async Task<IActionResult> CloneTemplate(Guid id, [FromBody] CloneRequest request)
        {
            try
            {
                var tenantId = GetCurrentTenantId();

                var sourceTemplate = await _dbContext.DocumentTemplates
                    .FirstOrDefaultAsync(t => t.Id == id && 
                        (t.TenantId == null || t.TenantId == tenantId) && 
                        t.Status == TemplateStatus.Published);

                if (sourceTemplate == null)
                    return NotFound("Plantilla origen no encontrada o no está publicada.");

                var sourceBytes = await _reportsClient.DownloadTemplateAsync(sourceTemplate.RepxTemplateKey);
                if (sourceBytes == null)
                    return BadRequest("No se pudo copiar el diseño original desde Facil Reports.");

                if (!await _reportsClient.UploadTemplateAsync(request.NewRepxTemplateKey, sourceBytes))
                    return BadRequest("No se pudo guardar la copia del diseño en Facil Reports.");

                var clonedTemplate = new DocumentTemplate
                {
                    Id = Guid.NewGuid(),
                    Name = string.IsNullOrWhiteSpace(request.NewName) ? $"{sourceTemplate.Name} (Clon)" : request.NewName,
                    RepxTemplateKey = request.NewRepxTemplateKey,
                    Status = TemplateStatus.Draft,
                    VersionNumber = 1,
                    ClonedFromId = sourceTemplate.Id,
                    DocumentTypeId = sourceTemplate.DocumentTypeId,
                    TenantId = tenantId,
                    ClientId = null, // Pertenece al Tenant, aún no a un cliente específico
                    MostrarRetenciones = sourceTemplate.MostrarRetenciones,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.DocumentTemplates.Add(clonedTemplate);
                await _dbContext.SaveChangesAsync();

                return Ok(new { message = "Plantilla clonada con éxito", templateId = clonedTemplate.Id });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        public class PublishRequest
        {
            public string NewRepxTemplateKey { get; set; } = string.Empty;
        }

        // PUT: api/tenant/templates/{id}/publish
        [HttpPut("{id:guid}/publish")]
        public async Task<IActionResult> PublishTemplate(Guid id, [FromBody] PublishRequest request)
        {
            try
            {
                var tenantId = GetCurrentTenantId();

                var templateToPublish = await _dbContext.DocumentTemplates
                    .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId);

                if (templateToPublish == null)
                    return NotFound("Plantilla no encontrada o no le pertenece a este Tenant.");

                if (templateToPublish.Status == TemplateStatus.Published)
                    return BadRequest("La plantilla ya está publicada. Debe crear una nueva versión para editarla.");

                if (templateToPublish.Status == TemplateStatus.Archived)
                    return BadRequest("No puedes republicar una plantilla archivada.");

                if (!string.IsNullOrWhiteSpace(request.NewRepxTemplateKey))
                {
                    templateToPublish.RepxTemplateKey = request.NewRepxTemplateKey;
                }

                templateToPublish.Status = TemplateStatus.Published;
                templateToPublish.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                return Ok(new { message = "Plantilla publicada correctamente." });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        public class NewVersionRequest
        {
            public string NewRepxTemplateKey { get; set; } = string.Empty;
        }

        // POST: api/tenant/templates/{id}/new-version
        [HttpPost("{id:guid}/new-version")]
        public async Task<IActionResult> CreateNewVersion(Guid id, [FromBody] NewVersionRequest request)
        {
            try
            {
                var tenantId = GetCurrentTenantId();

                var sourceTemplate = await _dbContext.DocumentTemplates
                    .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId && t.ClientId == null);

                if (sourceTemplate == null)
                    return NotFound("Plantilla del Tenant no encontrada.");

                if (sourceTemplate.Status != TemplateStatus.Published)
                    return BadRequest("Solo puedes versionar plantillas que estén Publicadas.");

                var newRepxTemplateKey = string.IsNullOrWhiteSpace(request.NewRepxTemplateKey) ? sourceTemplate.RepxTemplateKey : request.NewRepxTemplateKey;
                if (newRepxTemplateKey != sourceTemplate.RepxTemplateKey)
                {
                    var sourceBytes = await _reportsClient.DownloadTemplateAsync(sourceTemplate.RepxTemplateKey);
                    if (sourceBytes == null)
                        return BadRequest("No se pudo copiar el diseño publicado desde Facil Reports.");

                    if (!await _reportsClient.UploadTemplateAsync(newRepxTemplateKey, sourceBytes))
                        return BadRequest("No se pudo guardar la copia del diseño en Facil Reports.");
                }

                var newVersionTemplate = new DocumentTemplate
                {
                    Id = Guid.NewGuid(),
                    Name = sourceTemplate.Name,
                    RepxTemplateKey = newRepxTemplateKey,
                    Status = TemplateStatus.Draft,
                    VersionNumber = sourceTemplate.VersionNumber + 1,
                    PreviousVersionId = sourceTemplate.Id,
                    ClonedFromId = sourceTemplate.ClonedFromId,
                    DocumentTypeId = sourceTemplate.DocumentTypeId,
                    TenantId = tenantId,
                    ClientId = null,
                    MostrarRetenciones = sourceTemplate.MostrarRetenciones,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.DocumentTemplates.Add(newVersionTemplate);
                await _dbContext.SaveChangesAsync();

                return Ok(new { message = $"Versión {newVersionTemplate.VersionNumber} creada en estado Borrador.", templateId = newVersionTemplate.Id });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        public class SetMostrarRetencionesRequest
        {
            public bool MostrarRetenciones { get; set; }
        }

        // PUT: api/tenant/templates/{id}/mostrar-retenciones — solo sobre plantillas propias del
        // Tenant (Draft o Published: preferencia de datos del reporte, no un cambio de diseño).
        [HttpPut("{id:guid}/mostrar-retenciones")]
        public async Task<IActionResult> SetMostrarRetenciones(Guid id, [FromBody] SetMostrarRetencionesRequest request)
        {
            try
            {
                var tenantId = GetCurrentTenantId();

                var template = await _dbContext.DocumentTemplates
                    .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId && t.ClientId == null);

                if (template == null)
                    return NotFound("Plantilla no encontrada o no le pertenece a este Tenant.");

                template.MostrarRetenciones = request.MostrarRetenciones;
                template.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                return Ok(new { message = "Preferencia de retenciones actualizada." });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        public class AssignRequest
        {
            public Guid ClientId { get; set; }
            public Guid DocumentTypeId { get; set; }
            public Guid SelectedTemplateId { get; set; }
        }

        // POST: api/tenant/templates/assign
        [HttpPost("assign")]
        public async Task<IActionResult> AssignToClient([FromBody] AssignRequest request)
        {
            try
            {
                var tenantId = GetCurrentTenantId();

                // Verificar que el cliente pertenezca al Tenant
                var client = await _dbContext.Clients
                    .FirstOrDefaultAsync(c => c.Id == request.ClientId && c.TenantId == tenantId);

                if (client == null)
                    return NotFound("Cliente no encontrado o no pertenece a su cuenta.");

                // Verificar que la plantilla exista y esté publicada (Global o del Tenant)
                var template = await _dbContext.DocumentTemplates
                    .FirstOrDefaultAsync(t => t.Id == request.SelectedTemplateId && 
                        (t.TenantId == null || t.TenantId == tenantId) &&
                        t.Status == TemplateStatus.Published);

                if (template == null)
                    return BadRequest("La plantilla seleccionada no existe o no está publicada.");

                // Upsert de la asignación
                var setting = await _dbContext.ClientDocumentSettings
                    .FirstOrDefaultAsync(s => s.ClientId == request.ClientId && s.DocumentTypeId == request.DocumentTypeId);

                if (setting != null)
                {
                    setting.SelectedTemplateId = request.SelectedTemplateId;
                    setting.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    setting = new ClientDocumentSetting
                    {
                        Id = Guid.NewGuid(),
                        ClientId = request.ClientId,
                        DocumentTypeId = request.DocumentTypeId,
                        SelectedTemplateId = request.SelectedTemplateId,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _dbContext.ClientDocumentSettings.Add(setting);
                }

                await _dbContext.SaveChangesAsync();

                return Ok(new { message = "Plantilla asignada al cliente correctamente." });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
