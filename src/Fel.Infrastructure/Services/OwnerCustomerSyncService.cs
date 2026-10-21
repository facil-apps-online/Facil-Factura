using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Services
{
    public class TenantClientSyncItem
    {
        public Guid ClientId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string TaxId { get; set; } = string.Empty;
        // ToCreate (no está como tercero), ToComplete (existe y se le puede llenar algo o vincular), UpToDate, Review (mismo NIT, otro nombre: no se toca).
        public string State { get; set; } = string.Empty;
        public Guid? CustomerId { get; set; }
        // Qué se llenaría (solo en ToComplete) y qué datos importantes quedarían vacíos tras sincronizar.
        public List<string> Fills { get; set; } = new();
        public List<string> Missing { get; set; } = new();
    }

    public class TenantClientSyncResult
    {
        public int Created { get; set; }
        public int Completed { get; set; }
    }

    // Sincroniza los clientes del tenant como terceros tipo Cliente de la empresa propietaria (la que factura a los demás). Cada tercero guarda a qué cliente
    // del tenant corresponde (Customer.LinkedClientId), así que no se duplica aunque cambie de nombre. Solo llena lo vacío: lo que la propietaria ya
    // corrigió a mano no se pisa, y un tercero con el mismo NIT pero otro nombre se deja para revisión. Lo usan la pantalla de Terceros, la creación
    // automática de clientes nuevos y la generación de borradores de cobro.
    public class OwnerCustomerSyncService
    {
        private readonly FelDbContext _db;

        public OwnerCustomerSyncService(FelDbContext db) => _db = db;

        private static string Digits(string? v) => new string((v ?? string.Empty).Where(char.IsDigit).ToArray());

        // Mayúsculas sin tildes ni signos, para comparar nombres.
        private static string Normalize(string? v)
        {
            var decomposed = (v ?? string.Empty).Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToUpperInvariant(c));
            }
            return sb.ToString();
        }

        private static bool SimilarNames(string? a, string? b)
        {
            var x = Normalize(a); var y = Normalize(b);
            return x.Length > 0 && y.Length > 0 && (x == y || x.Contains(y) || y.Contains(x));
        }

        private static string ClientName(Client c) => !string.IsNullOrEmpty(c.CompanyName) ? c.CompanyName : c.CommercialName;

        public static string? OwnerError(Client? owner) => owner == null ? "No se encontró la empresa propietaria." : null;

        // Clientes del tenant que se sincronizan: activos, no de prueba y distintos de la propia empresa propietaria.
        private IQueryable<Client> TenantClients(Guid tenantId, Guid ownerClientId) =>
            _db.Clients.AsNoTracking().Where(c => c.TenantId == tenantId && c.Id != ownerClientId && c.IsActive && !c.IsDeveloperSandbox);

        // Cliente nuevo → tercero. El nivel tributario y el régimen salen del cliente cuando los trae; si no, de "Valores por defecto" de la propietaria.
        public static Customer BuildCustomer(Guid ownerClientId, Client t, ClientInvoiceDefaults? defaults)
        {
            var isCompany = t.PersonType != "PN";
            var name = ClientName(t);
            var customer = new Customer
            {
                Id = Guid.NewGuid(), ClientId = ownerClientId, PartyType = PartyType.Cliente, LinkedClientId = t.Id, Name = name,
                IdentificationType = isCompany ? "31" : "13", IdentificationNumber = Digits(t.TaxId).Length > 0 ? Digits(t.TaxId) : t.TaxId,
                VerificationDigit = t.VerificationDigit ?? string.Empty, Email = t.Email ?? string.Empty, Phone = t.Phone ?? string.Empty,
                Address = t.Address ?? string.Empty, CountryCode = "CO", CityCode = t.CityCode ?? string.Empty, CityName = t.City ?? string.Empty,
                TaxRegime = t.TaxRegime ?? string.Empty,
                DataicoTaxLevelCode = t.TaxRegime == "48" ? "RESPONSABLE_DE_IVA" : t.TaxRegime == "49" ? "NO_RESPONSABLE_DE_IVA"
                    : defaults?.CustomerTaxLevelCode ?? "NO_RESPONSABLE_DE_IVA",
                DataicoRegimen = t.IncomeTaxRegime == "47" ? "SIMPLE" : defaults?.CustomerRegimen ?? "ORDINARIO",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            };
            if (!isCompany)
            {
                var (first, second, firstLast, secondLast) = ColombianNameSplitter.Split(name);
                customer.FirstName = first; customer.SecondName = second; customer.FirstLastName = firstLast; customer.SecondLastName = secondLast;
            }
            return customer;
        }

        // Llena lo vacío de un tercero existente con lo del cliente del tenant y lo vincula. Con apply=false solo calcula qué llenaría.
        private static List<string> FillCustomer(Customer c, Client t, ClientInvoiceDefaults? defaults, bool apply)
        {
            var fills = new List<string>();
            var source = BuildCustomer(c.ClientId, t, defaults);

            void Str(string label, string current, string incoming, Action<string> set)
            {
                if (string.IsNullOrWhiteSpace(incoming) || !string.IsNullOrWhiteSpace(current)) return;
                if (apply) set(incoming);
                fills.Add(label);
            }
            Str("Correo", c.Email, source.Email, v => c.Email = v);
            Str("Teléfono", c.Phone, source.Phone, v => c.Phone = v);
            Str("Dirección", c.Address, source.Address, v => c.Address = v);
            Str("Código DANE", c.CityCode, source.CityCode, v => c.CityCode = v);
            Str("Ciudad", c.CityName, source.CityName, v => c.CityName = v);
            Str("Dígito de verificación", c.VerificationDigit, source.VerificationDigit, v => c.VerificationDigit = v);
            Str("Régimen tributario", c.TaxRegime, source.TaxRegime, v => c.TaxRegime = v);
            Str("Nivel tributario", c.DataicoTaxLevelCode, source.DataicoTaxLevelCode, v => c.DataicoTaxLevelCode = v);
            Str("Régimen", c.DataicoRegimen, source.DataicoRegimen, v => c.DataicoRegimen = v);

            if (c.LinkedClientId != t.Id)
            {
                if (apply) c.LinkedClientId = t.Id;
                fills.Add("Vínculo con el cliente del tenant");
            }
            if (apply && fills.Count > 0) c.UpdatedAt = DateTime.UtcNow;
            return fills;
        }

        // Datos que, aun después de sincronizar, el tercero no tendría y que luego dan problemas al emitir.
        private static List<string> MissingFields(Customer c)
        {
            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(c.Email)) missing.Add("Correo");
            if (string.IsNullOrWhiteSpace(c.Address)) missing.Add("Dirección");
            if (c.CountryCode == "CO" && string.IsNullOrWhiteSpace(c.CityCode)) missing.Add("Ciudad (código DANE)");
            return missing;
        }

        private sealed record Match(Customer? Existing, bool Review);

        private static Match FindMatch(List<Customer> customers, Client t)
        {
            var byLink = customers.FirstOrDefault(c => c.LinkedClientId == t.Id);
            if (byLink != null) return new Match(byLink, false);
            var nit = Digits(t.TaxId);
            var byNit = nit.Length == 0 ? null
                : customers.FirstOrDefault(c => c.PartyType == PartyType.Cliente && c.LinkedClientId == null && Digits(c.IdentificationNumber) == nit);
            if (byNit == null) return new Match(null, false);
            return SimilarNames(byNit.Name, ClientName(t)) ? new Match(byNit, false) : new Match(byNit, true);
        }

        private async Task<(Client Owner, ClientInvoiceDefaults? Defaults, List<Customer> Customers)?> LoadAsync(Guid ownerClientId, CancellationToken ct)
        {
            var owner = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == ownerClientId, ct);
            if (owner == null) return null;
            var defaults = await _db.ClientInvoiceDefaults.AsNoTracking().FirstOrDefaultAsync(d => d.ClientId == ownerClientId, ct);
            var customers = await _db.Customers.Where(c => c.ClientId == ownerClientId && c.PartyType == PartyType.Cliente).ToListAsync(ct);
            return (owner, defaults, customers);
        }

        // Qué haría la sincronización con cada cliente del tenant, sin guardar nada.
        public async Task<List<TenantClientSyncItem>> PreviewAsync(Guid ownerClientId, CancellationToken ct = default)
        {
            var loaded = await LoadAsync(ownerClientId, ct);
            if (loaded == null) return new List<TenantClientSyncItem>();
            var (owner, defaults, customers) = loaded.Value;

            var clients = await TenantClients(owner.TenantId, ownerClientId).OrderBy(c => c.CompanyName).ToListAsync(ct);
            var items = new List<TenantClientSyncItem>();
            foreach (var t in clients)
            {
                var item = new TenantClientSyncItem { ClientId = t.Id, Name = ClientName(t), TaxId = t.TaxId };
                var match = FindMatch(customers, t);
                if (match.Existing == null)
                {
                    item.State = "ToCreate";
                    item.Missing = MissingFields(BuildCustomer(ownerClientId, t, defaults));
                }
                else if (match.Review)
                {
                    item.State = "Review";
                    item.CustomerId = match.Existing.Id;
                }
                else
                {
                    item.CustomerId = match.Existing.Id;
                    item.Fills = FillCustomer(match.Existing, t, defaults, apply: false);
                    item.State = item.Fills.Count > 0 ? "ToComplete" : "UpToDate";
                    // Lo que le seguiría faltando una vez completado.
                    var projected = BuildCustomer(ownerClientId, t, defaults);
                    projected.Email = string.IsNullOrWhiteSpace(match.Existing.Email) ? projected.Email : match.Existing.Email;
                    projected.Address = string.IsNullOrWhiteSpace(match.Existing.Address) ? projected.Address : match.Existing.Address;
                    projected.CityCode = string.IsNullOrWhiteSpace(match.Existing.CityCode) ? projected.CityCode : match.Existing.CityCode;
                    projected.CountryCode = match.Existing.CountryCode;
                    item.Missing = MissingFields(projected);
                }
                items.Add(item);
            }
            return items;
        }

        // Crea y completa los terceros de los clientes indicados (o de todos si no se indica ninguno). Los marcados para revisión no se tocan. Guarda al final.
        public async Task<TenantClientSyncResult> ApplyAsync(Guid ownerClientId, IReadOnlyCollection<Guid>? clientIds, CancellationToken ct = default)
        {
            var result = new TenantClientSyncResult();
            var loaded = await LoadAsync(ownerClientId, ct);
            if (loaded == null) return result;
            var (owner, defaults, customers) = loaded.Value;

            var query = TenantClients(owner.TenantId, ownerClientId);
            if (clientIds is { Count: > 0 }) query = query.Where(c => clientIds.Contains(c.Id));
            var clients = await query.ToListAsync(ct);

            foreach (var t in clients)
            {
                var match = FindMatch(customers, t);
                if (match.Review) continue;
                if (match.Existing == null)
                {
                    var customer = BuildCustomer(ownerClientId, t, defaults);
                    _db.Customers.Add(customer);
                    customers.Add(customer);
                    result.Created++;
                }
                else if (FillCustomer(match.Existing, t, defaults, apply: true).Count > 0)
                {
                    result.Completed++;
                }
            }
            await _db.SaveChangesAsync(ct);
            return result;
        }

        // El tercero de un cliente del tenant, creándolo (o vinculándolo si ya existe con su NIT) cuando falta. Lo usa la generación de borradores.
        public async Task<Customer> GetOrCreateAsync(Guid ownerClientId, Client tenantClient, ClientInvoiceDefaults? defaults, CancellationToken ct = default)
        {
            var customers = await _db.Customers.Where(c => c.ClientId == ownerClientId && c.PartyType == PartyType.Cliente).ToListAsync(ct);
            var match = FindMatch(customers, tenantClient);
            if (match.Existing != null)
            {
                if (!match.Review && match.Existing.LinkedClientId == null)
                {
                    match.Existing.LinkedClientId = tenantClient.Id;
                    await _db.SaveChangesAsync(ct);
                }
                return match.Existing;
            }
            var customer = BuildCustomer(ownerClientId, tenantClient, defaults);
            _db.Customers.Add(customer);
            await _db.SaveChangesAsync(ct);
            return customer;
        }

        // Cliente nuevo del tenant: si el tenant tiene empresa propietaria y la sincronización automática está encendida, se crea su tercero al instante.
        public async Task EnsureForNewClientAsync(Guid tenantId, Guid tenantClientId, CancellationToken ct = default)
        {
            var settings = await _db.TenantInvoicingSettings.AsNoTracking().FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);
            if (settings?.OwnerClientId == null || !settings.AutoSyncCustomers || settings.OwnerClientId == tenantClientId) return;
            await ApplyAsync(settings.OwnerClientId.Value, new[] { tenantClientId }, ct);
        }
    }
}
