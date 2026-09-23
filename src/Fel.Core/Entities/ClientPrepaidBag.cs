using System;

namespace Fel.Core.Entities
{
    // Bolsa de dinero (un pago único) que un Client activó al comprar un ClientPrepaidPackage.
    // Mientras tenga saldo, sus documentos de ese integrador se facturan a
    // DiscountedPricePerDocument en vez de la tarifa estándar; al agotarse, vuelve la tarifa
    // estándar (ver BillingMetricsService). IntegratorId se copia del paquete al activar, igual que
    // DiscountedPricePerDocument, para no verse afectada por cambios futuros del catálogo.
    public class ClientPrepaidBag
    {
        public Guid Id { get; set; }
        public Guid ClientId { get; set; }
        public Client? Client { get; set; }

        public Guid PackageId { get; set; }
        public ClientPrepaidPackage? Package { get; set; }

        public Guid IntegratorId { get; set; }
        public Integrator? Integrator { get; set; }

        public decimal AmountPaid { get; set; }
        public decimal RemainingBalance { get; set; }
        public decimal DiscountedPricePerDocument { get; set; }
        public PrepaidBagStatus Status { get; set; } = PrepaidBagStatus.Active;
        public DateTime PurchasedAt { get; set; }
    }
}
