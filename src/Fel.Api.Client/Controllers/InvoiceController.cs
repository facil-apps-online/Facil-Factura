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

namespace Fel.Api.Client.Controllers
{
    [ApiController]
    [Route("api/client/invoices")]
    public class InvoiceController : ControllerBase
    {
        // Editable/publicable/eliminable mientras no haya llegado exitosamente a Dataico/DIAN:
        // un DRAFT nunca se envió, y un REJECTED fue rechazado por Dataico, así que tampoco llegó.
        private static bool IsEditable(string status) => status == "DRAFT" || status == "REJECTED";

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

        public InvoiceController(
            FelDbContext dbContext, IEnumerable<IDocumentSubmissionProvider> submissionProviders, IFacilReportsClient facilReportsClient,
            IDataicoApiService dataicoApiService, DataicoCustomPdfService customPdfService, ICryptoService cryptoService,
            ICryptoVault cryptoVault, IUblGenerator ublGenerator, IXmlSigner xmlSigner, IEmailSender emailSender)
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
        private static readonly string[] SupportedDianCodes = { "01", "91", "92" };

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
                .Select(d => new { id = d.Id, code = d.DianCode, name = d.Name })
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

                var invoices = await _dbContext.Documents
                    .Include(d => d.Customer)
                    .Include(d => d.Resolution)
                    .Include(d => d.ReferenceDocument)
                    .Include(d => d.Items.OrderBy(i => i.LineNumber)).ThenInclude(i => i.Retentions)
                    .Include(d => d.GeneralRetentions)
                    .Where(d => d.ClientId == clientId && d.IssueDate >= rangeStart && d.IssueDate <= rangeEnd)
                    .OrderByDescending(d => d.IssueDate)
                    .ToListAsync();

                // Notas (NC/ND) que referencian alguna de estas facturas — una sola consulta extra
                // para armar el ícono de "documentos relacionados" en cada fila, en vez de pedir
                // /related por cada factura del listado (N+1).
                var listIds = invoices.Select(i => i.Id).ToList();
                var childNotes = await _dbContext.Documents
                    .Where(d => d.ClientId == clientId && d.ReferenceDocumentId != null && listIds.Contains(d.ReferenceDocumentId.Value))
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
                    if (childrenByParent.TryGetValue(inv.Id, out var children)) inv.RelatedNotes = children;
                }

                return Ok(invoices);
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
                var invoice = await _dbContext.Documents
                    .Include(d => d.Customer)
                    .Include(d => d.Resolution)
                    .Include(d => d.Items.OrderBy(i => i.LineNumber)).ThenInclude(i => i.Retentions)
                    .Include(d => d.GeneralRetentions)
                    .FirstOrDefaultAsync(d => d.Id == id && d.ClientId == clientId);

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

        // Vista previa impresa (Draft o ya emitida) usando la plantilla REPX real vía Facil
        // Reports — no depende de que el documento tenga CUFE (a diferencia de
        // DataicoCustomPdfService, pensado para el correo posterior a la emisión).
        [HttpGet("{id}/preview")]
        public async Task<IActionResult> Preview(Guid id)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var invoice = await _dbContext.Documents
                    .Include(d => d.Customer)
                    .Include(d => d.Resolution)
                    .Include(d => d.Items.OrderBy(i => i.LineNumber)).ThenInclude(i => i.Retentions)
                    .Include(d => d.GeneralRetentions)
                    .FirstOrDefaultAsync(d => d.Id == id && d.ClientId == clientId);

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
                var data = InvoiceReportDataMapper.Build(invoice, invoice.Customer, client, invoice.Resolution, invoice.Items.ToList(), originalDocument, template.MostrarRetenciones, paymentMeansCatalog, formaPagoCatalog);
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
                invoice.Status = "DRAFT";
                invoice.CreatedAt = DateTime.UtcNow;
                if (invoice.IssueDate == default) invoice.IssueDate = Fel.Core.Models.ColombiaTime.Now;

                // Fetch the actual document type (for DianCode etc)
                if (invoice.DocumentTypeId.HasValue)
                {
                    var docType = await _dbContext.DocumentTypes.FindAsync(invoice.DocumentTypeId.Value);
                    if (docType != null) invoice.TypeCode = docType.Code;
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
                }

                foreach (var generalRetention in invoice.GeneralRetentions)
                {
                    generalRetention.Id = Guid.NewGuid();
                    generalRetention.DocumentId = invoice.Id;
                }

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
                var invoice = await _dbContext.Documents
                    .Include(d => d.Items.OrderBy(i => i.LineNumber))
                    .Include(d => d.GeneralRetentions)
                    .FirstOrDefaultAsync(d => d.Id == id && d.ClientId == clientId);

                if (invoice == null) return NotFound("Factura no encontrada.");
                if (!IsEditable(invoice.Status)) return BadRequest("Solo se pueden modificar borradores o facturas rechazadas.");

                invoice.CustomerId = invoiceData.CustomerId;
                invoice.DocumentTypeId = invoiceData.DocumentTypeId;
                invoice.Notes = invoiceData.Notes;
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
                var invoice = await _dbContext.Documents
                    .Include(d => d.Items.OrderBy(i => i.LineNumber)).ThenInclude(i => i.Retentions)
                    .Include(d => d.GeneralRetentions)
                    .Include(d => d.Customer)
                    .FirstOrDefaultAsync(d => d.Id == id && d.ClientId == clientId);

                if (invoice == null) return NotFound("Factura no encontrada.");

                if (!IsEditable(invoice.Status))
                    return BadRequest("La factura ya fue emitida o está en proceso.");

                if (invoice.Customer == null)
                    return BadRequest("La factura no tiene un cliente asociado.");

                var client = await _dbContext.Clients.Include(c => c.Integrator).FirstOrDefaultAsync(c => c.Id == clientId);
                if (client == null) return NotFound("Emisor no encontrado.");

                var provider = _submissionProviders.FirstOrDefault(p => p.IntegratorCode == client.Integrator.Code);
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
                    ? await _dbContext.Resolutions.FirstOrDefaultAsync(r => r.Id == invoice.ResolutionId.Value && r.ClientId == clientId && r.IsActive)
                    : await _dbContext.Resolutions
                        .Where(r => r.ClientId == clientId && r.IsActive && r.DocumentType == "FE")
                        .OrderByDescending(r => r.IsDefault)
                        .ThenByDescending(r => r.ValidTo)
                        .FirstOrDefaultAsync();

                if (resolution == null)
                {
                    return BadRequest("No hay una resolución de facturación activa registrada para este emisor.");
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
                    invoice.PriceCharged = client.PricePerDocument;
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
                var invoice = await _dbContext.Documents
                    .FirstOrDefaultAsync(d => d.Id == id && d.ClientId == clientId);

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

        [HttpGet("template")]
        public IActionResult DownloadTemplate()
        {
            using var workbook = new ClosedXML.Excel.XLWorkbook();

            var headerSheet = workbook.Worksheets.Add("Encabezados");
            Fel.Api.Client.Models.ExcelTemplateHelper.WriteHeaderRow(headerSheet, 1,
                "NumeroInterno", "Identificacion", "MedioDePago", "FormaDePago");
            headerSheet.Cell(2, 1).Value = "FAC-001";
            headerSheet.Cell(2, 2).Value = "12345678";
            headerSheet.Cell(2, 3).Value = "EFECTIVO";
            headerSheet.Cell(2, 4).Value = "DEBITO";
            headerSheet.Columns().AdjustToContents();

            var detailSheet = workbook.Worksheets.Add("Detalle");
            Fel.Api.Client.Models.ExcelTemplateHelper.WriteHeaderRow(detailSheet, 1,
                "NumeroInterno", "Codigo", "Nombre", "Cantidad", "ValorUnitario", "TarifaIVA");
            detailSheet.Cell(2, 1).Value = "FAC-001";
            detailSheet.Cell(2, 2).Value = "SKU-001";
            detailSheet.Cell(2, 3).Value = "Producto de ejemplo";
            detailSheet.Cell(2, 4).Value = 1;
            detailSheet.Cell(2, 5).Value = 50000;
            detailSheet.Cell(2, 6).Value = 19;
            detailSheet.Columns().AdjustToContents();

            var bytes = Fel.Api.Client.Models.ExcelTemplateHelper.ToBytes(workbook);
            return File(bytes, Fel.Api.Client.Models.ExcelTemplateHelper.XlsxContentType, "plantilla_facturas.xlsx");
        }

        // Plantilla de importación masiva (2 hojas):
        // Hoja "Encabezados": NumeroInterno | Identificacion | MedioDePago | FormaDePago
        // Hoja "Detalle": NumeroInterno | Codigo | Nombre | Cantidad | ValorUnitario | TarifaIVA
        [HttpPost("import")]
        public async Task<IActionResult> Import(IFormFile file)
        {
            try
            {
                var clientId = GetCurrentClientId();

                var client = await _dbContext.Clients.Include(c => c.Integrator).FirstOrDefaultAsync(c => c.Id == clientId);
                if (client == null) return NotFound("Emisor no encontrado.");

                if (client.Integrator.Code != "DATAICO")
                {
                    return BadRequest("Este emisor no tiene configurado un proveedor de documentos electrónicos habilitado.");
                }

                var resolution = await _dbContext.Resolutions
                    .Where(r => r.ClientId == clientId && r.IsActive && r.DocumentType == "FE")
                    .OrderByDescending(r => r.IsDefault)
                    .ThenByDescending(r => r.ValidTo)
                    .FirstOrDefaultAsync();

                if (resolution == null)
                {
                    return BadRequest("No hay una resolución de facturación activa registrada para este emisor.");
                }

                if (file == null || file.Length == 0)
                {
                    return BadRequest("Debes adjuntar un archivo Excel (.xlsx).");
                }

                using var stream = file.OpenReadStream();
                using var workbook = new ClosedXML.Excel.XLWorkbook(stream);
                var headerSheet = workbook.Worksheet("Encabezados");
                var detailSheet = workbook.Worksheet("Detalle");

                var summary = new Fel.Api.Client.Models.ImportSummary();

                foreach (var headerRow in headerSheet.RowsUsed().Skip(1))
                {
                    var rowNumber = headerRow.RowNumber();
                    summary.TotalRows++;

                    try
                    {
                        var internalNumber = headerRow.Cell(1).GetString().Trim();
                        var identification = headerRow.Cell(2).GetString().Trim();
                        var paymentMeans = headerRow.Cell(3).GetString().Trim().ToUpperInvariant();
                        var paymentMeansType = headerRow.Cell(4).GetString().Trim().ToUpperInvariant();

                        var customer = await _dbContext.Customers.FirstOrDefaultAsync(c => c.ClientId == clientId && c.IdentificationNumber == identification);
                        if (customer == null)
                        {
                            summary.Results.Add(new Fel.Api.Client.Models.ImportRowResult { Row = rowNumber, Success = false, Message = $"No existe un tercero con identificación {identification}." });
                            summary.Failed++;
                            continue;
                        }

                        var items = detailSheet.RowsUsed().Skip(1)
                            .Where(r => r.Cell(1).GetString().Trim() == internalNumber)
                            .Select((r, idx) => new DocumentItem
                            {
                                Id = Guid.NewGuid(),
                                LineNumber = idx,
                                Code = r.Cell(2).GetString().Trim(),
                                Name = r.Cell(3).GetString().Trim(),
                                Quantity = r.Cell(4).GetValue<decimal>(),
                                UnitPrice = r.Cell(5).GetValue<decimal>(),
                                TaxRate = r.Cell(6).IsEmpty() ? 0 : r.Cell(6).GetValue<decimal>()
                            }).ToList();

                        if (items.Count == 0)
                        {
                            summary.Results.Add(new Fel.Api.Client.Models.ImportRowResult { Row = rowNumber, Success = false, Message = $"No se encontraron ítems para el número interno {internalNumber} en la hoja Detalle." });
                            summary.Failed++;
                            continue;
                        }

                        // La carga masiva solo deja las facturas en borrador — el cliente las revisa y
                        // las emite una a una (o las edita) desde la lista. Nunca se envían a Dataico
                        // automáticamente desde el import.
                        var invoice = new Document
                        {
                            Id = Guid.NewGuid(),
                            ClientId = clientId,
                            CustomerId = customer.Id,
                            Customer = customer,
                            TypeCode = "FE",
                            Number = DateTime.UtcNow.Ticks.ToString(),
                            Status = "DRAFT",
                            CreatedAt = DateTime.UtcNow,
                            IssueDate = Fel.Core.Models.ColombiaTime.Now,
                            PaymentMeans = paymentMeans,
                            PaymentMeansType = paymentMeansType,
                            Subtotal = items.Sum(i => i.Quantity * i.UnitPrice),
                            TaxAmount = items.Sum(i => i.Quantity * i.UnitPrice * i.TaxRate / 100),
                            TotalAmount = items.Sum(i => i.Quantity * i.UnitPrice * (1 + i.TaxRate / 100))
                        };

                        foreach (var item in items)
                        {
                            item.DocumentId = invoice.Id;
                            item.TaxAmount = item.Quantity * item.UnitPrice * item.TaxRate / 100;
                            item.TotalAmount = item.Quantity * item.UnitPrice * (1 + item.TaxRate / 100);
                            invoice.Items.Add(item);
                        }

                        _dbContext.Documents.Add(invoice);
                        await _dbContext.SaveChangesAsync();

                        summary.Results.Add(new Fel.Api.Client.Models.ImportRowResult
                        {
                            Row = rowNumber,
                            Success = true,
                            Message = $"Borrador de factura {internalNumber} creado. Revísala y emítela desde la lista."
                        });
                        summary.Succeeded++;
                    }
                    catch (Exception ex)
                    {
                        summary.Results.Add(new Fel.Api.Client.Models.ImportRowResult { Row = rowNumber, Success = false, Message = ex.Message });
                        summary.Failed++;
                    }
                }

                return Ok(summary);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
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

                var invoice = await _dbContext.Documents
                    .Include(d => d.Customer)
                    .Include(d => d.Resolution)
                    .Include(d => d.Items.OrderBy(i => i.LineNumber)).ThenInclude(i => i.Retentions)
                    .Include(d => d.GeneralRetentions)
                    .FirstOrDefaultAsync(d => d.Id == id && d.ClientId == clientId);

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
                    Dictionary<string, object?> BuildReportData(DocumentTemplate template) =>
                        InvoiceReportDataMapper.Build(invoice, invoice.Customer, client, invoice.Resolution, invoice.Items.ToList(), originalDocument, template.MostrarRetenciones, resendPaymentMeansCatalog, resendFormaPagoCatalog);

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
                    var ublData = DianDocumentMapper.BuildInvoiceData(invoice, invoice.Customer, invoice.Items, invoice.Resolution, client, municipalities);
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
                    var data = InvoiceReportDataMapper.Build(invoice, invoice.Customer, client, invoice.Resolution, invoice.Items.ToList(), originalDocument, template.MostrarRetenciones, publishPaymentMeansCatalog, publishFormaPagoCatalog);
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
