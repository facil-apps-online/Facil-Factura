using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Services
{
    // Credenciales efectivas de MinSalud, IHCE y buzón de recepción: las propias de la sucursal si las tiene; si no, las del Client (su
    // valor por defecto). Las claves siguen cifradas: quien las usa las descifra con ICryptoService.
    public sealed record MinSaludCredentials(
        bool IsBranchOwn, string Environment, string? UserType,
        string? IdentificationType, string? IdentificationNumber, string? PasswordEncrypted,
        string? TestIdentificationType, string? TestIdentificationNumber, string? TestPasswordEncrypted);

    public sealed record IhceCredentials(
        bool IsBranchOwn, string? ClientId, string? ClientSecretEncrypted, string? ApimSubscriptionKeyEncrypted,
        string? TenantId, string? Endpoint, string Environment);

    // BranchId es la sucursal a la que pertenece lo que llegue por este buzón (la principal, si es el del Client).
    public sealed record ReceptionMailbox(
        Guid ClientId, Guid BranchId, bool IsBranchOwn, bool Enabled, string Host, int Port, bool UseSsl, string User, string PasswordEncrypted);

    public sealed class BranchCredentialResolver
    {
        private readonly FelDbContext _dbContext;

        public BranchCredentialResolver(FelDbContext dbContext) => _dbContext = dbContext;

        public static MinSaludCredentials FromClient(Client c) => new(
            false, c.MinSaludEnvironment, c.MinSaludUserType,
            c.MinSaludIdentificationType, c.MinSaludIdentificationNumber, c.MinSaludPasswordEncrypted,
            c.MinSaludTestIdentificationType, c.MinSaludTestIdentificationNumber, c.MinSaludTestPasswordEncrypted);

        public static IhceCredentials FromClientIhce(Client c) => new(
            false, c.IhceClientId, c.IhceClientSecretEncrypted, c.IhceApimSubscriptionKey, c.IhceTenantId, c.IhceEndpoint, string.IsNullOrEmpty(c.IhceEnvironment) ? "Sandbox" : c.IhceEnvironment);

        public static ReceptionMailbox FromClientMailbox(Client c, Guid mainBranchId) => new(
            c.Id, mainBranchId, false, c.ReceptionEmailEnabled, c.ReceptionEmailHost, c.ReceptionEmailPort, c.ReceptionEmailUseSsl,
            c.ReceptionEmailUser, c.ReceptionEmailPasswordEncrypted);

        public async Task<MinSaludCredentials> MinSaludAsync(Client client, Guid? branchId)
        {
            var own = branchId.HasValue
                ? await _dbContext.BranchMinSaludCredentials.AsNoTracking().FirstOrDefaultAsync(x => x.BranchId == branchId.Value && x.Branch.ClientId == client.Id)
                : null;
            return own == null
                ? FromClient(client)
                : new MinSaludCredentials(true, own.Environment, own.UserType, own.IdentificationType, own.IdentificationNumber, own.PasswordEncrypted,
                    own.TestIdentificationType, own.TestIdentificationNumber, own.TestPasswordEncrypted);
        }

        public async Task<IhceCredentials> IhceAsync(Client client, Guid? branchId)
        {
            var own = branchId.HasValue
                ? await _dbContext.BranchIhceCredentials.AsNoTracking().FirstOrDefaultAsync(x => x.BranchId == branchId.Value && x.Branch.ClientId == client.Id)
                : null;
            return own == null
                ? FromClientIhce(client)
                : new IhceCredentials(true, own.ClientId, own.ClientSecretEncrypted, own.ApimSubscriptionKeyEncrypted, own.TenantId, own.Endpoint, own.Environment);
        }

        // Buzón efectivo de una sucursal (para probar la conexión).
        public async Task<ReceptionMailbox> ReceptionMailboxAsync(Client client, Guid branchId)
        {
            var own = await _dbContext.BranchReceptionMailboxes.AsNoTracking().FirstOrDefaultAsync(x => x.BranchId == branchId && x.Branch.ClientId == client.Id);
            if (own != null)
                return new ReceptionMailbox(client.Id, branchId, true, own.Enabled, own.Host, own.Port, own.UseSsl, own.User, own.PasswordEncrypted);
            return FromClientMailbox(client, await BranchProvisioning.MainBranchIdAsync(_dbContext, client.Id));
        }

        // Todos los buzones que hay que revisar: el del Client (lo que llegue queda en la principal) y el de cada sucursal activa que tenga
        // el suyo habilitado. Solo de Clients activos.
        public async Task<List<ReceptionMailbox>> ActiveReceptionMailboxesAsync(CancellationToken ct)
        {
            var clients = await _dbContext.Clients.AsNoTracking().Where(c => c.IsActive && !c.IsDeveloperSandbox).ToListAsync(ct);
            var clientIds = clients.Select(c => c.Id).ToList();
            var mainBranchByClient = await _dbContext.Branches.AsNoTracking()
                .Where(b => b.IsMain && clientIds.Contains(b.ClientId))
                .ToDictionaryAsync(b => b.ClientId, b => b.Id, ct);

            var result = new List<ReceptionMailbox>();
            foreach (var client in clients.Where(c => c.ReceptionEmailEnabled && mainBranchByClient.ContainsKey(c.Id)))
                result.Add(FromClientMailbox(client, mainBranchByClient[client.Id]));

            var own = await _dbContext.BranchReceptionMailboxes.AsNoTracking()
                .Where(m => m.Enabled && m.Branch.IsActive && clientIds.Contains(m.Branch.ClientId))
                .Select(m => new { m.Branch.ClientId, m.BranchId, m.Host, m.Port, m.UseSsl, m.User, m.PasswordEncrypted })
                .ToListAsync(ct);
            foreach (var m in own)
                result.Add(new ReceptionMailbox(m.ClientId, m.BranchId, true, true, m.Host, m.Port, m.UseSsl, m.User, m.PasswordEncrypted));

            return result;
        }
    }
}
