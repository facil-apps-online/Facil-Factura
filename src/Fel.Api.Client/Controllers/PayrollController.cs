using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Fel.Api.Client.Models;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Dataico;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Fel.Infrastructure.Services;

namespace Fel.Api.Client.Controllers
{
    [ApiController]
    [Route("api/client/payroll")]
    public class PayrollController : ClientPortalControllerBase
    {
        private const string TypeCode = "NE";
        // No corresponden a filas propias del catálogo DocumentTypes (ambas caen bajo el mismo
        // DianCode 103, "Nota de Ajuste - Nómina Electrónica") — son solo códigos internos para
        // distinguir en la UI y el enrutamiento cuál de las dos notas de Dataico es cada una.
        private const string DeletionTypeCode = "NE-ELIMINACION";
        private const string ReplacementTypeCode = "NE-REEMPLAZO";

        // Editable/publicable/eliminable mientras no haya llegado exitosamente a Dataico/DIAN:
        // un DRAFT nunca se envió, y un REJECTED fue rechazado por Dataico, así que tampoco llegó.
        private static bool IsEditable(string status) => status == "DRAFT" || status == "REJECTED";

        private readonly FelDbContext _dbContext;
        private readonly ICryptoService _cryptoService;
        private readonly IDataicoApiService _dataicoApiService;
        private readonly DataicoCustomPdfService _customPdfService;

        public PayrollController(FelDbContext dbContext, ICryptoService cryptoService, IDataicoApiService dataicoApiService, DataicoCustomPdfService customPdfService)
        {
            _dbContext = dbContext;
            _cryptoService = cryptoService;
            _dataicoApiService = dataicoApiService;
            _customPdfService = customPdfService;
        }

        // Sin from/to, el rango por defecto cubre el mes calendario en curso más el anterior (los
        // 1-2 meses que se suelen revisar juntos en nómina), no días corridos — misma razón que en
        // InvoiceController.GetAll.
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var rangeStart = (from ?? new DateTime(Fel.Core.Models.ColombiaTime.Today.Year, Fel.Core.Models.ColombiaTime.Today.Month, 1).AddMonths(-1)).Date;
                var rangeEnd = (to ?? Fel.Core.Models.ColombiaTime.Today).Date.AddDays(1).AddTicks(-1);

                var entries = await BranchDocuments
                    .Include(d => d.Customer)
                    .Where(d => d.ClientId == clientId && (d.TypeCode == TypeCode || d.TypeCode == DeletionTypeCode || d.TypeCode == ReplacementTypeCode) && d.IssueDate >= rangeStart && d.IssueDate <= rangeEnd)
                    .OrderByDescending(d => d.IssueDate)
                    .ToListAsync();

                // document.Number solo es el consecutivo real (asignado por ResolutionNumbering) una
                // vez que la nómina se publica — mientras está en Draft/Rejected sigue siendo el
                // placeholder de creación, así que se muestra igual hasta que se emita.
                var result = entries.Select(d =>
                {
                    var draft = DeserializeDraft(d.SectorExtensionData);
                    return new
                    {
                        d.Id,
                        d.Status,
                        d.TotalAmount,
                        d.IssueDate,
                        d.TypeCode,
                        d.DianResponseMessage,
                        Customer = d.Customer,
                        draft.Prefix,
                        ConsecutiveNumber = $"{draft.Prefix}-{d.Number}",
                        draft.InitialSettlementDate,
                        draft.FinalSettlementDate
                    };
                });

                return Ok(result);
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
                var document = await BranchDocuments
                    .Include(d => d.Customer)
                    .FirstOrDefaultAsync(d => d.Id == id && d.ClientId == clientId && (d.TypeCode == TypeCode || d.TypeCode == DeletionTypeCode || d.TypeCode == ReplacementTypeCode));

                if (document == null) return NotFound("Nómina no encontrada.");

                var draft = DeserializeDraft(document.SectorExtensionData);
                return Ok(new
                {
                    document.Id,
                    document.Status,
                    document.TotalAmount,
                    document.CreatedAt,
                    document.IssueDate,
                    document.TypeCode,
                    document.Number,
                    document.Cufe,
                    document.DianResponseMessage,
                    document.ReferenceDocumentId,
                    document.ReferenceConcept,
                    Customer = document.Customer,
                    EmployeeId = document.CustomerId,
                    draft.Prefix,
                    draft.InitialSettlementDate,
                    draft.FinalSettlementDate,
                    draft.PaymentDate,
                    draft.Accruals,
                    draft.Deductions
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        // Notas de eliminación/reemplazo que referencian este comprobante — mismo patrón que
        // InvoiceController.
        [HttpGet("{id}/related")]
        public async Task<IActionResult> GetRelated(Guid id)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var related = await BranchDocuments
                    .Where(d => d.ClientId == clientId && d.ReferenceDocumentId == id)
                    .OrderByDescending(d => d.IssueDate)
                    .Select(d => new { d.Id, d.Number, d.TypeCode, d.Status, d.TotalAmount, d.IssueDate })
                    .ToListAsync();

                return Ok(related);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpPost("draft")]
        public async Task<IActionResult> CreateDraft([FromBody] CreatePayrollRequest request)
        {
            try
            {
                var clientId = GetCurrentClientId();

                var employee = await _dbContext.Customers.FirstOrDefaultAsync(c => c.Id == request.EmployeeId && c.ClientId == clientId && c.PartyType == PartyType.Empleado);
                if (employee == null) return BadRequest("El empleado seleccionado no existe.");

                var document = BuildDraftDocument(clientId, GetCurrentBranchId(), employee.Id, request);
                document.Status = "DRAFT";

                _dbContext.Documents.Add(document);
                await _dbContext.SaveChangesAsync();

                return CreatedAtAction(nameof(GetById), new { id = document.Id }, document);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpPut("{id}/draft")]
        public async Task<IActionResult> UpdateDraft(Guid id, [FromBody] CreatePayrollRequest request)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var document = await BranchDocuments.FirstOrDefaultAsync(d => d.Id == id && d.ClientId == clientId && (d.TypeCode == TypeCode || d.TypeCode == DeletionTypeCode || d.TypeCode == ReplacementTypeCode));
                if (document == null) return NotFound("Nómina no encontrada.");
                if (!IsEditable(document.Status)) return BadRequest("Solo se pueden modificar borradores o nóminas rechazadas.");

                var employee = await _dbContext.Customers.FirstOrDefaultAsync(c => c.Id == request.EmployeeId && c.ClientId == clientId && c.PartyType == PartyType.Empleado);
                if (employee == null) return BadRequest("El empleado seleccionado no existe.");

                var updated = BuildDraftDocument(clientId, GetCurrentBranchId(), employee.Id, request);
                document.CustomerId = employee.Id;
                document.Subtotal = updated.Subtotal;
                document.TaxAmount = updated.TaxAmount;
                document.TotalAmount = updated.TotalAmount;
                document.SectorExtensionData = updated.SectorExtensionData;

                await _dbContext.SaveChangesAsync();
                return Ok(document);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpPost("{id}/publish")]
        public async Task<IActionResult> Publish(Guid id)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var document = await BranchDocuments
                    .Include(d => d.Customer)
                    .FirstOrDefaultAsync(d => d.Id == id && d.ClientId == clientId && (d.TypeCode == TypeCode || d.TypeCode == DeletionTypeCode || d.TypeCode == ReplacementTypeCode));

                if (document == null) return NotFound("Nómina no encontrada.");
                if (!IsEditable(document.Status)) return BadRequest("La nómina ya fue emitida o está en proceso.");

                var client = await _dbContext.Clients.Include(c => c.Integrator).FirstOrDefaultAsync(c => c.Id == clientId);
                if (client == null) return NotFound("Emisor no encontrado.");

                if (client.Integrator.Code != "DATAICO")
                {
                    return BadRequest("Este emisor no tiene configurado un proveedor de documentos electrónicos habilitado.");
                }

                // Nómina no tiene resolución DIAN real (el consecutivo lo administra el empleador,
                // Anexo Técnico Nómina Electrónica), pero igual necesita ser secuencial de verdad —
                // se reutiliza la misma entidad Resolution (DocumentType="NE") solo para llevar el
                // NextNumber, con el mismo mecanismo atómico que ya usan las facturas.
                if (!document.ResolutionId.HasValue)
                {
                    var payrollResolution = await _dbContext.Resolutions.ForBranch(_dbContext, document.BranchId)
                        .Where(r => r.ClientId == clientId && r.IsActive && r.DocumentType == "NE")
                        .OrderByDescending(r => r.IsDefault)
                        .ThenByDescending(r => r.ValidTo)
                        .FirstOrDefaultAsync();

                    if (payrollResolution == null)
                    {
                        return BadRequest("No hay una numeración de nómina configurada para este emisor. Configura el número inicial en Resoluciones DIAN.");
                    }

                    document.Number = (await Fel.Infrastructure.Services.ResolutionNumbering.ClaimNextNumberAsync(_dbContext, payrollResolution.Id)).ToString();
                    document.ResolutionId = payrollResolution.Id;
                }

                var documentNumber = long.Parse(document.Number);

                var draft = DeserializeDraft(document.SectorExtensionData);
                // El prefijo queda guardado en el documento (la nómina lo lleva en su borrador).
                document.Prefix = draft.Prefix;
                Fel.Infrastructure.Dataico.Models.DataicoResult result;

                if (document.TypeCode == DeletionTypeCode || document.TypeCode == ReplacementTypeCode)
                {
                    if (!document.ReferenceDocumentId.HasValue)
                    {
                        return BadRequest("Esta nota debe referenciar el comprobante de nómina que ajusta.");
                    }
                    var originalDocument = await _dbContext.Documents.FindAsync(document.ReferenceDocumentId.Value);
                    if (originalDocument == null || string.IsNullOrEmpty(originalDocument.Cufe))
                    {
                        return BadRequest("El comprobante de nómina original no tiene un CUNE registrado (puede no haberse emitido por Dataico). No se puede generar la nota.");
                    }
                    var originalDraft = DeserializeDraft(originalDocument.SectorExtensionData);
                    // Nóminas emitidas antes de este cambio no tienen un Number real (era un
                    // placeholder de creación, nunca el consecutivo que de verdad se envió a
                    // Dataico) — no hay forma de recuperar ese número histórico real.
                    var originalNumber = long.Parse(originalDocument.Number);
                    var credentials = DataicoDocumentMapper.ToCredentials(client, _cryptoService);

                    if (document.TypeCode == DeletionTypeCode)
                    {
                        var deletionRequest = DataicoDocumentMapper.BuildPayrollDeletionRequest(
                            client, draft.Prefix, documentNumber, Fel.Core.Models.ColombiaTime.Now,
                            originalDraft.Prefix, originalNumber, originalDocument.Cufe, originalDocument.IssueDate, document.ReferenceConcept);
                        result = await _dataicoApiService.SendPayrollDeletionAsync(deletionRequest, credentials);
                    }
                    else
                    {
                        var replacementRequest = DataicoDocumentMapper.BuildPayrollReplacementRequest(
                            document.Customer!, client, draft.Prefix, documentNumber,
                            draft.InitialSettlementDate, draft.FinalSettlementDate, Fel.Core.Models.ColombiaTime.Now, draft.PaymentDate,
                            draft.Accruals.Select(ToConceptInput), draft.Deductions.Select(ToConceptInput),
                            originalDraft.Prefix, originalNumber, originalDocument.Cufe, originalDocument.IssueDate, document.ReferenceConcept);
                        result = await _dataicoApiService.SendPayrollReplacementAsync(replacementRequest, credentials);
                    }

                    document.Status = result.Success ? "APPROVED" : "REJECTED";
                    document.DianResponseMessage = result.Success ? result.RawResponse : (result.ErrorMessage ?? result.RawResponse);
                    document.Cufe = result.Cufe;
                    document.QrCode = result.QrCode;
                    document.ProcessedAt = DateTime.UtcNow;
                }
                else
                {
                    result = await SubmitPayrollToDataicoAsync(
                        document, document.Customer!, client,
                        draft.Prefix, documentNumber,
                        draft.InitialSettlementDate, draft.FinalSettlementDate, draft.PaymentDate,
                        draft.Accruals, draft.Deductions);
                }

                if (result.Success)
                {
                    document.IntegratorId = client.IntegratorId;

                    var pdfCredentials = DataicoDocumentMapper.ToCredentials(client, _cryptoService);
                    await _customPdfService.TrySendCustomPdfAsync(
                        document, document.Customer?.Email, client,
                        _ => NominaReportDataMapper.Build(
                            document, document.Customer!, client, draft.Prefix, documentNumber,
                            draft.InitialSettlementDate, draft.FinalSettlementDate, draft.PaymentDate,
                            draft.Accruals.Select(c => (c.Code, c.Description, c.Amount ?? 0m)),
                            draft.Deductions.Select(c => (c.Code, c.Description, c.Amount ?? 0m))),
                        pdfCredentials);
                }

                await _dbContext.SaveChangesAsync();

                if (!result.Success)
                {
                    return BadRequest(new { message = "Dataico rechazó la nómina.", detail = document.DianResponseMessage });
                }

                return Ok(new { message = "Nómina electrónica emitida correctamente.", document });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDraft(Guid id)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var document = await BranchDocuments.FirstOrDefaultAsync(d => d.Id == id && d.ClientId == clientId && (d.TypeCode == TypeCode || d.TypeCode == DeletionTypeCode || d.TypeCode == ReplacementTypeCode));
                if (document == null) return NotFound("Nómina no encontrada.");
                if (!IsEditable(document.Status)) return BadRequest("Solo se pueden eliminar borradores o nóminas rechazadas.");

                _dbContext.Documents.Remove(document);
                await _dbContext.SaveChangesAsync();
                return NoContent();
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
            var sheet = workbook.Worksheets.Add("NOMINA - SIMPLE");

            var headers = new[]
            {
                "PREFIJO", "NUMERO", "MES", "AÑO", "FECHA", "FECHA_PAGO", "PERIODO", "SALARIO",
                "LIQUIDACION_INICIO", "LIQUIDACION_FINAL", "TIPO", "CODIGO", "VALOR_SALARIAL", "VALOR_NO_SALARIAL",
                "FECHA_DE_INICIO_DEL_DEVENGADO", "FECHA_FINAL_DEL_DEVENGADO", "DIAS", "PORCENTAJE", "HORAS",
                "DESCRIPCION", "SANCION_PUBLICA", "SANCION_PRIVADA", "TIPO_DE_INCAPACIDAD", "COMPENSACION_ORDINARIA",
                "COMPENSACION_EXTRAORDINARIA", "INTERESES_DE_CESANTIAS", "PRIMER_NOMBRE", "SEGUNDO_NOMBRE",
                "APELLIDO", "SEGUNDO_APELLIDO", "TIPO_DE_IDENTIFICACION", "IDENTIFICACION", "CORREO", "TELEFONO",
                "CODIGO_DEL_EMPLEADO", "CIUDAD", "DEPARTAMENTO", "DIRECCION", "SALARIO_INTEGRAL",
                "EMPLEADO_DE_ALTO_RIESGO", "MEDIO_DE_PAGO", "TIPO_DE_CONTRATO", "FECHA_DE_INICIO",
                "FECHA_DE_FINALIZACION", "SUB_CODIGO", "TIPO_DE_TRABAJADOR", "BANCO", "NUMERO_DE_CUENTA",
                "TIPO_DE_CUENTA", "NOTA"
            };
            ExcelTemplateHelper.WriteHeaderRow(sheet, 1, headers);

            // Dos filas de ejemplo con datos ficticios: BASICO (devengo) + SALUD (deducción) del mismo empleado.
            void SetCommon(int row)
            {
                sheet.Cell(row, 1).Value = "N";
                sheet.Cell(row, 2).Value = 1;
                sheet.Cell(row, 3).Value = 1;
                sheet.Cell(row, 4).Value = 2026;
                sheet.Cell(row, 5).Value = new DateTime(2026, 1, 31);
                sheet.Cell(row, 6).Value = new DateTime(2026, 1, 31);
                sheet.Cell(row, 7).Value = "MENSUAL";
                sheet.Cell(row, 8).Value = 1300000;
                sheet.Cell(row, 9).Value = new DateTime(2026, 1, 1);
                sheet.Cell(row, 10).Value = new DateTime(2026, 1, 31);
                sheet.Cell(row, 27).Value = "Juan";
                sheet.Cell(row, 29).Value = "Pérez";
                sheet.Cell(row, 31).Value = "CEDULA_DE_CIUDADANIA";
                sheet.Cell(row, 32).Value = "12345678";
                sheet.Cell(row, 33).Value = "juan.perez@ejemplo.com";
                sheet.Cell(row, 34).Value = "3001234567";
                sheet.Cell(row, 36).Value = "BOGOTA";
                sheet.Cell(row, 37).Value = "BOGOTA";
                sheet.Cell(row, 38).Value = "Calle 10 # 20-30";
                sheet.Cell(row, 39).Value = false;
                sheet.Cell(row, 40).Value = false;
                sheet.Cell(row, 41).Value = "EFECTIVO";
                sheet.Cell(row, 42).Value = "TERMINO_INDEFINIDO";
                sheet.Cell(row, 43).Value = new DateTime(2024, 1, 1);
                sheet.Cell(row, 46).Value = "NO_APLICA";
                sheet.Cell(row, 47).Value = "DEPENDIENTE";
            }
            SetCommon(2);
            sheet.Cell(2, 11).Value = "DEVENGADO";
            sheet.Cell(2, 12).Value = "BASICO";
            sheet.Cell(2, 13).Value = 1300000;
            sheet.Cell(2, 17).Value = 30;

            SetCommon(3);
            sheet.Cell(3, 11).Value = "DEDUCCION";
            sheet.Cell(3, 12).Value = "SALUD";
            sheet.Cell(3, 13).Value = 52000;
            sheet.Cell(3, 18).Value = 4;

            ExcelTemplateHelper.AddDropdown(sheet, "K2:K1000", "DEVENGADO", "DEDUCCION");

            var codesSheet = workbook.Worksheets.Add("CODIGOS_DEVENGADOS_DEDUCCIONES");
            ExcelTemplateHelper.WriteHeaderRow(codesSheet, 1, "TIPO", "CODIGO");
            var accrualCodes = new[]
            {
                "BASICO", "AUXILIO_DE_TRANSPORTE", "VIATICO", "HORA_EXTRA_DIURNA", "HORA_EXTRA_NOCTURNA",
                "HORA_RECARGO_NOCTURNO", "HORA_EXTRA_DIURNA_DF", "HORA_RECARGO_DIURNA_DF", "HORA_EXTRA_NOCTURNA_DF",
                "HORA_RECARGO_NOCTURNO_DF", "VACACION", "VACACION_COMPENSADA", "PRIMA", "CESANTIAS", "INCAPACIDAD",
                "LICENCIA_PATERNIDAD", "LICENCIA_REMUNERADA", "LICENCIA_NO_REMUNERADA", "BONIFICACION", "AUXILIO",
                "HUELGA_LEGAL", "OTRO_CONCEPTO", "COMPENSACION", "BONO_EPCTV", "BONO_EPCTV_ALIMENTACION", "COMISION",
                "PAGO_TERCERO", "ANTICIPO", "DOTACION", "APOYO_PRACTICA", "TELETRABAJO", "BONIFICACION_RETIRO",
                "INDEMNIZACION", "REINTEGRO"
            };
            var deductionCodes = new[]
            {
                "SALUD", "FONDO_PENSION", "FONDO_SUBSISTENCIA", "SINDICATO", "SANCION", "LIBRANZA", "PAGO_TERCERO",
                "ANTICIPO", "OTRA_DEDUCCION", "PENSION_VOLUNTARIA", "RETENCION_FUENTE", "AFC", "COOPERATIVA",
                "EMBARGO_FISCAL", "PLANES_COMPLEMENTARIOS", "EDUCACION", "REINTEGRO", "DEUDA", "FONDO_SOLIDARIDAD_PENSIONAL"
            };
            var codeRow = 2;
            foreach (var code in accrualCodes) { codesSheet.Cell(codeRow, 1).Value = "DEVENGADO"; codesSheet.Cell(codeRow, 2).Value = code; codeRow++; }
            foreach (var code in deductionCodes) { codesSheet.Cell(codeRow, 1).Value = "DEDUCCION"; codesSheet.Cell(codeRow, 2).Value = code; codeRow++; }
            codesSheet.Columns().AdjustToContents();

            sheet.Columns().AdjustToContents();
            var bytes = ExcelTemplateHelper.ToBytes(workbook);
            return File(bytes, ExcelTemplateHelper.XlsxContentType, "plantilla_nomina.xlsx");
        }

        // Formato estándar de carga masiva de Dataico (compatible con el archivo que ya tenga el
        // cliente en ese formato): una fila = un concepto (devengo o deducción); varias filas que
        // comparten PREFIJO+NUMERO forman un solo documento de nómina de un mismo empleado.
        // Las columnas se ubican por nombre de encabezado (fila 1), no por posición fija.
        [HttpPost("import")]
        public async Task<IActionResult> Import(IFormFile file)
        {
            try
            {
                var clientId = GetCurrentClientId();

                var client = await _dbContext.Clients.Include(c => c.Integrator).FirstOrDefaultAsync(c => c.Id == clientId);
                if (client == null) return NotFound("Emisor no encontrado.");

                if (client.Integrator.Code != "DATAICO")
                {
                    return BadRequest("Este emisor no tiene configurado un proveedor de documentos electrónicos habilitado.");
                }

                if (file == null || file.Length == 0)
                {
                    return BadRequest("Debes adjuntar un archivo Excel (.xlsx).");
                }

                var summary = new ImportSummary();
                using var stream = file.OpenReadStream();
                using var workbook = new XLWorkbook(stream);
                var sheet = workbook.Worksheets.First();

                var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var cell in sheet.Row(1).CellsUsed())
                {
                    var header = cell.GetString().Trim();
                    if (!string.IsNullOrEmpty(header)) columns[header] = cell.Address.ColumnNumber;
                }

                string GetText(IXLRow row, string col) => columns.TryGetValue(col, out var idx) && !row.Cell(idx).IsEmpty() ? row.Cell(idx).GetString().Trim() : string.Empty;
                decimal? GetDecimal(IXLRow row, string col) => columns.TryGetValue(col, out var idx) && !row.Cell(idx).IsEmpty() ? row.Cell(idx).GetValue<decimal>() : (decimal?)null;
                int? GetInt(IXLRow row, string col) => columns.TryGetValue(col, out var idx) && !row.Cell(idx).IsEmpty() ? (int)row.Cell(idx).GetValue<decimal>() : (int?)null;
                DateTime? GetDate(IXLRow row, string col)
                {
                    if (!columns.TryGetValue(col, out var idx) || row.Cell(idx).IsEmpty()) return null;
                    var cell = row.Cell(idx);
                    if (cell.TryGetValue<DateTime>(out var dt)) return dt;
                    return DateTime.TryParse(cell.GetString().Trim(), out var parsed) ? parsed : null;
                }
                string? GetDateText(IXLRow row, string col) => GetDate(row, col)?.ToString("dd/MM/yyyy");

                // Los datos del empleado y del período (columnas repetidas por fila dentro de un mismo
                // PREFIJO+NUMERO) no siempre vienen diligenciados en TODAS las filas del grupo — muchos
                // archivos reales solo los llenan en una de ellas. Por eso se busca el primer valor no
                // vacío en cualquier fila del grupo, en vez de depender únicamente de la primera fila.
                string GetTextAny(List<IXLRow> groupRows, string col) => groupRows.Select(r => GetText(r, col)).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? string.Empty;
                decimal? GetDecimalAny(List<IXLRow> groupRows, string col) => groupRows.Select(r => GetDecimal(r, col)).FirstOrDefault(v => v.HasValue);
                DateTime? GetDateAny(List<IXLRow> groupRows, string col) => groupRows.Select(r => GetDate(r, col)).FirstOrDefault(v => v.HasValue);
                bool GetBoolAny(List<IXLRow> groupRows, string col)
                {
                    foreach (var r in groupRows)
                    {
                        if (columns.TryGetValue(col, out var idx) && !r.Cell(idx).IsEmpty()) return r.Cell(idx).GetValue<bool>();
                    }
                    return false;
                }

                var rows = sheet.RowsUsed().Skip(1)
                    .Where(r => !string.IsNullOrWhiteSpace(GetText(r, "PREFIJO")) || !string.IsNullOrWhiteSpace(GetText(r, "NUMERO")))
                    .ToList();
                var groups = rows.GroupBy(r => (Prefijo: GetText(r, "PREFIJO"), Numero: GetText(r, "NUMERO")));

                foreach (var group in groups)
                {
                    var groupRows = group.ToList();
                    var firstRow = groupRows.First();
                    var rowRange = groupRows.Count == 1 ? $"{firstRow.RowNumber()}" : $"{firstRow.RowNumber()}-{groupRows.Last().RowNumber()}";
                    summary.TotalRows++;

                    try
                    {
                        var identification = GetTextAny(groupRows, "IDENTIFICACION");
                        if (string.IsNullOrWhiteSpace(identification))
                        {
                            summary.Results.Add(new ImportRowResult { Row = firstRow.RowNumber(), Success = false, Message = $"Fila(s) {rowRange}: falta la identificación del empleado." });
                            summary.Failed++;
                            continue;
                        }

                        var employee = await _dbContext.Customers.FirstOrDefaultAsync(c => c.ClientId == clientId && c.IdentificationNumber == identification && c.PartyType == PartyType.Empleado);
                        if (employee == null)
                        {
                            var firstName = GetTextAny(groupRows, "PRIMER_NOMBRE");
                            var secondName = GetTextAny(groupRows, "SEGUNDO_NOMBRE");
                            var firstLastName = GetTextAny(groupRows, "APELLIDO");
                            var secondLastName = GetTextAny(groupRows, "SEGUNDO_APELLIDO");
                            var name = string.Join(" ", new[] { firstName, secondName, firstLastName, secondLastName }.Where(s => !string.IsNullOrEmpty(s)));
                            employee = new Customer
                            {
                                Id = Guid.NewGuid(),
                                ClientId = clientId,
                                Name = name,
                                FirstName = firstName,
                                SecondName = secondName,
                                FirstLastName = firstLastName,
                                SecondLastName = secondLastName,
                                IdentificationType = GetTextAny(groupRows, "TIPO_DE_IDENTIFICACION"),
                                IdentificationNumber = identification,
                                PartyType = PartyType.Empleado,
                                Email = GetTextAny(groupRows, "CORREO"),
                                Phone = GetTextAny(groupRows, "TELEFONO"),
                                Address = GetTextAny(groupRows, "DIRECCION"),
                                CityName = GetTextAny(groupRows, "CIUDAD"),
                                WorkerType = GetTextAny(groupRows, "TIPO_DE_TRABAJADOR"),
                                ContractType = GetTextAny(groupRows, "TIPO_DE_CONTRATO"),
                                PaymentMeans = GetTextAny(groupRows, "MEDIO_DE_PAGO"),
                                Bank = GetTextAny(groupRows, "BANCO"),
                                AccountType = GetTextAny(groupRows, "TIPO_DE_CUENTA"),
                                AccountNumber = GetTextAny(groupRows, "NUMERO_DE_CUENTA"),
                                HighRisk = GetBoolAny(groupRows, "EMPLEADO_DE_ALTO_RIESGO"),
                                IntegralSalary = GetBoolAny(groupRows, "SALARIO_INTEGRAL"),
                                BaseSalary = GetDecimalAny(groupRows, "SALARIO"),
                                StartDate = GetDateAny(groupRows, "FECHA_DE_INICIO"),
                                FireDate = GetDateAny(groupRows, "FECHA_DE_FINALIZACION"),
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                            };
                            _dbContext.Customers.Add(employee);
                        }

                        var accruals = new List<PayrollConceptItem>();
                        var deductions = new List<PayrollConceptItem>();

                        foreach (var row in groupRows)
                        {
                            var tipo = GetText(row, "TIPO").ToUpperInvariant();
                            var code = GetText(row, "CODIGO");
                            if (string.IsNullOrEmpty(code)) continue;

                            var amount = GetDecimal(row, "VALOR_SALARIAL");
                            if (code == "SANCION")
                            {
                                amount ??= GetDecimal(row, "SANCION_PUBLICA") ?? GetDecimal(row, "SANCION_PRIVADA");
                            }

                            var concept = new PayrollConceptItem
                            {
                                Code = code,
                                Description = GetText(row, "DESCRIPCION"),
                                Amount = amount,
                                AmountNs = GetDecimal(row, "VALOR_NO_SALARIAL"),
                                Days = GetInt(row, "DIAS"),
                                Percentage = GetDecimal(row, "PORCENTAJE"),
                                Hours = GetDecimal(row, "HORAS"),
                                InitialDate = GetDateText(row, "FECHA_DE_INICIO_DEL_DEVENGADO"),
                                FinalDate = GetDateText(row, "FECHA_FINAL_DEL_DEVENGADO"),
                                MedicalLeaveType = GetText(row, "TIPO_DE_INCAPACIDAD"),
                                CesantiasInterest = GetDecimal(row, "INTERESES_DE_CESANTIAS"),
                                OrdinaryCompensation = GetDecimal(row, "COMPENSACION_ORDINARIA"),
                                ExtraordinaryCompensation = GetDecimal(row, "COMPENSACION_EXTRAORDINARIA")
                            };

                            if (tipo == "DEDUCCION") deductions.Add(concept); else accruals.Add(concept);
                        }

                        if (accruals.Count == 0 && deductions.Count == 0)
                        {
                            summary.Results.Add(new ImportRowResult { Row = firstRow.RowNumber(), Success = false, Message = $"Fila(s) {rowRange}: no se encontraron conceptos válidos (columna CODIGO vacía)." });
                            summary.Failed++;
                            continue;
                        }

                        var accrualsTotal = accruals.Sum(a => (a.Amount ?? 0) + (a.AmountNs ?? 0));
                        var deductionsTotal = deductions.Sum(d => d.Amount ?? 0);

                        var prefix = string.IsNullOrEmpty(group.Key.Prefijo) ? "N" : group.Key.Prefijo;
                        var initialSettlement = GetDateAny(groupRows, "LIQUIDACION_INICIO") ?? Fel.Core.Models.ColombiaTime.Now;
                        var finalSettlement = GetDateAny(groupRows, "LIQUIDACION_FINAL") ?? Fel.Core.Models.ColombiaTime.Now;
                        var paymentDate = GetDateAny(groupRows, "FECHA_PAGO") ?? Fel.Core.Models.ColombiaTime.Now;

                        // La carga masiva solo deja los documentos en borrador — el cliente los revisa
                        // y los emite uno a uno (o los edita) desde la lista, igual que si los hubiera
                        // digitado a mano. Nunca se envían a Dataico automáticamente desde el import.
                        var document = new Document
                        {
                            Id = Guid.NewGuid(),
                            ClientId = clientId,
                            BranchId = GetCurrentBranchId(),
                            CustomerId = employee.Id,
                            Customer = employee,
                            TypeCode = "NE",
                            Number = string.IsNullOrEmpty(group.Key.Numero) ? DateTime.UtcNow.Ticks.ToString() : group.Key.Numero,
                            Status = "DRAFT",
                            CreatedAt = DateTime.UtcNow,
                            IssueDate = Fel.Core.Models.ColombiaTime.Now,
                            Subtotal = accrualsTotal,
                            TaxAmount = deductionsTotal,
                            TotalAmount = accrualsTotal - deductionsTotal,
                            SectorExtensionData = JsonSerializer.Serialize(new PayrollDraftData
                            {
                                Prefix = prefix,
                                InitialSettlementDate = initialSettlement,
                                FinalSettlementDate = finalSettlement,
                                PaymentDate = paymentDate,
                                Accruals = accruals,
                                Deductions = deductions
                            })
                        };

                        _dbContext.Documents.Add(document);
                        await _dbContext.SaveChangesAsync();

                        summary.Results.Add(new ImportRowResult
                        {
                            Row = firstRow.RowNumber(),
                            Success = true,
                            Message = $"Fila(s) {rowRange}: borrador de nómina de {employee.Name} creado. Revísalo y emítelo desde la lista."
                        });
                        summary.Succeeded++;
                    }
                    catch (Exception ex)
                    {
                        summary.Results.Add(new ImportRowResult { Row = firstRow.RowNumber(), Success = false, Message = $"Fila(s) {rowRange}: {ex.Message}" });
                        summary.Failed++;
                    }
                }

                return Ok(summary);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        // Documentos de la sucursal activa (todas las del usuario si eligió "todas"), para consultar.
        private IQueryable<Document> BranchDocuments => _dbContext.Documents.ForBranch(CurrentBranchScope);

        private static Document BuildDraftDocument(Guid clientId, Guid branchId, Guid employeeId, CreatePayrollRequest request)
        {
            var accruals = request.Accruals ?? new List<PayrollConceptItem>();
            var deductions = request.Deductions ?? new List<PayrollConceptItem>();
            var accrualsTotal = accruals.Sum(a => (a.Amount ?? 0) + (a.AmountNs ?? 0));
            var deductionsTotal = deductions.Sum(d => d.Amount ?? 0);

            var draft = new PayrollDraftData
            {
                Prefix = string.IsNullOrWhiteSpace(request.Prefix) ? "NE" : request.Prefix,
                InitialSettlementDate = request.InitialSettlementDate,
                FinalSettlementDate = request.FinalSettlementDate,
                PaymentDate = request.PaymentDate,
                Accruals = accruals,
                Deductions = deductions
            };

            return new Document
            {
                Id = Guid.NewGuid(),
                ClientId = clientId,
                BranchId = branchId,
                CustomerId = employeeId,
                TypeCode = request.NoteType == "ELIMINACION" ? DeletionTypeCode : request.NoteType == "REEMPLAZO" ? ReplacementTypeCode : TypeCode,
                Number = DateTime.UtcNow.Ticks.ToString(),
                CreatedAt = DateTime.UtcNow,
                IssueDate = Fel.Core.Models.ColombiaTime.Now,
                Subtotal = accrualsTotal,
                TaxAmount = deductionsTotal,
                TotalAmount = accrualsTotal - deductionsTotal,
                SectorExtensionData = JsonSerializer.Serialize(draft),
                ReferenceDocumentId = request.ReferenceDocumentId,
                ReferenceConcept = request.ReferenceConcept
            };
        }

        private static PayrollDraftData DeserializeDraft(string sectorExtensionData)
        {
            if (string.IsNullOrWhiteSpace(sectorExtensionData) || sectorExtensionData == "{}") return new PayrollDraftData();
            return JsonSerializer.Deserialize<PayrollDraftData>(sectorExtensionData) ?? new PayrollDraftData();
        }

        private async Task<Fel.Infrastructure.Dataico.Models.DataicoResult> SubmitPayrollToDataicoAsync(
            Document document, Customer employee, Fel.Core.Entities.Client client,
            string prefix, long number, DateTime initialSettlement, DateTime finalSettlement, DateTime paymentDate,
            List<PayrollConceptItem> accruals, List<PayrollConceptItem> deductions)
        {
            var dataicoRequest = DataicoDocumentMapper.BuildPayrollRequest(
                employee, client, prefix, number, initialSettlement, finalSettlement, Fel.Core.Models.ColombiaTime.Now, paymentDate,
                accruals.Select(ToConceptInput), deductions.Select(ToConceptInput));

            var credentials = DataicoDocumentMapper.ToCredentials(client, _cryptoService);
            var result = await _dataicoApiService.SendPayrollAsync(dataicoRequest, credentials);

            document.Status = result.Success ? "APPROVED" : "REJECTED";
            document.DianResponseMessage = result.Success ? result.RawResponse : (result.ErrorMessage ?? result.RawResponse);
            document.Cufe = result.Cufe; // CUNE de nómina — antes no se guardaba, necesario para Notas de Eliminación/Reemplazo.
            document.QrCode = result.QrCode;
            document.ProcessedAt = DateTime.UtcNow;

            return result;
        }

        private static DataicoDocumentMapper.PayrollConceptInput ToConceptInput(PayrollConceptItem c) => new()
        {
            Code = c.Code,
            Description = c.Description,
            Amount = c.Amount,
            AmountNs = c.AmountNs,
            Days = c.Days,
            Percentage = c.Percentage,
            Hours = c.Hours,
            InitialDate = c.InitialDate,
            FinalDate = c.FinalDate,
            MedicalLeaveType = c.MedicalLeaveType,
            CesantiasInterest = c.CesantiasInterest,
            OrdinaryCompensation = c.OrdinaryCompensation,
            ExtraordinaryCompensation = c.ExtraordinaryCompensation
        };

        private class PayrollDraftData
        {
            public string Prefix { get; set; } = "NE";
            public DateTime InitialSettlementDate { get; set; }
            public DateTime FinalSettlementDate { get; set; }
            public DateTime PaymentDate { get; set; }
            public List<PayrollConceptItem> Accruals { get; set; } = new();
            public List<PayrollConceptItem> Deductions { get; set; } = new();
        }
    }

    public class PayrollConceptItem
    {
        public string? Code { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal? Amount { get; set; }
        public decimal? AmountNs { get; set; }
        public int? Days { get; set; }
        public decimal? Percentage { get; set; }
        public decimal? Hours { get; set; }
        public string? InitialDate { get; set; }
        public string? FinalDate { get; set; }
        public string? MedicalLeaveType { get; set; }
        public decimal? CesantiasInterest { get; set; }
        public decimal? OrdinaryCompensation { get; set; }
        public decimal? ExtraordinaryCompensation { get; set; }
    }

    public class CreatePayrollRequest
    {
        public Guid EmployeeId { get; set; }
        public string Prefix { get; set; } = "NE";
        public DateTime InitialSettlementDate { get; set; }
        public DateTime FinalSettlementDate { get; set; }
        public DateTime PaymentDate { get; set; }
        public List<PayrollConceptItem>? Accruals { get; set; }
        public List<PayrollConceptItem>? Deductions { get; set; }

        // Presentes solo cuando este comprobante es una nota sobre otro ya emitido.
        public Guid? ReferenceDocumentId { get; set; }
        // "ELIMINACION" o "REEMPLAZO" — determina a cuál de los dos TypeCode internos se asigna.
        public string? NoteType { get; set; }
        public string? ReferenceConcept { get; set; }
    }
}
