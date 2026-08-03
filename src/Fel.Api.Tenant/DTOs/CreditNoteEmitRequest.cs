using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Fel.Api.Tenant.DTOs
{
    /// <summary>
    /// Estructura para emitir Notas Crédito (DIAN Código 91)
    /// </summary>
    public class CreditNoteEmitRequest
    {
        /// <summary>
        /// Prefijo y número de la Nota Crédito (Ej: NC-123)
        /// </summary>
        /// <example>NC-55</example>
        [Required]
        public string Number { get; set; } = string.Empty;

        /// <summary>
        /// CUFE o UUID de la Factura original a la que se le aplica la nota
        /// </summary>
        /// <example>9a8b7c6d5e4f3g2h1i0j9k8l7m6n5o4p3q2r1s0t...</example>
        [Required]
        public string ReferenceCufe { get; set; } = string.Empty;

        /// <summary>
        /// Motivo o Concepto de la Nota Crédito según catálogo de la DIAN (Ej: 2 para Anulación de factura, 1 para Devolución)
        /// </summary>
        /// <example>2</example>
        [Required]
        public string ReasonCode { get; set; } = string.Empty;

        /// <summary>
        /// Datos del Adquirente / Cliente original
        /// </summary>
        [Required]
        public InvoiceCustomerDto Customer { get; set; } = new InvoiceCustomerDto();

        /// <summary>
        /// Líneas devueltas o anuladas
        /// </summary>
        [Required]
        public List<InvoiceItemDto> Items { get; set; } = new List<InvoiceItemDto>();

        /// <example>100000.00</example>
        [Required]
        public decimal Subtotal { get; set; }

        /// <example>19000.00</example>
        [Required]
        public decimal TaxAmount { get; set; }

        /// <example>119000.00</example>
        [Required]
        public decimal TotalAmount { get; set; }
    }
}
