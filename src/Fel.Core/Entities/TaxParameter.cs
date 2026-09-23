using System;

namespace Fel.Core.Entities
{
    // Parámetro tributario con vigencia (UVT, y a futuro otros como SMLV o auxilio de transporte
    // si se llegan a necesitar). Se versiona por fecha de inicio de vigencia en vez de por año
    // calendario, porque el gobierno puede ajustar estos valores en cualquier momento del año.
    public class TaxParameter
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty; // Ej: "UVT"
        public decimal Value { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
