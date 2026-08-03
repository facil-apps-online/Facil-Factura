using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Fel.Infrastructure.Services;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace Fel.Api.Tenant.Controllers
{
    [ApiController]
    [Route("api/v1/{country}/{entity}/[controller]")]
    public class TenantHabilitationController : ControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly DianHabilitationScraperService _scraperService;
        private readonly DianTestSetRunnerService _runnerService;

        public TenantHabilitationController(
            FelDbContext dbContext,
            DianHabilitationScraperService scraperService,
            DianTestSetRunnerService runnerService)
        {
            _dbContext = dbContext;
            _scraperService = scraperService;
            _runnerService = runnerService;
        }

        public class HabilitationRequestDto
        {
            /// <summary>
            /// El enlace de acceso que la DIAN envió al correo del representante legal.
            /// Ejemplo: https://catalogo-vpfe.dian.gov.co/User/Login?token=...
            /// </summary>
            public string MagicLink { get; set; } = string.Empty;

            /// <summary>
            /// El identificador único del Software Propio registrado en la DIAN.
            /// </summary>
            public string SoftwareId { get; set; } = string.Empty;

            /// <summary>
            /// El PIN de asociación del Software Propio.
            /// </summary>
            public string SoftwarePin { get; set; } = string.Empty;

            /// <summary>
            /// El NIT del cliente que se está habilitando (sin dígito de verificación).
            /// </summary>
            public string Nit { get; set; } = string.Empty;
        }

        /// <summary>
        /// Automatiza la extracción del TestSetId y la ejecución del Set de Pruebas de la DIAN usando el Magic Link.
        /// </summary>
        [HttpPost("auto")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> StartAutoHabilitation([FromRoute] string country, [FromRoute] string entity, [FromBody] HabilitationRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.MagicLink))
                return BadRequest(new { message = "El MagicLink es requerido." });

            if (string.IsNullOrWhiteSpace(request.Nit))
                return BadRequest(new { message = "El Nit del cliente es requerido para registrar el progreso." });

            if (!Guid.TryParse(entity, out var tenantGuid))
                return BadRequest(new { message = "El entity (TenantId) no es un GUID válido." });

            // 1. Buscar al cliente en la BD
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.TenantId == tenantGuid && c.TaxId == request.Nit);
            
            if (client == null)
            {
                return NotFound(new { message = $"No se encontró un cliente con NIT {request.Nit} bajo el tenant {entity}." });
            }

            // Actualizar estado a IN PROCESS
            client.DianHabilitationStatus = "Processing";
            client.DianHabilitationProgress = 10;
            client.DianHabilitationMessage = "Iniciando conexión segura con la DIAN...";
            await _dbContext.SaveChangesAsync();

            // 2. Ejecutar Scraper para robar sesión, asociar software (si no está) y extraer TestSetId
            var scrapeResult = await _scraperService.ExtractTestSetIdAsync(request.MagicLink, request.SoftwareId, request.SoftwarePin);

            if (!scrapeResult.IsSuccess)
            {
                client.DianHabilitationStatus = "Failed";
                client.DianHabilitationProgress = 0;
                client.DianHabilitationMessage = scrapeResult.ErrorMessage;
                await _dbContext.SaveChangesAsync();

                return StatusCode(StatusCodes.Status502BadGateway, new 
                { 
                    message = "Fallo en la automatización del portal DIAN", 
                    detail = scrapeResult.ErrorMessage 
                });
            }

            // 3. El scraper funcionó y tenemos el TestSetId
            client.DianHabilitationProgress = 30;
            client.DianHabilitationMessage = $"Software asociado. TestSetId extraído: {scrapeResult.TestSetId}. Lanzando Set de Pruebas...";
            await _dbContext.SaveChangesAsync();

            // 4. Iniciar Background Runner para emitir los documentos
            _runnerService.RunTestSetBackground(
                clientId: client.Id, 
                testSetId: scrapeResult.TestSetId, 
                requiredInvoices: scrapeResult.RequiredInvoices, 
                requiredCreditNotes: scrapeResult.RequiredCreditNotes, 
                requiredDebitNotes: scrapeResult.RequiredDebitNotes
            );

            return Accepted(new 
            { 
                message = "Proceso de Auto-Habilitación iniciado correctamente.", 
                testSetId = scrapeResult.TestSetId,
                statusUrl = $"/api/v1/{country}/{entity}/TenantHabilitation/status/{request.Nit}"
            });
        }

        /// <summary>
        /// Consulta el estado actual de la Auto-Habilitación de un cliente.
        /// </summary>
        [HttpGet("status/{nit}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetHabilitationStatus([FromRoute] string country, [FromRoute] string entity, [FromRoute] string nit)
        {
            if (!Guid.TryParse(entity, out var tenantGuid))
                return BadRequest(new { message = "El entity (TenantId) no es un GUID válido." });

            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.TenantId == tenantGuid && c.TaxId == nit);
            if (client == null) return NotFound(new { message = "Cliente no encontrado" });

            return Ok(new
            {
                nit = client.TaxId,
                status = client.DianHabilitationStatus,
                progress = client.DianHabilitationProgress,
                message = client.DianHabilitationMessage
            });
        }
    }
}
