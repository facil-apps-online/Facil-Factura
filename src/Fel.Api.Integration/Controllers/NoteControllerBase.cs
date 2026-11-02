using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Fel.Api.Integration.Security;
using Fel.Api.Integration.Services;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Dataico;
using Fel.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Integration.Controllers
{
    // Emisión de notas crédito y débito: se arma el Document y se emite por el mismo motor del portal. Igual que la factura, es síncrona y el consecutivo
    // lo trae el integrador.
    public abstract class NoteControllerBase : ControllerBase
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly FelDbContext _dbContext;
        private readonly SimpleNoteFactory _factory;
        private readonly InvoicePublishingService _publishing;

        protected NoteControllerBase(FelDbContext dbContext, SimpleNoteFactory factory, InvoicePublishingService publishing)
        {
            _dbContext = dbContext;
            _factory = factory;
            _publishing = publishing;
        }

        protected async Task<IActionResult> IssueAsync(JsonElement body, string typeCode)
        {
            if (HttpContext.Items["ClientId"] is not string clientIdText || !Guid.TryParse(clientIdText, out var clientId))
                return Unauthorized(new { code = "INVALID_API_KEY", message = "No se pudo resolver el emisor autenticado." });

            ApiNoteRequest? request;
            try
            {
                var wrapper = typeCode == SimpleNoteFactory.CreditNote ? "credit_note" : "debit_note";
                var payload = body.ValueKind == JsonValueKind.Object && body.TryGetProperty(wrapper, out var inner) && inner.ValueKind == JsonValueKind.Object ? inner : body;
                request = payload.Deserialize<ApiNoteRequest>(JsonOptions);
            }
            catch (JsonException ex)
            {
                return BadRequest(new { code = "INVALID_JSON", message = "El cuerpo no tiene la estructura esperada.", details = new[] { ex.Message } });
            }
            if (request == null) return BadRequest(new { code = "INVALID_JSON", message = "El cuerpo está vacío." });

            var client = await _dbContext.Clients.Include(c => c.Integrator).FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null) return Unauthorized(new { code = "INVALID_API_KEY", message = "Emisor no encontrado." });
            if (HttpContext.GetBranchId() is not Guid branchId)
                return Unauthorized(new { code = "INVALID_API_KEY", message = "La API Key no corresponde a una sucursal." });

            var lockKey = $"ApiNote:{clientId}:{typeCode}:{request.numbering?.prefix?.Trim()}:{request.number?.Trim()}";
            await using var numberLock = await SqlAppLock.AcquireAsync(_dbContext, lockKey);
            if (numberLock == null)
                return Conflict(new { code = "REQUEST_IN_PROGRESS", message = "Ese número ya se está procesando. Consulta su estado en unos segundos." });

            var built = await _factory.BuildAsync(request, typeCode, client, branchId);
            if (built.Error != null) return StatusCode(built.Error.Status, new { code = built.Error.Code, message = built.Error.Message, details = built.Error.Details });
            if (built.ExistingLocked != null) return AlreadyExists(built.ExistingLocked);

            var document = built.Document!;
            if (!built.Reused) _dbContext.Documents.Add(document);
            await _dbContext.SaveChangesAsync();

            PublishOutcome outcome;
            try
            {
                outcome = await _publishing.PublishAsync(document, clientId);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new
                {
                    code = "PROVIDER_UNAVAILABLE",
                    message = "No se pudo confirmar la emisión con el proveedor. No reenvíes el mismo número: consulta el estado del documento.",
                    detail = ex.Message,
                    id = document.Id,
                    number = FullNumber(document),
                    status = document.Status,
                    statusUrl = $"/api/co/dian/documents/{document.Id}"
                });
            }

            if (outcome.Success)
            {
                // El contador de notas del portal avanza más allá del número usado para que no lo repita.
                if (document.ResolutionId.HasValue && long.TryParse(document.Number, out var issued))
                    await ResolutionNumbering.CommitNoteNumberAsync(_dbContext, clientId, document.BranchId, typeCode == SimpleNoteFactory.CreditNote ? NoteKind.CreditNote : NoteKind.DebitNote,
                        document.ResolutionId.Value, document.NoteNumberingId, document.Prefix, issued);

                var warnings = new List<string>(built.Warnings);
                if (!string.IsNullOrWhiteSpace(outcome.Warning)) warnings.Add(outcome.Warning);
                return Created($"/api/co/dian/documents/{document.Id}", new
                {
                    id = document.Id,
                    type = document.TypeCode,
                    number = FullNumber(document),
                    issueDate = document.IssueDate,
                    total = document.TotalAmount,
                    status = document.Status,
                    cufe = document.Cufe,
                    dianResponse = DataicoDianOutcome.Readable(document.DianResponseMessage),
                    warnings
                });
            }

            return outcome.Kind switch
            {
                PublishOutcomeKind.ProviderRejected => StatusCode(StatusCodes.Status422UnprocessableEntity, Rejected(document, "PROVIDER_REJECTED", outcome.Message, outcome.Detail)),
                PublishOutcomeKind.Conflict => AlreadyExists(document),
                PublishOutcomeKind.TemplateRequired => StatusCode(StatusCodes.Status422UnprocessableEntity, Rejected(document, "TEMPLATE_REQUIRED",
                    "Falta seleccionar la plantilla de esta clase de documento en Diseño de mis Facturas del portal.", null)),
                PublishOutcomeKind.NotFound => NotFound(new { code = "ISSUER_NOT_FOUND", message = outcome.Message }),
                _ => StatusCode(StatusCodes.Status422UnprocessableEntity, Rejected(document, "CANNOT_ISSUE", outcome.Message, null))
            };
        }

        private static string FullNumber(Document d) => $"{d.Prefix}{d.Number}";

        private static object Rejected(Document d, string code, string? message, string? detail) => new { code, message, detail, id = d.Id, number = FullNumber(d), status = d.Status };

        private IActionResult AlreadyExists(Document d) => Conflict(new
        {
            code = "DOCUMENT_ALREADY_EXISTS",
            message = $"El número {FullNumber(d)} ya fue usado por un documento en estado {d.Status}. Se devuelve su información; no se emitió uno nuevo.",
            id = d.Id,
            type = d.TypeCode,
            number = FullNumber(d),
            issueDate = d.IssueDate,
            total = d.TotalAmount,
            status = d.Status,
            cufe = d.Cufe,
            dianResponse = DataicoDianOutcome.Readable(d.DianResponseMessage)
        });
    }

    /// <summary>
    /// Emisión de la Nota Crédito Electrónica sobre una factura aprobada. Sin <c>items</c> la nota es total (anula la factura); con <c>items</c> es parcial.
    /// </summary>
    [ApiController]
    [Route("api/co/dian/credit-notes")]
    public class CreditNotesController : NoteControllerBase
    {
        public CreditNotesController(FelDbContext dbContext, SimpleNoteFactory factory, InvoicePublishingService publishing) : base(dbContext, factory, publishing) { }

        /// <summary>
        /// Emite una nota crédito. La factura se indica con <c>invoice_id</c> (el id devuelto al emitirla) o <c>invoice_number</c> (por ejemplo "SFRY1005").
        /// El <c>number</c> es tu consecutivo de notas; el cliente, la resolución y el medio de pago salen de la factura.
        /// </summary>
        /// <response code="201">Nota emitida. Trae el <c>id</c> (UUID), el número, el estado y el CUFE.</response>
        /// <response code="400">Datos inválidos o faltantes; <c>details</c> lista cada campo.</response>
        /// <response code="404">La factura referenciada no existe para este emisor.</response>
        /// <response code="409">El número de la nota ya fue usado; trae la información del documento existente.</response>
        /// <response code="422">La factura no está aprobada, la nota supera lo acreditable, o la DIAN o el integrador la rechazaron.</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        public Task<IActionResult> Issue([FromBody] JsonElement body) => IssueAsync(body, SimpleNoteFactory.CreditNote);
    }

    /// <summary>
    /// Emisión de la Nota Débito Electrónica sobre una factura aprobada (intereses, gastos por cobrar, cambio de valor u otros).
    /// </summary>
    [ApiController]
    [Route("api/co/dian/debit-notes")]
    public class DebitNotesController : NoteControllerBase
    {
        public DebitNotesController(FelDbContext dbContext, SimpleNoteFactory factory, InvoicePublishingService publishing) : base(dbContext, factory, publishing) { }

        /// <summary>
        /// Emite una nota débito. Igual que la nota crédito, pero <c>items</c> es obligatorio: lleva el valor que se suma a la factura.
        /// </summary>
        /// <response code="201">Nota emitida. Trae el <c>id</c> (UUID), el número, el estado y el CUFE.</response>
        /// <response code="400">Datos inválidos o faltantes; <c>details</c> lista cada campo.</response>
        /// <response code="404">La factura referenciada no existe para este emisor.</response>
        /// <response code="409">El número de la nota ya fue usado; trae la información del documento existente.</response>
        /// <response code="422">La factura no está aprobada, o la DIAN o el integrador rechazaron la nota.</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        public Task<IActionResult> Issue([FromBody] JsonElement body) => IssueAsync(body, SimpleNoteFactory.DebitNote);
    }
}
