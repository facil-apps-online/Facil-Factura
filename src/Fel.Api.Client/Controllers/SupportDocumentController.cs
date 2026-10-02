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

namespace Fel.Api.Client.Controllers
{
    [ApiController]
    [Route("api/client/support-documents")]
    public class SupportDocumentController : ControllerBase
    {
        private const string TypeCode = "DS";
        private const string AdjustmentTypeCode = "DS-AJUSTE";

        // Editable/publicable/eliminable mientras no haya llegado exitosamente a Dataico/DIAN:
        // un DRAFT nunca se envió, y un REJECTED fue rechazado por Dataico, así que tampoco llegó.
        private static bool IsEditable(string status) => status == "DRAFT" || status == "REJECTED";

        // La fecha de emisión se maneja en hora de Colombia y, igual que en facturas, no puede ser
        // anterior a hoy (de hoy en adelante). Sin fecha en el request se usa la de hoy.
        private static string? ValidateIssueDate(DateTime? issueDate) =>
            issueDate.HasValue && issueDate.Value.Date < Fel.Core.Models.ColombiaTime.Today
                ? "La fecha de emisión no puede ser anterior a hoy."
                : null;

        // Antes de usar el consecutivo de la resolución, el borrador se numeraba con DateTime.Ticks
        // (18 dígitos). Esos borradores/rechazados viejos se renumeran al publicar.
        private static bool NeedsConsecutive(string? number) =>
            string.IsNullOrEmpty(number) || (number.Length >= 15 && number.All(char.IsDigit));

        private async Task<Guid?> GetDocumentTypeIdAsync(string typeCode) =>
            await _dbContext.DocumentTypes.AsNoTracking()
                .Where(t => t.Code == typeCode)
                .Select(t => (Guid?)t.Id)
                .FirstOrDefaultAsync();

        private readonly FelDbContext _dbContext;
        private readonly ICryptoService _cryptoService;
        private readonly IDataicoApiService _dataicoApiService;
        private readonly DataicoCustomPdfService _customPdfService;
        private readonly IFacilReportsClient _facilReportsClient;

        public SupportDocumentController(FelDbContext dbContext, ICryptoService cryptoService, IDataicoApiService dataicoApiService, DataicoCustomPdfService customPdfService, IFacilReportsClient facilReportsClient)
        {
            _facilReportsClient = facilReportsClient;
            _dbContext = dbContext;
            _cryptoService = cryptoService;
            _dataicoApiService = dataicoApiService;
            _customPdfService = customPdfService;
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

                var docs = await _dbContext.Documents
                    .Include(d => d.Customer)
                    .Include(d => d.Resolution)
                    .Where(d => d.ClientId == clientId && (d.TypeCode == TypeCode || d.TypeCode == AdjustmentTypeCode) && d.IssueDate >= rangeStart && d.IssueDate <= rangeEnd)
                    .OrderByDescending(d => d.IssueDate)
                    .ToListAsync();

                return Ok(docs);
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
                var document = await _dbContext.Documents
                    .Include(d => d.Customer)
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
                var related = await _dbContext.Documents
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
                var document = await _dbContext.Documents
                    .Include(d => d.Customer)
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
                var data = SupportDocumentReportDataMapper.Build(document, document.Customer, client, document.Resolution, document.Items.ToList(), paymentMeansCatalog, formaPagoCatalog);
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

                var dateError = ValidateIssueDate(request.IssueDate);
                if (dateError != null) return BadRequest(dateError);

                var document = BuildDocument(clientId, provider.Id, request);
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
                var document = await _dbContext.Documents
                    .Include(d => d.Items.OrderBy(i => i.LineNumber))
                    .Include(d => d.GeneralRetentions)
                    .FirstOrDefaultAsync(d => d.Id == id && d.ClientId == clientId && (d.TypeCode == TypeCode || d.TypeCode == AdjustmentTypeCode));

                if (document == null) return NotFound("Documento soporte no encontrado.");
                if (!IsEditable(document.Status)) return BadRequest("Solo se pueden modificar borradores o documentos rechazados.");

                var provider = await _dbContext.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId && c.ClientId == clientId);
                if (provider == null) return BadRequest("El proveedor/tercero seleccionado no existe.");

                var dateError = ValidateIssueDate(request.IssueDate);
                if (dateError != null) return BadRequest(dateError);

                var updated = BuildDocument(clientId, provider.Id, request);

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

        [HttpPost("{id}/publish")]
        public async Task<IActionResult> Publish(Guid id)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var document = await _dbContext.Documents
                    .Include(d => d.Items.OrderBy(i => i.LineNumber)).ThenInclude(i => i.Retentions)
                    .Include(d => d.GeneralRetentions)
                    .Include(d => d.Customer)
                    .FirstOrDefaultAsync(d => d.Id == id && d.ClientId == clientId && (d.TypeCode == TypeCode || d.TypeCode == AdjustmentTypeCode));

                if (document == null) return NotFound("Documento soporte no encontrado.");
                if (!IsEditable(document.Status)) return BadRequest("El documento ya fue emitido o está en proceso.");

                var client = await _dbContext.Clients.Include(c => c.Integrator).FirstOrDefaultAsync(c => c.Id == clientId);
                if (client == null) return NotFound("Emisor no encontrado.");

                if (client.Integrator.Code != "DATAICO")
                {
                    return BadRequest("Este emisor no tiene configurado un proveedor de documentos electrónicos habilitado.");
                }

                // El cliente puede tener varias resoluciones activas (una por prefijo); si no se
                // eligió una explícitamente al crear el borrador, se usa la marcada como
                // predeterminada, y si ninguna lo está, se cae al comportamiento anterior (la más
                // reciente por vigencia) para no romper clientes que aún no eligieron default.
                var resolution = document.ResolutionId.HasValue
                    ? await _dbContext.Resolutions.FirstOrDefaultAsync(r => r.Id == document.ResolutionId.Value && r.ClientId == clientId && r.IsActive)
                    : await _dbContext.Resolutions
                        .Where(r => r.ClientId == clientId && r.IsActive && r.DocumentType == TypeCode)
                        .OrderByDescending(r => r.IsDefault)
                        .ThenByDescending(r => r.ValidTo)
                        .FirstOrDefaultAsync();

                if (resolution == null)
                {
                    return BadRequest("No hay una resolución de documento soporte activa registrada para este emisor.");
                }

                if (string.IsNullOrWhiteSpace(document.PaymentMeansType))
                {
                    return BadRequest("Debes indicar la forma de pago (Contado/Crédito).");
                }

                // DEBITO ("Contado") sí necesita el medio de pago concreto; CREDITO solo maneja
                // plazo de pago y nunca lo captura en el formulario.
                if (document.PaymentMeansType == "DEBITO" && string.IsNullOrWhiteSpace(document.PaymentMeans))
                {
                    return BadRequest("Debes indicar el medio de pago.");
                }

                if (document.IssueDate.Date < Fel.Core.Models.ColombiaTime.Today)
                {
                    return BadRequest("La fecha de emisión ya pasó. Edita el documento y actualiza la fecha (de hoy en adelante).");
                }

                // Consecutivo real, reclamado recién ahora que ya pasaron todas las validaciones (un
                // documento rechazado que se reintenta conserva el que ya tiene). El Documento Soporte
                // usa el rango de su resolución; la Nota de Ajuste, su contador propio del Client.
                if (NeedsConsecutive(document.Number))
                {
                    try
                    {
                        document.Number = document.TypeCode == AdjustmentTypeCode
                            ? (await Fel.Infrastructure.Services.ResolutionNumbering.ClaimNextSupportAdjustmentNumberAsync(_dbContext, clientId)).ToString()
                            : (await Fel.Infrastructure.Services.ResolutionNumbering.ClaimNextNumberAsync(_dbContext, resolution.Id)).ToString();
                    }
                    catch (Fel.Infrastructure.Services.ResolutionExhaustedException ex)
                    {
                        // Numeración agotada: no se emite ni se consume nada; el documento sigue editable.
                        return BadRequest(ex.Message);
                    }
                }

                Fel.Infrastructure.Dataico.Models.DataicoResult result;
                if (document.TypeCode == AdjustmentTypeCode)
                {
                    if (!document.ReferenceDocumentId.HasValue)
                    {
                        return BadRequest("Esta nota de ajuste debe referenciar el documento soporte que ajusta.");
                    }
                    var originalDocument = await _dbContext.Documents.FindAsync(document.ReferenceDocumentId.Value);
                    if (originalDocument == null || string.IsNullOrEmpty(originalDocument.Cufe))
                    {
                        return BadRequest("El documento soporte original no tiene un CUDS registrado (puede no haberse emitido por Dataico). No se puede generar la nota.");
                    }

                    var credentials = DataicoDocumentMapper.ToCredentials(client, _cryptoService);
                    var adjustmentRequest = DataicoDocumentMapper.BuildSupportDocAdjustmentRequest(
                        document, originalDocument, document.Customer!, resolution, client, document.PaymentMeans, document.PaymentMeansType, document.Items);
                    result = await _dataicoApiService.SendSupportDocAdjustmentAsync(adjustmentRequest, credentials);

                    document.Status = result.Success ? "APPROVED" : "REJECTED";
                    document.DianResponseMessage = result.Success ? result.RawResponse : (result.ErrorMessage ?? result.RawResponse);
                    document.Cufe = result.Cufe;
                    document.QrCode = result.QrCode;
                    document.ProcessedAt = DateTime.UtcNow;
                }
                else
                {
                    result = await SubmitSupportDocToDataicoAsync(document, document.Customer!, document.Items, client, resolution, document.PaymentMeans, document.PaymentMeansType);
                }

                if (result.Success)
                {
                    document.IntegratorId = client.IntegratorId;

                    var credentials = DataicoDocumentMapper.ToCredentials(client, _cryptoService);
                    var (paymentMeansCatalog, formaPagoCatalog) = await GetPaymentCatalogsAsync();
                    await _customPdfService.TrySendCustomPdfAsync(
                        document, document.Customer?.Email, client,
                        _ => SupportDocumentReportDataMapper.Build(document, document.Customer, client, resolution, document.Items.ToList(), paymentMeansCatalog, formaPagoCatalog),
                        credentials);
                }

                await _dbContext.SaveChangesAsync();

                if (!result.Success)
                {
                    return BadRequest(new { message = "Dataico rechazó el documento soporte.", detail = document.DianResponseMessage });
                }

                return Ok(new { message = "Documento soporte emitido correctamente.", document });
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
                var document = await _dbContext.Documents
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

        private static Document BuildDocument(Guid clientId, Guid providerId, CreateSupportDocumentRequest request)
        {
            decimal EffectiveRate(CreateSupportDocumentItem i) => i.IvaTreatment == IvaTreatment.Gravado ? i.TaxRate : 0;
            decimal LineBase(CreateSupportDocumentItem i) => i.Quantity * i.UnitPrice * (1 - i.DiscountRate / 100);

            var document = new Document
            {
                Id = Guid.NewGuid(),
                ClientId = clientId,
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
                    IvaTreatment = item.IvaTreatment,
                    TaxRate = rate,
                    DiscountRate = item.DiscountRate,
                    TaxAmount = lineBase * rate / 100,
                    TotalAmount = lineBase * (1 + rate / 100)
                };

                foreach (var retention in item.Retentions ?? new())
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

            foreach (var generalRetention in request.GeneralRetentions ?? new())
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

        private async Task<Fel.Infrastructure.Dataico.Models.DataicoResult> SubmitSupportDocToDataicoAsync(
            Document document, Customer provider, System.Collections.Generic.ICollection<DocumentItem> items,
            Fel.Core.Entities.Client client, Resolution resolution, string paymentMeans, string paymentMeansType)
        {
            var dataicoRequest = DataicoDocumentMapper.BuildSupportDocumentRequest(
                document, provider, items, resolution, client, paymentMeans, paymentMeansType);

            var credentials = DataicoDocumentMapper.ToCredentials(client, _cryptoService);
            var result = await _dataicoApiService.SendSupportDocAsync(dataicoRequest, credentials);

            document.Status = result.Success ? "APPROVED" : "REJECTED";
            document.DianResponseMessage = result.Success ? result.RawResponse : (result.ErrorMessage ?? result.RawResponse);
            document.Cufe = result.Cufe;
            document.QrCode = result.QrCode;
            document.ProcessedAt = DateTime.UtcNow;

            return result;
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
