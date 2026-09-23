using System;

namespace Fel.Core.Entities
{
    public class TariffTier
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty; // e.g. "Nivel 1"
        public int MinDocuments { get; set; }            // e.g. 1
        public int? MaxDocuments { get; set; }           // e.g. 2000 (null if it's the last tier)
        public decimal PricePerDocument { get; set; }    // e.g. 70
        public bool IsActive { get; set; } = true;

        // Null = tier global, aplica a cualquier integrador que no tenga un tier propio para ese
        // volumen. Un valor no nulo define una tarifa distinta específica para ese integrador
        // (p.ej. Dataico puede costarle más a Facil Factura que la emisión directa a la DIAN), que
        // tiene prioridad sobre el tier global en el mismo rango de volumen.
        public Guid? IntegratorId { get; set; }
        public Integrator? Integrator { get; set; }
    }
}
