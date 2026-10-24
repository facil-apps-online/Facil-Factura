using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Services
{
    public sealed record UnusedDraft(Guid Id, string Name, string DocumentType, DateTime CreatedAt);

    // Cuerpo de las acciones de limpieza (eliminar / archivar): los borradores que el usuario vio en la lista.
    public sealed record UnusedDraftsRequest(List<Guid>? Ids);

    // Borradores de plantilla que nunca se usaron, para limpiarlos desde los tres portales. Cada portal pasa su ámbito
    // (las globales, las del tenant o las del cliente) y solo toca lo suyo. "Sin uso" es: está en borrador, ningún cliente
    // la tiene seleccionada, ningún documento se emitió con ella y ninguna otra plantilla es versión o copia suya.
    // Se pueden borrar o archivar; el archivo .repx en Facil Reports no se toca (una copia puede compartir su clave).
    public class UnusedTemplateDraftService
    {
        private readonly FelDbContext _dbContext;

        public UnusedTemplateDraftService(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        private IQueryable<DocumentTemplate> Unused(Expression<Func<DocumentTemplate, bool>> scope) =>
            _dbContext.DocumentTemplates
                .Where(scope)
                .Where(t => t.Status == TemplateStatus.Draft
                    && !_dbContext.ClientDocumentSettings.Any(s => s.SelectedTemplateId == t.Id)
                    && !_dbContext.Documents.Any(d => d.UsedTemplateId == t.Id)
                    && !_dbContext.DocumentTemplates.Any(o => o.PreviousVersionId == t.Id || o.ClonedFromId == t.Id));

        public async Task<List<UnusedDraft>> ListAsync(Expression<Func<DocumentTemplate, bool>> scope) =>
            await Unused(scope)
                .OrderBy(t => t.CreatedAt)
                .Select(t => new UnusedDraft(t.Id, t.Name, t.DocumentType != null ? t.DocumentType.Name : "Sin tipo", t.CreatedAt))
                .ToListAsync();

        // Si algo depende de la plantilla (la tiene un cliente seleccionada, un documento la usó, o es versión anterior o
        // base de otra): en ese caso no se puede borrar. Lo usan los borrados individuales de los portales.
        public async Task<bool> IsReferencedAsync(Guid id) =>
            await _dbContext.ClientDocumentSettings.AnyAsync(s => s.SelectedTemplateId == id)
            || await _dbContext.Documents.AnyAsync(d => d.UsedTemplateId == id)
            || await _dbContext.DocumentTemplates.AnyAsync(o => o.PreviousVersionId == id || o.ClonedFromId == id);

        // Borra de los ids pedidos solo los que siguen siendo borradores sin uso al momento de borrar (la lista que vio quien
        // pidió pudo cambiar). Devuelve cuántos se borraron.
        public async Task<int> DeleteAsync(Expression<Func<DocumentTemplate, bool>> scope, IReadOnlyCollection<Guid> ids)
        {
            if (ids.Count == 0) return 0;
            var eligible = await Unused(scope).Where(t => ids.Contains(t.Id)).ToListAsync();
            _dbContext.DocumentTemplates.RemoveRange(eligible);
            await _dbContext.SaveChangesAsync();
            return eligible.Count;
        }

        // Archiva en vez de borrar: el registro queda, pero una plantilla archivada no se puede publicar ni asignar de nuevo
        // (las asignaciones solo ofrecen las publicadas). Mismas reglas de elegibilidad que el borrado.
        public async Task<int> ArchiveAsync(Expression<Func<DocumentTemplate, bool>> scope, IReadOnlyCollection<Guid> ids)
        {
            if (ids.Count == 0) return 0;
            var eligible = await Unused(scope).Where(t => ids.Contains(t.Id)).ToListAsync();
            var now = DateTime.UtcNow;
            foreach (var template in eligible)
            {
                template.Status = TemplateStatus.Archived;
                template.UpdatedAt = now;
            }
            await _dbContext.SaveChangesAsync();
            return eligible.Count;
        }
    }
}
