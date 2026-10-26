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
    // Deja elegida la plantilla "principal" de cada tipo de documento que se le habilita a un Client, para que no quede sin plantilla
    // (emitir por Dataico la exige elegida y publicada). La principal es la publicada más específica: la propia del Client, si no la del
    // Tenant, si no la global; y entre varias del mismo alcance la más antigua, que es la que se publicó primero para ese tipo. El Client
    // puede cambiarla después en Diseño de mis Facturas. Nunca pisa una elección que ya exista.
    //
    // Solo agrega las selecciones al contexto: guardarlas es del llamador, en el mismo SaveChanges con que habilita los tipos.
    public static class DefaultTemplateAssigner
    {
        public static async Task AddDefaultsAsync(FelDbContext db, Guid clientId, Guid tenantId, IEnumerable<Guid> documentTypeIds, CancellationToken ct = default)
        {
            var typeIds = documentTypeIds.Distinct().ToList();
            if (typeIds.Count == 0) return;

            var alreadyChosen = (await db.ClientDocumentSettings.AsNoTracking()
                .Where(s => s.ClientId == clientId && s.DocumentTypeId != null && typeIds.Contains(s.DocumentTypeId.Value))
                .Select(s => s.DocumentTypeId!.Value)
                .ToListAsync(ct)).ToHashSet();
            var missing = typeIds.Where(id => !alreadyChosen.Contains(id)).ToList();
            if (missing.Count == 0) return;

            var candidates = await db.DocumentTemplates.AsNoTracking()
                .Where(t => t.DocumentTypeId != null && missing.Contains(t.DocumentTypeId.Value) && t.Status == TemplateStatus.Published
                    && ((t.TenantId == null && t.ClientId == null) || (t.TenantId == tenantId && t.ClientId == null) || t.ClientId == clientId))
                .ToListAsync(ct);

            foreach (var typeId in missing)
            {
                var main = candidates
                    .Where(t => t.DocumentTypeId == typeId)
                    .OrderBy(t => t.ClientId == clientId ? 0 : t.TenantId == tenantId ? 1 : 2)
                    .ThenBy(t => t.CreatedAt)
                    .ThenBy(t => t.Id)
                    .FirstOrDefault();
                if (main == null) continue;

                db.ClientDocumentSettings.Add(new ClientDocumentSetting
                {
                    Id = Guid.NewGuid(),
                    ClientId = clientId,
                    DocumentTypeId = typeId,
                    SelectedTemplateId = main.Id,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }
    }
}
