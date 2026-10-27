using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Dataico;

// Lo que se creó (o ya existía) en el sistema a partir de una factura consultada en Dataico.
public sealed record MaterializedInvoice(
    Guid? CustomerId, bool CustomerCreated, IReadOnlyList<Guid?> ProductIds, IReadOnlyList<string> CreatedProducts, IReadOnlyList<string> Notes);

// Una nota crédito o débito sobre una factura que solo existe en Dataico necesita el tercero y los productos de esa factura. Si el cliente todavía
// no los tiene, se crean con lo que trae la factura (sin inventar datos fiscales): el tercero con su identificación, nombre, contacto, dirección y
// responsabilidades; cada producto con su código, nombre, precio, unidad e IVA. Lo que no se puede crear con certeza se deja y se avisa.
public static class DataicoExternalInvoiceMaterializer
{
    private static string Digits(string? value) => new string((value ?? string.Empty).Where(char.IsDigit).ToArray());

    public static async Task<MaterializedInvoice> EnsureAsync(FelDbContext db, Guid clientId, DataicoExternalInvoiceDraft draft)
    {
        var notes = new List<string>();
        var (customerId, customerCreated) = await EnsureCustomerAsync(db, clientId, draft, notes);
        var (productIds, created) = await EnsureProductsAsync(db, clientId, draft, notes);
        await db.SaveChangesAsync();
        return new MaterializedInvoice(customerId, customerCreated, productIds, created, notes);
    }

    private static async Task<(Guid? Id, bool Created)> EnsureCustomerAsync(FelDbContext db, Guid clientId, DataicoExternalInvoiceDraft draft, List<string> notes)
    {
        var digits = Digits(draft.CustomerTaxId);
        if (digits.Length == 0)
        {
            notes.Add("La factura no trae la identificación del adquirente: selecciónalo en el formulario.");
            return (null, false);
        }

        var existing = await db.Customers
            .Where(c => c.ClientId == clientId && (c.IdentificationNumber == digits || c.IdentificationNumber == draft.CustomerTaxId))
            .OrderBy(c => c.PartyType == PartyType.Cliente ? 0 : 1)
            .Select(c => (Guid?)c.Id).FirstOrDefaultAsync();
        if (existing != null) return (existing, false);

        var name = draft.CustomerName.Trim();
        if (name.Length == 0)
        {
            notes.Add("La factura no trae el nombre del adquirente, así que no se pudo crear: selecciónalo en el formulario.");
            return (null, false);
        }
        var idType = draft.CustomerIdentificationType.Trim();
        if (idType.Length == 0 || !await db.IdentificationTypes.AnyAsync(t => t.Code == idType && t.IsActive))
        {
            notes.Add($"El tipo de documento del adquirente ({(idType.Length == 0 ? "sin dato" : idType)}) no está en el catálogo, así que no se creó: selecciónalo o créalo en Terceros.");
            return (null, false);
        }

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Name = name,
            IdentificationType = idType,
            IdentificationNumber = digits,
            VerificationDigit = draft.CustomerVerificationDigit,
            PartyType = PartyType.Cliente,
            Email = draft.CustomerEmail,
            Phone = draft.CustomerPhone,
            CountryCode = "CO",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Dirección: solo con ciudad válida (código DANE de 5 dígitos en Colombia; ciudad y país del catálogo fuera); a medias no se guarda.
        var country = string.IsNullOrWhiteSpace(draft.CustomerCountryCode) ? "CO" : draft.CustomerCountryCode.Trim().ToUpperInvariant();
        if (country == "CO")
        {
            if (draft.CustomerCityCode.Trim().Length >= 5 && draft.CustomerCityCode.Trim().All(char.IsDigit))
            {
                customer.Address = draft.CustomerAddress;
                customer.CityCode = draft.CustomerCityCode.Trim();
                customer.CityName = draft.CustomerCityName;
            }
            else if (!string.IsNullOrWhiteSpace(draft.CustomerAddress))
                notes.Add("La factura trae la dirección del adquirente sin un código de ciudad válido, así que el tercero se creó sin dirección: puedes completarla en Terceros.");
        }
        else if (!string.IsNullOrWhiteSpace(draft.CustomerCityName) && await db.TaxCatalogItems.AnyAsync(i => i.Kind == TaxCatalogKind.Country && i.Category == country && i.IsActive))
        {
            customer.CountryCode = country;
            customer.Address = draft.CustomerAddress;
            customer.CityName = draft.CustomerCityName;
            customer.StateName = string.IsNullOrWhiteSpace(draft.CustomerStateName) ? null : draft.CustomerStateName;
        }

        // Responsabilidades fiscales como las trae la factura (lista 04 de la DIAN): O-47 simple, O-13 gran contribuyente, O-15 autorretenedor, O-23 agente de IVA.
        var codes = draft.CustomerTaxLevelCodes;
        var responsableIva = draft.CustomerTaxSchemeId == "01";
        customer.DataicoTaxLevelCode = responsableIva ? "RESPONSABLE_DE_IVA" : "NO_RESPONSABLE_DE_IVA";
        customer.TaxRegime = responsableIva ? "48" : "49";
        customer.FiscalResponsibilities = codes.Count > 0 ? string.Join(";", codes) : "R-99-PN";
        customer.IsGranContribuyente = codes.Contains("O-13");
        customer.IsAutorretenedorRenta = codes.Contains("O-15");
        customer.IsAgenteRetenedorIva = codes.Contains("O-23");
        var simple = codes.Contains("O-47");
        customer.DataicoRegimen = simple ? "SIMPLE" : "ORDINARIO";
        if (simple) customer.IncomeTaxRegime = "47";

        db.Customers.Add(customer);
        notes.Add("El adquirente no estaba en tus terceros: se creó con los datos de la factura. Completa nombres y apellidos si es persona natural.");
        return (customer.Id, true);
    }

    private static async Task<(IReadOnlyList<Guid?> Ids, IReadOnlyList<string> Created)> EnsureProductsAsync(FelDbContext db, Guid clientId, DataicoExternalInvoiceDraft draft, List<string> notes)
    {
        var codes = draft.Items.Select(i => i.Code.Trim()).Where(c => c.Length > 0).Distinct().ToList();
        var byCode = (await db.Products.Where(p => p.ClientId == clientId && p.Scope == ProductScope.Invoice && codes.Contains(p.Code)).Select(p => new { p.Code, p.Id }).ToListAsync())
            .GroupBy(p => p.Code).ToDictionary(g => g.Key, g => g.First().Id);

        var units = await db.UnitsOfMeasure.AsNoTracking().Where(u => u.IsActive).Select(u => new { u.Id, u.DianCode }).ToListAsync();
        var ids = new List<Guid?>();
        var created = new List<string>();
        var withoutCode = 0;

        foreach (var line in draft.Items)
        {
            var code = line.Code.Trim();
            if (code.Length == 0) { ids.Add(null); withoutCode++; continue; }
            if (byCode.TryGetValue(code, out var existing)) { ids.Add(existing); continue; }

            var unit = units.FirstOrDefault(u => u.DianCode == line.UnitOfMeasureCode);
            var treatment = line.IvaTreatment switch { "Exento" => IvaTreatment.Exento, "Excluido" => IvaTreatment.Excluido, _ => IvaTreatment.Gravado };
            var product = new Product
            {
                Id = Guid.NewGuid(),
                ClientId = clientId,
                Scope = ProductScope.Invoice,
                Code = code,
                Name = string.IsNullOrWhiteSpace(line.Name) ? code : line.Name.Trim(),
                UnitPrice = line.UnitPrice,
                UnitOfMeasureId = unit?.Id ?? UnitOfMeasure.DefaultUnidadId,
                IvaTreatment = treatment,
                IvaRate = treatment == IvaTreatment.Gravado ? line.TaxRate : 0,
                TaxesPending = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Products.Add(product);
            byCode[code] = product.Id;
            ids.Add(product.Id);
            created.Add($"{code} · {product.Name}");
        }

        if (created.Count > 0) notes.Add($"Se crearon {created.Count} producto{(created.Count == 1 ? "" : "s")} con los datos de la factura (código, nombre, precio e IVA). Revísalos en Productos.");
        if (withoutCode > 0) notes.Add($"{withoutCode} línea{(withoutCode == 1 ? "" : "s")} de la factura no traen código de producto, así que no se crearon como producto.");
        return (ids, created);
    }
}
