using System.Collections.Generic;
using System.Threading.Tasks;
using Fel.Core.Entities;

namespace Fel.Core.Interfaces
{
    // Emisión del Documento Soporte (adquisiciones a no obligados a facturar) y de su Nota de Ajuste por el proveedor del emisor. Aparte de
    // IDocumentSubmissionProvider porque los roles van invertidos (el tercero del documento es el vendedor, el Client es el adquirente) y porque
    // la numeración, las fechas y la resolución ya las resolvió quien llama (SupportDocumentPublishingService).
    public interface ISupportDocumentSubmissionProvider
    {
        // Código del Integrator que esta implementación tramita (el mismo de IDocumentSubmissionProvider).
        string IntegratorCode { get; }

        // adjustedDocument = el documento soporte que ajusta esta nota (null cuando el documento es el soporte).
        Task<DocumentSubmissionResult> SubmitSupportAsync(
            Document document,
            Customer seller,
            ICollection<DocumentItem> items,
            Client client,
            Resolution resolution,
            Document? adjustedDocument);
    }
}
