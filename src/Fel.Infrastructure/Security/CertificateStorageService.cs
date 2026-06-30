using System;
using System.IO;
using System.Threading.Tasks;
using Fel.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Fel.Infrastructure.Security
{
    public class CertificateStorageService : ICertificateStorageService
    {
        private readonly string _storagePath;

        public CertificateStorageService(IConfiguration configuration)
        {
            _storagePath = configuration["CertificateStoragePath"] ?? "/var/secure/certificates/";
            
            if (!Directory.Exists(_storagePath))
            {
                // Emulate creating the isolated directory
                // In a real Linux deployment, this should be done by the admin with chmod 700
                Directory.CreateDirectory(_storagePath);
            }
        }

        public async Task<string> SaveCertificateAsync(Guid clientId, Stream fileStream, string originalFileName)
        {
            var fileName = $"{clientId}_{Guid.NewGuid()}_{Path.GetFileName(originalFileName)}";
            var fullPath = Path.Combine(_storagePath, fileName);

            using (var fileStreamOutput = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
            {
                await fileStream.CopyToAsync(fileStreamOutput);
            }

            return fullPath;
        }

        public async Task<byte[]> GetCertificateBytesAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Certificate file not found at path: {filePath}");
            }

            return await File.ReadAllBytesAsync(filePath);
        }

        public void DeleteCertificate(string filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
