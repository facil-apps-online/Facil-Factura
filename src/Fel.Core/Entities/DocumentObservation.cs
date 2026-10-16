using System;

namespace Fel.Core.Entities
{
    // Una observación del documento (facturas y sus notas crédito/débito). Cada documento tiene las suyas en orden
    // consecutivo (1, 2, 3...): ese orden es el que se envía a Dataico y a la DIAN y el que se imprime.
    public class DocumentObservation
    {
        public const int MaxTextLength = 500;

        public Guid Id { get; set; }

        public Guid DocumentId { get; set; }
        public Document? Document { get; set; }

        // Consecutivo dentro del documento, desde 1.
        public int Position { get; set; }

        public string Text { get; set; } = string.Empty;
    }
}
