using Fel.Core.Ubl21.Base;
using System.Xml.Serialization;

namespace Fel.Core.Ubl21.Sectors.PublicServices
{
    [XmlRoot("Invoice", Namespace = UblNamespaces.Main)]
    public class PublicServicesInvoice : BaseInvoice
    {
        public PublicServicesInvoice()
        {
            // Especifico para servicios públicos, CustomizationId 12 (ejemplo)
            CustomizationId = "12";
        }

        public void AddPublicServicesExtension(string meterNumber, string consumptionPeriod)
        {
            // Inject specific nodes for public services
        }
    }
}
