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
        private readonly IFacilReportsClient _facilReportsClient;

        public PayrollController(FelDbContext dbContext, ICryptoService cryptoService, IDataicoApiService dataicoApiService, DataicoCustomPdfService customPdfService, IFacilReportsClient facilReportsClient)
        {
            _facilReportsClient = facilReportsClient;
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

                var annulments = await AnnulmentsAsync(clientId, entries.Select(d => d.Id).ToList());

                // document.Number solo es el consecutivo real una vez que Dataico aprueba la nómina; mientras es borrador o está
                // rechazada es el marcador de creación, así que no se muestra.
                var result = entries.Select(d =>
                {
                    var draft = DeserializeDraft(d.SectorExtensionData);
                    return new
                    {
                        d.Id,
                        d.Status,
                        d.TotalAmount,
                        // Subtotal es el devengado y TaxAmount las deducciones (así se guardan al crear el borrador).
                        TotalAccruals = d.Subtotal,
                        TotalDeductions = d.TaxAmount,
                        d.IssueDate,
                        d.TypeCode,
                        AnnulledBy = annulments.TryGetValue(d.Id, out var annulledBy) ? annulledBy : null,
                        d.DianResponseMessage,
                        Customer = d.Customer,
                        draft.Prefix,
                        ConsecutiveNumber = d.Status == "APPROVED" ? $"{draft.Prefix}-{d.Number}" : null,
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

        // Vista previa impresa (borrador o ya emitida) con la plantilla de nómina del cliente vía Facil Reports; no depende de que
        // el documento tenga CUNE. Mismo contrato que InvoiceController.Preview.
        [HttpGet("{id}/preview")]
        public async Task<IActionResult> Preview(Guid id)
        {
            try
            {
                var clientId = GetCurrentClientId();
                var document = await BranchDocuments
                    .Include(d => d.Customer)
                    .FirstOrDefaultAsync(d => d.Id == id && d.ClientId == clientId && (d.TypeCode == TypeCode || d.TypeCode == DeletionTypeCode || d.TypeCode == ReplacementTypeCode));
                if (document == null) return NotFound("Nómina no encontrada.");

                var client = await _dbContext.Clients.Include(c => c.Integrator).FirstOrDefaultAsync(c => c.Id == clientId);
                if (client == null) return NotFound("Emisor no encontrado.");

                // El pago usa el tipo "NE-PAGO"; las notas de eliminación y reemplazo, el de ajuste.
                var documentTypeCode = document.TypeCode == TypeCode ? "NE-PAGO" : "NE-AJUSTE";
                var documentType = await _dbContext.DocumentTypes.FirstOrDefaultAsync(t => t.Code == documentTypeCode);
                var template = documentType == null ? null : await DocumentTemplateResolver.ResolveAsync(_dbContext, client, documentType.Id);
                if (template == null)
                {
                    return BadRequest("Aún no hay una plantilla de impresión configurada para este tipo de documento.");
                }

                var draft = DeserializeDraft(document.SectorExtensionData);
                var approved = document.Status == "APPROVED";
                var location = await BranchProvisioning.LocationAsync(_dbContext, client, document.BranchId);
                var (workerTypes, contractTypes) = await GetEmployeeCatalogsAsync();
                var data = NominaReportDataMapper.Build(
                    document, document.Customer!, client, location, draft.Prefix, approved && long.TryParse(document.Number, out var number) ? number : 0,
                    draft.InitialSettlementDate, draft.FinalSettlementDate, draft.PaymentDate,
                    draft.Accruals.SelectMany(ToReportConcepts), draft.Deductions.SelectMany(ToReportConcepts),
                    workerTypes, contractTypes);
                // Mientras Dataico no apruebe no hay consecutivo (se asigna al aprobar): el borrador se identifica como tal.
                if (!approved && data["Documento"] is Dictionary<string, object?> header) header["DocumentoNumero"] = "Borrador";

                var pdfBytes = await _facilReportsClient.GenerateReportAsync(template.RepxTemplateKey, data);
                if (pdfBytes == null)
                {
                    return BadRequest("No se pudo generar la vista previa. Intenta de nuevo en unos segundos.");
                }

                return File(pdfBytes, "application/pdf");
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        // Un concepto del borrador puede dar una o dos filas del PDF. El valor no siempre está en Amount:
        // las compensaciones y las sanciones guardan sus partes por separado, y los intereses de cesantías
        // (con el porcentaje, que es el de los intereses) van en su propia fila, en la columna no salarial.
        private static IEnumerable<NominaConcepto> ToReportConcepts(PayrollConceptItem c)
        {
            var salarial = c.Code?.ToUpperInvariant() switch
            {
                "COMPENSACION" => (c.OrdinaryCompensation ?? 0m) + (c.ExtraordinaryCompensation ?? 0m),
                "SANCION" => c.Amount ?? (c.PrivateSanction ?? 0m) + (c.PublicSanction ?? 0m),
                _ => c.Amount ?? 0m
            };
            var conIntereses = string.Equals(c.Code, "CESANTIAS", StringComparison.OrdinalIgnoreCase) && c.CesantiasInterest is > 0;

            yield return new NominaConcepto(c.Code, c.Description, salarial, c.AmountNs ?? 0m, c.Days, conIntereses ? null : c.Percentage, c.Hours);
            if (conIntereses)
            {
                yield return new NominaConcepto("INTERESES_DE_CESANTIAS", "Intereses de cesantías", 0m, c.CesantiasInterest!.Value, null, c.Percentage);
            }
        }

        // Tipo de trabajador y de contrato los administra Superadmin en el catálogo global (TaxCatalogItem):
        // se resuelven acá para que el PDF muestre la etiqueta legible y no el código crudo.
        private async Task<(Dictionary<string, string> WorkerTypes, Dictionary<string, string> ContractTypes)> GetEmployeeCatalogsAsync()
        {
            var workerTypes = await _dbContext.TaxCatalogItems.Where(i => i.Kind == TaxCatalogKind.WorkerType).ToDictionaryAsync(i => i.Category, i => i.Name);
            var contractTypes = await _dbContext.TaxCatalogItems.Where(i => i.Kind == TaxCatalogKind.ContractType).ToDictionaryAsync(i => i.Category, i => i.Name);
            return (workerTypes, contractTypes);
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
                var annulments = await AnnulmentsAsync(clientId, new List<Guid> { document.Id });
                return Ok(new
                {
                    document.Id,
                    document.Status,
                    document.TotalAmount,
                    document.CreatedAt,
                    document.IssueDate,
                    document.TypeCode,
                    AnnulledBy = annulments.TryGetValue(document.Id, out var annulledBy) ? annulledBy : null,
                    Number = document.Status == "APPROVED" ? document.Number : null,
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
                    .Select(d => new { d.Id, Number = d.Status == "APPROVED" ? d.Number : null, d.TypeCode, d.Status, d.TotalAmount, d.IssueDate })
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

                var noteError = await ValidateNoteAsync(clientId, request.NoteType, request.ReferenceDocumentId, employee.Id, null);
                if (noteError != null) return BadRequest(noteError);

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

                // Una nota conserva su tipo y su comprobante de referencia; lo único que se edita es el motivo.
                var isNote = document.TypeCode != TypeCode;
                if (isNote)
                {
                    var noteError = await ValidateNoteAsync(clientId, document.TypeCode == DeletionTypeCode ? "ELIMINACION" : "REEMPLAZO", document.ReferenceDocumentId, employee.Id, document.Id);
                    if (noteError != null) return BadRequest(noteError);
                    document.ReferenceConcept = request.ReferenceConcept ?? string.Empty;
                }

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

                var identificationTypes = await _dbContext.IdentificationTypes.AsNoTracking().ToListAsync();

                // Dataico exige ciudad y departamento del empleado, y salen del código DANE. Se valida antes de reservar el consecutivo
                // para no gastar un número en un envío que Dataico rechazaría. La nota de eliminación no lleva datos del empleado.
                if (document.TypeCode != DeletionTypeCode && (document.Customer?.CityCode ?? string.Empty).Trim().Length < 5)
                {
                    return BadRequest($"El empleado {document.Customer?.Name} no tiene un código DANE de municipio válido (5 dígitos). Edítalo en Empleados e indica el código DANE.");
                }

                // El tipo de documento del empleado se envía con el nombre que usa la API de nómina de Dataico, que se parametriza en el
                // catálogo de tipos de identificación (datos maestros).
                if (document.TypeCode != DeletionTypeCode && IdentificationTypeResolver.PayrollCode(identificationTypes, document.Customer?.IdentificationType) == null)
                {
                    return BadRequest($"El tipo de documento '{document.Customer?.IdentificationType}' del empleado {document.Customer?.Name} no tiene equivalencia para nómina electrónica. Corrígelo en el empleado o configura la equivalencia en Datos maestros > Tipos de identificación.");
                }

                // Nómina no tiene resolución DIAN real (el consecutivo lo administra el empleador,
                // Anexo Técnico Nómina Electrónica), pero igual necesita ser secuencial de verdad —
                // se reutiliza la misma entidad Resolution (DocumentType="NE") solo para llevar el
                // NextNumber, con el mismo mecanismo atómico que ya usan las facturas.
                long documentNumber;
                Guid? pendingNumberResolutionId = null;
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

                    // El consecutivo no se reserva al intentar: se envía el que sigue y solo queda asignado si Dataico aprueba. Si rechaza,
                    // el documento no guarda número y el siguiente intento (de este u otro documento) usa el mismo.
                    documentNumber = await Fel.Infrastructure.Services.ResolutionNumbering.PeekNextNumberAsync(_dbContext, payrollResolution.Id);
                    pendingNumberResolutionId = payrollResolution.Id;
                }
                else
                {
                    // Reservado en un intento anterior, de cuando el número se tomaba antes de enviar: se conserva para no dejar un hueco.
                    documentNumber = long.Parse(document.Number);
                }

                var draft = DeserializeDraft(document.SectorExtensionData);
                // El prefijo queda guardado en el documento (la nómina lo lleva en su borrador).
                document.Prefix = draft.Prefix;
                Fel.Infrastructure.Dataico.Models.DataicoResult result;

                // Anular y modificar emiten lo mismo ante Dataico (una nota de eliminación). La diferencia es que, al modificar, cuando
                // Dataico aprueba se crea un borrador nuevo con los datos del original para corregirlo y emitirlo después.
                Document? originalDocument = null;
                if (document.TypeCode == DeletionTypeCode || document.TypeCode == ReplacementTypeCode)
                {
                    if (!document.ReferenceDocumentId.HasValue)
                    {
                        return BadRequest("Esta nota debe referenciar el comprobante de nómina que ajusta.");
                    }
                    originalDocument = await _dbContext.Documents.FindAsync(document.ReferenceDocumentId.Value);
                    if (originalDocument == null || string.IsNullOrEmpty(originalDocument.Cufe))
                    {
                        return BadRequest("El comprobante de nómina original no tiene un CUNE registrado (puede no haberse emitido por Dataico). No se puede generar la nota.");
                    }
                    if ((await AnnulmentsAsync(clientId, new List<Guid> { originalDocument.Id })).ContainsKey(originalDocument.Id))
                    {
                        return BadRequest("Este comprobante de nómina ya fue anulado.");
                    }
                    var originalDraft = DeserializeDraft(originalDocument.SectorExtensionData);
                    // Nóminas emitidas antes de este cambio no tienen un Number real (era un
                    // placeholder de creación, nunca el consecutivo que de verdad se envió a
                    // Dataico) — no hay forma de recuperar ese número histórico real.
                    var originalNumber = long.Parse(originalDocument.Number);
                    var credentials = DataicoDocumentMapper.ToCredentials(client, _cryptoService);

                    var deletionRequest = DataicoDocumentMapper.BuildPayrollDeletionRequest(
                        client, draft.Prefix, documentNumber, Fel.Core.Models.ColombiaTime.Now,
                        originalDraft.Prefix, originalNumber, originalDocument.Cufe, originalDocument.IssueDate, document.ReferenceConcept);
                    result = await _dataicoApiService.SendPayrollDeletionAsync(deletionRequest, credentials);

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
                        draft.Accruals, draft.Deductions, identificationTypes);
                }

                if (result.Success && pendingNumberResolutionId.HasValue)
                {
                    // Dataico aprobó: recién ahora el número queda asignado al documento y consumido de la numeración.
                    document.Number = documentNumber.ToString();
                    document.ResolutionId = pendingNumberResolutionId;
                    await Fel.Infrastructure.Services.ResolutionNumbering.CommitNumberAsync(_dbContext, pendingNumberResolutionId.Value, documentNumber);
                }

                Document? regeneratedDraft = null;
                if (result.Success && document.TypeCode == ReplacementTypeCode && originalDocument != null)
                {
                    regeneratedDraft = BuildRegeneratedDraft(originalDocument);
                    _dbContext.Documents.Add(regeneratedDraft);
                }

                if (result.Success)
                {
                    document.IntegratorId = client.IntegratorId;
                    // Sin el id de Dataico y el tipo de documento el paso del PDF por correo nunca corría para la nómina.
                    document.DataicoDocumentId = result.DataicoDocumentId;
                    var documentTypeCode = document.TypeCode == TypeCode ? "NE-PAGO" : "NE-AJUSTE";
                    document.DocumentTypeId = await _dbContext.DocumentTypes.Where(t => t.Code == documentTypeCode).Select(t => (Guid?)t.Id).FirstOrDefaultAsync();

                    var pdfCredentials = DataicoDocumentMapper.ToCredentials(client, _cryptoService);
                    var pdfLocation = await BranchProvisioning.LocationAsync(_dbContext, client, document.BranchId);
                    var (workerTypes, contractTypes) = await GetEmployeeCatalogsAsync();
                    await _customPdfService.TrySendCustomPdfAsync(
                        document, document.Customer?.Email, client,
                        _ => NominaReportDataMapper.Build(
                            document, document.Customer!, client, pdfLocation, draft.Prefix, documentNumber,
                            draft.InitialSettlementDate, draft.FinalSettlementDate, draft.PaymentDate,
                            draft.Accruals.SelectMany(ToReportConcepts), draft.Deductions.SelectMany(ToReportConcepts),
                            workerTypes, contractTypes),
                        pdfCredentials);
                }

                await _dbContext.SaveChangesAsync();

                if (!result.Success)
                {
                    return BadRequest(new { message = "Dataico rechazó la nómina.", detail = document.DianResponseMessage });
                }

                return Ok(new { message = "Nómina electrónica emitida correctamente.", document, newDraftId = regeneratedDraft?.Id });
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

        // Emisión en lote: los documentos seleccionados se emiten de a uno, en orden de creación, con la misma lógica de "{id}/publish".
        [HttpPost("batch/publish")]
        public async Task<IActionResult> BatchPublish([FromBody] BatchRequest request)
        {
            var invalid = ValidateBatch(request, out var ids);
            if (invalid != null) return invalid;

            var clientId = GetCurrentClientId();
            await StreamBatchAsync(_dbContext, ids, BranchDocuments.Where(d => d.ClientId == clientId && ids.Contains(d.Id) && (d.TypeCode == TypeCode || d.TypeCode == DeletionTypeCode || d.TypeCode == ReplacementTypeCode)).OrderBy(d => d.CreatedAt).Select(d => d.Id), id => Publish(id));
            return new EmptyResult();
        }

        // Eliminación en lote de borradores y rechazados, con la misma validación de "DELETE {id}".
        [HttpPost("batch/delete")]
        public async Task<IActionResult> BatchDelete([FromBody] BatchRequest request)
        {
            var invalid = ValidateBatch(request, out var ids);
            if (invalid != null) return invalid;

            var clientId = GetCurrentClientId();
            await StreamBatchAsync(_dbContext, ids, BranchDocuments.Where(d => d.ClientId == clientId && ids.Contains(d.Id) && (d.TypeCode == TypeCode || d.TypeCode == DeletionTypeCode || d.TypeCode == ReplacementTypeCode)).OrderBy(d => d.CreatedAt).Select(d => d.Id), id => DeleteDraft(id));
            return new EmptyResult();
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
                var identificationTypes = await _dbContext.IdentificationTypes.AsNoTracking().ToListAsync();
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
                                // Se guarda el código DIAN del catálogo sea cual sea la forma en que venga en el Excel (13, CC, CEDULA_DE_CIUDADANIA...).
                                IdentificationType = IdentificationTypeResolver.Find(identificationTypes, GetTextAny(groupRows, "TIPO_DE_IDENTIFICACION"))?.Code ?? GetTextAny(groupRows, "TIPO_DE_IDENTIFICACION"),
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
                            var privateSanction = code == "SANCION" ? GetDecimal(row, "SANCION_PRIVADA") : null;
                            var publicSanction = code == "SANCION" ? GetDecimal(row, "SANCION_PUBLICA") : null;
                            if (code == "SANCION")
                            {
                                // Dataico recibe las dos sanciones por separado; el monto sirve para los totales del borrador.
                                amount ??= (privateSanction ?? 0) + (publicSanction ?? 0);
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
                                ExtraordinaryCompensation = GetDecimal(row, "COMPENSACION_EXTRAORDINARIA"),
                                PrivateSanction = privateSanction,
                                PublicSanction = publicSanction
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

        // Comprobantes anulados: los que tienen una nota de eliminación o de reemplazo aprobada por Dataico. Devuelve, por cada uno,
        // el tipo de la nota que lo anuló.
        private async Task<Dictionary<Guid, string>> AnnulmentsAsync(Guid clientId, List<Guid> ids)
        {
            var notes = await _dbContext.Documents
                .Where(d => d.ClientId == clientId && d.ReferenceDocumentId != null && ids.Contains(d.ReferenceDocumentId.Value)
                            && d.Status == "APPROVED" && (d.TypeCode == DeletionTypeCode || d.TypeCode == ReplacementTypeCode))
                .Select(d => new { Original = d.ReferenceDocumentId!.Value, d.TypeCode })
                .ToListAsync();
            return notes.GroupBy(n => n.Original).ToDictionary(g => g.Key, g => g.First().TypeCode);
        }

        // Reglas para crear o editar una nota de eliminación/reemplazo: el comprobante debe existir, estar emitido, ser del mismo empleado
        // y no tener ya otra nota emitida o en curso. Devuelve el mensaje de error o null si todo está bien.
        private async Task<string?> ValidateNoteAsync(Guid clientId, string? noteType, Guid? referenceId, Guid employeeId, Guid? currentNoteId)
        {
            if (string.IsNullOrEmpty(noteType)) return null;
            if (noteType != "ELIMINACION" && noteType != "REEMPLAZO") return "El tipo de nota no es válido.";
            if (!referenceId.HasValue) return "La nota debe indicar el comprobante de nómina que anula.";

            var original = await _dbContext.Documents.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == referenceId.Value && d.ClientId == clientId && d.TypeCode == TypeCode);
            if (original == null) return "El comprobante de nómina que se quiere anular no existe.";
            if (original.Status != "APPROVED" || string.IsNullOrEmpty(original.Cufe)) return "Solo se puede anular o modificar una nómina que ya fue emitida.";
            if (original.CustomerId != employeeId) return "La nota debe ser del mismo empleado del comprobante original.";

            var existing = await _dbContext.Documents.AsNoTracking()
                .Where(d => d.ClientId == clientId && d.ReferenceDocumentId == referenceId && d.Id != currentNoteId
                            && (d.TypeCode == DeletionTypeCode || d.TypeCode == ReplacementTypeCode) && d.Status != "REJECTED")
                .Select(d => d.Status)
                .FirstOrDefaultAsync();
            if (existing == "APPROVED") return "Este comprobante de nómina ya fue anulado.";
            if (existing != null) return "Este comprobante de nómina ya tiene una nota en borrador o en proceso. Úsala o elimínala antes de crear otra.";
            return null;
        }

        // Borrador nuevo con los datos del comprobante anulado, para que el usuario lo corrija y lo emita. Queda enlazado al original.
        private static Document BuildRegeneratedDraft(Document original) => new()
        {
            Id = Guid.NewGuid(),
            ClientId = original.ClientId,
            BranchId = original.BranchId,
            CustomerId = original.CustomerId,
            TypeCode = TypeCode,
            Status = "DRAFT",
            Number = DateTime.UtcNow.Ticks.ToString(),
            CreatedAt = DateTime.UtcNow,
            IssueDate = Fel.Core.Models.ColombiaTime.Now,
            Subtotal = original.Subtotal,
            TaxAmount = original.TaxAmount,
            TotalAmount = original.TotalAmount,
            SectorExtensionData = original.SectorExtensionData,
            ReferenceDocumentId = original.Id,
            ReferenceConcept = string.Empty
        };

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
                // Solo una nota referencia otro comprobante; el enlace de un borrador regenerado lo pone el servidor, no el cliente.
                ReferenceDocumentId = request.NoteType is "ELIMINACION" or "REEMPLAZO" ? request.ReferenceDocumentId : null,
                // La columna no admite nulos: un borrador que no es nota llega sin concepto.
                ReferenceConcept = request.NoteType is "ELIMINACION" or "REEMPLAZO" ? request.ReferenceConcept ?? string.Empty : string.Empty
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
            List<PayrollConceptItem> accruals, List<PayrollConceptItem> deductions, IReadOnlyCollection<IdentificationType> identificationTypes)
        {
            var dataicoRequest = DataicoDocumentMapper.BuildPayrollRequest(
                employee, client, prefix, number, initialSettlement, finalSettlement, Fel.Core.Models.ColombiaTime.Now, paymentDate,
                accruals.Select(ToConceptInput), deductions.Select(ToConceptInput), identificationTypes);

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
            ExtraordinaryCompensation = c.ExtraordinaryCompensation,
            PrivateSanction = c.PrivateSanction,
            PublicSanction = c.PublicSanction
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
        public decimal? PrivateSanction { get; set; }
        public decimal? PublicSanction { get; set; }
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
