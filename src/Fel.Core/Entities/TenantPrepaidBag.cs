using System;

namespace Fel.Core.Entities
{
    public enum PrepaidBagStatus
    {
        Active = 0,
        Depleted = 1,
        Cancelled = 2
    }

    // Bolsa de dinero (un pago único) que un Tenant hizo al comprar un PrepaidPackage.
    // Mientras tenga saldo, el uso mensual de sus clientes se factura a DiscountedPricePerUser
    // en vez de la tarifa estándar del tenant, descontando ese saldo (ver
    // BillingMetricsService.GetTenantPerUserMetricsAsync); al agotarse, vuelve la tarifa
    // estándar. No está atada a ningún Client ni a un rango de fechas fijo.
    public class TenantPrepaidBag
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        public Guid PackageId { get; set; }
        public PrepaidPackage? Package { get; set; }

        public decimal AmountPaid { get; set; }
        public decimal RemainingBalance { get; set; }
        public decimal DiscountedPricePerUser { get; set; } // Copiado del paquete al comprar, para no verse afectado por cambios futuros del catálogo
        public PrepaidBagStatus Status { get; set; } = PrepaidBagStatus.Active;
        public DateTime PurchasedAt { get; set; }
    }
}
