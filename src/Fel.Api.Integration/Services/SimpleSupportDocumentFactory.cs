using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Models;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Dataico.Models;
using Fel.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Integration.Services
{
    // Cuerpo del documento soporte por la API: la misma estructura que recibe Dataico. El vendedor (el sujeto no obligado a facturar) va en
    // `customer`, como en Dataico; el comprador es siempre el emisor que se autentica.
    public class ApiSupportRequest : DataicoInvoiceRequest
    {
    }

    // Nota de ajuste al documento soporte. El documento se indica con source_document_id (el id que devolvió esta API) o source_document_number
    // (prefijo + número, por ejemplo "DSE5"). Sin items la nota replica las líneas del documento (útil para anular).
    public class ApiSupportAdjustmentRequest : ApiSupportRequest
    {
        public string? source_document_id { get; set; }
        public string? source_document_number { get; set; }
        // DEVOLUCION_PARCIAL | ANULACION | REBAJA | AJUSTE_DE_PRECIO | OTROS (o el código DIAN 1 a 5).
        public string? reason { get; set; }
        public string? reason_description { get; set; }
    }

    // Arma el documento soporte (Document DS) o su nota de ajuste (DS-AJUSTE) que luego emite el mismo motor del portal, por Dataico o por la DIAN
    // directa. Igual que la factura, el consecutivo lo trae el integrador y solo se exige que esté en el rango de la resolución y sin usar.
    public class SimpleSupportDocumentFactory
    {
        public const string SupportDocument = "DS";
        public const string SupportAdjustment = "DS-AJUSTE";

        // Conceptos de corrección de la nota de ajuste (16.2.4 del Anexo del documento soporte).
        private static readonly (string Code, string Key, string Name)[] Reasons =
        {
            ("1", "DEVOLUCION_PARCIAL", "Devolución parcial de los bienes y/o no aceptación parcial del servicio"),
            ("2", "ANULACION", "Anulación del documento soporte en adquisiciones efectuadas a sujetos no obligados a expedir factura de venta o documento equivalente"),
            ("3", "REBAJA", "Rebaja o descuento parcial o total"),
            ("4", "AJUSTE_DE_PRECIO", "Ajuste de precio"),
            ("5", "OTROS", "Otros")
        };

        private readonly FelDbContext _db;
        private readonly SimpleInvoiceFactory _invoiceFactory;

        public SimpleSupportDocumentFactory(FelDbContext db, SimpleInvoiceFactory invoiceFactory)
        {
            _db = db;
            _invoiceFactory = invoiceFactory;
        }

        public async Task<InvoiceBuildResult> BuildAsync(ApiSupportRequest request, bool isAdjustment, Client client, Guid branchId)
        {
            var adjustment = request as ApiSupportAdjustmentRequest;
            var problems = Validate(request, adjustment, isAdjustment);
            if (problems.Count > 0)
                return Fail(400, "VALIDATION_ERROR", "El documento tiene datos inválidos o faltantes. Revisa el detalle de cada campo.", problems);

            var typeCode = isAdjustment ? SupportAdjustment : SupportDocument;
            var documentType = await _db.DocumentTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Code == typeCode && t.IsActive);
            if (documentType == null) return Fail(422, "DOCUMENT_TYPE_UNAVAILABLE", $"El tipo de documento {typeCode} no está disponible.");

            var number = long.Parse(request.number.Trim(), CultureInfo.InvariantCulture);
            var numberText = number.ToString(CultureInfo.InvariantCulture);

            Document? original = null;
            (string Code, string Key, string Name)? reason = null;
            Resolution resolution;
            string prefix;
            if (isAdjustment)
            {
                original = await FindSupportDocumentAsync(adjustment!, client.Id);
                if (original == null)
                    return Fail(404, "SUPPORT_DOCUMENT_NOT_FOUND", "No se encontró el documento soporte que la nota ajusta. Envía source_document_id (el id que devolvió la API) o source_document_number (prefijo y número, por ejemplo DSE5); debe ser un documento soporte de este emisor.");
                if (original.Status != "APPROVED")
                    return Fail(422, "SUPPORT_DOCUMENT_NOT_APPROVED", $"El documento soporte {original.Prefix}{original.Number} está en estado {original.Status}: solo se pueden ajustar documentos aprobados.");
                if (!original.ResolutionId.HasValue || string.IsNullOrEmpty(original.Cufe))
                    return Fail(422, "SUPPORT_DOCUMENT_WITHOUT_CUDS", "El documento soporte referenciado no tiene resolución o CUDS registrado.");

                reason = Reasons.FirstOrDefault(r => string.Equals(r.Key, adjustment!.reason!.Trim(), StringComparison.OrdinalIgnoreCase) || r.Code == adjustment.reason.Trim());
                if (reason == null || reason.Value.Code == null)
                    return Fail(400, "VALIDATION_ERROR", "La nota tiene datos inválidos o faltantes. Revisa el detalle de cada campo.",
                        new List<string> { $"reason: \"{adjustment!.reason}\" no es un motivo válido. Usa uno de: {string.Join(", ", Reasons.Select(r => r.Key))} (o el código DIAN 1 a 5)." });

                resolution = original.Resolution!;
                prefix = (request.numbering?.prefix ?? string.Empty).Trim();
                if (prefix.Length == 0)
                    prefix = await ResolutionNumbering.GetNotePrefixAsync(_db, client.Id, branchId, NoteKind.SupportAdjustment, original.ResolutionId) ?? resolution.Prefix ?? string.Empty;
            }
            else
            {
                var found = await _invoiceFactory.FindResolutionAsync(client.Id, branchId, request.numbering, (request.numbering.prefix ?? string.Empty).Trim(), number, "DS");
                if (found.Error != null) return new InvoiceBuildResult { Error = found.Error };
                resolution = found.Resolution!;
                prefix = resolution.Prefix ?? string.Empty;
            }

            var existing = await _db.Documents
                .Include(d => d.Items).ThenInclude(i => i.Retentions)
                .Include(d => d.Items).ThenInclude(i => i.AdditionalTaxes)
                .Include(d => d.GeneralRetentions)
                .FirstOrDefaultAsync(d => d.ClientId == client.Id && d.TypeCode == typeCode && d.Prefix == prefix && d.Number == numberText);
            if (existing != null && !InvoicePublishingService.IsEditable(existing.Status))
                return new InvoiceBuildResult { ExistingLocked = existing };

            var warnings = new List<string>();
            Customer seller;
            if (isAdjustment)
            {
                seller = original!.Customer!;
            }
            else
            {
                var customerResult = await _invoiceFactory.EnsureCustomerAsync(client.Id, request.customer, warnings, PartyType.Proveedor);
                if (customerResult.Error != null) return new InvoiceBuildResult { Error = customerResult.Error };
                seller = customerResult.Customer!;
            }

            List<DocumentItem> items;
            if (request.items is { Count: > 0 })
            {
                var built = await _invoiceFactory.BuildItemsAsync(request.items, false, warnings);
                if (built.Error != null) return new InvoiceBuildResult { Error = built.Error };
                items = built.Items;
            }
            else
            {
                items = original!.Items.OrderBy(i => i.LineNumber).Select(CloneItem).ToList();
            }
            // El vendedor no obligado no es responsable de IVA: las líneas no llevan impuesto (igual que el formulario del portal).
            foreach (var item in items) { item.IvaTreatment = IvaTreatment.Exento; item.TaxRate = 0; }

            var paymentMeansType = isAdjustment ? original!.PaymentMeansType : request.payment_means_type.Trim().ToUpperInvariant();
            string? paymentMeans;
            if (isAdjustment) paymentMeans = original!.PaymentMeans;
            else
            {
                var paymentResult = await _invoiceFactory.ResolvePaymentMeansAsync(request);
                if (paymentResult.Error != null) return new InvoiceBuildResult { Error = paymentResult.Error };
                paymentMeans = paymentResult.Category;
            }

            var document = existing ?? new Document { Id = Guid.NewGuid(), ClientId = client.Id, CreatedAt = DateTime.UtcNow };
            if (existing != null)
            {
                _db.DocumentItems.RemoveRange(existing.Items);
                existing.Items.Clear();
                _db.DocumentGeneralRetentions.RemoveRange(existing.GeneralRetentions);
                existing.GeneralRetentions.Clear();
            }

            var issueDate = SimpleInvoiceFactory.ParseDate(request.issue_date) ?? ColombiaTime.Now;
            document.TrackingId = document.Id.ToString();
            document.BranchId = branchId;
            document.Status = "DRAFT";
            document.TypeCode = typeCode;
            document.DocumentTypeId = documentType.Id;
            document.Prefix = prefix;
            document.Number = numberText;
            document.ResolutionId = resolution.Id;
            document.CustomerId = seller.Id;
            document.Customer = seller;
            document.IssueDate = issueDate;
            document.PaymentMeansType = paymentMeansType;
            document.PaymentMeans = paymentMeans ?? string.Empty;
            document.PurchaseOrderReference = string.IsNullOrWhiteSpace(request.order_reference) ? null : request.order_reference.Trim();
            document.SectorExtensionData = "{}";
            document.Currency = "COP";
            var dueDate = SimpleInvoiceFactory.ParseDate(request.payment_date);
            document.PaymentTermDays = isAdjustment ? original!.PaymentTermDays : dueDate.HasValue ? Math.Max(0, (int)(dueDate.Value.Date - issueDate.Date).TotalDays) : null;

            var notes = (request.notes ?? new List<string>()).Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim());
            document.Notes = string.Join(" ", isAdjustment ? notes.Append(adjustment!.reason_description ?? string.Empty).Where(n => !string.IsNullOrWhiteSpace(n)) : notes);

            if (isAdjustment)
            {
                document.ReferenceDocumentId = original!.Id;
                document.DiscrepancyResponseCode = reason!.Value.Code;
                // Descripción oficial del concepto (la lista del Anexo); el texto libre del integrador va en las notas.
                document.ReferenceConcept = reason.Value.Name;
            }

            SimpleInvoiceFactory.ApplyCharges(document, request.charges);

            var line = 0;
            foreach (var item in items)
            {
                item.DocumentId = document.Id;
                item.LineNumber = line++;
                document.Items.Add(item);
            }

            foreach (var retention in request.retentions ?? new List<DataicoTax>())
            {
                if (retention.tax_rate is not > 0) continue;
                document.GeneralRetentions.Add(new DocumentGeneralRetention
                {
                    Id = Guid.NewGuid(), DocumentId = document.Id,
                    TaxCategory = retention.tax_category.Trim().ToUpperInvariant(), Rate = retention.tax_rate.Value
                });
            }

            DocumentTotalsCalculator.Apply(document, document.Items, isSupportDocument: true);

            if (isAdjustment)
            {
                var adjusted = await _db.Documents.AsNoTracking()
                    .Where(d => d.ReferenceDocumentId == original!.Id && d.TypeCode == SupportAdjustment && d.Status == "APPROVED" && d.Id != document.Id)
                    .SumAsync(d => (decimal?)d.TotalAmount) ?? 0m;
                if (reason!.Value.Code == "2" && adjusted + document.TotalAmount > original!.TotalAmount + 0.01m)
                    return Fail(422, "NOTE_EXCEEDS_DOCUMENT",
                        $"La nota por {document.TotalAmount:0.##} supera lo que queda por ajustar del documento soporte {original.Prefix}{original.Number}: total {original.TotalAmount:0.##}, ya ajustado {adjusted:0.##}.");
            }

            if (existing != null)
            {
                foreach (var item in document.Items)
                {
                    _db.Entry(item).State = EntityState.Added;
                    foreach (var retention in item.Retentions) _db.Entry(retention).State = EntityState.Added;
                    foreach (var tax in item.AdditionalTaxes) _db.Entry(tax).State = EntityState.Added;
                }
                foreach (var retention in document.GeneralRetentions) _db.Entry(retention).State = EntityState.Added;
            }

            var result = new InvoiceBuildResult { Document = document, Reused = existing != null };
            result.Warnings.AddRange(warnings);
            return result;
        }

        private async Task<Document?> FindSupportDocumentAsync(ApiSupportAdjustmentRequest request, Guid clientId)
        {
            var query = _db.Documents
                .Include(d => d.Customer)
                .Include(d => d.Resolution)
                .Include(d => d.Items).ThenInclude(i => i.Retentions)
                .Include(d => d.Items).ThenInclude(i => i.AdditionalTaxes)
                .Where(d => d.ClientId == clientId && d.TypeCode == SupportDocument);

            if (!string.IsNullOrWhiteSpace(request.source_document_id))
            {
                var id = request.source_document_id.Trim();
                if (Guid.TryParse(id, out var guid)) return await query.FirstOrDefaultAsync(d => d.Id == guid || d.DataicoDocumentId == id);
                return await query.FirstOrDefaultAsync(d => d.DataicoDocumentId == id);
            }
            var full = request.source_document_number!.Trim();
            return await query.FirstOrDefaultAsync(d => (d.Prefix + d.Number) == full);
        }

        private static List<string> Validate(ApiSupportRequest r, ApiSupportAdjustmentRequest? a, bool isAdjustment)
        {
            var p = new List<string>();
            if (string.IsNullOrWhiteSpace(r.number)) p.Add("number: es obligatorio; es tu consecutivo sin prefijo (por ejemplo \"1001\").");
            else if (!long.TryParse(r.number.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n < 1)
                p.Add($"number: \"{r.number}\" no es válido; debe ser un entero positivo sin prefijo ni separadores.");
            if (!string.IsNullOrWhiteSpace(r.issue_date) && SimpleInvoiceFactory.ParseDate(r.issue_date) == null)
                p.Add($"issue_date: \"{r.issue_date}\" no es una fecha válida; usa dd/MM/yyyy.");

            if (!isAdjustment)
            {
                if (r.numbering == null) p.Add("numbering: es obligatorio e indica el prefijo (y el número) de la resolución de documento soporte.");
                if (!string.IsNullOrWhiteSpace(r.payment_date) && SimpleInvoiceFactory.ParseDate(r.payment_date) == null)
                    p.Add($"payment_date: \"{r.payment_date}\" no es una fecha válida; usa dd/MM/yyyy.");
                var type = (r.payment_means_type ?? string.Empty).Trim().ToUpperInvariant();
                if (type != "DEBITO" && type != "CREDITO") p.Add("payment_means_type: debe ser DEBITO (contado) o CREDITO.");
                else if (type == "DEBITO" && string.IsNullOrWhiteSpace(r.payment_means)) p.Add("payment_means: es obligatorio cuando payment_means_type es DEBITO (por ejemplo CASH para efectivo).");
                else if (type == "CREDITO" && SimpleInvoiceFactory.ParseDate(r.payment_date) == null) p.Add("payment_date: es obligatorio cuando payment_means_type es CREDITO; es la fecha de vencimiento.");

                var c = r.customer;
                if (c == null) p.Add("customer: es obligatorio; es el vendedor (no obligado a facturar).");
                else
                {
                    if (string.IsNullOrWhiteSpace(c.party_identification_type)) p.Add("customer.party_identification_type: es obligatorio (por ejemplo CC o NIT).");
                    if (string.IsNullOrWhiteSpace(c.party_identification)) p.Add("customer.party_identification: es obligatorio.");
                    if (string.IsNullOrWhiteSpace(c.company_name) && string.IsNullOrWhiteSpace(c.first_name) && string.IsNullOrWhiteSpace(c.family_name))
                        p.Add("customer: falta el nombre; envía company_name, o first_name y family_name.");
                }
                if (r.items == null || r.items.Count == 0) p.Add("items: debe traer al menos una línea.");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(a!.source_document_id) && string.IsNullOrWhiteSpace(a.source_document_number))
                    p.Add("source_document_id o source_document_number: es obligatorio indicar el documento soporte que la nota ajusta (por ejemplo \"DSE5\").");
                if (string.IsNullOrWhiteSpace(a.reason)) p.Add("reason: es obligatorio (DEVOLUCION_PARCIAL, ANULACION, REBAJA, AJUSTE_DE_PRECIO u OTROS).");
            }

            if (r.items is { Count: > 0 })
            {
                SimpleInvoiceFactory.ValidateItemLines(r.items, "items", p);
                for (var i = 0; i < r.items.Count; i++)
                    if ((r.items[i].taxes ?? new List<DataicoTax>()).Any(t => t.tax_rate is > 0))
                        p.Add($"items[{i}].taxes: el documento soporte no lleva IVA ni otros impuestos; envía solo la base (price).");
            }
            return p;
        }

        // La línea del documento se copia con identificadores nuevos: la nota es un documento aparte.
        private DocumentItem CloneItem(DocumentItem source)
        {
            var item = new DocumentItem();
            _db.Entry(item).CurrentValues.SetValues(source);
            item.Id = Guid.NewGuid();
            foreach (var retention in source.Retentions)
            {
                var copy = new DocumentRetention();
                _db.Entry(copy).CurrentValues.SetValues(retention);
                copy.Id = Guid.NewGuid();
                copy.DocumentItemId = item.Id;
                item.Retentions.Add(copy);
            }
            foreach (var tax in source.AdditionalTaxes)
            {
                var copy = new DocumentItemTax();
                _db.Entry(copy).CurrentValues.SetValues(tax);
                copy.Id = Guid.NewGuid();
                copy.DocumentItemId = item.Id;
                item.AdditionalTaxes.Add(copy);
            }
            return item;
        }

        private static InvoiceBuildResult Fail(int status, string code, string message, IReadOnlyList<string>? details = null) =>
            new() { Error = new ApiError(status, code, message, details) };
    }
}
