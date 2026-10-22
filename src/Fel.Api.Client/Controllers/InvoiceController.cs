using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Dataico;
using Fel.Infrastructure.Dian;
using Fel.Infrastructure.Services;

namespace Fel.Api.Client.Controllers
{
    [ApiController]
    [Route("api/client/invoices")]
    public class InvoiceController : ClientPortalControllerBase
    {
        // Facturas y notas tienen esta pantalla; documento soporte y nómina tienen endpoints propios.
        // Siempre trae las observaciones (ver DocumentObservation): las leen el formulario, el envío al integrador y el PDF.
        private static IQueryable<Document> InvoiceDocuments(IQueryable<Document> query, Guid clientId, Guid? branchScope) =>
            query.Where(d => d.ClientId == clientId &&
                ((d.TypeCode.StartsWith("FE") && d.TypeCode != "FE-TEST") || d.TypeCode == "NC" || d.TypeCode == "ND")).ForBranch(branchScope)
                .Include(d => d.Observations.OrderBy(o => o.Position));

        // Editable/publicable/eliminable mientras no haya llegado exitosamente a Dataico/DIAN:
        // un DRAFT nunca se envió, y un REJECTED fue rechazado por Dataico, así que tampoco llegó.
        private static bool IsEditable(string status) => status == "DRAFT" || status == "REJECTED";

        // Campos propios de la factura de exportación (todos opcionales): condición de entrega (Incoterm, del catálogo, con una copia congelada de
        // su significado) y, por ítem, marca, modelo y subpartida arancelaria (solo dígitos, de 4 a 10). En cualquier otra factura se vacían, para
        // que no viajen ni se impriman. Devuelve el mensaje de error, o null si quedó bien.
        private async Task<string?> ApplyExportFieldsAsync(Document target, Document requested, bool isExport)
        {
            if (!isExport)
            {
                target.DeliveryTermsCode = null;
                target.DeliveryTermsName = null;
                foreach (var item in requested.Items) { item.BrandName = null; item.ModelName = null; item.TariffCode = null; }
                return null;
            }

            var code = (requested.DeliveryTermsCode ?? string.Empty).Trim().ToUpperInvariant();
            if (code.Length == 0)
            {
                target.DeliveryTermsCode = null;
                target.DeliveryTermsName = null;
            }
            else
            {
                var name = await _dbContext.TaxCatalogItems.AsNoTracking()
                    .Where(i => i.Kind == TaxCatalogKind.Incoterm && i.Category == code && i.IsActive)
                    .Select(i => i.Name).FirstOrDefaultAsync();
                if (name == null) return $"La condición de entrega {code} no está en el catálogo.";
                target.DeliveryTermsCode = code;
                target.DeliveryTermsName = name;
            }

            foreach (var item in requested.Items)
            {
                item.BrandName = string.IsNullOrWhiteSpace(item.BrandName) ? null : item.BrandName.Trim();
                item.ModelName = string.IsNullOrWhiteSpace(item.ModelName) ? null : item.ModelName.Trim();
                item.TariffCode = string.IsNullOrWhiteSpace(item.TariffCode) ? null : item.TariffCode.Trim();
                if (item.BrandName?.Length > 100 || item.ModelName?.Length > 100) return "La marca y el modelo admiten hasta 100 caracteres.";
                if (item.TariffCode != null && (item.TariffCode.Length is < 4 or > 10 || !item.TariffCode.All(char.IsDigit)))
                    return $"La subpartida arancelaria '{item.TariffCode}' debe tener solo dígitos, de 4 a 10.";
            }
            return null;
        }

        // Moneda del documento. Solo la factura general de un cliente habilitado por el tenant puede ir en una moneda distinta de COP;
        // la moneda debe estar en el catálogo y la TRM (mayor que cero) y su fecha son obligatorias. Una nota crédito/débito hereda
        // siempre la moneda y la TRM de la factura que ajusta. Devuelve el mensaje de error, o null si quedó bien.
        private async Task<string?> ApplyCurrencyAsync(Document target, Document requested, Guid clientId)
        {
            if (target.ReferenceDocumentId.HasValue)
            {
                var original = await _dbContext.Documents.AsNoTracking()
                    .Where(d => d.Id == target.ReferenceDocumentId.Value && d.ClientId == clientId)
                    .Select(d => new { d.Currency, d.ExchangeRate, d.ExchangeRateDate })
                    .FirstOrDefaultAsync();
                target.Currency = original?.Currency ?? "COP";
                target.ExchangeRate = original?.ExchangeRate;
                target.ExchangeRateDate = original?.ExchangeRateDate;
                return null;
            }

            var currency = (requested.Currency ?? string.Empty).Trim().ToUpperInvariant();
            if (currency.Length == 0 || currency == "COP")
            {
                target.Currency = "COP";
                target.ExchangeRate = null;
                target.ExchangeRateDate = null;
                return null;
            }

            if (!await _dbContext.Clients.AnyAsync(c => c.Id == clientId && c.ForeignCurrencyEnabled))
                return "Este cliente no está habilitado para facturar en moneda extranjera.";

            var documentTypeCode = requested.DocumentTypeId.HasValue
                ? await _dbContext.DocumentTypes.AsNoTracking().Where(t => t.Id == requested.DocumentTypeId.Value).Select(t => t.Code).FirstOrDefaultAsync()
                : null;
            if (documentTypeCode is not ("FE-STD" or "FE-EXP"))
                return "Solo la factura de venta estándar y la de exportación pueden emitirse en moneda extranjera.";

            if (!await _dbContext.TaxCatalogItems.AsNoTracking().AnyAsync(i => i.Kind == TaxCatalogKind.Currency && i.Category == currency && i.IsActive))
                return $"La moneda {currency} no está disponible para facturar.";
            if (requested.ExchangeRate is not > 0)
                return "Indica la TRM (tasa de cambio en pesos), mayor que cero.";
            if (!requested.ExchangeRateDate.HasValue)
                return "Indica la fecha de la TRM.";

            target.Currency = currency;
            target.ExchangeRate = requested.ExchangeRate;
            target.ExchangeRateDate = requested.ExchangeRateDate.Value.Date;
            return null;
        }

        private readonly FelDbContext _dbContext;
        private readonly IEnumerable<IDocumentSubmissionProvider> _submissionProviders;
        private readonly IFacilReportsClient _facilReportsClient;
        private readonly IDataicoApiService _dataicoApiService;
        private readonly DataicoCustomPdfService _customPdfService;
        private readonly ICryptoService _cryptoService;
        private readonly ICryptoVault _cryptoVault;
        private readonly IUblGenerator _ublGenerator;
        private readonly IXmlSigner _xmlSigner;
        private readonly IEmailSender _emailSender;
        private readonly Fel.Infrastructure.Services.DocumentLegendService _legendService;

        public InvoiceController(
            FelDbContext dbContext, IEnumerable<IDocumentSubmissionProvider> submissionProviders, IFacilReportsClient facilReportsClient,
            IDataicoApiService dataicoApiService, DataicoCustomPdfService customPdfService, ICryptoService cryptoService,
            ICryptoVault cryptoVault, IUblGenerator ublGenerator, IXmlSigner xmlSigner, IEmailSender emailSender, Fel.Infrastructure.Services.DocumentLegendService legendService)
        {
            _dbContext = dbContext;
            _submissionProviders = submissionProviders;
            _facilReportsClient = facilReportsClient;
            _dataicoApiService = dataicoApiService;
            _customPdfService = customPdfService;
            _cryptoService = cryptoService;
            _cryptoVault = cryptoVault;
            _ublGenerator = ublGenerator;
            _xmlSigner = xmlSigner;
            _emailSender = emailSender;
            _legendService = legendService;
        }

        // Medio de Pago y Forma de Pago los administra Superadmin en el mismo catálogo global que
        // tarifas de IVA/retenciones (TaxCatalogItem) — se resuelven acá, no se hardcodean
        // en el mapper, para que un cambio desde Superadmin se refleje sin tocar código.
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

        // Tipos de documento habilitados para ESTE Client (ver ClientEnabledDocumentType) — ya no
        // es una lista fija igual para todos: el Tenant decide qué tipos puede emitir cada Client,
        // con un set estándar (Factura/NC/ND) sembrado al crear el Client. Restringido además a
        // Factura/NC/ND porque el resto del catálogo (nómina, documento soporte...) tiene su propia
        // pantalla o todavía no tiene UI aquí.
        private static readonly string[] SupportedDianCodes = { "01", "02", "91", "92" };

        [HttpGet("document-types")]
        public async Task<IActionResult> GetDocumentTypes()
        {
            var clientId = GetCurrentClientId();

            var enabledIds = await _dbContext.ClientEnabledDocumentTypes
                .Where(e => e.ClientId == clientId)
                .Select(e => e.DocumentTypeId)
                .ToListAsync();

            var types = await _dbContext.DocumentTypes
                .Where(d => d.IsActive && SupportedDianCodes.Contains(d.DianCode) && enabledIds.Contains(d.Id))
                .Select(d => new { id = d.Id, code = d.DianCode, typeCode = d.Code, operationType = d.OperationType, name = d.Name })
                .ToListAsync();

            return Ok(types);
        }

        // Sin from/to, el rango por defecto es el mes calendario en curso (1 al día actual), no
        // "últimos 30 días" corridos — con días corridos, alguien cerrando el día 3 del mes ve una
        // mezcla de dos periodos fiscales, lo que genera errores reales al presentar IVA/exógena.
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var rangeStart = (from ?? new DateTime(Fel.Core.Models.ColombiaTime.Today.Year, Fel.Core.Models.ColombiaTime.Today.Month, 1)).Date;
                var rangeEnd = (to ?? Fel.Core.Models.ColombiaTime.Today).Date.AddDays(1).AddTicks(-1);

                var invoices = await InvoiceDocuments(_dbContext.Documents, clientId, CurrentBranchScope)
                    .Include(d => d.Customer)
                    .Include(d => d.Resolution)
                    .Include(d => d.ReferenceDocument)
                    .Include(d => d.Items.OrderBy(i => i.LineNumber)).ThenInclude(i => i.Retentions)
                    .Include(d => d.Items.OrderBy(i => i.LineNumber)).ThenInclude(i => i.AdditionalTaxes)
                    .Include(d => d.GeneralRetentions)
                    .Where(d => d.ClientId == clientId && d.IssueDate >= rangeStart && d.IssueDate <= rangeEnd)
                    .OrderByDescending(d => d.IssueDate)
                    .ToListAsync();

                // Notas (NC/ND) que referencian alguna de estas facturas — una sola consulta extra
                // para armar el ícono de "documentos relacionados" en cada fila, en vez de pedir
                // /related por cada factura del listado (N+1).
                var listIds = invoices.Select(i => i.Id).ToList();
                var childNotes = await _dbContext.Documents
                    .ForBranch(CurrentBranchScope)
                    .Where(d => d.ClientId == clientId && (d.TypeCode == "NC" || d.TypeCode == "ND") && d.ReferenceDocumentId != null && listIds.Contains(d.ReferenceDocumentId.Value))
                    .Select(d => new { d.Id, d.Number, d.TypeCode, d.Status, d.TotalAmount, ReferenceDocumentId = d.ReferenceDocumentId!.Value })
                    .ToListAsync();

                var childrenByParent = childNotes
                    .GroupBy(d => d.ReferenceDocumentId)
                    .ToDictionary(g => g.Key, g => g.Select(d => new RelatedDocumentSummary
                    {
                        Id = d.Id,
                        Number = d.Number,
                        TypeCode = d.TypeCode,
                        Status = d.Status,
                        TotalAmount = d.TotalAmount
                    }).ToList());

                foreach (var inv in invoices)
                {
                    if (childrenByParent.TryGetValue(inv.Id, out var children))
                    {
                        inv.RelatedNotes = children;
                        inv.IsAnnulled = InvoiceAnnulment.IsAnnulled(inv.TypeCode, inv.TotalAmount, children);
                    }
                }

                return Ok(invoices);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        // Consulta de una factura en Dataico por su número (prefijo + consecutivo), para clientes con Dataico. Solo lee: no guarda nada.
        [HttpGet("dataico-lookup")]
        public Task<IActionResult> DataicoLookup([FromQuery] string? number) => DataicoLookupAsync(DataicoLookupKind.Invoice, number);

        [HttpGet("dataico-lookup/file")]
        public Task<IActionResult> DataicoLookupFile([FromQuery] string? number, [FromQuery] string? type) => DataicoLookupFileAsync(DataicoLookupKind.Invoice, number, type);

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var invoice = await InvoiceDocuments(_dbContext.Documents, clientId, CurrentBranchScope)
                    .Include(d => d.Customer)
                    .Include(d => d.Resolution)
                    .Include(d => d.Items.OrderBy(i => i.LineNumber)).ThenInclude(i => i.Retentions)
                    .Include(d => d.Items.OrderBy(i => i.LineNumber)).ThenInclude(i => i.AdditionalTaxes)
                    .Include(d => d.GeneralRetentions)
                    .FirstOrDefaultAsync(d => d.Id == id);

                if (invoice == null) return NotFound("Factura no encontrada.");
                return Ok(invoice);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        // Notas crédito/débito que referencian este documento — para mostrar el vínculo en la
        // vista de detalle sin depender de que la nota caiga dentro del filtro de fechas actual
        // de la lista.
        [HttpGet("{id}/related")]
        public async Task<IActionResult> GetRelated(Guid id)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var related = await _dbContext.Documents
                    .ForBranch(CurrentBranchScope)
                    .Where(d => d.ClientId == clientId && (d.TypeCode == "NC" || d.TypeCode == "ND") && d.ReferenceDocumentId == id)
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

        // Vista previa impresa (Draft o ya emitida) usando la plantilla REPX real vía Facil
        // Reports — no depende de que el documento tenga CUFE (a diferencia de
        // DataicoCustomPdfService, pensado para el correo posterior a la emisión).
        [HttpGet("{id}/preview")]
        public async Task<IActionResult> Preview(Guid id)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var invoice = await InvoiceDocuments(_dbContext.Documents, clientId, CurrentBranchScope)
                    .Include(d => d.Customer)
                    .Include(d => d.Resolution)
                    .Include(d => d.Items.OrderBy(i => i.LineNumber)).ThenInclude(i => i.Retentions)
                    .Include(d => d.Items.OrderBy(i => i.LineNumber)).ThenInclude(i => i.AdditionalTaxes)
                    .Include(d => d.GeneralRetentions)
                    .FirstOrDefaultAsync(d => d.Id == id);

                if (invoice == null) return NotFound("Factura no encontrada.");
                if (!invoice.DocumentTypeId.HasValue) return BadRequest("El documento no tiene un tipo asignado.");

                var client = await _dbContext.Clients.Include(c => c.Integrator).FirstOrDefaultAsync(c => c.Id == clientId);
                if (client == null) return NotFound("Emisor no encontrado.");

                var template = await ResolveTemplateAsync(client, invoice.DocumentTypeId.Value);
                if (template == null)
                {
                    return BadRequest("Aún no hay una plantilla de impresión configurada para este tipo de documento.");
                }

                Document? originalDocument = invoice.ReferenceDocumentId.HasValue
                    ? await _dbContext.Documents.FindAsync(invoice.ReferenceDocumentId.Value)
                    : null;

                var (paymentMeansCatalog, formaPagoCatalog) = await GetPaymentCatalogsAsync();
                var previewLocation = await BranchProvisioning.LocationAsync(_dbContext, client, invoice.BranchId);
                var data = InvoiceReportDataMapper.Build(invoice, invoice.Customer, client, previewLocation, invoice.Resolution, invoice.Items.ToList(), originalDocument, template.MostrarRetenciones, paymentMeansCatalog, formaPagoCatalog, await _legendService.ResolveAsync(client, invoice.Resolution, invoice.TypeCode), currencyWords: await CurrencyWords.LoadAsync(_dbContext));
                if (await InvoiceAnnulment.IsAnnulledAsync(_dbContext, invoice)) data[InvoiceAnnulment.WatermarkKey] = InvoiceAnnulment.WatermarkText;
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

        // Cuál plantilla usar para la vista previa: la que el cliente eligió explícitamente
        // (ClientDocumentSettings) si está publicada; si no ha elegido ninguna, la más específica
        // publicada disponible (propia del cliente > del tenant > global) — mismo orden de
        // especificidad que ClientTemplatesController.GetAvailableTemplates. Se devuelve la
        // plantilla completa (no solo el RepxTemplateKey) porque InvoiceReportDataMapper.Build
        // también necesita su MostrarRetenciones.
        private Task<Fel.Core.Entities.DocumentTemplate?> ResolveTemplateAsync(Fel.Core.Entities.Client client, Guid documentTypeId) =>
            Fel.Infrastructure.Services.DocumentTemplateResolver.ResolveAsync(_dbContext, client, documentTypeId);

        [HttpPost("draft")]
        public async Task<IActionResult> CreateDraft([FromBody] Document invoice)
        {
            try
            {
                var clientId = GetCurrentClientId();
                
                invoice.Id = Guid.NewGuid();
                invoice.ClientId = clientId;
                invoice.BranchId = GetCurrentBranchId();
                invoice.Status = "DRAFT";
                invoice.CreatedAt = DateTime.UtcNow;
                if (invoice.IssueDate == default) invoice.IssueDate = Fel.Core.Models.ColombiaTime.Now;

                // Fetch the actual document type (for DianCode etc)
                var isMandate = false;
                var isExport = false;
                if (invoice.DocumentTypeId.HasValue)
                {
                    var docType = await _dbContext.DocumentTypes.FindAsync(invoice.DocumentTypeId.Value);
                    if (docType != null)
                    {
                        invoice.TypeCode = docType.Code;
                        isMandate = docType.OperationType == Fel.Core.Entities.DocumentType.MandateOperationType;
                        isExport = docType.Code == "FE-EXP";
                    }
                }

                var itemLine = 0;
                foreach (var item in invoice.Items)
                {
                    item.Id = Guid.NewGuid();
                    item.DocumentId = invoice.Id;
                    item.LineNumber = itemLine++;
                    foreach (var retention in item.Retentions)
                    {
                        retention.Id = Guid.NewGuid();
                        retention.DocumentItemId = item.Id;
                    }
                    foreach (var tax in item.AdditionalTaxes)
                    {
                        tax.Id = Guid.NewGuid();
                        tax.DocumentItemId = item.Id;
                    }
                }

                foreach (var generalRetention in invoice.GeneralRetentions)
                {
                    generalRetention.Id = Guid.NewGuid();
                    generalRetention.DocumentId = invoice.Id;
                }

                var currencyError = await ApplyCurrencyAsync(invoice, invoice, clientId);
                if (currencyError != null) return BadRequest(currencyError);
                var exportError = await ApplyExportFieldsAsync(invoice, invoice, isExport);
                if (exportError != null) return BadRequest(exportError);

                // Mandato: lista de observaciones (consecutivo 1, 2, 3...) en su tabla; cualquier otra factura o nota: solo la observación general (Notes).
                var observations = invoice.Observations.Where(o => !string.IsNullOrWhiteSpace(o.Text)).Select(o => o.Text.Trim()).ToList();
                invoice.Observations = new List<DocumentObservation>();
                if (isMandate)
                {
                    invoice.Notes = string.Empty;
                    for (var i = 0; i < observations.Count; i++)
                        invoice.Observations.Add(new DocumentObservation { Id = Guid.NewGuid(), DocumentId = invoice.Id, Position = i + 1, Text = observations[i] });
                }
                else
                {
                    invoice.Notes = (invoice.Notes ?? string.Empty).Trim();
                }

                Fel.Core.Models.RoundingAdjustment.Apply(invoice, invoice.Items, await _dbContext.Clients.AsNoTracking().Where(c => c.Id == clientId).Select(c => c.RoundingAdjustment).FirstOrDefaultAsync());

                _dbContext.Documents.Add(invoice);
                await _dbContext.SaveChangesAsync();

                return CreatedAtAction(nameof(GetById), new { id = invoice.Id }, invoice);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }
        
        [HttpPut("{id}/draft")]
        public async Task<IActionResult> UpdateDraft(Guid id, [FromBody] Document invoiceData)
        {
            try
            {
                var clientId = GetCurrentClientId();
                // No se incluyen las Retentions viejas: nunca se usan (los ítems se reemplazan
                // completos) y si quedan trackeadas, EF genera un DELETE explícito para ellas que
                // choca con el ON DELETE CASCADE de la FK, tirando DbUpdateConcurrencyException
                // (la fila ya no existe porque la BD la borró en cascada al borrar el ítem padre).
                var invoice = await InvoiceDocuments(_dbContext.Documents, clientId, CurrentBranchScope)
                    .Include(d => d.Items.OrderBy(i => i.LineNumber))
                    .Include(d => d.GeneralRetentions)
                    .FirstOrDefaultAsync(d => d.Id == id);

                if (invoice == null) return NotFound("Factura no encontrada.");
                if (!IsEditable(invoice.Status)) return BadRequest("Solo se pueden modificar borradores o facturas rechazadas.");

                invoice.CustomerId = invoiceData.CustomerId;
                invoice.DocumentTypeId = invoiceData.DocumentTypeId;
                invoice.ReferenceDocumentId = invoiceData.ReferenceDocumentId;
                var currencyError = await ApplyCurrencyAsync(invoice, invoiceData, clientId);
                if (currencyError != null) return BadRequest(currencyError);
                var isExport = invoiceData.DocumentTypeId.HasValue
                    && await _dbContext.DocumentTypes.AsNoTracking().AnyAsync(t => t.Id == invoiceData.DocumentTypeId.Value && t.Code == "FE-EXP");
                var exportError = await ApplyExportFieldsAsync(invoice, invoiceData, isExport);
                if (exportError != null) return BadRequest(exportError);
                // Mandato: lista de observaciones (reemplazo completo, consecutivo 1, 2, 3... en el orden recibido);
                // cualquier otra factura o nota: solo la observación general (Notes).
                var isMandate = invoiceData.DocumentTypeId.HasValue
                    && await _dbContext.DocumentTypes.AsNoTracking().AnyAsync(t => t.Id == invoiceData.DocumentTypeId.Value && t.OperationType == Fel.Core.Entities.DocumentType.MandateOperationType);
                _dbContext.DocumentObservations.RemoveRange(invoice.Observations);
                invoice.Observations.Clear();
                if (isMandate)
                {
                    invoice.Notes = string.Empty;
                    var observationPosition = 1;
                    foreach (var observation in invoiceData.Observations.Where(o => !string.IsNullOrWhiteSpace(o.Text)))
                    {
                        _dbContext.DocumentObservations.Add(new DocumentObservation
                        {
                            Id = Guid.NewGuid(),
                            DocumentId = invoice.Id,
                            Position = observationPosition++,
                            Text = observation.Text.Trim()
                        });
                    }
                }
                else
                {
                    invoice.Notes = (invoiceData.Notes ?? string.Empty).Trim();
                }
                // Datos de sector (hoy: contrato AIU) — se guardan tal cual los arma el formulario.
                invoice.SectorExtensionData = string.IsNullOrWhiteSpace(invoiceData.SectorExtensionData) ? "{}" : invoiceData.SectorExtensionData;
                invoice.SectorExtensionData = invoiceData.SectorExtensionData;

                invoice.ReferenceDocumentId = invoiceData.ReferenceDocumentId;
                invoice.ReferenceConcept = invoiceData.ReferenceConcept;
                invoice.DiscrepancyResponseCode = invoiceData.DiscrepancyResponseCode;

                invoice.Subtotal = invoiceData.Subtotal;
                invoice.TaxAmount = invoiceData.TaxAmount;
                invoice.TotalAmount = invoiceData.TotalAmount;
                invoice.GeneralDiscountReason = invoiceData.GeneralDiscountReason;
                invoice.GeneralDiscountAmount = invoiceData.GeneralDiscountAmount;
                invoice.GeneralChargeReason = invoiceData.GeneralChargeReason;
                invoice.GeneralChargeAmount = invoiceData.GeneralChargeAmount;

                invoice.ResolutionId = invoiceData.ResolutionId;
                invoice.IssueDate = invoiceData.IssueDate == default ? invoice.IssueDate : invoiceData.IssueDate;
                invoice.PaymentMeans = invoiceData.PaymentMeans;
                invoice.PaymentMeansType = invoiceData.PaymentMeansType;
                invoice.PaymentTermDays = invoiceData.PaymentTermDays;
                invoice.PurchaseOrderReference = invoiceData.PurchaseOrderReference;

                // Actualizar Items (las retenciones de cada ítem se borran en cascada con el ítem)
                _dbContext.DocumentItems.RemoveRange(invoice.Items);
                invoice.Items.Clear();

                var itemLine = 0;
                foreach (var item in invoiceData.Items)
                {
                    item.Id = Guid.NewGuid();
                    item.DocumentId = invoice.Id;
                    item.LineNumber = itemLine++;
                    foreach (var retention in item.Retentions)
                    {
                        retention.Id = Guid.NewGuid();
                        retention.DocumentItemId = item.Id;
                    }
                    foreach (var tax in item.AdditionalTaxes)
                    {
                        tax.Id = Guid.NewGuid();
                        tax.DocumentItemId = item.Id;
                    }

                    // Se agrega vía el DbSet (no invoice.Items.Add) para que EF lo marque Added de
                    // forma explícita: como el Document padre ya viene trackeado (se cargó arriba),
                    // el fixup automático por navegación puede inferir mal el estado de un ítem
                    // nuevo cuyo Id (Guid) ya viene asignado, tratándolo como Modified y generando
                    // un UPDATE para una fila que nunca existió (DbUpdateConcurrencyException).
                    _dbContext.DocumentItems.Add(item);
                }

                // Retenciones generales: mismo patrón de reemplazo completo que los ítems.
                _dbContext.DocumentGeneralRetentions.RemoveRange(invoice.GeneralRetentions);
                invoice.GeneralRetentions.Clear();
                foreach (var generalRetention in invoiceData.GeneralRetentions)
                {
                    generalRetention.Id = Guid.NewGuid();
                    generalRetention.DocumentId = invoice.Id;
                    _dbContext.DocumentGeneralRetentions.Add(generalRetention);
                }

                Fel.Core.Models.RoundingAdjustment.Apply(invoice, invoiceData.Items, await _dbContext.Clients.AsNoTracking().Where(c => c.Id == clientId).Select(c => c.RoundingAdjustment).FirstOrDefaultAsync());

                await _dbContext.SaveChangesAsync();
                return Ok(invoice);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpPost("{id}/publish")]
        public async Task<IActionResult> Publish(Guid id, [FromBody] PublishInvoiceRequest request)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var invoice = await InvoiceDocuments(_dbContext.Documents, clientId, CurrentBranchScope)
                    .Include(d => d.Items.OrderBy(i => i.LineNumber)).ThenInclude(i => i.Retentions)
                    .Include(d => d.Items.OrderBy(i => i.LineNumber)).ThenInclude(i => i.AdditionalTaxes)
                    .Include(d => d.GeneralRetentions)
                    .Include(d => d.Customer)
                    .FirstOrDefaultAsync(d => d.Id == id);

                if (invoice == null) return NotFound("Factura no encontrada.");

                if (!IsEditable(invoice.Status))
                    return BadRequest("La factura ya fue emitida o está en proceso.");

                if (invoice.Customer == null)
                    return BadRequest("La factura no tiene un cliente asociado.");

                // Factura de mandato (operación 11): cada línea debe declarar su mandante, con la DIAN directa y con Dataico.
                var operationType = invoice.DocumentTypeId.HasValue
                    ? await _dbContext.DocumentTypes.AsNoTracking().Where(t => t.Id == invoice.DocumentTypeId.Value).Select(t => t.OperationType).FirstOrDefaultAsync()
                    : null;
                if (operationType == Fel.Core.Entities.DocumentType.MandateOperationType && !invoice.ReferenceDocumentId.HasValue
                    && invoice.Items.Any(i => string.IsNullOrWhiteSpace(i.MandanteIdentification) || string.IsNullOrWhiteSpace(i.MandanteIdentificationType)))
                {
                    return BadRequest("En una factura de mandato cada línea debe tener su mandante.");
                }

                var client = await _dbContext.Clients.Include(c => c.Integrator).FirstOrDefaultAsync(c => c.Id == clientId);
                if (client == null) return NotFound("Emisor no encontrado.");

                var provider =_submissionProviders.FirstOrDefault(p => p.IntegratorCode == client.Integrator.Code);
                if (provider == null)
                {
                    return BadRequest("Este emisor no tiene configurado un proveedor de documentos electrónicos habilitado.");
                }

                // El cliente puede tener varias resoluciones activas (una por prefijo); si no se
                // eligió una explícitamente al crear el borrador, se usa la marcada como
                // predeterminada para FE, y si ninguna lo está, se cae al comportamiento anterior
                // (la más reciente por vigencia) para no romper clientes que aún no eligieron default.
                //
                // Esto aplica IGUAL para notas crédito/débito: la DIAN no autoriza una "resolución"
                // propia para notas (confirmado contra el ejemplo oficial CreditNote.xml — no trae
                // sts:InvoiceControl/Autorización/Rango — y contra el propio comentario de
                // DataicoDocumentMapper: "Dataico no maneja numeración registrada para notas"). La
                // resolución de facturas del emisor solo presta su Prefijo (CorporateRegistrationScheme),
                // no un rango autorizado.
                var resolution = invoice.ResolutionId.HasValue
                    // La resolución elegida debe ser de factura de venta (FE): sirve para la factura estándar, la de mandato y la de exportación,
                    // sin exigir una específica para cada una. Las de documento soporte o nómina no se pueden usar acá.
                    ? await _dbContext.Resolutions.ForBranch(_dbContext, invoice.BranchId).FirstOrDefaultAsync(r => r.Id == invoice.ResolutionId.Value && r.ClientId == clientId && r.IsActive && r.DocumentType == "FE")
                    : await _dbContext.Resolutions.ForBranch(_dbContext, invoice.BranchId)
                        .Where(r => r.ClientId == clientId && r.IsActive && r.DocumentType == "FE")
                        .OrderByDescending(r => r.IsDefault)
                        .ThenByDescending(r => r.ValidTo)
                        .FirstOrDefaultAsync();

                if (resolution == null)
                {
                    return BadRequest("No hay una resolución de facturación activa disponible para esta sucursal.");
                }

                var paymentMeans = string.IsNullOrWhiteSpace(request?.PaymentMeans) ? invoice.PaymentMeans : request.PaymentMeans;
                var paymentMeansType = string.IsNullOrWhiteSpace(request?.PaymentMeansType) ? invoice.PaymentMeansType : request.PaymentMeansType;

                if (string.IsNullOrWhiteSpace(paymentMeansType))
                {
                    return BadRequest("Debes indicar la forma de pago (Contado/Crédito).");
                }

                // DEBITO ("Contado" en la UI) sí necesita el medio de pago concreto (efectivo,
                // transferencia, etc.). CREDITO no lo captura en ningún lado del formulario — ese
                // caso solo maneja plazo de pago — así que exigirlo aquí rechazaba toda factura a
                // crédito antes de siquiera llegar al integrador.
                if (paymentMeansType == "DEBITO" && string.IsNullOrWhiteSpace(paymentMeans))
                {
                    return BadRequest("Debes indicar el medio de pago.");
                }

                // El ajuste de centavos se recalcula con lo que realmente se va a enviar (el borrador pudo guardarse antes de activarlo en el cliente).
                Fel.Core.Models.RoundingAdjustment.Apply(invoice, invoice.Items, client.RoundingAdjustment);

                DocumentSubmissionResult result;
                try
                {
                    result = await provider.SubmitAsync(invoice, invoice.Customer, invoice.Items, client, resolution, paymentMeans, paymentMeansType);
                }
                catch (NotSupportedException ex)
                {
                    return BadRequest(ex.Message);
                }

                if (result.Success)
                {
                    // Tarifa vigente al momento de emitir, para que el corte mensual
                    // (SuperadminBillingController.CalculateBilling) tenga con qué facturar — antes
                    // quedaba en 0 y el corte real generaba cobros en $0 sin importar el volumen.
                    invoice.PriceCharged = await BranchProvisioning.PricePerDocumentAsync(_dbContext, invoice.BranchId);
                    invoice.IntegratorId = client.IntegratorId;
                }

                await _dbContext.SaveChangesAsync();

                if (!result.Success)
                {
                    return BadRequest(new { message = "El proveedor de documentos electrónicos rechazó la factura.", detail = invoice.DianResponseMessage });
                }

                return Ok(new { message = "Factura emitida correctamente.", invoice });
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
                var invoice = await InvoiceDocuments(_dbContext.Documents, clientId, CurrentBranchScope)
                    .FirstOrDefaultAsync(d => d.Id == id);

                if (invoice == null) return NotFound("Factura no encontrada.");
                if (!IsEditable(invoice.Status)) return BadRequest("Solo se pueden eliminar borradores o facturas rechazadas.");

                _dbContext.Documents.Remove(invoice);
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
            await StreamBatchAsync(_dbContext, ids, InvoiceDocuments(_dbContext.Documents, clientId, CurrentBranchScope).Where(d => ids.Contains(d.Id)).OrderBy(d => d.CreatedAt).Select(d => d.Id), id => Publish(id, new PublishInvoiceRequest()));
            return new EmptyResult();
        }

        // Eliminación en lote de borradores y rechazados, con la misma validación de "DELETE {id}".
        [HttpPost("batch/delete")]
        public async Task<IActionResult> BatchDelete([FromBody] BatchRequest request)
        {
            var invalid = ValidateBatch(request, out var ids);
            if (invalid != null) return invalid;

            var clientId = GetCurrentClientId();
            await StreamBatchAsync(_dbContext, ids, InvoiceDocuments(_dbContext.Documents, clientId, CurrentBranchScope).Where(d => ids.Contains(d.Id)).OrderBy(d => d.CreatedAt).Select(d => d.Id), id => DeleteDraft(id));
            return new EmptyResult();
        }

        // ---- Carga masiva de facturas (borradores): plantilla, vista previa y confirmación (ver InvoiceImporter).
        [HttpGet("bulk/template")]
        public IActionResult DownloadBulkTemplate() =>
            File(Fel.Api.Client.Imports.BulkImportTemplates.Invoices(), Fel.Api.Client.Models.ExcelTemplateHelper.XlsxContentType, "plantilla_facturas.xlsx");

        // Valida el archivo completo SIN guardar nada y devuelve, factura por factura, lo que se crearía.
        [HttpPost("bulk/import/preview")]
        [RequestSizeLimit(Fel.Api.Client.Imports.ExcelSheetReader.MaxBytes)]
        public async Task<IActionResult> BulkImportPreview(IFormFile file, [FromForm] string? options)
        {
            var analysis = await AnalyzeBulkImportAsync(file, options);
            return analysis.Error != null ? BadRequest(analysis.Error) : Ok(analysis.Result!.Preview);
        }

        // Vuelve a validar el mismo archivo con las decisiones del usuario y guarda terceros, productos y facturas en una sola operación: o se crea todo o nada.
        [HttpPost("bulk/import/confirm")]
        [RequestSizeLimit(Fel.Api.Client.Imports.ExcelSheetReader.MaxBytes)]
        public async Task<IActionResult> BulkImportConfirm(IFormFile file, [FromForm] string? options)
        {
            var analysis = await AnalyzeBulkImportAsync(file, options);
            if (analysis.Error != null) return BadRequest(analysis.Error);
            var result = analysis.Result!;
            if (result.Preview.FileErrors.Count > 0) return BadRequest(string.Join(" ", result.Preview.FileErrors));
            if (result.Documents.Count == 0) return BadRequest("No hay facturas por crear en este archivo.");

            var roundingMode = await _dbContext.Clients.AsNoTracking().Where(c => c.Id == GetCurrentClientId()).Select(c => c.RoundingAdjustment).FirstOrDefaultAsync();
            foreach (var document in result.Documents) Fel.Core.Models.RoundingAdjustment.Apply(document, document.Items, roundingMode);

            _dbContext.Customers.AddRange(result.Customers);
            _dbContext.Products.AddRange(result.Products);
            _dbContext.Documents.AddRange(result.Documents);
            try { await _dbContext.SaveChangesAsync(); }
            catch (DbUpdateException ex)
            {
                // Todo o nada: no se creó nada. Se explica en vez de responder 500.
                return BadRequest("No se pudo guardar la carga; no se creó ninguna factura. " + (ex.InnerException?.Message ?? ex.Message));
            }

            var summary = new Fel.Api.Client.Imports.BulkImportResult
            {
                Created = result.Documents.Count,
                Skipped = result.Preview.Rows.Count(r => r.Status != Fel.Api.Client.Imports.ImportStatus.Create)
            };
            summary.Messages.Add("Las facturas quedaron como borradores: revísalas y emítelas desde la lista.");
            if (result.Customers.Count > 0) summary.Messages.Add($"Terceros creados: {result.Customers.Count}.");
            if (result.Products.Count > 0) summary.Messages.Add($"Productos creados: {result.Products.Count} (los que no traían IVA quedaron con los impuestos por completar).");
            return Ok(summary);
        }

        private async Task<(Fel.Api.Client.Imports.InvoiceImportResult? Result, string? Error)> AnalyzeBulkImportAsync(IFormFile? file, string? optionsJson)
        {
            var clientId = GetCurrentClientId();
            var branchId = GetCurrentBranchId();
            if (file == null || file.Length == 0) return (null, "Debes adjuntar un archivo Excel (.xlsx) o CSV.");
            if (file.Length > Fel.Api.Client.Imports.ExcelSheetReader.MaxBytes) return (null, "El archivo supera los 10 MB.");
            var options = Fel.Api.Client.Imports.BulkImportOptionsParser.Parse(optionsJson);
            if (options == null) return (null, "Las opciones de la carga no son válidas.");

            using var stream = file.OpenReadStream();
            var sheet = Fel.Api.Client.Imports.ExcelSheetReader.ReadFile(stream, file.FileName, Fel.Api.Client.Imports.InvoiceImporter.Aliases, Fel.Api.Client.Imports.InvoiceImporter.Required);

            // Sin resolución de factura de venta activa en esta sucursal no se carga nada (las facturas no se podrían emitir).
            var resolutions = await _dbContext.Resolutions.AsNoTracking().ForBranch(_dbContext, CurrentBranchScope)
                .Where(r => r.ClientId == clientId && r.IsActive && r.DocumentType == "FE")
                .OrderByDescending(r => r.IsDefault).ThenByDescending(r => r.ValidTo).ToListAsync();
            var resolution = options.ResolutionId.HasValue ? resolutions.FirstOrDefault(r => r.Id == options.ResolutionId.Value) : resolutions.FirstOrDefault();
            var docType = await _dbContext.ClientEnabledDocumentTypes.AsNoTracking()
                .Where(e => e.ClientId == clientId && e.DocumentType!.IsActive && e.DocumentType.Code == "FE-STD")
                .Select(e => e.DocumentType!).FirstOrDefaultAsync();
            if (sheet.FileErrors.Count == 0)
            {
                if (resolution == null) sheet.FileErrors.Add(resolutions.Count == 0
                    ? "Esta sucursal no tiene una resolución de factura de venta activa. Configúrala antes de cargar facturas."
                    : "La resolución elegida no es una resolución de factura de venta activa de esta sucursal.");
                else if (docType == null) sheet.FileErrors.Add("Este emisor no tiene habilitado el tipo de documento Factura de venta.");
            }
            if (sheet.FileErrors.Count > 0)
            {
                var failed = new Fel.Api.Client.Imports.InvoiceImportResult();
                failed.Preview.FileErrors.AddRange(sheet.FileErrors);
                return (failed, null);
            }

            var client = await _dbContext.Clients.AsNoTracking().FirstAsync(c => c.Id == clientId);
            var defaults = await _dbContext.ClientInvoiceDefaults.AsNoTracking().FirstOrDefaultAsync(x => x.ClientId == clientId);
            var customers = await _dbContext.Customers.AsNoTracking().Where(c => c.ClientId == clientId && c.PartyType == PartyType.Cliente)
                .Select(c => new { c.Id, c.IdentificationNumber }).ToListAsync();
            var existingCustomerIds = new Dictionary<string, Guid>();
            foreach (var c in customers) existingCustomerIds.TryAdd(Fel.Api.Client.Imports.InvoiceImporter.NormalizeParty(c.IdentificationNumber), c.Id);
            var existingProducts = (await _dbContext.Products.AsNoTracking().Include(p => p.UnitOfMeasure).Include(p => p.Taxes)
                    .Where(p => p.ClientId == clientId && p.Scope == ProductScope.Invoice).ToListAsync())
                .GroupBy(p => p.Code, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var rates = (await _dbContext.TaxCatalogItems.AsNoTracking().Where(t => t.Kind == TaxCatalogKind.IvaRate && t.IsActive).Select(t => t.Category).ToListAsync())
                .Select(c => decimal.TryParse(c, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : -1).Where(d => d >= 0).ToList();
            var catalogs = await _dbContext.TaxCatalogItems.AsNoTracking()
                .Where(i => (i.Kind == TaxCatalogKind.PaymentMeans || i.Kind == TaxCatalogKind.FormaPago) && i.IsActive).ToListAsync();

            var context = new Fel.Api.Client.Imports.InvoiceImportContext
            {
                ClientId = clientId,
                BranchId = branchId,
                DocumentTypeId = docType!.Id,
                DocumentTypeCode = docType.Code,
                ResolutionId = resolution!.Id,
                Today = Fel.Core.Models.ColombiaTime.Now,
                ExistingCustomerIds = existingCustomerIds,
                ExistingProducts = existingProducts,
                PaymentMeans = catalogs.Where(i => i.Kind == TaxCatalogKind.PaymentMeans).ToDictionary(i => i.Category, i => i.Name),
                FormaPago = catalogs.Where(i => i.Kind == TaxCatalogKind.FormaPago).ToDictionary(i => i.Category, i => i.Name),
                Customers = new Fel.Api.Client.Imports.CustomerImportContext
                {
                    ClientId = clientId,
                    Municipalities = await _dbContext.DianMunicipalities.AsNoTracking().ToListAsync(),
                    IdentificationTypes = await _dbContext.IdentificationTypes.AsNoTracking().Where(t => t.IsActive).ToDictionaryAsync(t => t.Code, t => t.Name),
                    ClientAddress = new Fel.Api.Client.Imports.AddressDefaults(client.Address, client.CityCode, client.City, null),
                    DefaultAddress = new Fel.Api.Client.Imports.AddressDefaults(defaults?.CustomerAddress, defaults?.CustomerCityCode, defaults?.CustomerCityName, defaults?.CustomerPostalCode),
                    Options = options
                },
                Products = new Fel.Api.Client.Imports.ProductImportContext
                {
                    ClientId = clientId,
                    Scope = ProductScope.Invoice,
                    Units = await _dbContext.UnitsOfMeasure.AsNoTracking().Where(u => u.IsActive).ToListAsync(),
                    IvaRates = new HashSet<decimal>(rates),
                    Options = options
                }
            };
            return (Fel.Api.Client.Imports.InvoiceImporter.Analyze(sheet, context), null);
        }

        private static string DocumentTypeLabel(string typeCode) => typeCode switch
        {
            "NC" => "nota crédito",
            "ND" => "nota débito",
            _ => "factura"
        };

        private static byte[] BuildInvoiceZip(string entryBaseName, string signedXml, byte[] pdfBytes)
        {
            using var stream = new MemoryStream();
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                var xmlEntry = archive.CreateEntry($"{entryBaseName}.xml", CompressionLevel.Optimal);
                using (var writer = new StreamWriter(xmlEntry.Open()))
                {
                    writer.Write(signedXml);
                }

                var pdfEntry = archive.CreateEntry($"{entryBaseName}.pdf", CompressionLevel.Optimal);
                using (var pdfStream = pdfEntry.Open())
                {
                    pdfStream.Write(pdfBytes, 0, pdfBytes.Length);
                }
            }
            return stream.ToArray();
        }

        // Reenvío manual de un documento ya aprobado. Si el emisor usa Dataico, reutiliza el mismo
        // PUT de "reenvío/actualización" que Dataico expone (ver SendCustomDocumentPdfAsync) —
        // requiere que el documento tenga guardado su DataicoDocumentId (el uuid que Dataico asignó
        // al emitir, NO el CUFE: ver el comentario en IDataicoApiService.SendCustomDocumentPdfAsync).
        // Si el emisor emite directo a la DIAN (sin Dataico), no existe tal endpoint del lado del
        // proveedor — hay que regenerar el XML firmado (determinístico a partir de los mismos datos
        // ya guardados, debe coincidir con el CUFE original) + el PDF, empacarlos en un zip, y
        // enviarlos por correo propio (IEmailSender: SMTP del Client > del Tenant > plataforma).
        [HttpPost("{id}/resend")]
        public async Task<IActionResult> Resend(Guid id, [FromBody] ResendInvoiceRequest request)
        {
            try
            {
                var clientId = GetCurrentClientId();
                if (string.IsNullOrWhiteSpace(request?.Email))
                {
                    return BadRequest("Debes indicar un correo de destino.");
                }

                var invoice = await InvoiceDocuments(_dbContext.Documents, clientId, CurrentBranchScope)
                    .Include(d => d.Customer)
                    .Include(d => d.Resolution)
                    .Include(d => d.Items.OrderBy(i => i.LineNumber)).ThenInclude(i => i.Retentions)
                    .Include(d => d.Items.OrderBy(i => i.LineNumber)).ThenInclude(i => i.AdditionalTaxes)
                    .Include(d => d.GeneralRetentions)
                    .FirstOrDefaultAsync(d => d.Id == id);

                if (invoice == null) return NotFound("Factura no encontrada.");
                if (invoice.Status != "APPROVED")
                {
                    return BadRequest("Solo se pueden reenviar documentos ya aprobados.");
                }
                if (!invoice.DocumentTypeId.HasValue)
                {
                    return BadRequest("El documento no tiene un tipo asignado.");
                }

                var client = await _dbContext.Clients
                    .Include(c => c.Integrator)
                    .Include(c => c.Tenant)
                    .FirstOrDefaultAsync(c => c.Id == clientId);
                if (client == null) return NotFound("Emisor no encontrado.");

                Document? originalDocument = invoice.ReferenceDocumentId.HasValue
                    ? await _dbContext.Documents.FindAsync(invoice.ReferenceDocumentId.Value)
                    : null;

                if (client.Integrator.Code == "DATAICO")
                {
                    if (string.IsNullOrEmpty(invoice.DataicoDocumentId))
                    {
                        return BadRequest("Este documento no tiene guardado su identificador de Dataico; no se puede reenviar por este medio.");
                    }

                    var (resendPaymentMeansCatalog, resendFormaPagoCatalog) = await GetPaymentCatalogsAsync();
                    var resendCurrencyWords = await CurrencyWords.LoadAsync(_dbContext);
                    var resendLocation = await BranchProvisioning.LocationAsync(_dbContext, client, invoice.BranchId);
                    var leyenda = await _legendService.ResolveAsync(client, invoice.Resolution, invoice.TypeCode);
                    var resendAnnulled = await InvoiceAnnulment.IsAnnulledAsync(_dbContext, invoice);
                    Dictionary<string, object?> BuildReportData(DocumentTemplate template)
                    {
                        var reportData = InvoiceReportDataMapper.Build(invoice, invoice.Customer, client, resendLocation, invoice.Resolution, invoice.Items.ToList(), originalDocument, template.MostrarRetenciones, resendPaymentMeansCatalog, resendFormaPagoCatalog, leyenda, currencyWords: resendCurrencyWords);
                        if (resendAnnulled) reportData[InvoiceAnnulment.WatermarkKey] = InvoiceAnnulment.WatermarkText;
                        return reportData;
                    }

                    var (pdfBytes, _) = await _customPdfService.ResolveCustomPdfAsync(invoice, client, BuildReportData);
                    var credentials = DataicoDocumentMapper.ToCredentials(client, _cryptoService);
                    var result = await _dataicoApiService.SendCustomDocumentPdfAsync(invoice.DataicoDocumentId!, invoice.TypeCode, pdfBytes, request.Email, credentials);

                    if (!result.Success)
                    {
                        return BadRequest(new { message = "Dataico rechazó el reenvío.", detail = result.ErrorMessage ?? result.RawResponse });
                    }

                    return Ok(new { message = "Documento reenviado correctamente." });
                }
                else
                {
                    var certificate = await _dbContext.Certificates.FirstOrDefaultAsync(c => c.ClientId == clientId && c.IsActive);
                    if (certificate == null)
                    {
                        return BadRequest("Este emisor no tiene un certificado digital activo cargado.");
                    }

                    if (invoice.Resolution == null)
                    {
                        return BadRequest("El documento no tiene una resolución asociada.");
                    }

                    X509Certificate2 cert;
                    X509Certificate2Collection certChain;
                    try
                    {
                        cert = _cryptoVault.GetCertificate(certificate.FileName, certificate.EncryptedPassword);
                        certChain = _cryptoVault.GetCertificateChain(certificate.FileName, certificate.EncryptedPassword);
                    }
                    catch (Exception ex)
                    {
                        return BadRequest($"No se pudo cargar el certificado: {ex.Message}");
                    }

                    var municipalities = await _dbContext.DianMunicipalities.AsNoTracking().ToDictionaryAsync(m => m.Code);
                    var xmlLocation = await BranchProvisioning.LocationAsync(_dbContext, client, invoice.BranchId);
                    var resendDocType = invoice.DocumentTypeId.HasValue
                        ? await _dbContext.DocumentTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == invoice.DocumentTypeId.Value)
                        : null;
                    var resendCountryNames = await _dbContext.TaxCatalogItems.AsNoTracking()
                        .Where(t => t.Kind == TaxCatalogKind.Country)
                        .ToDictionaryAsync(t => t.Category, t => t.Name);
                    var ublData = DianDocumentMapper.BuildInvoiceData(invoice, invoice.Customer, invoice.Items, invoice.Resolution, client, xmlLocation, municipalities, null, resendDocType, null, resendCountryNames);
                    var xml = _ublGenerator.GenerateInvoiceXml(ublData);
                    var recalculatedCufe = _ublGenerator.CalculateCufe(ublData);

                    if (!string.Equals(recalculatedCufe, invoice.Cufe, StringComparison.OrdinalIgnoreCase))
                    {
                        return BadRequest("No se pudo regenerar el documento de forma consistente con el CUFE emitido originalmente. Contacta soporte.");
                    }

                    string signedXml;
                    try
                    {
                        signedXml = _xmlSigner.SignXml(xml, cert, certChain);
                    }
                    catch (Exception ex)
                    {
                        return BadRequest($"No se pudo firmar el documento: {ex.Message}");
                    }

                    var template = await ResolveTemplateAsync(client, invoice.DocumentTypeId.Value);
                    if (template == null)
                    {
                        return BadRequest("Aún no hay una plantilla de impresión configurada para este tipo de documento.");
                    }

                    var (publishPaymentMeansCatalog, publishFormaPagoCatalog) = await GetPaymentCatalogsAsync();
                    var publishLocation = await BranchProvisioning.LocationAsync(_dbContext, client, invoice.BranchId);
                    var data = InvoiceReportDataMapper.Build(invoice, invoice.Customer, client, publishLocation, invoice.Resolution, invoice.Items.ToList(), originalDocument, template.MostrarRetenciones, publishPaymentMeansCatalog, publishFormaPagoCatalog, await _legendService.ResolveAsync(client, invoice.Resolution, invoice.TypeCode), currencyWords: await CurrencyWords.LoadAsync(_dbContext));
                    if (await InvoiceAnnulment.IsAnnulledAsync(_dbContext, invoice)) data[InvoiceAnnulment.WatermarkKey] = InvoiceAnnulment.WatermarkText;
                    var pdfBytes = await _facilReportsClient.GenerateReportAsync(template.RepxTemplateKey, data);
                    if (pdfBytes == null)
                    {
                        return BadRequest("No se pudo generar el PDF para el reenvío.");
                    }

                    var entryBaseName = string.IsNullOrEmpty(invoice.Number) ? invoice.Id.ToString() : invoice.Number;
                    var zipBytes = BuildInvoiceZip(entryBaseName, signedXml, pdfBytes);

                    var attachments = new List<EmailAttachment>
                    {
                        new EmailAttachment { FileName = $"{entryBaseName}.zip", Content = zipBytes, ContentType = "application/zip" }
                    };

                    var label = DocumentTypeLabel(invoice.TypeCode);
                    var subject = $"Reenvío de tu {label} N.º {invoice.Number}";
                    var bodyHtml = $"<p>Adjunto encontrarás el XML y la representación gráfica de tu {label} N.º {invoice.Number}.</p>";

                    var sendResult = await _emailSender.SendAsync(client, request.Email, subject, bodyHtml, attachments);
                    if (!sendResult.Success)
                    {
                        return BadRequest(new { message = "No se pudo reenviar el documento.", detail = sendResult.ErrorMessage });
                    }

                    return Ok(new { message = "Documento reenviado correctamente." });
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }
    }

    public class PublishInvoiceRequest
    {
        public string PaymentMeans { get; set; } = string.Empty;
        public string PaymentMeansType { get; set; } = string.Empty;
    }

    public class ResendInvoiceRequest
    {
        public string Email { get; set; } = string.Empty;
    }
}
