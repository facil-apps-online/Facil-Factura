using System;

namespace Fel.Core.Entities
{
    // Cómo se trata a un cliente en la facturación automática de planes.
    public enum PlanInvoicingMode
    {
        // Sigue las preferencias del tenant: se le genera un borrador por ciclo.
        Default = 0,
        // No se le generan borradores (se factura a mano, o no se factura este plan).
        Skip = 1
    }

    // Facturación automática de los planes del tenant (opcional). El tenant elige cuál de sus clientes es su propia empresa emisora; cada ciclo el
    // sistema deja un borrador de factura por cliente en esa empresa, con la cuota y el excedente de su plan. Nunca emite: la emisión la hace una
    // persona desde la empresa emisora, con el botón o por lotes. Sirve igual con Dataico y con emisión directa a la DIAN, porque el borrador es un
    // documento normal y la emisión usa el integrador de la empresa emisora.
    public class TenantInvoicingSettings
    {
        public Guid TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        // Cliente del tenant que factura a los demás (su propia empresa). Nulo = sin configurar.
        public Guid? OwnerClientId { get; set; }

        // Apagado por defecto: el tenant decide si usa el servicio.
        public bool GenerateDrafts { get; set; }

        // Día del mes (1 a 28) desde el que se genera el borrador del ciclo que acaba de empezar.
        public int DraftDay { get; set; } = 1;

        // Productos del catálogo de la empresa emisora que dan el nombre, el IVA y la unidad de cada línea. El precio y la cantidad salen del plan.
        // Un plan puede traer los suyos (TenantPlan.FeeProductId y ExtraProductId); si no, se usan estos. El de excedente, si falta, usa el de cuota.
        public Guid? FeeProductId { get; set; }
        public Guid? ExtraProductId { get; set; }

        // Cuando el tenant crea un cliente nuevo, se crea su tercero en la empresa emisora al instante. Encendido por defecto.
        public bool AutoSyncCustomers { get; set; } = true;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    // Registro de cada cobro generado: un renglón por cliente y ciclo. Evita generar dos veces el mismo borrador y deja el historial aunque el
    // borrador se descarte (DocumentId deja de existir): un cobro descartado no se vuelve a generar solo.
    public class PlanCharge
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        // Cliente del tenant al que se cobra.
        public Guid ClientId { get; set; }
        public Client? Client { get; set; }
        public Guid AssignmentId { get; set; }
        public Guid PlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        // Primer día del ciclo en el que se generó (la clave de no repetir).
        public DateTime RunPeriod { get; set; }
        public decimal FeeAmount { get; set; }
        public decimal ExtraAmount { get; set; }
        // Texto de lo que cubre ("cuota de noviembre 2026; excedente de octubre 2026").
        public string Description { get; set; } = string.Empty;
        // Borrador en la empresa emisora. Nulo si el cobro quedó en cero.
        public Guid? DocumentId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
