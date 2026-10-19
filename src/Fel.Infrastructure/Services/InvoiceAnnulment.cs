using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Services
{
    // Una factura está anulada cuando las notas crédito aceptadas por la DIAN que la referencian suman, como mínimo, su total: una
    // nota completa o varias parciales que al final cubren el valor de la factura. Una anulación parcial no cuenta; tampoco las
    // notas débito ni las rechazadas o pendientes. Se calcula cada vez (no se guarda), así que sigue a las notas que se acepten después.
    public static class InvoiceAnnulment
    {
        // Dato reservado que Facil Reports lee para dibujar el texto en diagonal sobre cada página (ver FastReportGenerator.WatermarkKey).
        public const string WatermarkKey = "__Watermark";
        public const string WatermarkText = "ANULADA";

        // Tolerancia de redondeo entre el total de la factura y la suma de sus notas.
        private const decimal Tolerance = 0.01m;

        public static bool IsAnnulled(string typeCode, decimal invoiceTotal, IEnumerable<RelatedDocumentSummary>? relatedNotes)
        {
            if (relatedNotes == null || invoiceTotal <= 0 || !typeCode.StartsWith("FE")) return false;
            var credited = relatedNotes.Where(n => n.TypeCode == "NC" && n.Status == "APPROVED").Sum(n => n.TotalAmount);
            return credited >= invoiceTotal - Tolerance;
        }

        public static async Task<bool> IsAnnulledAsync(FelDbContext db, Document invoice)
        {
            if (!invoice.TypeCode.StartsWith("FE") || invoice.TotalAmount <= 0) return false;
            var credited = await db.Documents.AsNoTracking()
                .Where(d => d.ClientId == invoice.ClientId && d.TypeCode == "NC" && d.Status == "APPROVED" && d.ReferenceDocumentId == invoice.Id)
                .SumAsync(d => (decimal?)d.TotalAmount) ?? 0m;
            return credited >= invoice.TotalAmount - Tolerance;
        }
    }
}
