using System;

namespace Fel.Core.Entities
{
    public class TariffTier
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty; // e.g. "Nivel 1"
        public int MinDocuments { get; set; }            // e.g. 1
        public int? MaxDocuments { get; set; }           // e.g. 2000 (null if it's the last tier)
        public decimal PricePerDocument { get; set; }    // e.g. 70
        public bool IsActive { get; set; } = true;
    }
}
