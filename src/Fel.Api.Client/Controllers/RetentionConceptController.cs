using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Api.Security;
using Fel.Core.Entities;
using Fel.Core.Models;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Client.Controllers
{
    // Lectura del catálogo de conceptos de retención (RetefUENTE/ReteIVA), administrado por
    // Superadmin, filtrado a lo que el Tenant habilitó para ESTE Client (ver
    // ClientEnabledRetentionConcept) — antes se ofrecía el catálogo completo (42 variantes,
    // incluida ReteIVA) a cualquier Client. El formulario de factura lo usa para que el cliente
    // elija manualmente, por línea, cuál retención aplica (no hay resolución automática por tipo
    // de persona ni mínimo en UVT).
    [ApiController]
    [Route("api/client/retention-concepts")]
    public class RetentionConceptController : ClientPortalControllerBase
    {
        private readonly FelDbContext _dbContext;

        public RetentionConceptController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // Solo las variantes habilitadas para este Client, para que el cliente elija manualmente
        // por línea de factura cuál retención aplica — sin ninguna resolución automática (ver
        // InvoicesPage.tsx).
        [HttpGet("catalog")]
        public async Task<IActionResult> GetCatalog([FromQuery] ProductScope scope = ProductScope.Invoice)
        {
            var clientId = GetCurrentClientId();

            var enabledIds = await _dbContext.ClientEnabledRetentionConcepts
                .Where(e => e.ClientId == clientId)
                .Where(e => e.Scope == scope)
                .Select(e => e.RetentionConceptId)
                .ToListAsync();

            var concepts = await _dbContext.RetentionConcepts
                .Where(c => c.IsActive && enabledIds.Contains(c.Id))
                .OrderBy(c => c.GroupLabel).ThenBy(c => c.Name)
                .Select(c => new
                {
                    c.Id,
                    c.GroupKey,
                    c.GroupLabel,
                    c.Name,
                    PersonType = c.PersonType.ToString(),
                    c.TaxCategory,
                    BaseType = c.BaseType.ToString(),
                    c.BaseUvt,
                    c.Rate
                })
                .ToListAsync();

            return Ok(new { concepts });
        }

        // Nombre corto de cada categoría de retención (RETE FUENTE, RETE ICA, RETE IVA), que parametriza la administración de la plataforma. Con la tarifa
        // arma el texto corto de la retención en los selectores del formulario.
        [HttpGet("short-names")]
        public async Task<IActionResult> GetShortNames()
        {
            GetCurrentClientId();
            var names = await RetentionShortNameCatalog.LoadAsync(_dbContext);
            return Ok(new[] { "RET_FUENTE", "RET_ICA", "RET_IVA" }.ToDictionary(c => c, c => RetentionShortNameCatalog.NameOf(names, c)));
        }

        // Cómo ve el cliente las retenciones en los selectores: las de cada línea y las del pie, por separado. Lo cambia el administrador del cliente y también
        // el tenant desde la ficha del cliente; manda el último cambio.
        [ClientRole(ClientUserRoles.Administrator)]
        [HttpPut("label-style")]
        public async Task<IActionResult> SetLabelStyle([FromBody] RetentionLabelStyleRequest request)
        {
            if (!RetentionLabelStyle.IsValid(request.Line) || !RetentionLabelStyle.IsValid(request.Footer))
                return BadRequest(new { message = "El estilo debe ser Short (nombre corto) o Full (nombre completo)." });

            var clientId = GetCurrentClientId();
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null) return NotFound(new { message = "Cliente no encontrado." });

            client.LineRetentionLabelStyle = request.Line!;
            client.FooterRetentionLabelStyle = request.Footer!;
            await _dbContext.SaveChangesAsync();
            return Ok(new { line = client.LineRetentionLabelStyle, footer = client.FooterRetentionLabelStyle });
        }
    }

    public class RetentionLabelStyleRequest
    {
        public string? Line { get; set; }
        public string? Footer { get; set; }
    }
}
