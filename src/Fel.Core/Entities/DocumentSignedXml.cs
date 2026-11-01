using System;

namespace Fel.Core.Entities
{
    // Los archivos del documento tal como se generaron al emitirlo, guardados para no tener que rearmarlos: el XML firmado que se envió a la DIAN,
    // la respuesta de aprobación de la DIAN (ApplicationResponse) y el contenedor AttachedDocument que se entrega al adquirente. Rearmarlos
    // dependía de que los datos y las reglas de armado no hubieran cambiado, y volver a firmar daba un archivo distinto al que la DIAN aceptó.
    // Va en su propia tabla para no engordar Documents; todo se guarda comprimido (gzip).
    public class DocumentSignedXml
    {
        public Guid DocumentId { get; set; }
        public Document Document { get; set; } = null!;
        public byte[] XmlGzip { get; set; } = Array.Empty<byte>();
        public DateTime SignedAt { get; set; } = DateTime.UtcNow;
        // Consecutivo de archivo DIAN con el que se envió (numeral 6.7): da el nombre de los archivos del correo.
        public int FileSequence { get; set; }
        // ApplicationResponse con la aprobación de la DIAN; null hasta que el documento queda aprobado.
        public byte[]? ApplicationResponseGzip { get; set; }
        // AttachedDocument firmado (factura + aprobación), el archivo que se envía al adquirente.
        public byte[]? AttachedDocumentGzip { get; set; }
    }
}
