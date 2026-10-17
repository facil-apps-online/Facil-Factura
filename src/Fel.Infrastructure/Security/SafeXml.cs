using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace Fel.Infrastructure.Security
{
    // Lectura de XML que viene de fuera (facturas recibidas por correo, respuestas de la DIAN). XDocument.Parse acepta DTD por defecto, lo que
    // permite entidades que se expanden sin control (denegación de servicio). Acá no se admite ninguna DTD, no se resuelve nada externo y el
    // tamaño tiene tope. Los documentos de la DIAN y de los proveedores nunca llevan DTD: uno que la traiga se rechaza.
    public static class SafeXml
    {
        private const long MaxCharacters = 20_000_000;

        public static XDocument Parse(string xml)
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaxCharacters
            };
            using var text = new StringReader(xml);
            using var reader = XmlReader.Create(text, settings);
            return XDocument.Load(reader);
        }
    }
}
