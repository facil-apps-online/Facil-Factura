using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Fel.Api.Tenant.DTOs
{
    /// <summary>
    /// Solicitud universal para emisión de documentos electrónicos.
    /// Soporta múltiples países y entidades mediante ProviderOptions.
    /// </summary>
    public class EmitDocumentRequest
    {
        /// <summary>
        /// Número del documento (Ej: FE-123, SETT-990)
        /// </summary>
        /// <example>FE-1024</example>
        [Required]
        public string Number { get; set; } = string.Empty;

        /// <summary>
        /// Datos del Cliente o Adquirente del documento
        /// </summary>
        [Required]
        public DocumentCustomerDto Customer { get; set; } = new DocumentCustomerDto();

        /// <summary>
        /// Detalle de líneas del documento (productos o servicios)
        /// </summary>
        [Required]
        public List<DocumentItemDto> Items { get; set; } = new List<DocumentItemDto>();

        /// <summary>
        /// Subtotal antes de impuestos
        /// </summary>
        /// <example>100000.00</example>
        [Required]
        public decimal Subtotal { get; set; }

        /// <summary>
        /// Monto total de impuestos aplicados
        /// </summary>
        /// <example>19000.00</example>
        [Required]
        public decimal TaxAmount { get; set; }

        /// <summary>
        /// Total a pagar (Subtotal + Impuestos)
        /// </summary>
        /// <example>119000.00</example>
        [Required]
        public decimal TotalAmount { get; set; }

        /// <summary>
        /// Notas adicionales para imprimir en el PDF o incluir en el XML
        /// </summary>
        /// <example>Pago a 30 días. Observaciones de entrega.</example>
        public string Notes { get; set; } = string.Empty;

        /// <summary>
        /// Datos específicos requeridos por la entidad local (Ej: RIPS para Minsalud, Mandatos para DIAN)
        /// </summary>
        /// <example>
        /// {
        ///   "DocumentTypeCode": "01",
        ///   "OperationType": "10",
        ///   "RipsData": {
        ///      "PatientId": "CC-1234567",
        ///      "CopayAmount": 5000
        ///   }
        /// }
        /// </example>
        public object? ProviderOptions { get; set; }
    }

    /// <summary>
    /// Información del Adquirente (Cliente, Paciente, etc.)
    /// </summary>
    public class DocumentCustomerDto
    {
        /// <summary>
        /// Número de identificación (NIT, CC, RUT, etc.)
        /// </summary>
        /// <example>900123456</example>
        [Required]
        public string IdentificationNumber { get; set; } = string.Empty;

        /// <summary>
        /// Nombre comercial o Razón Social
        /// </summary>
        /// <example>Empresa Adquirente S.A.S</example>
        [Required]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Correo electrónico para entrega del documento XML/PDF
        /// </summary>
        /// <example>recepcion@adquirente.com</example>
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Código del régimen tributario o responsabilidad (Ej: O-47 para DIAN, 601 para SAT)
        /// </summary>
        /// <example>O-47</example>
        public string TaxRegime { get; set; } = string.Empty;
    }

    /// <summary>
    /// Detalle de una línea de factura (Producto o Servicio)
    /// </summary>
    public class DocumentItemDto
    {
        /// <summary>
        /// Código SKU o Referencia del producto/servicio
        /// </summary>
        /// <example>SRV-001</example>
        [Required]
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Nombre descriptivo del ítem
        /// </summary>
        /// <example>Servicios de Consultoría TI</example>
        [Required]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Cantidad vendida
        /// </summary>
        /// <example>1</example>
        [Required]
        public decimal Quantity { get; set; }

        /// <summary>
        /// Precio unitario (sin impuestos)
        /// </summary>
        /// <example>100000.00</example>
        [Required]
        public decimal UnitPrice { get; set; }

        /// <summary>
        /// Porcentaje de impuesto principal aplicado (Ej: 19 para IVA 19%, 16 para IVA SAT)
        /// </summary>
        /// <example>19</example>
        public decimal TaxRate { get; set; }

        /// <summary>
        /// Opciones específicas de la línea (Ej: AIU discriminado por línea, impuestos locales)
        /// </summary>
        /// <example>
        /// {
        ///   "IsAIU": true,
        ///   "AiuBase": 10000
        /// }
        /// </example>
        public object? ItemOptions { get; set; }
    }
}
