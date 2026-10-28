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

        // Resultado de cubrir con bolsas los documentos de un Client: lo que se cobra en total y la parte que corresponde a cada
        // documento (en el mismo orden en que se recibieron las tarifas).
        public sealed record DocumentChargeResult(decimal Total, IReadOnlyList<decimal> PerDocument);

        // Cobro de los documentos de un Client entre sus bolsas prepago (FIFO) y la tarifa estándar de cada documento. Los documentos
        // vienen ordenados por fecha y cada uno trae SU tarifa (la de su sucursal): la bolsa cubre primero los más antiguos, sea cual sea
        // la sucursal, y lo que no cubre se cobra a la tarifa de la sucursal que lo emitió. Con una sola tarifa da exactamente el mismo
        // total que ComputeBagConsumption.
        public static DocumentChargeResult ComputeBagConsumptionPerDocument(
            IReadOnlyList<decimal> standardRates,
            IEnumerable<(Guid Id, decimal RemainingBalance, decimal DiscountedPricePerDocument)> bagsFifo)
        {
            var count = standardRates.Count;

            // 1) Lo que cubren las bolsas, bolsa por bolsa (igual que ComputeBagConsumption): documentos cubiertos y su costo.
            decimal remainingFraction = count;
            decimal bagsTotal = 0;
            var segments = new List<(decimal Fraction, decimal Price)>();
            foreach (var bag in bagsFifo)
            {
                if (remainingFraction <= 0) break;
                if (bag.RemainingBalance <= 0 || bag.DiscountedPricePerDocument <= 0) continue;

                var fraction = Math.Min(remainingFraction, bag.RemainingBalance / bag.DiscountedPricePerDocument);
                bagsTotal += Math.Round(fraction * bag.DiscountedPricePerDocument, 2);
                segments.Add((fraction, bag.DiscountedPricePerDocument));
                remainingFraction -= fraction;
            }

            // 2) Qué parte de cada documento cubre la bolsa y a qué tarifa se cobra el resto.
            var perDocument = new decimal[count];
            decimal uncoveredAtRates = 0;
            var segment = 0;
            var segmentLeft = segments.Count > 0 ? segments[0].Fraction : 0m;
            for (var i = 0; i < count; i++)
            {
                decimal docLeft = 1m;
                decimal cost = 0;
                while (docLeft > 0 && segment < segments.Count)
                {
                    var take = Math.Min(docLeft, segmentLeft);
                    cost += take * segments[segment].Price;
                    docLeft -= take;
                    segmentLeft -= take;
                    if (segmentLeft <= 0)
                    {
                        segment++;
                        segmentLeft = segment < segments.Count ? segments[segment].Fraction : 0m;
                    }
                }
                cost += docLeft * standardRates[i];
                uncoveredAtRates += docLeft * standardRates[i];
                perDocument[i] = cost;
            }

            return new DocumentChargeResult(bagsTotal + Math.Round(uncoveredAtRates, 2), perDocument);
        }

        // Reparte un total ya redondeado entre sucursales según lo que costó cada documento: cada una redondea a centavos y la
        // diferencia de redondeo se la lleva la que más pesa, así la suma siempre es exactamente el total.
        private static Dictionary<Guid, decimal> AllocateByBranch(decimal total, IReadOnlyList<decimal> perDocument, IReadOnlyList<Guid> branchOfDocument)
        {
            var raw = new Dictionary<Guid, decimal>();
            for (var i = 0; i < perDocument.Count; i++)
                raw[branchOfDocument[i]] = raw.GetValueOrDefault(branchOfDocument[i]) + perDocument[i];
            if (raw.Count == 0) return new Dictionary<Guid, decimal>();

            var rounded = raw.ToDictionary(kv => kv.Key, kv => Math.Round(kv.Value, 2));
            var heaviest = raw.OrderByDescending(kv => kv.Value).First().Key;
            rounded[heaviest] += total - rounded.Values.Sum();
            return rounded;
        }

        // Cobro por documentos de UN Client este período: agrupa por integrador (cada uno con sus bolsas y su tarifa) y devuelve lo que
        // le corresponde a cada sucursal. La tarifa estándar de un documento es la de su sucursal, o el override de esa sucursal para ese
        // integrador si existe y está en modo PerDocument. Solo lectura: no descuenta las bolsas, eso lo hace el corte real.
        private async Task<Dictionary<Guid, decimal>> ComputeClientDocumentChargesAsync(
            Guid clientId,
            IReadOnlyList<(Guid BranchId, Guid IntegratorId, DateTime EmittedAt)> documents,
            IReadOnlyDictionary<Guid, decimal> branchPrices)
        {
            var charges = new Dictionary<Guid, decimal>();
            if (documents.Count == 0) return charges;

            var overrides = await _dbContext.ClientIntegratorBillings.AsNoTracking()
                .Where(b => b.ClientId == clientId && b.Mode == TenantBillingMode.PerDocument)
                .ToListAsync();

            foreach (var group in documents.GroupBy(d => d.IntegratorId))
            {
                var ordered = group.OrderBy(d => d.EmittedAt).ToList();
                var rates = ordered
                    .Select(d => overrides.FirstOrDefault(o => o.BranchId == d.BranchId && o.IntegratorId == group.Key)?.PricePerDocument
                                 ?? branchPrices.GetValueOrDefault(d.BranchId))
                    .ToList();

                var bags = await _dbContext.ClientPrepaidBags.AsNoTracking()
                    .Where(b => b.ClientId == clientId && b.IntegratorId == group.Key && b.Status == PrepaidBagStatus.Active && b.RemainingBalance > 0)
                    .OrderBy(b => b.PurchasedAt)
                    .Select(b => new { b.Id, b.RemainingBalance, b.DiscountedPricePerDocument })
                    .ToListAsync();

                var result = ComputeBagConsumptionPerDocument(rates, bags.Select(b => (b.Id, b.RemainingBalance, b.DiscountedPricePerDocument)));
                foreach (var (branchId, amount) in AllocateByBranch(result.Total, result.PerDocument, ordered.Select(d => d.BranchId).ToList()))
                    charges[branchId] = charges.GetValueOrDefault(branchId) + amount;
            }

            return charges;
        }

        // --- 1. Client Level Metrics ---
        public async Task<ClientBillingMetrics> GetClientMetricsAsync(Guid clientId, int year, int month)
        {
            // Los documentos se miden por su fecha de emisión en el mes de Colombia, sin los enviados al sandbox (ver DocumentCounting).
            var (windowStart, windowEnd) = DocumentCounting.MonthWindowUtc(year, month);

            var client = await _dbContext.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null) throw new Exception("Client not found");

            var branches = await _dbContext.Branches.AsNoTracking().Where(b => b.ClientId == clientId).ToListAsync();

            // Los documentos de Clients sandbox quedan en "APPROVED" pero nunca se transmitieron a
            // la DIAN: son respuestas simuladas para developers (ver SandboxSimulation), así que no
            // se cobran. Se guardan igual para que el developer los vea en su portal.
            var docs = await _dbContext.Documents.AsNoTracking()
                .Where(d => d.ClientId == clientId && d.Status == "APPROVED")
                .Counted().EmittedBetween(windowStart, windowEnd)
                .Select(d => new { d.BranchId, d.IntegratorId, EmittedAt = d.ProcessedAt ?? d.CreatedAt })
                .ToListAsync();

            // Documentos emitidos antes de la Fase 3a no tienen IntegratorId propio — se asumen del integrador
            // actual del Client, la mejor aproximación disponible.
            var charges = await ComputeClientDocumentChargesAsync(
                clientId,
                docs.Select(d => (d.BranchId, d.IntegratorId ?? client.IntegratorId, d.EmittedAt)).ToList(),
                branches.ToDictionary(b => b.Id, b => b.PricePerDocument));

            // La cuota fija mensual la paga cada sucursal activa.
            var amountDue = charges.Values.Sum() + branches.Where(b => b.IsActive).Sum(b => b.SubscriptionRate);

            return new ClientBillingMetrics
            {
                ClientId = clientId,
                Year = year,
                Month = month,
                TotalDocuments = docs.Count,
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

        // Días del mes en que una sucursal cuenta para el cobro por usuario: [from, to). Una inactiva sin fecha de desactivación no cuenta;
        // con fecha, cuenta hasta ella. Falso si se activó después del mes o se desactivó antes.
        // billingStartsAt: "Cobro desde" del tenant; los días anteriores se cuentan pero no se cobran.
        private static bool TryGetPerUserCoverage(Branch branch, DateTime startDate, DateTime endDate, DateTime? billingStartsAt, out DateTime from, out DateTime to)
        {
            from = default;
            to = default;
            if (!branch.IsActive && branch.DeactivatedAt == null) return false;

            from = branch.CreatedAt > startDate ? branch.CreatedAt.Date : startDate;
            if (billingStartsAt.HasValue && billingStartsAt.Value.Date > from) from = billingStartsAt.Value.Date;
            to = branch.DeactivatedAt.HasValue && branch.DeactivatedAt.Value.Date < endDate ? branch.DeactivatedAt.Value.Date : endDate;
            return from < endDate && to > from;
        }

        // --- 1b. Per-user (tenant de marca blanca) ---
        // Cobro diario prorrateado por SUCURSAL activa, en fracciones de "usuario-mes" (1.0 = 1 sucursal activa todo el mes). Cada sucursal
        // cuenta desde su fecha de creación y, si se desactivó, hasta su fecha de desactivación (si se reactiva vuelve a contar completa).
        // La principal hereda la fecha de creación del Client, así que un Client con una sola sucursal da lo mismo que antes. Antes de cobrar
        // a tarifa estándar, ese uso se descuenta primero del saldo de las bolsas prepago activas del tenant (a la tarifa con descuento
        // de cada bolsa) — la bolsa no es por tiempo ni por cliente, es un pool de dinero compartido. Esta función es de solo lectura: no
        // descuenta las bolsas, el corte mensual (SuperadminBillingController.CalculateBilling) es quien aplica el descuento real una sola vez.
        public async Task<TenantBillingMetrics> GetTenantPerUserMetricsAsync(Guid tenantId, int year, int month)
        {
            var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var endDate = startDate.AddMonths(1);
            var daysInMonth = DateTime.DaysInMonth(year, month);

            var pricing = await _dbContext.TenantUserPricings.AsNoTracking().FirstOrDefaultAsync(p => p.TenantId == tenantId);
            var pricePerUser = pricing?.PricePerUser ?? 0m;
            var billingTerms = await _dbContext.Tenants.AsNoTracking().Where(t => t.Id == tenantId).Select(t => new { t.BillingStartsAt, t.IsBillingExempt }).FirstOrDefaultAsync();

            var branches = await _dbContext.Branches.AsNoTracking()
                .Include(b => b.Client)
                .Where(b => b.Client.TenantId == tenantId && b.Client.IsActive)
                .ToListAsync();

            var branchCountByClient = branches.GroupBy(b => b.ClientId).ToDictionary(g => g.Key, g => g.Count());

            // Fracción de usuario-mes que necesita cada sucursal este mes, antes de aplicar bolsa.
            var rawFractionByBranch = new Dictionary<Guid, decimal>();
            foreach (var branch in branches)
            {
                if (!TryGetPerUserCoverage(branch, startDate, endDate, billingTerms?.BillingStartsAt, out var coverageStart, out var coverageEnd)) continue;

                rawFractionByBranch[branch.Id] = (decimal)(coverageEnd - coverageStart).Days / daysInMonth;
            }

            // Un tenant exento no paga: se cuentan sus sucursales, pero el valor a cobrar y la fracción para descontar bolsas quedan en cero.
            var exempt = billingTerms?.IsBillingExempt == true;
            var fractionForBreakdown = rawFractionByBranch.Values.Sum();
            var monthFractionNeeded = exempt ? 0m : fractionForBreakdown;

            var bags = await _dbContext.TenantPrepaidBags.AsNoTracking()
                .Where(b => b.TenantId == tenantId && b.Status == PrepaidBagStatus.Active && b.RemainingBalance > 0)
                .OrderBy(b => b.PurchasedAt)
                .Select(b => new { b.Id, b.RemainingBalance, b.DiscountedPricePerUser })
                .ToListAsync();

            var (totalDue, _) = ComputeBagConsumption(
                monthFractionNeeded,
                pricePerUser,
                bags.Select(b => (b.Id, b.RemainingBalance, b.DiscountedPricePerUser)));

            // Reparto proporcional del total (ya con descuento aplicado) entre sucursales, solo
            // para el desglose informativo — el cobro real a Superadmin es el TotalDue global.
            var breakdown = new List<ClientUsageBreakdown>();
            foreach (var branch in branches)
            {
                if (!rawFractionByBranch.TryGetValue(branch.Id, out var rawFraction)) continue;

                var share = monthFractionNeeded > 0 ? rawFraction / fractionForBreakdown : 0m;
                var due = Math.Round(totalDue * share, 2);

                breakdown.Add(new ClientUsageBreakdown
                {
                    ClientId = branch.ClientId,
                    ClientName = BreakdownName(branch.Client, branch, branchCountByClient[branch.ClientId] > 1),
                    BranchId = branch.Id,
                    BranchName = branch.Name,
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
                TotalUsers = rawFractionByBranch.Count,
                MonthFractionNeeded = monthFractionNeeded,
                AmountDueToSuperadmin = totalDue,
                SuperadminTariffApplied = pricePerUser,
                AmountDueFromClients = 0, // La tarifa del tenant hacia sus clientes es independiente (Branch.SubscriptionRate)
                ClientBreakdown = breakdown.OrderByDescending(x => x.AmountDueToTenant).ToList()
            };
        }

        // Nombre con el que se muestra una fila del desglose: el del cliente, y la sucursal además cuando el cliente tiene más de una.
        private static string BreakdownName(Client client, Branch branch, bool clientHasSeveralBranches)
        {
            var clientName = !string.IsNullOrEmpty(client.CompanyName) ? client.CompanyName : client.CommercialName;
            return clientHasSeveralBranches ? $"{clientName} — {branch.Name}" : clientName;
        }

        // --- 2. Tenant Level Metrics ---
        public async Task<TenantBillingMetrics> GetTenantMetricsAsync(Guid tenantId, int year, int month)
        {
            var tenant = await _dbContext.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId);
            if (tenant?.BillingMode == TenantBillingMode.PerUser)
            {
                return await GetTenantPerUserMetricsAsync(tenantId, year, month);
            }

            var (windowStart, windowEnd) = DocumentCounting.MonthWindowUtc(year, month);

            // Documentos aprobados del período, con el integrador que realmente los procesó
            // (Document.IntegratorId, o el integrador actual del Client para documentos previos a
            // la Fase 3a que no quedaron marcados) y la sucursal que los emitió.
            var rows = await _dbContext.Documents.AsNoTracking()
                // Sin lo enviado al sandbox ni los sandbox de developers: no se cuenta ni se cobra. Fecha de emisión, en el mes de Colombia.
                .Where(d => d.Client.TenantId == tenantId && d.Status == "APPROVED")
                .Counted().EmittedBetween(windowStart, windowEnd)
                .Select(d => new
                {
                    d.ClientId,
                    d.BranchId,
                    ClientDefaultIntegratorId = d.Client.IntegratorId,
                    DocumentIntegratorId = d.IntegratorId,
                    EmittedAt = d.ProcessedAt ?? d.CreatedAt
                })
                .ToListAsync();

            var totalDocs = rows.Count;
            // Lo que se le cobra al tenant: solo lo emitido desde su "Cobro desde" y nada si está exento; el volumen de su tarifa cuenta desde esa fecha.
            var billableRows = rows.Where(r => DocumentCounting.IsBillable(r.EmittedAt, tenant?.BillingStartsAt, tenant?.IsBillingExempt ?? false)).ToList();

            // Cuánto le debe el tenant al superadmin: agrupado por integrador, porque cada uno
            // puede tener su propio tier de tarifa por volumen (TariffTier.IntegratorId) — el
            // prorrateo entre integradores sale de sumar cada grupo a su propia tarifa, no de
            // aplicar una sola tarifa al volumen total mezclado.
            decimal amountDueToSuperadmin = 0;
            foreach (var integratorGroup in billableRows.GroupBy(r => r.DocumentIntegratorId ?? r.ClientDefaultIntegratorId))
            {
                var tariff = await GetSuperadminTariffForVolumeAsync(integratorGroup.Count(), integratorGroup.Key);
                amountDueToSuperadmin += integratorGroup.Count() * tariff;
            }

            // Sucursales que entran al cálculo: las de los Clients con documentos este período (aunque ya no estén activas) y las activas
            // con cuota fija que no emitieron nada (sin esto, se les dejaba de cobrar la cuota los meses en que no facturaban).
            var clientIdsWithDocs = rows.Select(r => r.ClientId).Distinct().ToList();
            var branches = await _dbContext.Branches.AsNoTracking()
                .Include(b => b.Client)
                .Where(b => b.Client.TenantId == tenantId && !b.Client.IsDeveloperSandbox
                    && (clientIdsWithDocs.Contains(b.ClientId) || (b.Client.IsActive && b.IsActive && b.SubscriptionRate > 0)))
                .ToListAsync();

            var branchCountByClient = await _dbContext.Branches.AsNoTracking()
                .Where(b => b.Client.TenantId == tenantId)
                .GroupBy(b => b.ClientId)
                .Select(g => new { ClientId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ClientId, x => x.Count);

            // Cuánto le deben los clientes al tenant: documentos del período (a la tarifa de su sucursal o su override por integrador, con
            // las bolsas del cliente por delante) + la cuota fija de cada sucursal activa.
            var breakdown = new List<ClientUsageBreakdown>();
            decimal amountDueFromClients = 0;

            foreach (var clientBranches in branches.GroupBy(b => b.ClientId))
            {
                var clientRows = rows.Where(r => r.ClientId == clientBranches.Key).ToList();

                var charges = await ComputeClientDocumentChargesAsync(
                    clientBranches.Key,
                    clientRows.Select(r => (r.BranchId, r.DocumentIntegratorId ?? r.ClientDefaultIntegratorId, r.EmittedAt)).ToList(),
                    clientBranches.ToDictionary(b => b.Id, b => b.PricePerDocument));

                foreach (var branch in clientBranches)
                {
                    var documents = clientRows.Count(r => r.BranchId == branch.Id);
                    var due = charges.GetValueOrDefault(branch.Id) + (branch.IsActive ? branch.SubscriptionRate : 0m);
                    if (documents == 0 && due == 0) continue;

                    amountDueFromClients += due;
                    breakdown.Add(new ClientUsageBreakdown
                    {
                        ClientId = branch.ClientId,
                        ClientName = BreakdownName(branch.Client, branch, branchCountByClient.GetValueOrDefault(branch.ClientId) > 1),
                        BranchId = branch.Id,
                        BranchName = branch.Name,
                        DocumentsEmitted = documents,
                        PriceApplied = branch.PricePerDocument,
                        AmountDueToTenant = due
                    });
                }
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
                SuperadminTariffApplied = billableRows.Count > 0 ? Math.Round(amountDueToSuperadmin / billableRows.Count, 2) : 0,
                ClientBreakdown = breakdown.OrderByDescending(x => x.DocumentsEmitted).ToList()
            };
        }

        // --- 3. Superadmin Level Metrics ---
        public async Task<SuperadminBillingMetrics> GetSuperadminMetricsAsync(int year, int month)
        {
            var (windowStart, windowEnd) = DocumentCounting.MonthWindowUtc(year, month);

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
                // Sin lo enviado al sandbox ni los sandbox de developers: no se cuenta ni se cobra. Fecha de emisión, en el mes de Colombia.
                .Where(d => d.Status == "APPROVED" && !perUserTenantIds.Contains(d.Client.TenantId))
                .Counted().EmittedBetween(windowStart, windowEnd)
                .Select(d => new
                {
                    d.Client.TenantId,
                    TenantName = d.Client.Tenant.Name,
                    d.Client.Tenant.BillingStartsAt,
                    d.Client.Tenant.IsBillingExempt,
                    ClientDefaultIntegratorId = d.Client.IntegratorId,
                    DocumentIntegratorId = d.IntegratorId,
                    EmittedAt = d.ProcessedAt ?? d.CreatedAt
                })
                .ToListAsync();

            var totalDocs = rows.Count;
            decimal totalAmountDueFromTenants = 0;
            var breakdown = new List<TenantUsageBreakdown>();

            foreach (var tenantGroup in rows.GroupBy(r => new { r.TenantId, r.TenantName }))
            {
                decimal tenantDue = 0;
                // Cuentan todos los emitidos; se cobra solo lo emitido desde el "Cobro desde" del tenant y nada si está exento.
                var billable = tenantGroup.Where(r => DocumentCounting.IsBillable(r.EmittedAt, r.BillingStartsAt, r.IsBillingExempt));
                foreach (var integratorGroup in billable.GroupBy(r => r.DocumentIntegratorId ?? r.ClientDefaultIntegratorId))
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

        // --- 4. Superadmin: valor causado por día ---
        // Por documento: cada tenant e integrador paga todo el mes a la tarifa de su volumen del mes (la misma de GetSuperadminMetricsAsync),
        // así que cada documento aporta esa tarifa en su día y el último día coincide con el total del mes. Por usuario: el total del mes
        // (ya con bolsas) se reparte según los días-sucursal que cuentan cada día, es decir, crece cuando se activa una sucursal.
        public async Task<SuperadminDailyAccrual> GetSuperadminDailyAccrualAsync(int year, int month)
        {
            var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var endDate = startDate.AddMonths(1);
            var (windowStart, windowEnd) = DocumentCounting.MonthWindowUtc(year, month);
            var daysInMonth = DateTime.DaysInMonth(year, month);

            var perUserTenantIds = await _dbContext.Tenants.AsNoTracking()
                .Where(t => t.BillingMode == TenantBillingMode.PerUser)
                .Select(t => t.Id)
                .ToListAsync();

            var documentBased = new decimal[daysInMonth];
            var rows = await _dbContext.Documents.AsNoTracking()
                .Where(d => d.Status == "APPROVED" && !perUserTenantIds.Contains(d.Client.TenantId))
                .Counted().EmittedBetween(windowStart, windowEnd)
                .Select(d => new { d.Client.TenantId, d.Client.Tenant.BillingStartsAt, d.Client.Tenant.IsBillingExempt, ClientDefaultIntegratorId = d.Client.IntegratorId, DocumentIntegratorId = d.IntegratorId, EmittedAt = d.ProcessedAt ?? d.CreatedAt })
                .ToListAsync();

            // Solo lo que se cobra (desde el "Cobro desde" del tenant y sin los exentos); cada día es el de Colombia.
            foreach (var group in rows.Where(r => DocumentCounting.IsBillable(r.EmittedAt, r.BillingStartsAt, r.IsBillingExempt))
                         .GroupBy(r => new { r.TenantId, IntegratorId = r.DocumentIntegratorId ?? r.ClientDefaultIntegratorId }))
            {
                var tariff = await GetSuperadminTariffForVolumeAsync(group.Count(), group.Key.IntegratorId);
                foreach (var day in group.GroupBy(r => DocumentCounting.ToColombia(r.EmittedAt).Day))
                    documentBased[day.Key - 1] += day.Count() * tariff;
            }

            var perUser = new decimal[daysInMonth];
            if (perUserTenantIds.Count > 0)
            {
                var branches = await _dbContext.Branches.AsNoTracking()
                    .Where(b => perUserTenantIds.Contains(b.Client.TenantId) && b.Client.IsActive)
                    .Select(b => new { b.Client.TenantId, b.Client.Tenant.BillingStartsAt, b.Client.Tenant.IsBillingExempt, Branch = b })
                    .ToListAsync();

                foreach (var tenantBranches in branches.Where(b => !b.IsBillingExempt).GroupBy(b => b.TenantId))
                {
                    var weight = new decimal[daysInMonth];
                    foreach (var item in tenantBranches)
                    {
                        if (!TryGetPerUserCoverage(item.Branch, startDate, endDate, item.BillingStartsAt, out var coverFrom, out var coverTo)) continue;
                        for (var day = coverFrom.Day; day < coverFrom.Day + (coverTo - coverFrom).Days; day++)
                            weight[day - 1] += 1m;
                    }

                    var totalWeight = weight.Sum();
                    if (totalWeight == 0) continue;

                    var total = (await GetTenantPerUserMetricsAsync(tenantBranches.Key, year, month)).AmountDueToSuperadmin;
                    for (var i = 0; i < daysInMonth; i++)
                        perUser[i] += total * weight[i] / totalWeight;
                }
            }

            return new SuperadminDailyAccrual(documentBased, perUser);
        }
    }

    // Valor que el superadmin le cobra a los tenants repartido por día de un mes (índice 0 = día 1). Los dos arreglos suman el mismo total
    // que GetSuperadminMetricsAsync del mismo mes.
    public sealed record SuperadminDailyAccrual(decimal[] DocumentBased, decimal[] PerUser);

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
        // El tenant es un aliado al que su padre no le muestra lo que debe a la plataforma: los dos valores de arriba van en cero.
        public bool PlatformBillingHidden { get; set; }

        public decimal AmountDueFromClients { get; set; }
        
        public List<ClientUsageBreakdown> ClientBreakdown { get; set; } = new List<ClientUsageBreakdown>();
    }

    public class ClientUsageBreakdown
    {
        public Guid ClientId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public Guid? BranchId { get; set; }
        public string? BranchName { get; set; }
        public int DocumentsEmitted { get; set; }
        // Documentos rechazados por la DIAN en el período. No se cobran: se agregan al tablero del tenant aparte, junto a los emitidos.
        public int RejectedDocuments { get; set; }
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
