using System;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Services;
using MailKit.Net.Imap;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Tenant.Controllers
{
    // Credenciales propias de una sucursal (MinSalud, IHCE, buzón de recepción y eventos automáticos). Sin credenciales propias la sucursal
    // usa las de la principal (el valor por defecto de las demás); DELETE las quita y vuelve a heredar, salvo en la principal. Las claves nunca se devuelven: solo si están configuradas. Una sucursal con credenciales propias
    // reemplaza por completo a las de la principal, así que al crearlas no se arrastra ninguna clave heredada.
    [ApiController]
    [Route("api/tenant/clients/{clientId}/branches/{branchId}/credentials")]
    public class TenantBranchCredentialsController : ControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly ICryptoService _cryptoService;
        private readonly BranchCredentialResolver _resolver;

        public TenantBranchCredentialsController(FelDbContext dbContext, ICryptoService cryptoService, BranchCredentialResolver resolver)
        {
            _dbContext = dbContext;
            _cryptoService = cryptoService;
            _resolver = resolver;
        }

        private Guid GetCurrentTenantId()
        {
            if (Request.Headers.TryGetValue("x-tenant-id", out var tenantIdStr) && Guid.TryParse(tenantIdStr, out var tenantId))
                return tenantId;
            throw new UnauthorizedAccessException("x-tenant-id Header is missing");
        }

        // La sucursal debe ser de un Client del tenant.
        private async Task<(Client? Client, Branch? Branch)> GetOwnedAsync(Guid clientId, Guid branchId)
        {
            var tenantId = GetCurrentTenantId();
            var client = await _dbContext.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clientId && c.TenantId == tenantId);
            if (client == null) return (null, null);
            var branch = await _dbContext.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == branchId && b.ClientId == clientId);
            return (client, branch);
        }

        private const string MainIsDefault = "La sucursal principal es el valor por defecto de las demás: edítala en lugar de quitarla.";

        // ---------------- MinSalud ----------------

        public class MinSaludRequest
        {
            public string? MinSaludEnvironment { get; set; }
            public string? MinSaludUserType { get; set; }
            public string? MinSaludIdentificationType { get; set; }
            public string? MinSaludIdentificationNumber { get; set; }
            public string? MinSaludPassword { get; set; }
            public string? MinSaludTestIdentificationType { get; set; }
            public string? MinSaludTestIdentificationNumber { get; set; }
            public string? MinSaludTestPassword { get; set; }
        }

        private static object ToDto(MinSaludCredentials c) => new
        {
            isBranchOwn = c.IsBranchOwn,
            minSaludEnvironment = c.Environment,
            minSaludUserType = c.UserType,
            minSaludIdentificationType = c.IdentificationType,
            minSaludIdentificationNumber = c.IdentificationNumber,
            hasPassword = !string.IsNullOrEmpty(c.PasswordEncrypted),
            minSaludTestIdentificationType = c.TestIdentificationType,
            minSaludTestIdentificationNumber = c.TestIdentificationNumber,
            hasTestPassword = !string.IsNullOrEmpty(c.TestPasswordEncrypted)
        };

        [HttpGet("minsalud")]
        public async Task<IActionResult> GetMinSalud(Guid clientId, Guid branchId)
        {
            try
            {
                var (client, branch) = await GetOwnedAsync(clientId, branchId);
                if (client == null || branch == null) return NotFound();
                return Ok(ToDto(await _resolver.MinSaludAsync(clientId, branchId)));
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(ex.Message); }
        }

        [HttpPut("minsalud")]
        public async Task<IActionResult> SaveMinSalud(Guid clientId, Guid branchId, [FromBody] MinSaludRequest request)
        {
            try
            {
                var (client, branch) = await GetOwnedAsync(clientId, branchId);
                if (client == null || branch == null) return NotFound();
                if (!MinSaludEnvironments.EsValido(request.MinSaludEnvironment)) return BadRequest("El ambiente de MinSalud no es válido.");

                var row = await _dbContext.BranchMinSaludCredentials.FirstOrDefaultAsync(x => x.BranchId == branchId);
                if (row == null)
                {
                    row = new BranchMinSaludCredential { BranchId = branchId };
                    _dbContext.BranchMinSaludCredentials.Add(row);
                }

                row.Environment = request.MinSaludEnvironment!;
                row.UserType = request.MinSaludUserType;
                row.IdentificationType = request.MinSaludIdentificationType;
                row.IdentificationNumber = request.MinSaludIdentificationNumber;
                row.TestIdentificationType = request.MinSaludTestIdentificationType;
                row.TestIdentificationNumber = request.MinSaludTestIdentificationNumber;
                // Vacía = no cambiar la clave que ya tiene esta sucursal.
                if (!string.IsNullOrEmpty(request.MinSaludPassword)) row.PasswordEncrypted = _cryptoService.Encrypt(request.MinSaludPassword);
                if (!string.IsNullOrEmpty(request.MinSaludTestPassword)) row.TestPasswordEncrypted = _cryptoService.Encrypt(request.MinSaludTestPassword);

                await _dbContext.SaveChangesAsync();
                return Ok(ToDto(await _resolver.MinSaludAsync(clientId, branchId)));
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(ex.Message); }
        }

        [HttpDelete("minsalud")]
        public async Task<IActionResult> RemoveMinSalud(Guid clientId, Guid branchId)
        {
            try
            {
                var (client, branch) = await GetOwnedAsync(clientId, branchId);
                if (client == null || branch == null) return NotFound();
                if (branch.IsMain) return BadRequest(MainIsDefault);
                var row = await _dbContext.BranchMinSaludCredentials.FirstOrDefaultAsync(x => x.BranchId == branchId);
                if (row != null)
                {
                    _dbContext.BranchMinSaludCredentials.Remove(row);
                    await _dbContext.SaveChangesAsync();
                }
                return Ok(ToDto(await _resolver.MinSaludAsync(clientId, branchId)));
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(ex.Message); }
        }

        // ---------------- IHCE ----------------

        public class IhceRequest
        {
            public string? IhceClientId { get; set; }
            public string? IhceClientSecret { get; set; }
            public string? IhceApimSubscriptionKey { get; set; }
            public string? IhceTenantId { get; set; }
            public string? IhceEndpoint { get; set; }
            public string? IhceEnvironment { get; set; }
        }

        private static bool IsValidIhceEnvironment(string? value) => value == "Sandbox" || value == "Production";

        private static bool IsValidEndpoint(string? value) =>
            string.IsNullOrWhiteSpace(value) || (Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp));

        private static object ToDto(IhceCredentials c) => new
        {
            isBranchOwn = c.IsBranchOwn,
            ihceClientId = c.ClientId,
            hasClientSecret = !string.IsNullOrEmpty(c.ClientSecretEncrypted),
            hasApimSubscriptionKey = !string.IsNullOrEmpty(c.ApimSubscriptionKeyEncrypted),
            ihceTenantId = c.TenantId,
            ihceEndpoint = c.Endpoint,
            ihceEnvironment = c.Environment
        };

        [HttpGet("ihce")]
        public async Task<IActionResult> GetIhce(Guid clientId, Guid branchId)
        {
            try
            {
                var (client, branch) = await GetOwnedAsync(clientId, branchId);
                if (client == null || branch == null) return NotFound();
                return Ok(ToDto(await _resolver.IhceAsync(clientId, branchId)));
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(ex.Message); }
        }

        [HttpPut("ihce")]
        public async Task<IActionResult> SaveIhce(Guid clientId, Guid branchId, [FromBody] IhceRequest request)
        {
            try
            {
                var (client, branch) = await GetOwnedAsync(clientId, branchId);
                if (client == null || branch == null) return NotFound();
                if (!IsValidIhceEnvironment(request.IhceEnvironment)) return BadRequest("El ambiente de IHCE debe ser Sandbox o Production.");
                if (!IsValidEndpoint(request.IhceEndpoint)) return BadRequest("El endpoint de IHCE no es una URL válida.");

                var row = await _dbContext.BranchIhceCredentials.FirstOrDefaultAsync(x => x.BranchId == branchId);
                if (row == null)
                {
                    row = new BranchIhceCredential { BranchId = branchId };
                    _dbContext.BranchIhceCredentials.Add(row);
                }

                row.ClientId = request.IhceClientId?.Trim();
                row.TenantId = request.IhceTenantId?.Trim();
                row.Endpoint = request.IhceEndpoint?.Trim();
                row.Environment = request.IhceEnvironment!;
                if (!string.IsNullOrEmpty(request.IhceClientSecret)) row.ClientSecretEncrypted = _cryptoService.Encrypt(request.IhceClientSecret);
                if (!string.IsNullOrEmpty(request.IhceApimSubscriptionKey)) row.ApimSubscriptionKeyEncrypted = _cryptoService.Encrypt(request.IhceApimSubscriptionKey);

                await _dbContext.SaveChangesAsync();
                return Ok(ToDto(await _resolver.IhceAsync(clientId, branchId)));
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(ex.Message); }
        }

        [HttpDelete("ihce")]
        public async Task<IActionResult> RemoveIhce(Guid clientId, Guid branchId)
        {
            try
            {
                var (client, branch) = await GetOwnedAsync(clientId, branchId);
                if (client == null || branch == null) return NotFound();
                if (branch.IsMain) return BadRequest(MainIsDefault);
                var row = await _dbContext.BranchIhceCredentials.FirstOrDefaultAsync(x => x.BranchId == branchId);
                if (row != null)
                {
                    _dbContext.BranchIhceCredentials.Remove(row);
                    await _dbContext.SaveChangesAsync();
                }
                return Ok(ToDto(await _resolver.IhceAsync(clientId, branchId)));
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(ex.Message); }
        }

        // ---------------- Buzón de recepción ----------------

        public class ReceptionMailboxRequest
        {
            public bool ReceptionEmailEnabled { get; set; }
            public string ReceptionEmailHost { get; set; } = string.Empty;
            public int ReceptionEmailPort { get; set; } = 993;
            public bool ReceptionEmailUseSsl { get; set; } = true;
            public string ReceptionEmailUser { get; set; } = string.Empty;
            public string? ReceptionEmailPassword { get; set; }
        }

        private static object ToDto(ReceptionMailbox m) => new
        {
            isBranchOwn = m.IsBranchOwn,
            receptionEmailEnabled = m.Enabled,
            receptionEmailHost = m.Host,
            receptionEmailPort = m.Port,
            receptionEmailUseSsl = m.UseSsl,
            receptionEmailUser = m.User,
            hasPassword = !string.IsNullOrEmpty(m.PasswordEncrypted)
        };

        [HttpGet("reception")]
        public async Task<IActionResult> GetReception(Guid clientId, Guid branchId)
        {
            try
            {
                var (client, branch) = await GetOwnedAsync(clientId, branchId);
                if (client == null || branch == null) return NotFound();
                return Ok(ToDto(await _resolver.ReceptionMailboxAsync(clientId, branchId)));
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(ex.Message); }
        }

        [HttpPut("reception")]
        public async Task<IActionResult> SaveReception(Guid clientId, Guid branchId, [FromBody] ReceptionMailboxRequest request)
        {
            try
            {
                var (client, branch) = await GetOwnedAsync(clientId, branchId);
                if (client == null || branch == null) return NotFound();
                if (request.ReceptionEmailEnabled && (string.IsNullOrWhiteSpace(request.ReceptionEmailHost) || string.IsNullOrWhiteSpace(request.ReceptionEmailUser)))
                    return BadRequest("Para habilitar el buzón indica el host y el usuario.");
                if (request.ReceptionEmailPort < 1 || request.ReceptionEmailPort > 65535) return BadRequest("El puerto no es válido.");

                var row = await _dbContext.BranchReceptionMailboxes.FirstOrDefaultAsync(x => x.BranchId == branchId);
                if (row == null)
                {
                    row = new BranchReceptionMailbox { BranchId = branchId };
                    _dbContext.BranchReceptionMailboxes.Add(row);
                }

                row.Enabled = request.ReceptionEmailEnabled;
                row.Host = request.ReceptionEmailHost?.Trim() ?? string.Empty;
                row.Port = request.ReceptionEmailPort;
                row.UseSsl = request.ReceptionEmailUseSsl;
                row.User = request.ReceptionEmailUser?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(request.ReceptionEmailPassword)) row.PasswordEncrypted = _cryptoService.Encrypt(request.ReceptionEmailPassword);

                await _dbContext.SaveChangesAsync();
                return Ok(ToDto(await _resolver.ReceptionMailboxAsync(clientId, branchId)));
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(ex.Message); }
        }

        [HttpDelete("reception")]
        public async Task<IActionResult> RemoveReception(Guid clientId, Guid branchId)
        {
            try
            {
                var (client, branch) = await GetOwnedAsync(clientId, branchId);
                if (client == null || branch == null) return NotFound();
                if (branch.IsMain) return BadRequest(MainIsDefault);
                var row = await _dbContext.BranchReceptionMailboxes.FirstOrDefaultAsync(x => x.BranchId == branchId);
                if (row != null)
                {
                    _dbContext.BranchReceptionMailboxes.Remove(row);
                    await _dbContext.SaveChangesAsync();
                }
                return Ok(ToDto(await _resolver.ReceptionMailboxAsync(clientId, branchId)));
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(ex.Message); }
        }

        // ---------------- Eventos automáticos de recepción ----------------

        public class ReceptionEventsRequest
        {
            public bool AutoSendAcuseRecibo { get; set; }
            public bool AutoSendReciboBien { get; set; }
            public bool AutoSendAceptacion { get; set; }
            public bool AutoSendReclamo { get; set; }
        }

        private static object ToDto(ReceptionEvents e) => new
        {
            isBranchOwn = e.IsBranchOwn,
            autoSendAcuseRecibo = e.AcuseRecibo,
            autoSendReciboBien = e.ReciboBien,
            autoSendAceptacion = e.Aceptacion,
            autoSendReclamo = e.Reclamo
        };

        [HttpGet("reception-events")]
        public async Task<IActionResult> GetReceptionEvents(Guid clientId, Guid branchId)
        {
            try
            {
                var (client, branch) = await GetOwnedAsync(clientId, branchId);
                if (client == null || branch == null) return NotFound();
                return Ok(ToDto(await _resolver.ReceptionEventsAsync(clientId, branchId)));
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(ex.Message); }
        }

        [HttpPut("reception-events")]
        public async Task<IActionResult> SaveReceptionEvents(Guid clientId, Guid branchId, [FromBody] ReceptionEventsRequest request)
        {
            try
            {
                var (client, branch) = await GetOwnedAsync(clientId, branchId);
                if (client == null || branch == null) return NotFound();

                var row = await _dbContext.BranchReceptionEvents.FirstOrDefaultAsync(x => x.BranchId == branchId);
                if (row == null)
                {
                    row = new BranchReceptionEvents { BranchId = branchId };
                    _dbContext.BranchReceptionEvents.Add(row);
                }
                row.AutoSendAcuseRecibo = request.AutoSendAcuseRecibo;
                row.AutoSendReciboBien = request.AutoSendReciboBien;
                row.AutoSendAceptacion = request.AutoSendAceptacion;
                row.AutoSendReclamo = request.AutoSendReclamo;

                await _dbContext.SaveChangesAsync();
                return Ok(ToDto(await _resolver.ReceptionEventsAsync(clientId, branchId)));
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(ex.Message); }
        }

        [HttpDelete("reception-events")]
        public async Task<IActionResult> RemoveReceptionEvents(Guid clientId, Guid branchId)
        {
            try
            {
                var (client, branch) = await GetOwnedAsync(clientId, branchId);
                if (client == null || branch == null) return NotFound();
                if (branch.IsMain) return BadRequest(MainIsDefault);

                var row = await _dbContext.BranchReceptionEvents.FirstOrDefaultAsync(x => x.BranchId == branchId);
                if (row != null)
                {
                    _dbContext.BranchReceptionEvents.Remove(row);
                    await _dbContext.SaveChangesAsync();
                }
                return Ok(ToDto(await _resolver.ReceptionEventsAsync(clientId, branchId)));
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(ex.Message); }
        }

        [HttpPost("reception/test-connection")]
        public async Task<IActionResult> TestReception(Guid clientId, Guid branchId)
        {
            try
            {
                var (client, branch) = await GetOwnedAsync(clientId, branchId);
                if (client == null || branch == null) return NotFound();

                var mailbox = await _resolver.ReceptionMailboxAsync(clientId, branchId);
                if (string.IsNullOrWhiteSpace(mailbox.Host) || string.IsNullOrWhiteSpace(mailbox.PasswordEncrypted))
                    return BadRequest(new { message = "Guarda primero el host, usuario y contraseña." });

                try
                {
                    using var imap = new ImapClient();
                    await imap.ConnectAsync(mailbox.Host, mailbox.Port, mailbox.UseSsl);
                    await imap.AuthenticateAsync(mailbox.User, _cryptoService.Decrypt(mailbox.PasswordEncrypted));
                    await imap.DisconnectAsync(true);
                    return Ok(new { success = true, message = "Conexión exitosa." });
                }
                catch (Exception ex)
                {
                    return Ok(new { success = false, message = $"No se pudo conectar: {ex.Message}" });
                }
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(ex.Message); }
        }
    }
}
