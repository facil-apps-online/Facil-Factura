using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Fel.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Fel.Infrastructure.Storage
{
    public class PublicFileStorageService : IPublicFileStorageService
    {
        private static readonly HashSet<string> AllowedCategories = new(StringComparer.OrdinalIgnoreCase) { "logos" };

        private static readonly Dictionary<string, string> ContentTypesByExtension = new(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".webp"] = "image/webp",
            [".svg"] = "image/svg+xml"
        };

        private readonly string _storagePath;

        public PublicFileStorageService(IConfiguration configuration)
        {
            _storagePath = configuration["PublicFileStoragePath"] ?? "/app/public-storage";

            if (!Directory.Exists(_storagePath))
            {
                Directory.CreateDirectory(_storagePath);
            }
        }

        public async Task<string> SaveFileAsync(string category, Guid ownerId, Stream fileStream, string originalFileName)
        {
            if (!AllowedCategories.Contains(category))
            {
                throw new ArgumentException($"Categoría de archivo no permitida: {category}");
            }

            var extension = Path.GetExtension(originalFileName);
            if (!ContentTypesByExtension.ContainsKey(extension))
            {
                throw new ArgumentException("Tipo de imagen no soportado. Usa PNG, JPG, WEBP o SVG.");
            }

            var categoryPath = Path.Combine(_storagePath, category);
            if (!Directory.Exists(categoryPath))
            {
                Directory.CreateDirectory(categoryPath);
            }

            var fileName = $"{ownerId}_{Guid.NewGuid():N}{extension}";
            var fullPath = Path.Combine(categoryPath, fileName);

            using (var output = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
            {
                await fileStream.CopyToAsync(output);
            }

            return $"{category}/{fileName}";
        }

        public async Task<StoredFile?> GetFileAsync(string relativeKey)
        {
            var fullPath = Path.GetFullPath(Path.Combine(_storagePath, relativeKey));
            var storageRoot = Path.GetFullPath(_storagePath);

            if (!fullPath.StartsWith(storageRoot, StringComparison.Ordinal) || !File.Exists(fullPath))
            {
                return null;
            }

            var extension = Path.GetExtension(fullPath);
            var contentType = ContentTypesByExtension.TryGetValue(extension, out var ct) ? ct : "application/octet-stream";

            return new StoredFile
            {
                Bytes = await File.ReadAllBytesAsync(fullPath),
                ContentType = contentType
            };
        }
    }
}
