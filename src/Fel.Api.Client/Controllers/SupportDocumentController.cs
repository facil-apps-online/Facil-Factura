using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Dataico;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Fel.Infrastructure.Services;

namespace Fel.Api.Client.Controllers
{
    [ApiController]
    [Route("api/client/support-documents")]
    public class SupportDocumentController : ClientPortalControllerBase
    {
        private const string TypeCode = "DS";
        private const string AdjustmentTypeCode = "DS-AJUSTE";

        // Editable/publicable/eliminable mientras no haya llegado exitosamente a Dataico/DIAN:
        // un DRAFT nunca se envió, y un REJECTED fue rechazado por Dataico, así que tampoco llegó.
        private static bool IsEditable(string status) => status == "DRAFT" || status == "REJECTED";

        // Documento Soporte permite fechas anteriores, pero solo dentro de la vigencia de su resolución y nunca posteriores al día actual. Las Notas de
        // Ajuste agregan el límite del documento original. La regla vive en el servicio de publicación, que también usa la API de integración.
        private Task<string?> ValidateSupportDatesAsync(
            Guid clientId, Guid branchId, Guid? resolutionId, DateTime? issueDate,
            Guid? referenceDocumentId, bool requireCurrentResolution) =>
            _publishing.ValidateDatesAsync(clientId, branchId, resolutionId, issueDate, referenceDocumentId, requireCurrentResolution);

        private async Task<Guid?> GetDocumentTypeIdAsync(string typeCode) =>
            await _dbContext.DocumentTypes.AsNoTracking()
                .Where(t => t.Code == typeCode)
                .Select(t => (Guid?)t.Id)
                .FirstOrDefaultAsync();

        private readonly FelDbContext _dbContext;
        private readonly IFacilReportsClient _facilReportsClient;
        private readonly Fel.Infrastructure.Services.DocumentLegendService _legendService;
        private readonly SupportDocumentPublishingService _publishing;

        public SupportDocumentController(FelDbContext dbContext, IFacilReportsClient facilReportsClient, Fel.Infrastructure.Services.DocumentLegendService legendService, SupportDocumentPublishingService publishing)
        {
            _facilReportsClient = facilReportsClient;
            _dbContext = dbContext;
            _legendService = legendService;
            _publishing = publishing;
        }

        // Consulta en Dataico de un documento soporte (kind vacío) o de su nota de ajuste (kind=adjustment). Solo lee: no guarda nada.
        [HttpGet("dataico-lookup")]
        public Task<IActionResult> DataicoLookup([FromQuery] string? number, [FromQuery] string? kind) => DataicoLookupAsync(SupportKind(kind), number);

        [HttpGet("dataico-lookup/file")]
        public Task<IActionResult> DataicoLookupFile([FromQuery] string? number, [FromQuery] string? kind, [FromQuery] string? type) => DataicoLookupFileAsync(SupportKind(kind), number, type);

        private static DataicoLookupKind SupportKind(string? kind) =>
            string.Equals(kind, "adjustment", System.StringComparison.OrdinalIgnoreCase) ? DataicoLookupKind.SupportDocAdjustment : DataicoLookupKind.SupportDoc;

        // Ver el comentario equivalente en InvoiceController.
        private async Task<(Dictionary<string, string> PaymentMeans, Dictionary<string, string> FormaPago)> GetPaymentCatalogsAsync()
        {
            var paymentMeans = await _dbContext.TaxCatalogItems
                .Where(i => i.Kind == TaxCatalogKind.PaymentMeans)
                .ToDictionaryAsync(i => i.Category, i => i.Name);
            var formaPago = await _dbContext.TaxCatalogItems
                .Where(i => i.Kind == TaxCatalogKind.FormaPago)
                .ToDictionaryAsync(i => i.Category, i => i.Name);
            return (paymentMeans, formaPago);
        }

        // Sin from/to, el rango por defecto es el mes calendario en curso (ver misma nota en
        // InvoiceController.GetAll).
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var rangeStart = (from ?? new DateTime(Fel.Core.Models.ColombiaTime.Today.Year, Fel.Core.Models.ColombiaTime.Today.Month, 1)).Date;
                var rangeEnd = (to ?? Fel.Core.Models.ColombiaTime.Today).Date.AddDays(1).AddTicks(-1);

                // Solo lo que se ve en cada fila (ver DocumentListItem): el detalle y la edición piden el documento completo por id.
                var docs = await Fel.Api.Client.Models.DocumentListQuery.Project(
                        BranchDocuments
                            .AsNoTracking()
                            .Where(d => d.ClientId == clientId && (d.TypeCode == TypeCode || d.TypeCode == AdjustmentTypeCode) && d.IssueDate >= rangeStart && d.IssueDate <= rangeEnd)
                            .OrderByDescending(d => d.IssueDate),
                        withReference: false)
                    .ToListAsync();

                return Ok(docs);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        // Respuesta del integrador (Dataico o DIAN) tal como se guardó: el listado solo avisa que existe porque pesa hasta decenas de KB por documento.
        [HttpGet("{id}/dian-response")]
        public async Task<IActionResult> GetDianResponse(Guid id)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var message = await BranchDocuments
                    .AsNoTracking()
                    .Where(d => d.Id == id && d.ClientId == clientId && (d.TypeCode == TypeCode || d.TypeCode == AdjustmentTypeCode))
                    .Select(d => new { d.DianResponseMessage })
                    .FirstOrDefaultAsync();

                if (message == null) return NotFound("Documento soporte no encontrado.");
                return Ok(new { dianResponseMessage = message.DianResponseMessage });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var document = await BranchDocuments
                    .Include(d => d.Customer)
                    .Include(d => d.DocumentType)
                    .Include(d => d.Resolution)
                    .Include(d => d.Items.OrderBy(i => i.LineNumber)).ThenInclude(i => i.Retentions)
                    .Include(d => d.GeneralRetentions)
                    .FirstOrDefaultAsync(d => d.Id == id && d.ClientId == clientId && (d.TypeCode == TypeCode || d.TypeCode == AdjustmentTypeCode));

                if (document == null) return NotFound("Documento soporte no encontrado.");
                return Ok(document);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        // Notas de ajuste que referencian este documento — mismo patrón que InvoiceController.
        [HttpGet("{id}/related")]
        public async Task<IActionResult> GetRelated(Guid id)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var related = await BranchDocuments
                    .Where(d => d.ClientId == clientId && d.ReferenceDocumentId == id)
                    .OrderByDescending(d => d.IssueDate)
                    .Select(d => new { d.Id, d.Number, d.TypeCode, d.Status, d.TotalAmount, d.IssueDate })
                    .ToListAsync();

                return Ok(related);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        // Vista previa de impresión (borrador o ya emitido) con la plantilla del cliente — mismo flujo
        // que InvoiceController.Preview, con el mapper de documento soporte.
        [HttpGet("{id}/preview")]
        public async Task<IActionResult> Preview(Guid id)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var document = await BranchDocuments
                    .Include(d => d.Customer)
                    .Include(d => d.DocumentType)
                    .Include(d => d.Resolution)
                    .Include(d => d.Items.OrderBy(i => i.LineNumber)).ThenInclude(i => i.Retentions)
                    .Include(d => d.GeneralRetentions)
                    .FirstOrDefaultAsync(d => d.Id == id && d.ClientId == clientId && (d.TypeCode == TypeCode || d.TypeCode == AdjustmentTypeCode));

                if (document == null) return NotFound("Documento soporte no encontrado.");

                var documentTypeId = document.DocumentTypeId ?? await GetDocumentTypeIdAsync(document.TypeCode);
                if (!documentTypeId.HasValue) return BadRequest("El documento no tiene un tipo asignado.");

                var client = await _dbContext.Clients.Include(c => c.Integrator).FirstOrDefaultAsync(c => c.Id == clientId);
                if (client == null) return NotFound("Emisor no encontrado.");

                var template = await Fel.Infrastructure.Services.DocumentTemplateResolver.ResolveAsync(_dbContext, client, documentTypeId.Value);
                if (template == null)
                {
                    return BadRequest("Aún no hay una plantilla de impresión configurada para este tipo de documento.");
                }

                var (paymentMeansCatalog, formaPagoCatalog) = await GetPaymentCatalogsAsync();
                var data = SupportDocumentReportDataMapper.Build(document, document.Customer, client, await BranchProvisioning.LocationAsync(_dbContext, client, document.BranchId), document.Resolution, document.Items.ToList(), paymentMeansCatalog, formaPagoCatalog, await _legendService.ResolveAsync(client, document.Resolution, document.TypeCode), currencyWords: await CurrencyWords.LoadAsync(_dbContext), documentTypeTitle: document.DocumentType?.ReportTitle, originalDocument: await LoadAdjustedDocumentAsync(document), retentionShortNames: await RetentionShortNameCatalog.LoadAsync(_dbContext));
                var pdfBytes = await _facilReportsClient.GenerateReportAsync(template.RepxTemplateKey, data);
                if (pdfBytes == null)
                {
                    return BadRequest("No se pudo generar la vista previa. Intenta de nuevo en unos segundos.");
                }

                return File(pdfBytes, "application/pdf");
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpPost("draft")]
        public async Task<IActionResult> CreateDraft([FromBody] CreateSupportDocumentRequest request)
        {
            try
            {
                var clientId = GetCurrentClientId();

                var provider = await _dbContext.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId && c.ClientId == clientId);
                if (provider == null) return BadRequest("El proveedor/tercero seleccionado no existe.");

                if (request.Items == null || request.Items.Count == 0)
                {
                    return BadRequest("Debes agregar al menos un ítem.");
                }

                var dateError = await ValidateSupportDatesAsync(
                    clientId, GetCurrentBranchId(), request.ResolutionId, request.IssueDate,
                    request.ReferenceDocumentId, requireCurrentResolution: false);
                if (dateError != null) return BadRequest(dateError);

                var document = BuildDocument(clientId, GetCurrentBranchId(), provider.Id, request);
                document.DocumentTypeId = await GetDocumentTypeIdAsync(document.TypeCode);
                document.Status = "DRAFT";

                _dbContext.Documents.Add(document);
                await _dbContext.SaveChangesAsync();

                return CreatedAtAction(nameof(GetById), new { id = document.Id }, document);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpPut("{id}/draft")]
        public async Task<IActionResult> UpdateDraft(Guid id, [FromBody] CreateSupportDocumentRequest request)
        {
            try
            {
                var clientId = GetCurrentClientId();
                // No se incluyen las Retentions viejas: nunca se usan (los ítems se reemplazan
                // completos) y si quedan trackeadas, EF genera un DELETE explícito para ellas que
                // choca con el ON DELETE CASCADE de la FK, tirando DbUpdateConcurrencyException
                // (la fila ya no existe porque la BD la borró en cascada al borrar el ítem padre).
                var document = await BranchDocuments
                    .Include(d => d.Items.OrderBy(i => i.LineNumber))
                    .Include(d => d.GeneralRetentions)
                    .FirstOrDefaultAsync(d => d.Id == id && d.ClientId == clientId && (d.TypeCode == TypeCode || d.TypeCode == AdjustmentTypeCode));

                if (document == null) return NotFound("Documento soporte no encontrado.");
                if (!IsEditable(document.Status)) return BadRequest("Solo se pueden modificar borradores o documentos rechazados.");

                var provider = await _dbContext.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId && c.ClientId == clientId);
                if (provider == null) return BadRequest("El proveedor/tercero seleccionado no existe.");

                var dateError = await ValidateSupportDatesAsync(
                    clientId, GetCurrentBranchId(), request.ResolutionId, request.IssueDate,
                    request.ReferenceDocumentId, requireCurrentResolution: false);
                if (dateError != null) return BadRequest(dateError);

                var updated = BuildDocument(clientId, GetCurrentBranchId(), provider.Id, request);

                document.CustomerId = provider.Id;
                document.IssueDate = updated.IssueDate;
                document.Notes = updated.Notes;
                document.GeneralChargeReason = updated.GeneralChargeReason;
                document.GeneralChargeAmount = updated.GeneralChargeAmount;
                document.DocumentTypeId = await GetDocumentTypeIdAsync(document.TypeCode);
                document.ResolutionId = request.ResolutionId;
                document.PaymentMeans = request.PaymentMeans;
                document.PaymentMeansType = request.PaymentMeansType;
                document.PaymentTermDays = request.PaymentTermDays;
                document.PurchaseOrderReference = request.PurchaseOrderReference;
                document.Subtotal = updated.Subtotal;
                document.TaxAmount = updated.TaxAmount;
                document.TotalAmount = updated.TotalAmount;
                document.GeneralDiscountReason = updated.GeneralDiscountReason;
                document.GeneralDiscountAmount = updated.GeneralDiscountAmount;

                // Los ítems se recrean por completo; las retenciones de cada ítem se borran en
                // cascada al borrar el ítem, y las nuevas vienen anidadas en cada ítem del payload.
                _dbContext.DocumentItems.RemoveRange(document.Items);
                document.Items.Clear();
                var itemLine = 0;
                foreach (var item in updated.Items)
                {
                    item.DocumentId = document.Id;
                    item.LineNumber = itemLine++;
                    foreach (var retention in item.Retentions) retention.DocumentItemId = item.Id;
                    // Se agrega vía el DbSet (no document.Items.Add) para que EF lo marque Added de
                    // forma explícita: como el Document padre ya viene trackeado, el fixup automático
                    // por navegación puede inferir mal el estado de un ítem nuevo cuyo Id (Guid) ya
                    // viene asignado, tratándolo como Modified y generando un UPDATE para una fila
                    // que nunca existió (DbUpdateConcurrencyException).
                    _dbContext.DocumentItems.Add(item);
                }

                // Retenciones generales: mismo patrón de reemplazo completo que los ítems.
                _dbContext.DocumentGeneralRetentions.RemoveRange(document.GeneralRetentions);
                document.GeneralRetentions.Clear();
                foreach (var generalRetention in updated.GeneralRetentions)
                {
                    generalRetention.DocumentId = document.Id;
                    _dbContext.DocumentGeneralRetentions.Add(generalRetention);
                }

                await _dbContext.SaveChangesAsync();
                return Ok(document);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        // El documento soporte que ajusta una nota de ajuste, para que su PDF diga cuál es y con qué CUDS. Null en el documento soporte.
        private async Task<Document?> LoadAdjustedDocumentAsync(Document document) =>
            document.TypeCode == AdjustmentTypeCode && document.ReferenceDocumentId.HasValue
                ? await _dbContext.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == document.ReferenceDocumentId.Value)
                : null;

        [HttpPost("{id}/publish")]
        public async Task<IActionResult> Publish(Guid id)
        {
            try
            {
                var clientId = GetCurrentClientId();
                await using var publishLock = await SqlAppLock.AcquireAsync(_dbContext, $"DocumentPublish:{clientId}:{id}");
                if (publishLock == null) return Conflict("El documento ya está siendo enviado.");
                var document = await BranchDocuments
                    .Include(d => d.Items.OrderBy(i => i.LineNumber)).ThenInclude(i => i.Retentions)
                    .Include(d => d.GeneralRetentions)
                    .Include(d => d.Customer)
                    .Include(d => d.DocumentType)
                    .FirstOrDefaultAsync(d => d.Id == id && d.ClientId == clientId && (d.TypeCode == TypeCode || d.TypeCode == AdjustmentTypeCode));

                if (document == null) return NotFound("Documento soporte no encontrado.");

                var outcome = await _publishing.PublishAsync(document, clientId);
                switch (outcome.Kind)
                {
                    case PublishOutcomeKind.NotFound: return NotFound(outcome.Message);
                    case PublishOutcomeKind.TemplateRequired: return TemplateRequired();
                    case PublishOutcomeKind.Conflict: return Conflict(outcome.Message);
                    case PublishOutcomeKind.ProviderRejected:
                        // `sent` es el JSON exacto enviado a Dataico, para comparar contra un envío que sí se aceptó.
                        return BadRequest(new { message = outcome.Message, detail = outcome.Detail, sent = outcome.Sent });
                    case PublishOutcomeKind.BadRequest: return BadRequest(outcome.Message);
                    default: return Ok(new { message = outcome.Message, warning = outcome.Warning, document });
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDraft(Guid id)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var document = await BranchDocuments
                    .FirstOrDefaultAsync(d => d.Id == id && d.ClientId == clientId && (d.TypeCode == TypeCode || d.TypeCode == AdjustmentTypeCode));

                if (document == null) return NotFound("Documento soporte no encontrado.");
                if (!IsEditable(document.Status)) return BadRequest("Solo se pueden eliminar borradores o documentos rechazados.");

                _dbContext.Documents.Remove(document);
                await _dbContext.SaveChangesAsync();
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        // Emisión en lote: los documentos seleccionados se emiten de a uno, en orden de creación, con la misma lógica de "{id}/publish".
        [HttpPost("batch/publish")]
        public async Task<IActionResult> BatchPublish([FromBody] BatchRequest request)
        {
            var invalid = ValidateBatch(request, out var ids);
            if (invalid != null) return invalid;

            var clientId = GetCurrentClientId();
            await StreamBatchAsync(_dbContext, ids, BranchDocuments.Where(d => d.ClientId == clientId && ids.Contains(d.Id) && (d.TypeCode == TypeCode || d.TypeCode == AdjustmentTypeCode)).OrderBy(d => d.CreatedAt).Select(d => d.Id), id => Publish(id));
            return new EmptyResult();
        }

        // Eliminación en lote de borradores y rechazados, con la misma validación de "DELETE {id}".
        [HttpPost("batch/delete")]
        public async Task<IActionResult> BatchDelete([FromBody] BatchRequest request)
        {
            var invalid = ValidateBatch(request, out var ids);
            if (invalid != null) return invalid;

            var clientId = GetCurrentClientId();
            await StreamBatchAsync(_dbContext, ids, BranchDocuments.Where(d => d.ClientId == clientId && ids.Contains(d.Id) && (d.TypeCode == TypeCode || d.TypeCode == AdjustmentTypeCode)).OrderBy(d => d.CreatedAt).Select(d => d.Id), id => DeleteDraft(id));
            return new EmptyResult();
        }

        // Documentos de la sucursal activa (todas las del usuario si eligió "todas"), para consultar.
        private IQueryable<Document> BranchDocuments => _dbContext.Documents.ForBranch(CurrentBranchScope);

        private static Document BuildDocument(Guid clientId, Guid branchId, Guid providerId, CreateSupportDocumentRequest request)
        {
            decimal EffectiveRate(CreateSupportDocumentItem i) => 0;
            decimal LineBase(CreateSupportDocumentItem i) => i.Quantity * i.UnitPrice * (1 - i.DiscountRate / 100);

            var document = new Document
            {
                Id = Guid.NewGuid(),
                ClientId = clientId,
                BranchId = branchId,
                CustomerId = providerId,
                TypeCode = request.ReferenceDocumentId.HasValue ? AdjustmentTypeCode : TypeCode,
                // Sin número hasta publicar: ahí se reclama el consecutivo de la resolución/contador.
                Number = string.Empty,
                CreatedAt = DateTime.UtcNow,
                IssueDate = (request.IssueDate ?? Fel.Core.Models.ColombiaTime.Today).Date,
                Notes = request.Notes ?? string.Empty,
                ResolutionId = request.ResolutionId,
                PaymentMeans = request.PaymentMeans,
                PaymentMeansType = request.PaymentMeansType,
                PaymentTermDays = request.PaymentTermDays,
                PurchaseOrderReference = request.PurchaseOrderReference,
                GeneralDiscountReason = request.GeneralDiscountReason,
                GeneralDiscountAmount = request.GeneralDiscountAmount,
                GeneralChargeReason = request.GeneralChargeReason,
                GeneralChargeAmount = request.GeneralChargeAmount,
                ReferenceDocumentId = request.ReferenceDocumentId,
                ReferenceConcept = request.ReferenceConcept,
                Subtotal = request.Items.Sum(LineBase),
                TaxAmount = request.Items.Sum(i => LineBase(i) * EffectiveRate(i) / 100),
                // Bruto (subtotal + IVA), igual que en facturas: el descuento y el cargo general se
                // aplican al mostrar el neto (ver invoiceNetTotal en el portal) y en los PDF.
                TotalAmount = request.Items.Sum(i => LineBase(i) * (1 + EffectiveRate(i) / 100))
            };

            var supportItemLine = 0;
            foreach (var item in request.Items)
            {
                var rate = EffectiveRate(item);
                var lineBase = LineBase(item);
                var documentItem = new DocumentItem
                {
                    Id = Guid.NewGuid(),
                    DocumentId = document.Id,
                    LineNumber = supportItemLine++,
                    ProductId = item.ProductId,
                    Code = item.Code,
                    Name = item.Name,
                    UnitOfMeasureCode = string.IsNullOrWhiteSpace(item.UnitOfMeasureCode) ? "94" : item.UnitOfMeasureCode,
                    UnitOfMeasureAbbreviation = string.IsNullOrWhiteSpace(item.UnitOfMeasureAbbreviation) ? "EA" : item.UnitOfMeasureAbbreviation,
                    UnitOfMeasureDisplayFormat = string.IsNullOrWhiteSpace(item.UnitOfMeasureDisplayFormat) ? "Combined" : item.UnitOfMeasureDisplayFormat,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    IvaTreatment = IvaTreatment.Exento,
                    TaxRate = rate,
                    DiscountRate = item.DiscountRate,
                    TaxAmount = lineBase * rate / 100,
                    TotalAmount = lineBase * (1 + rate / 100)
                };

                foreach (var retention in (item.Retentions ?? new()).Where(r => !string.Equals(r.TaxCategory, "RET_IVA", StringComparison.OrdinalIgnoreCase)))
                {
                    documentItem.Retentions.Add(new DocumentRetention
                    {
                        Id = Guid.NewGuid(),
                        DocumentItemId = documentItem.Id,
                        TaxCategory = retention.TaxCategory,
                        Rate = retention.Rate,
                        BaseAmount = retention.BaseAmount,
                        Amount = retention.Amount
                    });
                }

                document.Items.Add(documentItem);
            }

            foreach (var generalRetention in (request.GeneralRetentions ?? new()).Where(r => !string.Equals(r.TaxCategory, "RET_IVA", StringComparison.OrdinalIgnoreCase)))
            {
                document.GeneralRetentions.Add(new DocumentGeneralRetention
                {
                    Id = Guid.NewGuid(),
                    DocumentId = document.Id,
                    TaxCategory = generalRetention.TaxCategory,
                    Rate = generalRetention.Rate
                });
            }

            return document;
        }
    }

    public class CreateSupportDocumentItem
    {
        public Guid? ProductId { get; set; }
        public string UnitOfMeasureCode { get; set; } = "94";
        public string UnitOfMeasureAbbreviation { get; set; } = "EA";
        public string UnitOfMeasureDisplayFormat { get; set; } = "Combined";
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public IvaTreatment IvaTreatment { get; set; } = IvaTreatment.Gravado;
        public decimal TaxRate { get; set; }
        public decimal DiscountRate { get; set; }
        public System.Collections.Generic.List<CreateDocumentRetention>? Retentions { get; set; }
    }

    public class CreateDocumentRetention
    {
        public string TaxCategory { get; set; } = "RET_FUENTE";
        public decimal Rate { get; set; }
        public decimal BaseAmount { get; set; }
        public decimal Amount { get; set; }
    }

    public class CreateGeneralRetention
    {
        public string TaxCategory { get; set; } = string.Empty;
        public decimal Rate { get; set; }
    }

    public class CreateSupportDocumentRequest
    {
        public Guid CustomerId { get; set; }
        public Guid? ResolutionId { get; set; }
        public string PaymentMeans { get; set; } = string.Empty;
        public string PaymentMeansType { get; set; } = string.Empty;
        public int? PaymentTermDays { get; set; }
        public string? PurchaseOrderReference { get; set; }
        public string? GeneralDiscountReason { get; set; }
        public decimal? GeneralDiscountAmount { get; set; }
        public string? GeneralChargeReason { get; set; }
        public decimal? GeneralChargeAmount { get; set; }
        public DateTime? IssueDate { get; set; }
        public string? Notes { get; set; }
        public System.Collections.Generic.List<CreateGeneralRetention>? GeneralRetentions { get; set; }
        public System.Collections.Generic.List<CreateSupportDocumentItem> Items { get; set; } = new();

        // Presentes solo cuando este documento es una Nota de Ajuste sobre otro documento soporte.
        public Guid? ReferenceDocumentId { get; set; }
        public string? ReferenceConcept { get; set; }
    }
}
