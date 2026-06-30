using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Fel.Core.Interfaces;
using Fel.Core.Ubl21;
using Fel.Core.Ubl21.Base;

namespace Fel.Infrastructure.Services
{
    public class XmlBuilderService : IXmlBuilderService
    {
        public string BuildXml<T>(T invoice) where T : BaseInvoice
        {
            var namespaces = new XmlSerializerNamespaces();
            namespaces.Add("cac", UblNamespaces.Cac);
            namespaces.Add("cbc", UblNamespaces.Cbc);
            namespaces.Add("ext", UblNamespaces.Ext);
            namespaces.Add("sts", UblNamespaces.Sts);

            var serializer = new XmlSerializer(typeof(T));
            
            var settings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false), // UTF-8 sin BOM, requerido por DIAN
                Indent = true,
                OmitXmlDeclaration = false
            };

            using var memoryStream = new MemoryStream();
            using var xmlWriter = XmlWriter.Create(memoryStream, settings);
            
            serializer.Serialize(xmlWriter, invoice, namespaces);
            
            return Encoding.UTF8.GetString(memoryStream.ToArray());
        }
    }
}
