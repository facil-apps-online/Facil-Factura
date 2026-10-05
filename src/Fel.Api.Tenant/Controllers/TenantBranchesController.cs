using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Tenant.Controllers
{
    // Sucursales de un Client, gestionadas por su Tenant: alta, edición, desactivación y reactivación, tarifa y llaves de API propias.
    [ApiController]
    [Route("api/tenant/clients/{clientId}/branches")]
    public class TenantBranchesController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public TenantBranchesController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public class SaveBranchRequest
        {
            public string Name { get; set; } = string.Empty;
            public string Code { get; set; } = string.Empty;
            public string? Address { get; set; }
            public string? City { get; set; }
            public string? CityCode { get; set; }
            public string? Phone { get; set; }
            public string? Email { get; set; }
            public decimal SubscriptionRate { get; set; }
            public decimal PricePerDocument { get; set; }
            // Solo al crear: resoluciones del Client que la sucursal usará desde el primer día.
            public List<Guid>? ResolutionIds { get; set; }
        }

        private Guid GetCurrentTenantId()
        {
            if (Request.Headers.TryGetValue("x-tenant-id", out var tenantIdStr) && Guid.TryParse(tenantIdStr, out var tenantId))
                return tenantId;
            throw new UnauthorizedAccessException("x-tenant-id Header is missing");
        }

        private async Task<Client?> GetOwnedClientAsync(Guid clientId)
        {
            var tenantId = GetCurrentTenantId();
            return await _dbContext.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clientId && c.TenantId == tenantId);
        }

        private static string? Validate(SaveBranchRequest r)
        {
            if (string.IsNullOrWhiteSpace(r.Name)) return "El nombre de la sucursal es obligatorio.";
            if (string.IsNullOrWhiteSpace(r.Code)) return "El código de la sucursal es obligatorio.";
            if (r.SubscriptionRate < 0 || r.PricePerDocument < 0) return "Las tarifas no pueden ser negativas.";
            if (!string.IsNullOrWhiteSpace(r.CityCode) && (r.CityCode.Trim().Length != 5 || !r.CityCode.Trim().All(char.IsDigit)))
                return "El código DANE del municipio debe tener 5 dígitos.";
            return null;
        }

        private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private object ToDto(Branch b, int userCount, List<Guid> resolutionIds) => new
        {
            b.Id, b.Name, b.Code, b.IsMain, b.IsActive, b.CreatedAt, b.DeactivatedAt,
            b.Address, b.City, b.CityCode, b.Phone, b.Email,
            b.SubscriptionRate, b.PricePerDocument,
            b.LiveApiKey, b.LiveApiSecret, b.TestApiKey, b.TestApiSecret,
            UserCount = userCount,
            ResolutionIds = resolutionIds
        };

        private async Task<object> LoadDtoAsync(Branch b)
        {
            var userCount = await _dbContext.ClientUsers.CountAsync(u =>
                u.ClientId == b.ClientId && u.IsActive && (u.AllBranches || u.Branches.Any(ub => ub.BranchId == b.Id)));
            var resolutionIds = await _dbContext.ResolutionBranches.Where(rb => rb.BranchId == b.Id).Select(rb => rb.ResolutionId).ToListAsync();
            return ToDto(b, userCount, resolutionIds);
        }

        [HttpGet]
        public async Task<IActionResult> List(Guid clientId)
        {
            try
            {
                if (await GetOwnedClientAsync(clientId) == null) return NotFound();

                var branches = await _dbContext.Branches.AsNoTracking()
                    .Where(b => b.ClientId == clientId)
                    .OrderByDescending(b => b.IsMain).ThenBy(b => b.CreatedAt)
                    .ToListAsync();

                var counts = await _dbContext.ClientUsers.AsNoTracking()
                    .Where(u => u.ClientId == clientId && u.IsActive)
                    .Select(u => new { u.AllBranches, BranchIds = u.Branches.Select(x => x.BranchId).ToList() })
                    .ToListAsync();
                var links = await _dbContext.ResolutionBranches.AsNoTracking()
                    .Where(rb => rb.Branch.ClientId == clientId)
                    .Select(rb => new { rb.BranchId, rb.ResolutionId })
                    .ToListAsync();

                return Ok(branches.Select(b => ToDto(b,
                    counts.Count(u => u.AllBranches || u.BranchIds.Contains(b.Id)),
                    links.Where(l => l.BranchId == b.Id).Select(l => l.ResolutionId).ToList())));
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(ex.Message); }
        }

        [HttpPost]
        public async Task<IActionResult> Create(Guid clientId, [FromBody] SaveBranchRequest request)
        {
            try
            {
                var client = await GetOwnedClientAsync(clientId);
                if (client == null) return NotFound();
                if (!client.IsActive) return BadRequest("El cliente está inactivo; reactívalo antes de crear sucursales.");

                var error = Validate(request);
                if (error != null) return BadRequest(error);

                var code = request.Code.Trim().ToUpperInvariant();
                if (await _dbContext.Branches.AnyAsync(b => b.ClientId == clientId && b.Code == code))
                    return BadRequest("Ya existe una sucursal con ese código en este cliente.");

                var resolutionIds = (request.ResolutionIds ?? new List<Guid>()).Distinct().ToList();
                if (resolutionIds.Count > 0)
                {
                    var owned = await _dbContext.Resolutions.CountAsync(r => r.ClientId == clientId && r.IsActive && resolutionIds.Contains(r.Id));
                    if (owned != resolutionIds.Count) return BadRequest("Alguna de las resoluciones elegidas no existe o está inactiva.");
                }

                var branch = new Branch
                {
                    Id = Guid.NewGuid(),
                    ClientId = clientId,
                    Name = request.Name.Trim(),
                    Code = code,
                    IsMain = false,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    Address = Clean(request.Address),
                    City = Clean(request.City),
                    CityCode = Clean(request.CityCode),
                    Phone = Clean(request.Phone),
                    Email = Clean(request.Email),
                    SubscriptionRate = request.SubscriptionRate,
                    PricePerDocument = request.PricePerDocument
                };
                _dbContext.Branches.Add(branch);
                foreach (var resolutionId in resolutionIds)
                    _dbContext.ResolutionBranches.Add(BranchProvisioning.LinkResolution(resolutionId, branch.Id));
                await _dbContext.SaveChangesAsync();

                return Ok(await LoadDtoAsync(branch));
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(ex.Message); }
        }

        [HttpPut("{branchId:guid}")]
        public async Task<IActionResult> Update(Guid clientId, Guid branchId, [FromBody] SaveBranchRequest request)
        {
            try
            {
                if (await GetOwnedClientAsync(clientId) == null) return NotFound();
                var branch = await _dbContext.Branches.FirstOrDefaultAsync(b => b.Id == branchId && b.ClientId == clientId);
                if (branch == null) return NotFound();

                var error = Validate(request);
                if (error != null) return BadRequest(error);

                // La principal conserva su nombre y código: el sistema la reconoce por ellos en datos y reportes anteriores.
                if (!branch.IsMain)
                {
                    var code = request.Code.Trim().ToUpperInvariant();
                    if (await _dbContext.Branches.AnyAsync(b => b.ClientId == clientId && b.Code == code && b.Id != branchId))
                        return BadRequest("Ya existe una sucursal con ese código en este cliente.");
                    branch.Name = request.Name.Trim();
                    branch.Code = code;
                }

                branch.Address = Clean(request.Address);
                branch.City = Clean(request.City);
                branch.CityCode = Clean(request.CityCode);
                branch.Phone = Clean(request.Phone);
                branch.Email = Clean(request.Email);
                branch.SubscriptionRate = request.SubscriptionRate;
                branch.PricePerDocument = request.PricePerDocument;
                await _dbContext.SaveChangesAsync();

                return Ok(await LoadDtoAsync(branch));
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(ex.Message); }
        }

        // Desactivar deja de cobrar la sucursal desde hoy (prorrateo por días) y la saca del selector del portal. La principal no se desactiva:
        // para dar de baja a todo el cliente se desactiva el cliente.
        [HttpPost("{branchId:guid}/deactivate")]
        public async Task<IActionResult> Deactivate(Guid clientId, Guid branchId)
        {
            try
            {
                if (await GetOwnedClientAsync(clientId) == null) return NotFound();
                var branch = await _dbContext.Branches.FirstOrDefaultAsync(b => b.Id == branchId && b.ClientId == clientId);
                if (branch == null) return NotFound();
                if (branch.IsMain) return BadRequest("La sucursal principal no se puede desactivar.");

                if (branch.IsActive)
                {
                    branch.IsActive = false;
                    branch.DeactivatedAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                }
                return Ok(await LoadDtoAsync(branch));
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(ex.Message); }
        }

        [HttpPost("{branchId:guid}/reactivate")]
        public async Task<IActionResult> Reactivate(Guid clientId, Guid branchId)
        {
            try
            {
                if (await GetOwnedClientAsync(clientId) == null) return NotFound();
                var branch = await _dbContext.Branches.FirstOrDefaultAsync(b => b.Id == branchId && b.ClientId == clientId);
                if (branch == null) return NotFound();

                if (!branch.IsActive)
                {
                    branch.IsActive = true;
                    branch.DeactivatedAt = null;
                    await _dbContext.SaveChangesAsync();
                }
                return Ok(await LoadDtoAsync(branch));
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(ex.Message); }
        }

        [HttpPost("{branchId:guid}/generate-key")]
        public async Task<IActionResult> GenerateApiKey(Guid clientId, Guid branchId, [FromQuery] string env)
        {
            try
            {
                if (await GetOwnedClientAsync(clientId) == null) return NotFound();
                var branch = await _dbContext.Branches.FirstOrDefaultAsync(b => b.Id == branchId && b.ClientId == clientId);
                if (branch == null) return NotFound();

                var newKey = $"sk_{(env ?? string.Empty).ToLower()}_{Guid.NewGuid():N}";
                var newSecret = Guid.NewGuid().ToString("N");

                if (string.Equals(env, "live", StringComparison.OrdinalIgnoreCase))
                {
                    branch.LiveApiKey = newKey;
                    branch.LiveApiSecret = newSecret;
                }
                else if (string.Equals(env, "test", StringComparison.OrdinalIgnoreCase))
                {
                    branch.TestApiKey = newKey;
                    branch.TestApiSecret = newSecret;
                }
                else return BadRequest("Invalid environment. Use 'live' or 'test'.");

                await _dbContext.SaveChangesAsync();
                return Ok(new { key = newKey, secret = newSecret });
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(ex.Message); }
        }
    }
}
