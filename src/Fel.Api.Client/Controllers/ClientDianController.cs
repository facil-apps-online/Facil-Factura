using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Fel.Api.Security;

namespace Fel.Api.Client.Controllers
{
    [ApiController]
    [Route("api/client/dian")]
    [ClientRole(ClientUserRoles.Administrator)]
    [AllowAllBranches]
    public class ClientDianController : ClientPortalControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly DianHabilitationScraperService _scraperService;
        private readonly DianTestSetSubmissionService _submissionService;
        private readonly ILogger<ClientDianController> _logger;

        public ClientDianController(FelDbContext dbContext, DianHabilitationScraperService scraperService, DianTestSetSubmissionService submissionService, ILogger<ClientDianController> logger)
        {
            _dbContext = dbContext;
            _scraperService = scraperService;
            _submissionService = submissionService;
            _logger = logger;
        }

        // El nombre y el PIN del software propio nunca vienen del caller — los genera/lee el
        // scraper (ver RegisterSoftwareAndReadTestSetAsync), por eso el request solo trae el
        // enlace mágico.
        public class HabilitationRequest
        {
            public string MagicLink { get; set; } = string.Empty;
        }

        [HttpPost("start-habilitation")]
        public async Task<IActionResult> StartHabilitation([FromBody] HabilitationRequest request)
        {
            try
            {
                var clientId = GetCurrentClientId();

                if (string.IsNullOrWhiteSpace(request.MagicLink))
                    return BadRequest("El enlace mágico es requerido.");

                if (!Fel.Infrastructure.Services.DianHabilitationScraperService.EsEnlaceValido(request.MagicLink))
                    return BadRequest("El enlace no parece un enlace mágico válido de la DIAN. Debe apuntar a catalogo-vpfe-hab.dian.gov.co (habilitación) o catalogo-vpfe.dian.gov.co (producción) e incluir el token.");

                var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == clientId);
                if (client == null) return NotFound("Cliente no encontrado.");

                client.DianHabilitationStatus = "Testing";
                client.DianHabilitationProgress = 10;
                client.DianHabilitationMessage = "Registrando software propio ante la DIAN...";
                await _dbContext.SaveChangesAsync();

                // 1. Registrar "Software propio" (si no estaba ya) y leer los datos reales del set
                // de pruebas — nombre estándar "FF - {Cliente}", nunca editable por el usuario.
                var softwareName = $"FF - {client.CompanyName}";
                var result = await _scraperService.RegisterSoftwareAndReadTestSetAsync(request.MagicLink, softwareName, client.SoftwareId);

                if (!result.IsSuccess)
                {
                    client.DianHabilitationStatus = "Failed";
                    client.DianHabilitationMessage = result.ErrorMessage;
                    await _dbContext.SaveChangesAsync();
                    return BadRequest(result.ErrorMessage);
                }

                // 2. Persistir lo que la DIAN devolvió — nunca lo que nosotros hubiéramos generado.
                if (!string.IsNullOrEmpty(result.SoftwareId)) client.SoftwareId = result.SoftwareId;
                if (!string.IsNullOrEmpty(result.SoftwarePin)) client.SoftwarePin = result.SoftwarePin;
                client.TestSetId = result.TestSetId;

                var testResolution = await _dbContext.Resolutions
                    .FirstOrDefaultAsync(r => r.ClientId == client.Id && r.DocumentType == "FE-TEST");

                if (testResolution == null)
                {
                    testResolution = new Resolution { Id = Guid.NewGuid(), ClientId = client.Id, DocumentType = "FE-TEST" };
                    _dbContext.Resolutions.Add(testResolution);
                    _dbContext.ResolutionBranches.Add(BranchProvisioning.LinkResolution(testResolution.Id, GetCurrentBranchId()));
                }

                testResolution.Prefix = result.Prefix;
                testResolution.ResolutionNumber = result.ResolutionNumber;
                testResolution.TechnicalKey = result.TechnicalKey;
                testResolution.NumberStart = result.RangeFromNumber;
                testResolution.NumberEnd = result.RangeToNumber;
                testResolution.NextNumber = null;
                testResolution.ValidFrom = result.ValidFrom;
                testResolution.ValidTo = result.ValidTo;
                testResolution.IsActive = true;
                testResolution.IsDefault = false;

                // Conteos reales exigidos por la DIAN para este TestSetId — gobiernan qué tipo de
                // documento envía DianTestSetSubmissionService en cada llamada a send-test-document.
                // Se reinician los "enviados" porque un TestSetId nuevo implica un set de pruebas nuevo.
                client.TestSetRequiredInvoices = result.RequiredInvoices;
                client.TestSetRequiredCreditNotes = result.RequiredCreditNotes;
                client.TestSetRequiredDebitNotes = result.RequiredDebitNotes;
                client.TestSetRequiredAcceptedInvoices = result.RequiredAcceptedInvoices;
                client.TestSetRequiredAcceptedCreditNotes = result.RequiredAcceptedCreditNotes;
                client.TestSetRequiredAcceptedDebitNotes = result.RequiredAcceptedDebitNotes;
                client.TestSetSentInvoices = 0;
                client.TestSetSentCreditNotes = 0;
                client.TestSetSentDebitNotes = 0;
                client.TestSetLastInvoiceCufe = null;
                client.TestSetLastInvoiceNumber = null;

                var hasCertificate = await _dbContext.Certificates.AnyAsync(c => c.ClientId == client.Id && c.IsActive);
                client.DianHabilitationProgress = 30;
                client.DianHabilitationMessage = hasCertificate
                    ? $"Set de pruebas listo: {result.RequiredAcceptedInvoices} factura(s) aceptada(s) requerida(s) de hasta {result.RequiredInvoices} intentos. Ya puedes enviar los documentos de prueba."
                    : $"Set de pruebas listo: {result.RequiredAcceptedInvoices} factura(s) aceptada(s) requerida(s) de hasta {result.RequiredInvoices} intentos. Falta cargar el certificado digital para poder enviarlas.";
                await _dbContext.SaveChangesAsync();

                return Ok(new
                {
                    message = "Software propio registrado y set de pruebas leído exitosamente.",
                    testSetId = result.TestSetId,
                    prefix = result.Prefix,
                    requiredInvoices = result.RequiredInvoices,
                    requiredCreditNotes = result.RequiredCreditNotes,
                    requiredDebitNotes = result.RequiredDebitNotes,
                    requiredAcceptedInvoices = result.RequiredAcceptedInvoices,
                    requiredAcceptedCreditNotes = result.RequiredAcceptedCreditNotes,
                    requiredAcceptedDebitNotes = result.RequiredAcceptedDebitNotes,
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
                    softwareId = client.SoftwareId,
                    softwarePin = client.SoftwarePin,
                    status = client.DianHabilitationStatus,
                    progress = client.DianHabilitationProgress,
                    message = client.DianHabilitationMessage,
                    testSet = new
                    {
                        requiredInvoices = client.TestSetRequiredInvoices,
                        requiredCreditNotes = client.TestSetRequiredCreditNotes,
                        requiredDebitNotes = client.TestSetRequiredDebitNotes,
                        requiredAcceptedInvoices = client.TestSetRequiredAcceptedInvoices,
                        requiredAcceptedCreditNotes = client.TestSetRequiredAcceptedCreditNotes,
                        requiredAcceptedDebitNotes = client.TestSetRequiredAcceptedDebitNotes,
                        sentInvoices = client.TestSetSentInvoices,
                        sentCreditNotes = client.TestSetSentCreditNotes,
                        sentDebitNotes = client.TestSetSentDebitNotes
                    }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // Envía UN documento del set de pruebas real a la DIAN (operación SOAP SendTestSetAsync).
        // Arma y firma el próximo documento de prueba SIN enviarlo a la DIAN ni gastar cupo del set
        // de pruebas — para revisar el XML antes de comprometer un intento real, que es limitado y
        // no se puede deshacer.
        [HttpGet("preview-test-document")]
        public async Task<IActionResult> PreviewTestDocument()
        {
            try
            {
                var clientId = GetCurrentClientId();
                var result = await _submissionService.PreviewNextTestDocumentAsync(clientId);

                if (!result.IsSuccess)
                    return BadRequest(new { message = result.ErrorMessage });

                return Ok(new
                {
                    documentKind = result.DocumentKind,
                    documentNumber = result.DocumentNumber,
                    cufe = result.Cufe,
                    signedXml = result.SignedXml
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        // Requiere que el cliente ya tenga TestSetId + resolución de prueba (start-habilitation) y
        // un certificado digital activo cargado — sin eso responde 400 con un mensaje claro en vez
        // de intentar firmar sin certificado.
        [HttpPost("send-test-document")]
        public async Task<IActionResult> SendTestDocument()
        {
            try
            {
                var clientId = GetCurrentClientId();
                var result = await _submissionService.SendNextTestDocumentAsync(clientId);

                if (!result.IsSuccess)
                    return BadRequest(new { message = result.ErrorMessage });

                return Ok(new
                {
                    documentNumber = result.DocumentNumber,
                    cufe = result.Cufe,
                    dianResponse = result.DianResponse,
                    trackId = result.TrackId
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        // Consulta el resultado de un envío anterior (trackId sacado de la respuesta cruda que
        // devolvió send-test-document) — separado a propósito para revisar cada resultado antes de
        // gastar el siguiente cupo del set de pruebas.
        [HttpGet("test-document-status")]
        public async Task<IActionResult> GetTestDocumentStatus([FromQuery] string trackId)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var result = await _submissionService.GetTestDocumentStatusAsync(clientId, trackId);

                if (!result.IsSuccess)
                    return BadRequest(new { message = result.ErrorMessage });

                var outcome = Fel.Infrastructure.Services.DianStatusOutcome.FromGetStatusZipResponse(result.DianResponse);
                return Ok(new
                {
                    resolved = outcome.Resolved,
                    accepted = outcome.Accepted,
                    statusDescription = outcome.StatusDescription,
                    rules = outcome.Rules
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }
    }
}
