using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Dataico;

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

        public InvoiceController(FelDbContext dbContext, IEnumerable<IDocumentSubmissionProvider> submissionProviders, IFacilReportsClient facilReportsClient)
        {
            _dbContext = dbContext;
            _submissionProviders = submissionProviders;
            _facilReportsClient = facilReportsClient;
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
                var rangeStart = (from ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1)).Date;
                var rangeEnd = (to ?? DateTime.UtcNow).Date.AddDays(1).AddTicks(-1);

                var invoices = await _dbContext.Documents
                    .Include(d => d.Customer)
                    .Include(d => d.Resolution)
                    .Where(d => d.ClientId == clientId && d.IssueDate >= rangeStart && d.IssueDate <= rangeEnd)
                    .OrderByDescending(d => d.IssueDate)
                    .ToListAsync();

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
                    .Include(d => d.Items).ThenInclude(i => i.Retentions)
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
                    .Include(d => d.Items).ThenInclude(i => i.Retentions)
                    .Include(d => d.GeneralRetentions)
                    .FirstOrDefaultAsync(d => d.Id == id && d.ClientId == clientId);

                if (invoice == null) return NotFound("Factura no encontrada.");
                if (!invoice.DocumentTypeId.HasValue) return BadRequest("El documento no tiene un tipo asignado.");

                var client = await _dbContext.Clients.Include(c => c.Integrator).FirstOrDefaultAsync(c => c.Id == clientId);
                if (client == null) return NotFound("Emisor no encontrado.");

                var templateKey = await ResolveTemplateKeyAsync(client, invoice.DocumentTypeId.Value);
                if (templateKey == null)
                {
                    return BadRequest("Aún no hay una plantilla de impresión configurada para este tipo de documento.");
                }

                Document? originalDocument = invoice.ReferenceDocumentId.HasValue
                    ? await _dbContext.Documents.FindAsync(invoice.ReferenceDocumentId.Value)
                    : null;

                var data = InvoiceReportDataMapper.Build(invoice, invoice.Customer, client, invoice.Resolution, invoice.Items.ToList(), originalDocument);
                var pdfBytes = await _facilReportsClient.GenerateReportAsync(templateKey, data);
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
        // especificidad que ClientTemplatesController.GetAvailableTemplates.
        private async Task<string?> ResolveTemplateKeyAsync(Fel.Core.Entities.Client client, Guid documentTypeId)
        {
            var selected = await _dbContext.ClientDocumentSettings
                .Include(s => s.SelectedTemplate)
                .FirstOrDefaultAsync(s => s.ClientId == client.Id && s.DocumentTypeId == documentTypeId);

            if (selected?.SelectedTemplate != null && selected.SelectedTemplate.Status == TemplateStatus.Published)
            {
                return selected.SelectedTemplate.RepxTemplateKey;
            }

            var candidates = await _dbContext.DocumentTemplates
                .Where(t => t.DocumentTypeId == documentTypeId && t.Status == TemplateStatus.Published &&
                            ((t.TenantId == null && t.ClientId == null) ||
                             (t.TenantId == client.TenantId && t.ClientId == null) ||
                             t.ClientId == client.Id))
                .ToListAsync();

            var best = candidates.FirstOrDefault(t => t.ClientId == client.Id)
                ?? candidates.FirstOrDefault(t => t.TenantId == client.TenantId && t.ClientId == null)
                ?? candidates.FirstOrDefault(t => t.TenantId == null && t.ClientId == null);

            return best?.RepxTemplateKey;
        }

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
                if (invoice.IssueDate == default) invoice.IssueDate = DateTime.UtcNow;

                // Fetch the actual document type (for DianCode etc)
                if (invoice.DocumentTypeId.HasValue)
                {
                    var docType = await _dbContext.DocumentTypes.FindAsync(invoice.DocumentTypeId.Value);
                    if (docType != null) invoice.TypeCode = docType.Code;
                }

                foreach (var item in invoice.Items)
                {
                    item.Id = Guid.NewGuid();
                    item.DocumentId = invoice.Id;
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
                    .Include(d => d.Items)
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

                foreach (var item in invoiceData.Items)
                {
                    item.Id = Guid.NewGuid();
                    item.DocumentId = invoice.Id;
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
                    .Include(d => d.Items).ThenInclude(i => i.Retentions)
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

                if (string.IsNullOrWhiteSpace(paymentMeans) || string.IsNullOrWhiteSpace(paymentMeansType))
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
                            .Select(r => new DocumentItem
                            {
                                Id = Guid.NewGuid(),
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
                            IssueDate = DateTime.UtcNow,
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

    }

    public class PublishInvoiceRequest
    {
        public string PaymentMeans { get; set; } = string.Empty;
        public string PaymentMeansType { get; set; } = string.Empty;
    }
}
