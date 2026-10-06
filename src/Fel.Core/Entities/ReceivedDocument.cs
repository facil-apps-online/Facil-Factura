using System;
using System.Collections.Generic;

namespace Fel.Core.Entities
{
    // Documento electrónico de un TERCERO (factura, nota crédito/débito de un proveedor) recibido
    // por nuestro Client — ya sea cargado a mano o bajado de su correo de facturación electrónica.
    // Es la base para generar los Eventos de Recepción (RADIAN) hacia la DIAN.
    public class ReceivedDocument
    {
        public Guid Id { get; set; }
        public Guid ClientId { get; set; }
        public Client Client { get; set; } = null!;

        // Sucursal cuyo buzón (o carga manual) recibió el documento.
        public Guid BranchId { get; set; }
        public Branch? Branch { get; set; }

        public string SourceType { get; set; } = "Manual"; // "Manual" | "Email"
        public string RawXml { get; set; } = string.Empty;

        public string Cufe { get; set; } = string.Empty;
        public string DocumentTypeCode { get; set; } = string.Empty; // 01=Factura, 91=NC, 92=ND
        public string IssuerTaxId { get; set; } = string.Empty;
        public string IssuerName { get; set; } = string.Empty;
        public string DocumentId { get; set; } = string.Empty; // cbc:ID completo (prefijo+número tal como viene en el XML del tercero)
        public DateTime IssueDate { get; set; }
        public decimal TotalAmount { get; set; }

        public DateTime ReceivedAt { get; set; }

        public ICollection<ReceivedDocumentEvent> Events { get; set; } = new List<ReceivedDocumentEvent>();
    }

    // Un evento RADIAN concreto ya disparado (o intentado) sobre un ReceivedDocument — un mismo
    // documento puede tener varios (Acuse, luego Recibo del bien, luego Aceptación), cada uno
    // independiente.
    public class ReceivedDocumentEvent
    {
        public Guid Id { get; set; }
        public Guid ReceivedDocumentId { get; set; }
        public ReceivedDocument ReceivedDocument { get; set; } = null!;

        public string EventCode { get; set; } = string.Empty; // 030 Acuse, 031 Reclamo, 032 Recibo del bien, 033 Aceptación
        public string Cude { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // "SENT" | "REJECTED"
        public string? DianResponseMessage { get; set; }
        public DateTime SentAt { get; set; }
    }
}
