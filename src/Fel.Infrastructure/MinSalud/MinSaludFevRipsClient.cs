using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Fel.Infrastructure.MinSalud
{
    // Una glosa del Mecanismo Único de Validación (ResultadosValidacion): RECHAZADO impide el CUV; NOTIFICACION solo informa.
    public record FevRipsFinding(string? Class, string? Code, string? Description, string? Notes, string? Source);

    public class FevRipsResult
    {
        // El Ministerio entregó el CUV.
        public bool Accepted { get; init; }
        // Falló el transporte o la autenticación: no hay veredicto del Ministerio, se puede reintentar sin tocar el RIPS.
        public bool TransportFailure { get; init; }
        public string? Cuv { get; init; }
        public long? ProcessId { get; init; }
        public string Message { get; init; } = string.Empty;
        public string? ResponseJson { get; init; }
        public List<FevRipsFinding> Findings { get; init; } = new();
    }

    public interface IMinSaludFevRipsClient
    {
        // Envía el RIPS y el AttachedDocument de la factura ya aprobada por la DIAN (CargarFevRips) y devuelve el CUV o las glosas.
        Task<FevRipsResult> SendInvoiceAsync(string ripsJson, string attachedDocumentXml, Client client, MinSaludCredentials credentials);
    }

    // Cliente del Mecanismo Único de Validación FEV-RIPS (Manual de Consumo API-Docker-FEV-RIPS v4.3): LoginSISPRO y PaquetesFevRips/CargarFevRips. El
    // contenedor del Ministerio corre en el VPS de Bogotá y se alcanza por el túnel privado (MinSalud:MuvProductionUrl y MuvTestUrl).
    public class MinSaludFevRipsClient : IMinSaludFevRipsClient
    {
        private readonly HttpClient _http;
        private readonly ICryptoService _crypto;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MinSaludFevRipsClient> _logger;

        public MinSaludFevRipsClient(HttpClient http, ICryptoService crypto, IConfiguration configuration, ILogger<MinSaludFevRipsClient> logger)
        {
            _http = http;
            _crypto = crypto;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<FevRipsResult> SendInvoiceAsync(string ripsJson, string attachedDocumentXml, Client client, MinSaludCredentials credentials)
        {
            // Los emisores de prueba de developers no tienen habilitación real ante el Ministerio: se simula el CUV, como en el RIPS sin factura.
            if (client.IsDeveloperSandbox)
            {
                var fake = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48)).ToLowerInvariant();
                return new FevRipsResult { Accepted = true, Cuv = fake, Message = "CUV generado (respuesta simulada: emisor de prueba sin habilitación ante el Ministerio)." };
            }

            var production = credentials.Environment == MinSaludEnvironments.Production;
            var environmentName = production ? "producción" : "pruebas";
            var baseUrl = _configuration[production ? "MinSalud:MuvProductionUrl" : "MinSalud:MuvTestUrl"];
            if (string.IsNullOrWhiteSpace(baseUrl))
                return Failure($"No hay URL del MUV configurada para el ambiente de {environmentName}.");

            var idType = production ? credentials.IdentificationType : credentials.TestIdentificationType;
            var idNumber = production ? credentials.IdentificationNumber : credentials.TestIdentificationNumber;
            var encryptedPassword = production ? credentials.PasswordEncrypted : credentials.TestPasswordEncrypted;
            if (string.IsNullOrWhiteSpace(idNumber) || string.IsNullOrWhiteSpace(encryptedPassword))
                return Failure($"Este emisor no tiene configuradas las credenciales del prestador ante el MUV-FEV-RIPS para el ambiente de {environmentName}.");

            try
            {
                var root = baseUrl.TrimEnd('/');
                var login = new
                {
                    persona = new { identificacion = new { tipo = idType ?? "CC", numero = idNumber } },
                    clave = _crypto.Decrypt(encryptedPassword),
                    nit = client.TaxId,
                    tipoUsuario = credentials.UserType
                };
                var loginResponse = await _http.PostAsJsonAsync($"{root}/api/Auth/LoginSISPRO", login);
                var loginBody = await loginResponse.Content.ReadAsStringAsync();
                if (!loginResponse.IsSuccessStatusCode)
                    return Failure($"El MUV-FEV-RIPS rechazó la autenticación ({(int)loginResponse.StatusCode}).", loginBody);
                var token = ReadToken(loginBody);
                if (string.IsNullOrWhiteSpace(token))
                    return Failure("El MUV-FEV-RIPS no devolvió un token de autenticación válido.", loginBody);

                // El RIPS viaja como objeto JSON y el AttachedDocument de la factura en Base64.
                var payload = $"{{\"rips\":{ripsJson},\"xmlFevFile\":{JsonSerializer.Serialize(Convert.ToBase64String(Encoding.UTF8.GetBytes(attachedDocumentXml)))}}}";
                using var submit = new HttpRequestMessage(HttpMethod.Post, $"{root}/api/PaquetesFevRips/CargarFevRips")
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };
                submit.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await _http.SendAsync(submit);
                var body = await response.Content.ReadAsStringAsync();
                return Parse(body, (int)response.StatusCode, response.IsSuccessStatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error de integración con el MUV-FEV-RIPS para el emisor {ClientId}", client.Id);
                return Failure($"Error de integración con el MUV-FEV-RIPS: {ex.Message}");
            }
        }

        // Las glosas de una respuesta ya guardada (vacío si no hay respuesta del Ministerio).
        public static List<FevRipsFinding> ReadFindings(string? responseJson) =>
            string.IsNullOrWhiteSpace(responseJson) ? new List<FevRipsFinding>() : Parse(responseJson, 0, true).Findings;

        private static FevRipsResult Failure(string message, string? responseJson = null) =>
            new() { TransportFailure = true, Message = message, ResponseJson = responseJson };

        // El manual no deja legible el nombre exacto del campo del token: se acepta cualquiera de los habituales.
        public static string? ReadToken(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.String) return doc.RootElement.GetString();
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
                foreach (var property in doc.RootElement.EnumerateObject())
                    if (property.Value.ValueKind == JsonValueKind.String && property.Name.ToLowerInvariant() is "token" or "accesstoken" or "jwt" or "bearer")
                        return property.Value.GetString();
            }
            catch (JsonException) { }
            return null;
        }

        // Respuesta de los métodos de transmisión (numeral 8.13 del manual): ResultState, ProcesoId, CodigoUnicoValidacion y ResultadosValidacion.
        public static FevRipsResult Parse(string body, int statusCode, bool httpSuccess)
        {
            JsonDocument doc;
            try { doc = JsonDocument.Parse(body); }
            catch (JsonException) { return Failure($"El MUV-FEV-RIPS respondió algo que no es JSON ({statusCode}).", body); }

            using (doc)
            {
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return Failure($"Respuesta inesperada del MUV-FEV-RIPS ({statusCode}).", body);
                var props = doc.RootElement.EnumerateObject().ToDictionary(p => p.Name.ToLowerInvariant(), p => p.Value);
                string? Text(string name) => props.TryGetValue(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

                if (!props.TryGetValue("resultstate", out var state) || state.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    return Failure($"El MUV-FEV-RIPS respondió sin resultado de validación ({statusCode}).", body);

                long? processId = null;
                foreach (var key in new[] { "procesoid", "porcesoid" })
                    if (props.TryGetValue(key, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var id)) processId = id;

                var findings = new List<FevRipsFinding>();
                if (props.TryGetValue("resultadosvalidacion", out var list) && list.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in list.EnumerateArray().Where(i => i.ValueKind == JsonValueKind.Object))
                    {
                        var f = item.EnumerateObject().ToDictionary(p => p.Name.ToLowerInvariant(), p => p.Value);
                        string? Field(string name) => f.TryGetValue(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                        findings.Add(new FevRipsFinding(Field("clase"), Field("codigo"), Field("descripcion"), Field("observaciones"), Field("fuente")));
                    }
                }

                var accepted = state.ValueKind == JsonValueKind.True;
                var cuv = Text("codigounicovalidacion");
                var rejections = findings.Where(f => string.Equals(f.Class, "RECHAZADO", StringComparison.OrdinalIgnoreCase)).ToList();
                var message = accepted
                    ? "CUV generado por el Mecanismo Único de Validación."
                    : rejections.Count > 0
                        ? string.Join("; ", rejections.Select(f => $"{f.Code}: {f.Notes ?? f.Description}"))
                        : "Rechazado por el Mecanismo Único de Validación.";
                return new FevRipsResult
                {
                    Accepted = accepted && !string.IsNullOrWhiteSpace(cuv),
                    Cuv = accepted ? cuv : null,
                    ProcessId = processId,
                    Message = accepted && string.IsNullOrWhiteSpace(cuv) ? "El Ministerio aprobó el envío pero no devolvió el CUV." : message,
                    ResponseJson = body,
                    Findings = findings
                };
            }
        }
    }
}
