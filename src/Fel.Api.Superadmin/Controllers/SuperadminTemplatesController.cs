using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Superadmin.Controllers
{
    [ApiController]
    [Route("api/superadmin/templates")]
    public class SuperadminTemplatesController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public SuperadminTemplatesController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
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
                        previousVersionId = t.PreviousVersionId
                    })
                    .ToListAsync();

                return Ok(templates);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
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
    }
}
