using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Services
{
    // Todo tenant arranca con su empresa propietaria: un cliente con el mismo NIT del tenant, que es la empresa que factura a sus clientes (facturación
    // automática de planes). Se crea con la misma provisión que un cliente cualquiera (sucursal principal, contadores de notas, integrador de emisión
    // directa y catálogos estándar) y queda como empresa emisora del tenant. El tenant la completa después: certificado, resolución, dirección. Idempotente:
    // si el tenant ya tiene empresa emisora, o ya existe un cliente con su NIT, no crea nada nuevo. Sin NIT no se puede crear.
    public static class TenantOwnerProvisioning
    {
        private static readonly Guid NativeIntegratorId = Guid.Parse("00000000-0000-0000-0000-000000000101");

        private static string Digits(string? value) => new string((value ?? string.Empty).Where(char.IsDigit).ToArray());

        // Devuelve el id de la empresa propietaria (nueva o existente), o null si el tenant no tiene NIT. No guarda: quien llama hace SaveChanges.
        public static async Task<Guid?> EnsureOwnerAsync(FelDbContext db, Tenant tenant, CancellationToken ct = default)
        {
            var nit = Digits(tenant.TaxId);
            if (nit.Length == 0) return null;

            var settings = await db.TenantInvoicingSettings.FirstOrDefaultAsync(s => s.TenantId == tenant.Id, ct);
            if (settings?.OwnerClientId != null) return settings.OwnerClientId;

            // Un cliente suyo con el mismo NIT ya es su empresa: se enlaza, no se duplica.
            var clients = await db.Clients.Where(c => c.TenantId == tenant.Id).Select(c => new { c.Id, c.TaxId }).ToListAsync(ct);
            var existing = clients.FirstOrDefault(c => Digits(c.TaxId) == nit);

            Guid ownerId;
            if (existing != null)
            {
                ownerId = existing.Id;
            }
            else
            {
                var now = DateTime.UtcNow;
                var isPerson = !string.IsNullOrWhiteSpace(tenant.FirstName);
                var client = new Client
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    CompanyName = Truncate(!string.IsNullOrWhiteSpace(tenant.LegalName) ? tenant.LegalName! : tenant.Name, 150),
                    CommercialName = !string.IsNullOrWhiteSpace(tenant.CommercialName) ? tenant.CommercialName : tenant.Name,
                    PersonType = isPerson ? "PN" : "PJ",
                    TaxId = tenant.TaxId,
                    VerificationDigit = tenant.VerificationDigit ?? string.Empty,
                    Email = FirstNonEmpty(tenant.EinvoicingEmail, tenant.ContactEmail, tenant.Email),
                    Phone = FirstNonEmpty(tenant.ContactPhone, tenant.Phone),
                    Address = FirstNonEmpty(tenant.PhysicalAddressLine1, tenant.Address),
                    City = FirstNonEmpty(tenant.PhysicalCity, tenant.City),
                    IsActive = true,
                    CreatedAt = now,
                    IntegratorId = NativeIntegratorId
                };
                db.Clients.Add(client);
                db.Branches.Add(BranchProvisioning.CreateMain(client));
                db.NoteNumberings.AddRange(BranchProvisioning.CreateSharedNoteNumberings(client.Id));
                db.ClientIntegratorAssignments.Add(new ClientIntegratorAssignment
                {
                    Id = Guid.NewGuid(), ClientId = client.Id, IntegratorId = client.IntegratorId, EffectiveFrom = now, EffectiveTo = null
                });
                foreach (var documentTypeId in DefaultCatalogSets.StandardDocumentTypeIds)
                    db.ClientEnabledDocumentTypes.Add(new ClientEnabledDocumentType { Id = Guid.NewGuid(), ClientId = client.Id, DocumentTypeId = documentTypeId });
                foreach (var retentionConceptId in DefaultCatalogSets.StandardRetentionConceptIds)
                    db.ClientEnabledRetentionConcepts.Add(new ClientEnabledRetentionConcept { Id = Guid.NewGuid(), ClientId = client.Id, RetentionConceptId = retentionConceptId });
                ownerId = client.Id;
            }

            if (settings == null)
            {
                settings = new TenantInvoicingSettings { TenantId = tenant.Id };
                db.TenantInvoicingSettings.Add(settings);
            }
            settings.OwnerClientId = ownerId;
            settings.UpdatedAt = DateTime.UtcNow;
            return ownerId;
        }

        private static string Truncate(string value, int max) => value.Length > max ? value[..max] : value;

        private static string FirstNonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
    }
}
