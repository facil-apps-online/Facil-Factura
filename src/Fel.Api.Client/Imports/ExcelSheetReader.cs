using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ClosedXML.Excel;

namespace Fel.Api.Client.Imports
{
    public sealed class SheetRow
    {
        public int Row { get; init; }
        private readonly Dictionary<string, string> _cells;
        public SheetRow(int row, Dictionary<string, string> cells) { Row = row; _cells = cells; }
        // Valor de la columna por su nombre canónico; vacío si la columna no existe o la celda está vacía.
        public string Get(string key) => _cells.TryGetValue(key, out var v) ? v : string.Empty;
    }

    public sealed class SheetData
    {
        public List<SheetRow> Rows { get; } = new();
        public List<string> FileErrors { get; } = new();
    }

    // Lee la primera hoja de un Excel reconociendo las columnas por NOMBRE (sin importar el orden, las tildes, las mayúsculas ni los guiones bajos).
    // aliases: nombre canónico -> otros nombres con que puede venir. Las columnas desconocidas se ignoran.
    public static class ExcelSheetReader
    {
        public const int MaxRows = 5000;
        public const long MaxBytes = 10 * 1024 * 1024;

        public static string NormalizeHeader(string? header)
        {
            if (string.IsNullOrWhiteSpace(header)) return string.Empty;
            var sb = new StringBuilder();
            foreach (var ch in header.Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
                if (ch == '%') continue; // "IVA%" -> "IVA"
                sb.Append(char.IsLetterOrDigit(ch) ? char.ToUpperInvariant(ch) : '_');
            }
            return string.Join("_", sb.ToString().Split('_', StringSplitOptions.RemoveEmptyEntries));
        }

        public static SheetData Read(Stream stream, IReadOnlyDictionary<string, string[]> aliases, IReadOnlyCollection<string> required)
        {
            var data = new SheetData();
            XLWorkbook workbook;
            try { workbook = new XLWorkbook(stream); }
            catch (Exception) { data.FileErrors.Add("No se pudo leer el archivo: debe ser un Excel (.xlsx)."); return data; }

            using (workbook)
            {
                var sheet = workbook.Worksheets.FirstOrDefault(s => s.Visibility == XLWorksheetVisibility.Visible) ?? workbook.Worksheets.FirstOrDefault();
                if (sheet == null || sheet.LastRowUsed() == null) { data.FileErrors.Add("El archivo no tiene datos."); return data; }

                // nombre normalizado de la columna del archivo -> nombre canónico
                var lookup = new Dictionary<string, string>();
                foreach (var (canonical, names) in aliases)
                {
                    lookup[NormalizeHeader(canonical)] = canonical;
                    foreach (var n in names) lookup[NormalizeHeader(n)] = canonical;
                }

                var headerRow = sheet.FirstRowUsed();
                var columns = new Dictionary<int, string>();
                foreach (var cell in headerRow.CellsUsed())
                {
                    var key = NormalizeHeader(cell.GetString());
                    if (lookup.TryGetValue(key, out var canonical) && !columns.ContainsValue(canonical)) columns[cell.Address.ColumnNumber] = canonical;
                }

                var missing = required.Where(r => !columns.ContainsValue(r)).ToList();
                if (missing.Count > 0)
                {
                    data.FileErrors.Add("Faltan columnas obligatorias: " + string.Join(", ", missing) + ". Descarga la plantilla para ver los nombres.");
                    return data;
                }

                var lastRow = sheet.LastRowUsed()!.RowNumber();
                if (lastRow - headerRow.RowNumber() > MaxRows)
                {
                    data.FileErrors.Add($"El archivo tiene más de {MaxRows:N0} filas. Divídelo en varios archivos.");
                    return data;
                }

                for (var r = headerRow.RowNumber() + 1; r <= lastRow; r++)
                {
                    var cells = new Dictionary<string, string>();
                    var any = false;
                    foreach (var (col, canonical) in columns)
                    {
                        var text = CellText(sheet.Cell(r, col));
                        if (text.Length > 0) any = true;
                        cells[canonical] = text;
                    }
                    if (any) data.Rows.Add(new SheetRow(r, cells));
                }
                if (data.Rows.Count == 0) data.FileErrors.Add("El archivo tiene los encabezados pero ninguna fila de datos.");
            }
            return data;
        }

        // Texto de una celda tal como la ve quien la llenó: los números sin notación científica ni ceros de más, los booleanos como TRUE/FALSE.
        private static string CellText(IXLCell cell)
        {
            if (cell.IsEmpty()) return string.Empty;
            var v = cell.Value;
            return v.Type switch
            {
                XLDataType.Number => v.GetNumber().ToString("0.###############", CultureInfo.InvariantCulture),
                XLDataType.Boolean => v.GetBoolean() ? "TRUE" : "FALSE",
                XLDataType.DateTime => v.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                _ => v.ToString().Trim()
            };
        }
    }

    public static class ImportText
    {
        public static string Collapse(string? value) =>
            string.Join(" ", (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        // Sin tildes y en mayúsculas, solo letras y números separados por un espacio: para comparar nombres de ciudades, categorías, etc.
        public static string Fold(string? value)
        {
            var sb = new StringBuilder();
            foreach (var ch in (value ?? string.Empty).Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
                sb.Append(char.IsLetterOrDigit(ch) ? char.ToUpperInvariant(ch) : ' ');
            }
            return Collapse(sb.ToString());
        }

        // TRUE/FALSE, SI/NO, 1/0, X. Null = vacío o no reconocido.
        public static bool? ParseBool(string? value)
        {
            var v = Fold(value);
            if (v.Length == 0) return null;
            if (v is "TRUE" or "SI" or "S" or "YES" or "Y" or "1" or "X" or "VERDADERO") return true;
            if (v is "FALSE" or "NO" or "N" or "0" or "FALSO") return false;
            return null;
        }

        // Número con punto o coma decimal ("910000", "910.000,50", "1,5"). Null si no se puede leer.
        public static decimal? ParseDecimal(string? value)
        {
            var v = (value ?? string.Empty).Trim().Replace("$", "").Replace(" ", "");
            if (v.Length == 0) return null;
            var lastComma = v.LastIndexOf(',');
            var lastDot = v.LastIndexOf('.');
            if (lastComma >= 0 && lastDot >= 0) v = lastComma > lastDot ? v.Replace(".", "").Replace(',', '.') : v.Replace(",", "");
            else if (lastComma >= 0) v = v.Replace(',', '.');
            return decimal.TryParse(v, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var d) ? d : null;
        }

        public static string DigitsOnly(string? value) => new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
    }
}
