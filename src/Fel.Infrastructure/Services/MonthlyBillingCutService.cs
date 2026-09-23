using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fel.Infrastructure.Services
{
    public class MonthlyBillingCutResult
    {
        public bool AlreadyExisted { get; set; }
        public int TenantsBilled { get; set; }
    }

    // Corte mensual de facturación Superadmin -> Tenant (TenantBilling). Vive en
    // Fel.Infrastructure, no en el controlador, para que tanto el disparo manual
    // (SuperadminBillingController.CalculateBilling) como el automático
    // (Fel.Worker.BillingCutWorker) corran exactamente la misma lógica sin duplicarla.
    public class MonthlyBillingCutService
    {
        private readonly FelDbContext _dbContext;
        private readonly BillingMetricsService _billingMetrics;
        private readonly ILogger<MonthlyBillingCutService> _logger;

        public MonthlyBillingCutService(FelDbContext dbContext, BillingMetricsService billingMetrics, ILogger<MonthlyBillingCutService> logger)
        {
            _dbContext = dbContext;
            _billingMetrics = billingMetrics;
            _logger = logger;
        }

        public async Task<MonthlyBillingCutResult> RunAsync(int year, int month)
        {
            var existingBilling = await _dbContext.TenantBillings.AnyAsync(b => b.Year == year && b.Month == month);
            if (existingBilling)
            {
                return new MonthlyBillingCutResult { AlreadyExisted = true };
            }

            // El corte siempre corre en el rango [día 1 00:00 UTC del mes, día 1 00:00 UTC del mes
            // siguiente) — sin esto, la hora/zona de quien dispare el corte podía correrlo.
            var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var endDate = startDate.AddMonths(1);

            var perUserTenantIds = await _dbContext.Tenants
                .Where(t => t.BillingMode == TenantBillingMode.PerUser)
                .Select(t => t.Id)
                .ToListAsync();

            // Obtener todos los documentos exitosos del mes (excluyendo tenants PerUser, que se calculan aparte)
            var consumedDocs = await _dbContext.Documents
                .Where(d => d.Status == "APPROVED" && d.ProcessedAt >= startDate && d.ProcessedAt < endDate
                    && !perUserTenantIds.Contains(d.Client.TenantId))
                .Include(d => d.Client)
                .ToListAsync();

            var docsByTenant = consumedDocs.GroupBy(d => d.Client.TenantId);
            var tenantsBilled = 0;

            foreach (var group in docsByTenant)
            {
                var tenantId = group.Key;
                var totalDocs = group.Count();

                // Lo que el Tenant le debe a Superadmin es la tarifa por volumen (TariffTier) de
                // cada integrador usado ese mes, no la suma de PriceCharged — ese campo es la
                // tarifa que el Tenant le cobró a SU cliente (Client.PricePerDocument), una
                // relación de dinero distinta que no tiene por qué coincidir con lo que Superadmin
                // le cobra al Tenant. Se agrupa por integrador porque cada uno puede tener su
                // propio tier de tarifa (TariffTier.IntegratorId).
                decimal totalAmount = 0;
                foreach (var integratorGroup in group.GroupBy(d => d.IntegratorId ?? d.Client.IntegratorId))
                {
                    var tariff = await _billingMetrics.GetSuperadminTariffForVolumeAsync(integratorGroup.Count(), integratorGroup.Key);
                    totalAmount += integratorGroup.Count() * tariff;
                }

                _dbContext.TenantBillings.Add(new TenantBilling
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Month = month,
                    Year = year,
                    TotalDocuments = totalDocs,
                    TotalAmount = totalAmount,
                    Currency = "COP",
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow
                });
                tenantsBilled++;
            }

            foreach (var tenantId in perUserTenantIds)
            {
                var metrics = await _billingMetrics.GetTenantPerUserMetricsAsync(tenantId, year, month);
                if (metrics.TotalUsers is null or 0) continue;

                if (metrics.MonthFractionNeeded > 0)
                {
                    await ConsumeBagBalanceAsync(tenantId, metrics.MonthFractionNeeded, metrics.SuperadminTariffApplied);
                }

                _dbContext.TenantBillings.Add(new TenantBilling
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Month = month,
                    Year = year,
                    TotalDocuments = 0,
                    TotalUsers = metrics.TotalUsers,
                    TotalAmount = metrics.AmountDueToSuperadmin,
                    Currency = "COP",
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow
                });
                tenantsBilled++;
            }

            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Corte mensual {Year}-{Month} generado para {Count} tenants.", year, month, tenantsBilled);

            return new MonthlyBillingCutResult { AlreadyExisted = false, TenantsBilled = tenantsBilled };
        }

        // Descuenta el saldo de las bolsas prepago activas del tenant necesario para cubrir
        // 'monthFractionNeeded' (fracción de usuario-mes), más antigua primero (FIFO), a la
        // tarifa con descuento de cada bolsa; marca cada bolsa como Depleted al llegar a cero.
        private async Task ConsumeBagBalanceAsync(Guid tenantId, decimal monthFractionNeeded, decimal standardPricePerUser)
        {
            var bags = await _dbContext.TenantPrepaidBags
                .Where(b => b.TenantId == tenantId && b.Status == PrepaidBagStatus.Active && b.RemainingBalance > 0)
                .OrderBy(b => b.PurchasedAt)
                .ToListAsync();

            var (_, consumptions) = BillingMetricsService.ComputeBagConsumption(
                monthFractionNeeded,
                standardPricePerUser,
                bags.Select(b => (b.Id, b.RemainingBalance, b.DiscountedPricePerUser)));

            foreach (var (bagId, amountConsumed) in consumptions)
            {
                var bag = bags.First(b => b.Id == bagId);
                bag.RemainingBalance -= amountConsumed;
                if (bag.RemainingBalance <= 0)
                {
                    bag.RemainingBalance = 0;
                    bag.Status = PrepaidBagStatus.Depleted;
                }
            }
        }
    }
}
