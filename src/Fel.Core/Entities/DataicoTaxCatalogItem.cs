using System;

namespace Fel.Core.Entities
{
    public enum TaxCatalogKind
    {
        Retention = 0, // Va al pie del documento (RET_FUENTE, RET_ICA, RET_IVA), pero Dataico la recibe por ítem.
        OtherTax = 1, // Impuesto adicional por ítem, distinto del IVA (IMP_CONSUMO, IMP_BOLSA_PLASTICA, etc.)
        IvaRate = 2, // Tarifas de IVA válidas cuando el tratamiento es Gravado (ej. "19", "5")
        PaymentTerm = 3, // Plazos de pago en días (ej. "0", "30", "60"), para calcular la fecha de vencimiento
        PaymentMeans = 4, // Medio de pago aceptado por Dataico para Facturas/Documentos Soporte (ej. "EFECTIVO", "TRANSFERENCIA")
        WorkerType = 5, // Tipo de trabajador para Nómina Electrónica (ej. "DEPENDIENTE")
        ContractType = 6, // Tipo de contrato para Nómina Electrónica (ej. "TERMINO_INDEFINIDO")
        PayrollPaymentMeans = 7, // Medio de pago del empleado para Nómina Electrónica (catálogo propio, distinto de PaymentMeans)
        TaxLevelCode = 8, // Nivel tributario del tercero (ej. "COMUN", "RESPONSABLE_DE_IVA")
        Regimen = 9, // Régimen del tercero (ej. "ORDINARIO", "SIMPLE", "AUTORRETENEDOR")
        AccountType = 10 // Tipo de cuenta bancaria (ej. "AHORROS", "CORRIENTE")
    }

    // Catálogo global de valores válidos para Dataico (impuestos, retenciones, tarifas de IVA,
    // plazos de pago, medios de pago), administrado por Superadmin. Los tenants/clientes solo
    // seleccionan de esta lista en vez de escribir el valor a mano, evitando errores de tipeo que
    // Dataico rechazaría silenciosamente por venir en un formato/código inválido. "Category" es
    // literalmente el valor a enviar (código de texto para retenciones/impuestos/medio de pago, o
    // el número como texto para tarifas de IVA y plazos de pago); "Name" es la etiqueta legible.
    public class DataicoTaxCatalogItem
    {
        public Guid Id { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public TaxCatalogKind Kind { get; set; }
        // Solo aplica a Kind = Retention: la tarifa concreta de esta combinación (ej. RET_ICA al
        // 0.966%), para que el cliente elija de una lista en vez de escribir el número a mano.
        public decimal? Rate { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
