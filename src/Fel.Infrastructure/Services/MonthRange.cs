using System;
using System.Collections.Generic;
using System.Globalization;

namespace Fel.Infrastructure.Services
{
    // Rango de meses ("AAAA-MM" a "AAAA-MM") de las vistas de consumo del tenant y del superadmin. El cobro se calcula mes a mes, así que un
    // rango siempre es una lista de meses completos.
    public static class MonthRange
    {
        public const int MaxMonths = 24;

        // Devuelve el error (o null) y la lista de meses. Sin extremos usa el mes en curso; con uno solo, ese mes.
        public static string? Parse(string? from, string? to, out List<(int Year, int Month)> months)
        {
            months = new List<(int, int)>();
            var now = DateTime.UtcNow;
            var start = new DateTime(now.Year, now.Month, 1);
            var end = start;
            if (!string.IsNullOrWhiteSpace(from) && !DateTime.TryParseExact(from, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out start))
                return "El mes inicial no es válido (use AAAA-MM).";
            if (!string.IsNullOrWhiteSpace(to) && !DateTime.TryParseExact(to, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out end))
                return "El mes final no es válido (use AAAA-MM).";
            if (string.IsNullOrWhiteSpace(from) && !string.IsNullOrWhiteSpace(to)) start = end;
            if (string.IsNullOrWhiteSpace(to) && !string.IsNullOrWhiteSpace(from)) end = start;
            if (end < start) return "El mes final no puede ser anterior al inicial.";
            for (var d = start; d <= end; d = d.AddMonths(1)) months.Add((d.Year, d.Month));
            return months.Count > MaxMonths ? $"El rango puede tener hasta {MaxMonths} meses." : null;
        }

        public static string Key(int year, int month) => $"{year:0000}-{month:00}";

        public static string Label(int year, int month) => new DateTime(year, month, 1).ToString("MMMM yyyy", new CultureInfo("es-CO"));
    }
}
