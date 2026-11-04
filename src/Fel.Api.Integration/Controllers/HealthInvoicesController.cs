using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Fel.Api.Integration.Security;
using Fel.Api.Integration.Services;
using Fel.Core.Entities;
using Fel.Core.Models;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Dataico.Models;
using Fel.Infrastructure.MinSalud;
using Fel.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Integration.Controllers
{
    /// <summary>
    /// Factura Electrónica de Venta del sector salud con el RIPS como soporte. Se emite directo a la DIAN con la extensión de salud del Ministerio
    /// (Documento Técnico 2) y, ya aprobada, el RIPS se envía al Mecanismo Único de Validación del Ministerio, que devuelve el CUV.
    /// </summary>
    /// <remarks>
    /// Solo con emisión directa (NATIVE). El mismo JSON de la factura más el bloque <c>health</c> y, opcional, el bloque <c>rips</c>. Si el RIPS viaja
    /// en la factura se valida antes de emitir; si no, se envía después con <c>POST health-invoices/{id}/rips</c>.
    /// </remarks>
    [ApiController]
    [Route("api/co/dian/health-invoices")]
    public class HealthInvoicesController : InvoiceControllerBase
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly FelDbContext _db;
        private readonly IHealthRipsService _rips;
        private List<string> _ripsWarnings = new();

        public HealthInvoicesController(FelDbContext dbContext, SimpleInvoiceFactory factory, InvoicePublishingService publishing, DocumentPdfService pdf, IHealthRipsService rips)
            : base(dbContext, factory, publishing, pdf)
        {
            _db = dbContext;
            _rips = rips;
        }

        /// <summary>
        /// Emite una factura del sector salud. Requiere una resolución activa de factura. El bloque <c>health</c> lleva <c>provider_code</c> (10 dígitos),
        /// <c>payment_modality</c>, <c>coverage</c>, <c>contract_number</c> o <c>policy_number</c>, <c>period_start</c>, <c>period_end</c> y los recaudos
        /// (<c>copayment</c>, <c>moderator_fee</c>, <c>shared_payments</c>, <c>advance</c>). El bloque <c>rips</c> es el RIPS soporte (Resolución 2275 de 2023).
        /// </summary>
        /// <response code="201">Factura emitida. Trae el estado del RIPS en <c>rips</c> (<c>ACCEPTED</c> con el CUV, <c>REJECTED</c> con las glosas, <c>PENDING</c> o <c>NOT_SENT</c>).</response>
        /// <response code="400">Datos inválidos o faltantes; <c>details</c> lista cada campo.</response>
        /// <response code="409">El número ya fue usado.</response>
        /// <response code="422">El emisor no usa emisión directa, el RIPS no cuadra con la factura, o la DIAN rechazó el documento.</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        public Task<IActionResult> Issue([FromBody] JsonElement body) => IssueAsync(body, "SALUD");

        protected override Task<ApiError?> ValidateBuiltAsync(DataicoInvoiceRequest request, Client client, Document document)
        {
            if (client.Integrator?.Code != "NATIVE")
                return Task.FromResult<ApiError?>(new ApiError(422, "HEALTH_NATIVE_ONLY",
                    "La factura del sector salud solo se emite con emisión directa a la DIAN; este emisor usa otro integrador."));

            if (request.rips is not { } rips || rips.ValueKind == JsonValueKind.Null) return Task.FromResult<ApiError?>(null);
            var health = HealthSectorData.TryRead(document.SectorExtensionData);
            if (health == null) return Task.FromResult<ApiError?>(null);

            var check = HealthRipsValidator.Validate(rips, health, client.TaxId, $"{document.Prefix}{document.Number}", document.TotalAmount);
            _ripsWarnings = check.Warnings;
            return Task.FromResult<ApiError?>(check.IsValid ? null
                : new ApiError(422, "RIPS_INVALID", "El RIPS no cumple las reglas del Ministerio de Salud. No se emitió la factura.", check.Errors));
        }

        protected override async Task<(string Key, object Value)?> AfterIssuedAsync(DataicoInvoiceRequest request, Client client, Document document, Guid branchId)
        {
            if (document.Status != "APPROVED")
                return ("rips", new { status = "NOT_SENT", message = "La factura no quedó aprobada por la DIAN; el RIPS se envía con POST /api/co/dian/health-invoices/{id}/rips cuando lo esté." });
            if (request.rips is not { } rips || rips.ValueKind != JsonValueKind.Object)
                return ("rips", new { status = "NOT_SENT", message = "La factura no trajo RIPS. Envíalo con POST /api/co/dian/health-invoices/{id}/rips." });

            var row = await _rips.SubmitAsync(document, client, rips.GetRawText());
            return ("rips", RipsSummary(row, _ripsWarnings));
        }

        /// <summary>
        /// Valida sin emitir: el bloque <c>health</c> y el RIPS, y los cruces entre los dos. Opcionalmente <c>invoice_number</c> y <c>total</c> para
        /// contrastar el número de la factura y el valor de los servicios.
        /// </summary>
        /// <response code="200">Resultado con <c>valid</c>, <c>errors</c> y <c>warnings</c>.</response>
        [HttpPost("validate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Validate([FromBody] JsonElement body)
        {
            if (HttpContext.Items["ClientId"] is not string clientIdText || !Guid.TryParse(clientIdText, out var clientId))
                return Unauthorized(new { code = "INVALID_API_KEY", message = "No se pudo resolver el emisor autenticado." });
            var client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null) return Unauthorized(new { code = "INVALID_API_KEY", message = "Emisor no encontrado." });

            var payload = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("invoice", out var inner) && inner.ValueKind == JsonValueKind.Object ? inner : body;
            DataicoInvoiceRequest? request;
            try { request = payload.Deserialize<DataicoInvoiceRequest>(JsonOptions); }
            catch (JsonException ex) { return BadRequest(new { code = "INVALID_JSON", message = "El cuerpo no tiene la estructura esperada.", details = new[] { ex.Message } }); }
            if (request == null) return BadRequest(new { code = "INVALID_JSON", message = "El cuerpo está vacío." });

            var errors = new List<string>();
            var warnings = new List<string>();
            decimal? total = payload.TryGetProperty("total", out var totalValue) && totalValue.TryGetDecimal(out var t) ? t : null;
            if (request.health == null) errors.Add("health: es obligatorio.");
            else
            {
                var problems = new List<string>();
                var health = SimpleInvoiceFactory.ReadHealth(request.health, SimpleInvoiceFactory.ParseDate(request.issue_date) ?? ColombiaTime.Now, problems);
                errors.AddRange(problems);
                if (health != null)
                {
                    errors.AddRange(HealthCatalogs.Validate(health.OperationType, health, total ?? decimal.MaxValue).Select(e => "health: " + e));
                    if (request.rips is { ValueKind: JsonValueKind.Object } rips)
                    {
                        var number = $"{request.numbering?.prefix?.Trim()}{request.number?.Trim()}";
                        var check = HealthRipsValidator.Validate(rips, health, client.TaxId, number, total);
                        errors.AddRange(check.Errors);
                        warnings.AddRange(check.Warnings);
                    }
                }
            }
            return Ok(new { valid = errors.Count == 0, errors, warnings });
        }

        /// <summary>
        /// Envía (o reenvía) el RIPS de una factura ya aprobada por la DIAN al Ministerio. Con <c>{ "rips": { ... } }</c> reemplaza el guardado; sin
        /// cuerpo reenvía el último. Una factura cuyo RIPS ya fue aceptado devuelve su CUV sin volver a enviarse.
        /// </summary>
        /// <response code="200">El Ministerio aceptó el RIPS; trae el CUV.</response>
        /// <response code="404">La factura no existe.</response>
        /// <response code="409">La factura no es de salud o todavía no está aprobada.</response>
        /// <response code="422">El RIPS no cuadra con la factura o el Ministerio lo rechazó; trae las glosas.</response>
        /// <response code="502">No se pudo contactar al Ministerio; reintenta con el mismo cuerpo.</response>
        [HttpPost("{id:guid}/rips")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> SendRips(Guid id, [FromBody] JsonElement? body)
        {
            var (document, client, error) = await FindAsync(id);
            if (error != null) return error;
            if (client!.Integrator?.Code != "NATIVE")
                return StatusCode(422, new { code = "HEALTH_NATIVE_ONLY", message = "El RIPS soporte solo se envía para emisores con emisión directa." });
            if (document!.Status != "APPROVED")
                return Conflict(new { code = "DOCUMENT_NOT_APPROVED", message = $"La factura está en estado {document.Status}; el RIPS se envía cuando la DIAN la apruebe." });

            string? ripsJson = null;
            var payload = body is { ValueKind: JsonValueKind.Object } b && b.TryGetProperty("rips", out var r) ? r : (JsonElement?)null;
            var stored = await _db.DocumentRips.AsNoTracking().FirstOrDefaultAsync(x => x.DocumentId == id);
            if (payload is { ValueKind: JsonValueKind.Object } given)
            {
                var health = HealthSectorData.TryRead(document.SectorExtensionData);
                if (health == null) return Conflict(new { code = "NOT_A_HEALTH_INVOICE", message = "El documento no tiene datos de salud." });
                var check = HealthRipsValidator.Validate(given, health, client.TaxId, $"{document.Prefix}{document.Number}", document.TotalAmount);
                if (!check.IsValid)
                    return StatusCode(422, new { code = "RIPS_INVALID", message = "El RIPS no cumple las reglas del Ministerio de Salud.", details = check.Errors, warnings = check.Warnings });
                ripsJson = given.GetRawText();
            }
            else if (stored == null)
            {
                return BadRequest(new { code = "VALIDATION_ERROR", message = "La factura no tiene RIPS guardado; envía { \"rips\": { ... } }." });
            }

            var row = await _rips.SubmitAsync(document, client, ripsJson);
            var summary = RipsSummary(row, new List<string>());
            return row.Status switch
            {
                DocumentRips.Accepted => Ok(summary),
                DocumentRips.Rejected => StatusCode(422, new { code = "RIPS_REJECTED", message = row.Message, rips = summary }),
                _ => StatusCode(502, new { code = "MUV_UNAVAILABLE", message = row.Message, rips = summary })
            };
        }

        /// <summary>Estado del RIPS de una factura: <c>ACCEPTED</c> con el CUV, <c>REJECTED</c> con las glosas del Ministerio, o <c>PENDING</c>.</summary>
        /// <response code="200">Estado del RIPS.</response>
        /// <response code="404">La factura no existe o no tiene RIPS.</response>
        [HttpGet("{id:guid}/rips")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetRips(Guid id)
        {
            var (document, _, error) = await FindAsync(id);
            if (error != null) return error;
            var row = await _db.DocumentRips.AsNoTracking().FirstOrDefaultAsync(x => x.DocumentId == document!.Id);
            if (row == null) return NotFound(new { code = "RIPS_NOT_FOUND", message = "La factura no tiene RIPS. Envíalo con POST /api/co/dian/health-invoices/{id}/rips." });
            return Ok(RipsSummary(row, new List<string>()));
        }

        // La factura debe ser del emisor y de la sucursal de la llave, y de salud.
        private async Task<(Document? Document, Client? Client, IActionResult? Error)> FindAsync(Guid id)
        {
            if (HttpContext.Items["ClientId"] is not string clientIdText || !Guid.TryParse(clientIdText, out var clientId) || HttpContext.GetBranchId() is not Guid branchId)
                return (null, null, Unauthorized(new { code = "INVALID_API_KEY", message = "No se pudo resolver el emisor autenticado." }));
            var document = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id && d.ClientId == clientId && d.BranchId == branchId && d.TypeCode == SimpleInvoiceFactory.HealthInvoice);
            if (document == null)
                return (null, null, NotFound(new { code = "DOCUMENT_NOT_FOUND", message = "No hay una factura de salud con ese id." }));
            var client = await _db.Clients.Include(c => c.Integrator).FirstAsync(c => c.Id == clientId);
            return (document, client, null);
        }

        private static object RipsSummary(DocumentRips row, List<string> warnings) => new
        {
            status = row.Status,
            cuv = row.Cuv,
            processId = row.ProcessId,
            message = row.Message,
            attempts = row.Attempts,
            sentAt = row.SentAt,
            findings = MinSaludFevRipsClient.ReadFindings(row.ResponseJson),
            warnings
        };
    }
}
