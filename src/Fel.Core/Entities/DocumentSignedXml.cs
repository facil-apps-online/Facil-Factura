using System;

namespace Fel.Core.Entities
{
    // XML firmado tal como se envió a la DIAN, guardado al emitir. El reenvío y las descargas usan este archivo en vez de rearmarlo: rearmarlo
    // dependía de que los datos y las reglas de armado no hubieran cambiado, y volvía a firmar, así que el XML ya no era el que la DIAN aceptó.
    // Va en su propia tabla para no engordar Documents; se guarda comprimido (gzip).
    public class DocumentSignedXml
    {
        public Guid DocumentId { get; set; }
        public Document Document { get; set; } = null!;
        public byte[] XmlGzip { get; set; } = Array.Empty<byte>();
        public DateTime SignedAt { get; set; } = DateTime.UtcNow;
    }
}
