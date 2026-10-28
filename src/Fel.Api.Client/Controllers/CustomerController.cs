using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Fel.Api.Client.Models;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Fel.Api.Security;

namespace Fel.Api.Client.Controllers
{
    [ApiController]
    [Route("api/client/customers")]
    [AllowAllBranches]
    public class CustomerController : ClientPortalControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly Fel.Infrastructure.Services.DianRutParserService _rutParser;

        public CustomerController(FelDbContext dbContext, Fel.Infrastructure.Services.DianRutParserService rutParser)
        {
            _dbContext = dbContext;
            _rutParser = rutParser;
        }

        // Lee un RUT (Formulario 001 de la DIAN) y devuelve sus datos para prellenar el alta de un
        // tercero (persona natural o jurídica) — no crea nada, el usuario revisa y confirma en el
        // formulario. Mismo servicio que usa Superadmin para el alta de Tenants
        // (Fel.Infrastructure.Services.DianRutParserService).
        [HttpPost("parse-rut")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ParseRut([FromForm] IFormFile file)
        {
            try
            {
                GetCurrentClientId();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }

            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No se adjuntó ningún archivo." });

            if (!string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "El RUT debe ser el PDF que descarga el portal de la DIAN." });

            using var stream = file.OpenReadStream();
            var rut = await _rutParser.ParsePdfAsync(stream);

            if (!rut.IsSuccess)
                return BadRequest(new { message = rut.ErrorMessage });

            var isNatural = !string.IsNullOrWhiteSpace(rut.FirstName) || !string.IsNullOrWhiteSpace(rut.FirstLastName);

            return Ok(new
            {
                personType = isNatural ? "Natural" : "Juridica",
                identificationType = isNatural ? "13" : "31",
                identificationNumber = rut.TaxId,
                verificationDigit = rut.VerificationDigit,
                name = rut.LegalName,
                firstName = rut.FirstName,
                secondName = rut.SecondName,
                firstLastName = rut.FirstLastName,
                secondLastName = rut.SecondLastName,
                address = rut.Address,
                cityName = rut.City,
                cityCode = rut.CityCode,
                department = rut.Department,
                email = rut.Email,
                phone = rut.Phone,
                // El RUT no trae el código DANE de municipio ni el régimen tributario propio de
                // Dataico directamente — solo las responsabilidades (casilla 53), de donde se
                // deduce una sugerencia razonable para los dos catálogos que Dataico exige.
                dataicoRegimen = rut.ResponsibilityCodes.Contains("47") ? "SIMPLE" : "ORDINARIO",
                dataicoTaxLevelCode = rut.ResponsibilityCodes.Contains("48") ? "RESPONSABLE_DE_IVA" : "NO_RESPONSABLE_DE_IVA",
                isElectronicInvoicer = rut.IsElectronicInvoicer,
                isGranContribuyente = rut.IsGranContribuyente,
                isAgenteRetenedorIva = rut.IsAgenteRetenedorIva,
                isAutorretenedorRenta = rut.IsAutorretenedorRenta,
                incomeTaxRegime = rut.IncomeTaxRegimeCode
            });
        }

        [HttpGet]
        // lite=true: solo lo que necesita un selector de terceros en un formulario (id, nombre, identificación, correo y rol). Evita mandar al navegador
        // todos los campos de miles de terceros cuando solo se va a elegir uno.
        public async Task<IActionResult> GetAll([FromQuery] PartyType? partyType, [FromQuery] bool lite = false)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var query = _dbContext.Customers.AsNoTracking().Where(c => c.ClientId == clientId);

                if (partyType.HasValue)
                {
                    query = query.Where(c => c.PartyType == partyType.Value);
                }

                if (lite)
                {
                    return Ok(await query
                        .OrderByDescending(c => c.CreatedAt)
                        .Select(c => new { c.Id, c.Name, c.IdentificationType, c.IdentificationNumber, c.VerificationDigit, c.Email, c.PartyType })
                        .ToListAsync());
                }

                var customers = await query
                    .OrderByDescending(c => c.CreatedAt)
                    .ToListAsync();

                return Ok(customers);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        // Búsqueda para los selectores de terceros de los formularios: se escribe y el servidor devuelve las primeras coincidencias, en vez de mandar al
        // navegador todos los terceros (hay clientes con miles). Sin texto devuelve los más recientes.
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] PartyType? partyType, [FromQuery] string? q, [FromQuery] int limit = CustomerSearch.DefaultLimit)
        {
            try
            {
                var clientId = GetCurrentClientId();
                limit = Math.Clamp(limit, 1, CustomerSearch.MaxLimit);
                var query = _dbContext.Customers.AsNoTracking().Where(c => c.ClientId == clientId);
                if (partyType.HasValue) query = query.Where(c => c.PartyType == partyType.Value);

                var text = (q ?? string.Empty).Trim();
                query = CustomerSearch.Filter(query, text);

                // Con texto, primero los que empiezan por lo escrito (identificación o nombre); sin texto, los más recientes.
                var ordered = text.Length == 0
                    ? query.OrderByDescending(c => c.CreatedAt)
                    : query.OrderBy(c => c.IdentificationNumber.StartsWith(text) || c.Name.StartsWith(text) ? 0 : 1).ThenBy(c => c.Name);

                return Ok(await CustomerSearch.Project(ordered.Take(limit)).ToListAsync());
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var customer = await _dbContext.Customers
                    .FirstOrDefaultAsync(c => c.Id == id && c.ClientId == clientId);

                if (customer == null) return NotFound("Cliente no encontrado.");
                return Ok(customer);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        // País del tercero: alfa-2 en mayúscula ("CO" si no viene) y debe existir en el catálogo de países. Fuera de Colombia no hay
        // código DANE: la ciudad (obligatoria) y el departamento o estado van como texto. Devuelve el mensaje de error o null.
        private async Task<string?> NormalizeCountryAsync(Customer customer)
        {
            var code = (customer.CountryCode ?? string.Empty).Trim().ToUpperInvariant();
            if (code.Length == 0) code = "CO";
            if (code != "CO" && !await _dbContext.TaxCatalogItems.AsNoTracking().AnyAsync(i => i.Kind == TaxCatalogKind.Country && i.Category == code && i.IsActive))
                return $"El país {code} no está en el catálogo de países.";

            customer.CountryCode = code;
            if (code == "CO")
            {
                customer.StateName = null;
                return null;
            }

            customer.CityCode = string.Empty;
            customer.StateName = string.IsNullOrWhiteSpace(customer.StateName) ? null : customer.StateName.Trim();
            return string.IsNullOrWhiteSpace(customer.CityName) ? "Indica la ciudad del tercero." : null;
        }

        private static string CleanName(string? value) =>
            Regex.Replace((value ?? string.Empty).Trim(), @"\s+", " ");

        private static void NormalizeNames(Customer customer)
        {
            customer.Name = CleanName(customer.Name);
            customer.FirstName = CleanName(customer.FirstName);
            customer.SecondName = CleanName(customer.SecondName);
            customer.FirstLastName = CleanName(customer.FirstLastName);
            customer.SecondLastName = CleanName(customer.SecondLastName);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Customer customer)
        {
            try
            {
                var clientId = GetCurrentClientId();
                NormalizeNames(customer);

                var countryError = await NormalizeCountryAsync(customer);
                if (countryError != null) return BadRequest(countryError);

                // Validar si ya existe
                var existing = await _dbContext.Customers
                    .FirstOrDefaultAsync(c => c.ClientId == clientId &&
                                              c.IdentificationNumber == customer.IdentificationNumber &&
                                              c.PartyType == customer.PartyType);
                    
                if (existing != null)
                    return BadRequest($"Ya existe un tercero con esta identificación para el rol {customer.PartyType}.");

                customer.Id = Guid.NewGuid();
                customer.ClientId = clientId;
                // El vínculo con un cliente del tenant solo lo pone la sincronización; un tercero que se crea a mano nunca lo trae.
                customer.LinkedClientId = null;
                customer.CreatedAt = DateTime.UtcNow;
                customer.UpdatedAt = DateTime.UtcNow;

                _dbContext.Customers.Add(customer);
                await _dbContext.SaveChangesAsync();

                return CreatedAtAction(nameof(GetById), new { id = customer.Id }, customer);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] Customer updateData)
        {
            try
            {
                var clientId = GetCurrentClientId();
                NormalizeNames(updateData);
                var customer = await _dbContext.Customers
                    .FirstOrDefaultAsync(c => c.Id == id && c.ClientId == clientId);

                if (customer == null) return NotFound("Cliente no encontrado.");

                var countryError = await NormalizeCountryAsync(updateData);
                if (countryError != null) return BadRequest(countryError);

                var duplicate = await _dbContext.Customers.AnyAsync(c => c.ClientId == clientId &&
                    c.Id != id &&
                    c.IdentificationNumber == updateData.IdentificationNumber &&
                    c.PartyType == updateData.PartyType);
                if (duplicate)
                    return BadRequest($"Ya existe un tercero con esta identificación para el rol {updateData.PartyType}.");

                customer.Name = updateData.Name;
                customer.FirstName = updateData.FirstName;
                customer.SecondName = updateData.SecondName;
                customer.FirstLastName = updateData.FirstLastName;
                customer.SecondLastName = updateData.SecondLastName;
                customer.IdentificationType = updateData.IdentificationType;
                customer.IdentificationNumber = updateData.IdentificationNumber;
                customer.VerificationDigit = updateData.VerificationDigit;
                customer.PartyType = updateData.PartyType;
                customer.Email = updateData.Email;
                customer.Phone = updateData.Phone;
                customer.Address = updateData.Address;
                customer.CountryCode = updateData.CountryCode;
                customer.StateName = updateData.StateName;
                customer.CityCode = updateData.CityCode;
                customer.CityName = updateData.CityName;
                customer.PostalCode = updateData.PostalCode;
                customer.Latitude = updateData.Latitude;
                customer.Longitude = updateData.Longitude;
                customer.TaxRegime = updateData.TaxRegime;
                customer.FiscalResponsibilities = updateData.FiscalResponsibilities;
                customer.DataicoTaxLevelCode = updateData.DataicoTaxLevelCode;
                customer.DataicoRegimen = updateData.DataicoRegimen;
                customer.IsGranContribuyente = updateData.IsGranContribuyente;
                customer.IsAgenteRetenedorIva = updateData.IsAgenteRetenedorIva;
                customer.IsAutorretenedorRenta = updateData.IsAutorretenedorRenta;
                customer.IncomeTaxRegime = string.IsNullOrWhiteSpace(updateData.IncomeTaxRegime) ? null : updateData.IncomeTaxRegime;
                customer.WorkerType = updateData.WorkerType;
                customer.ContractType = updateData.ContractType;
                customer.PaymentMeans = updateData.PaymentMeans;
                customer.Bank = updateData.Bank;
                customer.AccountType = updateData.AccountType;
                customer.AccountNumber = updateData.AccountNumber;
                customer.HighRisk = updateData.HighRisk;
                customer.IntegralSalary = updateData.IntegralSalary;
                customer.BaseSalary = updateData.BaseSalary;
                customer.StartDate = updateData.StartDate;
                customer.FireDate = updateData.FireDate;
                customer.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();
                return Ok(customer);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        // ---- Carga masiva de terceros: plantilla, vista previa y confirmación (ver CustomerImporter).
        [HttpGet("template")]
        public IActionResult DownloadTemplate() =>
            File(Fel.Api.Client.Imports.BulkImportTemplates.Customers(), ExcelTemplateHelper.XlsxContentType, "plantilla_terceros.xlsx");

        // Valida el archivo completo SIN guardar nada y devuelve, fila por fila, lo que se crearía, lo que se omite y lo que el usuario debe decidir.
        [HttpPost("import/preview")]
        [RequestSizeLimit(Fel.Api.Client.Imports.ExcelSheetReader.MaxBytes)]
        public async Task<IActionResult> ImportPreview(IFormFile file, [FromForm] string? options)
        {
            var analysis = await AnalyzeImportAsync(file, options);
            return analysis.Error != null ? BadRequest(analysis.Error) : Ok(analysis.Result!.Preview);
        }

        // Vuelve a validar el mismo archivo con las decisiones del usuario y guarda los terceros válidos en una sola operación: o se crean todos o ninguno.
        [HttpPost("import/confirm")]
        [RequestSizeLimit(Fel.Api.Client.Imports.ExcelSheetReader.MaxBytes)]
        public async Task<IActionResult> ImportConfirm(IFormFile file, [FromForm] string? options)
        {
            var analysis = await AnalyzeImportAsync(file, options);
            if (analysis.Error != null) return BadRequest(analysis.Error);
            var result = analysis.Result!;
            if (result.Preview.FileErrors.Count > 0) return BadRequest(string.Join(" ", result.Preview.FileErrors));
            if (result.Customers.Count == 0) return BadRequest("No hay terceros por crear en este archivo.");

            _dbContext.Customers.AddRange(result.Customers);
            try { await _dbContext.SaveChangesAsync(); }
            catch (DbUpdateException ex)
            {
                // Todo o nada: no se creó ningún tercero. Se explica en vez de responder 500.
                return BadRequest("No se pudo guardar la carga; no se creó ningún tercero. " + (ex.InnerException?.Message ?? ex.Message));
            }
            return Ok(new Fel.Api.Client.Imports.BulkImportResult { Created = result.Customers.Count, Skipped = result.Preview.Rows.Count(r => r.Status != Fel.Api.Client.Imports.ImportStatus.Create) });
        }

        private async Task<(Fel.Api.Client.Imports.CustomerImportResult? Result, string? Error)> AnalyzeImportAsync(IFormFile? file, string? optionsJson)
        {
            var clientId = GetCurrentClientId();
            if (file == null || file.Length == 0) return (null, "Debes adjuntar un archivo Excel (.xlsx).");
            if (file.Length > Fel.Api.Client.Imports.ExcelSheetReader.MaxBytes) return (null, "El archivo supera los 10 MB.");

            Fel.Api.Client.Imports.BulkImportOptions options;
            try
            {
                options = string.IsNullOrWhiteSpace(optionsJson)
                    ? new Fel.Api.Client.Imports.BulkImportOptions()
                    : System.Text.Json.JsonSerializer.Deserialize<Fel.Api.Client.Imports.BulkImportOptions>(optionsJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            }
            catch (System.Text.Json.JsonException) { return (null, "Las opciones de la carga no son válidas."); }

            var client = await _dbContext.Clients.AsNoTracking().FirstAsync(c => c.Id == clientId);
            // Dirección "por defecto para terceros nuevos" que el cliente configura en sus valores por defecto.
            var defaults = await _dbContext.ClientInvoiceDefaults.AsNoTracking().FirstOrDefaultAsync(x => x.ClientId == clientId);
            var existing = await _dbContext.Customers.AsNoTracking().Where(c => c.ClientId == clientId)
                .Select(c => new { c.PartyType, c.IdentificationNumber }).ToListAsync();
            var context = new Fel.Api.Client.Imports.CustomerImportContext
            {
                ClientId = clientId,
                Municipalities = await _dbContext.DianMunicipalities.AsNoTracking().ToListAsync(),
                IdentificationTypes = await _dbContext.IdentificationTypes.AsNoTracking().Where(t => t.IsActive).ToDictionaryAsync(t => t.Code, t => t.Name),
                ExistingKeys = new HashSet<string>(existing.Select(c => $"{c.PartyType}|{c.IdentificationNumber}")),
                ClientAddress = new Fel.Api.Client.Imports.AddressDefaults(client.Address, client.CityCode, client.City, null),
                DefaultAddress = new Fel.Api.Client.Imports.AddressDefaults(defaults?.CustomerAddress, defaults?.CustomerCityCode, defaults?.CustomerCityName, defaults?.CustomerPostalCode),
                Options = options
            };

            using var stream = file.OpenReadStream();
            var sheet = Fel.Api.Client.Imports.ExcelSheetReader.Read(stream, Fel.Api.Client.Imports.CustomerImporter.Aliases, Fel.Api.Client.Imports.CustomerImporter.Required);
            return (Fel.Api.Client.Imports.CustomerImporter.Analyze(sheet, context), null);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var customer = await _dbContext.Customers
                    .FirstOrDefaultAsync(c => c.Id == id && c.ClientId == clientId);

                if (customer == null) return NotFound("Cliente no encontrado.");

                _dbContext.Customers.Remove(customer);
                await _dbContext.SaveChangesAsync();
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }
    }
}
