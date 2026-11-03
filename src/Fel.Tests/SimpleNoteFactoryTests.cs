using Fel.Api.Integration.Services;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Dataico.Models;
using Microsoft.EntityFrameworkCore;

namespace Fel.Tests
{
    // La fábrica de notas crédito y débito sobre una factura aprobada, contra una base en memoria.
    public class SimpleNoteFactoryTests
    {
        private static readonly Guid BranchId = Guid.NewGuid();

        private sealed class Fixture
        {
            public FelDbContext Db { get; }
            public Client Client { get; }
            public Document Invoice { get; }
            public SimpleNoteFactory Factory { get; }

            public Fixture(string invoiceStatus = "APPROVED")
            {
                var options = new DbContextOptionsBuilder<FelDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
                Db = new FelDbContext(options);
                Client = new Client { Id = Guid.NewGuid(), TaxId = "900123456" };
                Db.Clients.Add(Client);
                var resolution = new Resolution
                {
                    Id = Guid.NewGuid(), ClientId = Client.Id, ResolutionNumber = "18760000001", Prefix = "SETT", DocumentType = "FE", IsActive = true,
                    NumberStart = 1, NumberEnd = 5000, ValidFrom = DateTime.Today.AddYears(-1), ValidTo = DateTime.Today.AddYears(1)
                };
                Db.Resolutions.Add(resolution);
                Db.ResolutionBranches.Add(new ResolutionBranch { ResolutionId = resolution.Id, BranchId = BranchId });
                Db.DocumentTypes.Add(new DocumentType { Id = Guid.NewGuid(), Code = "FE-STD", Name = "Factura", IsActive = true });
                Db.DocumentTypes.Add(new DocumentType { Id = Guid.NewGuid(), Code = "NC", Name = "Nota Crédito", IsActive = true });
                Db.DocumentTypes.Add(new DocumentType { Id = Guid.NewGuid(), Code = "ND", Name = "Nota Débito", IsActive = true });
                Db.IdentificationTypes.Add(new IdentificationType { Id = Guid.NewGuid(), Code = "31", Name = "NIT", DataicoCode = "NIT", IsActive = true });
                Db.UnitsOfMeasure.Add(new UnitOfMeasure { Id = Guid.NewGuid(), DianCode = "94", Abbreviation = "EA", Name = "Unidad", IsActive = true });
                Db.TaxCatalogItems.Add(new TaxCatalogItem { Id = Guid.NewGuid(), Kind = TaxCatalogKind.CreditNoteReason, Category = "1", Name = "Devolución parcial", DianCode = "DEVOLUCION", IsActive = true });
                Db.TaxCatalogItems.Add(new TaxCatalogItem { Id = Guid.NewGuid(), Kind = TaxCatalogKind.CreditNoteReason, Category = "2", Name = "Anulación de factura electrónica", DianCode = "ANULACION", IsActive = true });
                Db.TaxCatalogItems.Add(new TaxCatalogItem { Id = Guid.NewGuid(), Kind = TaxCatalogKind.CreditNoteReason, Category = "3", Name = "Rebaja o descuento", DianCode = "OTROS", IsActive = true });
                Db.TaxCatalogItems.Add(new TaxCatalogItem { Id = Guid.NewGuid(), Kind = TaxCatalogKind.DebitNoteReason, Category = "1", Name = "Intereses", DianCode = "OTROS", IsActive = true });
                Db.TaxCatalogItems.Add(new TaxCatalogItem { Id = Guid.NewGuid(), Kind = TaxCatalogKind.PaymentMeans, Category = "CASH", Name = "Efectivo", DianCode = "10", IsActive = true });
                Db.SaveChanges();

                var invoiceFactory = new SimpleInvoiceFactory(Db);
                var request = new DataicoInvoiceRequest
                {
                    number = "1001", issue_date = "01/11/2026", payment_means_type = "DEBITO", payment_means = "CASH",
                    numbering = new DataicoNumbering { prefix = "SETT" },
                    customer = new DataicoParty { party_identification_type = "NIT", party_identification = "900123456", company_name = "ACME SAS" },
                    items = { new DataicoInvoiceItem { sku = "A1", description = "Producto", quantity = 2, price = 10000, taxes = { new DataicoTax { tax_category = "IVA", tax_rate = 19 } } } }
                };
                Invoice = invoiceFactory.BuildAsync(request, Client, BranchId).GetAwaiter().GetResult().Document!;
                Invoice.Status = invoiceStatus;
                Db.Documents.Add(Invoice);
                Db.SaveChanges();

                Factory = new SimpleNoteFactory(Db, invoiceFactory);
            }
        }

        private static ApiNoteRequest Request(string number = "1", Action<ApiNoteRequest>? tweak = null)
        {
            var request = new ApiNoteRequest { invoice_number = "SETT1001", number = number, reason = "ANULACION" };
            tweak?.Invoke(request);
            return request;
        }

        [Fact]
        public async Task A_credit_note_without_items_copies_the_invoice_lines_and_totals()
        {
            var f = new Fixture();
            var result = await f.Factory.BuildAsync(Request(), "NC", f.Client, BranchId);

            Assert.Null(result.Error);
            var note = result.Document!;
            Assert.Equal("NC", note.TypeCode);
            Assert.Equal(f.Invoice.Id, note.ReferenceDocumentId);
            Assert.Equal(f.Invoice.CustomerId, note.CustomerId);
            Assert.Equal(f.Invoice.ResolutionId, note.ResolutionId);
            Assert.Equal("CASH", note.PaymentMeans);
            Assert.Equal("2", note.DiscrepancyResponseCode);
            Assert.Equal(23800m, note.TotalAmount);
            var item = Assert.Single(note.Items);
            Assert.NotEqual(f.Invoice.Items.First().Id, item.Id);
            Assert.Equal(note.Id, item.DocumentId);
        }

        [Fact]
        public async Task A_partial_credit_note_calculates_its_own_totals()
        {
            var f = new Fixture();
            var result = await f.Factory.BuildAsync(Request(tweak: r =>
            {
                r.reason = "DEVOLUCION";
                r.items = new List<DataicoInvoiceItem> { new() { description = "Producto", quantity = 1, price = 10000, taxes = { new DataicoTax { tax_category = "IVA", tax_rate = 19 } } } };
            }), "NC", f.Client, BranchId);

            Assert.Null(result.Error);
            Assert.Equal(11900m, result.Document!.TotalAmount);
            Assert.Equal("1", result.Document.DiscrepancyResponseCode);
        }

        [Fact]
        public async Task Finds_the_invoice_by_id()
        {
            var f = new Fixture();
            var result = await f.Factory.BuildAsync(Request(tweak: r => { r.invoice_number = null; r.invoice_id = f.Invoice.Id.ToString(); }), "NC", f.Client, BranchId);

            Assert.Null(result.Error);
            Assert.Equal(f.Invoice.Id, result.Document!.ReferenceDocumentId);
        }

        [Fact]
        public async Task Rejects_an_unknown_invoice_with_404()
        {
            var f = new Fixture();
            var result = await f.Factory.BuildAsync(Request(tweak: r => r.invoice_number = "SETT9999"), "NC", f.Client, BranchId);

            Assert.Equal(404, result.Error!.Status);
            Assert.Equal("INVOICE_NOT_FOUND", result.Error.Code);
        }

        [Fact]
        public async Task Does_not_find_an_invoice_of_another_issuer()
        {
            var f = new Fixture();
            var other = new Client { Id = Guid.NewGuid(), TaxId = "800000001" };
            var result = await f.Factory.BuildAsync(Request(), "NC", other, BranchId);

            Assert.Equal("INVOICE_NOT_FOUND", result.Error!.Code);
        }

        [Fact]
        public async Task Rejects_a_note_on_an_invoice_that_is_not_approved()
        {
            var f = new Fixture(invoiceStatus: "REJECTED");
            var result = await f.Factory.BuildAsync(Request(), "NC", f.Client, BranchId);

            Assert.Equal("INVOICE_NOT_APPROVED", result.Error!.Code);
        }

        [Fact]
        public async Task Rejects_a_credit_note_that_exceeds_what_is_left_to_credit()
        {
            var f = new Fixture();
            var first = await f.Factory.BuildAsync(Request("1"), "NC", f.Client, BranchId);
            first.Document!.Status = "APPROVED";
            f.Db.Documents.Add(first.Document);
            await f.Db.SaveChangesAsync();

            var second = await f.Factory.BuildAsync(Request("2"), "NC", f.Client, BranchId);

            Assert.Equal("NOTE_EXCEEDS_INVOICE", second.Error!.Code);
        }

        [Fact]
        public async Task Returns_the_existing_note_when_the_number_was_already_issued()
        {
            var f = new Fixture();
            var first = await f.Factory.BuildAsync(Request("7"), "NC", f.Client, BranchId);
            first.Document!.Status = "APPROVED";
            f.Db.Documents.Add(first.Document);
            await f.Db.SaveChangesAsync();

            var again = await f.Factory.BuildAsync(Request("7"), "NC", f.Client, BranchId);

            Assert.NotNull(again.ExistingLocked);
            Assert.Equal(first.Document.Id, again.ExistingLocked!.Id);
        }

        [Fact]
        public async Task A_debit_note_needs_items()
        {
            var f = new Fixture();
            var result = await f.Factory.BuildAsync(Request(tweak: r => r.reason = "INTERESES"), "ND", f.Client, BranchId);

            Assert.Equal("VALIDATION_ERROR", result.Error!.Code);
            Assert.Contains("items", string.Join("|", result.Error.Details!));
        }

        [Fact]
        public async Task A_debit_note_with_items_is_built_on_the_invoice()
        {
            var f = new Fixture();
            var result = await f.Factory.BuildAsync(Request(tweak: r =>
            {
                r.reason = "INTERESES";
                r.items = new List<DataicoInvoiceItem> { new() { description = "Intereses de mora", quantity = 1, price = 5000 } };
            }), "ND", f.Client, BranchId);

            Assert.Null(result.Error);
            Assert.Equal("ND", result.Document!.TypeCode);
            Assert.Equal("1", result.Document.DiscrepancyResponseCode);
            Assert.Equal(f.Invoice.Id, result.Document.ReferenceDocumentId);
        }

        [Fact]
        public async Task Rejects_an_unknown_reason_and_lists_the_valid_ones()
        {
            var f = new Fixture();
            var result = await f.Factory.BuildAsync(Request(tweak: r => r.reason = "CAPRICHO"), "NC", f.Client, BranchId);

            Assert.Equal("VALIDATION_ERROR", result.Error!.Code);
            Assert.Contains("DEVOLUCION", string.Join("|", result.Error.Details!));
        }

        [Fact]
        public async Task Without_an_invoice_reference_it_asks_for_the_customer_the_items_the_number_and_the_reason()
        {
            var f = new Fixture();
            var result = await f.Factory.BuildAsync(new ApiNoteRequest(), "NC", f.Client, BranchId);

            Assert.Equal("VALIDATION_ERROR", result.Error!.Code);
            var details = string.Join("|", result.Error.Details!);
            Assert.Contains("customer:", details);
            Assert.Contains("items:", details);
            Assert.Contains("number:", details);
            Assert.Contains("reason:", details);
        }

        private static ApiNoteRequest Unreferenced(Action<ApiNoteRequest>? tweak = null)
        {
            var request = new ApiNoteRequest
            {
                number = "7", reason = "DEVOLUCION",
                customer = new DataicoParty { party_identification_type = "NIT", party_identification = "900999888", company_name = "OTRA SAS" },
                items = new List<DataicoInvoiceItem> { new DataicoInvoiceItem { sku = "A1", description = "Producto devuelto", quantity = 1, price = 50000, taxes = { new DataicoTax { tax_category = "IVA", tax_rate = 19 } } } }
            };
            tweak?.Invoke(request);
            return request;
        }

        [Fact]
        public async Task An_unreferenced_credit_note_uses_its_own_customer_items_and_the_invoice_resolution()
        {
            var f = new Fixture();
            var result = await f.Factory.BuildAsync(Unreferenced(), "NC", f.Client, BranchId);

            Assert.Null(result.Error);
            var note = result.Document!;
            Assert.Equal("NC", note.TypeCode);
            Assert.Null(note.ReferenceDocumentId);
            Assert.Equal("900999888", note.Customer!.IdentificationNumber);
            Assert.Equal(PartyType.Cliente, note.Customer.PartyType);
            Assert.Equal(f.Invoice.ResolutionId, note.ResolutionId);
            Assert.Equal("SETT", note.Prefix);
            Assert.Equal("7", note.Number);
            Assert.Equal("1", note.DiscrepancyResponseCode);
            Assert.Equal(50000m, note.Subtotal);
            Assert.Equal(59500m, note.TotalAmount);
            Assert.Equal("DEBITO", note.PaymentMeansType);
            Assert.Equal("CASH", note.PaymentMeans);
        }

        [Fact]
        public async Task An_unreferenced_credit_note_is_not_limited_by_the_invoices_total()
        {
            var f = new Fixture();
            var result = await f.Factory.BuildAsync(Unreferenced(r => r.items![0].price = 9_000_000), "NC", f.Client, BranchId);

            Assert.Null(result.Error);
        }

        [Fact]
        public async Task An_unreferenced_credit_note_needs_a_current_invoice_resolution()
        {
            var f = new Fixture();
            foreach (var r in f.Db.Resolutions) r.ValidTo = DateTime.Today.AddDays(-1);
            await f.Db.SaveChangesAsync();

            var result = await f.Factory.BuildAsync(Unreferenced(), "NC", f.Client, BranchId);

            Assert.Equal("RESOLUTION_NOT_FOUND", result.Error!.Code);
        }

        [Fact]
        public async Task An_unreferenced_debit_note_is_not_available_yet()
        {
            var f = new Fixture();
            var result = await f.Factory.BuildAsync(Unreferenced(r => r.reason = "INTERESES"), "ND", f.Client, BranchId);

            Assert.Equal("UNREFERENCED_DEBIT_NOT_AVAILABLE", result.Error!.Code);
        }

        [Fact]
        public async Task Retrying_an_unreferenced_credit_note_reuses_the_same_document()
        {
            var f = new Fixture();
            var first = await f.Factory.BuildAsync(Unreferenced(), "NC", f.Client, BranchId);
            first.Document!.Status = "REJECTED";
            f.Db.Documents.Add(first.Document);
            await f.Db.SaveChangesAsync();

            var retry = await f.Factory.BuildAsync(Unreferenced(r => r.items![0].price = 60000), "NC", f.Client, BranchId);

            Assert.Null(retry.Error);
            Assert.True(retry.Reused);
            Assert.Equal(first.Document.Id, retry.Document!.Id);
        }
    }
}
