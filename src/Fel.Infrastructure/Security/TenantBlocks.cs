using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Security
{
    public enum TenantBlockLevel
    {
        None,
        // Suspensión de la plataforma (p. ej. por mora): el tenant entra a su portal pero solo puede consultar.
        ReadOnly,
        // Bloqueo del tenant padre sobre un aliado: nadie del tenant entra.
        Blocked
    }

    public sealed record TenantBlockInfo(TenantBlockLevel Level, string? Reason)
    {
        public static readonly TenantBlockInfo None = new(TenantBlockLevel.None, null);
    }

    // Estado de bloqueo efectivo de un tenant. Cada tenant guarda dos bloqueos independientes (ver Tenant.PlatformBlockedAt y
    // Tenant.ParentBlockedAt) y el efecto baja por la jerarquía: un tenant está afectado si él o cualquiera de sus ancestros tiene
    // alguno. Se calcula al consultar, no se copia a los hijos: al levantar el bloqueo todo vuelve sin tocar nada más.
    //
    // Para los clientes (portal y API de integración) cualquier nivel distinto de None es un bloqueo total. Para los usuarios del tenant,
    // Blocked impide entrar y ReadOnly deja entrar sin poder modificar nada.
    //
    // Se guarda una foto de todos los tenants en memoria 30 segundos (son pocos): quien cambia un bloqueo la refresca de inmediato en su API
    // (Invalidate) y las demás APIs lo ven en ese lapso.
    public static class TenantBlocks
    {
        public const string ClientMessage = "Servicio suspendido. Comuníquese con su proveedor.";

        private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

        private sealed record Node(Guid? ParentId, bool Platform, string? PlatformReason, bool ParentBlock, string? ParentReason);

        private static readonly SemaphoreSlim Gate = new(1, 1);
        private static Dictionary<Guid, Node>? _snapshot;
        private static DateTime _loadedAtUtc;

        public static void Invalidate() => _snapshot = null;

        public static async Task<TenantBlockInfo> GetAsync(FelDbContext dbContext, Guid tenantId)
        {
            var map = await LoadAsync(dbContext);
            string? platformReason = null;
            var platform = false;

            var visited = new HashSet<Guid>();
            Guid? current = tenantId;
            while (current is Guid id && visited.Add(id) && map.TryGetValue(id, out var node))
            {
                // El bloqueo del padre corta de entrada: se devuelve con el motivo del nodo que lo puso.
                if (node.ParentBlock) return new TenantBlockInfo(TenantBlockLevel.Blocked, node.ParentReason);
                if (node.Platform && !platform) { platform = true; platformReason = node.PlatformReason; }
                current = node.ParentId;
            }

            return platform ? new TenantBlockInfo(TenantBlockLevel.ReadOnly, platformReason) : TenantBlockInfo.None;
        }

        private static async Task<Dictionary<Guid, Node>> LoadAsync(FelDbContext dbContext)
        {
            var snapshot = _snapshot;
            if (snapshot != null && DateTime.UtcNow - _loadedAtUtc < Ttl) return snapshot;

            await Gate.WaitAsync();
            try
            {
                snapshot = _snapshot;
                if (snapshot != null && DateTime.UtcNow - _loadedAtUtc < Ttl) return snapshot;

                snapshot = await dbContext.Tenants.AsNoTracking()
                    .Select(t => new { t.Id, t.ParentTenantId, Platform = t.PlatformBlockedAt != null, t.PlatformBlockReason, ParentBlock = t.ParentBlockedAt != null, t.ParentBlockReason })
                    .ToDictionaryAsync(t => t.Id, t => new Node(t.ParentTenantId, t.Platform, t.PlatformBlockReason, t.ParentBlock, t.ParentBlockReason));
                _snapshot = snapshot;
                _loadedAtUtc = DateTime.UtcNow;
                return snapshot;
            }
            finally
            {
                Gate.Release();
            }
        }
    }
}
