using System.Collections.Generic;
using System.Threading.Tasks;
using Fel.Core.Entities;

namespace Fel.Core.Interfaces
{
    // Resultado unificado de intentar emitir un documento, sin importar por cuál proveedor
    // (Dataico, DIAN directa, o el que se agregue mañana) se haya tramitado.
    public class DocumentSubmissionResult
    {
        public bool Success { get; set; }
        public string Status { get; set; } = "REJECTED"; // APPROVED, REJECTED, PROCESSING
        public string? Cufe { get; set; }
        public string? ResponseMessage { get; set; }
    }

    // Sabe cómo tramitar un documento ante la DIAN (directamente o vía un integrador) y devolver
    // el resultado en el formato común de arriba. InvoiceController resuelve cuál implementación
    // usar según Client.DocumentProvider sin conocer los detalles de cada integrador, así se agrega
    // uno nuevo sin tocar el controlador.
    public interface IDocumentSubmissionProvider
    {
        // Código del Integrator (catálogo Fel.Core/Entities/Integrator.cs) que esta implementación
        // tramita — InvoiceController resuelve cuál usar comparando contra Client.Integrator.Code.
        string IntegratorCode { get; }

        Task<DocumentSubmissionResult> SubmitAsync(
            Document invoice,
            Customer customer,
            ICollection<DocumentItem> items,
            Client client,
            Resolution resolution,
            string paymentMeans,
            string paymentMeansType);
    }
}
