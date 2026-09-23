using System;

namespace Fel.Core.Entities
{
    // Historial de qué Integrator tuvo asignado un Client y durante qué rango de fechas. Necesario
    // porque Client.IntegratorId solo guarda el valor actual — si el Client cambia de integrador a
    // mitad de mes, la facturación por usuario (que cobra por fracción de días activos, no por
    // documento) necesita saber en qué tramo de fechas estuvo en cada uno para prorratear el mes
    // entre ambos. EffectiveTo = null significa que es el tramo vigente.
    public class ClientIntegratorAssignment
    {
        public Guid Id { get; set; }
        public Guid ClientId { get; set; }
        public Client? Client { get; set; }

        public Guid IntegratorId { get; set; }
        public Integrator? Integrator { get; set; }

        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
    }
}
