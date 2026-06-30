using System;

namespace Fel.Core.Entities
{
    public class Resolution
    {
        public Guid Id { get; set; }
        public Guid ClientId { get; set; }
        public Client Client { get; set; } = null!;
        
        public string ResolutionNumber { get; set; } = string.Empty;
        public string Prefix { get; set; } = string.Empty;
        public long NumberStart { get; set; }
        public long NumberEnd { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime ValidTo { get; set; }
        public string TechnicalKey { get; set; } = string.Empty; // Clave técnica DIAN
        
        // Tipo de documento (ej. FE, NC, ND, POS)
        public string DocumentType { get; set; } = string.Empty; 
        public bool IsActive { get; set; }
    }
}
