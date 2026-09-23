using System;
using System.Collections.Generic;

namespace Fel.Core.Entities
{
    public class DocumentItem
    {
        public Guid Id { get; set; }

        public Guid DocumentId { get; set; }
        public Document? Document { get; set; }

        public Guid? ProductId { get; set; }
        public Product? Product { get; set; }

        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public IvaTreatment IvaTreatment { get; set; } = IvaTreatment.Gravado;
        public decimal TaxRate { get; set; } // Tarifa de IVA cuando IvaTreatment = Gravado (0 si Exento/Excluido)
        public decimal DiscountRate { get; set; } // % aplicado sobre Cantidad*ValorUnitario antes de calcular impuestos

        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }

        public string SectorExtensionData { get; set; } = "{}";

        // La retención depende del concepto de cada línea (bienes vs. servicios tienen tarifas
        // distintas de RET_FUENTE/RET_ICA), por eso vive en el ítem y no en el documento completo.
        public ICollection<DocumentRetention> Retentions { get; set; } = new List<DocumentRetention>();

        // La retención de esta línea se calcula automáticamente a partir del concepto del producto;
        // este interruptor permite anularla puntualmente en una línea excepcional sin tocar el
        // producto ni el catálogo.
        public bool RetentionOverridden { get; set; }
    }
}
