using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using Fel.Api.Security;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Fel.Api.Client.Controllers
{
    // Usuarios del portal del Client: los gestiona su Administrador. Cada usuario tiene un rol y las sucursales a las que puede entrar.
    [ApiController]
    [Route("api/client/users")]
    [ClientRole(ClientUserRoles.Administrator)]
    [AllowAllBranches]
    public class ClientUsersController : ClientPortalControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly PasswordResetService _passwordResetService;
        private readonly string _portalUrl;

        public ClientUsersController(FelDbContext dbContext, PasswordResetService passwordResetService, IConfiguration config)
        {
            _dbContext = dbContext;
            _passwordResetService = passwordResetService;
            _portalUrl = config["PortalUrl"] ?? "https://clients.facil-factura.pro";
        }

        public class SaveClientUserRequest
        {
            public string Name { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Role { get; set; } = string.Empty;
            public bool AllBranches { get; set; }
            public List<Guid>? BranchIds { get; set; }
        }

        [HttpGet]
        public async Task<IActionResult> List()
        {
            var clientId = GetCurrentClientId();
            var users = await _dbContext.ClientUsers.AsNoTracking()
                .Where(u => u.ClientId == clientId)
                .OrderBy(u => u.CreatedAt)
                .Select(u => new
                {
                    u.Id, u.Name, u.Email, u.Role, u.AllBranches, u.IsActive, u.CreatedAt,
                    BranchIds = u.Branches.Select(b => b.BranchId).ToList()
                })
                .ToListAsync();

            return Ok(users.Select(u => new
            {
                u.Id, u.Name, u.Email, u.Role, u.AllBranches, u.IsActive, u.CreatedAt,
                // Quien ve todas las sucursales no depende de la lista; se devuelve vacía para no mostrar enlaces que no aplican.
                BranchIds = u.AllBranches ? new List<Guid>() : u.BranchIds,
                IsSelf = u.Id == Branch.UserId
            }));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] SaveClientUserRequest request)
        {
            var clientId = GetCurrentClientId();

            var error = ValidateIdentity(request.Name, request.Email, request.Role);
            if (error != null) return BadRequest(error);

            var (branchIds, branchError) = await ResolveBranchesAsync(clientId, request);
            if (branchError != null) return BadRequest(branchError);

            var email = request.Email.Trim();
            // Mismo criterio que el alta desde el tenant: el correo identifica al usuario en el inicio de sesión de toda la empresa.
            if (await _dbContext.ClientUsers.AnyAsync(u => u.Email == email))
                return BadRequest("Ese correo ya está en uso por otro acceso.");

            // Sin contraseña manual: se crea con un hash aleatorio que nadie conoce y se invita a la persona a que establezca la suya.
            var user = new ClientUser
            {
                Id = Guid.NewGuid(),
                ClientId = clientId,
                Name = request.Name.Trim(),
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),
                Role = request.Role,
                AllBranches = request.AllBranches,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            foreach (var branchId in branchIds)
                user.Branches.Add(new ClientUserBranch { ClientUserId = user.Id, BranchId = branchId });
            _dbContext.ClientUsers.Add(user);
            await _dbContext.SaveChangesAsync();

            var (sent, detail) = await SendInvitationAsync(user);
            return Ok(new { user.Id, user.Name, user.Email, user.Role, user.AllBranches, user.IsActive, invitationSent = sent, invitationError = detail });
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] SaveClientUserRequest request)
        {
            var clientId = GetCurrentClientId();
            var user = await _dbContext.ClientUsers.Include(u => u.Branches).FirstOrDefaultAsync(u => u.Id == id && u.ClientId == clientId);
            if (user == null) return NotFound();

            if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest("El nombre es obligatorio.");
            if (!ClientUserRoles.IsValid(request.Role)) return BadRequest("El rol no es válido.");

            var (branchIds, branchError) = await ResolveBranchesAsync(clientId, request);
            if (branchError != null) return BadRequest(branchError);

            // Nadie se quita a sí mismo el rol de Administrador (se quedaría sin poder deshacerlo).
            if (user.Id == Branch.UserId && request.Role != ClientUserRoles.Administrator)
                return BadRequest("No puedes quitarte a ti mismo el rol de Administrador.");

            if (!await OwnerRemainsAsync(clientId, user.Id, willBeActive: user.IsActive, request.Role, request.AllBranches))
                return BadRequest("Debe quedar al menos un Administrador activo con acceso a todas las sucursales.");

            user.Name = request.Name.Trim();
            user.Role = request.Role;
            user.AllBranches = request.AllBranches;

            // Quien ve todas las sucursales no lleva lista; el resto, exactamente la elegida.
            _dbContext.ClientUserBranches.RemoveRange(user.Branches.Where(b => !branchIds.Contains(b.BranchId)));
            foreach (var branchId in branchIds.Where(b => user.Branches.All(x => x.BranchId != b)))
                user.Branches.Add(new ClientUserBranch { ClientUserId = user.Id, BranchId = branchId });

            await _dbContext.SaveChangesAsync();
            return Ok(new { user.Id, user.Name, user.Email, user.Role, user.AllBranches, user.IsActive });
        }

        [HttpPost("{id:guid}/resend-invitation")]
        public async Task<IActionResult> ResendInvitation(Guid id)
        {
            var user = await _dbContext.ClientUsers.FirstOrDefaultAsync(u => u.Id == id && u.ClientId == GetCurrentClientId());
            if (user == null) return NotFound();
            if (!user.IsActive) return BadRequest("Este acceso está desactivado; reactívalo antes de reenviar la invitación.");

            var (sent, detail) = await SendInvitationAsync(user);
            return Ok(sent
                ? new { message = "Invitación reenviada.", sent = true, detail = (string?)null }
                : new { message = "No se pudo enviar el correo de invitación.", sent = false, detail });
        }

        [HttpPost("{id:guid}/deactivate")]
        public async Task<IActionResult> Deactivate(Guid id)
        {
            var clientId = GetCurrentClientId();
            var user = await _dbContext.ClientUsers.FirstOrDefaultAsync(u => u.Id == id && u.ClientId == clientId);
            if (user == null) return NotFound();

            if (user.Id == Branch.UserId) return BadRequest("No puedes desactivar tu propio acceso.");
            if (!await OwnerRemainsAsync(clientId, user.Id, willBeActive: false, user.Role, user.AllBranches))
                return BadRequest("Debe quedar al menos un Administrador activo con acceso a todas las sucursales.");

            user.IsActive = false;
            await _dbContext.SaveChangesAsync();
            return Ok(new { user.Id, user.IsActive });
        }

        [HttpPost("{id:guid}/reactivate")]
        public async Task<IActionResult> Reactivate(Guid id)
        {
            var user = await _dbContext.ClientUsers.FirstOrDefaultAsync(u => u.Id == id && u.ClientId == GetCurrentClientId());
            if (user == null) return NotFound();

            user.IsActive = true;
            await _dbContext.SaveChangesAsync();
            return Ok(new { user.Id, user.IsActive });
        }

        private static string? ValidateIdentity(string name, string email, string role)
        {
            if (string.IsNullOrWhiteSpace(name)) return "El nombre es obligatorio.";
            if (string.IsNullOrWhiteSpace(email) || !MailAddress.TryCreate(email.Trim(), out _)) return "El correo no es válido.";
            if (!ClientUserRoles.IsValid(role)) return "El rol no es válido.";
            return null;
        }

        // Sucursales del usuario: ninguna lista si ve todas; si no, al menos una y todas del Client y activas.
        private async Task<(List<Guid> BranchIds, string? Error)> ResolveBranchesAsync(Guid clientId, SaveClientUserRequest request)
        {
            if (request.AllBranches) return (new List<Guid>(), null);

            var requested = (request.BranchIds ?? new List<Guid>()).Distinct().ToList();
            if (requested.Count == 0) return (requested, "Elige al menos una sucursal o marca todas.");

            var valid = await _dbContext.Branches.AsNoTracking()
                .Where(b => b.ClientId == clientId && b.IsActive && requested.Contains(b.Id))
                .Select(b => b.Id)
                .ToListAsync();
            return valid.Count == requested.Count ? (requested, null) : (requested, "Alguna de las sucursales elegidas no existe o está inactiva.");
        }

        // Siempre debe quedar alguien que pueda administrar todo el Client: un Administrador activo con acceso a todas las sucursales.
        private async Task<bool> OwnerRemainsAsync(Guid clientId, Guid userId, bool willBeActive, string newRole, bool newAllBranches)
        {
            if (willBeActive && newRole == ClientUserRoles.Administrator && newAllBranches) return true;
            return await _dbContext.ClientUsers.AnyAsync(u =>
                u.ClientId == clientId && u.Id != userId && u.IsActive && u.Role == ClientUserRoles.Administrator && u.AllBranches);
        }

        private async Task<(bool Sent, string? Detail)> SendInvitationAsync(ClientUser user)
        {
            var client = await _dbContext.Clients.AsNoTracking().Include(c => c.Tenant).FirstAsync(c => c.Id == user.ClientId);
            var result = await _passwordResetService.RequestAsync(
                PortalUserType.Client, user.Id, user.Email, user.Name, _portalUrl, "invitation", client.Tenant?.CoreTenantId,
                client.Tenant?.LogoLightUrl, client.Tenant?.CommercialName);

            if (result.IsSuccess) return (true, null);
            return (false, result.IsNotConfigured ? "El servicio de correo no está configurado." : (result.Error ?? "No se pudo enviar la invitación."));
        }
    }
}
