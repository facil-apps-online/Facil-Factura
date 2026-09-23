using System;
using System.Threading.Tasks;
using Fel.Core.Models;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Integration.Controllers
{
    /// <summary>
    /// Recepción de Facturas Electrónicas del sector salud con soporte RIPS (Registro Individual
    /// de Prestación de Servicios de Salud).
    /// </summary>
    [ApiController]
    [Route("api/co/dian/health-invoices")]
    public class HealthInvoicesController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public HealthInvoicesController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <summary>
        /// Aún no implementado: la factura con RIPS adjunto todavía no tiene generación UBL
        /// propia en el motor directo-DIAN, ni envía nada a MinSalud. Antes esta acción encolaba
        /// el mensaje en "fel:documents:queue", una cola que ningún worker consume — respondía
        /// 202 sin procesar nada realmente. El RIPS independiente de factura (directo a MinSalud)
        /// sí funciona, vía <c>TenantDocumentsController</c>.
        ///
        /// A los Clients de prueba de developers se les responde una simulación en vez del 501,
        /// para que puedan programar contra la forma definitiva de la respuesta mientras tanto.
        /// </summary>
        /// <response code="200">Respuesta simulada (solo para Clients de prueba de developers).</response>
        /// <response code="501">Tipo de documento aún no disponible en el API.</response>
        [HttpPost("rips")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status501NotImplemented)]
        public async Task<IActionResult> ReceiveRipsInvoice([FromBody] HealthInvoiceRequest request)
        {
            if (await EsClientDePruebaAsync())
            {
                return Ok(new
                {
                    cufe = SandboxSimulation.NuevoIdentificadorFicticio(),
                    cuv = SandboxSimulation.NuevoIdentificadorFicticio(),
                    status = "PROCESSED",
                    simulated = true,
                    message = SandboxSimulation.MensajeNoImplementado(
                        "La Factura del Sector Salud con RIPS adjunto"),
                    trackId = SandboxSimulation.NuevoTrackId()
                });
            }

            return StatusCode(StatusCodes.Status501NotImplemented, new
            {
                message = "La Factura del Sector Salud con RIPS adjunto aún no está disponible en este API. Próximamente."
            });
        }

        private async Task<bool> EsClientDePruebaAsync()
        {
            if (HttpContext.Items["ClientId"] is not string clientIdStr || !Guid.TryParse(clientIdStr, out var clientId))
                return false;

            return await _dbContext.Clients.AsNoTracking()
                .AnyAsync(c => c.Id == clientId && c.IsDeveloperSandbox);
        }
    }
}
