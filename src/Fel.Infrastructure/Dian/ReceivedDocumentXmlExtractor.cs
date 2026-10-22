using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Fel.Infrastructure.Security;

namespace Fel.Infrastructure.Dian
{
    // Los paquetes de documentos de la DIAN suelen traer un AttachedDocument como contenedor.
    // El documento fiscal real va embebido dentro de ese contenedor como XML o Base64.
    public static class ReceivedDocumentXmlExtractor
    {
        private static bool IsFiscalRoot(string name) => name is "Invoice" or "CreditNote" or "DebitNote";

        public static string Extract(string xml)
        {
            var current = xml;
            for (var depth = 0; depth < 4; depth++)
            {
                var document = SafeXml.Parse(current);
                var root = document.Root ?? throw new InvalidOperationException("El XML no tiene un elemento raíz.");
                if (IsFiscalRoot(root.Name.LocalName)) return current;
                if (root.Name.LocalName != "AttachedDocument")
                    throw new NotSupportedException($"Tipo de documento no soportado para recepción: '{root.Name.LocalName}'. Se esperaba Invoice, CreditNote o DebitNote.");

                var embedded = FindEmbeddedXml(root);
                if (embedded == null)
                    throw new InvalidOperationException("El AttachedDocument no contiene un XML fiscal embebido.");
                current = embedded;
            }

            throw new InvalidOperationException("El XML tiene demasiados niveles de documentos contenedores.");
        }

        private static string? FindEmbeddedXml(XElement root)
        {
            foreach (var element in root.Descendants().Where(e => e.Name.LocalName == "EmbeddedDocumentBinaryObject"))
            {
                var value = DecodeCandidate(element.Value);
                if (value != null) return value;
            }

            foreach (var element in root.Descendants().Where(e => e.Name.LocalName is "Description" or "EmbeddedDocument"))
            {
                var value = DecodeCandidate(element.Value);
                if (value != null) return value;
            }

            return null;
        }

        private static string? DecodeCandidate(string raw)
        {
            var value = raw.Trim();
            if (value.Length == 0) return null;
            if (LooksLikeXml(value)) return IsFiscalXml(value) ? value : null;

            try
            {
                var bytes = Convert.FromBase64String(value);
                var decoded = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF', ' ', '\r', '\n', '\t');
                return IsFiscalXml(decoded) ? decoded : null;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private static bool LooksLikeXml(string value) => value.StartsWith("<", StringComparison.Ordinal);

        private static bool IsFiscalXml(string value)
        {
            try
            {
                var root = SafeXml.Parse(value).Root;
                return root != null && IsFiscalRoot(root.Name.LocalName);
            }
            catch
            {
                return false;
            }
        }
    }
}
