using Fel.Core.Entities;
using Fel.Core.Models;

namespace Fel.Tests
{
    // Los valores esperados salen de las fórmulas de DocumentsPage.tsx (recalcItem / recalculateTotals): el servidor debe dar lo mismo que el portal.
    public class DocumentTotalsCalculatorTests
    {
        private static DocumentItem Item(decimal qty, decimal price, decimal discount = 0, IvaTreatment iva = IvaTreatment.Gravado, decimal rate = 19) =>
            new() { Quantity = qty, UnitPrice = price, DiscountRate = discount, IvaTreatment = iva, TaxRate = rate };

        [Fact]
        public void GravadoLine_ComputesIvaAndTotals()
        {
            var doc = new Document();
            var items = new[] { Item(1, 5_000_000m) };

            DocumentTotalsCalculator.Apply(doc, items);

            Assert.Equal(950_000m, items[0].TaxAmount);
            Assert.Equal(5_950_000m, items[0].TotalAmount);
            Assert.Equal(5_000_000m, doc.Subtotal);
            Assert.Equal(950_000m, doc.TaxAmount);
            Assert.Equal(5_950_000m, doc.TotalAmount);
        }

        [Fact]
        public void LineDiscount_ReducesBaseBeforeTax()
        {
            var doc = new Document();
            var items = new[] { Item(2, 100m, discount: 10) }; // base 180

            DocumentTotalsCalculator.Apply(doc, items);

            Assert.Equal(34.2m, items[0].TaxAmount);
            Assert.Equal(180m, doc.Subtotal);
            Assert.Equal(214.2m, doc.TotalAmount);
        }

        [Theory]
        [InlineData(IvaTreatment.Exento)]
        [InlineData(IvaTreatment.Excluido)]
        public void ExemptAndExcluded_HaveNoIva(IvaTreatment treatment)
        {
            var doc = new Document();
            var items = new[] { Item(1, 1000m, iva: treatment, rate: 19) };

            DocumentTotalsCalculator.Apply(doc, items);

            Assert.Equal(0m, items[0].TaxAmount);
            Assert.Equal(1000m, doc.TotalAmount);
        }

        [Fact]
        public void SupportDocument_HasNoIva()
        {
            var doc = new Document();
            var items = new[] { Item(1, 1000m) };

            DocumentTotalsCalculator.Apply(doc, items, isSupportDocument: true);

            Assert.Equal(0m, items[0].TaxAmount);
            Assert.Equal(1000m, doc.TotalAmount);
        }

        [Fact]
        public void AdditionalTax_UsesLineBase_AndAddsToTaxTotal()
        {
            var doc = new Document();
            var item = Item(1, 1000m);
            item.AdditionalTaxes.Add(new DocumentItemTax { TaxCategory = "INC", Rate = 8 });

            DocumentTotalsCalculator.Apply(doc, new[] { item });

            Assert.Equal(80m, item.AdditionalTaxes.Single().Amount);
            Assert.Equal(1000m, item.AdditionalTaxes.Single().BaseAmount);
            Assert.Equal(1270m, item.TotalAmount);
            Assert.Equal(270m, doc.TaxAmount);
            Assert.Equal(1270m, doc.TotalAmount);
        }

        [Fact]
        public void Retentions_ReteIvaOnIva_OthersOnLineBase_NotSubtractedFromGross()
        {
            var doc = new Document();
            var item = Item(1, 1000m);
            item.Retentions.Add(new DocumentRetention { TaxCategory = "RET_FUENTE", Rate = 4 });
            item.Retentions.Add(new DocumentRetention { TaxCategory = "RET_IVA", Rate = 15 });

            DocumentTotalsCalculator.Apply(doc, new[] { item });

            Assert.Equal(40m, item.Retentions.Single(r => r.TaxCategory == "RET_FUENTE").Amount);
            var reteIva = item.Retentions.Single(r => r.TaxCategory == "RET_IVA");
            Assert.Equal(190m, reteIva.BaseAmount);
            Assert.Equal(28.5m, reteIva.Amount);
            Assert.Equal(1190m, doc.TotalAmount); // las retenciones no reducen el total bruto
        }

        [Fact]
        public void MultipleLines_AreSummed()
        {
            var doc = new Document();
            var items = new[] { Item(1, 1000m), Item(3, 200m, rate: 5), Item(1, 50m, iva: IvaTreatment.Excluido) };

            DocumentTotalsCalculator.Apply(doc, items);

            Assert.Equal(1650m, doc.Subtotal);
            Assert.Equal(190m + 30m, doc.TaxAmount);
            Assert.Equal(1870m, doc.TotalAmount);
        }
    }
}
