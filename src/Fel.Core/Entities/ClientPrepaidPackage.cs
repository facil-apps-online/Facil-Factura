using System;
using System.Collections.Generic;

namespace Fel.Core.Entities
{
    // Catálogo de paquetes prepago que un Tenant ofrece a uno de sus Clients — el mismo mecanismo
    // que PrepaidPackage/TenantPrepaidBag (Superadmin hacia Tenant), aplicado un nivel más abajo.
    // Escogido en vez de un sistema independiente porque el algoritmo de consumo
    // (BillingMetricsService.ComputeBagConsumption) ya es genérico: no le importa si la unidad es
    // un usuario-mes o un documento, solo que hay dinero prepago que se descuenta a una tarifa
    // preferencial mientras dure.
    //
    // Queda atado a un Integrator porque la tarifa que descuenta (DiscountedPricePerDocument) es
    // específica de ese integrador — el mismo Client puede tener paquetes distintos para Dataico y
    // para DIAN directa si algún día factura por ambos en paralelo.
    public class ClientPrepaidPackage
    {
        public Guid Id { get; set; }
        public Guid ClientId { get; set; }
        public Client? Client { get; set; }

        public Guid IntegratorId { get; set; }
        public Integrator? Integrator { get; set; }

        public string Name { get; set; } = string.Empty;
        public decimal TotalPrice { get; set; } // Lo que el Client paga de una vez por el paquete
        public decimal DiscountedPricePerDocument { get; set; } // Tarifa por documento mientras dure el saldo
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }

        public ICollection<ClientPrepaidBag> Bags { get; set; } = new List<ClientPrepaidBag>();
    }
}
