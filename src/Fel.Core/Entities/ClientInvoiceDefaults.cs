using System;

namespace Fel.Core.Entities
{
    // Valores con los que arranca el formulario de "Nueva factura" de un Client, definidos por su administrador. Una fila por Client; todos
    // los campos son opcionales: vacío = sin valor por defecto. Los códigos son los mismos que guarda el documento (Category del catálogo).
    public class ClientInvoiceDefaults
    {
        public Guid Id { get; set; }

        public Guid ClientId { get; set; }
        public Client Client { get; set; } = null!;

        // Tipo de documento con el que abre el formulario (estándar, servicios públicos, exportación...).
        public Guid? DocumentTypeId { get; set; }
        public DocumentType? DocumentType { get; set; }

        // Medio de pago (Category de TaxCatalogKind.PaymentMeans).
        public string? PaymentMeans { get; set; }

        // Forma de pago (Category de TaxCatalogKind.FormaPago: DEBITO = contado, CREDITO = crédito) y, si es crédito, el plazo en días.
        public string? PaymentMeansType { get; set; }
        public int? PaymentTermDays { get; set; }

        // Moneda (Category de TaxCatalogKind.Currency); solo aplica si el Client tiene habilitada la moneda extranjera.
        public string? Currency { get; set; }

        // Observación general que se precarga en cada factura nueva.
        public string? Notes { get; set; }

        // Datos con los que arranca el formulario de un tercero nuevo de tipo Cliente (el usuario puede cambiarlos). Ciudad en Colombia:
        // el código DANE es la fuente y el nombre se copia del catálogo de municipios al guardar.
        public string? CustomerAddress { get; set; }
        public string? CustomerCityCode { get; set; }
        public string? CustomerCityName { get; set; }
        public string? CustomerPostalCode { get; set; }
        public string? CustomerPhone { get; set; }
        // Régimen y nivel tributario (Category de TaxCatalogKind.Regimen / TaxLevelCode) y país (alfa-2; vacío = Colombia). Con un país distinto de
        // Colombia no hay código DANE: la ciudad queda como texto.
        public string? CustomerRegimen { get; set; }
        public string? CustomerTaxLevelCode { get; set; }
        public string? CustomerCountryCode { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
