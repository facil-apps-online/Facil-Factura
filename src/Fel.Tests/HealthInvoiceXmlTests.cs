using System.Xml.Linq;
using Fel.Core.Models;
using Fel.Infrastructure.Ubl;

namespace Fel.Tests
{
    // Factura del sector salud (Documento Técnico 2 del Ministerio de Salud, julio de 2026): la extensión "Sector Salud", el período, los recaudos y las
    // reglas de los códigos. El XML se valida además contra el XSD de la DIAN cuando la caja de herramientas está en el árbol.
    public class HealthInvoiceXmlTests
    {
        private static readonly XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
        private static readonly XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
        private static readonly XNamespace ext = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";
        private static readonly XNamespace inv = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";

        private static HealthSectorData Health() => new()
        {
            ProviderCode = "1100100001", PaymentModality = "04", Coverage = "01", ContractNumber = "CUCON-2026-0001",
            PeriodStart = new DateTime(2026, 11, 1), PeriodEnd = new DateTime(2026, 11, 2),
            Prepaid = new List<HealthPrepaid>
            {
                new() { ConceptCode = "01", Amount = 3000m, ReceivedDate = new DateTime(2026, 11, 1) },
                new() { ConceptCode = "02", Amount = 2000m, ReceivedDate = new DateTime(2026, 11, 2) }
            }
        };

        private static UblInvoiceData Invoice(string operation = "SS-CUFE", HealthSectorData? health = null)
        {
            var data = XmlSchemaConformanceTests.Sample("01", operation);
            data.Health = health ?? Health();
            return data;
        }

        private static XDocument Generate(UblInvoiceData data) => XDocument.Parse(new UblGenerator(new XmlSchemaConformanceTests.FakeCrypto()).GenerateInvoiceXml(data));

        [Fact]
        public void The_health_invoice_conforms_to_the_dian_schema()
        {
            var folder = XmlSchemaConformanceTests.ToolboxXsdFolder();
            if (folder == null) return;
            var xml = Generate(Invoice()).ToString();

            var errors = XmlSchemaConformanceTests.Validate(xml, XmlSchemaConformanceTests.LoadSchemas(folder));
            Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors.Take(20)));
        }

        [Fact]
        public void The_sector_salud_extension_has_the_required_fields_and_goes_after_the_signature()
        {
            var doc = Generate(Invoice());
            Assert.Equal("SS-CUFE", doc.Root!.Element(cbc + "CustomizationID")!.Value);

            var extensions = doc.Root.Element(ext + "UBLExtensions")!.Elements().ToList();
            Assert.Equal(3, extensions.Count);
            Assert.Contains("SignaturePlaceholder", extensions[1].ToString());

            var group = extensions[2].Descendants(inv + "Group").Single();
            Assert.Equal("Sector Salud", (string?)group.Attribute("schemeName"));
            var collection = group.Element(inv + "Collection")!;
            Assert.Equal("Usuario", (string?)collection.Attribute("schemeName"));

            var items = collection.Elements(inv + "AdditionalInformation").ToDictionary(i => i.Element(inv + "Name")!.Value, i => i.Element(inv + "Value")!);
            Assert.Equal("1100100001", items["CODIGO_PRESTADOR"].Value);
            Assert.Equal("04", items["MODALIDAD_PAGO"].Value);
            Assert.Equal("salud_modalidad_pago.gc", (string?)items["MODALIDAD_PAGO"].Attribute("schemeName"));
            Assert.Equal("04", (string?)items["MODALIDAD_PAGO"].Attribute("schemeID"));
            Assert.Equal("01", items["COBERTURA_PLAN_BENEFICIOS"].Value);
            Assert.Equal("salud_cobertura.gc", (string?)items["COBERTURA_PLAN_BENEFICIOS"].Attribute("schemeName"));
            Assert.Equal("CUCON-2026-0001", items["NUMERO_CONTRATO"].Value);
            Assert.False(items.ContainsKey("NUMERO_POLIZA"));
            Assert.False(items.ContainsKey("FACTURA_SIN_CONTRATO"));
        }

        [Fact]
        public void A_policy_replaces_the_contract_and_a_missing_contract_carries_its_reason()
        {
            var policy = Health();
            policy.ContractNumber = null; policy.Coverage = "04"; policy.PolicyNumber = "POL-77";
            var names = Generate(Invoice(health: policy)).Descendants(inv + "Name").Select(n => n.Value).ToList();
            Assert.Contains("NUMERO_POLIZA", names);
            Assert.DoesNotContain("NUMERO_CONTRATO", names);

            var noContract = Health();
            noContract.ContractNumber = null; noContract.NoContractReason = "01";
            var withReason = Generate(Invoice(health: noContract)).Descendants(inv + "Name").Select(n => n.Value).ToList();
            Assert.Contains("FACTURA_SIN_CONTRATO", withReason);
        }

        [Fact]
        public void The_period_and_the_collections_are_written_in_order_and_do_not_reduce_the_payable_amount()
        {
            var doc = Generate(Invoice());
            var period = doc.Root!.Element(cac + "InvoicePeriod")!;
            Assert.Equal("2026-11-01", period.Element(cbc + "StartDate")!.Value);
            Assert.Equal("2026-11-02", period.Element(cbc + "EndDate")!.Value);

            var prepaid = doc.Root.Elements(cac + "PrepaidPayment").ToList();
            Assert.Equal(2, prepaid.Count);
            Assert.Equal("1", prepaid[0].Element(cbc + "ID")!.Value);
            Assert.Equal("01", (string?)prepaid[0].Element(cbc + "ID")!.Attribute("schemeID"));
            Assert.Equal("3000.00", prepaid[0].Element(cbc + "PaidAmount")!.Value);
            Assert.Equal("COP", (string?)prepaid[0].Element(cbc + "PaidAmount")!.Attribute("currencyID"));
            Assert.Equal("2", prepaid[1].Element(cbc + "ID")!.Value);
            Assert.Equal("02", (string?)prepaid[1].Element(cbc + "ID")!.Attribute("schemeID"));

            var totals = doc.Root.Element(cac + "LegalMonetaryTotal")!;
            Assert.Equal("5000.00", totals.Element(cbc + "PrepaidAmount")!.Value);
            Assert.Equal("11900.00", totals.Element(cbc + "PayableAmount")!.Value);
        }

        [Fact]
        public void The_collection_invoice_carries_no_sector_salud_extension()
        {
            var doc = Generate(Invoice("SS-Recaudo"));
            Assert.Equal("SS-Recaudo", doc.Root!.Element(cbc + "CustomizationID")!.Value);
            Assert.DoesNotContain("Sector Salud", doc.ToString());
        }

        [Fact]
        public void An_invoice_without_health_data_is_unchanged()
        {
            var doc = Generate(XmlSchemaConformanceTests.Sample("01"));
            Assert.Null(doc.Root!.Element(cac + "InvoicePeriod"));
            Assert.Empty(doc.Root.Elements(cac + "PrepaidPayment"));
            Assert.DoesNotContain("Interoperabilidad", doc.ToString());
        }

        [Fact]
        public void A_valid_block_has_no_errors()
        {
            Assert.Empty(HealthCatalogs.Validate("SS-CUFE", Health(), 11900m));
        }

        [Theory]
        [InlineData("provider")]
        [InlineData("modality")]
        [InlineData("coverage")]
        [InlineData("both")]
        [InlineData("policy-coverage")]
        [InlineData("no-reason")]
        [InlineData("period")]
        [InlineData("concept")]
        [InlineData("repeated")]
        [InlineData("too-much")]
        public void Each_invalid_block_is_reported(string rule)
        {
            var h = Health();
            switch (rule)
            {
                case "provider": h.ProviderCode = "12345"; break;
                case "modality": h.PaymentModality = "05"; break;
                case "coverage": h.Coverage = "99"; break;
                case "both": h.PolicyNumber = "POL-1"; h.Coverage = "04"; break;
                case "policy-coverage": h.ContractNumber = null; h.PolicyNumber = "POL-1"; h.Coverage = "01"; break;
                case "no-reason": h.ContractNumber = null; break;
                case "period": h.PeriodEnd = h.PeriodStart.AddDays(-1); break;
                case "concept": h.Prepaid[0].ConceptCode = "09"; break;
                case "repeated": h.Prepaid[1].ConceptCode = "01"; break;
                case "too-much": h.Prepaid[0].Amount = 20000m; break;
            }
            Assert.NotEmpty(HealthCatalogs.Validate("SS-CUFE", h, 11900m));
        }

        [Fact]
        public void The_collection_operation_skips_the_block_rules_and_the_others_require_it()
        {
            Assert.Empty(HealthCatalogs.Validate("SS-Recaudo", null, 5000m));
            Assert.NotEmpty(HealthCatalogs.Validate("SS-CUFE", null, 5000m));
        }
    }
}
