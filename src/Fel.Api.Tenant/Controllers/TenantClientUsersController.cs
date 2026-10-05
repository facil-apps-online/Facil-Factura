using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Fel.Api.Tenant.Controllers
{
    // Usuarios del portal de un Client, gestionados por su Tenant. Son los mismos que administra el Administrador del cliente desde su
    // portal (ClientUserAdminService aplica las mismas reglas a ambos); aquí no hay "usuario actual", así que el tenant puede corregir
    // cualquier acceso, siempre que quede un Administrador activo con todas las sucursales.
    [ApiController]
    [Route("api/tenant/clients/{clientId}/users")]
    public class TenantClientUsersController : ControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly ClientUserAdminService _users;
        private readonly string _clientPortalUrl;

        public TenantClientUsersController(FelDbContext dbContext, ClientUserAdminService users, IConfiguration config)
        {
            _dbContext = dbContext;
            _users = users;
            _clientPortalUrl = config["ClientPortalUrl"] ?? "https://clients.facil-factura.pro";
        }

        public class SaveClientUserRequest
        {
            public string Name { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Role { get; set; } = string.Empty;
            public bool AllBranches { get; set; }
            public List<Guid>? BranchIds { get; set; }
        }

        private Guid GetCurrentTenantId()
        {
            if (Request.Headers.TryGetValue("x-tenant-id", out var tenantIdStr) && Guid.TryParse(tenantIdStr, out var tenantId))
                return tenantId;
            throw new UnauthorizedAccessException("x-tenant-id Header is missing");
        }

        private async Task<bool> OwnsClientAsync(Guid clientId)
        {
            var tenantId = GetCurrentTenantId();
            return await _dbContext.Clients.AnyAsync(c => c.Id == clientId && c.TenantId == tenantId);
        }

        private static ClientUserAdminService.SaveInput ToInput(SaveClientUserRequest r) =>
            new(r.Name ?? string.Empty, r.Email ?? string.Empty, r.Role ?? string.Empty, r.AllBranches, r.BranchIds);

        private IActionResult Failure<T>(UserAdminResult<T> result) =>
            result.NotFound ? NotFound() : BadRequest(result.Error);

        // Catálogo de roles para el formulario (así tenant-web no los escribe a mano).
        [HttpGet("roles")]
        public IActionResult Roles()
        {
            try
            {
                GetCurrentTenantId();
                var roles = new List<object>();
                foreach (var (value, label) in ClientUserRoles.All) roles.Add(new { value, label });
                return Ok(roles);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(ex.Message); }
        }

        [HttpGet]
        public async Task<IActionResult> List(Guid clientId)
        {
            try
            {
                if (!await OwnsClientAsync(clientId)) return NotFound();
                return Ok(await _users.ListAsync(clientId, null));
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(ex.Message); }
        }

        [HttpPost]
        public async Task<IActionResult> Create(Guid clientId, [FromBody] SaveClientUserRequest request)
        {
            try
            {
                if (!await OwnsClientAsync(clientId)) return NotFound();

                var result = await _users.CreateAsync(clientId, ToInput(request), _clientPortalUrl);
                if (!result.Succeeded) return Failure(result);

                var (user, invitation) = result.Value;
                return Ok(new { user.Id, user.Name, user.Email, user.Role, user.AllBranches, user.IsActive, invitationSent = invitation.Sent, invitationError = invitation.Error });
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(ex.Message); }
        }

        [HttpPut("{userId:guid}")]
        public async Task<IActionResult> Update(Guid clientId, Guid userId, [FromBody] SaveClientUserRequest request)
        {
            try
            {
                if (!await OwnsClientAsync(clientId)) return NotFound();

                var result = await _users.UpdateAsync(clientId, userId, ToInput(request), null);
                if (!result.Succeeded) return Failure(result);

                var user = result.Value!;
                return Ok(new { user.Id, user.Name, user.Email, user.Role, user.AllBranches, user.IsActive });
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(ex.Message); }
        }

        [HttpPost("{userId:guid}/resend-invitation")]
        public async Task<IActionResult> ResendInvitation(Guid clientId, Guid userId)
        {
            try
            {
                if (!await OwnsClientAsync(clientId)) return NotFound();

                var result = await _users.ResendInvitationAsync(clientId, userId, _clientPortalUrl);
                if (!result.Succeeded) return Failure(result);

                var invitation = result.Value!;
                return Ok(invitation.Sent
                    ? new { message = "Invitación reenviada.", sent = true, detail = (string?)null }
                    : new { message = "No se pudo enviar el correo de invitación.", sent = false, detail = invitation.Error });
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(ex.Message); }
        }

        [HttpPost("{userId:guid}/deactivate")]
        public async Task<IActionResult> Deactivate(Guid clientId, Guid userId)
        {
            try
            {
                if (!await OwnsClientAsync(clientId)) return NotFound();

                var result = await _users.SetActiveAsync(clientId, userId, active: false, null);
                return result.Succeeded ? Ok(new { result.Value!.Id, result.Value.IsActive }) : Failure(result);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(ex.Message); }
        }

        [HttpPost("{userId:guid}/reactivate")]
        public async Task<IActionResult> Reactivate(Guid clientId, Guid userId)
        {
            try
            {
                if (!await OwnsClientAsync(clientId)) return NotFound();

                var result = await _users.SetActiveAsync(clientId, userId, active: true, null);
                return result.Succeeded ? Ok(new { result.Value!.Id, result.Value.IsActive }) : Failure(result);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(ex.Message); }
        }
    }
}
