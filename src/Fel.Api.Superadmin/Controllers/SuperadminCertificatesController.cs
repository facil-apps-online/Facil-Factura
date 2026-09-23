using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Superadmin.Controllers
{
    [ApiController]
    [Route("api/superadmin/certificates")]
    public class SuperadminCertificatesController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public SuperadminCertificatesController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // Vista consolidada de todos los certificados de TODOS los tenants, ordenada por fecha de
        // vencimiento — para que Superadmin sepa qué hay que provisionar/dar seguimiento pronto,
        // sin tener que entrar tenant por tenant.
        [HttpGet]
        public async Task<IActionResult> GetAllCertificates()
        {
            var certificates = await _dbContext.Set<Certificate>()
                .Where(c => c.IsActive)
                .OrderBy(c => c.ExpirationDate)
                .Select(c => new
                {
                    c.ClientId,
                    ClientName = string.IsNullOrWhiteSpace(c.Client.CommercialName) ? c.Client.CompanyName : c.Client.CommercialName,
                    TenantId = c.Client.TenantId,
                    TenantName = c.Client.Tenant.CommercialName,
                    c.FileName,
                    c.ExpirationDate,
                    c.CreatedAt
                })
                .ToListAsync();

            return Ok(certificates);
        }
    }
}
