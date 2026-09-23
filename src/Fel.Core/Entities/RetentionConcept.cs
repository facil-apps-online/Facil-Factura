using System;

namespace Fel.Core.Entities
{
    // A qué tipo de adquirente (tercero comprador) aplica esta variante de la tarifa. Colombia
    // distingue formalmente "declarante"/"no declarante" de renta, pero para simplificar la
    // captura se usa el tipo de persona (Natural/Jurídica) del tercero como aproximación: Ambas
    // cuando el concepto no distingue tarifa por tipo de persona.
    public enum RetentionPersonType
    {
        Ambas = 0,
        Natural = 1,
        Juridica = 2
    }

    // Base sobre la que se calcula la tarifa: el subtotal de la línea (la mayoría de conceptos de
    // RET_FUENTE) o el IVA generado de la línea (las dos filas de "Retención en la fuente por IVA").
    public enum RetentionBaseType
    {
        Subtotal = 0,
        IvaGenerado = 1
    }

    // Una fila del catálogo de retención en la fuente (o ReteIVA) administrado por Superadmin.
    // El cliente elige manualmente una fila puntual de este catálogo en cada línea de factura
    // (ver InvoicesPage.tsx) — no hay ninguna resolución automática por tipo de persona ni mínimo
    // en UVT; GroupKey/GroupLabel/PersonType/BaseUvt quedan del diseño original pensado para esa
    // resolución automática y hoy no se usan para decidir nada, solo agrupan/describen el catálogo.
    public class RetentionConcept
    {
        public Guid Id { get; set; }
        public string GroupKey { get; set; } = string.Empty;
        public string GroupLabel { get; set; } = string.Empty; // Nombre genérico del concepto (selector del producto)
        public string Name { get; set; } = string.Empty; // Nombre de esta variante puntual (ej. "Compras generales (declarantes)")
        public RetentionPersonType PersonType { get; set; } = RetentionPersonType.Ambas;
        public string TaxCategory { get; set; } = "RET_FUENTE"; // RET_FUENTE o RET_IVA (lo que Dataico espera)
        public RetentionBaseType BaseType { get; set; } = RetentionBaseType.Subtotal;
        public decimal BaseUvt { get; set; } // Base mínima en UVT para que aplique la retención (0 = sin mínimo)
        public decimal Rate { get; set; } // Tarifa %
        public DateTime EffectiveFrom { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
