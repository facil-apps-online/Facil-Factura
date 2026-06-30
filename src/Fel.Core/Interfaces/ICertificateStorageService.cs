using System;
using System.IO;
using System.Threading.Tasks;

namespace Fel.Core.Interfaces
{
    public interface ICertificateStorageService
    {
        Task<string> SaveCertificateAsync(Guid clientId, Stream fileStream, string originalFileName);
        Task<byte[]> GetCertificateBytesAsync(string filePath);
        void DeleteCertificate(string filePath);
    }
}
