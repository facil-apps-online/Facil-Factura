using Fel.Core.Ubl21.Base;
using System.Xml.Serialization;

namespace Fel.Core.Ubl21.Sectors.Health
{
    [XmlRoot("Invoice", Namespace = UblNamespaces.Main)]
    public class HealthSectorInvoice : BaseInvoice
    {
        public HealthSectorInvoice()
        {
            // Especifico para salud, DIAN requiere CustomizationId 11 o específico
            CustomizationId = "11";
        }

        // We can add helper methods to inject the Health Sector UBLExtension
        public void AddHealthSectorExtension(string providerCode, string healthOperationType)
        {
            // This would create the specific XML nodes for Sector Salud
            // and add them to this.Extensions
        }
    }
}
