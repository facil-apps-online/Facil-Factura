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

        // Orden de creación de la línea dentro del formulario — sin esto, recargar la factura
        // (edición, representación gráfica, XML) no garantiza el mismo orden en que se agregaron
        // los ítems, porque Id es un GUID aleatorio y las consultas no tenían ORDER BY propio.
        public int LineNumber { get; set; }

        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }

        // Copia congelada del UnitOfMeasure del producto al momento de crear la línea — igual
        // criterio que Code/Name/UnitPrice: si el catálogo cambia después, una factura ya emitida
        // no debe cambiar de contenido.
        public string UnitOfMeasureCode { get; set; } = "94";
        public string UnitOfMeasureAbbreviation { get; set; } = "EA";
        public string UnitOfMeasureDisplayFormat { get; set; } = "Combined";
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
