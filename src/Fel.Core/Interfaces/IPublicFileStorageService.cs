using System;
using System.IO;
using System.Threading.Tasks;

namespace Fel.Core.Interfaces
{
    public class StoredFile
    {
        public byte[] Bytes { get; set; } = Array.Empty<byte>();
        public string ContentType { get; set; } = "application/octet-stream";
    }

    public interface IPublicFileStorageService
    {
        Task<string> SaveFileAsync(string category, Guid ownerId, Stream fileStream, string originalFileName);
        Task<StoredFile?> GetFileAsync(string relativeKey);
    }
}
