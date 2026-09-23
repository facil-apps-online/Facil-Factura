using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Tenant.Controllers
{
    /// <summary>
    /// Permite al tenant ver y asignar, en nombre de un Client puntual, la plantilla que usa cada
    /// tipo de documento — mismo alcance de datos que <c>api/client/templates</c>, pero consultado
    /// por el tenant para uno de sus clientes en vez de por el cliente para sí mismo.
    /// </summary>
    [ApiController]
    [Route("api/tenant/clients/{clientId}/templates")]
    public class TenantClientTemplatesController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public TenantClientTemplatesController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
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

        private async Task<Client?> GetOwnedClientAsync(Guid clientId)
        {
            var tenantId = GetCurrentTenantId();
            return await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == clientId && c.TenantId == tenantId);
        }

        // GET: api/tenant/clients/{clientId}/templates/settings
        //
        // Parte de los tipos de documento HABILITADOS para este cliente (ClientEnabledDocumentTypes,
        // sembrados al crear el cliente), no de ClientDocumentSettings — antes solo devolvía filas
        // que ya existían, así que un cliente que nunca había elegido plantilla aparecía con la
        // lista vacía y el tenant no tenía en qué hacer clic para asignar la primera vez.
        [HttpGet("settings")]
        public async Task<IActionResult> GetSettings(Guid clientId)
        {
            var client = await GetOwnedClientAsync(clientId);
            if (client == null) return StatusCode(StatusCodes.Status403Forbidden);

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

        // GET: api/tenant/clients/{clientId}/templates/available/{documentTypeId}
        [HttpGet("available/{documentTypeId:guid}")]
        public async Task<IActionResult> GetAvailableTemplates(Guid clientId, Guid documentTypeId)
        {
            var client = await GetOwnedClientAsync(clientId);
            if (client == null) return StatusCode(StatusCodes.Status403Forbidden);

            // Mismo alcance que api/client/templates/available: globales, del Tenant, o propias de
            // este Client puntual — nunca las propias de otro Client del mismo Tenant.
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
    }
}
