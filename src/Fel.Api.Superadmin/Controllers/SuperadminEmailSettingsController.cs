using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Superadmin.Controllers
{
    // Configuración del correo de la plataforma: la API key de Brevo (cifrada, nunca se devuelve) y los remitentes por tipo de correo.
    [ApiController]
    [Route("api/superadmin/email-settings")]
    public class SuperadminEmailSettingsController : ControllerBase
    {
        private static readonly HttpClient BrevoHttp = new() { Timeout = TimeSpan.FromSeconds(30) };

        private readonly FelDbContext _db;
        private readonly ICryptoVault _cryptoVault;

        public SuperadminEmailSettingsController(FelDbContext db, ICryptoVault cryptoVault)
        {
            _db = db;
            _cryptoVault = cryptoVault;
        }

        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken ct)
        {
            var settings = await _db.PlatformEmailSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == 1, ct) ?? new PlatformEmailSettings();
            var counts = await _db.EmailOutbox.AsNoTracking()
                .Where(e => e.Status == EmailOutboxStatus.Pending || e.Status == EmailOutboxStatus.Failed || e.Status == EmailOutboxStatus.Dead || e.Status == EmailOutboxStatus.Held)
                .GroupBy(e => e.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct);
            var recentProblems = await _db.EmailOutbox.AsNoTracking()
                .Where(e => e.Status == EmailOutboxStatus.Failed || e.Status == EmailOutboxStatus.Dead)
                .OrderByDescending(e => e.UpdatedAt).Take(20)
                .Select(e => new { e.Id, Kind = e.Kind.ToString(), Status = e.Status.ToString(), e.RecipientEmail, e.Subject, e.Attempts, e.Channel, e.LastError, e.UpdatedAt })
                .ToListAsync(ct);

            return Ok(new
            {
                apiKeyConfigured = !string.IsNullOrWhiteSpace(settings.BrevoApiKeyEncrypted),
                settings.DocumentsFromEmail,
                settings.CertificatesFromEmail,
                settings.NotificationsFromEmail,
                settings.PlatformFromEmail,
                settings.PlatformFromName,
                settings.SupportEmail,
                settings.UpdatedAt,
                settings.UpdatedBy,
                queue = new
                {
                    pending = counts.FirstOrDefault(c => c.Status == EmailOutboxStatus.Pending)?.Count ?? 0,
                    held = counts.FirstOrDefault(c => c.Status == EmailOutboxStatus.Held)?.Count ?? 0,
                    failed = counts.FirstOrDefault(c => c.Status == EmailOutboxStatus.Failed)?.Count ?? 0,
                    dead = counts.FirstOrDefault(c => c.Status == EmailOutboxStatus.Dead)?.Count ?? 0
                },
                recentProblems
            });
        }

        // La API key solo se reemplaza si llega una nueva; vacía conserva la guardada.
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] PlatformEmailSettingsRequest request, CancellationToken ct)
        {
            var senders = new[] { request.DocumentsFromEmail, request.CertificatesFromEmail, request.NotificationsFromEmail, request.PlatformFromEmail };
            if (senders.Any(s => !IsEmail(s)))
                return BadRequest(new { message = "Todos los remitentes deben ser correos válidos." });
            if (string.IsNullOrWhiteSpace(request.PlatformFromName))
                return BadRequest(new { message = "Indica el nombre del remitente de la plataforma." });
            if (!string.IsNullOrWhiteSpace(request.SupportEmail) && !IsEmail(request.SupportEmail))
                return BadRequest(new { message = "El correo de soporte no es válido." });

            var settings = await _db.PlatformEmailSettings.FirstOrDefaultAsync(s => s.Id == 1, ct);
            if (settings == null)
            {
                settings = new PlatformEmailSettings { Id = 1 };
                _db.PlatformEmailSettings.Add(settings);
            }

            if (!string.IsNullOrWhiteSpace(request.BrevoApiKey))
                settings.BrevoApiKeyEncrypted = _cryptoVault.EncryptPassword(request.BrevoApiKey.Trim());
            settings.DocumentsFromEmail = request.DocumentsFromEmail.Trim();
            settings.CertificatesFromEmail = request.CertificatesFromEmail.Trim();
            settings.NotificationsFromEmail = request.NotificationsFromEmail.Trim();
            settings.PlatformFromEmail = request.PlatformFromEmail.Trim();
            settings.PlatformFromName = request.PlatformFromName.Trim();
            settings.SupportEmail = request.SupportEmail?.Trim() ?? string.Empty;
            settings.UpdatedAt = DateTime.UtcNow;
            settings.UpdatedBy = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "superadmin";
            await _db.SaveChangesAsync(ct);

            return Ok(new { message = "Configuración de correo guardada." });
        }

        // Consulta la cuenta de Brevo con la key guardada: confirma que la key sirve y a qué cuenta pertenece, sin enviar nada.
        [HttpPost("test")]
        public async Task<IActionResult> Test(CancellationToken ct)
        {
            var settings = await _db.PlatformEmailSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == 1, ct);
            if (settings == null || string.IsNullOrWhiteSpace(settings.BrevoApiKeyEncrypted))
                return BadRequest(new { success = false, message = "Todavía no hay una API key de Brevo guardada." });

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.brevo.com/v3/account");
                request.Headers.Add("api-key", _cryptoVault.DecryptPassword(settings.BrevoApiKeyEncrypted));
                using var response = await BrevoHttp.SendAsync(request, ct);
                var body = await response.Content.ReadAsStringAsync(ct);
                if (!response.IsSuccessStatusCode)
                    return Ok(new { success = false, message = $"Brevo respondió {(int)response.StatusCode}: {(body.Length > 300 ? body[..300] : body)}" });

                var root = JsonDocument.Parse(body).RootElement;
                var email = root.TryGetProperty("email", out var e) ? e.GetString() : null;
                var company = root.TryGetProperty("companyName", out var c) ? c.GetString() : null;
                return Ok(new { success = true, message = $"La key es válida. Cuenta de Brevo: {company ?? email ?? "sin nombre"}{(company != null && email != null ? $" ({email})" : string.Empty)}." });
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                return Ok(new { success = false, message = $"No se pudo consultar Brevo: {ex.Message}" });
            }
        }

        private static bool IsEmail(string? value) =>
            !string.IsNullOrWhiteSpace(value) && System.Net.Mail.MailAddress.TryCreate(value.Trim(), out var address) && address.Address == value.Trim();
    }

    public class PlatformEmailSettingsRequest
    {
        public string? BrevoApiKey { get; set; }
        public string DocumentsFromEmail { get; set; } = string.Empty;
        public string CertificatesFromEmail { get; set; } = string.Empty;
        public string NotificationsFromEmail { get; set; } = string.Empty;
        public string PlatformFromEmail { get; set; } = string.Empty;
        public string PlatformFromName { get; set; } = string.Empty;
        public string? SupportEmail { get; set; }
    }
}
