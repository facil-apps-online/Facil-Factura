using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Api.Security;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Client.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AllowAllBranches]
    public class ClientController : ClientPortalControllerBase
    {
        private readonly FelDbContext _dbContext;

        public ClientController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // Datos mínimos del emisor autenticado que el formulario de factura necesita para
        // previsualizar cálculos (ej. si aplica retenciones automáticas) sin tener que guardar
        // primero para verlos.
        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            var client = await _dbContext.Clients.FindAsync(GetCurrentClientId());
            if (client == null) return NotFound();

            return Ok(new { client.Id, client.AppliesRetentions, client.ElectronicInvoiceLegend, client.SupportDocumentLegend });
        }

        [ClientRole(ClientUserRoles.Administrator)]
        [HttpPut("document-legends")]
        public async Task<IActionResult> UpdateDocumentLegends([FromBody] UpdateDocumentLegendsRequest request)
        {
            var client = await _dbContext.Clients.FindAsync(GetCurrentClientId());
            if (client == null) return NotFound();
            client.ElectronicInvoiceLegend = request.ElectronicInvoiceLegend?.Trim() ?? string.Empty;
            client.SupportDocumentLegend = request.SupportDocumentLegend?.Trim() ?? string.Empty;
            await _dbContext.SaveChangesAsync();
            return Ok(new { client.ElectronicInvoiceLegend, client.SupportDocumentLegend });
        }
    }

    public class UpdateDocumentLegendsRequest
    {
        public string? ElectronicInvoiceLegend { get; set; }
        public string? SupportDocumentLegend { get; set; }
    }
}
