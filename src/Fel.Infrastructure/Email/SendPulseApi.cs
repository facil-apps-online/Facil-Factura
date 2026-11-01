using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Fel.Core.Interfaces;

namespace Fel.Infrastructure.Email
{
    public class SendPulseException : Exception
    {
        public SendPulseException(string message) : base(message) { }
    }

    // Lo que SendPulse sabe de un correo ya enviado (GET /smtp/emails/{id}).
    public record SendPulseTracking(int SmtpAnswerCode, string SmtpAnswerData, bool Opened, DateTime? OpenedAt);

    // Cliente de la API SMTP de SendPulse (https://sendpulse.com/integrations/api/smtp). El token OAuth dura una hora y se reutiliza entre
    // envíos; se pide uno nuevo al vencer o si SendPulse lo rechaza.
    public static class SendPulseApi
    {
        private const string BaseUrl = "https://api.sendpulse.com";
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };
        private static readonly SemaphoreSlim TokenLock = new(1, 1);
        private static string? _token;
        private static string? _tokenFor;
        private static DateTime _tokenExpiresAt;

        public static async Task<string> GetTokenAsync(string clientId, string secret, CancellationToken ct, bool forceNew = false)
        {
            await TokenLock.WaitAsync(ct);
            try
            {
                if (!forceNew && _token != null && _tokenFor == clientId && DateTime.UtcNow < _tokenExpiresAt) return _token;

                var payload = JsonSerializer.Serialize(new { grant_type = "client_credentials", client_id = clientId, client_secret = secret });
                using var response = await Http.PostAsync($"{BaseUrl}/oauth/access_token", new StringContent(payload, Encoding.UTF8, "application/json"), ct);
                var body = await response.Content.ReadAsStringAsync(ct);
                if (!response.IsSuccessStatusCode)
                    throw new SendPulseException($"SendPulse no autenticó las credenciales ({(int)response.StatusCode}): {Truncate(body, 300)}");

                using var doc = JsonDocument.Parse(body);
                _token = doc.RootElement.GetProperty("access_token").GetString();
                var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var e) && e.TryGetInt32(out var seconds) ? seconds : 3600;
                // Margen de un minuto para no usar un token que vence en pleno envío.
                _tokenExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn - 60);
                _tokenFor = clientId;
                return _token ?? throw new SendPulseException("SendPulse no devolvió el token de acceso.");
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException)
            {
                throw new SendPulseException("La respuesta de autenticación de SendPulse no tiene el formato esperado.");
            }
            finally
            {
                TokenLock.Release();
            }
        }

        // Envía el correo y devuelve el id que SendPulse le asigna (sirve para consultar después qué pasó con él).
        public static async Task<string> SendAsync(
            string clientId, string secret, string fromName, string fromEmail, string? replyTo,
            string recipientEmail, string subject, string bodyHtml, IReadOnlyList<EmailAttachment>? attachments, CancellationToken ct)
        {
            var email = new Dictionary<string, object?>
            {
                ["subject"] = subject,
                // SendPulse exige el HTML en Base64.
                ["html"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(bodyHtml)),
                ["from"] = new { name = fromName, email = fromEmail },
                ["to"] = new[] { new { email = recipientEmail } }
            };
            if (!string.IsNullOrWhiteSpace(replyTo)) email["reply_to"] = new { name = fromName, email = replyTo };
            if (attachments is { Count: > 0 })
                email["attachments_binary"] = attachments.ToDictionary(a => a.FileName, a => Convert.ToBase64String(a.Content));

            var (status, body) = await CallAsync(clientId, secret, HttpMethod.Post, "/smtp/emails", JsonSerializer.Serialize(new { email }), ct);
            if (status is < 200 or >= 300)
                throw new SendPulseException($"SendPulse respondió {status}: {Truncate(body, 500)}");

            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.False)
                    throw new SendPulseException($"SendPulse no aceptó el correo: {Truncate(body, 500)}");
                return doc.RootElement.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty;
            }
            catch (JsonException)
            {
                throw new SendPulseException($"SendPulse devolvió una respuesta inesperada: {Truncate(body, 300)}");
            }
        }

        // null si SendPulse todavía no conoce el correo o no se pudo leer; el seguimiento lo vuelve a intentar en la siguiente vuelta.
        public static async Task<SendPulseTracking?> GetTrackingAsync(string clientId, string secret, string messageId, CancellationToken ct)
        {
            var (status, body) = await CallAsync(clientId, secret, HttpMethod.Get, $"/smtp/emails/{Uri.EscapeDataString(messageId)}", null, ct);
            if (status is < 200 or >= 300) return null;

            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (!root.TryGetProperty("smtp_answer_code", out var codeElement)) return null;
                // El código llega como texto en este endpoint y como número en otros.
                var code = codeElement.ValueKind == JsonValueKind.Number ? codeElement.GetInt32() : int.TryParse(codeElement.GetString(), out var parsed) ? parsed : 0;
                var data = root.TryGetProperty("smtp_answer_data", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() ?? string.Empty : string.Empty;

                var opened = false;
                DateTime? openedAt = null;
                if (root.TryGetProperty("tracking", out var tracking) && tracking.ValueKind == JsonValueKind.Object
                    && tracking.TryGetProperty("open", out var open) && open.ValueKind == JsonValueKind.Number && open.GetInt32() > 0)
                {
                    opened = true;
                    if (tracking.TryGetProperty("client_info", out var info) && info.ValueKind == JsonValueKind.Array && info.GetArrayLength() > 0
                        && info[0].TryGetProperty("action_date", out var date) && DateTime.TryParse(date.GetString(), out var parsedDate))
                        openedAt = DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);
                }
                return new SendPulseTracking(code, data, opened, openedAt);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
            {
                return null;
            }
        }

        private static async Task<(int Status, string Body)> CallAsync(string clientId, string secret, HttpMethod method, string path, string? json, CancellationToken ct)
        {
            for (var attempt = 0; ; attempt++)
            {
                var token = await GetTokenAsync(clientId, secret, ct, forceNew: attempt > 0);
                using var request = new HttpRequestMessage(method, BaseUrl + path);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                if (json != null) request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using var response = await Http.SendAsync(request, ct);
                var body = await response.Content.ReadAsStringAsync(ct);
                // Token rechazado (vencido o revocado): se pide otro y se reintenta una vez.
                if ((int)response.StatusCode == 401 && attempt == 0) continue;
                return ((int)response.StatusCode, body);
            }
        }

        private static string Truncate(string value, int max) => value.Length > max ? value[..max] : value;
    }
}
