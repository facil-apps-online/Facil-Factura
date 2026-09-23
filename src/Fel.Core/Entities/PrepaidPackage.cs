using System;
using System.Collections.Generic;

namespace Fel.Core.Entities
{
    // Catálogo de paquetes prepago que Superadmin ofrece a un Tenant en modo PerUser.
    // Es un pago único (bolsa de dinero) que, mientras tenga saldo, deja la tarifa por
    // usuario en DiscountedPricePerUser (más barata que la tarifa estándar del tenant);
    // al agotarse el saldo, la tarifa vuelve a la estándar. No es por tiempo.
    public class PrepaidPackage
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        public string Name { get; set; } = string.Empty;
        public decimal TotalPrice { get; set; } // Lo que el tenant paga de una vez por la bolsa
        public decimal DiscountedPricePerUser { get; set; } // Tarifa por usuario mientras dure el saldo
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }

        public ICollection<TenantPrepaidBag> Bags { get; set; } = new List<TenantPrepaidBag>();
    }
}
