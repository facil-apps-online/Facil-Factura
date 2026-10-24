using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Services
{
    // Resultado de una operación sobre usuarios: éxito con valor, "no existe" (404) o un mensaje de validación (400).
    public sealed class UserAdminResult<T>
    {
        public T? Value { get; private init; }
        public string? Error { get; private init; }
        public bool NotFound { get; private init; }
        public bool Succeeded => Error == null && !NotFound;

        public static UserAdminResult<T> Ok(T value) => new() { Value = value };
        public static UserAdminResult<T> Invalid(string error) => new() { Error = error };
        public static UserAdminResult<T> Missing() => new() { NotFound = true };
    }

    // Administración de los usuarios del portal de un Client: la usan el portal (su Administrador) y tenant-web (el tenant), con las mismas
    // reglas. Cada usuario tiene un rol y las sucursales a las que puede entrar.
    public sealed class ClientUserAdminService
    {
        private readonly FelDbContext _dbContext;
        private readonly PasswordResetService _passwordResetService;

        public ClientUserAdminService(FelDbContext dbContext, PasswordResetService passwordResetService)
        {
            _dbContext = dbContext;
            _passwordResetService = passwordResetService;
        }

        public sealed record SaveInput(string Name, string Email, string Role, bool AllBranches, IReadOnlyList<Guid>? BranchIds);
        public sealed record UserRow(Guid Id, string Name, string Email, string Role, bool AllBranches, bool IsActive, DateTime CreatedAt, IReadOnlyList<Guid> BranchIds, bool IsSelf);
        public sealed record InvitationOutcome(bool Sent, string? Error);

        // actingUserId: el usuario del portal que hace la operación (null cuando la hace el tenant), para impedir que alguien se quite
        // a sí mismo el acceso.
        public async Task<IReadOnlyList<UserRow>> ListAsync(Guid clientId, Guid? actingUserId)
        {
            var users = await _dbContext.ClientUsers.AsNoTracking()
                .Where(u => u.ClientId == clientId)
                .OrderBy(u => u.CreatedAt)
                .Select(u => new
                {
                    u.Id, u.Name, u.Email, u.Role, u.AllBranches, u.IsActive, u.CreatedAt,
                    BranchIds = u.Branches.Select(b => b.BranchId).ToList()
                })
                .ToListAsync();

            // Quien ve todas las sucursales no depende de la lista; se devuelve vacía para no mostrar enlaces que no aplican.
            return users.Select(u => new UserRow(u.Id, u.Name, u.Email, u.Role, u.AllBranches, u.IsActive, u.CreatedAt,
                u.AllBranches ? new List<Guid>() : u.BranchIds, actingUserId.HasValue && u.Id == actingUserId.Value)).ToList();
        }

        public async Task<UserAdminResult<(ClientUser User, InvitationOutcome Invitation)>> CreateAsync(Guid clientId, SaveInput input, string portalUrl)
        {
            var error = ValidateIdentity(input.Name, input.Email, input.Role);
            if (error != null) return UserAdminResult<(ClientUser, InvitationOutcome)>.Invalid(error);

            var (branchIds, branchError) = await ResolveBranchesAsync(clientId, input);
            if (branchError != null) return UserAdminResult<(ClientUser, InvitationOutcome)>.Invalid(branchError);

            var email = input.Email.Trim();
            // En una empresa el correo no se repite; en distintas empresas del mismo tenant sí: es la misma persona (un contador con varios clientes).
            if (await _dbContext.ClientUsers.AnyAsync(u => u.ClientId == clientId && u.Email == email))
                return UserAdminResult<(ClientUser, InvitationOutcome)>.Invalid("Ese correo ya tiene acceso en esta empresa.");

            // ¿La persona ya tiene acceso en otra empresa de este tenant? Entonces comparte su contraseña y su estado de invitación.
            var tenantId = await _dbContext.Clients.AsNoTracking().Where(c => c.Id == clientId).Select(c => c.TenantId).FirstAsync();
            var existing = await _dbContext.ClientUsers.AsNoTracking()
                .Where(u => u.Email == email && u.Client.TenantId == tenantId)
                .OrderBy(u => u.CreatedAt).FirstOrDefaultAsync();

            // Sin contraseña manual: se crea con un hash aleatorio que nadie conoce y se invita a la persona a que establezca la suya.
            var user = new ClientUser
            {
                Id = Guid.NewGuid(),
                ClientId = clientId,
                Name = input.Name.Trim(),
                Email = email,
                PasswordHash = existing?.PasswordHash ?? BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),
                InvitedAt = existing?.InvitedAt,
                Role = input.Role,
                AllBranches = input.AllBranches,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            foreach (var branchId in branchIds)
                user.Branches.Add(new ClientUserBranch { ClientUserId = user.Id, BranchId = branchId });
            _dbContext.ClientUsers.Add(user);
            await _dbContext.SaveChangesAsync();

            // Quien ya tenía acceso en otra empresa del tenant no necesita otra invitación: entra con su misma contraseña y verá también esta empresa.
            if (existing != null)
                return UserAdminResult<(ClientUser, InvitationOutcome)>.Ok((user, new InvitationOutcome(false,
                    "Este correo ya tiene acceso a otra empresa del tenant: entra con su misma contraseña y verá también esta empresa. No se envió otra invitación.")));

            return UserAdminResult<(ClientUser, InvitationOutcome)>.Ok((user, await SendInvitationAsync(user, portalUrl)));
        }

        public async Task<UserAdminResult<ClientUser>> UpdateAsync(Guid clientId, Guid userId, SaveInput input, Guid? actingUserId)
        {
            var user = await _dbContext.ClientUsers.Include(u => u.Branches).FirstOrDefaultAsync(u => u.Id == userId && u.ClientId == clientId);
            if (user == null) return UserAdminResult<ClientUser>.Missing();

            if (string.IsNullOrWhiteSpace(input.Name)) return UserAdminResult<ClientUser>.Invalid("El nombre es obligatorio.");
            if (!ClientUserRoles.IsValid(input.Role)) return UserAdminResult<ClientUser>.Invalid("El rol no es válido.");

            var (branchIds, branchError) = await ResolveBranchesAsync(clientId, input);
            if (branchError != null) return UserAdminResult<ClientUser>.Invalid(branchError);

            // Nadie se quita a sí mismo el rol de Administrador (se quedaría sin poder deshacerlo).
            if (actingUserId == user.Id && input.Role != ClientUserRoles.Administrator)
                return UserAdminResult<ClientUser>.Invalid("No puedes quitarte a ti mismo el rol de Administrador.");

            if (!await OwnerRemainsAsync(clientId, user.Id, willBeActive: user.IsActive, input.Role, input.AllBranches))
                return UserAdminResult<ClientUser>.Invalid("Debe quedar al menos un Administrador activo con acceso a todas las sucursales.");

            user.Name = input.Name.Trim();
            user.Role = input.Role;
            user.AllBranches = input.AllBranches;

            // Quien ve todas las sucursales no lleva lista; el resto, exactamente la elegida.
            _dbContext.ClientUserBranches.RemoveRange(user.Branches.Where(b => !branchIds.Contains(b.BranchId)));
            foreach (var branchId in branchIds.Where(b => user.Branches.All(x => x.BranchId != b)))
                user.Branches.Add(new ClientUserBranch { ClientUserId = user.Id, BranchId = branchId });

            await _dbContext.SaveChangesAsync();
            return UserAdminResult<ClientUser>.Ok(user);
        }

        public async Task<UserAdminResult<InvitationOutcome>> ResendInvitationAsync(Guid clientId, Guid userId, string portalUrl)
        {
            var user = await _dbContext.ClientUsers.FirstOrDefaultAsync(u => u.Id == userId && u.ClientId == clientId);
            if (user == null) return UserAdminResult<InvitationOutcome>.Missing();
            if (!user.IsActive) return UserAdminResult<InvitationOutcome>.Invalid("Este acceso está desactivado; reactívalo antes de reenviar la invitación.");

            return UserAdminResult<InvitationOutcome>.Ok(await SendInvitationAsync(user, portalUrl));
        }

        // El enlace de invitación para que quien administra lo copie y lo envíe por otro medio; cuenta como invitación hecha.
        public async Task<UserAdminResult<string>> GetInvitationLinkAsync(Guid clientId, Guid userId, string portalUrl)
        {
            var user = await _dbContext.ClientUsers.FirstOrDefaultAsync(u => u.Id == userId && u.ClientId == clientId);
            if (user == null) return UserAdminResult<string>.Missing();
            if (!user.IsActive) return UserAdminResult<string>.Invalid("Este acceso está desactivado; reactívalo antes de copiar el enlace de invitación.");

            var link = await _passwordResetService.GetLinkAsync(PortalUserType.Client, user.Id, portalUrl);
            var tenantId = await _dbContext.Clients.AsNoTracking().Where(c => c.Id == clientId).Select(c => c.TenantId).FirstAsync();
            await MarkInvitedAsync(user, tenantId);
            return UserAdminResult<string>.Ok(link);
        }

        // La invitación es de la persona: queda cumplida en todos sus accesos del tenant (los de su mismo correo).
        private async Task MarkInvitedAsync(ClientUser user, Guid tenantId)
        {
            var now = DateTime.UtcNow;
            var group = await _dbContext.ClientUsers.Where(u => u.Email == user.Email && u.Client.TenantId == tenantId && u.InvitedAt == null).ToListAsync();
            foreach (var member in group) member.InvitedAt = now;
            user.InvitedAt ??= now;
            await _dbContext.SaveChangesAsync();
        }

        public async Task<UserAdminResult<ClientUser>> SetActiveAsync(Guid clientId, Guid userId, bool active, Guid? actingUserId)
        {
            var user = await _dbContext.ClientUsers.FirstOrDefaultAsync(u => u.Id == userId && u.ClientId == clientId);
            if (user == null) return UserAdminResult<ClientUser>.Missing();

            if (!active)
            {
                if (actingUserId == user.Id) return UserAdminResult<ClientUser>.Invalid("No puedes desactivar tu propio acceso.");
                if (!await OwnerRemainsAsync(clientId, user.Id, willBeActive: false, user.Role, user.AllBranches))
                    return UserAdminResult<ClientUser>.Invalid("Debe quedar al menos un Administrador activo con acceso a todas las sucursales.");
            }

            user.IsActive = active;
            await _dbContext.SaveChangesAsync();
            return UserAdminResult<ClientUser>.Ok(user);
        }

        private static string? ValidateIdentity(string name, string email, string role)
        {
            if (string.IsNullOrWhiteSpace(name)) return "El nombre es obligatorio.";
            if (string.IsNullOrWhiteSpace(email) || !MailAddress.TryCreate(email.Trim(), out _)) return "El correo no es válido.";
            if (!ClientUserRoles.IsValid(role)) return "El rol no es válido.";
            return null;
        }

        // Sucursales del usuario: ninguna lista si ve todas; si no, al menos una y todas del Client y activas.
        private async Task<(List<Guid> BranchIds, string? Error)> ResolveBranchesAsync(Guid clientId, SaveInput input)
        {
            if (input.AllBranches) return (new List<Guid>(), null);

            var requested = (input.BranchIds ?? new List<Guid>()).Distinct().ToList();
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

        private async Task<InvitationOutcome> SendInvitationAsync(ClientUser user, string portalUrl)
        {
            var client = await _dbContext.Clients.AsNoTracking().Include(c => c.Tenant).FirstAsync(c => c.Id == user.ClientId);
            var result = await _passwordResetService.RequestAsync(
                PortalUserType.Client, user.Id, user.Email, user.Name, portalUrl, "invitation", client.Tenant?.CoreTenantId,
                client.Tenant?.LogoLightUrl, client.Tenant?.CommercialName);

            if (result.IsSuccess)
            {
                await MarkInvitedAsync(user, client.TenantId);
                return new InvitationOutcome(true, null);
            }
            return new InvitationOutcome(false, result.IsNotConfigured ? "El servicio de correo no está configurado." : (result.Error ?? "No se pudo enviar la invitación."));
        }
    }
}
