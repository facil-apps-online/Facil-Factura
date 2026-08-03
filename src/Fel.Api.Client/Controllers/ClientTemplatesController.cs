using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Client.Controllers
{
    [ApiController]
    [Route("api/client/templates")]
    public class ClientTemplatesController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public ClientTemplatesController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        private Guid GetCurrentClientId()
        {
            if (Request.Headers.TryGetValue("x-client-id", out var clientIdStr))
            {
                if (Guid.TryParse(clientIdStr, out var clientId))
                    return clientId;
            }
            throw new UnauthorizedAccessException("x-client-id Header is missing");
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

                // Obtener plantillas publicadas globales o del Tenant de este cliente
                var availableTemplates = await _dbContext.DocumentTemplates
                    .Where(t => t.DocumentTypeId == documentTypeId && 
                                t.Status == TemplateStatus.Published &&
                                (t.TenantId == null || t.TenantId == client.TenantId))
                    .Select(t => new
                    {
                        id = t.Id,
                        name = t.Name,
                        isGlobal = t.TenantId == null
                    })
                    .ToListAsync();

                return Ok(availableTemplates);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // GET: api/client/templates/settings
        [HttpGet("settings")]
        public async Task<IActionResult> GetMySettings()
        {
            try
            {
                var clientId = GetCurrentClientId();

                var settings = await _dbContext.ClientDocumentSettings
                    .Include(s => s.DocumentType)
                    .Include(s => s.SelectedTemplate)
                    .Where(s => s.ClientId == clientId)
                    .Select(s => new
                    {
                        settingId = s.Id,
                        documentTypeId = s.DocumentTypeId,
                        documentTypeName = s.DocumentType != null ? s.DocumentType.Name : "N/A",
                        selectedTemplateId = s.SelectedTemplateId,
                        selectedTemplateName = s.SelectedTemplate.Name
                    })
                    .ToListAsync();

                return Ok(settings);
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

                // Validar que la plantilla exista y tenga permisos
                var template = await _dbContext.DocumentTemplates
                    .FirstOrDefaultAsync(t => t.Id == request.TemplateId && 
                                              t.DocumentTypeId == request.DocumentTypeId &&
                                              t.Status == TemplateStatus.Published &&
                                              (t.TenantId == null || t.TenantId == client.TenantId));

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
