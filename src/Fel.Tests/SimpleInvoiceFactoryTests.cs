using Fel.Api.Integration.Services;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Dataico.Models;
using Microsoft.EntityFrameworkCore;

namespace Fel.Tests
{
    // La fábrica que convierte el JSON simple (estructura Dataico) en el Document del portal. Se prueba contra una base en memoria.
    public class SimpleInvoiceFactoryTests
    {
        private static readonly Guid BranchId = Guid.NewGuid();

        private sealed class Fixture
        {
            public FelDbContext Db { get; }
            public Client Client { get; }
            public Resolution Resolution { get; }
            public SimpleInvoiceFactory Factory { get; }

            public Fixture(long start = 1, long end = 5000, DateTime? validTo = null)
            {
                var options = new DbContextOptionsBuilder<FelDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
                Db = new FelDbContext(options);
                Client = new Client { Id = Guid.NewGuid(), TaxId = "900123456" };
                Db.Clients.Add(Client);
                Resolution = new Resolution
                {
                    Id = Guid.NewGuid(), ClientId = Client.Id, ResolutionNumber = "18760000001", Prefix = "SETT", DocumentType = "FE", IsActive = true,
                    NumberStart = start, NumberEnd = end, ValidFrom = DateTime.Today.AddYears(-1), ValidTo = validTo ?? DateTime.Today.AddYears(1)
                };
                Db.Resolutions.Add(Resolution);
                Db.ResolutionBranches.Add(new ResolutionBranch { ResolutionId = Resolution.Id, BranchId = BranchId });
                var posResolution = new Resolution
                {
                    Id = Guid.NewGuid(), ClientId = Client.Id, ResolutionNumber = "18760000099", Prefix = "POS", DocumentType = "DE-POS", IsActive = true,
                    NumberStart = 1, NumberEnd = 5000, ValidFrom = DateTime.Today.AddYears(-1), ValidTo = DateTime.Today.AddYears(1)
                };
                Db.Resolutions.Add(posResolution);
                Db.ResolutionBranches.Add(new ResolutionBranch { ResolutionId = posResolution.Id, BranchId = BranchId });
                Db.DocumentTypes.Add(new DocumentType { Id = Guid.NewGuid(), Code = "DE-POS", Name = "Tiquete POS", IsActive = true, DianCode = "20" });
                Db.DocumentTypes.Add(new DocumentType { Id = Guid.NewGuid(), Code = "FE-STD", Name = "Factura", IsActive = true });
                Db.DocumentTypes.Add(new DocumentType { Id = Guid.NewGuid(), Code = "FE-MANDATO", Name = "Mandato", IsActive = true, OperationType = "11" });
                Db.DocumentTypes.Add(new DocumentType { Id = Guid.NewGuid(), Code = "FE-AIU", Name = "Servicios AIU", IsActive = true, OperationType = "09" });
                Db.IdentificationTypes.Add(new IdentificationType { Id = Guid.NewGuid(), Code = "31", Name = "NIT", DataicoCode = "NIT", IsActive = true });
                Db.IdentificationTypes.Add(new IdentificationType { Id = Guid.NewGuid(), Code = "13", Name = "Cédula de Ciudadanía", DataicoCode = "CC", IsActive = true });
                Db.TaxCatalogItems.Add(new TaxCatalogItem { Id = Guid.NewGuid(), Kind = TaxCatalogKind.PaymentMeans, Category = "CASH", Name = "Efectivo", DianCode = "10", IsActive = true });
                Db.UnitsOfMeasure.Add(new UnitOfMeasure { Id = Guid.NewGuid(), DianCode = "94", Abbreviation = "EA", Name = "Unidad", IsActive = true });
                Db.TaxCatalogItems.Add(new TaxCatalogItem { Id = Guid.NewGuid(), Kind = TaxCatalogKind.Retention, Category = "RET_FUENTE", IsActive = true });
                Db.DianMunicipalities.Add(new DianMunicipality { Code = "11001", Name = "Bogotá, D.C.", DepartmentCode = "11", DepartmentName = "Bogotá D.C." });
                Db.SaveChanges();
                Factory = new SimpleInvoiceFactory(Db);
            }
        }

        private static DataicoInvoiceRequest Request(string number = "1001", Action<DataicoInvoiceRequest>? tweak = null)
        {
            var request = new DataicoInvoiceRequest
            {
                number = number,
                issue_date = "01/11/2026",
                payment_means_type = "DEBITO",
                payment_means = "EFECTIVO",
                numbering = new DataicoNumbering { prefix = "SETT", resolution_number = "18760000001" },
                customer = new DataicoParty { party_identification_type = "NIT", party_identification = "900123456", company_name = "ACME SAS" },
                items =
                {
                    new DataicoInvoiceItem
                    {
                        sku = "A1", description = "Producto", quantity = 2, price = 10000,
                        taxes = { new DataicoTax { tax_category = "IVA", tax_rate = 19 } }
                    }
                }
            };
            tweak?.Invoke(request);
            return request;
        }

        [Fact]
        public async Task Builds_the_document_with_server_calculated_totals()
        {
            var f = new Fixture();
            var result = await f.Factory.BuildAsync(Request(), f.Client, BranchId);

            Assert.Null(result.Error);
            var doc = result.Document!;
            Assert.Equal("1001", doc.Number);
            Assert.Equal("SETT", doc.Prefix);
            Assert.Equal("FE-STD", doc.TypeCode);
            Assert.Equal(20000m, doc.Subtotal);
            Assert.Equal(3800m, doc.TaxAmount);
            Assert.Equal(23800m, doc.TotalAmount);
            Assert.Equal(f.Resolution.Id, doc.ResolutionId);
        }

        [Theory]
        [InlineData("CASH")]
        [InlineData("cash")]
        [InlineData("10")]
        [InlineData("Efectivo")]
        [InlineData("EFECTIVO")]
        public async Task Resolves_the_payment_means_to_the_catalog_key(string input)
        {
            var f = new Fixture();
            var result = await f.Factory.BuildAsync(Request(tweak: r => r.payment_means = input), f.Client, BranchId);

            Assert.Null(result.Error);
            Assert.Equal("CASH", result.Document!.PaymentMeans);
        }

        [Fact]
        public async Task Rejects_an_unknown_payment_means_and_lists_valid_ones()
        {
            var f = new Fixture();
            var result = await f.Factory.BuildAsync(Request(tweak: r => r.payment_means = "BITCOIN"), f.Client, BranchId);

            Assert.Equal("VALIDATION_ERROR", result.Error!.Code);
            Assert.Contains("CASH", string.Join("|", result.Error.Details!));
        }

        [Fact]
        public async Task Creates_the_customer_with_check_digit_for_a_nit()
        {
            var f = new Fixture();
            var result = await f.Factory.BuildAsync(Request(), f.Client, BranchId);

            var customer = result.Document!.Customer!;
            Assert.Equal("31", customer.IdentificationType);
            Assert.Equal("900123456", customer.IdentificationNumber);
            Assert.False(string.IsNullOrEmpty(customer.VerificationDigit));
            Assert.Equal("ACME SAS", customer.Name);
        }

        [Fact]
        public async Task Accepts_numbers_out_of_order_inside_the_range()
        {
            var f = new Fixture();
            var five = await f.Factory.BuildAsync(Request("5"), f.Client, BranchId);
            var four = await f.Factory.BuildAsync(Request("4"), f.Client, BranchId);

            Assert.Null(five.Error);
            Assert.Null(four.Error);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("5001")]
        public async Task Rejects_numbers_outside_the_authorized_range_with_a_clear_message(string number)
        {
            var f = new Fixture(start: 1, end: 5000);
            var result = await f.Factory.BuildAsync(Request(number), f.Client, BranchId);

            Assert.NotNull(result.Error);
            if (number == "0") Assert.Equal("VALIDATION_ERROR", result.Error!.Code);
            else
            {
                Assert.Equal("NUMBER_OUT_OF_RANGE", result.Error!.Code);
                Assert.Contains("1 a 5000", result.Error.Message);
            }
        }

        [Fact]
        public async Task Rejects_an_expired_resolution()
        {
            var f = new Fixture(validTo: DateTime.Today.AddDays(-1));
            var result = await f.Factory.BuildAsync(Request(), f.Client, BranchId);

            Assert.Equal("RESOLUTION_EXPIRED", result.Error!.Code);
        }

        [Fact]
        public async Task Names_each_invalid_field()
        {
            var f = new Fixture();
            var result = await f.Factory.BuildAsync(Request("", r =>
            {
                r.items[0].quantity = 0;
                r.items[0].discount_rate = 120;
                r.payment_means_type = "OTRO";
            }), f.Client, BranchId);

            Assert.Equal("VALIDATION_ERROR", result.Error!.Code);
            var details = string.Join("|", result.Error.Details!);
            Assert.Contains("number:", details);
            Assert.Contains("items[0].quantity", details);
            Assert.Contains("items[0].discount_rate", details);
            Assert.Contains("payment_means_type", details);
        }

        [Fact]
        public async Task Reports_the_available_resolutions_when_the_prefix_is_unknown()
        {
            var f = new Fixture();
            var result = await f.Factory.BuildAsync(Request(tweak: r => r.numbering.prefix = "XXX"), f.Client, BranchId);

            Assert.Equal("RESOLUTION_NOT_FOUND", result.Error!.Code);
            Assert.Contains("SETT", result.Error.Message);
        }

        [Fact]
        public async Task Accepts_an_address_without_a_municipality_and_leaves_it_out_with_a_warning()
        {
            var f = new Fixture();
            var result = await f.Factory.BuildAsync(Request(tweak: r => r.customer.address_line = "Calle 10 # 5-20"), f.Client, BranchId);

            Assert.Null(result.Error);
            Assert.True(string.IsNullOrEmpty(result.Document!.Customer!.Address));
            Assert.Single(result.Warnings);
        }

        [Fact]
        public async Task Resolves_the_municipality_by_name_and_keeps_the_address()
        {
            var f = new Fixture();
            var result = await f.Factory.BuildAsync(Request(tweak: r => { r.customer.address_line = "Calle 10 # 5-20"; r.customer.city = "BOGOTA, D.C."; }), f.Client, BranchId);

            Assert.Null(result.Error);
            Assert.Equal("11001", result.Document!.Customer!.CityCode);
            Assert.Equal("Calle 10 # 5-20", result.Document.Customer.Address);
            Assert.Empty(result.Warnings);
        }

        [Fact]
        public async Task Resolves_the_municipality_from_the_dane_segments()
        {
            var f = new Fixture();
            var result = await f.Factory.BuildAsync(Request(tweak: r => { r.customer.address_line = "Calle 10"; r.customer.department = "11"; r.customer.city = "001"; }), f.Client, BranchId);

            Assert.Equal("11001", result.Document!.Customer!.CityCode);
        }

        [Fact]
        public async Task Returns_the_existing_document_when_the_number_was_already_issued()
        {
            var f = new Fixture();
            var first = await f.Factory.BuildAsync(Request(), f.Client, BranchId);
            first.Document!.Status = "APPROVED";
            f.Db.Documents.Add(first.Document);
            await f.Db.SaveChangesAsync();

            var again = await f.Factory.BuildAsync(Request(), f.Client, BranchId);

            Assert.Null(again.Error);
            Assert.NotNull(again.ExistingLocked);
            Assert.Equal(first.Document.Id, again.ExistingLocked!.Id);
        }

        [Fact]
        public async Task Reuses_the_same_document_when_the_number_was_rejected()
        {
            var f = new Fixture();
            var first = await f.Factory.BuildAsync(Request(), f.Client, BranchId);
            first.Document!.Status = "REJECTED";
            f.Db.Documents.Add(first.Document);
            await f.Db.SaveChangesAsync();

            var retry = await f.Factory.BuildAsync(Request(tweak: r => r.items[0].quantity = 3), f.Client, BranchId);

            Assert.True(retry.Reused);
            Assert.Equal(first.Document.Id, retry.Document!.Id);
            Assert.Single(retry.Document.Items);
            Assert.Equal(30000m, retry.Document.Subtotal);
        }

        [Fact]
        public async Task Rejects_fixed_amount_taxes_until_their_contract_is_defined()
        {
            var f = new Fixture();
            var result = await f.Factory.BuildAsync(Request(tweak: r => r.items[0].taxes.Add(new DataicoTax { tax_category = "IMPUESTO_BOLSA", tax_amount = 66 })), f.Client, BranchId);

            Assert.Equal("VALIDATION_ERROR", result.Error!.Code);
            Assert.Contains("tax_amount", string.Join("|", result.Error.Details!));
        }

        [Fact]
        public async Task An_aiu_invoice_generates_the_four_lines_from_the_contract()
        {
            var f = new Fixture();
            var result = await f.Factory.BuildAsync(Request(tweak: r =>
            {
                r.operation = "AIU";
                r.items = new List<DataicoInvoiceItem>();
                r.aiu = new DataicoAiuContract { base_amount = 1_000_000, contract_object = "Obra civil de adecuación de bodega" };
            }), f.Client, BranchId);

            Assert.Null(result.Error);
            var doc = result.Document!;
            Assert.Equal("FE-AIU", doc.TypeCode);
            Assert.Equal(new[] { "AIU-A", "AIU-I", "AIU-U", "SERVICIO" }, doc.Items.Select(i => i.Code).ToArray());
            Assert.Equal(new[] { 100_000m, 50_000m, 50_000m, 1_000_000m }, doc.Items.Select(i => i.UnitPrice).ToArray());
            var stored = Fel.Core.Models.AiuContractData.TryRead(doc.SectorExtensionData);
            Assert.Equal(10, stored!.AdminPercent);
            Assert.Equal(19, stored.IvaRate);
            Assert.Equal("Obra civil de adecuación de bodega", stored.ContractObject);
        }

        private sealed class FakeCrypto : Fel.Core.Interfaces.ICryptoService
        {
            public string Encrypt(string plainText) => plainText;
            public string Decrypt(string cipherText) => cipherText;
            public string GenerateCufeSha384(string dataToHash) => System.Convert.ToHexString(System.Security.Cryptography.SHA384.HashData(System.Text.Encoding.UTF8.GetBytes(dataToHash))).ToLowerInvariant();
        }

        private static DataicoPosData Box() => new()
        {
            box_plate = "CAJA-01", box_location = "ALMACEN", cashier = "Maria Perez", box_type = "Registradora", sale_code = "V-1001"
        };

        [Fact]
        public async Task A_pos_document_uses_the_pos_resolution_and_stores_the_box_data()
        {
            var f = new Fixture();
            var result = await f.Factory.BuildAsync(Request(tweak: r => { r.operation = "POS"; r.numbering = new DataicoNumbering { prefix = "POS" }; r.pos = Box(); }), f.Client, BranchId);

            Assert.Null(result.Error);
            var doc = result.Document!;
            Assert.Equal("DE-POS", doc.TypeCode);
            Assert.Equal("POS", doc.Prefix);
            var pos = Fel.Core.Models.PosSaleData.TryRead(doc.SectorExtensionData)!;
            Assert.Equal("CAJA-01", pos.BoxPlate);
            Assert.Equal("V-1001", pos.SaleCode);
            Assert.False(pos.HasBuyerBenefits);
        }

        [Fact]
        public async Task A_pos_document_without_a_customer_goes_to_the_consumidor_final()
        {
            var f = new Fixture();
            var result = await f.Factory.BuildAsync(Request(tweak: r =>
            {
                r.operation = "POS"; r.numbering = new DataicoNumbering { prefix = "POS" }; r.pos = Box(); r.customer = new DataicoParty();
            }), f.Client, BranchId);

            Assert.Null(result.Error);
            Assert.Equal("222222222222", result.Document!.Customer!.IdentificationNumber);
        }

        [Fact]
        public async Task A_pos_document_needs_the_box_data_and_complete_benefits()
        {
            var f = new Fixture();
            var missing = await f.Factory.BuildAsync(Request(tweak: r => { r.operation = "POS"; r.numbering = new DataicoNumbering { prefix = "POS" }; }), f.Client, BranchId);
            Assert.Contains("pos:", string.Join("|", missing.Error!.Details!));

            var partial = await f.Factory.BuildAsync(Request(tweak: r =>
            {
                r.operation = "POS"; r.numbering = new DataicoNumbering { prefix = "POS" };
                r.pos = Box(); r.pos.buyer_code = "123";
            }), f.Client, BranchId);
            Assert.Contains("beneficios", string.Join("|", partial.Error!.Details!));
        }

        [Fact]
        public async Task A_pos_document_does_not_take_a_factura_resolution()
        {
            var f = new Fixture();
            var result = await f.Factory.BuildAsync(Request(tweak: r => { r.operation = "POS"; r.pos = Box(); }), f.Client, BranchId);

            Assert.Equal("RESOLUTION_NOT_FOUND", result.Error!.Code);
        }

        [Fact]
        public void The_pos_xml_has_its_profile_operation_and_box_extensions()
        {
            var generator = new Fel.Infrastructure.Ubl.UblGenerator(new FakeCrypto());
            var data = new Fel.Core.Models.UblInvoiceData
            {
                DocumentNumber = "1", Prefix = "POS", DianCode = "20", OperationType = "10", IssueDate = DateTime.Now, IssueTime = DateTime.Now, LineExtensionAmount = 10000m,
                Issuer = new Fel.Core.Models.IssuerData { TaxId = "900123456", Name = "ACME" },
                Pos = new Fel.Core.Models.PosSaleData { BoxPlate = "CAJA-01", BoxLocation = "ALMACEN", Cashier = "Maria Perez", BoxType = "Registradora", SaleCode = "V-1", BuyerCode = "99", BuyerName = "Juan", BuyerPoints = 5 }
            };

            var xml = generator.GenerateInvoiceXml(data);

            Assert.Contains("DIAN 2.1: Documento Equivalente POS", xml);
            Assert.Contains("<InformacionCajaVenta>", xml);
            var flat = System.Text.RegularExpressions.Regex.Replace(xml, @">\s+<", "><");
            Assert.Contains("<Name>PlacaCaja</Name><Value>CAJA-01</Value>", flat);
            Assert.Contains("<Name>SubTotal</Name><Value>10000.00</Value>", flat);
            Assert.Contains("<InformacionVeneficiosComprador>", xml);
            Assert.True(xml.IndexOf("SignaturePlaceholder", StringComparison.Ordinal) < xml.IndexOf("InformacionCajaVenta", StringComparison.Ordinal));
        }

        [Fact]
        public async Task An_aiu_invoice_validates_the_contract_block()
        {
            var f = new Fixture();
            var result = await f.Factory.BuildAsync(Request(tweak: r => { r.operation = "AIU"; r.items = new List<DataicoInvoiceItem>(); r.aiu = new DataicoAiuContract { contract_object = "corto" }; }), f.Client, BranchId);

            Assert.Equal("VALIDATION_ERROR", result.Error!.Code);
            var details = string.Join("|", result.Error.Details!);
            Assert.Contains("aiu.base_amount", details);
            Assert.Contains("aiu.contract_object", details);
        }

        [Fact]
        public async Task A_mandate_invoice_needs_the_mandante_on_every_line()
        {
            var f = new Fixture();
            var result = await f.Factory.BuildAsync(Request(tweak: r => r.operation = "MANDATO"), f.Client, BranchId);

            Assert.Equal("VALIDATION_ERROR", result.Error!.Code);
            Assert.Contains("mandante", string.Join("|", result.Error.Details!));
        }

        [Fact]
        public async Task A_retention_with_a_known_category_is_calculated_on_the_line_base()
        {
            var f = new Fixture();
            var result = await f.Factory.BuildAsync(Request(tweak: r => r.items[0].retentions = new List<DataicoTax> { new() { tax_category = "RET_FUENTE", tax_rate = 2.5m } }), f.Client, BranchId);

            Assert.Null(result.Error);
            var retention = Assert.Single(result.Document!.Items.First().Retentions);
            Assert.Equal(500m, retention.Amount);
        }
    }
}
