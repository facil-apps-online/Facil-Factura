using System;

namespace Fel.Core.Entities
{
    // Qué hace el sistema cuando un cliente llega al límite de documentos incluidos en su plan. El excedente se cobra siempre al precio del plan
    // (un precio de 0 significa que no se cobra); esta acción solo decide si, además, se bloquea al cliente.
    public enum PlanExceedAction
    {
        Notify = 0,
        Block = 1
    }

    public enum PlanAlertKind
    {
        Warning = 0,       // llegó al porcentaje de aviso (80% por defecto)
        LimitReached = 1,  // llegó al 100% de los documentos incluidos
        Blocked = 2,       // el sistema bloqueó al cliente por llegar al límite
        Unblocked = 3      // el sistema levantó ese bloqueo (ciclo nuevo, cambio de plan o de acción)
    }

    // Preferencias de alertas y límites del tenant: nivel general, que un plan o un cliente pueden pisar. Sin fila se usan los valores por defecto.
    public class TenantPlanSettings
    {
        public Guid TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        // Avisos en el tablero del tenant (los bloqueos no dependen de este interruptor).
        public bool AlertsEnabled { get; set; } = true;
        // Porcentaje de lo incluido al que se avisa antes del límite.
        public int WarnPercent { get; set; } = 80;
        // Correo al tenant (Tenant.Email) cuando se genera un aviso.
        public bool EmailTenant { get; set; } = true;
        // Correo al cliente (Client.Email) cuando se genera un aviso.
        public bool EmailClient { get; set; } = false;
        public PlanExceedAction DefaultExceedAction { get; set; } = PlanExceedAction.Notify;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    // Aviso de consumo de un cliente en un ciclo. Hay uno por (asignación, ciclo, tipo, sucursal): no se repite aunque el proceso corra a menudo.
    public class PlanAlert
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid ClientId { get; set; }
        public Client? Client { get; set; }
        public Guid AssignmentId { get; set; }
        public DateTime PeriodStart { get; set; }
        public PlanAlertKind Kind { get; set; }
        public Guid? BranchId { get; set; }
        public int Documents { get; set; }
        public int Included { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        // Cuándo lo marcó como visto el tenant.
        public DateTime? SeenAt { get; set; }
    }
}
