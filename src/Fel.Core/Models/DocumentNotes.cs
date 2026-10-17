using System.Collections.Generic;
using System.Linq;
using Fel.Core.Entities;

namespace Fel.Core.Models
{
    // Observaciones de un documento que se envían y se imprimen. La factura de mandato lleva una lista (DocumentObservation, una por
    // fila y en orden); cualquier otra factura o nota lleva solo la observación general (Document.Notes). Un único lugar decide cuál
    // aplica, para que el XML de la DIAN, el envío a Dataico y el PDF digan siempre lo mismo.
    public static class DocumentNotes
    {
        public static IReadOnlyList<string> Texts(Document document)
        {
            var list = document.Observations
                .Where(o => !string.IsNullOrWhiteSpace(o.Text))
                .OrderBy(o => o.Position)
                .Select(o => o.Text.Trim())
                .ToList();
            if (list.Count > 0) return list;

            return string.IsNullOrWhiteSpace(document.Notes) ? new List<string>() : new List<string> { document.Notes.Trim() };
        }
    }
}
