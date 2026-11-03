using Fel.Api.Integration.Services;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Dataico.Models;
using Fel.Infrastructure.Dian;
using Fel.Infrastructure.Services;
using Fel.Infrastructure.Ubl;
using Microsoft.EntityFrameworkCore;

namespace Fel.Tests
{
    // De punta a punta, sin red: el JSON de la API pasa por la fábrica (el Document del portal), por el mapeo del motor nativo y por el generador UBL,
    // y el XML resultante se valida contra los XSD oficiales de la DIAN. Cubre los tipos de factura que un integrador envía por /invoices.
    public class ApiInvoiceKindsXmlTests
    {
        private static readonly Guid BranchId = Guid.NewGuid();

        private sealed class Env
        {
            public FelDbContext Db { get; }
            public Client Client { get; }
            public SimpleInvoiceFactory Factory { get; }

            public Env()
            {
                Db = new FelDbContext(new DbContextOptionsBuilder<FelDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
                Client = new Client
                {
                    Id = Guid.NewGuid(), TaxId = "900303194", CompanyName = "SOFACTORY SAS", Address = "Cra 17 # 63A-26", City = "Bogotá, D.C.", CityCode = "11001",
                    Email = "sofactorysas@gmail.com", Phone = "3000000000", PersonType = "PJ", TaxRegime = "48", MercantileRegistration = "10181", ForeignCurrencyEnabled = true
                };
                Db.Clients.Add(Client);
                var resolution = new Resolution
                {
                    Id = Guid.NewGuid(), ClientId = Client.Id, ResolutionNumber = "18760000001", Prefix = "SETT", DocumentType = "FE", IsActive = true,
                    NumberStart = 1, NumberEnd = 5000, ValidFrom = DateTime.Today.AddYears(-1), ValidTo = DateTime.Today.AddYears(1), TechnicalKey = "fc8eac422eba16e22ffd8c6f94b3f40a6e38162c"
                };
                Db.Resolutions.Add(resolution);
                Db.ResolutionBranches.Add(new ResolutionBranch { ResolutionId = resolution.Id, BranchId = BranchId });
                Db.DocumentTypes.AddRange(
                    new DocumentType { Id = Guid.NewGuid(), Code = "FE-STD", Name = "Factura", IsActive = true, DianCode = "01", OperationType = "10" },
                    new DocumentType { Id = Guid.NewGuid(), Code = "FE-EXP", Name = "Exportación", IsActive = true, DianCode = "02", OperationType = "10" },
                    new DocumentType { Id = Guid.NewGuid(), Code = "FE-MANDATO", Name = "Mandato", IsActive = true, DianCode = "01", OperationType = "11" });
                Db.IdentificationTypes.AddRange(
                    new IdentificationType { Id = Guid.NewGuid(), Code = "31", Name = "NIT", DataicoCode = "NIT", IsActive = true },
                    new IdentificationType { Id = Guid.NewGuid(), Code = "13", Name = "Cédula", DataicoCode = "CC", IsActive = true });
                Db.TaxCatalogItems.AddRange(
                    new TaxCatalogItem { Id = Guid.NewGuid(), Kind = TaxCatalogKind.PaymentMeans, Category = "CASH", Name = "Efectivo", DianCode = "10", IsActive = true },
                    new TaxCatalogItem { Id = Guid.NewGuid(), Kind = TaxCatalogKind.Currency, Category = "USD", Name = "Dólar", IsActive = true },
                    new TaxCatalogItem { Id = Guid.NewGuid(), Kind = TaxCatalogKind.Incoterm, Category = "FOB", Name = "Libre a bordo", IsActive = true });
                Db.UnitsOfMeasure.Add(new UnitOfMeasure { Id = Guid.NewGuid(), DianCode = "94", Abbreviation = "EA", Name = "Unidad", IsActive = true });
                Db.DianMunicipalities.Add(new DianMunicipality { Code = "11001", Name = "Bogotá, D.C.", DepartmentCode = "11", DepartmentName = "Bogotá D.C." });
                Db.SaveChanges();
                Factory = new SimpleInvoiceFactory(Db);
            }

            public async Task<string> XmlAsync(DataicoInvoiceRequest request)
            {
                var result = await Factory.BuildAsync(request, Client, BranchId);
                Assert.True(result.Error == null, result.Error == null ? "" : result.Error.Code + ": " + result.Error.Message + " " + string.Join("; ", result.Error.Details ?? Array.Empty<string>()));
                var document = result.Document!;
                var documentType = Db.DocumentTypes.First(t => t.Code == document.TypeCode);
                var resolution = Db.Resolutions.First(r => r.Id == document.ResolutionId);
                var municipalities = Db.DianMunicipalities.ToDictionary(m => m.Code);
                var paymentMeans = Db.TaxCatalogItems.Where(t => t.Kind == TaxCatalogKind.PaymentMeans).ToDictionary(t => t.Category, t => t.DianCode!);
                var data = DianDocumentMapper.BuildInvoiceData(document, document.Customer!, document.Items, resolution, Client, EmitterLocation.For(Client, null), municipalities, paymentMeans, documentType);
                return new UblGenerator(new XmlSchemaConformanceTests.FakeCrypto()).GenerateInvoiceXml(data);
            }
        }

        private static DataicoInvoiceRequest Request(Action<DataicoInvoiceRequest>? tweak = null)
        {
            var request = new DataicoInvoiceRequest
            {
                number = "1001", issue_date = "01/11/2026", payment_means_type = "DEBITO", payment_means = "EFECTIVO",
                numbering = new DataicoNumbering { prefix = "SETT", resolution_number = "18760000001" },
                customer = new DataicoParty { party_identification_type = "NIT", party_identification = "900123456", company_name = "ACME SAS", party_type = "1" },
                items =
                {
                    new DataicoInvoiceItem { sku = "A1", description = "Producto", quantity = 2, price = 10000, taxes = { new DataicoTax { tax_category = "IVA", tax_rate = 19 } } }
                }
            };
            tweak?.Invoke(request);
            return request;
        }

        private static void AssertConforms(string xml)
        {
            var folder = XmlSchemaConformanceTests.ToolboxXsdFolder();
            if (folder == null) return;
            var errors = XmlSchemaConformanceTests.Validate(xml, XmlSchemaConformanceTests.LoadSchemas(folder));
            Assert.True(errors.Count == 0, $"{errors.Count} errores contra el XSD de la DIAN:\n" + string.Join("\n", errors.Take(20)));
        }

        [Fact]
        public async Task A_standard_invoice_conforms()
        {
            var xml = await new Env().XmlAsync(Request());
            Assert.Contains(">01<", xml);
            AssertConforms(xml);
        }

        [Fact]
        public async Task A_foreign_currency_invoice_carries_the_currency_and_the_trm()
        {
            var xml = await new Env().XmlAsync(Request(r => { r.currency = "USD"; r.currency_exchange_rate = 4000m; r.currency_exchange_rate_date = "01/11/2026"; }));
            Assert.Contains("currencyID=\"USD\"", xml);
            Assert.Contains("PaymentExchangeRate", xml);
            Assert.Contains("4000", xml);
            AssertConforms(xml);
        }

        [Fact]
        public async Task A_mandate_invoice_declares_the_mandante_on_every_line()
        {
            var xml = await new Env().XmlAsync(Request(r =>
            {
                r.operation = "MANDATO";
                r.items[0].mandante_identification = "800111222";
                r.items[0].mandante_identification_type = "NIT";
                r.notes = new List<string> { "Primera observación", "Segunda observación" };
            }));
            Assert.Contains("800111222", xml);
            AssertConforms(xml);
        }

        [Fact]
        public async Task An_export_invoice_uses_the_export_type_code()
        {
            var xml = await new Env().XmlAsync(Request(r => { r.invoice_type_code = "FACTURA_EXPORTACION"; r.currency = "USD"; r.currency_exchange_rate = 4000m; r.currency_exchange_rate_date = "01/11/2026"; }));
            Assert.Contains(">02<", xml);
            AssertConforms(xml);
        }

        // La condición de entrega, la marca, el modelo y la subpartida son opcionales (una exportación de servicios no los lleva): sin ellos el XML es válido.
        [Fact]
        public async Task An_export_invoice_without_incoterm_still_conforms()
        {
            var xml = await new Env().XmlAsync(Request(r => r.invoice_type_code = "FACTURA_EXPORTACION"));
            Assert.DoesNotContain("DeliveryTerms", xml);
            AssertConforms(xml);
        }
    }
}
