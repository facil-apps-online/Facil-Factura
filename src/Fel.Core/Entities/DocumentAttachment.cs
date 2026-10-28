using System;

namespace Fel.Core.Entities
{
    // Un archivo PDF adjunto a un documento (factura, nota crédito o débito) que el cliente quiere mandar por correo con el documento. Se carga mientras el
    // documento es borrador o está rechazado y se conserva con él: el reenvío y la consulta desde el detalle lo usan. Dataico recibe los adjuntos en base64
    // dentro de la misma petición del correo (actions.attachments) y su tope es de unos 7 MB en base64, o sea unos 5 MB de archivos.
    //
    // No cuelga como colección de Document a propósito: el contenido pesa y ninguna consulta de documentos debe arrastrarlo; se lee solo por su documento.
    public class DocumentAttachment
    {
        // Tope de los archivos sumados de un documento (en bytes sin codificar). Dataico acepta unos 7 MB de petición y base64 agrega cerca de un tercio.
        public const long MaxTotalBytes = 5L * 1024 * 1024;
        // Tope de toda la petición de correo a Dataico ya en base64 (PDF propio del documento + adjuntos).
        public const long MaxRequestBytes = 7L * 1024 * 1024;
        public const int MaxFileNameLength = 200;

        public Guid Id { get; set; }

        public Guid DocumentId { get; set; }

        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/pdf";
        public long SizeBytes { get; set; }
        public byte[] Content { get; set; } = Array.Empty<byte>();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
