using Fel.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Client.Models
{
    // Una fila de la lista de documentos recibidos: lo que se ve y el estado de cada evento. El XML, el PDF original y las respuestas técnicas de los
    // eventos no viajan; el historial de un evento se pide al abrirlo.
    public class ReceivedDocumentListItem
    {
        public Guid Id { get; set; }
        public string SourceType { get; set; } = string.Empty;
        public string Cufe { get; set; } = string.Empty;
        public string DocumentTypeCode { get; set; } = string.Empty;
        public string IssuerTaxId { get; set; } = string.Empty;
        public string IssuerName { get; set; } = string.Empty;
        public string DocumentId { get; set; } = string.Empty;
        public DateTime IssueDate { get; set; }
        public decimal TotalAmount { get; set; }
        public bool IsCredit { get; set; }
        public bool IsSupportDocument { get; set; }
        public DateTime ReceivedAt { get; set; }
        public List<ReceivedEventSummary> Events { get; set; } = new();
    }

    public class ReceivedEventSummary
    {
        public Guid Id { get; set; }
        public string EventCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public int? EventNumber { get; set; }
        // Recortado: en la fila solo se ve como ayuda al pasar el cursor; completo está en el historial del evento.
        public string? DianResponseMessage { get; set; }
    }

    public static class ReceivedDocumentList
    {
        // Un documento soporte trae InvoiceTypeCode = 05. Se pregunta en la base de datos para no traer el XML completo de cada documento (decenas de KB).
        public const string SupportDocumentXmlPattern = "%InvoiceTypeCode%>05</%InvoiceTypeCode>%";

        // Una página de la lista: busca por proveedor, NIT o número de documento (todas las palabras, en cualquier orden), ordena por la columna pedida y
        // corta. `query` ya viene limitado al cliente y a la sucursal.
        public static async Task<(List<ReceivedDocumentListItem> Items, int Total)> PageAsync(IQueryable<ReceivedDocument> query, string? text, string? sort, string? dir, int page, int pageSize)
        {
            pageSize = Math.Clamp(pageSize, 1, 100);
            page = Math.Max(page, 1);
            foreach (var term in (text ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var t = term;
                query = query.Where(d => d.IssuerName.Contains(t) || d.IssuerTaxId.Contains(t) || d.DocumentId.Contains(t));
            }

            var total = await query.CountAsync();
            var desc = !string.Equals(dir, "asc", StringComparison.OrdinalIgnoreCase);
            var ordered = (sort, desc) switch
            {
                ("issuer", false) => query.OrderBy(d => d.IssuerName),
                ("issuer", true) => query.OrderByDescending(d => d.IssuerName),
                ("documentId", false) => query.OrderBy(d => d.DocumentId),
                ("documentId", true) => query.OrderByDescending(d => d.DocumentId),
                ("total", false) => query.OrderBy(d => d.TotalAmount),
                ("total", true) => query.OrderByDescending(d => d.TotalAmount),
                ("source", false) => query.OrderBy(d => d.SourceType),
                ("source", true) => query.OrderByDescending(d => d.SourceType),
                (_, false) => query.OrderBy(d => d.IssueDate),
                _ => query.OrderByDescending(d => d.IssueDate)
            };

            var items = await ordered.ThenBy(d => d.Id).Skip((page - 1) * pageSize).Take(pageSize)
                .Select(d => new ReceivedDocumentListItem
                {
                    Id = d.Id,
                    SourceType = d.SourceType,
                    Cufe = d.Cufe,
                    DocumentTypeCode = d.DocumentTypeCode,
                    IssuerTaxId = d.IssuerTaxId,
                    IssuerName = d.IssuerName,
                    DocumentId = d.DocumentId,
                    IssueDate = d.IssueDate,
                    TotalAmount = d.TotalAmount,
                    IsCredit = d.IsCredit,
                    IsSupportDocument = d.DocumentTypeCode == "05" || EF.Functions.Like(d.RawXml, SupportDocumentXmlPattern),
                    ReceivedAt = d.ReceivedAt,
                    Events = d.Events.OrderByDescending(e => e.SentAt).Select(e => new ReceivedEventSummary
                    {
                        Id = e.Id,
                        EventCode = e.EventCode,
                        Status = e.Status,
                        SentAt = e.SentAt,
                        EventNumber = e.EventNumber,
                        DianResponseMessage = e.DianResponseMessage == null ? null : e.DianResponseMessage.Substring(0, 300)
                    }).ToList()
                })
                .ToListAsync();
            return (items, total);
        }
    }
}
