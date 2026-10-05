using System;

namespace Fel.Core.Entities
{
    /// <summary>
    /// Leyenda específica para un tipo de documento y prefijo. No se enlaza a una resolución
    /// concreta: una nueva resolución con el mismo prefijo conserva la configuración.
    /// </summary>
    public class DocumentLegendByPrefix
    {
        public Guid Id { get; set; }
        public Guid ClientId { get; set; }
        public Client Client { get; set; } = null!;
        public string DocumentType { get; set; } = string.Empty;
        public string Prefix { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
