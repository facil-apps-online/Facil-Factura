using System;

namespace Fel.Core.Entities
{
    // Retenciones definidas una sola vez para todo el documento (ReteICA, ReteIVA, o cualquier otra
    // que no varíe por línea) en vez de por ítem — se administran como una lista libre (agregar/
    // quitar), igual que las retenciones por ítem, para que la interacción sea uniforme en toda la
    // factura. Al emitir, se prorratean entre los ítems porque Dataico exige las retenciones por
    // ítem en su API (ver DataicoDocumentMapper.BuildGeneralRetentionsPerItem).
    public class DocumentGeneralRetention
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }
        public Document? Document { get; set; }
        public string TaxCategory { get; set; } = string.Empty;
        public decimal Rate { get; set; }
    }
}
