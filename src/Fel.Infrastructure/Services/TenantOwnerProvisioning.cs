using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Services
{
    public class OwnerProvisioningResult
    {
        public Guid? OwnerClientId { get; set; }
        // true si se creó el cliente propietario en esta llamada.
        public bool Created { get; set; }
        // Campos del cliente propietario que se llenaron con el RUT (solo los que estaban vacíos).
        public List<string> FilledFields { get; set; } = new();
    }

    // Todo tenant arranca con su empresa propietaria: un cliente con el mismo NIT del tenant, que es la empresa que factura a sus clientes (facturación
    // automática de planes). Se crea con la misma provisión que un cliente cualquiera (sucursal principal, contadores de notas, integrador de emisión
    // directa y catálogos estándar) y queda como empresa emisora del tenant. El tenant la completa después: certificado, resolución, dirección. Idempotente:
    // si el tenant ya tiene empresa emisora, o ya existe un cliente con su NIT, no crea nada nuevo. Sin NIT no se puede crear.
    //
    // Con un RUT (TenantRutData) la empresa nace con lo fiscal lleno; y si ya existía, se le completa solo lo que le falta: nunca se pisa lo ya escrito.
    public static class TenantOwnerProvisioning
    {
        private static readonly Guid NativeIntegratorId = Guid.Parse("00000000-0000-0000-0000-000000000101");

        private static string Digits(string? value) => new string((value ?? string.Empty).Where(char.IsDigit).ToArray());

        // No guarda: quien llama hace SaveChanges.
        public static async Task<OwnerProvisioningResult> EnsureOwnerAsync(FelDbContext db, Tenant tenant, TenantRutData? rut = null, CancellationToken ct = default)
        {
            var result = new OwnerProvisioningResult();
            var nit = Digits(tenant.TaxId);
            if (nit.Length == 0) return result;

            var settings = await db.TenantInvoicingSettings.FirstOrDefaultAsync(s => s.TenantId == tenant.Id, ct);

            // Cliente propietario existente: el de la configuración, o un cliente suyo con el mismo NIT.
            Client? owner = null;
            if (settings?.OwnerClientId != null)
                owner = await db.Clients.FirstOrDefaultAsync(c => c.Id == settings.OwnerClientId && c.TenantId == tenant.Id, ct);
            if (owner == null)
            {
                var candidates = await db.Clients.Where(c => c.TenantId == tenant.Id).ToListAsync(ct);
                owner = candidates.FirstOrDefault(c => Digits(c.TaxId) == nit);
            }

            if (owner != null)
            {
                if (rut != null) FillClient(owner, rut, onlyEmpty: true, result.FilledFields);
            }
            else
            {
                var now = DateTime.UtcNow;
                var isPerson = !string.IsNullOrWhiteSpace(tenant.FirstName);
                owner = new Client
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
                    IntegratorId = await DefaultIntegrator.ForTenantAsync(db, tenant.Id),
                    IsGranContribuyente = tenant.IsGranContribuyente,
                    IsAgenteRetenedorIva = tenant.IsAgenteRetenedorIva,
                    IsAutorretenedorRenta = tenant.IsAutorretenedorRenta,
                    IncomeTaxRegime = tenant.IncomeTaxRegime
                };
                if (rut != null) FillClient(owner, rut, onlyEmpty: false, null);
                db.Clients.Add(owner);
                db.Branches.Add(BranchProvisioning.CreateMain(owner));
                db.NoteNumberings.AddRange(BranchProvisioning.CreateSharedNoteNumberings(owner.Id));
                db.ClientIntegratorAssignments.Add(new ClientIntegratorAssignment
                {
                    Id = Guid.NewGuid(), ClientId = owner.Id, IntegratorId = owner.IntegratorId, EffectiveFrom = now, EffectiveTo = null
                });
                foreach (var documentTypeId in DefaultCatalogSets.StandardDocumentTypeIds)
                    db.ClientEnabledDocumentTypes.Add(new ClientEnabledDocumentType { Id = Guid.NewGuid(), ClientId = owner.Id, DocumentTypeId = documentTypeId });
                foreach (var retentionConceptId in DefaultCatalogSets.StandardRetentionConceptIds)
                    db.ClientEnabledRetentionConcepts.Add(new ClientEnabledRetentionConcept { Id = Guid.NewGuid(), ClientId = owner.Id, RetentionConceptId = retentionConceptId });
                result.Created = true;
            }

            if (settings == null)
            {
                settings = new TenantInvoicingSettings { TenantId = tenant.Id };
                db.TenantInvoicingSettings.Add(settings);
            }
            if (settings.OwnerClientId != owner.Id)
            {
                settings.OwnerClientId = owner.Id;
                settings.UpdatedAt = DateTime.UtcNow;
            }
            result.OwnerClientId = owner.Id;
            return result;
        }

        // Lo fiscal del RUT sobre el cliente propietario. Con onlyEmpty solo llena lo que está vacío y las marcas que estaban apagadas (nunca apaga una);
        // sin onlyEmpty (cliente nuevo) pone todo lo que el RUT informa. "changes" recibe el nombre de cada campo llenado.
        private static void FillClient(Client c, TenantRutData rut, bool onlyEmpty, List<string>? changes)
        {
            void Str(string label, string? current, string? incoming, Action<string> set)
            {
                if (string.IsNullOrWhiteSpace(incoming)) return;
                if (onlyEmpty && !string.IsNullOrWhiteSpace(current)) return;
                if (string.Equals(current, incoming, StringComparison.Ordinal)) return;
                set(incoming!);
                changes?.Add(label);
            }
            void Flag(string label, bool current, bool incoming, Action<bool> set)
            {
                if (!incoming || current) return;
                set(true);
                changes?.Add(label);
            }

            // El tipo de persona solo se fija en un cliente nuevo: en uno existente no se cambia.
            if (!onlyEmpty && rut.PersonType is "PJ" or "PN") c.PersonType = rut.PersonType!;
            Str("Nombre comercial", c.CommercialName, rut.CommercialName, v => c.CommercialName = v);
            Str("Correo", c.Email, rut.Email, v => c.Email = v);
            Str("Teléfono", c.Phone, rut.Phone, v => c.Phone = v);
            Str("Dirección", c.Address, rut.Address, v => c.Address = v);
            Str("Ciudad", c.City, rut.City, v => c.City = v);
            Str("Código DANE del municipio", c.CityCode, rut.CityCode, v => c.CityCode = v);
            Str("Departamento", c.OrganizationDepartment, rut.Department, v => c.OrganizationDepartment = v);
            Str("Actividad económica", c.EconomicActivity, rut.EconomicActivity, v => c.EconomicActivity = v);
            Str("Régimen tributario", c.TaxRegime, rut.TaxRegimeCode, v => c.TaxRegime = v);
            Str("Régimen de renta", c.IncomeTaxRegime, rut.IncomeTaxRegime, v => c.IncomeTaxRegime = v);
            Flag("Gran contribuyente", c.IsGranContribuyente, rut.IsGranContribuyente, v => c.IsGranContribuyente = v);
            Flag("Agente retenedor de IVA", c.IsAgenteRetenedorIva, rut.IsAgenteRetenedorIva, v => c.IsAgenteRetenedorIva = v);
            Flag("Autorretenedor de renta", c.IsAutorretenedorRenta, rut.IsAutorretenedorRenta, v => c.IsAutorretenedorRenta = v);

            // Representante legal: se llena completo solo si el cliente no tiene ninguno todavía.
            if (onlyEmpty && !string.IsNullOrWhiteSpace(c.LegalRepresentativeDocumentNumber)) return;
            Str("Representante legal: nombre", c.LegalRepresentativeFirstName, rut.LegalRepresentativeFirstName, v => c.LegalRepresentativeFirstName = v);
            Str("Representante legal: otros nombres", c.LegalRepresentativeOtherNames, rut.LegalRepresentativeOtherNames, v => c.LegalRepresentativeOtherNames = v);
            Str("Representante legal: primer apellido", c.LegalRepresentativeFirstLastName, rut.LegalRepresentativeFirstLastName, v => c.LegalRepresentativeFirstLastName = v);
            Str("Representante legal: segundo apellido", c.LegalRepresentativeSecondLastName, rut.LegalRepresentativeSecondLastName, v => c.LegalRepresentativeSecondLastName = v);
            Str("Representante legal: tipo de documento", c.LegalRepresentativeDocumentType, rut.LegalRepresentativeDocumentType, v => c.LegalRepresentativeDocumentType = v);
            Str("Representante legal: documento", c.LegalRepresentativeDocumentNumber, rut.LegalRepresentativeDocumentNumber, v => c.LegalRepresentativeDocumentNumber = v);
            Str("Representante legal: calidad", c.LegalRepresentativeRepresentationCode, rut.LegalRepresentativeRepresentationCode, v => c.LegalRepresentativeRepresentationCode = v);
            if (!string.IsNullOrWhiteSpace(rut.LegalRepresentativeDocumentNumber) && string.IsNullOrWhiteSpace(c.LegalRepresentativeDocumentCountryCode))
                c.LegalRepresentativeDocumentCountryCode = "CO";
            if (rut.LegalRepresentativeStartDate != null && (!onlyEmpty || c.LegalRepresentativeStartDate == null))
            {
                c.LegalRepresentativeStartDate = rut.LegalRepresentativeStartDate;
                changes?.Add("Representante legal: fecha de inicio");
            }
        }

        private static string Truncate(string value, int max) => value.Length > max ? value[..max] : value;

        private static string FirstNonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
    }
}
