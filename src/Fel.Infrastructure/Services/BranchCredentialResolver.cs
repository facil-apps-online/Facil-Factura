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
    // Credenciales efectivas de MinSalud, IHCE, buzón de recepción y eventos automáticos de una sucursal: las propias si las tiene; si no,
    // las de la sucursal principal (que son el valor por defecto de las demás). Si ni siquiera la principal las tiene, quedan vacías.
    // Las claves siguen cifradas: quien las usa las descifra con ICryptoService.
    public sealed record MinSaludCredentials(
        bool IsBranchOwn, string Environment, string? UserType,
        string? IdentificationType, string? IdentificationNumber, string? PasswordEncrypted,
        string? TestIdentificationType, string? TestIdentificationNumber, string? TestPasswordEncrypted);

    public sealed record IhceCredentials(
        bool IsBranchOwn, string? ClientId, string? ClientSecretEncrypted, string? ApimSubscriptionKeyEncrypted,
        string? TenantId, string? Endpoint, string Environment);

    // BranchId es la sucursal a la que pertenece lo que llegue por este buzón.
    public sealed record ReceptionMailbox(
        Guid ClientId, Guid BranchId, bool IsBranchOwn, bool Enabled, string Host, int Port, bool UseSsl, string User, string PasswordEncrypted);

    // Eventos RADIAN que se crean solos al recibir un documento en la sucursal.
    public sealed record ReceptionEvents(bool IsBranchOwn, bool AcuseRecibo, bool ReciboBien, bool Aceptacion, bool Reclamo);

    public sealed class BranchCredentialResolver
    {
        private readonly FelDbContext _dbContext;

        public BranchCredentialResolver(FelDbContext dbContext) => _dbContext = dbContext;

        public static readonly MinSaludCredentials NoMinSalud = new(false, MinSaludEnvironments.Test, null, null, null, null, null, null, null);
        public static readonly IhceCredentials NoIhce = new(false, null, null, null, null, null, "Sandbox");
        public static readonly ReceptionEvents NoEvents = new(false, false, false, false, false);

        // Sucursal efectiva para leer: la pedida (que debe ser del Client) o, si no se indica, la principal.
        private async Task<(Guid BranchId, Guid MainId)> BranchesAsync(Guid clientId, Guid? branchId)
        {
            var mainId = await BranchProvisioning.MainBranchIdAsync(_dbContext, clientId);
            return (branchId ?? mainId, mainId);
        }

        public async Task<MinSaludCredentials> MinSaludAsync(Guid clientId, Guid? branchId)
        {
            var (id, mainId) = await BranchesAsync(clientId, branchId);
            var own = await _dbContext.BranchMinSaludCredentials.AsNoTracking().FirstOrDefaultAsync(x => x.BranchId == id && x.Branch.ClientId == clientId);
            var row = own ?? (id == mainId ? null : await _dbContext.BranchMinSaludCredentials.AsNoTracking().FirstOrDefaultAsync(x => x.BranchId == mainId));
            return row == null
                ? NoMinSalud
                : new MinSaludCredentials(own != null, row.Environment, row.UserType, row.IdentificationType, row.IdentificationNumber, row.PasswordEncrypted,
                    row.TestIdentificationType, row.TestIdentificationNumber, row.TestPasswordEncrypted);
        }

        public async Task<IhceCredentials> IhceAsync(Guid clientId, Guid? branchId)
        {
            var (id, mainId) = await BranchesAsync(clientId, branchId);
            var own = await _dbContext.BranchIhceCredentials.AsNoTracking().FirstOrDefaultAsync(x => x.BranchId == id && x.Branch.ClientId == clientId);
            var row = own ?? (id == mainId ? null : await _dbContext.BranchIhceCredentials.AsNoTracking().FirstOrDefaultAsync(x => x.BranchId == mainId));
            return row == null
                ? NoIhce
                : new IhceCredentials(own != null, row.ClientId, row.ClientSecretEncrypted, row.ApimSubscriptionKeyEncrypted, row.TenantId, row.Endpoint, row.Environment);
        }

        // Buzón efectivo de una sucursal (para mostrarlo y probar la conexión). Si no hay ninguno devuelve uno vacío y deshabilitado.
        public async Task<ReceptionMailbox> ReceptionMailboxAsync(Guid clientId, Guid? branchId)
        {
            var (id, mainId) = await BranchesAsync(clientId, branchId);
            var own = await _dbContext.BranchReceptionMailboxes.AsNoTracking().FirstOrDefaultAsync(x => x.BranchId == id && x.Branch.ClientId == clientId);
            var row = own ?? (id == mainId ? null : await _dbContext.BranchReceptionMailboxes.AsNoTracking().FirstOrDefaultAsync(x => x.BranchId == mainId));
            return row == null
                ? new ReceptionMailbox(clientId, id, false, false, string.Empty, 993, true, string.Empty, string.Empty)
                : new ReceptionMailbox(clientId, id, own != null, row.Enabled, row.Host, row.Port, row.UseSsl, row.User, row.PasswordEncrypted);
        }

        public async Task<ReceptionEvents> ReceptionEventsAsync(Guid clientId, Guid? branchId)
        {
            var (id, mainId) = await BranchesAsync(clientId, branchId);
            var own = await _dbContext.BranchReceptionEvents.AsNoTracking().FirstOrDefaultAsync(x => x.BranchId == id && x.Branch.ClientId == clientId);
            var row = own ?? (id == mainId ? null : await _dbContext.BranchReceptionEvents.AsNoTracking().FirstOrDefaultAsync(x => x.BranchId == mainId));
            return row == null ? NoEvents : new ReceptionEvents(own != null, row.AutoSendAcuseRecibo, row.AutoSendReciboBien, row.AutoSendAceptacion, row.AutoSendReclamo);
        }

        // Todos los buzones que hay que revisar: el de cada sucursal activa que lo tenga habilitado (lo que llegue queda en ella), de Clients
        // activos. Las sucursales sin buzón propio no se revisan: no tienen dónde recibir (el de la principal es de la principal).
        public async Task<List<ReceptionMailbox>> ActiveReceptionMailboxesAsync(CancellationToken ct)
        {
            return await _dbContext.BranchReceptionMailboxes.AsNoTracking()
                .Where(m => m.Enabled && m.Branch.IsActive && m.Branch.Client.IsActive && !m.Branch.Client.IsDeveloperSandbox)
                .Select(m => new ReceptionMailbox(m.Branch.ClientId, m.BranchId, true, true, m.Host, m.Port, m.UseSsl, m.User, m.PasswordEncrypted))
                .ToListAsync(ct);
        }
    }
}
