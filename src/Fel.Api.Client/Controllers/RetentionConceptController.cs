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
    // Lectura del catálogo de conceptos de retención administrado por Superadmin. El formulario de
    // los documentos lo usa para que el cliente elija manualmente, por línea, cuál retención aplica
    // (no hay resolución automática por tipo de persona ni mínimo en UVT); se ofrece solo la
    // categoría "Retenciones Generales", igual para todos los clientes.
    [ApiController]
    [Route("api/client/retention-concepts")]
    public class RetentionConceptController : ClientPortalControllerBase
    {
        private readonly FelDbContext _dbContext;

        public RetentionConceptController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // Categoría del catálogo que se ofrece en las líneas de los documentos: la misma para todos los clientes, de menor a mayor tarifa.
        private const string LineRetentionGroupLabel = "Retenciones Generales";

        // Las retenciones que el cliente elige manualmente por línea de documento — sin ninguna resolución automática (ver
        // DocumentsPage.tsx). Son las activas de "Retenciones Generales", iguales para todos los clientes: ya no se filtra por lo
        // que el tenant habilitó (ClientEnabledRetentionConcept), y el alcance (factura o documento soporte) no cambia la lista.
        [HttpGet("catalog")]
        public async Task<IActionResult> GetCatalog([FromQuery] ProductScope scope = ProductScope.Invoice)
        {
            GetCurrentClientId();

            var concepts = await _dbContext.RetentionConcepts
                .Where(c => c.IsActive && c.GroupLabel == LineRetentionGroupLabel)
                .OrderBy(c => c.Rate).ThenBy(c => c.Name)
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
