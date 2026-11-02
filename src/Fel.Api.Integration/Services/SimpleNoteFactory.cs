using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Models;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Dataico.Models;
using Fel.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Integration.Services
{
    // Cuerpo de una nota crédito o débito por la API. Los nombres siguen los de Dataico; la factura se indica con invoice_id (el id que devolvió
    // esta API, o el id de Dataico del documento) o con invoice_number (prefijo + número, por ejemplo "SFRY1005").
    public class ApiNoteRequest
    {
        [JsonPropertyName("invoice_id")] public string? invoice_id { get; set; }
        [JsonPropertyName("invoice_number")] public string? invoice_number { get; set; }
        public string? number { get; set; }
        public string? issue_date { get; set; }
        public DataicoNumbering? numbering { get; set; }
        // Nota crédito: DEVOLUCION | ANULACION | OTROS (o el código DIAN 1 a 6). Nota débito: INTERESES | GASTOS_POR_COBRAR | CAMBIO_DEL_VALOR | OTROS (o 1 a 4).
        public string? reason { get; set; }
        public string? reason_description { get; set; }
        public List<string>? notes { get; set; }
        // Sin ítems, la nota crédito es total: copia las líneas de la factura. La nota débito siempre los necesita.
        public List<DataicoInvoiceItem>? items { get; set; }
    }

    // Arma la nota crédito o débito (Document NC / ND) que luego emite el mismo motor del portal. El cliente, la resolución y el medio de pago salen de la
    // factura; el consecutivo lo trae el integrador y solo se exige que no esté usado con ese prefijo.
    public class SimpleNoteFactory
    {
        public const string CreditNote = "NC";
        public const string DebitNote = "ND";

        private static readonly Dictionary<string, string> CreditReasons = new(StringComparer.OrdinalIgnoreCase)
        {
            ["DEVOLUCION"] = "1", ["ANULACION"] = "2", ["OTROS"] = "3"
        };

        private static readonly Dictionary<string, string> DebitReasons = new(StringComparer.OrdinalIgnoreCase)
        {
            ["INTERESES"] = "1", ["GASTOS_POR_COBRAR"] = "2", ["CAMBIO_DEL_VALOR"] = "3", ["OTROS"] = "4"
        };

        private readonly FelDbContext _db;
        private readonly SimpleInvoiceFactory _invoiceFactory;

        public SimpleNoteFactory(FelDbContext db, SimpleInvoiceFactory invoiceFactory)
        {
            _db = db;
            _invoiceFactory = invoiceFactory;
        }

        public async Task<InvoiceBuildResult> BuildAsync(ApiNoteRequest request, string typeCode, Client client, Guid branchId)
        {
            var isCredit = typeCode == CreditNote;
            var problems = Validate(request, isCredit);
            if (problems.Count > 0)
                return Fail(400, "VALIDATION_ERROR", "La nota tiene datos inválidos o faltantes. Revisa el detalle de cada campo.", problems);

            var documentType = await _db.DocumentTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Code == typeCode && t.IsActive);
            if (documentType == null) return Fail(422, "DOCUMENT_TYPE_UNAVAILABLE", $"El tipo de documento {typeCode} no está disponible.");

            var original = await FindInvoiceAsync(request, client.Id);
            if (original == null)
                return Fail(404, "INVOICE_NOT_FOUND", "No se encontró la factura que la nota referencia. Envía invoice_id (el id que devolvió la API) o invoice_number (prefijo y número, por ejemplo SFRY1005); debe ser una factura de este emisor.");
            if (original.Status != "APPROVED")
                return Fail(422, "INVOICE_NOT_APPROVED", $"La factura {original.Prefix}{original.Number} está en estado {original.Status}: solo se pueden emitir notas sobre facturas aprobadas.");
            if (!original.ResolutionId.HasValue)
                return Fail(422, "INVOICE_WITHOUT_RESOLUTION", "La factura referenciada no tiene resolución asociada.");

            var reasonResult = await ResolveReasonAsync(request.reason!, isCredit);
            if (reasonResult.Error != null) return new InvoiceBuildResult { Error = reasonResult.Error };

            var number = long.Parse(request.number!.Trim(), CultureInfo.InvariantCulture);
            var numberText = number.ToString(CultureInfo.InvariantCulture);
            var prefix = (request.numbering?.prefix ?? string.Empty).Trim();
            if (prefix.Length == 0)
                prefix = await ResolutionNumbering.GetNotePrefixAsync(_db, client.Id, branchId, isCredit ? NoteKind.CreditNote : NoteKind.DebitNote, original.ResolutionId)
                    ?? original.Resolution?.Prefix ?? string.Empty;

            var existing = await _db.Documents
                .Include(d => d.Items).ThenInclude(i => i.Retentions)
                .Include(d => d.Items).ThenInclude(i => i.AdditionalTaxes)
                .Include(d => d.Observations)
                .FirstOrDefaultAsync(d => d.ClientId == client.Id && d.TypeCode == typeCode && d.Prefix == prefix && d.Number == numberText && d.ReferenceDocumentId != null);
            if (existing != null && !InvoicePublishingService.IsEditable(existing.Status))
                return new InvoiceBuildResult { ExistingLocked = existing };

            var warnings = new List<string>();
            List<DocumentItem> items;
            if (request.items is { Count: > 0 })
            {
                var built = await _invoiceFactory.BuildItemsAsync(request.items, false, warnings);
                if (built.Error != null) return new InvoiceBuildResult { Error = built.Error };
                items = built.Items;
            }
            else
            {
                items = original.Items.OrderBy(i => i.LineNumber).Select(CloneItem).ToList();
            }

            var document = existing ?? new Document { Id = Guid.NewGuid(), ClientId = client.Id, CreatedAt = DateTime.UtcNow };
            if (existing != null)
            {
                _db.DocumentItems.RemoveRange(existing.Items);
                existing.Items.Clear();
                _db.DocumentObservations.RemoveRange(existing.Observations);
                existing.Observations.Clear();
            }

            document.TrackingId = document.Id.ToString();
            document.BranchId = branchId;
            document.Status = "DRAFT";
            document.TypeCode = typeCode;
            document.DocumentTypeId = documentType.Id;
            document.Prefix = prefix;
            document.Number = numberText;
            document.ResolutionId = original.ResolutionId;
            document.CustomerId = original.CustomerId;
            document.Customer = original.Customer;
            document.IssueDate = ParseDate(request.issue_date) ?? ColombiaTime.Now;
            document.PaymentMeansType = original.PaymentMeansType;
            document.PaymentMeans = original.PaymentMeans;
            document.PaymentTermDays = original.PaymentTermDays;
            document.Currency = original.Currency;
            document.ExchangeRate = original.ExchangeRate;
            document.ExchangeRateDate = original.ExchangeRateDate;
            document.ReferenceDocumentId = original.Id;
            document.DiscrepancyResponseCode = reasonResult.Code;
            document.ReferenceConcept = string.IsNullOrWhiteSpace(request.reason_description) ? reasonResult.Name : request.reason_description.Trim();
            document.Notes = string.Join(" ", (request.notes ?? new List<string>()).Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()));

            var line = 0;
            foreach (var item in items)
            {
                item.DocumentId = document.Id;
                item.LineNumber = line++;
                document.Items.Add(item);
            }
            DocumentTotalsCalculator.Apply(document, document.Items);

            if (isCredit)
            {
                var credited = await _db.Documents.AsNoTracking()
                    .Where(d => d.ReferenceDocumentId == original.Id && d.TypeCode == CreditNote && d.Status == "APPROVED" && d.Id != document.Id)
                    .SumAsync(d => (decimal?)d.TotalAmount) ?? 0m;
                if (credited + document.TotalAmount > original.TotalAmount + 0.01m)
                    return Fail(422, "NOTE_EXCEEDS_INVOICE",
                        $"La nota por {document.TotalAmount:0.##} supera lo que queda por acreditar de la factura {original.Prefix}{original.Number}: total {original.TotalAmount:0.##}, ya acreditado {credited:0.##}.");
            }

            if (existing != null)
            {
                foreach (var item in document.Items)
                {
                    _db.Entry(item).State = EntityState.Added;
                    foreach (var retention in item.Retentions) _db.Entry(retention).State = EntityState.Added;
                    foreach (var tax in item.AdditionalTaxes) _db.Entry(tax).State = EntityState.Added;
                }
            }

            var result = new InvoiceBuildResult { Document = document, Reused = existing != null };
            result.Warnings.AddRange(warnings);
            return result;
        }

        private async Task<Document?> FindInvoiceAsync(ApiNoteRequest request, Guid clientId)
        {
            var query = _db.Documents
                .Include(d => d.Customer)
                .Include(d => d.Resolution)
                .Include(d => d.Items).ThenInclude(i => i.Retentions)
                .Include(d => d.Items).ThenInclude(i => i.AdditionalTaxes)
                .Where(d => d.ClientId == clientId && d.ReferenceDocumentId == null && d.TypeCode.StartsWith("FE"));

            if (!string.IsNullOrWhiteSpace(request.invoice_id))
            {
                var id = request.invoice_id.Trim();
                if (Guid.TryParse(id, out var guid))
                    return await query.FirstOrDefaultAsync(d => d.Id == guid || d.DataicoDocumentId == id);
                return await query.FirstOrDefaultAsync(d => d.DataicoDocumentId == id);
            }

            var full = request.invoice_number!.Trim();
            return await query.FirstOrDefaultAsync(d => (d.Prefix + d.Number) == full);
        }

        // Cada motivo es una fila del catálogo (Category = código DIAN, Name = descripción).
        private async Task<(string? Code, string? Name, ApiError? Error)> ResolveReasonAsync(string reason, bool isCredit)
        {
            var map = isCredit ? CreditReasons : DebitReasons;
            var code = map.TryGetValue(reason.Trim(), out var mapped) ? mapped : reason.Trim();
            var kind = isCredit ? TaxCatalogKind.CreditNoteReason : TaxCatalogKind.DebitNoteReason;
            var entry = await _db.TaxCatalogItems.AsNoTracking().FirstOrDefaultAsync(i => i.Kind == kind && i.IsActive && i.Category == code);
            if (entry != null) return (entry.Category, entry.Name, null);

            var valid = string.Join(", ", map.Select(p => p.Key));
            return (null, null, new ApiError(400, "VALIDATION_ERROR", "La nota tiene datos inválidos o faltantes. Revisa el detalle de cada campo.",
                new List<string> { $"reason: \"{reason}\" no es un motivo válido. Usa uno de: {valid} (o el código DIAN)." }));
        }

        private static List<string> Validate(ApiNoteRequest r, bool isCredit)
        {
            var p = new List<string>();
            if (string.IsNullOrWhiteSpace(r.invoice_id) && string.IsNullOrWhiteSpace(r.invoice_number))
                p.Add("invoice_id o invoice_number: es obligatorio indicar la factura que la nota referencia (por ejemplo invoice_number \"SFRY1005\").");
            if (string.IsNullOrWhiteSpace(r.number) || !long.TryParse(r.number.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n <= 0)
                p.Add("number: es obligatorio y debe ser un entero positivo; es tu consecutivo de la nota sin prefijo.");
            if (string.IsNullOrWhiteSpace(r.reason))
                p.Add(isCredit ? "reason: es obligatorio (DEVOLUCION, ANULACION u OTROS)." : "reason: es obligatorio (INTERESES, GASTOS_POR_COBRAR, CAMBIO_DEL_VALOR u OTROS).");
            if (!string.IsNullOrWhiteSpace(r.issue_date) && ParseDate(r.issue_date) == null)
                p.Add("issue_date: debe tener formato dd/MM/yyyy.");
            if (!isCredit && (r.items == null || r.items.Count == 0))
                p.Add("items: la nota débito necesita al menos un ítem con el valor que ajusta.");
            for (var i = 0; i < (r.items?.Count ?? 0); i++)
            {
                var item = r.items![i];
                if (string.IsNullOrWhiteSpace(item.description)) p.Add($"items[{i}].description: es obligatorio.");
                if (item.quantity <= 0) p.Add($"items[{i}].quantity: debe ser mayor que 0.");
                if (item.price < 0) p.Add($"items[{i}].price: no puede ser negativo.");
            }
            return p;
        }

        // El ítem de la factura se copia con identificadores nuevos: la nota es un documento aparte.
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

        private static DateTime? ParseDate(string? value) =>
            DateTime.TryParseExact(value?.Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

        private static InvoiceBuildResult Fail(int status, string code, string message, IReadOnlyList<string>? details = null) =>
            new() { Error = new ApiError(status, code, message, details) };
    }
}
