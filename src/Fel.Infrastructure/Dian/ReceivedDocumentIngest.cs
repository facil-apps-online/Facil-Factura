using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Fel.Core.Entities;

namespace Fel.Infrastructure.Dian
{
    // Ingreso de un documento de un tercero (XML suelto o ZIP con XML y PDF): lo comparten la carga del portal y la API de integración
    // para que ambos lean el archivo y armen el ReceivedDocument de la misma manera.
    public static class ReceivedDocumentIngest
    {
        public static (string Xml, byte[]? Pdf, string? PdfFileName) ReadFile(byte[] content, string? fileName)
        {
            // Detecta ZIP por firma de archivo aunque venga con otra extensión.
            var isZip = (fileName?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ?? false)
                || (content.Length >= 4 && content[0] == 0x50 && content[1] == 0x4B);

            if (!isZip)
            {
                // StreamReader descarta la marca BOM, como al leer el archivo subido.
                using var reader = new StreamReader(new MemoryStream(content), Encoding.UTF8);
                return (ReceivedDocumentXmlExtractor.Extract(reader.ReadToEnd()), null, null);
            }

            using var zip = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read);
            var xmlEntry = zip.Entries.FirstOrDefault(e => e.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
            if (xmlEntry == null)
                throw new InvalidOperationException("El ZIP no contiene ningún archivo .xml.");

            string xml;
            using (var entryStream = xmlEntry.Open())
            using (var entryReader = new StreamReader(entryStream, Encoding.UTF8))
                xml = entryReader.ReadToEnd();

            var pdfEntry = zip.Entries.FirstOrDefault(e => e.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));
            byte[]? pdf = null;
            string? pdfName = null;
            if (pdfEntry != null)
            {
                using var pdfStream = new MemoryStream();
                using var pdfEntryStream = pdfEntry.Open();
                pdfEntryStream.CopyTo(pdfStream);
                pdf = pdfStream.ToArray();
                pdfName = Path.GetFileName(pdfEntry.Name);
            }
            return (ReceivedDocumentXmlExtractor.Extract(xml), pdf, pdfName);
        }

        public static ReceivedDocument ToEntity(ParsedReceivedDocument parsed, Guid clientId, Guid branchId, string sourceType, string xml, byte[]? pdf, string? pdfFileName) =>
            new()
            {
                Id = Guid.NewGuid(),
                ClientId = clientId,
                BranchId = branchId,
                SourceType = sourceType,
                RawXml = xml,
                OriginalPdf = pdf,
                OriginalPdfFileName = pdfFileName,
                Cufe = parsed.Cufe,
                DocumentTypeCode = parsed.DocumentTypeCode,
                IssuerTaxId = parsed.IssuerTaxId,
                IssuerName = parsed.IssuerName,
                DocumentId = parsed.DocumentId,
                IssueDate = parsed.IssueDate,
                TotalAmount = parsed.TotalAmount,
                IsCredit = parsed.IsCredit,
                ReceivedAt = DateTime.UtcNow
            };
    }
}
