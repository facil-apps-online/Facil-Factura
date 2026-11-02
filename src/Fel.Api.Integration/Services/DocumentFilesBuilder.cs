using System;
using System.Text;
using System.Threading.Tasks;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Dian;
using Fel.Infrastructure.Services;

namespace Fel.Api.Integration.Services
{
    // Los archivos de un documento emitido para entregarlos en la respuesta de la API: PDF, XML firmado, respuesta de la DIAN y contenedor AttachedDocument,
    // en Base64 real. Lo que aún no existe llega en null; si el PDF no se pudo generar, pdfError dice por qué.
    public static class DocumentFilesBuilder
    {
        public static async Task<object> BuildAsync(FelDbContext db, DocumentPdfService pdfService, Guid documentId, Guid clientId)
        {
            var stored = await DocumentSignedXmlStore.LoadAsync(db, documentId);
            var (pdf, pdfError) = await pdfService.RenderAsync(documentId, clientId);
            return new
            {
                pdfBase64 = pdf == null ? null : Convert.ToBase64String(pdf),
                pdfError,
                xmlBase64 = ToBase64(stored?.SignedXml),
                dianResponseBase64 = ToBase64(stored?.ApplicationResponse),
                attachedDocumentBase64 = ToBase64(stored?.AttachedDocument)
            };
        }

        public static object Links(Guid id) => new
        {
            self = $"/api/co/dian/documents/{id}",
            pdf = $"/api/co/dian/documents/{id}/pdf",
            xml = $"/api/co/dian/documents/{id}/xml",
            files = $"/api/co/dian/documents/{id}/files"
        };

        private static string? ToBase64(string? text) => string.IsNullOrEmpty(text) ? null : Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
    }
}
