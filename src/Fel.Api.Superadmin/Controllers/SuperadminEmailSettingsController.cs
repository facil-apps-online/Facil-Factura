using System;
using System.Linq;
using Fel.Infrastructure.Email;
using System.Threading;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Superadmin.Controllers
{
    // Configuración del correo de la plataforma: las credenciales de SendPulse (el secreto va cifrado y nunca se devuelve), el tope por hora y los
    // remitentes por tipo de correo; y el seguimiento de los correos enviados.
    [ApiController]
    [Route("api/superadmin/email-settings")]
    public class SuperadminEmailSettingsController : ControllerBase
    {
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
            var hourAgo = DateTime.UtcNow.AddHours(-1);
            var sentLastHour = await _db.EmailOutbox.AsNoTracking().CountAsync(e => e.Channel == "platform" && e.Status == EmailOutboxStatus.Sent && e.SentAt >= hourAgo, ct);
            var recentProblems = await _db.EmailOutbox.AsNoTracking()
                .Where(e => e.Status == EmailOutboxStatus.Failed || e.Status == EmailOutboxStatus.Dead || e.DeliveryStatus == EmailDeliveryStatus.Bounced)
                .OrderByDescending(e => e.UpdatedAt).Take(20)
                .Select(e => new { e.Id, Kind = e.Kind.ToString(), Status = e.Status.ToString(), DeliveryStatus = e.DeliveryStatus.ToString(), e.RecipientEmail, e.Subject, e.Attempts, e.Channel, LastError = e.DeliveryStatus == EmailDeliveryStatus.Bounced ? e.DeliveryDetail : e.LastError, e.UpdatedAt })
                .ToListAsync(ct);

            return Ok(new
            {
                credentialsConfigured = !string.IsNullOrWhiteSpace(settings.SendPulseClientId) && !string.IsNullOrWhiteSpace(settings.SendPulseSecretEncrypted),
                settings.SendPulseClientId,
                settings.MaxPerHour,
                sentLastHour,
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

        // El secreto de SendPulse solo se reemplaza si llega uno nuevo; vacío conserva el guardado.
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] PlatformEmailSettingsRequest request, CancellationToken ct)
        {
            var senders = new[] { request.DocumentsFromEmail, request.CertificatesFromEmail, request.NotificationsFromEmail, request.PlatformFromEmail };
            if (senders.Any(s => !IsEmail(s)))
                return BadRequest(new { message = "Todos los remitentes deben ser correos válidos." });
            if (string.IsNullOrWhiteSpace(request.PlatformFromName))
                return BadRequest(new { message = "Indica el nombre del remitente de la plataforma." });
            if (request.MaxPerHour < 1)
                return BadRequest(new { message = "El tope por hora debe ser al menos 1." });
            if (!string.IsNullOrWhiteSpace(request.SupportEmail) && !IsEmail(request.SupportEmail))
                return BadRequest(new { message = "El correo de soporte no es válido." });

            var settings = await _db.PlatformEmailSettings.FirstOrDefaultAsync(s => s.Id == 1, ct);
            if (settings == null)
            {
                settings = new PlatformEmailSettings { Id = 1 };
                _db.PlatformEmailSettings.Add(settings);
            }

            settings.SendPulseClientId = request.SendPulseClientId?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(request.SendPulseSecret))
                settings.SendPulseSecretEncrypted = _cryptoVault.EncryptPassword(request.SendPulseSecret.Trim());
            settings.MaxPerHour = request.MaxPerHour;
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

        // Pide un token a SendPulse con las credenciales guardadas: confirma que sirven, sin enviar nada.
        [HttpPost("test")]
        public async Task<IActionResult> Test(CancellationToken ct)
        {
            var settings = await _db.PlatformEmailSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == 1, ct);
            if (settings == null || string.IsNullOrWhiteSpace(settings.SendPulseClientId) || string.IsNullOrWhiteSpace(settings.SendPulseSecretEncrypted))
                return BadRequest(new { success = false, message = "Todavía no hay credenciales de SendPulse guardadas." });

            try
            {
                await SendPulseApi.GetTokenAsync(settings.SendPulseClientId, _cryptoVault.DecryptPassword(settings.SendPulseSecretEncrypted), ct, forceNew: true);
                return Ok(new { success = true, message = "Las credenciales son válidas: SendPulse entregó el token de acceso." });
            }
            catch (Exception ex) when (ex is SendPulseException or HttpRequestException or TaskCanceledException)
            {
                return Ok(new { success = false, message = ex.Message });
            }
        }

        // Correos enviados por la plataforma, del más reciente al más antiguo, con lo que SendPulse reporta de cada uno.
        [HttpGet("messages")]
        public async Task<IActionResult> Messages(
            [FromQuery] string? status, [FromQuery] string? kind, [FromQuery] string? search,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        {
            pageSize = Math.Clamp(pageSize, 10, 200);
            page = Math.Max(1, page);

            var query = _db.EmailOutbox.AsNoTracking().AsQueryable();
            if (Enum.TryParse<EmailOutboxStatus>(status, out var parsedStatus)) query = query.Where(e => e.Status == parsedStatus);
            else if (status == "Bounced") query = query.Where(e => e.DeliveryStatus == EmailDeliveryStatus.Bounced);
            else if (status == "Delivered") query = query.Where(e => e.DeliveryStatus == EmailDeliveryStatus.Delivered);
            else if (status == "Opened") query = query.Where(e => e.OpenedAt != null);
            if (Enum.TryParse<EmailKind>(kind, out var parsedKind)) query = query.Where(e => e.Kind == parsedKind);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(e => e.RecipientEmail.Contains(term) || e.Subject.Contains(term));
            }

            var total = await query.CountAsync(ct);
            var items = await query.OrderByDescending(e => e.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
                .Select(e => new
                {
                    e.Id,
                    Kind = e.Kind.ToString(),
                    Status = e.Status.ToString(),
                    DeliveryStatus = e.DeliveryStatus.ToString(),
                    e.RecipientEmail,
                    e.Subject,
                    e.Channel,
                    e.Attempts,
                    e.LastError,
                    e.DeliveryDetail,
                    e.CreatedAt,
                    e.SentAt,
                    e.OpenedAt,
                    ClientName = e.Client != null ? e.Client.CompanyName : null
                })
                .ToListAsync(ct);

            return Ok(new { total, page, pageSize, items });
        }

        private static bool IsEmail(string? value) =>
            !string.IsNullOrWhiteSpace(value) && System.Net.Mail.MailAddress.TryCreate(value.Trim(), out var address) && address.Address == value.Trim();
    }

    public class PlatformEmailSettingsRequest
    {
        public string? SendPulseClientId { get; set; }
        public string? SendPulseSecret { get; set; }
        public int MaxPerHour { get; set; } = 50;
        public string DocumentsFromEmail { get; set; } = string.Empty;
        public string CertificatesFromEmail { get; set; } = string.Empty;
        public string NotificationsFromEmail { get; set; } = string.Empty;
        public string PlatformFromEmail { get; set; } = string.Empty;
        public string PlatformFromName { get; set; } = string.Empty;
        public string? SupportEmail { get; set; }
    }
}
