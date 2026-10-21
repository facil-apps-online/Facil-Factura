using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Fel.Core.Entities;
using Fel.Infrastructure.Dian;

namespace Fel.Api.Client.Imports
{
    public sealed record AddressDefaults(string? Address, string? CityCode, string? CityName, string? PostalCode);

    public sealed class CustomerImportContext
    {
        public Guid ClientId { get; init; }
        public IReadOnlyList<DianMunicipality> Municipalities { get; init; } = Array.Empty<DianMunicipality>();
        // Tipos de documento activos: código DIAN -> nombre.
        public IReadOnlyDictionary<string, string> IdentificationTypes { get; init; } = new Dictionary<string, string>();
        // Terceros que ya existen: "{rol}|{número}".
        public ISet<string> ExistingKeys { get; init; } = new HashSet<string>();
        public AddressDefaults ClientAddress { get; init; } = new(null, null, null, null);
        public AddressDefaults DefaultAddress { get; init; } = new(null, null, null, null);
        public BulkImportOptions Options { get; init; } = new();
    }

    public sealed class CustomerImportResult
    {
        public ImportPreview Preview { get; } = new();
        public List<Customer> Customers { get; } = new();
    }

    // Carga masiva de terceros. Las columnas son las del Excel que usan los clientes (TIPO, IDENTIFICACION, DIGITO_VERIFICACION, TIPO_IDENTIFICACION, NOMBRE,
    // OTROS_NOMBRES, APELLIDO, SEGUNDO_APELLIDO, RAZON_SOCIAL, RESPONSABLE_IVA, REGIMEN, CORREO, TELEFONO, DEPARTAMENTO, CIUDAD, DIRECCION, CATEGORIA) y se reconocen
    // por nombre. Es lógica pura (sin base de datos): el controlador le entrega los catálogos y los terceros que ya existen, y le devuelve la vista previa y los
    // terceros por crear; la misma función sirve para la vista previa y para confirmar.
    public static class CustomerImporter
    {
        public static readonly IReadOnlyDictionary<string, string[]> Aliases = new Dictionary<string, string[]>
        {
            ["TIPO"] = new[] { "TIPO_PERSONA" },
            ["IDENTIFICACION"] = new[] { "NUMERO_IDENTIFICACION", "NIT", "DOCUMENTO", "NUMERO_DOCUMENTO", "CEDULA" },
            ["DIGITO_VERIFICACION"] = new[] { "DV", "DIGITO" },
            ["TIPO_IDENTIFICACION"] = new[] { "TIPO_DOCUMENTO", "TIPO_ID" },
            ["NOMBRE"] = new[] { "PRIMER_NOMBRE", "NOMBRES" },
            ["OTROS_NOMBRES"] = new[] { "SEGUNDO_NOMBRE" },
            ["APELLIDO"] = new[] { "PRIMER_APELLIDO", "APELLIDOS" },
            ["SEGUNDO_APELLIDO"] = Array.Empty<string>(),
            ["RAZON_SOCIAL"] = new[] { "NOMBRE_COMPLETO", "EMPRESA" },
            ["RESPONSABLE_IVA"] = new[] { "RESPONSABLE_DE_IVA" },
            ["REGIMEN"] = Array.Empty<string>(),
            ["CORREO"] = new[] { "EMAIL", "CORREO_ELECTRONICO" },
            ["TELEFONO"] = new[] { "CELULAR", "TEL" },
            ["DEPARTAMENTO"] = Array.Empty<string>(),
            ["CIUDAD"] = new[] { "MUNICIPIO" },
            ["DIRECCION"] = Array.Empty<string>(),
            ["CATEGORIA"] = new[] { "ROL", "TIPO_TERCERO" }
        };

        public static readonly string[] Required = { "IDENTIFICACION", "TIPO_IDENTIFICACION" };

        // Valores con que suele venir el tipo de documento -> código DIAN.
        private static readonly Dictionary<string, string> IdTypeByText = new()
        {
            ["CC"] = "13", ["CEDULA"] = "13", ["CEDULA DE CIUDADANIA"] = "13",
            ["NIT"] = "31",
            ["PASAPORTE"] = "41",
            ["CE"] = "22", ["CEDULA DE EXTRANJERIA"] = "22",
            ["TI"] = "12", ["TARJETA DE IDENTIDAD"] = "12",
            ["RC"] = "11", ["REGISTRO CIVIL"] = "11",
            ["NUIP"] = "91",
            ["TE"] = "21", ["TARJETA DE EXTRANJERIA"] = "21",
            // PEP es el Permiso Especial de Permanencia (tabla 13.2.1 de la DIAN), no la "persona expuesta políticamente".
            ["PEP"] = "47", ["PERMISO ESPECIAL DE PERMANENCIA"] = "47",
            ["PPT"] = "48", ["PERMISO POR PROTECCION TEMPORAL"] = "48",
            ["IE"] = "42", ["DOCUMENTO DE IDENTIFICACION EXTRANJERO"] = "42",
            ["NIT OTRO PAIS"] = "50", ["NIT DE OTRO PAIS"] = "50"
        };

        public const string PartyOmit = "Omitir";
        private static readonly ImportOption[] PartyOptions =
        {
            new("Cliente", "Cliente"), new("Proveedor", "Proveedor"), new("ClienteProveedor", "Cliente y proveedor"), new("Empleado", "Empleado"), new(PartyOmit, "Omitir esta fila")
        };

        public static CustomerImportResult Analyze(SheetData sheet, CustomerImportContext ctx)
        {
            var result = new CustomerImportResult();
            result.Preview.FileErrors.AddRange(sheet.FileErrors);
            if (sheet.FileErrors.Count > 0) return result;

            var seen = new Dictionary<string, int>(); // "{rol}|{número}" -> fila donde apareció
            var index = MunicipalityIndex.Build(ctx.Municipalities);

            foreach (var row in sheet.Rows)
            {
                var preview = new ImportRowPreview { Row = row.Row };
                result.Preview.Rows.Add(preview);
                ctx.Options.Decisions.TryGetValue(row.Row, out var decisions);
                decisions ??= new Dictionary<string, string>();

                // ---- documento
                var idTypeText = ImportText.Fold(row.Get("TIPO_IDENTIFICACION"));
                string? idType = ResolveIdType(idTypeText, ctx.IdentificationTypes);
                if (idType == null && decisions.TryGetValue("identificationType", out var chosenType) && ctx.IdentificationTypes.ContainsKey(chosenType)) idType = chosenType;

                var rawNumber = row.Get("IDENTIFICACION");
                string dvFromNumber = string.Empty;
                var dash = rawNumber.LastIndexOf('-');
                if (dash > 0 && idType == "31") { dvFromNumber = ImportText.DigitsOnly(rawNumber[(dash + 1)..]); rawNumber = rawNumber[..dash]; }
                var number = idType is "41" or "42" ? Regex.Replace(rawNumber, "[^A-Za-z0-9]", "").ToUpperInvariant() : ImportText.DigitsOnly(rawNumber);

                if (number.Length == 0) preview.Errors.Add("Falta el número de identificación.");
                else if (idType is "13" or "31" or "22" or "12" or "11" or "21" or "47" or "48" or "91" && !Regex.IsMatch(rawNumber.Trim().Replace(".", "").Replace(" ", ""), @"^\d+$"))
                    preview.Warnings.Add($"La identificación '{row.Get("IDENTIFICACION")}' trae caracteres que no son dígitos; se guardó solo con los números.");

                // ---- nombre
                var first = ImportText.Collapse(row.Get("NOMBRE"));
                var second = ImportText.Collapse(row.Get("OTROS_NOMBRES"));
                var last = ImportText.Collapse(row.Get("APELLIDO"));
                var secondLast = ImportText.Collapse(row.Get("SEGUNDO_APELLIDO"));
                var razon = ImportText.Collapse(row.Get("RAZON_SOCIAL"));
                var composed = ImportText.Collapse(string.Join(" ", new[] { first, second, last, secondLast }.Where(s => s.Length > 0)));
                var isNit = idType is "31" or "50";
                var name = isNit ? (razon.Length > 0 ? razon : composed) : (composed.Length > 0 ? composed : razon);
                if (name.Length == 0) preview.Errors.Add("Falta el nombre o la razón social.");
                foreach (var surname in new[] { last, secondLast })
                    if (surname.Length > 0 && !surname.Any(char.IsLetter)) preview.Warnings.Add($"El apellido '{surname}' parece un dato mal digitado.");

                // ---- coherencia entre TIPO (natural/jurídica) y el documento
                var personType = ImportText.Fold(row.Get("TIPO"));
                if (personType.StartsWith("NATURAL") && idType == "31") preview.Warnings.Add("Persona natural con NIT.");
                if (personType.StartsWith("JURIDICA") && idType != null && !isNit) preview.Warnings.Add("Persona jurídica con documento de persona natural.");

                // ---- dígito de verificación (solo NIT)
                var dv = string.Empty;
                if (idType == "31" && number.Length > 0)
                {
                    var computed = NitValidation.CalculateCheckDigit(number).ToString();
                    var given = ImportText.DigitsOnly(row.Get("DIGITO_VERIFICACION"));
                    if (given.Length == 0) given = dvFromNumber;
                    if (given.Length > 0 && given != computed) preview.Warnings.Add($"El dígito de verificación {given} no coincide con el calculado ({computed}); se usó el calculado.");
                    dv = computed;
                }

                // ---- roles
                var roles = ResolveRoles(row.Get("CATEGORIA"), decisions, preview, out var needsRoleDecision);

                // ---- impuestos del tercero
                var responsable = ImportText.ParseBool(row.Get("RESPONSABLE_IVA"));
                string taxLevel = string.Empty, taxRegime = string.Empty;
                if (responsable == true) { taxLevel = "RESPONSABLE_DE_IVA"; taxRegime = "48"; }
                else if (responsable == false) { taxLevel = "NO_RESPONSABLE_DE_IVA"; taxRegime = "49"; }
                else preview.Warnings.Add("Sin dato de responsabilidad de IVA.");

                var regimenText = ImportText.Fold(row.Get("REGIMEN"));
                string regimen = "ORDINARIO", regimenNote = "(por defecto)";
                bool gran = false, autorretenedor = false;
                string? incomeTaxRegime = null;
                if (regimenText.Length > 0)
                {
                    regimenNote = string.Empty;
                    switch (regimenText)
                    {
                        case "ORDINARIO": break;
                        case "SIMPLE": regimen = "SIMPLE"; incomeTaxRegime = "47"; break;
                        case "AUTORRETENEDOR": regimen = "AUTORRETENEDOR"; autorretenedor = true; break;
                        case "GRAN CONTRIBUYENTE": gran = true; break;
                        default: preview.Warnings.Add($"Régimen '{row.Get("REGIMEN")}' no reconocido; se usó ORDINARIO."); regimenNote = "(por defecto)"; break;
                    }
                }

                // ---- contacto y ubicación
                var email = row.Get("CORREO").Trim().ToLowerInvariant();
                if (email.Length == 0) preview.Warnings.Add("Sin correo.");
                else if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$")) preview.Warnings.Add($"El correo '{email}' no tiene un formato válido.");
                var phone = Regex.Replace(row.Get("TELEFONO"), @"[^\d+]", "");
                var address = ImportText.Collapse(row.Get("DIRECCION"));
                var cityText = row.Get("CIUDAD");
                var deptText = row.Get("DEPARTAMENTO");
                string cityCode = string.Empty, cityName = string.Empty, stateName = string.Empty, postalCode = string.Empty;
                if (cityText.Length > 0)
                {
                    var muni = index.Find(cityText, deptText);
                    if (muni != null) { cityCode = muni.Code; cityName = muni.Name; stateName = muni.DepartmentName; }
                    else preview.Warnings.Add($"Ciudad '{cityText}' no reconocida en el catálogo de municipios; quedó sin ciudad.");
                }
                // Falta dirección o ciudad: se completa según lo que eligió el usuario (la del cliente, la de terceros nuevos, o en blanco).
                var mode = ctx.Options.AddressMode;
                var source = mode == "client" ? ctx.ClientAddress : mode == "default" ? ctx.DefaultAddress : null;
                if (source != null)
                {
                    if (address.Length == 0 && !string.IsNullOrWhiteSpace(source.Address))
                    {
                        address = ImportText.Collapse(source.Address);
                        preview.Notes.Add(mode == "client" ? "Dirección tomada de la del cliente." : "Dirección tomada de la configurada por defecto.");
                    }
                    if (cityText.Length == 0 && cityCode.Length == 0 && !string.IsNullOrWhiteSpace(source.CityCode))
                    {
                        var muni = ctx.Municipalities.FirstOrDefault(m => m.Code == source.CityCode);
                        cityCode = source.CityCode!;
                        cityName = muni?.Name ?? source.CityName ?? string.Empty;
                        stateName = muni?.DepartmentName ?? string.Empty;
                        postalCode = source.PostalCode ?? string.Empty;
                        preview.Notes.Add(mode == "client" ? "Ciudad tomada de la del cliente." : "Ciudad tomada de la configurada por defecto.");
                    }
                }

                // ---- decisiones pendientes y errores
                if (idType == null)
                {
                    if (preview.Errors.Count == 0)
                    {
                        preview.Status = ImportStatus.Decision;
                        preview.Decision = new ImportDecision
                        {
                            Field = "identificationType",
                            Label = $"Tipo de documento '{row.Get("TIPO_IDENTIFICACION")}'",
                            Options = ctx.IdentificationTypes.OrderBy(t => t.Key).Select(t => new ImportOption(t.Key, t.Value)).ToList()
                        };
                    }
                    else preview.Status = ImportStatus.Error;
                }
                else if (preview.Errors.Count > 0) preview.Status = ImportStatus.Error;
                else if (needsRoleDecision)
                {
                    preview.Status = ImportStatus.Decision;
                    preview.Decision = new ImportDecision { Field = "partyType", Label = $"Categoría '{row.Get("CATEGORIA")}'", Options = PartyOptions.ToList() };
                }
                else if (roles.Count == 0)
                {
                    preview.Status = ImportStatus.Skipped;
                }

                // ---- lo que se guardaría
                preview.Values["Documento"] = idType == null ? $"{row.Get("TIPO_IDENTIFICACION")} {number}".Trim() : $"{idType} · {number}{(dv.Length > 0 ? "-" + dv : "")}";
                preview.Values["Nombre"] = name;
                preview.Values["Rol"] = roles.Count == 0 ? null : string.Join(" + ", roles.Select(RoleLabel));
                preview.Values["Correo"] = email.Length == 0 ? null : email;
                preview.Values["Teléfono"] = phone.Length == 0 ? null : phone;
                preview.Values["Ciudad"] = cityName.Length == 0 ? null : stateName.Length > 0 && ImportText.Fold(stateName) != ImportText.Fold(cityName) ? $"{cityName}, {stateName}" : cityName;
                preview.Values["Dirección"] = address.Length == 0 ? null : address;
                preview.Values["Régimen"] = $"{regimen}{(gran ? " · Gran contribuyente" : "")}{(autorretenedor ? " · Autorretenedor" : "")} {regimenNote}".Trim();

                if (preview.Status != ImportStatus.Create) continue;

                // ---- duplicados (por rol) y creación
                var omitted = new List<string>();
                var created = new List<Customer>();
                foreach (var role in roles)
                {
                    var key = $"{role}|{number}";
                    if (ctx.ExistingKeys.Contains(key)) { omitted.Add($"{RoleLabel(role)}: ya existe"); continue; }
                    if (seen.TryGetValue(key, out var firstRow)) { omitted.Add($"{RoleLabel(role)}: repetido en la fila {firstRow}"); continue; }
                    seen[key] = row.Row;
                    created.Add(new Customer
                    {
                        Id = Guid.NewGuid(),
                        ClientId = ctx.ClientId,
                        Name = name,
                        FirstName = first.Length > 0 ? first : null,
                        SecondName = second.Length > 0 ? second : null,
                        FirstLastName = last.Length > 0 ? last : null,
                        SecondLastName = secondLast.Length > 0 ? secondLast : null,
                        IdentificationType = idType!,
                        IdentificationNumber = number,
                        VerificationDigit = dv,
                        PartyType = role,
                        Email = email,
                        Phone = phone,
                        Address = address,
                        CountryCode = "CO",
                        StateName = stateName.Length > 0 ? stateName : null,
                        CityCode = cityCode,
                        CityName = cityName,
                        PostalCode = postalCode.Length > 0 ? postalCode : null,
                        DataicoTaxLevelCode = taxLevel,
                        DataicoRegimen = regimen,
                        TaxRegime = taxRegime,
                        IsGranContribuyente = gran,
                        IsAutorretenedorRenta = autorretenedor,
                        IncomeTaxRegime = incomeTaxRegime,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }

                if (created.Count == 0)
                {
                    preview.Status = ImportStatus.Duplicate;
                    preview.Warnings.Clear();
                    preview.Notes.Clear();
                    preview.Notes.Add(string.Join("; ", omitted));
                    preview.Values["Rol"] = null;
                    continue;
                }
                if (omitted.Count > 0) preview.Warnings.Add("Se omite: " + string.Join("; ", omitted) + ".");
                preview.Values["Rol"] = string.Join(" + ", created.Select(c => RoleLabel(c.PartyType)));
                result.Customers.AddRange(created);
            }

            result.Preview.Tally();
            return result;
        }

        private static string? ResolveIdType(string folded, IReadOnlyDictionary<string, string> catalog)
        {
            if (folded.Length == 0) return null;
            if (IdTypeByText.TryGetValue(folded, out var code) && catalog.ContainsKey(code)) return code;
            return catalog.ContainsKey(folded) ? folded : null; // ya viene el código DIAN ("13", "31")
        }

        private static string RoleLabel(PartyType role) => role switch
        {
            PartyType.Cliente => "Cliente", PartyType.Proveedor => "Proveedor", PartyType.Empleado => "Empleado", _ => role.ToString()
        };

        private static List<PartyType> ResolveRoles(string raw, Dictionary<string, string> decisions, ImportRowPreview preview, out bool needsDecision)
        {
            needsDecision = false;
            var folded = ImportText.Fold(raw);
            if (decisions.TryGetValue("partyType", out var chosen)) return FromChoice(chosen);

            switch (folded)
            {
                case "CLIENTE": return new() { PartyType.Cliente };
                case "PROVEEDOR": return new() { PartyType.Proveedor };
                case "CLIENTE PROVEEDOR": case "CLIENTE Y PROVEEDOR": case "CLIENTE PROVEEDOR AMBOS": case "AMBOS":
                    return new() { PartyType.Cliente, PartyType.Proveedor };
                case "NOMINA": case "EMPLEADO": return new() { PartyType.Empleado };
                case "MANDANTE": return new() { PartyType.Mandante };
                case "":
                    preview.Warnings.Add("Sin categoría; se cargó como Cliente.");
                    return new() { PartyType.Cliente };
                default:
                    needsDecision = true;
                    return new();
            }
        }

        private static List<PartyType> FromChoice(string choice) => choice switch
        {
            "Cliente" => new() { PartyType.Cliente },
            "Proveedor" => new() { PartyType.Proveedor },
            "ClienteProveedor" => new() { PartyType.Cliente, PartyType.Proveedor },
            "Empleado" => new() { PartyType.Empleado },
            _ => new() // Omitir
        };
    }

    // Busca un municipio por su nombre sin importar tildes, mayúsculas ni sufijos como "D.C."; el departamento desempata homónimos.
    public sealed class MunicipalityIndex
    {
        private readonly List<(string Name, string Dept, DianMunicipality Muni)> _items;
        private MunicipalityIndex(List<(string, string, DianMunicipality)> items) => _items = items;

        public static MunicipalityIndex Build(IEnumerable<DianMunicipality> municipalities) =>
            new(municipalities.Select(m => (Clean(m.Name), Clean(m.DepartmentName), m)).ToList());

        // "Bogotá, D.C." -> "BOGOTA"
        private static string Clean(string? value)
        {
            var f = " " + ImportText.Fold(value) + " ";
            f = f.Replace(" D C ", " ").Replace(" DC ", " ").Replace(" DISTRITO CAPITAL ", " ");
            return ImportText.Collapse(f);
        }

        public DianMunicipality? Find(string city, string? department)
        {
            var c = Clean(city);
            if (c.Length == 0) return null;
            var d = Clean(department);

            DianMunicipality? Pick(List<(string Name, string Dept, DianMunicipality Muni)> candidates)
            {
                if (candidates.Count == 1) return candidates[0].Muni;
                if (candidates.Count > 1 && d.Length > 0)
                {
                    var byDept = candidates.Where(x => x.Dept == d || x.Dept.Contains(d) || d.Contains(x.Dept)).ToList();
                    if (byDept.Count == 1) return byDept[0].Muni;
                }
                return null;
            }

            var exact = Pick(_items.Where(x => x.Name == c).ToList());
            if (exact != null) return exact;
            if (_items.Any(x => x.Name == c)) return null; // homónimos sin departamento que desempate: no se adivina
            // Variantes ("VILLA DE SAN DIEGO DE UBATE" frente a "UBATE"): una contiene a la otra y es la única que coincide.
            return Pick(_items.Where(x => x.Name.Length >= 4 && (c.EndsWith(" " + x.Name) || c.StartsWith(x.Name + " ") || x.Name.EndsWith(" " + c))).ToList());
        }
    }
}
