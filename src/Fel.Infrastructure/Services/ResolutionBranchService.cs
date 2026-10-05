using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Services
{
    // Sucursales donde se usa una resolución. Lo comparten el portal del cliente y tenant-web para que ambos apliquen las mismas reglas.
    public sealed class ResolutionBranchService
    {
        private readonly FelDbContext _dbContext;

        public ResolutionBranchService(FelDbContext dbContext) => _dbContext = dbContext;

        // Sucursales donde se usará una resolución nueva: las elegidas; si no se eligió ninguna, las de la resolución que reemplaza
        // (mismo tipo y prefijo, para no dejar sin ella a las demás sucursales) y, si no hay, 'fallbackBranchId' (la sucursal activa
        // en el portal, la principal en el tenant).
        public async Task<(List<Guid> Ids, string? Error)> ResolveForNewAsync(Guid clientId, List<Guid>? requested, string documentType, string prefix, Guid? fallbackBranchId)
        {
            var ids = (requested ?? new List<Guid>()).Distinct().ToList();
            if (ids.Count == 0)
            {
                ids = await _dbContext.ResolutionBranches
                    .Where(rb => rb.Resolution.ClientId == clientId && rb.Resolution.DocumentType == documentType && rb.Resolution.Prefix == prefix && rb.Resolution.IsActive)
                    .Select(rb => rb.BranchId).Distinct().ToListAsync();
                if (ids.Count == 0 && fallbackBranchId is Guid fallback) ids.Add(fallback);
                if (ids.Count == 0) return (ids, "Elige las sucursales donde se usará la resolución.");
            }
            return await ValidBranchesAsync(clientId, ids);
        }

        // Las sucursales deben ser del Client y estar activas.
        public async Task<(List<Guid> Ids, string? Error)> ValidBranchesAsync(Guid clientId, List<Guid> ids)
        {
            var valid = await _dbContext.Branches.AsNoTracking()
                .Where(b => b.ClientId == clientId && b.IsActive && ids.Contains(b.Id))
                .Select(b => b.Id).ToListAsync();
            return valid.Count == ids.Count ? (valid, null) : (valid, "Alguna de las sucursales elegidas no existe o está inactiva.");
        }

        // Reemplaza las sucursales donde se usa la resolución. Debe quedar al menos una: sin sucursal nadie podría emitir con ella.
        // Devuelve null si la resolución no es del Client.
        public async Task<(List<Guid>? Ids, string? Error)> SetBranchesAsync(Guid clientId, Guid resolutionId, List<Guid> branchIds)
        {
            if (!await _dbContext.Resolutions.AnyAsync(r => r.Id == resolutionId && r.ClientId == clientId)) return (null, null);

            var requested = branchIds.Distinct().ToList();
            if (requested.Count == 0) return (new List<Guid>(), "La resolución debe estar disponible en al menos una sucursal.");

            var (valid, error) = await ValidBranchesAsync(clientId, requested);
            if (error != null) return (valid, error);

            var current = await _dbContext.ResolutionBranches.Where(rb => rb.ResolutionId == resolutionId).ToListAsync();
            _dbContext.ResolutionBranches.RemoveRange(current.Where(rb => !valid.Contains(rb.BranchId)));
            foreach (var branchId in valid.Where(b => current.All(rb => rb.BranchId != b)))
                _dbContext.ResolutionBranches.Add(BranchProvisioning.LinkResolution(resolutionId, branchId));

            await _dbContext.SaveChangesAsync();
            return (valid, null);
        }
    }
}
