using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Controllers
{
    [ApiController]
    [Route("api/tenant/clients/{clientId}/certificate")]
    public class TenantCertificatesController : ControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly ICertificateStorageService _storageService;
        private readonly ICryptoVault _cryptoVault;

        public TenantCertificatesController(FelDbContext dbContext, ICertificateStorageService storageService, ICryptoVault cryptoVault)
        {
            _dbContext = dbContext;
            _storageService = storageService;
            _cryptoVault = cryptoVault;
        }

        private Guid GetCurrentTenantId()
        {
            if (Request.Headers.TryGetValue("x-tenant-id", out var tenantIdStr))
            {
                if (Guid.TryParse(tenantIdStr, out var tenantId))
                    return tenantId;
            }
            throw new UnauthorizedAccessException("x-tenant-id Header is missing");
        }

        [HttpGet]
        public async Task<IActionResult> GetCertificate(Guid clientId)
        {
            var tenantId = GetCurrentTenantId();
            var clientExists = await _dbContext.Clients.AnyAsync(c => c.Id == clientId && c.TenantId == tenantId);
            if (!clientExists) return Forbid();

            var certificate = await _dbContext.Set<Certificate>()
                .Where(c => c.ClientId == clientId && c.IsActive)
                .Select(c => new {
                    c.FileName,
                    c.ExpirationDate,
                    c.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (certificate == null) return NotFound();
            return Ok(certificate);
        }

        [HttpPost]
        public async Task<IActionResult> UploadCertificate(Guid clientId, [FromForm] IFormFile file, [FromForm] string password)
        {
            if (file == null || file.Length == 0) return BadRequest("Debe cargar un archivo válido (.p12 o .pfx).");
            if (string.IsNullOrEmpty(password)) return BadRequest("Debe ingresar la contraseña del certificado.");

            var tenantId = GetCurrentTenantId();
            var clientExists = await _dbContext.Clients.AnyAsync(c => c.Id == clientId && c.TenantId == tenantId);
            if (!clientExists) return Forbid();

            using var stream = file.OpenReadStream();
            var savedPath = await _storageService.SaveCertificateAsync(clientId, stream, file.FileName);
            var encryptedPass = _cryptoVault.EncryptPassword(password);

            DateTime expirationDate;
            try
            {
                var x509 = _cryptoVault.GetCertificate(savedPath, encryptedPass);
                expirationDate = x509.NotAfter;
            }
            catch (Exception)
            {
                _storageService.DeleteCertificate(savedPath);
                return BadRequest("La contraseña es incorrecta o el archivo de certificado no es válido.");
            }

            var oldCert = await _dbContext.Set<Certificate>().FirstOrDefaultAsync(c => c.ClientId == clientId && c.IsActive);
            if (oldCert != null)
            {
                oldCert.IsActive = false;
            }

            var newCert = new Certificate
            {
                Id = Guid.NewGuid(),
                ClientId = clientId,
                FileName = savedPath,
                EncryptedPassword = encryptedPass,
                ExpirationDate = expirationDate,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _dbContext.Set<Certificate>().Add(newCert);
            await _dbContext.SaveChangesAsync();

            return Ok(new {
                FileName = file.FileName,
                ExpirationDate = expirationDate,
                CreatedAt = newCert.CreatedAt
            });
        }
    }
}
