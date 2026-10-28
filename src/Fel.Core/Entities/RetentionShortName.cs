using System;

namespace Fel.Core.Entities
{
    // Nombre corto de cada categoría de retención (RET_FUENTE, RET_ICA, RET_IVA) que la administración de la plataforma parametriza. Con la tarifa forma el
    // texto corto de la retención ("RETE FUENTE 2.5%"), que sale en los selectores de los formularios (según el estilo del cliente) y en el PDF.
    public class RetentionShortName
    {
        public const int MaxLength = 30;

        public Guid Id { get; set; }
        public string TaxCategory { get; set; } = string.Empty;
        public string ShortName { get; set; } = string.Empty;

        // Cuando la tabla no tiene la categoría (o no se pudo leer) se usa este valor, el mismo con el que se siembra.
        public static string DefaultFor(string taxCategory) => taxCategory switch
        {
            "RET_FUENTE" => "RETE FUENTE",
            "RET_ICA" => "RETE ICA",
            "RET_IVA" => "RETE IVA",
            _ => taxCategory
        };
    }
}
