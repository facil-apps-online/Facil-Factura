using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Fel.Api.Integration.Security;
using Fel.Api.Integration.Services;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Dataico;
using Fel.Infrastructure.Dataico.Models;
using Fel.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Integration.Controllers
{
    /// <summary>
    /// Emisión de la Factura Electrónica de Venta (estándar, de mandato y de exportación) con un JSON sencillo, con la misma estructura
    /// que recibe Dataico. El emisor sale de la API Key; los totales los calcula el servidor.
    /// </summary>
    /// <remarks>
    /// El documento se crea y se emite por el mismo motor del portal (directo a la DIAN o por el integrador que tenga configurado el emisor),
    /// así que también aparece en el portal. La respuesta es síncrona: trae el resultado final de la emisión.
    /// </remarks>
    [ApiController]
    [Route("api/co/dian/invoices")]
    public class InvoicesController : ControllerBase
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly FelDbContext _dbContext;
        private readonly SimpleInvoiceFactory _factory;
        private readonly InvoicePublishingService _publishing;

        public InvoicesController(FelDbContext dbContext, SimpleInvoiceFactory factory, InvoicePublishingService publishing)
        {
            _dbContext = dbContext;
            _factory = factory;
            _publishing = publishing;
        }

        /// <summary>
        /// Emite una factura. El consecutivo (<c>number</c>) lo lleva el integrador: puede enviarse en cualquier orden mientras esté
        /// dentro del rango de la resolución y no esté usado.
        /// </summary>
        /// <param name="body">El JSON de la factura, directo o dentro de <c>{ "invoice": { ... } }</c> como lo envía Dataico.</param>
        /// <response code="201">Factura emitida. Trae el <c>id</c> (UUID), el número, el estado y el CUFE.</response>
        /// <response code="400">Datos inválidos o faltantes; <c>details</c> lista cada campo.</response>
        /// <response code="409">El número ya fue usado. Trae el <c>id</c> y el estado del documento existente.</response>
        /// <response code="422">La DIAN o el integrador rechazaron la factura, o el emisor no está listo (resolución, rango, certificado).</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Issue([FromBody] JsonElement body)
        {
            if (HttpContext.Items["ClientId"] is not string clientIdText || !Guid.TryParse(clientIdText, out var clientId))
                return Unauthorized(new { code = "INVALID_API_KEY", message = "No se pudo resolver el emisor autenticado." });

            DataicoInvoiceRequest? request;
            try
            {
                var payload = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("invoice", out var inner) && inner.ValueKind == JsonValueKind.Object ? inner : body;
                request = payload.Deserialize<DataicoInvoiceRequest>(JsonOptions);
            }
            catch (JsonException ex)
            {
                return BadRequest(new { code = "INVALID_JSON", message = "El cuerpo no tiene la estructura esperada.", details = new[] { ex.Message } });
            }
            if (request == null) return BadRequest(new { code = "INVALID_JSON", message = "El cuerpo está vacío." });

            var client = await _dbContext.Clients.Include(c => c.Integrator).FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null) return Unauthorized(new { code = "INVALID_API_KEY", message = "Emisor no encontrado." });

            // Un solo envío a la vez por prefijo y número: dos peticiones simultáneas del mismo consecutivo no crean dos documentos.
            var lockKey = $"ApiInvoice:{clientId}:{request.numbering?.prefix?.Trim()}:{request.number?.Trim()}";
            await using var numberLock = await SqlAppLock.AcquireAsync(_dbContext, lockKey);
            if (numberLock == null)
                return Conflict(new { code = "REQUEST_IN_PROGRESS", message = "Ese número ya se está procesando. Consulta su estado en unos segundos." });

            if (HttpContext.GetBranchId() is not Guid branchId)
                return Unauthorized(new { code = "INVALID_API_KEY", message = "La API Key no corresponde a una sucursal." });

            var built = await _factory.BuildAsync(request, client, branchId);
            if (built.Error != null) return Problem(built.Error);
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
                // El proveedor no contestó o falló a mitad del envío: el documento puede haber llegado. Queda marcado como incierto.
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
                    await ResolutionNumbering.CommitNumberAsync(_dbContext, document.ResolutionId.Value, issued);

                return Created($"/api/co/dian/documents/{document.Id}", Summary(document, built.Warnings, outcome.Warning));
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

        private static object Summary(Document d, List<string> warnings, string? warning)
        {
            var all = new List<string>(warnings);
            if (!string.IsNullOrWhiteSpace(warning)) all.Add(warning);
            return new
            {
                id = d.Id,
                type = d.TypeCode,
                number = FullNumber(d),
                issueDate = d.IssueDate,
                total = d.TotalAmount,
                status = d.Status,
                cufe = d.Cufe,
                dianResponse = DataicoDianOutcome.Readable(d.DianResponseMessage),
                warnings = all
            };
        }

        private static object Rejected(Document d, string code, string? message, string? detail) => new
        {
            code,
            message,
            detail,
            id = d.Id,
            number = FullNumber(d),
            status = d.Status
        };

        // El número ya está usado por un documento que no se puede cambiar: se devuelve lo que se tiene de él.
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

        private IActionResult Problem(ApiError error) =>
            StatusCode(error.Status, new { code = error.Code, message = error.Message, details = error.Details });
    }
}
