using System.Xml;
using System.Xml.Schema;
using Fel.Core.Models;
using Fel.Infrastructure.Ubl;

namespace Fel.Tests
{
    // Los eventos RADIAN que arma el motor nativo (030, 031, 032, 033) contra el XSD de ApplicationResponse de la caja de herramientas de RADIAN.
    // Si la caja no está en el árbol (otro equipo, CI), las pruebas se saltan solas.
    public class ReceptionEventXmlTests
    {
        private static string? RadianFolder()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "Caja de Herramientas", "Caja-de-herramientas-RADIAN-1", "RADIAN 1.1");
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
            foreach (var file in Directory.GetFiles(Path.Combine(xsdFolder, "common"), "*.xsd"))
                Add(file);
            foreach (var file in new[] { "maindoc/UBL-ApplicationResponse-2.1.xsd", "maindoc/DIAN_UBL_Structures.xsd" })
                Add(Path.Combine(xsdFolder, file));
            set.Compile();
            return set;
        }

        private static UblEventData Sample(string code, string description) => new()
        {
            DocumentNumber = "ACR0021", IssueDate = new DateTime(2026, 11, 4, 9, 15, 0), IssueTime = new DateTime(2026, 11, 4, 9, 15, 0),
            Environment = "2", SoftwareId = "fa326ca7-c1f8-40d3-a6fc-24d7c1040607", SoftwarePin = "12345",
            SenderTaxId = "900303194", SenderName = "SOFACTORY SAS",
            ReceiverTaxId = "800111222", ReceiverName = "PROVEEDOR SAS",
            ResponseCode = code, ResponseDescription = description,
            ReferencedDocumentId = "SETG980000358", ReferencedCufe = new string('a', 96), ReferencedDocumentTypeCode = "01"
        };

        public static IEnumerable<object[]> Events() => new[]
        {
            new object[] { "030", "Acuse de recibo de Factura Electrónica de Venta" },
            new object[] { "031", "Reclamo de la Factura Electrónica de Venta" },
            new object[] { "032", "Recibo del bien y prestación de servicio" },
            new object[] { "033", "Aceptación expresa" },
        };

        [Theory]
        [MemberData(nameof(Events))]
        public void The_reception_event_conforms_to_the_radian_schema(string code, string description)
        {
            var folder = RadianFolder();
            if (folder == null) return;
            var schemas = LoadSchemas(Path.Combine(folder, "XSD"));
            var generator = new UblGenerator(new XmlSchemaConformanceTests.FakeCrypto());
            var data = Sample(code, description);

            var xml = generator.GenerateEventXml(data, generator.CalculateEventCude(data));

            var errors = XmlSchemaConformanceTests.Validate(xml, schemas);
            Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
        }
    }
}
