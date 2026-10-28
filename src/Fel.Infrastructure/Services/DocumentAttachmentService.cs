using System.Text;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Services
{
    public sealed record DocumentAttachmentInfo(Guid Id, string FileName, long SizeBytes, DateTime CreatedAt);

    public sealed record DocumentAttachmentResult(DocumentAttachmentInfo? Attachment, string? Error)
    {
        public bool Success => Error == null;
    }

    // Los PDF que el cliente adjunta a una factura, nota crédito o nota débito para mandarlos por correo con el documento (Dataico los recibe en base64 en
    // actions.attachments). Reúne las reglas en un solo lugar para el portal y la API de integración: solo PDF (por extensión y por su encabezado), MaxTotalBytes
    // sumados por documento (sin límite en la cantidad de archivos), y solo mientras el documento es borrador o está rechazado.
    public class DocumentAttachmentService
    {
        private readonly FelDbContext _db;

        public DocumentAttachmentService(FelDbContext db)
        {
            _db = db;
        }

        // Un documento emitido ya no cambia: sus adjuntos quedan de solo lectura (se pueden consultar y descargar).
        public static bool IsEditable(string status) => status == "DRAFT" || status == "REJECTED";

        public async Task<List<DocumentAttachmentInfo>> ListAsync(Guid documentId) =>
            await _db.DocumentAttachments.AsNoTracking()
                .Where(a => a.DocumentId == documentId)
                .OrderBy(a => a.CreatedAt).ThenBy(a => a.FileName)
                .Select(a => new DocumentAttachmentInfo(a.Id, a.FileName, a.SizeBytes, a.CreatedAt))
                .ToListAsync();

        public async Task<DocumentAttachment?> GetAsync(Guid documentId, Guid attachmentId) =>
            await _db.DocumentAttachments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == attachmentId && a.DocumentId == documentId);

        // Valida y guarda un PDF. El documento ya debe ser del cliente (lo comprueba quien llama).
        public async Task<DocumentAttachmentResult> AddAsync(Document document, string? fileName, byte[] content)
        {
            if (!IsEditable(document.Status))
                return Fail("Los adjuntos solo se pueden cambiar mientras el documento es un borrador o está rechazado.");

            var name = SafeFileName(fileName);
            if (name == null) return Fail("El archivo debe ser un PDF (nombre terminado en .pdf).");
            if (content.Length == 0) return Fail("El archivo está vacío.");
            if (!LooksLikePdf(content)) return Fail($"«{name}» no es un PDF válido.");
            if (content.Length > DocumentAttachment.MaxTotalBytes)
                return Fail($"«{name}» pesa {Mb(content.Length)} y el máximo por documento es {Mb(DocumentAttachment.MaxTotalBytes)}.");

            var existing = await _db.DocumentAttachments.Where(a => a.DocumentId == document.Id).Select(a => a.SizeBytes).ToListAsync();
            if (existing.Sum() + content.Length > DocumentAttachment.MaxTotalBytes)
                return Fail($"Con «{name}» los adjuntos sumarían {Mb(existing.Sum() + content.Length)} y el máximo por documento es {Mb(DocumentAttachment.MaxTotalBytes)}.");

            var attachment = new DocumentAttachment
            {
                Id = Guid.NewGuid(),
                DocumentId = document.Id,
                FileName = name,
                ContentType = "application/pdf",
                SizeBytes = content.Length,
                Content = content,
                CreatedAt = DateTime.UtcNow
            };
            _db.DocumentAttachments.Add(attachment);
            await _db.SaveChangesAsync();
            return new DocumentAttachmentResult(new DocumentAttachmentInfo(attachment.Id, attachment.FileName, attachment.SizeBytes, attachment.CreatedAt), null);
        }

        // Devuelve null si se eliminó, o el motivo si no se pudo.
        public async Task<string?> RemoveAsync(Document document, Guid attachmentId)
        {
            if (!IsEditable(document.Status))
                return "Los adjuntos solo se pueden cambiar mientras el documento es un borrador o está rechazado.";
            var attachment = await _db.DocumentAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.DocumentId == document.Id);
            if (attachment == null) return "Adjunto no encontrado.";
            _db.DocumentAttachments.Remove(attachment);
            await _db.SaveChangesAsync();
            return null;
        }

        // Los archivos de un documento tal como se mandan a Dataico (nombre y contenido).
        public async Task<List<(string Name, byte[] Content)>> LoadForSendAsync(Guid documentId)
        {
            var rows = await _db.DocumentAttachments.AsNoTracking()
                .Where(a => a.DocumentId == documentId)
                .OrderBy(a => a.CreatedAt).ThenBy(a => a.FileName)
                .Select(a => new { a.FileName, a.Content })
                .ToListAsync();
            return rows.Select(r => (r.FileName, r.Content)).ToList();
        }

        // Tamaño de la petición de correo a Dataico ya en base64 (el PDF propio del documento y los adjuntos); el tope de Dataico es de unos 7 MB.
        public static long Base64Size(long rawBytes) => (rawBytes + 2) / 3 * 4;

        // Solo el nombre, sin rutas ni caracteres que no valgan en un archivo, terminado en .pdf; null si no es un PDF por su nombre.
        public static string? SafeFileName(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return null;
            var name = Path.GetFileName(fileName.Replace('\\', '/')).Trim();
            if (!name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) return null;
            var sb = new StringBuilder();
            foreach (var c in name) sb.Append(char.IsControl(c) || Path.GetInvalidFileNameChars().Contains(c) ? '_' : c);
            var clean = sb.ToString();
            if (clean.Length > DocumentAttachment.MaxFileNameLength)
                clean = clean[..(DocumentAttachment.MaxFileNameLength - 4)] + ".pdf";
            return clean.Length > 4 ? clean : null;
        }

        // Un PDF empieza con "%PDF-" (la especificación admite algo de basura antes, dentro de los primeros 1024 bytes).
        public static bool LooksLikePdf(byte[] content)
        {
            var head = content.AsSpan(0, Math.Min(content.Length, 1024));
            return head.IndexOf("%PDF-"u8) >= 0;
        }

        private static DocumentAttachmentResult Fail(string error) => new(null, error);
        private static string Mb(long bytes) => $"{bytes / 1024.0 / 1024.0:0.0} MB";
    }
}
