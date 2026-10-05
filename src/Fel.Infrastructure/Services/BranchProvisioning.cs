using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Services
{
    // Todo Client nace con su sucursal principal y con sus contadores compartidos de notas. Se usa en cada sitio que crea un
    // Client (el alta del tenant y los clientes sandbox de developers), para que ninguno quede sin ellos.
    public static class BranchProvisioning
    {
        // La sucursal principal hereda el estado y la fecha de creación del Client: el cobro por sucursal activa da
        // lo mismo que daba por cliente activo.
        public static Branch CreateMain(Client client) => new Branch
        {
            Id = Guid.NewGuid(),
            ClientId = client.Id,
            Name = Branch.MainName,
            Code = Branch.MainCode,
            IsMain = true,
            IsActive = client.IsActive,
            CreatedAt = client.CreatedAt
        };

        // Un contador compartido por cada tipo de nota (crédito, débito y ajuste del soporte).
        public static IEnumerable<NoteNumbering> CreateSharedNoteNumberings(Guid clientId) =>
            new[] { NoteKind.CreditNote, NoteKind.DebitNote, NoteKind.SupportAdjustment }
                .Select(kind => NoteNumbering.Shared(clientId, kind));

        // Sucursal principal del Client, para lo que se crea fuera de una sesión del portal (tenant, correo, API).
        public static Task<Guid> MainBranchIdAsync(FelDbContext dbContext, Guid clientId) =>
            dbContext.Branches.Where(b => b.ClientId == clientId && b.IsMain).Select(b => b.Id).FirstAsync();

        // Precio por documento que el Tenant le cobra al Client por una sucursal (0 si el documento no tiene sucursal).
        public static async Task<decimal> PricePerDocumentAsync(FelDbContext dbContext, Guid? branchId) =>
            branchId.HasValue
                ? await dbContext.Branches.AsNoTracking().Where(b => b.Id == branchId.Value).Select(b => b.PricePerDocument).FirstOrDefaultAsync()
                : 0m;

        // Deja una resolución disponible en una sucursal.
        public static ResolutionBranch LinkResolution(Guid resolutionId, Guid branchId) =>
            new ResolutionBranch { ResolutionId = resolutionId, BranchId = branchId };

        // Ubicación del emisor para un documento: la de su sucursal (o la del Client si no tiene sucursal o ésta no tiene dirección propia).
        public static async Task<EmitterLocation> LocationAsync(FelDbContext dbContext, Client client, Guid? branchId)
        {
            var branch = branchId.HasValue
                ? await dbContext.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == branchId.Value && b.ClientId == client.Id)
                : null;
            return EmitterLocation.For(client, branch);
        }
    }
}
