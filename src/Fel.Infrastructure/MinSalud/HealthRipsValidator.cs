using System.Globalization;
using System.Text.Json;
using Fel.Core.Models;

namespace Fel.Infrastructure.MinSalud
{
    public class RipsValidation
    {
        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();
        public bool IsValid => Errors.Count == 0;
    }

    // Prevalidador del RIPS soporte de una factura de salud: lo que el Mecanismo Único de Validación del Ministerio rechaza y se puede saber antes de emitir.
    // Cruza el RIPS (JSON) con el bloque de salud y con la factura (Documento Técnico 2 y Manual de Consumo API-Docker-FEV-RIPS v4.3). Las reglas clínicas
    // (diagnóstico, sexo y edad) y los cruces de cobertura contra tipo de usuario las aplica el Ministerio; aquí no se repiten.
    public static class HealthRipsValidator
    {
        // Tipos de documento de identificación de la tabla TipoIdPISIS que se han podido leer (SISPRO informa 17; faltan tres por confirmar).
        private static readonly string[] KnownIdTypes = { "AS", "CC", "CD", "CE", "CN", "DE", "MS", "NI", "NV", "PA", "PE", "PT", "RC", "SC", "TI" };
        private static readonly string[] ServiceGroups = { "consultas", "procedimientos", "urgencias", "hospitalizacion", "medicamentos", "otrosServicios" };

        // invoiceTotal es opcional: sin él no se compara el valor de los servicios contra el de la factura.
        public static RipsValidation Validate(JsonElement rips, HealthSectorData health, string providerTaxId, string invoiceNumber, decimal? invoiceTotal)
        {
            var result = new RipsValidation();
            if (rips.ValueKind != JsonValueKind.Object)
            {
                result.Errors.Add("rips: debe ser un objeto JSON con la estructura del RIPS soporte (Resolución 2275 de 2023).");
                return result;
            }

            var nit = new string(providerTaxId.TakeWhile(char.IsDigit).ToArray());
            var obligated = Text(rips, "numDocumentoIdObligado");
            if (string.IsNullOrWhiteSpace(obligated)) result.Errors.Add("rips.numDocumentoIdObligado: es obligatorio.");
            else if (obligated.Trim() != nit) result.Errors.Add($"rips.numDocumentoIdObligado: debe ser el NIT del emisor sin dígito de verificación ({nit}).");

            var number = Text(rips, "numFactura");
            if (string.IsNullOrWhiteSpace(number)) result.Errors.Add("rips.numFactura: es obligatorio.");
            else if (!string.Equals(number.Trim(), invoiceNumber, StringComparison.Ordinal)) result.Errors.Add($"rips.numFactura: debe ser el número de la factura ({invoiceNumber}).");
            if (!string.IsNullOrWhiteSpace(Text(rips, "tipoNota")) || !string.IsNullOrWhiteSpace(Text(rips, "numNota")))
                result.Errors.Add("rips.tipoNota y rips.numNota: van vacíos en el RIPS de una factura.");

            if (!rips.TryGetProperty("usuarios", out var users) || users.ValueKind != JsonValueKind.Array || users.GetArrayLength() == 0)
            {
                result.Errors.Add("rips.usuarios: debe traer al menos un usuario.");
                return result;
            }

            var consecutives = new HashSet<long>();
            var sums = new Dictionary<string, decimal>();
            decimal servicesTotal = 0;
            var index = 0;
            foreach (var user in users.EnumerateArray())
            {
                index++;
                var at = $"rips.usuarios[{index}]";
                if (user.ValueKind != JsonValueKind.Object) { result.Errors.Add($"{at}: debe ser un objeto."); continue; }

                if (!Number(user, "consecutivo").HasValue || Number(user, "consecutivo") <= 0) result.Errors.Add($"{at}.consecutivo: es obligatorio y mayor que cero.");
                else if (!consecutives.Add((long)Number(user, "consecutivo")!.Value)) result.Errors.Add($"{at}.consecutivo: está repetido.");

                var idType = Text(user, "tipoDocumentoIdentificacion");
                if (string.IsNullOrWhiteSpace(idType)) result.Errors.Add($"{at}.tipoDocumentoIdentificacion: es obligatorio.");
                else if (!KnownIdTypes.Contains(idType.Trim().ToUpperInvariant())) result.Warnings.Add($"{at}.tipoDocumentoIdentificacion: '{idType}' no está en la lista conocida de TipoIdPISIS; el Ministerio lo valida.");
                if (string.IsNullOrWhiteSpace(Text(user, "numDocumentoIdentificacion"))) result.Errors.Add($"{at}.numDocumentoIdentificacion: es obligatorio.");
                if (string.IsNullOrWhiteSpace(Text(user, "tipoUsuario"))) result.Errors.Add($"{at}.tipoUsuario: es obligatorio.");

                var birth = Text(user, "fechaNacimiento");
                if (!DateTime.TryParseExact(birth, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var birthDate))
                    result.Errors.Add($"{at}.fechaNacimiento: debe tener formato yyyy-MM-dd.");
                else if (birthDate.Date > DateTime.UtcNow.Date) result.Errors.Add($"{at}.fechaNacimiento: no puede ser futura.");

                if (!user.TryGetProperty("servicios", out var services) || services.ValueKind != JsonValueKind.Object)
                {
                    result.Errors.Add($"{at}.servicios: es obligatorio.");
                    continue;
                }

                var hasServices = false;
                foreach (var group in ServiceGroups)
                {
                    if (!services.TryGetProperty(group, out var items) || items.ValueKind != JsonValueKind.Array) continue;
                    var n = 0;
                    foreach (var item in items.EnumerateArray())
                    {
                        n++;
                        hasServices = true;
                        var where = $"{at}.servicios.{group}[{n}]";

                        var provider = Text(item, "codPrestador");
                        if (string.IsNullOrWhiteSpace(provider) || provider.Length != 12 || !provider.All(char.IsDigit))
                            result.Errors.Add($"{where}.codPrestador: debe tener 12 dígitos (10 del prestador y 2 de la sede).");
                        else if (!provider.StartsWith(health.ProviderCode, StringComparison.Ordinal))
                            result.Errors.Add($"{where}.codPrestador: los primeros 10 dígitos deben coincidir con provider_code ({health.ProviderCode}).");

                        servicesTotal += Number(item, "vrServicio") ?? 0m;

                        var concept = Text(item, "conceptoRecaudo");
                        var moderator = Number(item, "valorPagoModerador") ?? 0m;
                        if (!string.IsNullOrWhiteSpace(concept))
                        {
                            if (!new[] { "01", "02", "03", "04", "05" }.Contains(concept)) result.Errors.Add($"{where}.conceptoRecaudo: '{concept}' no es válido (01 a 05).");
                            sums[concept] = sums.GetValueOrDefault(concept) + moderator;
                        }
                        else if (moderator > 0) result.Errors.Add($"{where}.valorPagoModerador: informa conceptoRecaudo cuando hay un valor recaudado.");
                    }
                }
                if (!hasServices) result.Errors.Add($"{at}.servicios: el usuario no trae ningún servicio.");
            }

            // Lo recaudado en la factura debe ser lo mismo que suma el RIPS por concepto (el anticipo solo se reporta en la factura).
            foreach (var concept in new[] { "01", "02", "03" })
            {
                var inInvoice = health.Prepaid.Where(p => p.ConceptCode == concept).Sum(p => p.Amount);
                var inRips = sums.GetValueOrDefault(concept);
                if (Math.Abs(inInvoice - inRips) > 0.01m)
                    result.Errors.Add(string.Create(CultureInfo.InvariantCulture, $"El recaudo {ConceptName(concept)} de la factura ({inInvoice:0.00}) debe ser igual a la suma de valorPagoModerador del RIPS ({inRips:0.00})."));
            }

            if (invoiceTotal.HasValue && Math.Abs(servicesTotal - invoiceTotal.Value) > 1m)
                result.Warnings.Add(string.Create(CultureInfo.InvariantCulture, $"La suma de vrServicio del RIPS ({servicesTotal:0.00}) no coincide con el total de la factura ({invoiceTotal.Value:0.00}); el Ministerio lo contrasta."));
            return result;
        }

        private static string ConceptName(string concept) => concept switch { "01" => "de copago", "02" => "de cuota moderadora", _ => "de pagos compartidos" };

        private static string? Text(JsonElement element, string name) =>
            element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var v)
                ? v.ValueKind switch { JsonValueKind.String => v.GetString(), JsonValueKind.Number => v.GetRawText(), _ => null }
                : null;

        private static decimal? Number(JsonElement element, string name)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var v)) return null;
            if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)) return d;
            if (v.ValueKind == JsonValueKind.String && decimal.TryParse(v.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var s)) return s;
            return null;
        }
    }
}
