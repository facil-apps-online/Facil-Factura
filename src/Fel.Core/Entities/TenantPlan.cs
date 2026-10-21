using System;

namespace Fel.Core.Entities
{
    // Duración del ciclo de un plan, en meses. Los ciclos se cuentan desde el mes en que empieza la asignación (ver ClientPlanAssignment).
    public enum PlanCycle
    {
        Monthly = 0,
        Quarterly = 1,
        Annual = 2
    }

    // Cuándo se cobra la cuota fija: al empezar el ciclo (por adelantado) o al terminarlo (vencida). El excedente siempre se cobra al cierre.
    public enum PlanFeeTiming
    {
        Advance = 0,
        Arrears = 1
    }

    // Sobre qué se aplica el plan: toda la empresa, o cada sucursal activa con su propia cuota y sus propios documentos incluidos.
    public enum PlanScope
    {
        PerClient = 0,
        PerBranch = 1
    }

    // Estados de documento que cuentan para el consumo. Un borrador nunca cuenta.
    [Flags]
    public enum PlanCountedStatuses
    {
        Approved = 1,
        Rejected = 2,
        InProcess = 4
    }

    // Tipos de documento que cuentan para el consumo.
    [Flags]
    public enum PlanCountedTypes
    {
        Invoices = 1,           // TypeCode FE*
        Notes = 2,              // NC y ND
        SupportDocuments = 4,   // DS*
        Payroll = 8,            // NE*
        All = Invoices | Notes | SupportDocuments | Payroll
    }

    // Plan comercial que un Tenant ofrece a sus Clients (lo que el Tenant cobra, distinto de lo que la plataforma le cobra al Tenant). Se arma con
    // reglas combinables: cuota fija, documentos incluidos (o ilimitados), precio por documento (o por excedente) y qué cuenta como documento.
    public class TenantPlan
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }

        public PlanCycle Cycle { get; set; } = PlanCycle.Monthly;

        // Cuota fija por ciclo (por empresa o por sucursal activa, según Scope).
        public decimal FixedFee { get; set; }
        public PlanFeeTiming FeeTiming { get; set; } = PlanFeeTiming.Advance;

        // Documentos incluidos por ciclo. Nulo = ilimitados (solo se cobra la cuota). 0 = todo documento se cobra a PricePerDocument.
        public int? IncludedDocuments { get; set; }

        // Precio de cada documento que pasa de los incluidos (o de cada documento si no hay incluidos).
        public decimal PricePerDocument { get; set; }

        public PlanScope Scope { get; set; } = PlanScope.PerClient;
        public PlanCountedStatuses CountedStatuses { get; set; } = PlanCountedStatuses.Approved | PlanCountedStatuses.Rejected;
        public PlanCountedTypes CountedTypes { get; set; } = PlanCountedTypes.All;

        // Qué hacer al llegar al límite de documentos incluidos. Nulo = la opción general del tenant (TenantPlanSettings).
        public PlanExceedAction? ExceedAction { get; set; }

        // Archivar no borra: los clientes con historial siguen apuntando a su plan.
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    // Plan que tiene un Client desde una fecha. Un Client tiene un solo plan vigente a la vez; cambiarlo cierra el anterior (EndDate) y abre uno nuevo,
    // así queda el historial. Los ajustes puntuales (cuota o precio especiales) pisan los del plan solo para este cliente.
    public class ClientPlanAssignment
    {
        public Guid Id { get; set; }
        public Guid ClientId { get; set; }
        public Client? Client { get; set; }
        public Guid PlanId { get; set; }
        public TenantPlan? Plan { get; set; }

        // Primer día en que rige. Los ciclos de 3 y 12 meses se cuentan desde el mes de esta fecha.
        public DateTime StartDate { get; set; }
        // Último día en que rige; nulo = vigente.
        public DateTime? EndDate { get; set; }

        public decimal? FixedFeeOverride { get; set; }
        public decimal? PricePerDocumentOverride { get; set; }
        // Pisa la acción al llegar al límite solo para este cliente. Nulo = la del plan o la general.
        public PlanExceedAction? ExceedActionOverride { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
