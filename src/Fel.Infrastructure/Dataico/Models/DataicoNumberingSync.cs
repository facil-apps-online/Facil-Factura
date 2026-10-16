using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace Fel.Infrastructure.Dataico.Models
{
    // Una resolución de la DIAN tal como la devuelve POST /v2/numberings/sync_dian/{módulo}.
    public class DataicoSyncedResolution
    {
        public string Prefix { get; set; } = string.Empty;
        // ELECTRONICO, POS... (subtype de la numeración a la que pertenece).
        public string Subtype { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public long Start { get; set; }
        public long End { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Message { get; set; }
    }

    public class DataicoNumberingSyncResult
    {
        public bool Success { get; set; }
        public List<DataicoSyncedResolution> Resolutions { get; } = new();
        // Mensajes de "errors" que Dataico devuelve junto a las numeraciones (por ejemplo, la DIAN no autorizó
        // a Dataico a consultar los rangos de este NIT) o el error de la llamada.
        public List<string> Errors { get; } = new();

        // Respuesta: { "numberings": [ { prefix, numbering_type, subtype, dian_resolutions: [ { code, code_msg, number,
        // start, end, start_date, end_date } ] } ], "errors": [ { code?, message } ] }. Las fechas vienen dd/MM/yyyy.
        public static DataicoNumberingSyncResult Parse(DataicoResult call)
        {
            var result = new DataicoNumberingSyncResult();
            JsonDocument? doc = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(call.RawResponse)) doc = JsonDocument.Parse(call.RawResponse);
            }
            catch (JsonException)
            {
                // Respuesta que no es JSON (p. ej. HTML de la pasarela): se informa más abajo.
            }

            if (doc == null)
            {
                result.Errors.Add(call.ErrorMessage ?? $"Dataico respondió {call.StatusCode} sin un cuerpo válido.");
                return result;
            }

            using (doc)
            {
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    result.Errors.Add($"Dataico respondió {call.StatusCode} con un formato inesperado.");
                    return result;
                }

                if (root.TryGetProperty("numberings", out var numberings) && numberings.ValueKind == JsonValueKind.Array)
                {
                    foreach (var numbering in numberings.EnumerateArray())
                    {
                        if (numbering.ValueKind != JsonValueKind.Object) continue;
                        var prefix = Text(numbering, "prefix");
                        var subtype = Text(numbering, "subtype");
                        if (!numbering.TryGetProperty("dian_resolutions", out var list) || list.ValueKind != JsonValueKind.Array) continue;

                        foreach (var item in list.EnumerateArray())
                        {
                            if (item.ValueKind != JsonValueKind.Object) continue;
                            result.Resolutions.Add(new DataicoSyncedResolution
                            {
                                Prefix = prefix,
                                Subtype = subtype,
                                Number = Text(item, "number"),
                                Start = Long(item, "start"),
                                End = Long(item, "end"),
                                StartDate = Date(item, "start_date"),
                                EndDate = Date(item, "end_date"),
                                Message = Text(item, "code_msg")
                            });
                        }
                    }
                }

                if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
                {
                    foreach (var error in errors.EnumerateArray())
                    {
                        var message = error.ValueKind == JsonValueKind.Object ? Text(error, "message") : error.ToString();
                        if (!string.IsNullOrWhiteSpace(message)) result.Errors.Add(message);
                    }
                }
            }

            // Una respuesta HTTP 200 con errores y con resoluciones es válida: se usan las resoluciones y se informan los errores.
            result.Success = call.Success || result.Resolutions.Count > 0;
            if (!call.Success && result.Errors.Count == 0)
                result.Errors.Add(call.ErrorMessage ?? $"Dataico respondió {call.StatusCode}.");
            return result;
        }

        private static string Text(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
                ? (value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString()).Trim()
                : string.Empty;

        private static long Long(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var value)) return 0;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)) return number;
            return long.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        }

        private static DateTime? Date(JsonElement element, string name)
        {
            var text = Text(element, name);
            return DateTime.TryParseExact(text, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
        }
    }
}
