using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Api.Integration.Security;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Dataico;
using Fel.Infrastructure.Dian;
using Fel.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Integration.Controllers
{
    /// <summary>
    /// Facturas que el emisor recibió de sus proveedores y los eventos RADIAN que se registran sobre ellas
    /// (Acuse de Recibo 030, Reclamo 031, Recibo del Bien 032, Aceptación Expresa 033) — necesarios para que
    /// la factura circule como Título Valor o sea deducible de costos. Es el mismo motor que usa el portal:
    /// los eventos se firman y envían con el certificado del emisor o por Dataico, según su configuración.
    /// Solo aplican a facturas a crédito, y se ven los documentos de la sucursal dueña de la llave de API.
    /// </summary>
    [ApiController]
    [Route("api/co/dian/received-documents")]
    public class ReceptionEventsController : ControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly IReceptionEventService _eventService;
        private readonly IDataicoApiService _dataicoApiService;
        private readonly ICryptoService _cryptoService;

        public ReceptionEventsController(FelDbContext dbContext, IReceptionEventService eventService, IDataicoApiService dataicoApiService, ICryptoService cryptoService)
        {
            _dbContext = dbContext;
            _eventService = eventService;
            _dataicoApiService = dataicoApiService;
            _cryptoService = cryptoService;
        }

        public class ReceivedDocumentUploadRequest
        {
            /// <summary>XML de la factura recibida, o un ZIP con el XML (y el PDF si lo trae), en Base64.</summary>
            public string File { get; set; } = string.Empty;

            /// <summary>Nombre del archivo (opcional). El ZIP se reconoce también por su contenido.</summary>
            public string? FileName { get; set; }
        }

        private bool TryGetClientAndBranch(out Guid clientId, out Guid branchId)
        {
            clientId = Guid.Empty;
            branchId = Guid.Empty;
            if (HttpContext.Items["ClientId"] is not string s || !Guid.TryParse(s, out clientId)) return false;
            if (HttpContext.GetBranchId() is not Guid b) return false;
            branchId = b;
            return true;
        }

        private IActionResult NoEmisor() => Unauthorized(new { code = "INVALID_API_KEY", message = "No se pudo resolver el emisor autenticado." });

        private IActionResult NotFoundReceived() => NotFound(new { code = "RECEIVED_DOCUMENT_NOT_FOUND", message = "No existe un documento recibido con ese id para este emisor." });

        /// <summary>
        /// Documentos recibidos, del más reciente al más antiguo, con el estado de cada evento. <c>q</c> busca por proveedor, NIT o número
        /// (todas las palabras, en cualquier orden); <c>cufe</c> trae uno exacto.
        /// </summary>
        /// <response code="200">Página de documentos recibidos y el total.</response>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] string? cufe, [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
        {
            if (!TryGetClientAndBranch(out var clientId, out var branchId)) return NoEmisor();
            pageSize = Math.Clamp(pageSize, 1, 100);
            page = Math.Max(page, 1);

            var query = _dbContext.ReceivedDocuments.AsNoTracking().ForBranch(branchId).Where(d => d.ClientId == clientId);
            if (!string.IsNullOrWhiteSpace(cufe))
            {
                var exact = cufe.Trim();
                query = query.Where(d => d.Cufe == exact);
            }
            foreach (var term in (q ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var t = term;
                query = query.Where(d => d.IssuerName.Contains(t) || d.IssuerTaxId.Contains(t) || d.DocumentId.Contains(t));
            }

            var total = await query.CountAsync();
            var items = await query.OrderByDescending(d => d.IssueDate).ThenBy(d => d.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(d => new
                {
                    id = d.Id,
                    cufe = d.Cufe,
                    type = d.DocumentTypeCode,
                    number = d.DocumentId,
                    issuerTaxId = d.IssuerTaxId,
                    issuerName = d.IssuerName,
                    issueDate = d.IssueDate,
                    total = d.TotalAmount,
                    isCredit = d.IsCredit,
                    receivedAt = d.ReceivedAt,
                    events = d.Events.OrderBy(e => e.SentAt)
                        .Select(e => new { code = e.EventCode, status = e.Status, sentAt = e.SentAt }).ToList()
                })
                .ToListAsync();

            return Ok(new { items, total, page, pageSize });
        }

        /// <summary>
        /// Ingresa una factura recibida de un proveedor (XML, o ZIP con XML y PDF, en Base64). Si el emisor tiene eventos configurados para
        /// enviarse solos, se disparan al ingresar y vienen en la respuesta.
        /// </summary>
        /// <response code="201">Documento ingresado.</response>
        /// <response code="400">El archivo no se pudo leer o no es una factura válida.</response>
        /// <response code="409">Ya existe un documento recibido con ese CUFE/CUDE.</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Upload([FromBody] ReceivedDocumentUploadRequest request)
        {
            if (!TryGetClientAndBranch(out var clientId, out var branchId)) return NoEmisor();
            if (request == null || string.IsNullOrWhiteSpace(request.File))
                return BadRequest(new { code = "VALIDATION_ERROR", message = "'file' es obligatorio: el XML o el ZIP de la factura recibida, en Base64." });

            byte[] content;
            try
            {
                content = Convert.FromBase64String(request.File);
            }
            catch (FormatException)
            {
                return BadRequest(new { code = "VALIDATION_ERROR", message = "'file' no es un Base64 válido." });
            }

            string xml;
            byte[]? pdf;
            string? pdfFileName;
            try
            {
                (xml, pdf, pdfFileName) = ReceivedDocumentIngest.ReadFile(content, request.FileName);
            }
            catch (Exception ex)
            {
                return BadRequest(new { code = "INVALID_FILE", message = $"No se pudo leer el archivo: {ex.Message}" });
            }

            ParsedReceivedDocument parsed;
            try
            {
                parsed = ReceivedDocumentParser.Parse(xml);
            }
            catch (Exception ex)
            {
                return BadRequest(new { code = "INVALID_DOCUMENT", message = $"No se pudo interpretar el XML: {ex.Message}" });
            }

            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null) return NoEmisor();

            var existing = await _dbContext.ReceivedDocuments.AsNoTracking()
                .Where(d => d.ClientId == clientId && d.Cufe == parsed.Cufe).Select(d => d.Id).FirstOrDefaultAsync();
            if (existing != Guid.Empty)
                return Conflict(new { code = "DUPLICATE_RECEIVED_DOCUMENT", message = "Ya existe un documento recibido con ese CUFE/CUDE para este emisor.", id = existing });

            var received = ReceivedDocumentIngest.ToEntity(parsed, clientId, branchId, "Api", xml, pdf, pdfFileName);
            _dbContext.ReceivedDocuments.Add(received);
            await _dbContext.SaveChangesAsync();

            await _eventService.TriggerEnabledEventsAsync(received, client);

            var events = await EventsOf(received.Id);
            return Created($"/api/co/dian/received-documents/{received.Id}/events", new
            {
                id = received.Id,
                cufe = received.Cufe,
                type = received.DocumentTypeCode,
                number = received.DocumentId,
                issuerTaxId = received.IssuerTaxId,
                issuerName = received.IssuerName,
                issueDate = received.IssueDate,
                total = received.TotalAmount,
                isCredit = received.IsCredit,
                events
            });
        }

        /// <summary>
        /// Registra un evento RADIAN sobre un documento recibido. Orden que exige la DIAN: 030 primero; el 032 después del 030; el 033 y el 031
        /// después de ambos, y son excluyentes entre sí. Un evento ya aceptado no se reenvía: se devuelve el mismo.
        /// </summary>
        /// <param name="id">Id del documento recibido.</param>
        /// <param name="eventCode">030 Acuse de recibo, 031 Reclamo, 032 Recibo del bien o servicio, 033 Aceptación expresa.</param>
        /// <response code="200">Evento registrado (SENT) o ya existente (DUPLICATE).</response>
        /// <response code="404">No existe el documento recibido.</response>
        /// <response code="409">El evento ya se está procesando.</response>
        /// <response code="422">Evento no permitido (código inválido, factura que no es a crédito) o rechazado.</response>
        [HttpPost("{id:guid}/events/{eventCode}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> TriggerEvent(Guid id, string eventCode)
        {
            if (!TryGetClientAndBranch(out var clientId, out var branchId)) return NoEmisor();
            if (eventCode is not ("030" or "031" or "032" or "033"))
                return UnprocessableEntity(new { code = "INVALID_EVENT_CODE", message = "Código de evento inválido. Valores permitidos: 030, 031, 032, 033." });

            var received = await _dbContext.ReceivedDocuments.ForBranch(branchId).FirstOrDefaultAsync(d => d.Id == id && d.ClientId == clientId);
            if (received == null) return NotFoundReceived();
            if (!received.IsCredit)
                return UnprocessableEntity(new { code = "NOT_CREDIT_INVOICE", message = "Los eventos RADIAN solo aplican a facturas a crédito." });

            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null) return NoEmisor();

            var evt = await _eventService.TriggerEventAsync(received, client, eventCode);
            var body = new
            {
                id = evt.Id,
                code = evt.EventCode,
                status = evt.Status,
                cude = string.IsNullOrEmpty(evt.Cude) ? null : evt.Cude,
                number = evt.EventNumber,
                message = evt.DianResponseMessage,
                sentAt = evt.SentAt
            };
            return evt.Status switch
            {
                "SENT" or "DUPLICATE" => Ok(body),
                "PENDING" => Conflict(new { code = "EVENT_IN_PROGRESS", message = evt.DianResponseMessage, body.status }),
                _ => UnprocessableEntity(new { code = "EVENT_REJECTED", message = evt.DianResponseMessage, body.status })
            };
        }

        /// <summary>Historial de eventos de un documento recibido: estado, CUDE y respuesta de la DIAN de cada intento.</summary>
        /// <response code="200">Eventos, el más reciente primero.</response>
        /// <response code="404">No existe el documento recibido.</response>
        [HttpGet("{id:guid}/events")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEvents(Guid id)
        {
            if (!TryGetClientAndBranch(out var clientId, out var branchId)) return NoEmisor();
            var exists = await _dbContext.ReceivedDocuments.AsNoTracking().ForBranch(branchId).AnyAsync(d => d.Id == id && d.ClientId == clientId);
            if (!exists) return NotFoundReceived();
            return Ok(await EventsOf(id));
        }

        /// <summary>
        /// XML del evento (el que se envió a la DIAN). Con emisión directa es el que firmó la plataforma; con Dataico, el que ese proveedor
        /// entregó. Los eventos anteriores a esta función con emisión directa no lo tienen guardado (404). Se pide con el <c>id</c> del evento
        /// que devuelve el historial.
        /// </summary>
        /// <response code="200">El XML del evento (application/xml).</response>
        /// <response code="404">No existe el evento o no tiene archivo almacenado.</response>
        /// <response code="502">Dataico no permitió descargar el archivo.</response>
        [HttpGet("{id:guid}/events/{eventId:guid}/file")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> DownloadEventFile(Guid id, Guid eventId)
        {
            if (!TryGetClientAndBranch(out var clientId, out var branchId)) return NoEmisor();
            var eventRow = await _dbContext.ReceivedDocumentEvents
                .Include(e => e.ReceivedDocument).ThenInclude(d => d.Client).ThenInclude(c => c.Integrator)
                .Where(e => e.Id == eventId && e.ReceivedDocumentId == id && e.ReceivedDocument.ClientId == clientId && e.ReceivedDocument.BranchId == branchId)
                .FirstOrDefaultAsync();
            if (eventRow == null) return NotFound(new { code = "EVENT_NOT_FOUND", message = "No existe ese evento para este emisor." });

            var (outcome, bytes, fileName) = await ReceptionEventFiles.GetXmlAsync(eventRow, _dataicoApiService, _cryptoService);
            if (outcome == ReceptionEventFiles.Outcome.NotAvailable)
                return NotFound(new { code = "EVENT_FILE_NOT_AVAILABLE", message = "Este evento no tiene archivo almacenado." });
            if (outcome == ReceptionEventFiles.Outcome.ProviderFailed)
                return StatusCode(StatusCodes.Status502BadGateway, new { code = "PROVIDER_DOWNLOAD_FAILED", message = "Dataico no permitió descargar este archivo." });
            return File(bytes!, "application/xml", fileName);
        }

        private async Task<object> EventsOf(Guid receivedId) =>
            await _dbContext.ReceivedDocumentEvents.AsNoTracking()
                .Where(e => e.ReceivedDocumentId == receivedId)
                .OrderByDescending(e => e.SentAt)
                .Select(e => new { id = e.Id, code = e.EventCode, status = e.Status, cude = e.Cude, number = e.EventNumber, message = e.DianResponseMessage, sentAt = e.SentAt })
                .ToListAsync();
    }
}
