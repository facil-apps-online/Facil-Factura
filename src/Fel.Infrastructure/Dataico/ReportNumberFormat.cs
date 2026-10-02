using System.Collections.Concurrent;
using System.Globalization;
using Fel.Core.Models;

namespace Fel.Infrastructure.Dataico
{
    // Formato numérico de los PDF según Client.DecimalSeparator: "." = punto decimal y coma de miles
    // (1,234,567.89 — el estándar y el valor por defecto); "," = coma decimal y punto de miles
    // (1.234.567,89). Reemplaza el es-CO fijo que usaban los mappers de factura, documento soporte y
    // nómina; es lo mismo que aplica el portal de clientes en pantalla (client-web/lib/numberFormat.ts).
    public static class ReportNumberFormat
    {
        private static readonly ConcurrentDictionary<string, NumberFormatInfo> Cache = new();

        public static NumberFormatInfo For(string? decimalSeparator)
        {
            var sep = DecimalSeparators.IsValid(decimalSeparator) ? decimalSeparator! : DecimalSeparators.Point;
            return Cache.GetOrAdd(sep, s =>
            {
                var info = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
                info.NumberDecimalSeparator = s;
                info.NumberGroupSeparator = DecimalSeparators.GroupFor(s);
                info.PercentDecimalSeparator = s;
                info.PercentGroupSeparator = DecimalSeparators.GroupFor(s);
                return info;
            });
        }
    }
}
