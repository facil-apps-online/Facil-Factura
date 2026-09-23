using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;

namespace Fel.Infrastructure.Services
{
    public class BillingMetricsService
    {
        private readonly FelDbContext _dbContext;

        public BillingMetricsService(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // Un tier con IntegratorId propio tiene prioridad sobre el tier global (IntegratorId
        // null) para el mismo rango de volumen — así Dataico puede tener una tarifa por volumen
        // distinta de DIAN directa sin que eso afecte a los integradores que no tienen tier propio.
        // Público porque SuperadminBillingController.CalculateBilling (el corte real) también lo
        // necesita, para no duplicar la lógica de selección de tier.
        public async Task<decimal> GetSuperadminTariffForVolumeAsync(int volume, Guid integratorId)
        {
            var candidates = await _dbContext.TariffTiers.AsNoTracking()
                .Where(t => t.IsActive && volume >= t.MinDocuments && (t.MaxDocuments == null || volume <= t.MaxDocuments)
                    && (t.IntegratorId == integratorId || t.IntegratorId == null))
                .ToListAsync();

            var tier = candidates.FirstOrDefault(t => t.IntegratorId == integratorId)
                ?? candidates.FirstOrDefault(t => t.IntegratorId == null);

            return tier?.PricePerDocument ?? 70m; // Default to highest if not found
        }

        // Cuánto le cobra el Tenant al Client por 'documentCount' documentos de un integrador dado
        // este período: primero se descuentan del saldo de sus bolsas prepago activas de ESE
        // integrador (FIFO, a su tarifa preferencial — ver ComputeBagConsumption), y lo que sobra
        // se cobra a la tarifa estándar (el override de ClientIntegratorBilling si existe y está
        // en modo PerDocument, si no Client.PricePerDocument de siempre). Solo lectura: no
        // descuenta las bolsas de verdad, eso lo hace el corte real cuando exista (como
        // SuperadminBillingController.ConsumeBagBalanceAsync para las bolsas de Tenant).
        private async Task<decimal> GetClientDocumentChargeAsync(Guid clientId, int documentCount, decimal defaultPricePerDocument, Guid integratorId)
        {
            if (documentCount <= 0) return 0m;

            var over = await _dbContext.ClientIntegratorBillings.AsNoTracking()
                .FirstOrDefaultAsync(b => b.ClientId == clientId && b.IntegratorId == integratorId);
            var standardRate = over != null && over.Mode == TenantBillingMode.PerDocument ? over.PricePerDocument : defaultPricePerDocument;

            var bags = await _dbContext.ClientPrepaidBags.AsNoTracking()
                .Where(b => b.ClientId == clientId && b.IntegratorId == integratorId && b.Status == PrepaidBagStatus.Active && b.RemainingBalance > 0)
                .OrderBy(b => b.PurchasedAt)
                .Select(b => new { b.Id, b.RemainingBalance, b.DiscountedPricePerDocument })
                .ToListAsync();

            var (totalDue, _) = ComputeBagConsumption(
                documentCount,
                standardRate,
                bags.Select(b => (b.Id, b.RemainingBalance, b.DiscountedPricePerDocument)));

            return totalDue;
        }

        // --- 1. Client Level Metrics ---
        public async Task<ClientBillingMetrics> GetClientMetricsAsync(Guid clientId, int year, int month)
        {
            var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var endDate = startDate.AddMonths(1);

            var client = await _dbContext.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null) throw new Exception("Client not found");

            // Los documentos de Clients sandbox quedan en "APPROVED" pero nunca se transmitieron a
            // la DIAN: son respuestas simuladas para developers (ver SandboxSimulation), así que no
            // se cobran. Se guardan igual para que el developer los vea en su portal.
            var docsByIntegrator = await _dbContext.Documents.AsNoTracking()
                .Where(d => d.ClientId == clientId && d.CreatedAt >= startDate && d.CreatedAt < endDate && d.Status == "APPROVED"
                    && !d.Client.IsDeveloperSandbox)
                .GroupBy(d => d.IntegratorId)
                .Select(g => new { IntegratorId = g.Key, Count = g.Count() })
                .ToListAsync();

            var totalDocs = docsByIntegrator.Sum(g => g.Count);
            var amountDue = client.SubscriptionRate;
            foreach (var g in docsByIntegrator)
            {
                // Documentos emitidos antes de la Fase 3a no tienen IntegratorId propio — se
                // asumen del integrador actual del Client, la mejor aproximación disponible.
                var integratorId = g.IntegratorId ?? client.IntegratorId;
                amountDue += await GetClientDocumentChargeAsync(client.Id, g.Count, client.PricePerDocument, integratorId);
            }

            return new ClientBillingMetrics
            {
                ClientId = clientId,
                Year = year,
                Month = month,
                TotalDocuments = totalDocs,
                AmountDueToTenant = amountDue
            };
        }

        // Recorre las bolsas activas del tenant (FIFO por fecha de compra): mientras tengan
        // saldo, esa porción del uso se cobra a su DiscountedPricePerUser (no gratis); lo que
        // sobra después de agotar todas las bolsas se cobra a la tarifa estándar del tenant.
        // Es una función pura para poder reutilizarla tanto en la vista de solo lectura como
        // en el corte mensual, que sí aplica el descuento de saldo con entidades trackeadas.
        public static (decimal TotalDue, List<(Guid BagId, decimal AmountConsumed)> Consumptions) ComputeBagConsumption(
            decimal monthFractionNeeded,
            decimal standardPricePerUser,
            IEnumerable<(Guid Id, decimal RemainingBalance, decimal DiscountedPricePerUser)> bagsFifo)
        {
            var consumptions = new List<(Guid BagId, decimal AmountConsumed)>();
            var remainingFraction = monthFractionNeeded;
            decimal totalDue = 0;

            foreach (var bag in bagsFifo)
            {
                if (remainingFraction <= 0) break;
                if (bag.RemainingBalance <= 0 || bag.DiscountedPricePerUser <= 0) continue;

                var maxFractionThisBagCanCover = bag.RemainingBalance / bag.DiscountedPricePerUser;
                var fractionFromThisBag = Math.Min(remainingFraction, maxFractionThisBagCanCover);
                var amountFromThisBag = Math.Round(fractionFromThisBag * bag.DiscountedPricePerUser, 2);

                totalDue += amountFromThisBag;
                consumptions.Add((bag.Id, amountFromThisBag));
                remainingFraction -= fractionFromThisBag;
            }

            if (remainingFraction > 0)
            {
                totalDue += Math.Round(remainingFraction * standardPricePerUser, 2);
            }

            return (totalDue, consumptions);
        }

        // --- 1b. Per-user (tenant de marca blanca) ---
        // Cobro diario prorrateado por Client activo, en fracciones de "usuario-mes"
        // (1.0 = 1 cliente activo todo el mes). Antes de cobrar a tarifa estándar, ese uso se
        // descuenta primero del saldo de las bolsas prepago activas del tenant (a la tarifa
        // con descuento de cada bolsa) — la bolsa no es por tiempo ni por cliente, es un pool
        // de dinero compartido. Esta función es de solo lectura: no descuenta las bolsas, el
        // corte mensual (SuperadminBillingController.CalculateBilling) es quien aplica el
        // descuento real una sola vez.
        public async Task<TenantBillingMetrics> GetTenantPerUserMetricsAsync(Guid tenantId, int year, int month)
        {
            var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var endDate = startDate.AddMonths(1);
            var daysInMonth = DateTime.DaysInMonth(year, month);

            var pricing = await _dbContext.TenantUserPricings.AsNoTracking().FirstOrDefaultAsync(p => p.TenantId == tenantId);
            var pricePerUser = pricing?.PricePerUser ?? 0m;

            var clients = await _dbContext.Clients.AsNoTracking()
                .Where(c => c.TenantId == tenantId && c.IsActive)
                .ToListAsync();

            // Fracción de usuario-mes que necesita cada cliente este mes, antes de aplicar bolsa.
            var rawFractionByClient = new Dictionary<Guid, decimal>();
            foreach (var client in clients)
            {
                var coverageStart = client.CreatedAt > startDate ? client.CreatedAt.Date : startDate;
                if (coverageStart >= endDate) continue; // se activó después de este mes

                var billableDays = (endDate - coverageStart).Days;
                rawFractionByClient[client.Id] = (decimal)billableDays / daysInMonth;
            }

            var monthFractionNeeded = rawFractionByClient.Values.Sum();

            var bags = await _dbContext.TenantPrepaidBags.AsNoTracking()
                .Where(b => b.TenantId == tenantId && b.Status == PrepaidBagStatus.Active && b.RemainingBalance > 0)
                .OrderBy(b => b.PurchasedAt)
                .Select(b => new { b.Id, b.RemainingBalance, b.DiscountedPricePerUser })
                .ToListAsync();

            var (totalDue, _) = ComputeBagConsumption(
                monthFractionNeeded,
                pricePerUser,
                bags.Select(b => (b.Id, b.RemainingBalance, b.DiscountedPricePerUser)));

            // Reparto proporcional del total (ya con descuento aplicado) entre clientes, solo
            // para el desglose informativo — el cobro real a Superadmin es el TotalDue global.
            var breakdown = new List<ClientUsageBreakdown>();
            foreach (var client in clients)
            {
                if (!rawFractionByClient.TryGetValue(client.Id, out var rawFraction)) continue;

                var share = monthFractionNeeded > 0 ? rawFraction / monthFractionNeeded : 0m;
                var due = Math.Round(totalDue * share, 2);

                breakdown.Add(new ClientUsageBreakdown
                {
                    ClientId = client.Id,
                    ClientName = !string.IsNullOrEmpty(client.CompanyName) ? client.CompanyName : client.CommercialName,
                    DocumentsEmitted = 0,
                    PriceApplied = pricePerUser,
                    AmountDueToTenant = due
                });
            }

            return new TenantBillingMetrics
            {
                TenantId = tenantId,
                Year = year,
                Month = month,
                TotalDocuments = 0,
                TotalUsers = clients.Count,
                MonthFractionNeeded = monthFractionNeeded,
                AmountDueToSuperadmin = totalDue,
                SuperadminTariffApplied = pricePerUser,
                AmountDueFromClients = 0, // La tarifa del tenant hacia sus clientes es independiente (Client.SubscriptionRate)
                ClientBreakdown = breakdown.OrderByDescending(x => x.AmountDueToTenant).ToList()
            };
        }

        // --- 2. Tenant Level Metrics ---
        public async Task<TenantBillingMetrics> GetTenantMetricsAsync(Guid tenantId, int year, int month)
        {
            var tenant = await _dbContext.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId);
            if (tenant?.BillingMode == TenantBillingMode.PerUser)
            {
                return await GetTenantPerUserMetricsAsync(tenantId, year, month);
            }

            var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var endDate = startDate.AddMonths(1);

            // Documentos aprobados del período, con el integrador que realmente los procesó
            // (Document.IntegratorId, o el integrador actual del Client para documentos previos a
            // la Fase 3a que no quedaron marcados).
            var rows = await _dbContext.Documents.AsNoTracking()
                .Include(d => d.Client)
                // Sin los sandbox de developers: son documentos simulados, no se cobran.
                .Where(d => d.Client.TenantId == tenantId && d.CreatedAt >= startDate && d.CreatedAt < endDate && d.Status == "APPROVED"
                    && !d.Client.IsDeveloperSandbox)
                .Select(d => new
                {
                    d.ClientId,
                    ClientName = d.Client.CompanyName != "" ? d.Client.CompanyName : d.Client.CommercialName,
                    d.Client.PricePerDocument,
                    d.Client.SubscriptionRate,
                    ClientDefaultIntegratorId = d.Client.IntegratorId,
                    DocumentIntegratorId = d.IntegratorId
                })
                .ToListAsync();

            var totalDocs = rows.Count;

            // Cuánto le debe el tenant al superadmin: agrupado por integrador, porque cada uno
            // puede tener su propio tier de tarifa por volumen (TariffTier.IntegratorId) — el
            // prorrateo entre integradores sale de sumar cada grupo a su propia tarifa, no de
            // aplicar una sola tarifa al volumen total mezclado.
            decimal amountDueToSuperadmin = 0;
            foreach (var integratorGroup in rows.GroupBy(r => r.DocumentIntegratorId ?? r.ClientDefaultIntegratorId))
            {
                var tariff = await GetSuperadminTariffForVolumeAsync(integratorGroup.Count(), integratorGroup.Key);
                amountDueToSuperadmin += integratorGroup.Count() * tariff;
            }

            // Cuánto le deben los clientes al tenant: documentos del período (a la tarifa de su
            // propio integrador, si el Client tiene un override para ese integrador) + suscripción
            // fija.
            var breakdown = new List<ClientUsageBreakdown>();
            decimal amountDueFromClients = 0;
            var clientIdsWithDocs = new HashSet<Guid>();

            foreach (var clientGroup in rows.GroupBy(r => r.ClientId))
            {
                clientIdsWithDocs.Add(clientGroup.Key);
                var first = clientGroup.First();
                decimal due = first.SubscriptionRate;

                foreach (var integratorGroup in clientGroup.GroupBy(r => r.DocumentIntegratorId ?? r.ClientDefaultIntegratorId))
                {
                    due += await GetClientDocumentChargeAsync(clientGroup.Key, integratorGroup.Count(), first.PricePerDocument, integratorGroup.Key);
                }

                amountDueFromClients += due;

                breakdown.Add(new ClientUsageBreakdown
                {
                    ClientId = clientGroup.Key,
                    ClientName = first.ClientName,
                    DocumentsEmitted = clientGroup.Count(),
                    PriceApplied = first.PricePerDocument,
                    AmountDueToTenant = due
                });
            }

            // Clientes con suscripción fija que no emitieron ningún documento este período: sin
            // esto, se les dejaba de cobrar la suscripción los meses en que no facturaban.
            var subscriptionOnlyClients = await _dbContext.Clients.AsNoTracking()
                .Where(c => c.TenantId == tenantId && c.IsActive && c.SubscriptionRate > 0 && !clientIdsWithDocs.Contains(c.Id))
                .ToListAsync();

            foreach (var c in subscriptionOnlyClients)
            {
                amountDueFromClients += c.SubscriptionRate;
                breakdown.Add(new ClientUsageBreakdown
                {
                    ClientId = c.Id,
                    ClientName = !string.IsNullOrEmpty(c.CompanyName) ? c.CompanyName : c.CommercialName,
                    DocumentsEmitted = 0,
                    PriceApplied = c.PricePerDocument,
                    AmountDueToTenant = c.SubscriptionRate
                });
            }

            return new TenantBillingMetrics
            {
                TenantId = tenantId,
                Year = year,
                Month = month,
                TotalDocuments = totalDocs,
                AmountDueToSuperadmin = amountDueToSuperadmin,
                AmountDueFromClients = amountDueFromClients,
                // Ya no es una tarifa única: cada integrador puede tener la suya. Se deja como
                // promedio ponderado del total, solo informativo.
                SuperadminTariffApplied = totalDocs > 0 ? Math.Round(amountDueToSuperadmin / totalDocs, 2) : 0,
                ClientBreakdown = breakdown.OrderByDescending(x => x.DocumentsEmitted).ToList()
            };
        }

        // --- 3. Superadmin Level Metrics ---
        public async Task<SuperadminBillingMetrics> GetSuperadminMetricsAsync(int year, int month)
        {
            var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var endDate = startDate.AddMonths(1);

            // Los tenants en modo PerUser no se facturan por volumen de documentos aquí
            // (se calculan aparte más abajo), aunque sí puedan tener Documents (vía Dataico).
            var perUserTenantIds = await _dbContext.Tenants.AsNoTracking()
                .Where(t => t.BillingMode == TenantBillingMode.PerUser)
                .Select(t => t.Id)
                .ToListAsync();

            // Documentos del período, con el integrador que los procesó (o el actual del Client
            // para los previos a la Fase 3a).
            var rows = await _dbContext.Documents.AsNoTracking()
                .Include(d => d.Client)
                .ThenInclude(c => c.Tenant)
                // Sin los sandbox de developers: son documentos simulados, no se cobran.
                .Where(d => d.CreatedAt >= startDate && d.CreatedAt < endDate && d.Status == "APPROVED"
                    && !perUserTenantIds.Contains(d.Client.TenantId)
                    && !d.Client.IsDeveloperSandbox)
                .Select(d => new
                {
                    d.Client.TenantId,
                    TenantName = d.Client.Tenant.Name,
                    ClientDefaultIntegratorId = d.Client.IntegratorId,
                    DocumentIntegratorId = d.IntegratorId
                })
                .ToListAsync();

            var totalDocs = rows.Count;
            decimal totalAmountDueFromTenants = 0;
            var breakdown = new List<TenantUsageBreakdown>();

            foreach (var tenantGroup in rows.GroupBy(r => new { r.TenantId, r.TenantName }))
            {
                decimal tenantDue = 0;
                foreach (var integratorGroup in tenantGroup.GroupBy(r => r.DocumentIntegratorId ?? r.ClientDefaultIntegratorId))
                {
                    var tariff = await GetSuperadminTariffForVolumeAsync(integratorGroup.Count(), integratorGroup.Key);
                    tenantDue += integratorGroup.Count() * tariff;
                }
                totalAmountDueFromTenants += tenantDue;

                var docsEmitted = tenantGroup.Count();
                breakdown.Add(new TenantUsageBreakdown
                {
                    TenantId = tenantGroup.Key.TenantId,
                    TenantName = tenantGroup.Key.TenantName,
                    DocumentsEmitted = docsEmitted,
                    // Ya no es una tarifa única cuando el tenant mezcla integradores con tiers
                    // distintos: queda el promedio ponderado, solo informativo.
                    TariffApplied = docsEmitted > 0 ? Math.Round(tenantDue / docsEmitted, 2) : 0,
                    AmountDueToSuperadmin = tenantDue
                });
            }

            foreach (var tenantId in perUserTenantIds)
            {
                var metrics = await GetTenantPerUserMetricsAsync(tenantId, year, month);
                if (metrics.TotalUsers is null or 0) continue;

                var tenant = await _dbContext.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId);
                totalAmountDueFromTenants += metrics.AmountDueToSuperadmin;

                breakdown.Add(new TenantUsageBreakdown
                {
                    TenantId = tenantId,
                    TenantName = tenant?.Name ?? string.Empty,
                    DocumentsEmitted = 0,
                    TariffApplied = metrics.SuperadminTariffApplied,
                    AmountDueToSuperadmin = metrics.AmountDueToSuperadmin
                });
            }

            return new SuperadminBillingMetrics
            {
                Year = year,
                Month = month,
                TotalDocuments = totalDocs,
                TotalAmountDueFromTenants = totalAmountDueFromTenants,
                TenantBreakdown = breakdown.OrderByDescending(x => x.DocumentsEmitted).ToList()
            };
        }
    }

    // --- DTOs ---
    public class ClientBillingMetrics
    {
        public Guid ClientId { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public int TotalDocuments { get; set; }
        public decimal AmountDueToTenant { get; set; }
    }

    public class TenantBillingMetrics
    {
        public Guid TenantId { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public int TotalDocuments { get; set; }
        public int? TotalUsers { get; set; }
        public decimal MonthFractionNeeded { get; set; } // Solo aplica en modo PerUser: fracción de usuario-mes a cobrar (usada por el corte para descontar bolsas)

        public decimal AmountDueToSuperadmin { get; set; }
        public decimal SuperadminTariffApplied { get; set; }

        public decimal AmountDueFromClients { get; set; }
        
        public List<ClientUsageBreakdown> ClientBreakdown { get; set; } = new List<ClientUsageBreakdown>();
    }

    public class ClientUsageBreakdown
    {
        public Guid ClientId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public int DocumentsEmitted { get; set; }
        public decimal PriceApplied { get; set; }
        public decimal AmountDueToTenant { get; set; }
    }

    public class SuperadminBillingMetrics
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int TotalDocuments { get; set; }
        public decimal TotalAmountDueFromTenants { get; set; }
        public List<TenantUsageBreakdown> TenantBreakdown { get; set; } = new List<TenantUsageBreakdown>();
    }

    public class TenantUsageBreakdown
    {
        public Guid TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public int DocumentsEmitted { get; set; }
        public decimal TariffApplied { get; set; }
        public decimal AmountDueToSuperadmin { get; set; }
    }
}
