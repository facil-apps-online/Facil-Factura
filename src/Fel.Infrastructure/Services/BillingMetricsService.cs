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

        private async Task<decimal> GetSuperadminTariffForVolumeAsync(int volume)
        {
            var tier = await _dbContext.TariffTiers
                .Where(t => t.IsActive && volume >= t.MinDocuments && (t.MaxDocuments == null || volume <= t.MaxDocuments))
                .FirstOrDefaultAsync();

            return tier?.PricePerDocument ?? 70m; // Default to highest if not found
        }

        // --- 1. Client Level Metrics ---
        public async Task<ClientBillingMetrics> GetClientMetricsAsync(Guid clientId, int year, int month)
        {
            var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var endDate = startDate.AddMonths(1);

            var client = await _dbContext.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null) throw new Exception("Client not found");

            var totalDocs = await _dbContext.Documents.AsNoTracking()
                .Where(d => d.ClientId == clientId && d.CreatedAt >= startDate && d.CreatedAt < endDate && d.Status == "APPROVED")
                .CountAsync();

            var amountDue = totalDocs * client.PricePerDocument;

            return new ClientBillingMetrics
            {
                ClientId = clientId,
                Year = year,
                Month = month,
                TotalDocuments = totalDocs,
                AmountDueToTenant = amountDue
            };
        }

        // --- 2. Tenant Level Metrics ---
        public async Task<TenantBillingMetrics> GetTenantMetricsAsync(Guid tenantId, int year, int month)
        {
            var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var endDate = startDate.AddMonths(1);

            // Obtenemos los documentos aprobados agrupados por cliente
            var clientDocsQuery = await _dbContext.Documents.AsNoTracking()
                .Include(d => d.Client)
                .Where(d => d.Client.TenantId == tenantId && d.CreatedAt >= startDate && d.CreatedAt < endDate && d.Status == "APPROVED")
                .GroupBy(d => new { d.ClientId, d.Client.CompanyName, d.Client.CommercialName, d.Client.PricePerDocument })
                .Select(g => new
                {
                    ClientId = g.Key.ClientId,
                    ClientName = !string.IsNullOrEmpty(g.Key.CompanyName) ? g.Key.CompanyName : g.Key.CommercialName,
                    PricePerDocument = g.Key.PricePerDocument,
                    DocumentsEmitted = g.Count()
                })
                .ToListAsync();

            var totalDocs = clientDocsQuery.Sum(x => x.DocumentsEmitted);
            
            // Cuánto le debe el tenant al superadmin
            var superadminTariff = await GetSuperadminTariffForVolumeAsync(totalDocs);
            var amountDueToSuperadmin = totalDocs * superadminTariff;

            // Cuánto le deben los clientes al tenant
            var breakdown = new List<ClientUsageBreakdown>();
            decimal amountDueFromClients = 0;

            foreach (var c in clientDocsQuery)
            {
                var due = c.DocumentsEmitted * c.PricePerDocument;
                amountDueFromClients += due;
                
                breakdown.Add(new ClientUsageBreakdown
                {
                    ClientId = c.ClientId,
                    ClientName = c.ClientName,
                    DocumentsEmitted = c.DocumentsEmitted,
                    PriceApplied = c.PricePerDocument,
                    AmountDueToTenant = due
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
                SuperadminTariffApplied = superadminTariff,
                ClientBreakdown = breakdown.OrderByDescending(x => x.DocumentsEmitted).ToList()
            };
        }

        // --- 3. Superadmin Level Metrics ---
        public async Task<SuperadminBillingMetrics> GetSuperadminMetricsAsync(int year, int month)
        {
            var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var endDate = startDate.AddMonths(1);

            // Obtenemos los documentos agrupados por Tenant
            var tenantDocsQuery = await _dbContext.Documents.AsNoTracking()
                .Include(d => d.Client)
                .ThenInclude(c => c.Tenant)
                .Where(d => d.CreatedAt >= startDate && d.CreatedAt < endDate && d.Status == "APPROVED")
                .GroupBy(d => new { d.Client.TenantId, d.Client.Tenant.Name })
                .Select(g => new
                {
                    TenantId = g.Key.TenantId,
                    TenantName = g.Key.Name,
                    DocumentsEmitted = g.Count()
                })
                .ToListAsync();

            var totalDocs = tenantDocsQuery.Sum(x => x.DocumentsEmitted);
            decimal totalAmountDueFromTenants = 0;
            var breakdown = new List<TenantUsageBreakdown>();

            foreach (var t in tenantDocsQuery)
            {
                var tariff = await GetSuperadminTariffForVolumeAsync(t.DocumentsEmitted);
                var due = t.DocumentsEmitted * tariff;
                totalAmountDueFromTenants += due;

                breakdown.Add(new TenantUsageBreakdown
                {
                    TenantId = t.TenantId,
                    TenantName = t.TenantName,
                    DocumentsEmitted = t.DocumentsEmitted,
                    TariffApplied = tariff,
                    AmountDueToSuperadmin = due
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
