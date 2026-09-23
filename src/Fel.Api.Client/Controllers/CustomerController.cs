using System;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Fel.Api.Client.Models;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;

namespace Fel.Api.Client.Controllers
{
    [ApiController]
    [Route("api/client/customers")]
    public class CustomerController : ControllerBase
    {
        private readonly FelDbContext _dbContext;

        public CustomerController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        private Guid GetCurrentClientId()
        {
            if (Request.Headers.TryGetValue("x-client-id", out var clientIdStr))
            {
                if (Guid.TryParse(clientIdStr, out var clientId))
                    return clientId;
            }
            throw new UnauthorizedAccessException("x-client-id Header is missing");
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] PartyType? partyType)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var query = _dbContext.Customers.Where(c => c.ClientId == clientId);

                if (partyType.HasValue)
                {
                    query = query.Where(c => c.PartyType == partyType.Value);
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

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Customer customer)
        {
            try
            {
                var clientId = GetCurrentClientId();
                
                // Validar si ya existe
                var existing = await _dbContext.Customers
                    .FirstOrDefaultAsync(c => c.ClientId == clientId && c.IdentificationNumber == customer.IdentificationNumber);
                    
                if (existing != null)
                    return BadRequest("Ya existe un cliente con este número de identificación.");

                customer.Id = Guid.NewGuid();
                customer.ClientId = clientId;
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
                var customer = await _dbContext.Customers
                    .FirstOrDefaultAsync(c => c.Id == id && c.ClientId == clientId);

                if (customer == null) return NotFound("Cliente no encontrado.");

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
                customer.CityCode = updateData.CityCode;
                customer.CityName = updateData.CityName;
                customer.PostalCode = updateData.PostalCode;
                customer.Latitude = updateData.Latitude;
                customer.Longitude = updateData.Longitude;
                customer.TaxRegime = updateData.TaxRegime;
                customer.FiscalResponsibilities = updateData.FiscalResponsibilities;
                customer.DataicoTaxLevelCode = updateData.DataicoTaxLevelCode;
                customer.DataicoRegimen = updateData.DataicoRegimen;
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

        [HttpGet("template")]
        public IActionResult DownloadTemplate()
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("Terceros");

            ExcelTemplateHelper.WriteHeaderRow(sheet, 1,
                "Nombre", "TipoIdentificacion", "NumeroIdentificacion", "DigitoVerificacion", "TipoTercero",
                "Email", "Telefono", "Direccion", "CodigoCiudadDANE", "NivelTributarioDataico", "RegimenDataico");

            sheet.Cell(2, 1).Value = "Juan Pérez";
            sheet.Cell(2, 2).Value = "13";
            sheet.Cell(2, 3).Value = "12345678";
            sheet.Cell(2, 4).Value = "";
            sheet.Cell(2, 5).Value = "Cliente";
            sheet.Cell(2, 6).Value = "juan.perez@ejemplo.com";
            sheet.Cell(2, 7).Value = "3001234567";
            sheet.Cell(2, 8).Value = "Calle 10 # 20-30";
            sheet.Cell(2, 9).Value = "11001";
            sheet.Cell(2, 10).Value = "COMUN";
            sheet.Cell(2, 11).Value = "ORDINARIO";

            ExcelTemplateHelper.AddDropdown(sheet, "E2:E1000", "Cliente", "Proveedor", "Empleado");

            sheet.Columns().AdjustToContents();
            var bytes = ExcelTemplateHelper.ToBytes(workbook);
            return File(bytes, ExcelTemplateHelper.XlsxContentType, "plantilla_terceros.xlsx");
        }

        // Plantilla (fila 1 = encabezados, datos desde la fila 2):
        // Nombre | TipoIdentificacion | NumeroIdentificacion | DV | TipoTercero | Email | Telefono | Direccion | CodigoDANE | NivelTributarioDataico | RegimenDataico
        [HttpPost("import")]
        public async Task<IActionResult> Import(IFormFile file)
        {
            try
            {
                var clientId = GetCurrentClientId();

                if (file == null || file.Length == 0)
                {
                    return BadRequest("Debes adjuntar un archivo Excel (.xlsx).");
                }

                var summary = new ImportSummary();
                using var stream = file.OpenReadStream();
                using var workbook = new XLWorkbook(stream);
                var sheet = workbook.Worksheets.First();
                var rows = sheet.RowsUsed().Skip(1);

                foreach (var row in rows)
                {
                    summary.TotalRows++;
                    var rowNumber = row.RowNumber();

                    try
                    {
                        var name = row.Cell(1).GetString().Trim();
                        var identificationNumber = row.Cell(3).GetString().Trim();

                        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(identificationNumber))
                        {
                            summary.Results.Add(new ImportRowResult { Row = rowNumber, Success = false, Message = "Nombre e identificación son obligatorios." });
                            summary.Failed++;
                            continue;
                        }

                        if (await _dbContext.Customers.AnyAsync(c => c.ClientId == clientId && c.IdentificationNumber == identificationNumber))
                        {
                            summary.Results.Add(new ImportRowResult { Row = rowNumber, Success = false, Message = $"Ya existe un tercero con identificación {identificationNumber}." });
                            summary.Failed++;
                            continue;
                        }

                        var partyTypeText = row.Cell(5).GetString().Trim();
                        if (!Enum.TryParse<PartyType>(partyTypeText, true, out var partyType))
                        {
                            partyType = PartyType.Cliente;
                        }

                        var customer = new Customer
                        {
                            Id = Guid.NewGuid(),
                            ClientId = clientId,
                            Name = name,
                            IdentificationType = row.Cell(2).GetString().Trim(),
                            IdentificationNumber = identificationNumber,
                            VerificationDigit = row.Cell(4).GetString().Trim(),
                            PartyType = partyType,
                            Email = row.Cell(6).GetString().Trim(),
                            Phone = row.Cell(7).GetString().Trim(),
                            Address = row.Cell(8).GetString().Trim(),
                            CityCode = row.Cell(9).GetString().Trim(),
                            DataicoTaxLevelCode = row.Cell(10).GetString().Trim(),
                            DataicoRegimen = row.Cell(11).GetString().Trim(),
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        _dbContext.Customers.Add(customer);
                        summary.Results.Add(new ImportRowResult { Row = rowNumber, Success = true, Message = "Creado." });
                        summary.Succeeded++;
                    }
                    catch (Exception ex)
                    {
                        summary.Results.Add(new ImportRowResult { Row = rowNumber, Success = false, Message = ex.Message });
                        summary.Failed++;
                    }
                }

                await _dbContext.SaveChangesAsync();
                return Ok(summary);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
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
