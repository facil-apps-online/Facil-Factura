using System;

namespace Fel.Core.Models
{
    // Evento de Recepción RADIAN (ApplicationResponse) — Anexo Técnico de Factura Electrónica v1.9,
    // numeral 6.5 (ver docs/dian-radian/). Esquema propio de la DIAN, no reusa UblInvoiceData.
    public class UblEventData
    {
        public string DocumentNumber { get; set; } = string.Empty; // Num_DE: consecutivo propio del evento
        public DateTime IssueDate { get; set; }
        public DateTime IssueTime { get; set; }
        public string Environment { get; set; } = "2";
        public string SoftwarePin { get; set; } = string.Empty;

        // SenderParty: quien genera el evento — nuestro Client (el que recibió la factura ajena).
        public string SenderTaxId { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;

        // ReceiverParty: quien recibe el evento — el emisor original de la factura referenciada.
        public string ReceiverTaxId { get; set; } = string.Empty;
        public string ReceiverName { get; set; } = string.Empty;

        public string ResponseCode { get; set; } = string.Empty; // 030/031/032/033
        public string ResponseDescription { get; set; } = string.Empty;

        // Documento referenciado (la factura/nota de un tercero sobre la que se dispara el evento).
        public string ReferencedDocumentId { get; set; } = string.Empty; // Prefijo+Número
        public string ReferencedCufe { get; set; } = string.Empty;
        public string ReferencedDocumentTypeCode { get; set; } = string.Empty; // 01/91/92
    }
}
