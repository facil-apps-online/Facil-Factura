using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Security
{
    public sealed record ApiCredentials(Client Client, Branch Branch, string Secret, bool IsSandbox, TenantBlockLevel Block);

    // Resuelve la llave de API de la autenticación HMAC a su sucursal y su Client. Las llaves son de la sucursal; el
    // Client y la sucursal deben estar activos. Una sola consulta compartida por los middlewares HMAC de todas las APIs.
    public static class ApiCredentialResolver
    {
        public static async Task<ApiCredentials?> ResolveAsync(FelDbContext dbContext, string apiKey)
        {
            // Las llaves de prueba llevan el prefijo "test_".
            var isSandbox = apiKey.StartsWith("test_");

            var branches = dbContext.Branches.Include(b => b.Client).Where(b => b.IsActive && b.Client.IsActive);
            var branch = isSandbox
                ? await branches.FirstOrDefaultAsync(b => b.TestApiKey == apiKey)
                : await branches.FirstOrDefaultAsync(b => b.LiveApiKey == apiKey);
            if (branch == null) return null;

            // Un tenant bloqueado (o con un ancestro bloqueado) deja a sus clientes sin API: el middleware responde 403.
            var block = await TenantBlocks.GetAsync(dbContext, branch.Client.TenantId);
            // Un cliente bloqueado por su tenant tampoco emite por API.
            var level = branch.Client.BlockedAt != null ? TenantBlockLevel.Blocked : block.Level;
            return new ApiCredentials(branch.Client, branch, isSandbox ? branch.TestApiSecret : branch.LiveApiSecret, isSandbox, level);
        }
    }
}
