using System.Collections.Generic;
using System.Linq;

namespace Fel.Api.Client.Imports
{
    // Estados de una fila en la vista previa de una carga masiva.
    public static class ImportStatus
    {
        public const string Create = "create";       // se crearía
        public const string Duplicate = "duplicate"; // ya existe: se omite
        public const string Error = "error";         // no se puede crear: se omite
        public const string Decision = "decision";   // falta que el usuario elija; sin elegir, se omite
        public const string Skipped = "skipped";     // el usuario eligió omitirla
    }

    public sealed record ImportOption(string Value, string Label);

    // Lo que el usuario debe resolver en una fila (por ejemplo, el rol de un tercero cuya categoría no se reconoce).
    public sealed class ImportDecision
    {
        public string Field { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
        public List<ImportOption> Options { get; init; } = new();
        public string? Suggested { get; init; }
    }

    public sealed class ImportRowPreview
    {
        public int Row { get; set; }
        public string Status { get; set; } = ImportStatus.Create;
        // Errores: impiden crear la fila. Avisos: se crea, pero conviene revisarlo.
        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();
        // Notas: lo que el sistema completó o ajustó por su cuenta (p. ej. la dirección tomada del cliente); no es un problema.
        public List<string> Notes { get; } = new();
        // Lo que quedaría guardado (campos principales), en el orden en que la pantalla los muestra.
        public Dictionary<string, string?> Values { get; } = new();
        public ImportDecision? Decision { get; set; }
    }

    public sealed class ImportPreview
    {
        public int TotalRows { get; set; }
        public int ToCreate { get; set; }
        public int Duplicates { get; set; }
        public int Errors { get; set; }
        public int NeedsDecision { get; set; }
        public int Skipped { get; set; }
        public int WithWarnings { get; set; }
        // Problemas del archivo completo (columnas que faltan, demasiadas filas...): no se muestra la tabla.
        public List<string> FileErrors { get; } = new();
        public List<ImportRowPreview> Rows { get; } = new();

        public void Tally()
        {
            TotalRows = Rows.Count;
            ToCreate = Rows.Count(r => r.Status == ImportStatus.Create);
            Duplicates = Rows.Count(r => r.Status == ImportStatus.Duplicate);
            Errors = Rows.Count(r => r.Status == ImportStatus.Error);
            NeedsDecision = Rows.Count(r => r.Status == ImportStatus.Decision);
            Skipped = Rows.Count(r => r.Status == ImportStatus.Skipped);
            WithWarnings = Rows.Count(r => r.Status == ImportStatus.Create && r.Warnings.Count > 0);
        }
    }

    // Lo que el usuario eligió en la vista previa y se vuelve a enviar al confirmar. Las claves de Decisions son el número de fila del Excel.
    public sealed class BulkImportOptions
    {
        // Para las filas sin dirección o ciudad: "client" (la del cliente), "default" (la configurada por defecto para terceros nuevos) o "blank".
        public string AddressMode { get; set; } = "blank";
        public Dictionary<int, Dictionary<string, string>> Decisions { get; set; } = new();
    }

    public static class BulkImportLimits
    {
        // Largos de las columnas de la base (Products y Customers). Una fila que los supera no se guarda: se avisa en la vista previa.
        public const int Name = 250;          // terceros
        public const int ProductName = 1000;  // productos
        public const int Code = 100;           // a la DIAN se envían los primeros 50 (ver BaseUblStrategy.DianItemCode)
        public const int DianCode = 50;
        public const int IdentificationNumber = 50;

        public static ImportOption[] NameTooLong(int max) => new ImportOption[]
        {
            new("truncate", $"Cortar el nombre a {max} caracteres"), new("omit", "Omitir esta fila")
        };

        public static ImportOption[] CodeTooLong(int max) => new ImportOption[]
        {
            new("truncate", $"Cortar el código a {max} caracteres"), new("omit", "Omitir esta fila")
        };
    }

    public static class BulkImportOptionsParser
    {
        // Opciones que el portal manda junto al archivo (JSON). Null en error.
        public static BulkImportOptions? Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new BulkImportOptions();
            try { return System.Text.Json.JsonSerializer.Deserialize<BulkImportOptions>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new BulkImportOptions(); }
            catch (System.Text.Json.JsonException) { return null; }
        }
    }

    public sealed class BulkImportResult
    {
        public int Created { get; set; }
        public int Skipped { get; set; }
        public List<string> Messages { get; } = new();
    }
}
