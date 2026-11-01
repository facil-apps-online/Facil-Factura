using System.Collections.Generic;
using System.Linq;
using Fel.Core.Entities;

namespace Fel.Core.Models
{
    // Calcula en el servidor los valores por línea y los totales de un documento, con las mismas reglas que el formulario del portal
    // (DocumentsPage.tsx: recalcItem y recalculateTotals), para los documentos que llegan sin totales (API de integración). No redondea:
    // igual que el portal, guarda los valores completos y el redondeo a dos decimales lo hace quien arma el XML. El ajuste de centavos del
    // cliente no se aplica aquí: se aplica después con RoundingAdjustment.Apply, igual que al guardar el borrador.
    public static class DocumentTotalsCalculator
    {
        // Categoría de la retención que se calcula sobre el IVA generado de la línea (ReteIVA); las demás, sobre la base de la línea.
        public const string IvaWithholdingCategory = "RET_IVA";

        // Base gravable de la línea: cantidad por valor unitario menos el descuento de esa línea.
        public static decimal LineBase(DocumentItem item) => item.Quantity * item.UnitPrice * (1 - item.DiscountRate / 100);

        // Deja en cada línea el IVA, los impuestos adicionales, las retenciones y el total, y en el documento el subtotal, el IVA y el total bruto
        // (subtotal + impuestos, sin restar retenciones). En el documento soporte no hay IVA (isSupportDocument = true).
        public static void Apply(Document document, IEnumerable<DocumentItem> items, bool isSupportDocument = false)
        {
            var lines = items as IReadOnlyCollection<DocumentItem> ?? items.ToList();
            decimal subtotal = 0m, tax = 0m;

            foreach (var item in lines)
            {
                var rate = !isSupportDocument && item.IvaTreatment == IvaTreatment.Gravado ? item.TaxRate : 0m;
                var lineBase = LineBase(item);

                item.TaxAmount = lineBase * rate / 100;

                foreach (var additional in item.AdditionalTaxes)
                {
                    additional.BaseAmount = lineBase;
                    additional.Amount = lineBase * additional.Rate / 100;
                }

                foreach (var retention in item.Retentions)
                {
                    retention.BaseAmount = retention.TaxCategory == IvaWithholdingCategory ? item.TaxAmount : lineBase;
                    retention.Amount = retention.BaseAmount * retention.Rate / 100;
                }

                var additionalTotal = item.AdditionalTaxes.Sum(t => t.Amount);
                item.TotalAmount = lineBase + item.TaxAmount + additionalTotal;

                subtotal += lineBase;
                tax += item.TaxAmount + additionalTotal;
            }

            document.Subtotal = subtotal;
            document.TaxAmount = tax;
            document.TotalAmount = subtotal + tax;
        }
    }
}
