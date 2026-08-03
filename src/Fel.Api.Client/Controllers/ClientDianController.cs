using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace Fel.Api.Client.Controllers
{
    [ApiController]
    [Route("api/client/dian")]
    public class ClientDianController : ControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly DianHabilitationScraperService _scraperService;
        private readonly DianTestSetRunnerService _runnerService;
        private readonly ILogger<ClientDianController> _logger;

        public ClientDianController(FelDbContext dbContext, DianHabilitationScraperService scraperService, DianTestSetRunnerService runnerService, ILogger<ClientDianController> logger)
        {
            _dbContext = dbContext;
            _scraperService = scraperService;
            _runnerService = runnerService;
            _logger = logger;
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

        public class HabilitationRequest
        {
            public string MagicLink { get; set; } = string.Empty;
            public string SoftwareId { get; set; } = string.Empty;
            public string SoftwarePin { get; set; } = string.Empty;
        }

        [HttpPost("start-habilitation")]
        public async Task<IActionResult> StartHabilitation([FromBody] HabilitationRequest request)
        {
            try
            {
                var clientId = GetCurrentClientId();

                if (string.IsNullOrWhiteSpace(request.MagicLink))
                    return BadRequest("El enlace mágico es requerido.");

                // Validar estructura básica del link
                if (!request.MagicLink.Contains("catalogo-vpfe.dian.gov.co") || !request.MagicLink.Contains("token="))
                    return BadRequest("El enlace proporcionado no parece ser un enlace válido de la DIAN.");

                var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == clientId);
                if (client == null) return NotFound("Cliente no encontrado.");

                client.DianHabilitationStatus = "Testing";
                client.DianHabilitationProgress = 10;
                client.DianHabilitationMessage = "Iniciando conexión con DIAN...";
                
                // Guardar datos de Software Propio
                if (!string.IsNullOrWhiteSpace(request.SoftwareId))
                    client.SoftwareId = request.SoftwareId;
                if (!string.IsNullOrWhiteSpace(request.SoftwarePin))
                    client.SoftwarePin = request.SoftwarePin;
                    
                await _dbContext.SaveChangesAsync();

                // 1. Scraping a la DIAN
                var result = await _scraperService.ExtractTestSetIdAsync(request.MagicLink, request.SoftwareId, request.SoftwarePin);

                if (!result.IsSuccess)
                {
                    client.DianHabilitationStatus = "Failed";
                    client.DianHabilitationMessage = result.ErrorMessage;
                    await _dbContext.SaveChangesAsync();
                    return BadRequest(result.ErrorMessage);
                }

                // 2. Actualizar el cliente con el TestSetId
                client.TestSetId = result.TestSetId;
                client.DianHabilitationProgress = 30;
                client.DianHabilitationMessage = "TestSetId extraído con éxito. Iniciando simulación...";
                await _dbContext.SaveChangesAsync();

                // 3. Disparar el Worker
                _runnerService.RunTestSetBackground(
                    client.Id, 
                    result.TestSetId, 
                    result.RequiredInvoices, 
                    result.RequiredCreditNotes, 
                    result.RequiredDebitNotes
                );

                return Ok(new
                {
                    message = "Set de Pruebas configurado exitosamente.",
                    testSetId = result.TestSetId,
                    requiredInvoices = result.RequiredInvoices,
                    requiredCreditNotes = result.RequiredCreditNotes,
                    requiredDebitNotes = result.RequiredDebitNotes,
                    status = client.DianHabilitationStatus
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error iniciando habilitación");
                return StatusCode(500, "Error interno del servidor procesando la habilitación.");
            }
        }
        
        [HttpGet("habilitation-status")]
        public async Task<IActionResult> GetHabilitationStatus()
        {
            try
            {
                var clientId = GetCurrentClientId();
                var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == clientId);
                if (client == null) return NotFound();

                return Ok(new
                {
                    testSetId = client.TestSetId,
                    status = client.DianHabilitationStatus,
                    progress = client.DianHabilitationProgress,
                    message = client.DianHabilitationMessage
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
