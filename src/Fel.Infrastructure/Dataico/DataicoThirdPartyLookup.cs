using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Fel.Infrastructure.Dian;

namespace Fel.Infrastructure.Dataico
{
    public sealed record DataicoThirdPartyOutcome(DianAcquirerResult? Result, string? Error);

    // Consulta de un tercero en la DIAN a través de Dataico (GET dian_terceros), para los clientes que emiten con Dataico: equivale a la consulta de
    // adquirentes que la emisión directa hace con su certificado. Solo lee. Formas de respuesta confirmadas contra Dataico:
    //  - empresa (NIT): { email, company_name, identification, identification_type };
    //  - persona (CC): { email, first_name, other_names?, family_name, second_last_name?, identification, identification_type };
    //  - tipo no admitido o número vacío: 400 { error }; la DIAN no responde (por ejemplo, un número mal formado): 500 { error: "Error en la API de DIAN" }.
    // Dataico solo admite NIT/31 y CC/13; con otro tipo responde 400 (ver SupportedTypes).
    public static class DataicoThirdPartyLookup
    {
        public static readonly IReadOnlySet<string> SupportedTypes = new HashSet<string> { "13", "31" };

        private const string Url = "https://api.dataico.com/direct/dataico_api/v2/dian_terceros";

        public static async Task<DataicoThirdPartyOutcome> LookupAsync(DataicoCredentials credentials, string identificationType, string identificationNumber)
        {
            try
            {
                var uri = $"{Url}?identification_type={Uri.EscapeDataString(identificationType)}&identification={Uri.EscapeDataString(identificationNumber)}";
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.Add("Auth-token", credentials.AuthToken);
                if (!string.IsNullOrEmpty(credentials.AccountId)) request.Headers.Add("dataico-account-id", credentials.AccountId);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                using var response = await DataicoClient.Client.SendAsync(request, cts.Token);
                var body = await response.Content.ReadAsStringAsync(cts.Token);
                return Parse((int)response.StatusCode, body);
            }
            catch (Exception)
            {
                return new DataicoThirdPartyOutcome(null, "No se pudo consultar a Dataico. Intente de nuevo.");
            }
        }

        // Aparte de la llamada para poder probar la lectura de la respuesta sin red.
        public static DataicoThirdPartyOutcome Parse(int status, string body)
        {
            JsonElement root = default;
            var isJson = false;
            try { root = JsonDocument.Parse(body).RootElement; isJson = root.ValueKind == JsonValueKind.Object; } catch (JsonException) { }

            string? Text(string property) =>
                isJson && root.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()) ? v.GetString()!.Trim() : null;

            if (status == 404) return new DataicoThirdPartyOutcome(new DianAcquirerResult(false, null, Array.Empty<string>(), null, "La DIAN no tiene registrado este documento."), null);
            if (status < 200 || status >= 300)
                return new DataicoThirdPartyOutcome(null, status >= 500 ? "La DIAN no respondió la consulta del tercero (vía Dataico). Intente de nuevo." : Text("error") ?? "Dataico rechazó la consulta.");

            var name = Text("company_name") ?? NaturalPersonName(Text("first_name"), Text("other_names"), Text("family_name"), Text("second_last_name"));
            var emails = (Text("email") ?? string.Empty)
                .Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(e => System.Text.RegularExpressions.Regex.IsMatch(e, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var found = name != null || emails.Count > 0;
            return new DataicoThirdPartyOutcome(new DianAcquirerResult(found, name, emails, null, found ? null : "La DIAN no tiene registrado este documento."), null);
        }

        // Nombre completo en el orden de la DIAN: nombres y luego apellidos.
        private static string? NaturalPersonName(params string?[] parts)
        {
            var name = string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
            return name.Length == 0 ? null : name;
        }
    }
}
