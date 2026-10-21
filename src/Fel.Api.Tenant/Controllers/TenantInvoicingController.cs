using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Models;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Tenant.Controllers
{
    // Facturación automática de los planes (opcional): empresa emisora del tenant, productos de las líneas, día del ciclo en que se generan los borradores
    // y el listado de cobros generados. Los borradores se revisan y se emiten desde el portal de la empresa emisora, con el botón o por lotes.
    [ApiController]
    [Route("api/tenant/invoicing")]
    public class TenantInvoicingController : ControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly PlanInvoicingService _invoicing;

        public TenantInvoicingController(FelDbContext dbContext, PlanInvoicingService invoicing)
        {
            _dbContext = dbContext;
            _invoicing = invoicing;
        }

        private Guid GetCurrentTenantId()
        {
            if (Request.Headers.TryGetValue("x-tenant-id", out var tenantIdStr) && Guid.TryParse(tenantIdStr, out var tenantId))
                return tenantId;
            throw new UnauthorizedAccessException("x-tenant-id Header is missing");
        }

        public class SettingsRequest
        {
            public Guid? OwnerClientId { get; set; }
            public bool GenerateDrafts { get; set; }
            public int DraftDay { get; set; } = 1;
            public Guid? FeeProductId { get; set; }
            public Guid? ExtraProductId { get; set; }
        }

        private async Task<object> BuildSettingsAsync(Guid tenantId, TenantInvoicingSettings? row)
        {
            var s = row ?? new TenantInvoicingSettings { TenantId = tenantId };
            var ownerName = s.OwnerClientId == null ? null
                : await _dbContext.Clients.AsNoTracking().Where(c => c.Id == s.OwnerClientId).Select(c => !string.IsNullOrEmpty(c.CommercialName) ? c.CommercialName : c.CompanyName).FirstOrDefaultAsync();
            return new { s.OwnerClientId, ownerClientName = ownerName, s.GenerateDrafts, s.DraftDay, s.FeeProductId, s.ExtraProductId };
        }

        [HttpGet("settings")]
        public async Task<IActionResult> GetSettings()
        {
            var tenantId = GetCurrentTenantId();
            var row = await _dbContext.TenantInvoicingSettings.AsNoTracking().FirstOrDefaultAsync(s => s.TenantId == tenantId);
            return Ok(await BuildSettingsAsync(tenantId, row));
        }

        [HttpPut("settings")]
        public async Task<IActionResult> PutSettings([FromBody] SettingsRequest request)
        {
            var tenantId = GetCurrentTenantId();
            if (request.DraftDay is < 1 or > 28) return BadRequest("El día del ciclo debe estar entre 1 y 28.");

            if (request.OwnerClientId != null && !await _dbContext.Clients.AnyAsync(c => c.Id == request.OwnerClientId && c.TenantId == tenantId && c.IsActive))
                return BadRequest("La empresa emisora debe ser un cliente activo de este tenant.");

            foreach (var productId in new[] { request.FeeProductId, request.ExtraProductId }.Where(p => p != null))
            {
                if (request.OwnerClientId == null || !await _dbContext.Products.AnyAsync(p => p.Id == productId && p.ClientId == request.OwnerClientId))
                    return BadRequest("Los productos deben ser del catálogo de la empresa emisora.");
            }
            if (request.GenerateDrafts && (request.OwnerClientId == null || request.FeeProductId == null))
                return BadRequest("Para generar borradores elige la empresa emisora y el producto de la cuota.");

            var row = await _dbContext.TenantInvoicingSettings.FirstOrDefaultAsync(s => s.TenantId == tenantId);
            if (row == null)
            {
                row = new TenantInvoicingSettings { TenantId = tenantId };
                _dbContext.TenantInvoicingSettings.Add(row);
            }
            // Cambiar de empresa emisora invalida los productos elegidos (son de su catálogo).
            if (row.OwnerClientId != request.OwnerClientId && request.OwnerClientId != null && row.OwnerClientId != null)
            {
                if (request.FeeProductId == row.FeeProductId) request.FeeProductId = null;
                if (request.ExtraProductId == row.ExtraProductId) request.ExtraProductId = null;
                if (request.GenerateDrafts && request.FeeProductId == null) return BadRequest("Al cambiar de empresa emisora elige de nuevo el producto de la cuota.");
            }
            row.OwnerClientId = request.OwnerClientId;
            row.GenerateDrafts = request.GenerateDrafts;
            row.DraftDay = request.DraftDay;
            row.FeeProductId = request.FeeProductId;
            row.ExtraProductId = request.ExtraProductId;
            row.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            return Ok(await BuildSettingsAsync(tenantId, row));
        }

        // Productos del catálogo de la empresa emisora, para elegir los de cada línea de cobro.
        [HttpGet("products")]
        public async Task<IActionResult> GetProducts()
        {
            var tenantId = GetCurrentTenantId();
            var ownerId = await _dbContext.TenantInvoicingSettings.AsNoTracking().Where(s => s.TenantId == tenantId).Select(s => s.OwnerClientId).FirstOrDefaultAsync();
            if (ownerId == null) return Ok(new List<object>());
            var products = await _dbContext.Products.AsNoTracking()
                .Where(p => p.ClientId == ownerId && p.Scope == ProductScope.Invoice)
                .OrderBy(p => p.Name)
                .Select(p => new { p.Id, p.Code, p.Name, p.IvaTreatment, p.IvaRate, p.PriceIncludesIva })
                .ToListAsync();
            return Ok(products);
        }

        // Genera ahora los borradores del ciclo en curso, sin esperar al día configurado ni exigir el interruptor. No repite los ya generados.
        [HttpPost("generate")]
        public async Task<IActionResult> Generate()
        {
            var tenantId = GetCurrentTenantId();
            var result = await _invoicing.GenerateForTenantAsync(tenantId, ColombiaTime.Now.Date, automatic: false);
            return Ok(new { result.Created, result.Skipped, result.Messages });
        }

        // Cobros generados, por rango de meses (del ciclo en que se generaron). El estado sale del borrador de la empresa emisora.
        [HttpGet("charges")]
        public async Task<IActionResult> GetCharges([FromQuery] string? from, [FromQuery] string? to, [FromQuery] Guid? clientId)
        {
            var tenantId = GetCurrentTenantId();
            var error = MonthRange.Parse(from, to, out var months);
            if (error != null) return BadRequest(error);
            var start = new DateTime(months[0].Year, months[0].Month, 1);
            var end = new DateTime(months[^1].Year, months[^1].Month, 1).AddMonths(1);

            var query = _dbContext.PlanCharges.AsNoTracking().Where(c => c.TenantId == tenantId && c.RunPeriod >= start && c.RunPeriod < end);
            if (clientId != null) query = query.Where(c => c.ClientId == clientId);
            var charges = await query.OrderByDescending(c => c.RunPeriod).ThenBy(c => c.Client!.CompanyName)
                .Select(c => new
                {
                    c.Id, c.ClientId,
                    clientName = !string.IsNullOrEmpty(c.Client!.CommercialName) ? c.Client.CommercialName : c.Client.CompanyName,
                    c.PlanName, c.RunPeriod, c.FeeAmount, c.ExtraAmount, c.Description, c.DocumentId, c.CreatedAt
                })
                .ToListAsync();

            var documentIds = charges.Where(c => c.DocumentId != null).Select(c => c.DocumentId!.Value).ToList();
            var documents = await _dbContext.Documents.AsNoTracking().Where(d => documentIds.Contains(d.Id))
                .Select(d => new { d.Id, d.Status, d.Number, d.Prefix, d.TotalAmount })
                .ToDictionaryAsync(d => d.Id);

            var items = charges.Select(c =>
            {
                string state;
                if (c.DocumentId == null) state = "NoCharge";
                else if (!documents.TryGetValue(c.DocumentId.Value, out var d)) state = "Discarded";
                else state = d.Status switch { "DRAFT" => "Draft", "APPROVED" => "Issued", "REJECTED" => "Rejected", _ => "InProcess" };
                documents.TryGetValue(c.DocumentId ?? Guid.Empty, out var doc);
                return new
                {
                    c.Id, c.ClientId, c.clientName, c.PlanName, runPeriod = c.RunPeriod, c.FeeAmount, c.ExtraAmount, total = c.FeeAmount + c.ExtraAmount, c.Description,
                    c.DocumentId, state, number = doc == null || string.IsNullOrEmpty(doc.Number) ? null : (doc.Prefix ?? string.Empty) + doc.Number
                };
            }).ToList();

            var settings = await _dbContext.TenantInvoicingSettings.AsNoTracking().FirstOrDefaultAsync(s => s.TenantId == tenantId);
            decimal Sum(string st) => items.Where(i => i.state == st).Sum(i => i.total);
            return Ok(new
            {
                from = MonthRange.Key(months[0].Year, months[0].Month), to = MonthRange.Key(months[^1].Year, months[^1].Month),
                ownerClientId = settings?.OwnerClientId,
                summary = new
                {
                    drafts = items.Count(i => i.state == "Draft"), draftsTotal = Sum("Draft"),
                    issued = items.Count(i => i.state == "Issued"), issuedTotal = Sum("Issued"),
                    rejected = items.Count(i => i.state == "Rejected"), inProcess = items.Count(i => i.state == "InProcess"),
                    discarded = items.Count(i => i.state == "Discarded")
                },
                items
            });
        }
    }
}
