using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Services
{
    // Nombre en letras de cada moneda (PESOS, DÓLARES, EUROS), tomado del catálogo de monedas que administra el superadmin: una moneda nueva
    // sale bien en el valor en letras sin tocar código.
    public static class CurrencyWords
    {
        public static async Task<Dictionary<string, string>> LoadAsync(FelDbContext dbContext) =>
            await dbContext.TaxCatalogItems.AsNoTracking()
                .Where(i => i.Kind == TaxCatalogKind.Currency && i.WordsName != null && i.WordsName != "")
                .ToDictionaryAsync(i => i.Category, i => i.WordsName!);

        // Un documento sin moneda es en pesos. Si a la moneda le faltara el nombre (el superadmin lo exige, así que no debería pasar), se usa el
        // código ISO: preferible a nombrar otra moneda por error.
        public static string Resolve(IReadOnlyDictionary<string, string>? catalog, string? currency)
        {
            var code = string.IsNullOrWhiteSpace(currency) ? "COP" : currency;
            return catalog != null && catalog.TryGetValue(code, out var name) ? name : code;
        }
    }
}
