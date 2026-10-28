using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Services
{
    // Lee los nombres cortos de las retenciones (RetentionShortName) y arma el texto corto de una retención: "RETE FUENTE 2.5%". Si falta una categoría
    // se usa el nombre con el que se siembra la tabla, así que un PDF o un selector nunca se quedan sin rótulo.
    public static class RetentionShortNameCatalog
    {
        public static async Task<Dictionary<string, string>> LoadAsync(FelDbContext db) =>
            await db.RetentionShortNames.AsNoTracking().ToDictionaryAsync(r => r.TaxCategory, r => r.ShortName);

        public static string NameOf(IReadOnlyDictionary<string, string>? names, string taxCategory) =>
            names != null && names.TryGetValue(taxCategory, out var name) && !string.IsNullOrWhiteSpace(name) ? name : RetentionShortName.DefaultFor(taxCategory);
    }
}
