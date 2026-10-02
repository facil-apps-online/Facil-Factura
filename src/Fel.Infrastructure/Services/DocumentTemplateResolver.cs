using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Services
{
    // Cuál plantilla usar para imprimir/previsualizar un documento: la que el cliente eligió
    // explícitamente (ClientDocumentSettings) si está publicada; si no ha elegido ninguna, la más
    // específica publicada disponible (propia del cliente > del tenant > global) — mismo orden de
    // especificidad que ClientTemplatesController.GetAvailableTemplates. Se devuelve la plantilla
    // completa (no solo el RepxTemplateKey) porque InvoiceReportDataMapper.Build también necesita
    // su MostrarRetenciones. Compartido por la vista previa de facturas y de documento soporte.
    public static class DocumentTemplateResolver
    {
        public static async Task<DocumentTemplate?> ResolveAsync(FelDbContext dbContext, Client client, Guid documentTypeId)
        {
            var selected = await dbContext.ClientDocumentSettings
                .Include(s => s.SelectedTemplate)
                .FirstOrDefaultAsync(s => s.ClientId == client.Id && s.DocumentTypeId == documentTypeId);

            if (selected?.SelectedTemplate != null && selected.SelectedTemplate.Status == TemplateStatus.Published)
            {
                return selected.SelectedTemplate;
            }

            var candidates = await dbContext.DocumentTemplates
                .Where(t => t.DocumentTypeId == documentTypeId && t.Status == TemplateStatus.Published &&
                            ((t.TenantId == null && t.ClientId == null) ||
                             (t.TenantId == client.TenantId && t.ClientId == null) ||
                             t.ClientId == client.Id))
                .ToListAsync();

            return candidates.FirstOrDefault(t => t.ClientId == client.Id)
                ?? candidates.FirstOrDefault(t => t.TenantId == client.TenantId && t.ClientId == null)
                ?? candidates.FirstOrDefault(t => t.TenantId == null && t.ClientId == null);
        }
    }
}
