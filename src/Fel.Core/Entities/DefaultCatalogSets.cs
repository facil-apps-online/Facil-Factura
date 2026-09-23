using System;
using System.Collections.Generic;

namespace Fel.Core.Entities
{
    // IDs fijos (de los seeds de DocumentType/RetentionConcept en FelDbContext) que definen qué
    // se habilita por defecto al crear un Client. Centralizado aquí porque lo usan tanto
    // TenantClientsController (altas nuevas) como la migración que hace el backfill de los
    // Clients que ya existían antes de este mecanismo de habilitación.
    public static class DefaultCatalogSets
    {
        // Factura (FE-STD), Nota Crédito, Nota Débito — lo mismo que ya se ofrecía antes de que
        // existiera este mecanismo (InvoiceController.SupportedDianCodes), para no regresionar a
        // los Clients existentes.
        public static readonly IReadOnlyList<Guid> StandardDocumentTypeIds = new[]
        {
            Guid.Parse("00000000-0000-0000-0000-000000000001"), // FE-STD
            Guid.Parse("00000000-0000-0000-0000-000000000009"), // NC
            Guid.Parse("00000000-0000-0000-0000-000000000010")  // ND
        };

        // Compras generales, Servicios generales y Honorarios y comisiones (declarante + no
        // declarante de cada uno) — los conceptos de retención más comunes. Deliberadamente sin
        // ReteIVA (RETEIVA_SERVICIOS/RETEIVA_COMPRAS), que solo aplica a regímenes específicos.
        public static readonly IReadOnlyList<Guid> StandardRetentionConceptIds = new[]
        {
            Guid.Parse("30000000-0000-0000-0000-000000000001"), // Compras generales (declarantes)
            Guid.Parse("30000000-0000-0000-0000-000000000002"), // Compras generales (no declarantes)
            Guid.Parse("3000000f-0000-0000-0000-000000000015"), // Servicios generales (declarantes)
            Guid.Parse("30000010-0000-0000-0000-000000000016"), // Servicios generales (no declarantes)
            Guid.Parse("30000021-0000-0000-0000-000000000033"), // Honorarios y comisiones (personas jurídicas)
            Guid.Parse("30000022-0000-0000-0000-000000000034")  // Honorarios y comisiones (personas naturales, no declarantes)
        };
    }
}
