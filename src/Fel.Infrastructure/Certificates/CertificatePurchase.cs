using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Certificates;

// Si un cliente puede comprar certificados digitales. El Tenant lo decide por cliente (Client.CertificatePurchaseEnabled); sin decisión,
// sí puede, salvo que el Tenant facture y el cliente use Dataico, que firma con su propio certificado.
public static class CertificatePurchase
{
    public const string DisabledMessage = "Este cliente no tiene habilitada la compra de certificados. Actívala en su ficha.";

    // Si el certificado emitido puede instalarse en la plataforma para firmar: solo un cliente que factura de forma nativa. Con Dataico firma
    // Dataico y un titular no factura: su certificado solo se entrega (descarga o correo).
    public static async Task<bool> CanInstallAsync(FelDbContext db, Client client, CancellationToken ct = default)
    {
        if (!client.InvoicingEnabled) return false;
        if (!(await TenantModules.GetAsync(db, client.TenantId)).Invoicing) return false;
        return !await db.Integrators.AsNoTracking().AnyAsync(i => i.Id == client.IntegratorId && i.Code == "DATAICO", ct);
    }

    public static async Task<bool> IsEnabledAsync(FelDbContext db, Client client, CancellationToken ct = default)
    {
        // Un titular (sin facturación) existe para comprar certificados.
        if (!client.InvoicingEnabled) return true;
        if (client.CertificatePurchaseEnabled.HasValue) return client.CertificatePurchaseEnabled.Value;
        var modules = await TenantModules.GetAsync(db, client.TenantId);
        if (!modules.Invoicing) return true;
        return !await db.Integrators.AsNoTracking().AnyAsync(i => i.Id == client.IntegratorId && i.Code == "DATAICO", ct);
    }
}
