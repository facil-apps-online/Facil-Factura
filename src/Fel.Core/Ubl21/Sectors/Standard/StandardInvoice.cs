using Fel.Core.Ubl21.Base;
using System.Xml.Serialization;

namespace Fel.Core.Ubl21.Sectors.Standard
{
    [XmlRoot("Invoice", Namespace = UblNamespaces.Main)]
    public class StandardInvoice : BaseInvoice
    {
        public StandardInvoice()
        {
            CustomizationId = "10"; // Documento Equivalente o Factura de Venta Estándar
        }
    }
}
