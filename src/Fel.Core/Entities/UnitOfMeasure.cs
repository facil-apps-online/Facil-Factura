using System;

namespace Fel.Core.Entities
{
    // Catálogo global (no por Tenant/Client) de unidades de medida DIAN — mismo criterio que
    // TaxCatalogItem/RetentionConcept: administrado desde Superadmin, leído sin
    // autenticación de tenant desde los demás portales.
    public class UnitOfMeasure
    {
        // Fila semilla "Unidad" (94/EA) — default de Product.UnitOfMeasureId y fallback cuando el
        // Excel de importación de productos no trae unidad.
        public static readonly Guid DefaultUnidadId = Guid.Parse("30000000-0000-0000-0000-000000000001");

        public Guid Id { get; set; }

        // Código que se envía en el XML UBL (unitCode) — para "Unidad" es el histórico "94"; para
        // el resto de unidades coincide con Abbreviation (ej. "KGM").
        public string DianCode { get; set; } = string.Empty;

        // Código UN/CEFACT Recomendación 20 (ej. "EA", "KGM", "HUR").
        public string Abbreviation { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        // Cómo se imprime en la representación gráfica por defecto — el Client puede sobrescribir
        // esto globalmente para sus propias facturas (ver Client.UnitOfMeasureDisplayOverride).
        // Combined = "94 - EA", CodeOnly = "94", AbbreviationOnly = "EA".
        public string DisplayFormat { get; set; } = "Combined";

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
    }
}
