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
    /// Recepción de eventos de un adquirente sobre una factura ya recibida (Acuse de Recibo,
    /// Recibo del Bien, Aceptación, Reclamo) — necesarios para que la factura circule como
    /// Título Valor (RADIAN) o sea deducible de costos.
    /// </summary>
    [ApiController]
    [Route("api/co/dian/reception-events")]
    public class ReceptionEventsController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public ReceptionEventsController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <summary>
        /// Aún no implementado: los eventos RADIAN todavía no tienen generación/envío propio en
        /// el motor directo-DIAN. Antes esta acción encolaba el mensaje en "fel:documents:queue",
        /// una cola que ningún worker consume — respondía 202 sin procesar nada realmente.
        ///
        /// A los Clients de prueba de developers se les responde una simulación en vez del 501,
        /// para que puedan programar contra la forma definitiva de la respuesta mientras tanto.
        /// </summary>
        /// <response code="200">Respuesta simulada (solo para Clients de prueba de developers).</response>
        /// <response code="501">Tipo de documento aún no disponible en el API.</response>
        [HttpPost]
        [ApiExplorerSettings(IgnoreApi = true)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status501NotImplemented)]
        public async Task<IActionResult> ReceiveEvent([FromBody] ReceptionEventRequest request)
        {
            if (await EsClientDePruebaAsync())
            {
                return Ok(new
                {
                    cude = SandboxSimulation.NuevoIdentificadorFicticio(),
                    status = "PROCESSED",
                    simulated = true,
                    message = SandboxSimulation.MensajeNoImplementado(
                        "El registro de eventos de recepción (Acuse/RADIAN)"),
                    trackId = SandboxSimulation.NuevoTrackId()
                });
            }

            return StatusCode(StatusCodes.Status501NotImplemented, new
            {
                message = "Los eventos de recepción (Acuse/RADIAN) aún no están disponibles en este API. Próximamente."
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
