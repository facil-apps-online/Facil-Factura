using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Services
{
    // Calculaba automáticamente las retenciones por línea a partir del concepto de retención del
    // producto, el tipo de persona del tercero adquirente, y los parámetros tributarios vigentes
    // (UVT). Se desconectó de InvoiceController (la retención ahora se elige 100% manual por línea,
    // ver InvoicesPage.tsx) porque la resolución automática asumía que las personas naturales nunca
    // retenían, lo cual no es correcto en general. Se conserva sin usar por si se retoma la
    // automatización con reglas corregidas.
    public class RetentionCalculationService
    {
        private readonly FelDbContext _dbContext;

        public RetentionCalculationService(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // Recibe la lista de ítems explícita (en vez de Document.Items) porque en algunos flujos de
        // actualización los ítems nuevos se agregan directo por el DbSet y la navegación del
        // Document padre podría no reflejarlos todavía de forma confiable.
        public async Task ApplyRetentionsAsync(IEnumerable<DocumentItem> items, DateTime issueDate, Client client, Customer customer)
        {
            var itemList = items as IReadOnlyList<DocumentItem> ?? items.ToList();

            foreach (var item in itemList)
            {
                item.Retentions.Clear();
            }

            if (!client.AppliesRetentions) return;

            var groupKeysByProductId = await ResolveProductGroupKeysAsync(itemList);
            if (groupKeysByProductId.Count == 0) return;

            // NIT (identificación 31) = persona jurídica; cualquier otro tipo de identificación se
            // trata como persona natural, criterio acordado como aproximación de "declarante".
            var personType = customer.IdentificationType == "31" ? RetentionPersonType.Juridica : RetentionPersonType.Natural;
            var uvt = await GetParameterValueAsync("UVT", issueDate);

            foreach (var item in itemList)
            {
                if (item.RetentionOverridden) continue;
                if (!item.ProductId.HasValue || !groupKeysByProductId.TryGetValue(item.ProductId.Value, out var groupKey) || string.IsNullOrEmpty(groupKey))
                    continue;

                var concept = await ResolveConceptAsync(groupKey, personType, issueDate);
                if (concept == null) continue;

                var lineBase = item.Quantity * item.UnitPrice * (1 - item.DiscountRate / 100);
                var minBase = concept.BaseUvt * uvt;
                if (lineBase < minBase) continue;

                var retentionBase = concept.BaseType == RetentionBaseType.IvaGenerado ? item.TaxAmount : lineBase;
                if (retentionBase <= 0) continue;

                item.Retentions.Add(new DocumentRetention
                {
                    Id = Guid.NewGuid(),
                    DocumentItemId = item.Id,
                    TaxCategory = concept.TaxCategory,
                    Rate = concept.Rate,
                    BaseAmount = retentionBase,
                    Amount = retentionBase * concept.Rate / 100
                });
            }
        }

        private async Task<Dictionary<Guid, string?>> ResolveProductGroupKeysAsync(IEnumerable<DocumentItem> items)
        {
            var productIds = items.Where(i => i.ProductId.HasValue).Select(i => i.ProductId!.Value).Distinct().ToList();
            if (productIds.Count == 0) return new Dictionary<Guid, string?>();

            return await _dbContext.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.RetentionGroupKey);
        }

        private async Task<decimal> GetParameterValueAsync(string code, DateTime asOf)
        {
            var parameter = await _dbContext.TaxParameters
                .Where(p => p.Code == code && p.EffectiveFrom <= asOf)
                .OrderByDescending(p => p.EffectiveFrom)
                .FirstOrDefaultAsync();
            return parameter?.Value ?? 0m;
        }

        // Prioriza la variante que coincide exactamente con el tipo de persona del tercero sobre la
        // genérica ("Ambas"), y dentro de cada una, la de vigencia más reciente que ya haya iniciado.
        private async Task<RetentionConcept?> ResolveConceptAsync(string groupKey, RetentionPersonType personType, DateTime asOf)
        {
            var candidates = await _dbContext.RetentionConcepts
                .Where(c => c.GroupKey == groupKey && c.IsActive && c.EffectiveFrom <= asOf
                    && (c.PersonType == personType || c.PersonType == RetentionPersonType.Ambas))
                .ToListAsync();

            return candidates
                .OrderByDescending(c => c.PersonType == personType)
                .ThenByDescending(c => c.EffectiveFrom)
                .FirstOrDefault();
        }
    }
}
