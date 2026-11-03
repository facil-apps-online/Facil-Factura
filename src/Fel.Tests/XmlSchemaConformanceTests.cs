using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Fel.Core.Models;
using Fel.Infrastructure.Ubl;

namespace Fel.Tests
{
    // El XML que arma el motor nativo contra los XSD oficiales de la DIAN (Caja de Herramientas del proyecto, FE v1.9): factura, notas crédito y débito,
    // documento soporte y su ajuste, y el documento equivalente POS. La DIAN valida esto antes que cualquier regla de negocio.
    // Si la caja de herramientas no está en el árbol (otro equipo, CI), las pruebas se saltan solas.
    public class XmlSchemaConformanceTests
    {
        internal sealed class FakeCrypto : Fel.Core.Interfaces.ICryptoService
        {
            public string Encrypt(string plainText) => plainText;
            public string Decrypt(string cipherText) => cipherText;
            public string GenerateCufeSha384(string dataToHash) =>
                Convert.ToHexString(System.Security.Cryptography.SHA384.HashData(System.Text.Encoding.UTF8.GetBytes(dataToHash))).ToLowerInvariant();
        }

        internal static string? ToolboxXsdFolder()
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

        internal static XmlSchemaSet LoadSchemas(string xsdFolder)
        {
            var set = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };
            // El esquema de xmldsig trae un DOCTYPE: se lee con DTD habilitado (solo los XSD del proyecto, nunca el XML que se valida).
            void Add(string path)
            {
                using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = new XmlUrlResolver() });
                set.Add(null, reader);
            }
            // Los esquemas comunes traen firma (xmldsig) y XAdES, que los documentos principales referencian sin importarlos por su cuenta.
            foreach (var file in Directory.GetFiles(Path.Combine(xsdFolder, "common"), "*.xsd"))
                Add(file);
            foreach (var file in new[] { "maindoc/UBL-Invoice-2.1.xsd", "maindoc/UBL-CreditNote-2.1.xsd", "maindoc/UBL-DebitNote-2.1.xsd", "maindoc/DIAN_UBL_Structures.xsd" })
                Add(Path.Combine(xsdFolder, file));
            set.Compile();
            return set;
        }

        internal static List<string> Validate(string xml, XmlSchemaSet schemas)
        {
            var errors = new List<string>();
            var settings = new XmlReaderSettings { ValidationType = ValidationType.Schema, Schemas = schemas, DtdProcessing = DtdProcessing.Prohibit };
            settings.ValidationEventHandler += (_, e) => errors.Add($"{e.Severity} línea {e.Exception?.LineNumber}: {e.Message}");
            using var reader = XmlReader.Create(new StringReader(xml), settings);
            while (reader.Read()) { }
            // Del arnés, no del motor: el documento aún no está firmado (queda el marcador) y el XSD de la caja es más estricto que la propia DIAN con schemeID
            // (el ejemplo oficial Generica.xml también lo incumple).
            return errors.Where(e => !e.Contains("SignaturePlaceholder") && !e.Contains("'schemeID' attribute is invalid")).ToList();
        }

        private static UblInvoiceData Sample(string dianCode, string operationType = "10")
        {
            var now = new DateTime(2026, 11, 2, 10, 30, 0);
            var data = new UblInvoiceData
            {
                DocumentNumber = "1005", Prefix = "SFRY", IssueDate = now, IssueTime = now,
                TechnicalKey = "fc8eac422eba16e22ffd8c6f94b3f40a6e38162c", SoftwareId = "56f2ae4e-9812-4fad-9255-08fcfcd5ccb0", SoftwarePin = "12345",
                Environment = "2", ResolutionNumber = "18764092013879", ResolutionValidFrom = new DateTime(2025, 4, 15), ResolutionValidTo = new DateTime(2027, 4, 15),
                ResolutionNumberStart = 1000, ResolutionNumberEnd = 2000, DianCode = dianCode, OperationType = operationType, Currency = "COP",
                Issuer = new IssuerData { TaxId = "900303194", Name = "SOFACTORY SAS", TaxLevelCodes = new List<string> { "O-47" }, TaxSchemeId = "01", TaxSchemeName = "IVA", AdditionalAccountId = "1", MercantileRegistration = "10181", DepartmentCode = "11", DepartmentName = "Bogotá D.C.", CityCode = "11001", CityName = "Bogotá, D.C.", Address = "Cra. 17 # 63A-26", Email = "sofactorysas@gmail.com" },
                Customer = new CustomerData { TaxId = "222222222222", IdentificationCode = "13", Name = "Consumidor final", Email = "cliente@acme.co" },
                PaymentMeans = new List<PaymentMeansData> { new() { Id = "1", PaymentMeansCode = "10" } },
                LineExtensionAmount = 10000m, TaxExclusiveAmount = 10000m, TaxInclusiveAmount = 11900m, PayableAmount = 11900m,
                Taxes = new List<TaxSubtotal> { new() { TaxId = "01", TaxName = "IVA", TaxAmount = 1900m, TaxableAmount = 10000m, Percent = 19m } },
                Lines = new List<InvoiceLine>
                {
                    new()
                    {
                        ItemCode = "PRUEBA-API", Description = "Prueba de integración API", Quantity = 1, UnitCode = "94", UnitPrice = 10000m, LineExtensionAmount = 10000m,
                        Taxes = new List<TaxSubtotal> { new() { TaxId = "01", TaxName = "IVA", TaxAmount = 1900m, TaxableAmount = 10000m, Percent = 19m } }
                    }
                }
            };
            if (dianCode is "91" or "92")
            {
                data.BillingReferenceDocumentNumber = "SFRY1004";
                data.BillingReferenceCufe = new string('a', 96);
                data.BillingReferenceDate = now.AddDays(-1);
                data.DiscrepancyResponseCode = "2";
                data.DiscrepancyDescription = "Anulación de factura electrónica";
            }
            return data;
        }

        private static IEnumerable<(string Name, UblInvoiceData Data)> Cases()
        {
            yield return ("Factura 01", Sample("01"));
            yield return ("Nota crédito 91", Sample("91"));
            yield return ("Nota débito 92", Sample("92"));
            yield return ("Documento soporte 05", Sample("05"));
            yield return ("Ajuste documento soporte 95", Sample("95"));
            var pos = Sample("20");
            pos.Pos = new PosSaleData { BoxPlate = "CAJA-01", BoxLocation = "ALMACEN", Cashier = "Maria Perez", BoxType = "Registradora", SaleCode = "V-1" };
            yield return ("Documento equivalente POS 20", pos);
        }

        public static IEnumerable<object[]> CaseNames() => Cases().Select(c => new object[] { c.Name });

        [Theory]
        [MemberData(nameof(CaseNames))]
        public void The_generated_xml_conforms_to_the_official_schemas(string name)
        {
            var folder = ToolboxXsdFolder();
            if (folder == null) return; // sin caja de herramientas no hay contra qué validar

            var data = Cases().First(c => c.Name == name).Data;
            var xml = new UblGenerator(new FakeCrypto()).GenerateInvoiceXml(data);

            var errors = Validate(xml, LoadSchemas(folder));
            Assert.True(errors.Count == 0, $"{name}: {errors.Count} errores contra el XSD de la DIAN:\n" + string.Join("\n", errors.Take(25)));
        }

        // Calibración del arnés: los ejemplos oficiales de la propia caja de herramientas deben pasar con el mismo esquema. Si uno falla, el defecto es
        // de la validación (no del XML del motor) y hay que entender por qué antes de fiarse de los demás resultados.
        [Theory]
        [InlineData("Generica.xml")]
        [InlineData("CreditNote.xml")]
        [InlineData("DebitNote.xml")]
        public void The_official_toolbox_examples_pass_the_same_validation(string example)
        {
            var folder = ToolboxXsdFolder();
            if (folder == null) return;
            var path = Path.Combine(Path.GetDirectoryName(folder)!, "Ejemplificaciones", "XMLs de ejemplo", example);

            var errors = Validate(File.ReadAllText(path), LoadSchemas(folder));
            Assert.True(errors.Count == 0, $"{example}: {errors.Count} errores:{Environment.NewLine}" + string.Join(Environment.NewLine, errors.Take(15)));
        }

        // El identificador que se guarda en el documento y va en el QR tiene que ser el que lleva el XML en cbc:UUID.
        [Theory]
        [MemberData(nameof(CaseNames))]
        public void The_stored_document_key_is_the_one_inside_the_xml(string name)
        {
            var generator = new UblGenerator(new FakeCrypto());
            var data = Cases().First(c => c.Name == name).Data;
            var xml = XDocument.Parse(generator.GenerateInvoiceXml(data));
            XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
            var uuid = xml.Descendants(cbc + "UUID").First().Value;

            Assert.Equal(uuid, generator.CalculateDocumentKey(data));
        }
    }
}
