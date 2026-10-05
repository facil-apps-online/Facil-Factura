using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
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
    }
}
