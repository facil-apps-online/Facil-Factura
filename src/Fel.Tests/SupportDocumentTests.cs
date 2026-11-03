using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
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
    // Documento soporte y su nota de ajuste: la fábrica de la API, el mapeo de roles invertidos (el Client compra, el tercero vende) y el XML contra el esquema.
    public class SupportDocumentTests
    {
        private static readonly Guid BranchId = Guid.NewGuid();

        private sealed class FakeCrypto : Fel.Core.Interfaces.ICryptoService
        {
            public string Encrypt(string plainText) => plainText;
            public string Decrypt(string cipherText) => cipherText;
            public string GenerateCufeSha384(string dataToHash) =>
                Convert.ToHexString(System.Security.Cryptography.SHA384.HashData(System.Text.Encoding.UTF8.GetBytes(dataToHash))).ToLowerInvariant();
        }

        private sealed class Fixture
        {
            public FelDbContext Db { get; }
            public Client Client { get; }
            public Resolution Resolution { get; }
            public SimpleSupportDocumentFactory Factory { get; }

            public Fixture()
            {
                var options = new DbContextOptionsBuilder<FelDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
                Db = new FelDbContext(options);
                Client = new Client { Id = Guid.NewGuid(), TaxId = "900303194", CompanyName = "SOFACTORY SAS", Address = "Cra 17 # 63A-26", City = "Bogotá", CityCode = "11001", Email = "sofactorysas@gmail.com" };
                Db.Clients.Add(Client);
                Resolution = new Resolution
                {
                    Id = Guid.NewGuid(), ClientId = Client.Id, ResolutionNumber = "18764000000001", Prefix = "DSE", DocumentType = "DS", IsActive = true, TechnicalKey = string.Empty,
                    NumberStart = 1, NumberEnd = 5000, ValidFrom = DateTime.Today.AddYears(-1), ValidTo = DateTime.Today.AddYears(1)
                };
                Db.Resolutions.Add(Resolution);
                Db.ResolutionBranches.Add(new ResolutionBranch { ResolutionId = Resolution.Id, BranchId = BranchId });
                Db.DocumentTypes.Add(new DocumentType { Id = Guid.NewGuid(), Code = "DS", Name = "Documento soporte", IsActive = true, DianCode = "05" });
                Db.DocumentTypes.Add(new DocumentType { Id = Guid.NewGuid(), Code = "DS-AJUSTE", Name = "Ajuste documento soporte", IsActive = true, DianCode = "95" });
                Db.IdentificationTypes.Add(new IdentificationType { Id = Guid.NewGuid(), Code = "13", Name = "Cédula de Ciudadanía", DataicoCode = "CC", IsActive = true });
                Db.UnitsOfMeasure.Add(new UnitOfMeasure { Id = Guid.NewGuid(), DianCode = "94", Abbreviation = "EA", Name = "Unidad", IsActive = true });
                Db.TaxCatalogItems.Add(new TaxCatalogItem { Id = Guid.NewGuid(), Kind = TaxCatalogKind.PaymentMeans, Category = "CASH", Name = "Efectivo", DianCode = "10", IsActive = true });
                Db.DianMunicipalities.Add(new DianMunicipality { Code = "11001", Name = "Bogotá, D.C.", DepartmentCode = "11", DepartmentName = "Bogotá D.C." });
                Db.SaveChanges();
                Factory = new SimpleSupportDocumentFactory(Db, new SimpleInvoiceFactory(Db));
            }
        }

        private static ApiSupportRequest Request(string number = "1", Action<ApiSupportRequest>? tweak = null)
        {
            var request = new ApiSupportRequest
            {
                number = number,
                issue_date = DateTime.Today.ToString("dd/MM/yyyy"),
                payment_means_type = "DEBITO",
                payment_means = "CASH",
                numbering = new DataicoNumbering { prefix = "DSE" },
                customer = new DataicoParty
                {
                    party_identification_type = "CC", party_identification = "16000111", first_name = "José Vicente", family_name = "Zapata Estrada",
                    address_line = "Calle 10 # 5-20", department = "11", city = "001"
                },
                items = { new DataicoInvoiceItem { sku = "SERV", description = "Servicio de transporte", quantity = 2, price = 50000 } }
            };
            tweak?.Invoke(request);
            return request;
        }

        [Fact]
        public async Task Builds_the_support_document_without_taxes()
        {
            var f = new Fixture();
            var result = await f.Factory.BuildAsync(Request(), false, f.Client, BranchId);

            Assert.Null(result.Error);
            var doc = result.Document!;
            Assert.Equal("DS", doc.TypeCode);
            Assert.Equal("DSE", doc.Prefix);
            Assert.Equal("1", doc.Number);
            Assert.Equal(f.Resolution.Id, doc.ResolutionId);
            Assert.Equal(100000m, doc.Subtotal);
            Assert.Equal(0m, doc.TaxAmount);
            Assert.Equal(100000m, doc.TotalAmount);
            Assert.All(doc.Items, i => Assert.Equal(0m, i.TaxRate));
            Assert.Equal("16000111", doc.Customer!.IdentificationNumber);
        }

        [Fact]
        public async Task Rejects_taxes_on_the_lines()
        {
            var f = new Fixture();
            var result = await f.Factory.BuildAsync(
                Request(tweak: r => r.items[0].taxes.Add(new DataicoTax { tax_category = "IVA", tax_rate = 19 })), false, f.Client, BranchId);

            Assert.Equal(400, result.Error!.Status);
            Assert.Contains(result.Error.Details!, d => d.Contains("items[0].taxes"));
        }

        [Fact]
        public async Task Rejects_a_prefix_without_a_support_resolution()
        {
            var f = new Fixture();
            var result = await f.Factory.BuildAsync(Request(tweak: r => r.numbering.prefix = "NOPE"), false, f.Client, BranchId);

            Assert.Equal("RESOLUTION_NOT_FOUND", result.Error!.Code);
        }

        [Fact]
        public async Task The_adjustment_needs_an_approved_support_document_and_copies_its_lines()
        {
            var f = new Fixture();
            var built = await f.Factory.BuildAsync(Request(), false, f.Client, BranchId);
            var original = built.Document!;
            original.Cufe = new string('a', 96);
            f.Db.Documents.Add(original);
            await f.Db.SaveChangesAsync();

            var adjustment = new ApiSupportAdjustmentRequest { number = "1", source_document_number = "DSE1", reason = "ANULACION" };

            original.Status = "DRAFT";
            await f.Db.SaveChangesAsync();
            var notApproved = await f.Factory.BuildAsync(adjustment, true, f.Client, BranchId);
            Assert.Equal("SUPPORT_DOCUMENT_NOT_APPROVED", notApproved.Error!.Code);

            original.Status = "APPROVED";
            await f.Db.SaveChangesAsync();
            var result = await f.Factory.BuildAsync(adjustment, true, f.Client, BranchId);

            Assert.Null(result.Error);
            var note = result.Document!;
            Assert.Equal("DS-AJUSTE", note.TypeCode);
            Assert.Equal(original.Id, note.ReferenceDocumentId);
            Assert.Equal("2", note.DiscrepancyResponseCode);
            Assert.Equal(100000m, note.TotalAmount);
            Assert.NotEqual(original.Items.First().Id, note.Items.First().Id);
        }

        // ---- Mapeo de roles y XML ----

        private static (Document Document, Customer Seller, List<DocumentItem> Items, Resolution Resolution, Client Client, EmitterLocation Location) Domain(string sellerType = "13", string sellerId = "16000111")
        {
            var client = new Client { Id = Guid.NewGuid(), TaxId = "900303194", CompanyName = "SOFACTORY SAS", Address = "Cra 17 # 63A-26", City = "Bogotá", CityCode = "11001", Email = "sofactorysas@gmail.com", SoftwareId = "56f2ae4e-9812-4fad-9255-08fcfcd5ccb0", SoftwarePin = "12345" };
            var resolution = new Resolution
            {
                Id = Guid.NewGuid(), ClientId = client.Id, ResolutionNumber = "18764000000001", Prefix = "DSE", DocumentType = "DS", TechnicalKey = string.Empty,
                NumberStart = 1, NumberEnd = 5000, ValidFrom = new DateTime(2025, 1, 1), ValidTo = new DateTime(2027, 1, 1)
            };
            var seller = new Customer
            {
                Id = Guid.NewGuid(), ClientId = client.Id, IdentificationType = sellerType, IdentificationNumber = sellerId, Name = "José Vicente Zapata Estrada",
                Address = "Calle 10 # 5-20", CityCode = "11001", CityName = "Bogotá, D.C.", Email = "jose@example.com"
            };
            var document = new Document
            {
                Id = Guid.NewGuid(), ClientId = client.Id, TypeCode = "DS", Prefix = "DSE", Number = "7", IssueDate = new DateTime(2026, 11, 2, 10, 30, 0),
                PaymentMeansType = "DEBITO", PaymentMeans = "CASH", Currency = "COP", Status = "DRAFT"
            };
            var items = new List<DocumentItem>
            {
                new() { Id = Guid.NewGuid(), Code = "SERV", Name = "Servicio de transporte", Quantity = 2, UnitPrice = 50000, UnitOfMeasureCode = "94", IvaTreatment = IvaTreatment.Exento, TaxRate = 0, TotalAmount = 100000 }
            };
            var location = new EmitterLocation(client.Address, client.City, client.CityCode, string.Empty, client.Email, null, null);
            return (document, seller, items, resolution, client, location);
        }

        private static readonly Dictionary<string, DianMunicipality> Municipalities = new() { ["11001"] = new DianMunicipality { Code = "11001", Name = "Bogotá, D.C.", DepartmentCode = "11", DepartmentName = "Bogotá D.C." } };
        private static readonly Dictionary<string, string> PaymentCodes = new() { ["CASH"] = "10" };

        [Fact]
        public void The_client_buys_and_the_third_party_sells()
        {
            var d = Domain();
            var data = DianDocumentMapper.BuildSupportDocumentData(d.Document, d.Seller, d.Items, d.Resolution, d.Client, d.Location, Municipalities, PaymentCodes, null, null);

            Assert.Equal("05", data.DianCode);
            Assert.Equal("10", data.OperationType);
            Assert.Equal("16000111", data.Issuer.TaxId);
            Assert.Equal("13", data.Issuer.IdentificationCode);
            Assert.Equal("ZZ", data.Issuer.TaxSchemeId);
            Assert.Equal("900303194", data.Customer.TaxId);
            Assert.Equal("31", data.Customer.IdentificationCode);
            Assert.Equal(100000m, data.PayableAmount);
        }

        [Fact]
        public void The_adjustment_references_the_support_document_by_its_full_number()
        {
            var d = Domain();
            var original = new Document { Id = Guid.NewGuid(), Prefix = "DSE", Number = "5", Cufe = new string('b', 96), IssueDate = new DateTime(2026, 10, 30) };
            d.Document.TypeCode = "DS-AJUSTE";
            d.Document.DiscrepancyResponseCode = "2";
            d.Document.ReferenceConcept = "Anulación";
            var data = DianDocumentMapper.BuildSupportDocumentData(d.Document, d.Seller, d.Items, d.Resolution, d.Client, d.Location, Municipalities, PaymentCodes, null, original);

            Assert.Equal("95", data.DianCode);
            Assert.Equal("DSE5", data.BillingReferenceDocumentNumber);
            Assert.Equal(original.Cufe, data.BillingReferenceCufe);
            Assert.Equal("2", data.DiscrepancyResponseCode);
        }

        [Fact]
        public void The_stored_key_is_the_cuds_that_the_xml_carries()
        {
            var d = Domain();
            var data = DianDocumentMapper.BuildSupportDocumentData(d.Document, d.Seller, d.Items, d.Resolution, d.Client, d.Location, Municipalities, PaymentCodes, null, null);
            var generator = new UblGenerator(new FakeCrypto());

            var key = generator.CalculateDocumentKey(data);
            var xml = generator.GenerateInvoiceXml(data);

            Assert.Equal(generator.CalculateCuds(data), key);
            Assert.Contains($">{key}</cbc:UUID>", xml);
            Assert.Contains("CUDS-SHA384", xml);
        }

        [Fact]
        public void The_seller_group_follows_the_annex_for_a_natural_person()
        {
            var d = Domain();
            var data = DianDocumentMapper.BuildSupportDocumentData(d.Document, d.Seller, d.Items, d.Resolution, d.Client, d.Location, Municipalities, PaymentCodes, null, null);
            var xml = XDocument.Parse(new UblGenerator(new FakeCrypto()).GenerateInvoiceXml(data));
            XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2", cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

            var seller = xml.Descendants(cac + "AccountingSupplierParty").Single();
            Assert.Equal("2", seller.Element(cbc + "AdditionalAccountID")!.Value);
            var companyId = seller.Descendants(cbc + "CompanyID").Single();
            Assert.Equal("13", companyId.Attribute("schemeName")!.Value);
            Assert.Null(companyId.Attribute("schemeID"));
            Assert.Empty(seller.Descendants(cac + "PartyLegalEntity"));
            Assert.Equal("R-99-PN", seller.Descendants(cbc + "TaxLevelCode").Single().Value);

            var buyer = xml.Descendants(cac + "AccountingCustomerParty").Single();
            Assert.Equal("1", buyer.Element(cbc + "AdditionalAccountID")!.Value);
            Assert.Equal("31", buyer.Descendants(cbc + "CompanyID").Single().Attribute("schemeName")!.Value);
            Assert.NotNull(buyer.Descendants(cbc + "CompanyID").Single().Attribute("schemeID"));
            Assert.NotEmpty(xml.Descendants(cac + "PaymentMeans"));
        }

        // ---- El emisor sale del Client ----

        [Fact]
        public void The_issuer_comes_from_the_client_parameters()
        {
            var d = Domain();
            d.Client.PersonType = "PN";
            d.Client.TaxRegime = "49";
            d.Client.IsGranContribuyente = true;
            d.Client.IsAutorretenedorRenta = true;
            d.Client.MercantileRegistration = "  778899 ";

            var issuer = DianDocumentMapper.BuildIssuerData(d.Client, d.Location, Municipalities);

            Assert.Equal("2", issuer.AdditionalAccountId);
            Assert.Equal("ZZ", issuer.TaxSchemeId);
            Assert.Equal("No aplica", issuer.TaxSchemeName);
            Assert.Equal(new[] { "O-13", "O-15" }, issuer.TaxLevelCodes);
            Assert.Equal("11", issuer.DepartmentCode);
        }

        [Fact]
        public void A_responsible_legal_entity_with_the_simple_regime_is_reported_as_such()
        {
            var d = Domain();
            d.Client.PersonType = "PJ";
            d.Client.TaxRegime = "48";
            d.Client.IncomeTaxRegime = "47";

            var issuer = DianDocumentMapper.BuildIssuerData(d.Client, d.Location, Municipalities);

            Assert.Equal("1", issuer.AdditionalAccountId);
            Assert.Equal("01", issuer.TaxSchemeId);
            Assert.Equal(new[] { "O-47" }, issuer.TaxLevelCodes);
        }

        [Fact]
        public void The_mercantile_registration_is_only_written_when_the_client_has_one()
        {
            var d = Domain();
            var generator = new UblGenerator(new FakeCrypto());
            XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2", cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
            Fel.Core.Models.UblInvoiceData Invoice() => DianDocumentMapper.BuildInvoiceData(d.Document, d.Seller, d.Items, d.Resolution, d.Client, d.Location, Municipalities);

            var without = XDocument.Parse(generator.GenerateInvoiceXml(Invoice()));
            Assert.Empty(without.Descendants(cac + "CorporateRegistrationScheme").Elements(cbc + "Name"));

            d.Client.MercantileRegistration = "778899";
            var with = XDocument.Parse(generator.GenerateInvoiceXml(Invoice()));
            Assert.Equal("778899", with.Descendants(cac + "CorporateRegistrationScheme").Elements(cbc + "Name").Single().Value);
        }

        [Fact]
        public void An_issuer_without_a_valid_municipality_is_reported()
        {
            var d = Domain();
            var noCity = d.Location with { CityCode = null };

            Assert.NotNull(DianDocumentMapper.IssuerLocationProblem(noCity, Municipalities));
            Assert.Null(DianDocumentMapper.IssuerLocationProblem(d.Location, Municipalities));
        }

        // ---- Esquema oficial ----

        private static string? ToolboxXsdFolder()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "Caja de Herramientas", "Caja de herramientas FE_V19_(v2026)", "XSD");
                if (Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            return null;
        }

        private static XmlSchemaSet LoadSchemas(string xsdFolder)
        {
            var set = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };
            void Add(string path)
            {
                using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = new XmlUrlResolver() });
                set.Add(null, reader);
            }
            foreach (var file in Directory.GetFiles(Path.Combine(xsdFolder, "common"), "*.xsd")) Add(file);
            foreach (var file in new[] { "maindoc/UBL-Invoice-2.1.xsd", "maindoc/UBL-CreditNote-2.1.xsd", "maindoc/UBL-DebitNote-2.1.xsd", "maindoc/DIAN_UBL_Structures.xsd" })
                Add(Path.Combine(xsdFolder, file));
            set.Compile();
            return set;
        }

        [Theory]
        [InlineData("13", "16000111", false, "Persona natural residente")]
        [InlineData("31", "900123456", false, "Persona jurídica residente")]
        [InlineData("13", "16000111", true, "Ajuste")]
        [InlineData("42", "AB123456", false, "Extranjero no residente")]
        public void The_support_document_xml_conforms_to_the_official_schema(string sellerType, string sellerId, bool adjustment, string label)
        {
            var folder = ToolboxXsdFolder();
            if (folder == null) return;

            var d = Domain(sellerType, sellerId);
            if (sellerType == "42") { d.Seller.CountryCode = "US"; d.Seller.CityCode = string.Empty; d.Seller.CityName = "Miami"; d.Seller.StateName = "Florida"; }
            Document? original = null;
            if (adjustment)
            {
                original = new Document { Id = Guid.NewGuid(), Prefix = "DSE", Number = "5", Cufe = new string('b', 96), IssueDate = new DateTime(2026, 10, 30) };
                d.Document.TypeCode = "DS-AJUSTE";
                d.Document.DiscrepancyResponseCode = "4";
                d.Document.ReferenceConcept = "Ajuste de precio";
            }
            var data = DianDocumentMapper.BuildSupportDocumentData(d.Document, d.Seller, d.Items, d.Resolution, d.Client, d.Location, Municipalities, PaymentCodes, null, original);
            var xml = new UblGenerator(new FakeCrypto()).GenerateInvoiceXml(data);

            var errors = new List<string>();
            var settings = new XmlReaderSettings { ValidationType = ValidationType.Schema, Schemas = LoadSchemas(folder), DtdProcessing = DtdProcessing.Prohibit };
            settings.ValidationEventHandler += (_, e) => errors.Add($"{e.Severity} línea {e.Exception?.LineNumber}: {e.Message}");
            using (var reader = XmlReader.Create(new StringReader(xml), settings)) { while (reader.Read()) { } }
            // Igual que en XmlSchemaConformanceTests: el marcador de firma y el schemeID son del arnés, no del motor.
            errors = errors.Where(e => !e.Contains("SignaturePlaceholder") && !e.Contains("'schemeID' attribute is invalid")).ToList();

            Assert.True(errors.Count == 0, $"{label}: {errors.Count} errores contra el XSD de la DIAN:\n" + string.Join("\n", errors.Take(25)));
        }
    }
}
