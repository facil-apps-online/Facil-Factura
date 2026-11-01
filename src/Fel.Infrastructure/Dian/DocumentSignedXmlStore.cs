using System.IO.Compression;
using System.Text;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Dian
{
    // Guarda y lee el XML firmado de un documento (ver DocumentSignedXml). Guardar reemplaza el anterior: si el envío a la DIAN se reintenta,
    // queda el último XML firmado, que es el que se envió.
    public static class DocumentSignedXmlStore
    {
        // Agrega o actualiza la fila en el contexto y la guarda al instante, para que el XML no se pierda si algo falla más adelante en la emisión.
        public static async Task SaveAsync(FelDbContext db, Guid documentId, string signedXml, CancellationToken ct = default)
        {
            var existing = await db.DocumentSignedXmls.FirstOrDefaultAsync(x => x.DocumentId == documentId, ct);
            var gzip = Compress(signedXml);
            if (existing == null)
                db.DocumentSignedXmls.Add(new DocumentSignedXml { DocumentId = documentId, XmlGzip = gzip, SignedAt = DateTime.UtcNow });
            else
            {
                existing.XmlGzip = gzip;
                existing.SignedAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync(ct);
        }

        // null si el documento se emitió antes de que se guardara el XML.
        public static async Task<string?> LoadAsync(FelDbContext db, Guid documentId, CancellationToken ct = default)
        {
            var gzip = await db.DocumentSignedXmls.AsNoTracking().Where(x => x.DocumentId == documentId).Select(x => x.XmlGzip).FirstOrDefaultAsync(ct);
            return gzip == null || gzip.Length == 0 ? null : Decompress(gzip);
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
