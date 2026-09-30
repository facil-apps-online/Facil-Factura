using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Superadmin.Controllers
{
    [ApiController]
    [Route("api/superadmin/templates")]
    public class SuperadminTemplatesController : ControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly IFacilReportsClient _facilReportsClient;

        public SuperadminTemplatesController(FelDbContext dbContext, IFacilReportsClient facilReportsClient)
        {
            _dbContext = dbContext;
            _facilReportsClient = facilReportsClient;
        }

        private static async Task<byte[]> ReadBytesAsync(IFormFile file)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            return ms.ToArray();
        }

        // GET: api/superadmin/templates
        [HttpGet]
        public async Task<IActionResult> GetGlobalTemplates()
        {
            try
            {
                var templates = await _dbContext.DocumentTemplates
                    .Include(t => t.DocumentType)
                    .Where(t => t.TenantId == null && t.ClientId == null) // Globales exclusivas del Superadmin
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
                        previousVersionId = t.PreviousVersionId,
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

        // GET: api/superadmin/templates/by-type/{documentTypeId} — plantillas globales de un solo
        // tipo de documento, para la pantalla de "Modelos de Diseño" de ese tipo.
        [HttpGet("by-type/{documentTypeId:guid}")]
        public async Task<IActionResult> GetTemplatesByType(Guid documentTypeId)
        {
            try
            {
                var templates = await _dbContext.DocumentTemplates
                    .Where(t => t.TenantId == null && t.ClientId == null && t.DocumentTypeId == documentTypeId)
                    .OrderByDescending(t => t.CreatedAt)
                    .Select(t => new
                    {
                        id = t.Id,
                        name = t.Name,
                        repxTemplateKey = t.RepxTemplateKey,
                        version = t.VersionNumber,
                        status = t.Status.ToString(),
                        createdAt = t.CreatedAt,
                        updatedAt = t.UpdatedAt,
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

        // POST: api/superadmin/templates/{id}/preview — renderiza el .repx con datos de ejemplo
        // (no reales), para poder ver cómo se ve el diseño sin necesitar un documento emitido.
        [HttpPost("{id:guid}/preview")]
        public async Task<IActionResult> Preview(Guid id)
        {
            try
            {
                var template = await _dbContext.DocumentTemplates
                    .Include(t => t.DocumentType)
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (template == null) return NotFound("Plantilla no encontrada.");
                if (string.IsNullOrWhiteSpace(template.RepxTemplateKey))
                {
                    return BadRequest("Esta plantilla aún no tiene un archivo .repx asociado.");
                }

                var data = Fel.Infrastructure.Services.TemplatePreviewSampleData.BuildSampleData(template.DocumentType?.Code ?? "", template.MostrarRetenciones);
                var pdfBytes = await _facilReportsClient.GenerateReportAsync(template.RepxTemplateKey, data);
                if (pdfBytes == null)
                {
                    return BadRequest("No se pudo generar la vista previa. Revisa que Facil Reports esté disponible.");
                }

                return File(pdfBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        public class CreateTemplateRequest
        {
            public string Name { get; set; } = string.Empty;
            public Guid DocumentTypeId { get; set; }
            public string RepxTemplateKey { get; set; } = string.Empty;
        }

        // POST: api/superadmin/templates
        [HttpPost]
        public async Task<IActionResult> CreateGlobalTemplate([FromBody] CreateTemplateRequest request)
        {
            try
            {
                var documentType = await _dbContext.DocumentTypes.FindAsync(request.DocumentTypeId);
                if (documentType == null) return NotFound("Tipo de documento no encontrado.");

                var newTemplate = new DocumentTemplate
                {
                    Id = Guid.NewGuid(),
                    Name = request.Name,
                    RepxTemplateKey = request.RepxTemplateKey,
                    Status = TemplateStatus.Draft,
                    VersionNumber = 1,
                    DocumentTypeId = request.DocumentTypeId,
                    TenantId = null,
                    ClientId = null,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.DocumentTemplates.Add(newTemplate);
                await _dbContext.SaveChangesAsync();

                return Ok(new { message = "Plantilla global creada en estado Borrador.", templateId = newTemplate.Id });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: api/superadmin/templates/upload — sube el archivo .repx directamente a Facil
        // Reports y crea la plantilla en un solo paso, en vez de exigir subirlo aparte (a mano,
        // por curl/Postman) y pegar aquí la clave resultante.
        [HttpPost("upload")]
        [RequestSizeLimit(20_000_000)]
        public async Task<IActionResult> UploadNewTemplate([FromForm] UploadTemplateForm form)
        {
            try
            {
                if (form.File == null || form.File.Length == 0)
                {
                    return BadRequest("Debes adjuntar un archivo .repx.");
                }

                if (!form.File.FileName.EndsWith(".repx", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest("El archivo debe tener extensión .repx.");
                }

                var documentType = await _dbContext.DocumentTypes.FindAsync(form.DocumentTypeId);
                if (documentType == null) return NotFound("Tipo de documento no encontrado.");

                var templateKey = $"facilfactura_{documentType.Code}_{Guid.NewGuid():N}".ToLowerInvariant();
                var bytes = await ReadBytesAsync(form.File);

                if (!await _facilReportsClient.UploadTemplateAsync(templateKey, bytes))
                {
                    return StatusCode(502, "No se pudo subir el archivo a Facil Reports. Revisa que el servicio esté configurado y disponible.");
                }

                var newTemplate = new DocumentTemplate
                {
                    Id = Guid.NewGuid(),
                    Name = form.Name,
                    RepxTemplateKey = templateKey,
                    Status = TemplateStatus.Draft,
                    VersionNumber = 1,
                    DocumentTypeId = form.DocumentTypeId,
                    TenantId = null,
                    ClientId = null,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.DocumentTemplates.Add(newTemplate);
                await _dbContext.SaveChangesAsync();

                return Ok(new { message = "Plantilla subida y creada en estado Borrador.", templateId = newTemplate.Id, templateKey });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        public class UploadTemplateForm
        {
            public IFormFile File { get; set; } = null!;
            public string Name { get; set; } = string.Empty;
            public Guid DocumentTypeId { get; set; }
        }

        public class PublishRequest
        {
            public string NewRepxTemplateKey { get; set; } = string.Empty;
        }

        // PUT: api/superadmin/templates/{id}/publish
        [HttpPut("{id:guid}/publish")]
        public async Task<IActionResult> PublishTemplate(Guid id, [FromBody] PublishRequest request)
        {
            try
            {
                var template = await _dbContext.DocumentTemplates
                    .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == null && t.ClientId == null);

                if (template == null)
                    return NotFound("Plantilla global no encontrada.");

                if (template.Status == TemplateStatus.Published)
                    return BadRequest("La plantilla ya está publicada.");

                if (template.Status == TemplateStatus.Archived)
                    return BadRequest("No puedes publicar una plantilla archivada.");

                if (!string.IsNullOrWhiteSpace(request.NewRepxTemplateKey))
                {
                    template.RepxTemplateKey = request.NewRepxTemplateKey;
                }

                // Si esta plantilla es una nueva versión de otra, archivar la vieja para evitar duplicados activos
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

                return Ok(new { message = "Plantilla global publicada correctamente. Ya es inmutable y visible para Tenants." });
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

        // POST: api/superadmin/templates/{id}/new-version
        [HttpPost("{id:guid}/new-version")]
        public async Task<IActionResult> CreateNewVersion(Guid id, [FromBody] NewVersionRequest request)
        {
            try
            {
                var sourceTemplate = await _dbContext.DocumentTemplates
                    .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == null && t.ClientId == null);

                if (sourceTemplate == null)
                    return NotFound("Plantilla global origen no encontrada.");

                if (sourceTemplate.Status != TemplateStatus.Published)
                    return BadRequest("Solo puedes versionar plantillas que estén Publicadas.");

                var newVersionTemplate = new DocumentTemplate
                {
                    Id = Guid.NewGuid(),
                    Name = sourceTemplate.Name, // Mantiene el nombre lógico
                    RepxTemplateKey = string.IsNullOrWhiteSpace(request.NewRepxTemplateKey) ? sourceTemplate.RepxTemplateKey : request.NewRepxTemplateKey,
                    Status = TemplateStatus.Draft,
                    VersionNumber = sourceTemplate.VersionNumber + 1,
                    PreviousVersionId = sourceTemplate.Id,
                    DocumentTypeId = sourceTemplate.DocumentTypeId,
                    TenantId = null,
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

        // POST: api/superadmin/templates/{id}/upload-new-version — mismo atajo que /upload, pero
        // versionando una plantilla global ya Publicada.
        [HttpPost("{id:guid}/upload-new-version")]
        [RequestSizeLimit(20_000_000)]
        public async Task<IActionResult> UploadNewVersion(Guid id, IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest("Debes adjuntar un archivo .repx.");
                }

                if (!file.FileName.EndsWith(".repx", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest("El archivo debe tener extensión .repx.");
                }

                var sourceTemplate = await _dbContext.DocumentTemplates
                    .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == null && t.ClientId == null);

                if (sourceTemplate == null)
                    return NotFound("Plantilla global origen no encontrada.");

                if (sourceTemplate.Status != TemplateStatus.Published)
                    return BadRequest("Solo puedes versionar plantillas que estén Publicadas.");

                var templateKey = $"facilfactura_v{sourceTemplate.VersionNumber + 1}_{Guid.NewGuid():N}".ToLowerInvariant();
                var bytes = await ReadBytesAsync(file);

                if (!await _facilReportsClient.UploadTemplateAsync(templateKey, bytes))
                {
                    return StatusCode(502, "No se pudo subir el archivo a Facil Reports. Revisa que el servicio esté configurado y disponible.");
                }

                var newVersionTemplate = new DocumentTemplate
                {
                    Id = Guid.NewGuid(),
                    Name = sourceTemplate.Name,
                    RepxTemplateKey = templateKey,
                    Status = TemplateStatus.Draft,
                    VersionNumber = sourceTemplate.VersionNumber + 1,
                    PreviousVersionId = sourceTemplate.Id,
                    DocumentTypeId = sourceTemplate.DocumentTypeId,
                    TenantId = null,
                    ClientId = null,
                    MostrarRetenciones = sourceTemplate.MostrarRetenciones,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.DocumentTemplates.Add(newVersionTemplate);
                await _dbContext.SaveChangesAsync();

                return Ok(new { message = $"Versión {newVersionTemplate.VersionNumber} subida en estado Borrador.", templateId = newVersionTemplate.Id, templateKey });
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

        // PUT: api/superadmin/templates/{id}/mostrar-retenciones
        [HttpPut("{id:guid}/mostrar-retenciones")]
        public async Task<IActionResult> SetMostrarRetenciones(Guid id, [FromBody] SetMostrarRetencionesRequest request)
        {
            try
            {
                var template = await _dbContext.DocumentTemplates
                    .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == null && t.ClientId == null);

                if (template == null)
                    return NotFound("Plantilla global no encontrada.");

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

        // DELETE: api/superadmin/templates/{id} — solo borradores sin versiones posteriores; una
        // plantilla Publicada se retira archivándola al publicar la siguiente versión, no borrándola.
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteTemplate(Guid id)
        {
            try
            {
                var template = await _dbContext.DocumentTemplates
                    .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == null && t.ClientId == null);
                if (template == null) return NotFound();

                if (template.Status == TemplateStatus.Published)
                {
                    return BadRequest("No puedes eliminar una plantilla publicada. Súbele una nueva versión para reemplazarla.");
                }

                if (await _dbContext.DocumentTemplates.AnyAsync(t => t.PreviousVersionId == id))
                {
                    return BadRequest("No puedes eliminar esta plantilla porque tiene una versión posterior que depende de ella.");
                }

                _dbContext.DocumentTemplates.Remove(template);
                await _dbContext.SaveChangesAsync();

                return Ok(new { message = "Plantilla eliminada." });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
