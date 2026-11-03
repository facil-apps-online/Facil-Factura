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
    /// <summary>
    /// Documento Soporte electrónico (adquisiciones a sujetos no obligados a facturar) y su Nota de Ajuste. Igual que la factura, la emisión es síncrona y
    /// pasa por el mismo motor del portal: el documento se ve allí y sale por Dataico o por la DIAN directa según el emisor. El emisor es siempre el
    /// COMPRADOR (el de la API Key); el vendedor va en <c>customer</c>.
    /// </summary>
    [ApiController]
    [Route("api/co/dian/support-documents")]
    public class SupportDocumentsController : ControllerBase
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly FelDbContext _dbContext;
        private readonly SimpleSupportDocumentFactory _factory;
        private readonly SupportDocumentPublishingService _publishing;
        private readonly DocumentPdfService _pdf;

        public SupportDocumentsController(FelDbContext dbContext, SimpleSupportDocumentFactory factory, SupportDocumentPublishingService publishing, DocumentPdfService pdf)
        {
            _dbContext = dbContext;
            _factory = factory;
            _publishing = publishing;
            _pdf = pdf;
        }

        /// <summary>
        /// Emite un documento soporte. <c>customer</c> es el vendedor no obligado a facturar; <c>numbering</c> indica el prefijo de tu resolución de
        /// documento soporte y <c>number</c> es tu consecutivo. Las líneas no llevan impuestos. El servidor calcula los totales.
        /// </summary>
        /// <response code="201">Documento emitido. Trae el <c>id</c> (UUID), el número, el estado y el CUDS (<c>cufe</c>).</response>
        /// <response code="400">Datos inválidos o faltantes; <c>details</c> lista cada campo.</response>
        /// <response code="409">El número ya fue usado; trae la información del documento existente.</response>
        /// <response code="422">La resolución no existe o no está vigente, o la DIAN o el integrador rechazaron el documento.</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        public Task<IActionResult> Issue([FromBody] JsonElement body) => IssueAsync(body, false);

        /// <summary>
        /// Emite la nota de ajuste a un documento soporte aprobado. El documento se indica con <c>source_document_id</c> o <c>source_document_number</c>;
        /// <c>reason</c> es DEVOLUCION_PARCIAL, ANULACION, REBAJA, AJUSTE_DE_PRECIO u OTROS. Sin <c>items</c> la nota replica las líneas del documento.
        /// </summary>
        /// <response code="201">Nota emitida. Trae el <c>id</c> (UUID), el número, el estado y el CUDS.</response>
        /// <response code="400">Datos inválidos o faltantes; <c>details</c> lista cada campo.</response>
        /// <response code="404">El documento soporte no existe para este emisor.</response>
        /// <response code="409">El número de la nota ya fue usado; trae la información del documento existente.</response>
        /// <response code="422">El documento no está aprobado, o la DIAN o el integrador rechazaron la nota.</response>
        [HttpPost("adjustment")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        public Task<IActionResult> IssueAdjustment([FromBody] JsonElement body) => IssueAsync(body, true);

        private async Task<IActionResult> IssueAsync(JsonElement body, bool isAdjustment)
        {
            if (HttpContext.Items["ClientId"] is not string clientIdText || !Guid.TryParse(clientIdText, out var clientId))
                return Unauthorized(new { code = "INVALID_API_KEY", message = "No se pudo resolver el emisor autenticado." });

            ApiSupportRequest? request;
            var suppressEmail = false;
            try
            {
                var wrapper = isAdjustment ? "support_document_adjustment" : "support_document";
                var payload = body.ValueKind == JsonValueKind.Object && body.TryGetProperty(wrapper, out var inner) && inner.ValueKind == JsonValueKind.Object ? inner : body;
                suppressEmail = payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("send_email", out var flag) && flag.ValueKind == JsonValueKind.False;
                request = isAdjustment ? payload.Deserialize<ApiSupportAdjustmentRequest>(JsonOptions) : payload.Deserialize<ApiSupportRequest>(JsonOptions);
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

            var lockKey = $"ApiSupport:{clientId}:{(isAdjustment ? "A" : "D")}:{request.numbering?.prefix?.Trim()}:{request.number?.Trim()}";
            await using var numberLock = await SqlAppLock.AcquireAsync(_dbContext, lockKey);
            if (numberLock == null)
                return Conflict(new { code = "REQUEST_IN_PROGRESS", message = "Ese número ya se está procesando. Consulta su estado en unos segundos." });

            var built = await _factory.BuildAsync(request, isAdjustment, client, branchId);
            if (built.Error != null) return StatusCode(built.Error.Status, new { code = built.Error.Code, message = built.Error.Message, details = built.Error.Details });
            if (built.ExistingLocked != null) return AlreadyExists(built.ExistingLocked);

            var document = built.Document!;
            document.SuppressEmail = suppressEmail;
            if (!built.Reused) _dbContext.Documents.Add(document);
            await _dbContext.SaveChangesAsync();

            // El publicador espera el tipo y el vendedor cargados.
            document.DocumentType = await _dbContext.DocumentTypes.FirstOrDefaultAsync(t => t.Id == document.DocumentTypeId);

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
                // El contador del portal solo avanza, así que un número enviado antes que otro no lo retrasa.
                if (document.ResolutionId.HasValue && long.TryParse(document.Number, out var issued))
                {
                    if (isAdjustment)
                        await ResolutionNumbering.CommitNoteNumberAsync(_dbContext, clientId, document.BranchId, NoteKind.SupportAdjustment, document.ResolutionId.Value, document.NoteNumberingId, document.Prefix, issued);
                    else
                        await ResolutionNumbering.CommitNumberAsync(_dbContext, document.ResolutionId.Value, issued);
                }

                var warnings = new List<string>(built.Warnings);
                if (!string.IsNullOrWhiteSpace(outcome.Warning)) warnings.Add(outcome.Warning);
                object? files = string.Equals(Request.Query["include"], "files", StringComparison.OrdinalIgnoreCase) && document.Status == "APPROVED"
                    ? await DocumentFilesBuilder.BuildAsync(_dbContext, _pdf, document.Id, clientId)
                    : null;
                return Created($"/api/co/dian/documents/{document.Id}", new
                {
                    id = document.Id,
                    type = document.TypeCode,
                    number = FullNumber(document),
                    issueDate = document.IssueDate,
                    total = document.TotalAmount,
                    status = document.Status,
                    cufe = document.Cufe,
                    qr = document.QrCode,
                    dianResponse = DataicoDianOutcome.Readable(document.DianResponseMessage),
                    links = DocumentFilesBuilder.Links(document.Id),
                    files,
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
}
