using Fel.Core.Entities;

namespace Fel.Api.Client.Models
{
    // Una fila de los listados de facturas y documentos soporte: solo lo que se ve en pantalla o lo que una acción de la fila necesita antes de pedir el
    // documento completo. El detalle, la edición y la respuesta de la DIAN se piden por id (GET {id}, GET {id}/dian-response) cuando el usuario los abre.
    public class DocumentListItem
    {
        public Guid Id { get; set; }
        public string TypeCode { get; set; } = string.Empty;
        public string? Prefix { get; set; }
        public string Number { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime IssueDate { get; set; }
        public string Currency { get; set; } = "COP";
        public decimal Subtotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        // Retenciones (las de cada ítem más las generales del documento) y total neto ya calculados: la fila no recorre los ítems para obtenerlos.
        public decimal RetentionsTotal { get; set; }
        public decimal NetTotal { get; set; }
        public string? PaymentMeans { get; set; }
        public string? PaymentMeansType { get; set; }
        public Guid? ResolutionId { get; set; }
        public DocumentListResolution? Resolution { get; set; }
        public DocumentListCustomer? Customer { get; set; }
        public Guid? ReferenceDocumentId { get; set; }
        public RelatedDocumentSummary? ReferenceDocument { get; set; }
        // El texto de la respuesta del integrador pesa hasta decenas de KB por documento: solo se avisa que existe.
        public bool HasDianResponse { get; set; }
        public List<RelatedDocumentSummary>? RelatedNotes { get; set; }
        public bool IsAnnulled { get; set; }
    }

    public class DocumentListResolution
    {
        public Guid Id { get; set; }
        public string? Prefix { get; set; }
    }

    public class DocumentListCustomer
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string IdentificationNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
    }

    public static class DocumentListQuery
    {
        // withReference: incluye el resumen del documento que la nota referencia (solo los listados de facturas lo muestran).
        public static IQueryable<DocumentListItem> Project(IQueryable<Document> query, bool withReference) =>
            query.Select(d => new DocumentListItem
            {
                Id = d.Id,
                TypeCode = d.TypeCode,
                Prefix = d.Prefix,
                Number = d.Number,
                Status = d.Status,
                IssueDate = d.IssueDate,
                Currency = d.Currency,
                Subtotal = d.Subtotal,
                TaxAmount = d.TaxAmount,
                TotalAmount = d.TotalAmount,
                // Retenciones generales: RET_IVA se calcula sobre el IVA y las demás sobre el subtotal; se suman las tarifas de cada grupo
                // (SQL Server no permite sumar expresiones que mezclan la columna de la retención con la del documento).
                RetentionsTotal =
                    (d.Items.SelectMany(i => i.Retentions).Sum(r => (decimal?)r.Amount) ?? 0m) +
                    d.TaxAmount * (d.GeneralRetentions.Where(g => g.TaxCategory == "RET_IVA").Sum(g => (decimal?)g.Rate) ?? 0m) / 100 +
                    d.Subtotal * (d.GeneralRetentions.Where(g => g.TaxCategory != "RET_IVA").Sum(g => (decimal?)g.Rate) ?? 0m) / 100,
                NetTotal = d.TotalAmount
                    - ((d.Items.SelectMany(i => i.Retentions).Sum(r => (decimal?)r.Amount) ?? 0m) +
                       d.TaxAmount * (d.GeneralRetentions.Where(g => g.TaxCategory == "RET_IVA").Sum(g => (decimal?)g.Rate) ?? 0m) / 100 +
                       d.Subtotal * (d.GeneralRetentions.Where(g => g.TaxCategory != "RET_IVA").Sum(g => (decimal?)g.Rate) ?? 0m) / 100)
                    - (d.GeneralDiscountAmount ?? 0m) - (d.RoundingAdjustmentAmount ?? 0m) + (d.GeneralChargeAmount ?? 0m),
                PaymentMeans = d.PaymentMeans,
                PaymentMeansType = d.PaymentMeansType,
                ResolutionId = d.ResolutionId,
                Resolution = d.Resolution == null ? null : new DocumentListResolution { Id = d.Resolution.Id, Prefix = d.Resolution.Prefix },
                Customer = d.Customer == null ? null : new DocumentListCustomer { Id = d.Customer.Id, Name = d.Customer.Name, IdentificationNumber = d.Customer.IdentificationNumber, Email = d.Customer.Email },
                ReferenceDocumentId = d.ReferenceDocumentId,
                ReferenceDocument = withReference && d.ReferenceDocument != null
                    ? new RelatedDocumentSummary { Id = d.ReferenceDocument.Id, Number = d.ReferenceDocument.Number, TypeCode = d.ReferenceDocument.TypeCode, Status = d.ReferenceDocument.Status, TotalAmount = d.ReferenceDocument.TotalAmount }
                    : null,
                HasDianResponse = d.DianResponseMessage != null && d.DianResponseMessage != ""
            });
    }
}
