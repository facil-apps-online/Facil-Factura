using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Fel.Api.Tenant.DTOs
{
    /// <summary>
    /// Estructura para emitir una Factura Electrónica de Venta (DIAN Código 01, AIU, Mandatos, etc.)
    /// </summary>
    public class InvoiceEmitRequest
    {
        /// <summary>
        /// Prefijo y número de la factura (Ej: FE-123)
        /// </summary>
        /// <example>FE-1024</example>
        [Required]
        public string Number { get; set; } = string.Empty;

        /// <summary>
        /// Código del tipo de documento DIAN (Ej: 01 para Factura Estándar, 02 para Exportación). 
        /// Por defecto asume 01 si no se envía.
        /// </summary>
        /// <example>01</example>
        public string DocumentTypeCode { get; set; } = "01";

        /// <summary>
        /// Datos del Adquirente / Cliente
        /// </summary>
        [Required]
        public InvoiceCustomerDto Customer { get; set; } = new InvoiceCustomerDto();

        /// <summary>
        /// Líneas de la factura (Productos o Servicios)
        /// </summary>
        [Required]
        public List<InvoiceItemDto> Items { get; set; } = new List<InvoiceItemDto>();

        /// <summary>
        /// Subtotal antes de impuestos
        /// </summary>
        /// <example>100000.00</example>
        [Required]
        public decimal Subtotal { get; set; }

        /// <summary>
        /// Total de impuestos aplicados
        /// </summary>
        /// <example>19000.00</example>
        [Required]
        public decimal TaxAmount { get; set; }

        /// <summary>
        /// Total a pagar
        /// </summary>
        /// <example>119000.00</example>
        [Required]
        public decimal TotalAmount { get; set; }

        /// <summary>
        /// Observaciones adicionales a imprimir en la representación gráfica
        /// </summary>
        /// <example>Condiciones de pago a 30 días.</example>
        public string Notes { get; set; } = string.Empty;

        /// <summary>
        /// Objeto opcional para Extensiones específicas como RIPS de Minsalud, Mandatos o AIU
        /// </summary>
        public object? SectorExtensionData { get; set; }
    }

    public class InvoiceCustomerDto
    {
        /// <example>900123456</example>
        [Required]
        public string IdentificationNumber { get; set; } = string.Empty;

        /// <example>Empresa Cliente S.A.S</example>
        [Required]
        public string Name { get; set; } = string.Empty;

        /// <example>recepcion@cliente.com</example>
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    public class InvoiceItemDto
    {
        /// <example>PRD-001</example>
        [Required]
        public string Code { get; set; } = string.Empty;

        /// <example>Desarrollo de Software a la Medida</example>
        [Required]
        public string Name { get; set; } = string.Empty;

        /// <example>1</example>
        [Required]
        public decimal Quantity { get; set; }

        /// <example>100000.00</example>
        [Required]
        public decimal UnitPrice { get; set; }

        /// <example>19</example>
        public decimal TaxRate { get; set; }
    }
}
