using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Fel.Api.Security;

namespace Fel.Api.Client.Controllers
{
    [ApiController]
    [Route("api/client/templates")]
    [ClientRole(ClientUserRoles.Administrator)]
    [AllowAllBranches]
    public class ClientTemplatesController : ClientPortalControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly IFacilReportsClient _reportsClient;

        public ClientTemplatesController(FelDbContext dbContext, IFacilReportsClient reportsClient)
        {
            _dbContext = dbContext;
            _reportsClient = reportsClient;
        }

        // GET: api/client/templates/available/{documentTypeId}
        [HttpGet("available/{documentTypeId:guid}")]
        public async Task<IActionResult> GetAvailableTemplates(Guid documentTypeId)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var client = await _dbContext.Clients.FindAsync(clientId);

                if (client == null)
                    return NotFound("Cliente no encontrado.");

                // Publicadas: globales, del Tenant de este cliente, o propias de este cliente (nunca las
                // propias de OTRO cliente del mismo Tenant — por eso el chequeo de ClientId != clientId
                // en el segundo término, no solo TenantId).
                var availableTemplates = await _dbContext.DocumentTemplates
                    .Where(t => t.DocumentTypeId == documentTypeId &&
                                t.Status == TemplateStatus.Published &&
                                ((t.TenantId == null && t.ClientId == null) ||
                                 (t.TenantId == client.TenantId && t.ClientId == null) ||
                                 t.ClientId == clientId))
                    .Select(t => new
                    {
                        id = t.Id,
                        name = t.Name,
                        isGlobal = t.TenantId == null && t.ClientId == null,
                        isOwn = t.ClientId == clientId
                    })
                    .ToListAsync();

                return Ok(availableTemplates);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // GET: api/client/templates
        // Listado de gestión (a diferencia de /available, que solo trae Publicadas de un tipo de
        // documento): trae globales y del Tenant Publicadas, más TODAS las propias del cliente (para
        // que pueda ver y publicar sus borradores), sin filtrar por tipo de documento.
        [HttpGet]
        public async Task<IActionResult> GetMyTemplates()
        {
            try
            {
                var clientId = GetCurrentClientId();
                var client = await _dbContext.Clients.FindAsync(clientId);
                if (client == null) return NotFound("Cliente no encontrado.");

                var templates = await _dbContext.DocumentTemplates
                    .Include(t => t.DocumentType)
                    .Where(t =>
                        (t.TenantId == null && t.ClientId == null && t.Status == TemplateStatus.Published) ||
                        (t.TenantId == client.TenantId && t.ClientId == null && t.Status == TemplateStatus.Published) ||
                        t.ClientId == clientId)
                    .OrderByDescending(t => t.CreatedAt)
                    .Select(t => new
                    {
                        id = t.Id,
                        name = t.Name,
                        repxTemplateKey = t.RepxTemplateKey,
                        status = t.Status.ToString(),
                        versionNumber = t.VersionNumber,
                        documentTypeId = t.DocumentTypeId,
                        documentType = t.DocumentType != null ? t.DocumentType.Name : "N/A",
                        scope = t.ClientId == clientId ? "Propio" : t.TenantId == client.TenantId ? "Tenant" : "Global",
                        clonedFromId = t.ClonedFromId,
                        mostrarRetenciones = t.MostrarRetenciones
                    })
                    .ToListAsync();

                return Ok(templates);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: api/client/templates/{id}/preview — renderiza el .repx con datos de ejemplo (no
        // reales), para poder ver cómo se ve el diseño sin necesitar un documento emitido.
        [HttpPost("{id:guid}/preview")]
        public async Task<IActionResult> Preview(Guid id)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var client = await _dbContext.Clients.FindAsync(clientId);
                if (client == null) return NotFound("Cliente no encontrado.");

                var template = await _dbContext.DocumentTemplates
                    .Include(t => t.DocumentType)
                    .FirstOrDefaultAsync(t => t.Id == id &&
                        ((t.TenantId == null && t.ClientId == null) ||
                         (t.TenantId == client.TenantId && t.ClientId == null) ||
                         t.ClientId == clientId));

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

        public class CloneTemplateRequest
        {
            public string NewName { get; set; } = string.Empty;
            public string NewRepxTemplateKey { get; set; } = string.Empty;
        }

        // POST: api/client/templates/{id}/clone
        [HttpPost("{id:guid}/clone")]
        public async Task<IActionResult> CloneTemplate(Guid id, [FromBody] CloneTemplateRequest request)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var client = await _dbContext.Clients.FindAsync(clientId);
                if (client == null) return NotFound("Cliente no encontrado.");

                var sourceTemplate = await _dbContext.DocumentTemplates
                    .FirstOrDefaultAsync(t => t.Id == id &&
                        t.Status == TemplateStatus.Published &&
                        ((t.TenantId == null && t.ClientId == null) ||
                         (t.TenantId == client.TenantId && t.ClientId == null) ||
                         t.ClientId == clientId));

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
                    TenantId = client.TenantId,
                    ClientId = clientId,
                    MostrarRetenciones = sourceTemplate.MostrarRetenciones,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.DocumentTemplates.Add(clonedTemplate);
                await _dbContext.SaveChangesAsync();

                return Ok(new { message = "Plantilla clonada con éxito. Ahora tienes tu versión propia.", templateId = clonedTemplate.Id });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        public class PublishTemplateRequest
        {
            public string NewRepxTemplateKey { get; set; } = string.Empty;
        }

        // PUT: api/client/templates/{id}/publish
        [HttpPut("{id:guid}/publish")]
        public async Task<IActionResult> PublishTemplate(Guid id, [FromBody] PublishTemplateRequest request)
        {
            try
            {
                var clientId = GetCurrentClientId();

                var template = await _dbContext.DocumentTemplates
                    .FirstOrDefaultAsync(t => t.Id == id && t.ClientId == clientId);

                if (template == null)
                    return NotFound("Plantilla no encontrada o no te pertenece.");

                if (template.Status == TemplateStatus.Published)
                    return BadRequest("La plantilla ya está publicada. Debe crear una nueva versión para editarla.");

                if (template.Status == TemplateStatus.Archived)
                    return BadRequest("No puedes republicar una plantilla archivada.");

                if (!string.IsNullOrWhiteSpace(request.NewRepxTemplateKey))
                {
                    template.RepxTemplateKey = request.NewRepxTemplateKey;
                }

                if (template.PreviousVersionId.HasValue)
                {
                    var previousTemplate = await _dbContext.DocumentTemplates.FindAsync(template.PreviousVersionId.Value);
                    if (previousTemplate != null && previousTemplate.Status == TemplateStatus.Published)
                    {
                        previousTemplate.Status = TemplateStatus.Archived;
                        previousTemplate.UpdatedAt = DateTime.UtcNow;
                    }
                }

                template.Status = TemplateStatus.Published;
                template.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                return Ok(new { message = "Plantilla publicada correctamente." });
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

        // PUT: api/client/templates/{id}/mostrar-retenciones — solo sobre plantillas propias del
        // cliente (Draft o Published: es una preferencia de datos del reporte, no un cambio de
        // diseño del .repx, así que no exige una nueva versión).
        [HttpPut("{id:guid}/mostrar-retenciones")]
        public async Task<IActionResult> SetMostrarRetenciones(Guid id, [FromBody] SetMostrarRetencionesRequest request)
        {
            try
            {
                var clientId = GetCurrentClientId();

                var template = await _dbContext.DocumentTemplates
                    .FirstOrDefaultAsync(t => t.Id == id && t.ClientId == clientId);

                if (template == null)
                    return NotFound("Plantilla no encontrada o no te pertenece.");

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

        public class NewVersionTemplateRequest
        {
            public string NewRepxTemplateKey { get; set; } = string.Empty;
        }

        // POST: api/client/templates/{id}/new-version
        [HttpPost("{id:guid}/new-version")]
        public async Task<IActionResult> CreateNewVersion(Guid id, [FromBody] NewVersionTemplateRequest request)
        {
            try
            {
                var clientId = GetCurrentClientId();

                var sourceTemplate = await _dbContext.DocumentTemplates
                    .FirstOrDefaultAsync(t => t.Id == id && t.ClientId == clientId);

                if (sourceTemplate == null)
                    return NotFound("Plantilla no encontrada o no te pertenece.");

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
                    TenantId = sourceTemplate.TenantId,
                    ClientId = clientId,
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

        // GET: api/client/templates/settings
        //
        // Parte de los tipos de documento HABILITADOS para este cliente (ClientEnabledDocumentTypes,
        // sembrados al crear el cliente), no de ClientDocumentSettings — antes solo devolvía filas
        // que ya existían, así que un cliente que nunca había elegido plantilla veía la lista vacía
        // y no tenía en qué hacer clic para elegir la primera vez.
        [HttpGet("settings")]
        public async Task<IActionResult> GetMySettings()
        {
            try
            {
                var clientId = GetCurrentClientId();

                var enabledTypes = await _dbContext.ClientEnabledDocumentTypes
                    .Include(e => e.DocumentType)
                    .Where(e => e.ClientId == clientId)
                    .ToListAsync();

                var existingSettings = await _dbContext.ClientDocumentSettings
                    .Include(s => s.SelectedTemplate)
                    .Where(s => s.ClientId == clientId)
                    .ToListAsync();

                var result = enabledTypes.Select(e =>
                {
                    var setting = existingSettings.FirstOrDefault(s => s.DocumentTypeId == e.DocumentTypeId);
                    return new
                    {
                        settingId = setting?.Id ?? e.DocumentTypeId,
                        documentTypeId = e.DocumentTypeId,
                        documentTypeName = e.DocumentType != null ? e.DocumentType.Name : "N/A",
                        selectedTemplateId = setting?.SelectedTemplateId,
                        selectedTemplateName = setting?.SelectedTemplate?.Name
                    };
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        public class SelectTemplateRequest
        {
            public Guid DocumentTypeId { get; set; }
            public Guid TemplateId { get; set; }
        }

        // POST: api/client/templates/select
        [HttpPost("select")]
        public async Task<IActionResult> SelectTemplate([FromBody] SelectTemplateRequest request)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var client = await _dbContext.Clients.FindAsync(clientId);

                if (client == null)
                    return NotFound("Cliente no encontrado.");

                // Validar que la plantilla exista y tenga permisos (misma regla de alcance que
                // GetAvailableTemplates: global, del Tenant, o propia de este cliente — nunca la
                // propia de otro cliente del mismo Tenant).
                var template = await _dbContext.DocumentTemplates
                    .FirstOrDefaultAsync(t => t.Id == request.TemplateId &&
                                              t.DocumentTypeId == request.DocumentTypeId &&
                                              t.Status == TemplateStatus.Published &&
                                              ((t.TenantId == null && t.ClientId == null) ||
                                               (t.TenantId == client.TenantId && t.ClientId == null) ||
                                               t.ClientId == clientId));

                if (template == null)
                    return BadRequest("La plantilla no existe, no corresponde a este tipo de documento, o no tienes permisos para usarla.");

                var setting = await _dbContext.ClientDocumentSettings
                    .FirstOrDefaultAsync(s => s.ClientId == clientId && s.DocumentTypeId == request.DocumentTypeId);

                if (setting != null)
                {
                    setting.SelectedTemplateId = request.TemplateId;
                    setting.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    setting = new ClientDocumentSetting
                    {
                        Id = Guid.NewGuid(),
                        ClientId = clientId,
                        DocumentTypeId = request.DocumentTypeId,
                        SelectedTemplateId = request.TemplateId,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _dbContext.ClientDocumentSettings.Add(setting);
                }

                await _dbContext.SaveChangesAsync();

                return Ok(new { message = "Preferencia de plantilla guardada correctamente." });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
