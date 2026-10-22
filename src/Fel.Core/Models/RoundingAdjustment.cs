using System;
using System.Collections.Generic;
using System.Linq;
using Fel.Core.Entities;

namespace Fel.Core.Models
{
    // Ajuste de centavos de las facturas de un Client (Client.RoundingAdjustment). Hay entidades, como las del Estado, que no manejan centavos: la
    // factura debe quedar con un valor a pagar sin ellos. No se redondea: lo que sobra se quita con un descuento general (hacia abajo, nunca hacia arriba),
    // de modo que el XML —que es contra lo que concilia el cliente— lleva el valor a pagar ya ajustado. El descuento se calcula sobre el mismo valor a
    // pagar del XML (total con IVA + cargos − descuento general, antes de retenciones) y no cambia el IVA.
    public static class RoundingAdjustment
    {
        public const string None = "None";
        public const string Peso = "Peso";
        public const string Decena = "Decena";
        public const string Centena = "Centena";

        public static bool IsValid(string? value) => value is None or Peso or Decena or Centena;

        // Múltiplo al que se deja el valor a pagar; 0 = sin ajuste.
        public static decimal Unit(string? mode) => mode switch { Peso => 1m, Decena => 10m, Centena => 100m, _ => 0m };

        // Motivo del descuento, tal como se lee en el XML y en la representación gráfica.
        public static string Reason(string? mode) => mode switch
        {
            Peso => "Ajuste al peso",
            Decena => "Ajuste a la decena",
            Centena => "Ajuste a la centena",
            _ => string.Empty
        };

        // Lo que sobra de un valor a pagar sobre su múltiplo inferior: 1.234.567,89 → 0,89 (peso), 7,89 (decena), 67,89 (centena).
        public static decimal Compute(decimal payable, string? mode)
        {
            var unit = Unit(mode);
            if (unit == 0 || payable <= 0) return 0m;
            var rounded = Math.Round(payable, 2, MidpointRounding.AwayFromZero); // el XML informa los valores con dos decimales
            return rounded - Math.Floor(rounded / unit) * unit;
        }

        // Una factura de venta en pesos (no una nota, ni una factura en moneda extranjera) es la única que se ajusta.
        public static bool Applies(Document document) =>
            document.TypeCode.StartsWith("FE", StringComparison.OrdinalIgnoreCase)
            && document.ReferenceDocumentId == null
            && string.Equals(document.Currency, "COP", StringComparison.OrdinalIgnoreCase);

        // Calcula y deja en el documento el descuento del ajuste según el Client. Se llama al guardar el borrador y de nuevo al emitir, con los mismos
        // ítems que arman el XML (base de cada línea sin redondear + IVA de la línea). Sin ajuste (o si no aplica) lo deja en nulo.
        public static void Apply(Document document, IEnumerable<DocumentItem> items, string? mode)
        {
            document.RoundingAdjustmentAmount = null;
            document.RoundingAdjustmentReason = null;
            if (Unit(mode) == 0 || !Applies(document)) return;

            var gross = items.Sum(i => i.Quantity * i.UnitPrice * (1 - i.DiscountRate / 100) + i.TaxAmount + i.AdditionalTaxes.Sum(t => t.Amount));
            var payable = gross - (document.GeneralDiscountAmount ?? 0) + (document.GeneralChargeAmount ?? 0);
            var adjustment = Compute(payable, mode);
            if (adjustment <= 0) return;
            document.RoundingAdjustmentAmount = adjustment;
            document.RoundingAdjustmentReason = Reason(mode);
        }
    }
}
