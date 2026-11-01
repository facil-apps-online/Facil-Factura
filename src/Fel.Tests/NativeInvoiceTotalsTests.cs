using Fel.Core.Entities;
using Fel.Core.Models;
using Fel.Infrastructure.Dian;
using Fel.Infrastructure.Services;

namespace Fel.Tests
{
    // Totales del XML nativo (DianDocumentMapper.BuildInvoiceData, el mismo camino de facturas y notas del portal).
    //
    // Grupo "Fijan lo que ya funciona": con valores exactos a dos decimales el resultado no debe cambiar al unificar el redondeo.
    // Grupo "Redondeo DIAN": el encabezado debe ser la suma de las líneas ya redondeadas (Anexo Técnico 5.2.1), y cada valor
    // debe quedar con dos decimales, porque así lo valida la DIAN (FAU04, FAS01b...).
    public class NativeInvoiceTotalsTests
    {
        private static UblInvoiceData Build(Document? document = null, params DocumentItem[] items)
        {
            var client = new Client { TaxId = "900123456" };
            var customer = new Customer { Name = "Cliente", IdentificationNumber = "800111222", IdentificationType = "31" };
            var resolution = new Resolution { Prefix = "SETT" };
            var location = new EmitterLocation("Calle 1", "Bogota", null, "300", "a@b.co", null, null);
            document ??= new Document { TypeCode = "FE-STD", Number = "1", IssueDate = new DateTime(2026, 11, 1) };
            return DianDocumentMapper.BuildInvoiceData(
                document, customer, items, resolution, client, location, new Dictionary<string, DianMunicipality>());
        }

        private static DocumentItem Item(decimal qty, decimal price, decimal taxRate = 19, decimal discount = 0, IvaTreatment iva = IvaTreatment.Gravado)
        {
            var item = new DocumentItem { Quantity = qty, UnitPrice = price, DiscountRate = discount, IvaTreatment = iva, TaxRate = taxRate, Code = "A", Name = "Item" };
            // El portal guarda el IVA de la línea sin redondear: se simula igual con el calculador del servidor.
            item.TaxAmount = DocumentTotalsCalculator.LineBase(item) * (iva == IvaTreatment.Gravado ? taxRate : 0m) / 100;
            return item;
        }

        private static bool HasAtMostTwoDecimals(decimal value) => value == Math.Round(value, 2);

        // ----- Fijan lo que ya funciona -----

        [Fact]
        public void ExactAmounts_SingleLine()
        {
            var data = Build(null, Item(1, 5_000_000m));

            Assert.Equal(5_000_000m, data.LineExtensionAmount);
            Assert.Equal(5_000_000m, data.TaxExclusiveAmount);
            Assert.Equal(5_950_000m, data.TaxInclusiveAmount);
            Assert.Equal(5_950_000m, data.PayableAmount);
            var iva = Assert.Single(data.Taxes);
            Assert.Equal(950_000m, iva.TaxAmount);
            Assert.Equal(5_000_000m, iva.TaxableAmount);
        }

        [Fact]
        public void ExactAmounts_LineDiscount()
        {
            var data = Build(null, Item(2, 100m, discount: 10));

            Assert.Equal(180m, data.LineExtensionAmount);
            Assert.Equal(34.2m, data.Taxes.Single().TaxAmount);
            Assert.Equal(214.2m, data.PayableAmount);
        }

        [Fact]
        public void GeneralDiscountChargeAndRoundingAdjustment_AdjustPayable()
        {
            var document = new Document
            {
                TypeCode = "FE-STD", Number = "1", IssueDate = new DateTime(2026, 11, 1),
                GeneralDiscountAmount = 100m, GeneralChargeAmount = 50m, RoundingAdjustmentAmount = 0.2m
            };

            var data = Build(document, Item(1, 1000m));

            Assert.Equal(1190m, data.TaxInclusiveAmount);
            Assert.Equal(1190m - 100m - 0.2m + 50m, data.PayableAmount);
        }

        [Fact]
        public void ExemptAndExcluded_StillReportTheirTaxBase()
        {
            var data = Build(null, Item(1, 1000m, 19), Item(1, 500m, iva: IvaTreatment.Exento), Item(1, 200m, iva: IvaTreatment.Excluido));

            Assert.Equal(1700m, data.LineExtensionAmount);
            Assert.Equal(190m, data.Taxes.Sum(t => t.TaxAmount));
            Assert.Equal(1700m, data.Taxes.Sum(t => t.TaxableAmount));
            Assert.Equal(1890m, data.PayableAmount);
        }

        // ----- Redondeo DIAN -----

        [Fact]
        public void LineValues_AreRoundedWithTheDianRule()
        {
            // 481.275: el dígito siguiente es 5 y le sigue un 0, la regla del anexo lo deja en 481.27 (no 481.28).
            var data = Build(null, Item(1, 481.275m, taxRate: 0, iva: IvaTreatment.Excluido));

            Assert.Equal(481.27m, data.Lines.Single().LineExtensionAmount);
            Assert.Equal(481.27m, data.LineExtensionAmount);
        }

        [Fact]
        public void HeaderEqualsSumOfRoundedLines()
        {
            // Tres líneas de 10.005: cada una queda en 10.00 y el encabezado debe sumar lo mismo que las líneas.
            var data = Build(null, Item(1, 10.005m), Item(1, 10.005m), Item(1, 10.005m));

            Assert.Equal(data.Lines.Sum(l => l.LineExtensionAmount), data.LineExtensionAmount);
            Assert.Equal(data.Lines.Sum(l => l.Taxes.Sum(t => t.TaxAmount)), data.Taxes.Sum(t => t.TaxAmount));
            Assert.Equal(data.LineExtensionAmount + data.Taxes.Sum(t => t.TaxAmount), data.TaxInclusiveAmount);
        }

        [Fact]
        public void AllReportedAmounts_HaveTwoDecimals()
        {
            var data = Build(null, Item(3, 33.333333m), Item(7, 12.345678m, discount: 12.5m), Item(2, 0.015m, taxRate: 5));

            Assert.True(HasAtMostTwoDecimals(data.LineExtensionAmount));
            Assert.True(HasAtMostTwoDecimals(data.TaxExclusiveAmount));
            Assert.True(HasAtMostTwoDecimals(data.TaxInclusiveAmount));
            Assert.True(HasAtMostTwoDecimals(data.PayableAmount));
            Assert.All(data.Lines, l =>
            {
                Assert.True(HasAtMostTwoDecimals(l.LineExtensionAmount));
                Assert.All(l.Taxes, t => { Assert.True(HasAtMostTwoDecimals(t.TaxAmount)); Assert.True(HasAtMostTwoDecimals(t.TaxableAmount)); });
            });
            Assert.All(data.Taxes, t => { Assert.True(HasAtMostTwoDecimals(t.TaxAmount)); Assert.True(HasAtMostTwoDecimals(t.TaxableAmount)); });
            Assert.Equal(data.Lines.Sum(l => l.LineExtensionAmount), data.LineExtensionAmount);
        }

        [Fact]
        public void Iva_IsRoundedPerLine_AndHeaderSumsTheRoundedLines()
        {
            // 3 x 33.33 = 99.99 con IVA 19% -> 18.9981 -> 19.00 por línea.
            var data = Build(null, Item(3, 33.33m), Item(3, 33.33m));

            Assert.Equal(19.00m, data.Lines[0].Taxes.Single().TaxAmount);
            Assert.Equal(38.00m, data.Taxes.Single().TaxAmount);
            Assert.Equal(199.98m, data.LineExtensionAmount);
            Assert.Equal(237.98m, data.TaxInclusiveAmount);
        }
    }
}
