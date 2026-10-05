using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Services
{
    // Todo Client nace con su sucursal principal. Se usa en cada sitio que crea un Client (el alta del tenant y los
    // clientes sandbox de developers), para que ninguno quede sin sucursal.
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

        // Sucursal principal del Client, para lo que se crea fuera de una sesión del portal (tenant, correo, API).
        public static Task<Guid> MainBranchIdAsync(FelDbContext dbContext, Guid clientId) =>
            dbContext.Branches.Where(b => b.ClientId == clientId && b.IsMain).Select(b => b.Id).FirstAsync();

        // Deja una resolución disponible en una sucursal.
        public static ResolutionBranch LinkResolution(Guid resolutionId, Guid branchId) =>
            new ResolutionBranch { ResolutionId = resolutionId, BranchId = branchId };
    }
}
