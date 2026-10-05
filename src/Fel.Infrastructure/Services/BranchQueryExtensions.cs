using System;
using System.Linq;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;

namespace Fel.Infrastructure.Services
{
    // Filtro por sucursal de las consultas del portal. branchId nulo = todas las sucursales (sin filtro), así el mismo
    // código sirve para una sucursal concreta y para la vista de "todas".
    public static class BranchQueryExtensions
    {
        public static IQueryable<Document> ForBranch(this IQueryable<Document> query, Guid? branchId) =>
            branchId is Guid id ? query.Where(d => d.BranchId == id) : query;

        public static IQueryable<ReceivedDocument> ForBranch(this IQueryable<ReceivedDocument> query, Guid? branchId) =>
            branchId is Guid id ? query.Where(r => r.BranchId == id) : query;

        // Las resoluciones son del Client y se relacionan con las sucursales que pueden usarlas.
        public static IQueryable<Resolution> ForBranch(this IQueryable<Resolution> query, FelDbContext dbContext, Guid? branchId) =>
            branchId is Guid id
                ? query.Where(r => dbContext.ResolutionBranches.Any(rb => rb.ResolutionId == r.Id && rb.BranchId == id))
                : query;
    }
}
