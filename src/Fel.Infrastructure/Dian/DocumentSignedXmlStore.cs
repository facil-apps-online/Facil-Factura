using System.IO.Compression;
using System.Text;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Dian
{
    // Lo guardado de un documento (ver DocumentSignedXml), ya descomprimido.
    public record StoredDocumentXml(string SignedXml, string? ApplicationResponse, string? AttachedDocument, int FileSequence, DateTime SignedAt);

    // Guarda y lee los archivos de un documento. Guardar el XML firmado reemplaza el anterior: si el envío a la DIAN se reintenta, queda el
    // último firmado, que es el que se envió.
    public static class DocumentSignedXmlStore
    {
        // Guarda al instante, para que el XML no se pierda si algo falla más adelante en la emisión.
        public static async Task SaveSignedAsync(FelDbContext db, Guid documentId, string signedXml, int fileSequence, CancellationToken ct = default)
        {
            var existing = await db.DocumentSignedXmls.FirstOrDefaultAsync(x => x.DocumentId == documentId, ct);
            if (existing == null)
            {
                db.DocumentSignedXmls.Add(new DocumentSignedXml { DocumentId = documentId, XmlGzip = Compress(signedXml), SignedAt = DateTime.UtcNow, FileSequence = fileSequence });
            }
            else
            {
                existing.XmlGzip = Compress(signedXml);
                existing.SignedAt = DateTime.UtcNow;
                existing.FileSequence = fileSequence;
                // Un XML nuevo invalida lo que se armó con el anterior.
                existing.ApplicationResponseGzip = null;
                existing.AttachedDocumentGzip = null;
            }
            await db.SaveChangesAsync(ct);
        }

        public static async Task SaveApplicationResponseAsync(FelDbContext db, Guid documentId, string applicationResponse, CancellationToken ct = default)
        {
            var row = await db.DocumentSignedXmls.FirstOrDefaultAsync(x => x.DocumentId == documentId, ct);
            if (row == null) return;
            row.ApplicationResponseGzip = Compress(applicationResponse);
            await db.SaveChangesAsync(ct);
        }

        public static async Task SaveAttachedDocumentAsync(FelDbContext db, Guid documentId, string attachedDocument, CancellationToken ct = default)
        {
            var row = await db.DocumentSignedXmls.FirstOrDefaultAsync(x => x.DocumentId == documentId, ct);
            if (row == null) return;
            row.AttachedDocumentGzip = Compress(attachedDocument);
            await db.SaveChangesAsync(ct);
        }

        // null si el documento se emitió antes de que se guardara el XML.
        public static async Task<StoredDocumentXml?> LoadAsync(FelDbContext db, Guid documentId, CancellationToken ct = default)
        {
            var row = await db.DocumentSignedXmls.AsNoTracking().FirstOrDefaultAsync(x => x.DocumentId == documentId, ct);
            if (row == null || row.XmlGzip.Length == 0) return null;
            return new StoredDocumentXml(
                Decompress(row.XmlGzip),
                row.ApplicationResponseGzip is { Length: > 0 } ar ? Decompress(ar) : null,
                row.AttachedDocumentGzip is { Length: > 0 } ad ? Decompress(ad) : null,
                row.FileSequence,
                row.SignedAt);
        }

        private static byte[] Compress(string xml)
        {
            using var output = new MemoryStream();
            using (var gz = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
            {
                var bytes = Encoding.UTF8.GetBytes(xml);
                gz.Write(bytes, 0, bytes.Length);
            }
            return output.ToArray();
        }

        private static string Decompress(byte[] gzip)
        {
            using var input = new MemoryStream(gzip);
            using var gz = new GZipStream(input, CompressionMode.Decompress);
            using var reader = new StreamReader(gz, Encoding.UTF8);
            return reader.ReadToEnd();
        }
    }
}
