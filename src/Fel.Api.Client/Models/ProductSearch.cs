using Fel.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Client.Models
{
    // Búsqueda y páginas del catálogo de productos: hay clientes con miles de productos, así que el servidor busca, ordena y corta, y el portal solo
    // recibe lo que muestra (la lista de Productos por páginas y, en los formularios, las primeras coincidencias de lo que se escribe).
    public static class ProductSearch
    {
        public const int DefaultLimit = 20;
        public const int MaxLimit = 50;
        public const int MaxByIds = 200;

        // Todas las palabras escritas deben aparecer, en cualquier orden, en el código, el código estándar o el nombre.
        public static IQueryable<Product> Filter(IQueryable<Product> query, string? text)
        {
            foreach (var term in (text ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var t = term;
                query = query.Where(p => p.Code.Contains(t) || p.Name.Contains(t) || p.StandardCode.Contains(t));
            }
            return query;
        }

        // Primeras coincidencias para un selector de formulario: primero los que empiezan por lo escrito; sin texto, los más recientes.
        public static async Task<List<Product>> SearchAsync(IQueryable<Product> query, ProductScope? scope, string? text, int limit)
        {
            limit = Math.Clamp(limit, 1, MaxLimit);
            if (scope.HasValue) query = query.Where(p => p.Scope == scope.Value);
            var q = (text ?? string.Empty).Trim();
            query = Filter(query, q).Include(p => p.Taxes).Include(p => p.UnitOfMeasure);
            var ordered = q.Length == 0
                ? query.OrderByDescending(p => p.CreatedAt)
                : query.OrderBy(p => p.Code.StartsWith(q) || p.Name.StartsWith(q) ? 0 : 1).ThenBy(p => p.Name);
            return await ordered.Take(limit).ToListAsync();
        }

        // Una página de la lista de Productos, con cuántos hay en total y cuántos tienen los impuestos por completar (todos los del alcance, para el aviso).
        public static async Task<(List<Product> Items, int Total, int PendingCount)> PageAsync(IQueryable<Product> query, ProductScope? scope, string? text, bool onlyPending,
            string? sort, string? dir, int page, int pageSize)
        {
            pageSize = Math.Clamp(pageSize, 1, 100);
            page = Math.Max(page, 1);
            if (scope.HasValue) query = query.Where(p => p.Scope == scope.Value);
            var pendingCount = await query.CountAsync(p => p.TaxesPending);
            if (onlyPending) query = query.Where(p => p.TaxesPending);
            query = Filter(query, text);

            var total = await query.CountAsync();
            var desc = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
            var ordered = (sort, desc) switch
            {
                ("code", false) => query.OrderBy(p => p.Code),
                ("code", true) => query.OrderByDescending(p => p.Code),
                ("price", false) => query.OrderBy(p => p.UnitPrice),
                ("price", true) => query.OrderByDescending(p => p.UnitPrice),
                // Igual que la columna IVA de la tabla: los pendientes primero, luego los no gravados y luego por tarifa.
                ("iva", false) => query.OrderBy(p => p.TaxesPending ? -2m : p.IvaTreatment == IvaTreatment.Gravado ? p.IvaRate : -1m),
                ("iva", true) => query.OrderByDescending(p => p.TaxesPending ? -2m : p.IvaTreatment == IvaTreatment.Gravado ? p.IvaRate : -1m),
                (_, true) => query.OrderByDescending(p => p.Name),
                _ => query.OrderBy(p => p.Name)
            };
            var items = await ordered.ThenBy(p => p.Id).Skip((page - 1) * pageSize).Take(pageSize)
                .Include(p => p.Taxes).Include(p => p.UnitOfMeasure).ToListAsync();
            return (items, total, pendingCount);
        }
    }
}
