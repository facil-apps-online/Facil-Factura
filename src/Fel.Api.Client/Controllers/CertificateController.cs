using System;
using System.IO;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Fel.Api.Client.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CertificateController : ControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly ICertificateStorageService _storageService;
        private readonly ICryptoService _cryptoService;

        public CertificateController(FelDbContext dbContext, ICertificateStorageService storageService, ICryptoService cryptoService)
        {
            _dbContext = dbContext;
            _storageService = storageService;
            _cryptoService = cryptoService;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadCertificate([FromForm] Guid clientId, [FromForm] string password, IFormFile p12File)
        {
            if (p12File == null || p12File.Length == 0)
            {
                return BadRequest(new { Message = "Archivo .p12 es requerido." });
            }

            var client = await _dbContext.Clients.FindAsync(clientId);
            if (client == null)
            {
                return NotFound(new { Message = "Client not found" });
            }

            // Save the file securely
            using var stream = p12File.OpenReadStream();
            string savedPath = await _storageService.SaveCertificateAsync(clientId, stream, p12File.FileName);

            // Encrypt the password using the MasterKey via CryptoService
            string encryptedPassword = _cryptoService.Encrypt(password);

            // Save record in DB
            var cert = new Certificate
            {
                Id = Guid.NewGuid(),
                ClientId = clientId,
                FileName = savedPath, // Storing the absolute safe path
                EncryptedPassword = encryptedPassword,
                ExpirationDate = DateTime.UtcNow.AddYears(1), // Should ideally be read from the cert itself
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _dbContext.Certificates.Add(cert);
            await _dbContext.SaveChangesAsync();

            return Ok(new { Message = "Certificado subido y guardado de forma segura.", CertificateId = cert.Id });
        }
    }
}
