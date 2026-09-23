using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Superadmin.Controllers
{
    [ApiController]
    [Route("api/superadmin/templates")]
    public class SuperadminTemplatesController : ControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly IFacilReportsClient _facilReportsClient;

        public SuperadminTemplatesController(FelDbContext dbContext, IFacilReportsClient facilReportsClient)
        {
            _dbContext = dbContext;
            _facilReportsClient = facilReportsClient;
        }

        private static async Task<byte[]> ReadBytesAsync(IFormFile file)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            return ms.ToArray();
        }

        // GET: api/superadmin/templates
        [HttpGet]
        public async Task<IActionResult> GetGlobalTemplates()
        {
            try
            {
                var templates = await _dbContext.DocumentTemplates
                    .Include(t => t.DocumentType)
                    .Where(t => t.TenantId == null && t.ClientId == null) // Globales exclusivas del Superadmin
                    .OrderByDescending(t => t.CreatedAt)
                    .Select(t => new
                    {
                        id = t.Id,
                        name = t.Name,
                        repxTemplateKey = t.RepxTemplateKey,
                        status = t.Status.ToString(),
                        versionNumber = t.VersionNumber,
                        documentTypeId = t.DocumentTypeId,
                        documentType = t.DocumentType != null ? t.DocumentType.Name : "N/A",
                        previousVersionId = t.PreviousVersionId
                    })
                    .ToListAsync();

                return Ok(templates);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // GET: api/superadmin/templates/by-type/{documentTypeId} — plantillas globales de un solo
        // tipo de documento, para la pantalla de "Modelos de Diseño" de ese tipo.
        [HttpGet("by-type/{documentTypeId:guid}")]
        public async Task<IActionResult> GetTemplatesByType(Guid documentTypeId)
        {
            try
            {
                var templates = await _dbContext.DocumentTemplates
                    .Where(t => t.TenantId == null && t.ClientId == null && t.DocumentTypeId == documentTypeId)
                    .OrderByDescending(t => t.CreatedAt)
                    .Select(t => new
                    {
                        id = t.Id,
                        name = t.Name,
                        repxTemplateKey = t.RepxTemplateKey,
                        version = t.VersionNumber,
                        status = t.Status.ToString(),
                        createdAt = t.CreatedAt,
                        updatedAt = t.UpdatedAt
                    })
                    .ToListAsync();

                return Ok(templates);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: api/superadmin/templates/{id}/preview — renderiza el .repx con datos de ejemplo
        // (no reales), para poder ver cómo se ve el diseño sin necesitar un documento emitido.
        [HttpPost("{id:guid}/preview")]
        public async Task<IActionResult> Preview(Guid id)
        {
            try
            {
                var template = await _dbContext.DocumentTemplates
                    .Include(t => t.DocumentType)
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (template == null) return NotFound("Plantilla no encontrada.");
                if (string.IsNullOrWhiteSpace(template.RepxTemplateKey))
                {
                    return BadRequest("Esta plantilla aún no tiene un archivo .repx asociado.");
                }

                var data = BuildSampleData(template.DocumentType?.Code ?? "");
                var pdfBytes = await _facilReportsClient.GenerateReportAsync(template.RepxTemplateKey, data);
                if (pdfBytes == null)
                {
                    return BadRequest("No se pudo generar la vista previa. Revisa que Facil Reports esté disponible.");
                }

                return File(pdfBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        // Datos ficticios pero realistas — mismo contrato de nombres planos que
        // InvoiceReportDataMapper/SupportDocumentReportDataMapper/NominaReportDataMapper, para que
        // la vista previa se vea igual de completa que un documento real.
        private static System.Collections.Generic.Dictionary<string, object?> BuildSampleData(string documentTypeCode)
        {
            if (documentTypeCode.StartsWith("DS")) return SampleSupportDocumentData(documentTypeCode);
            if (documentTypeCode.StartsWith("NE")) return SampleNominaData(documentTypeCode);
            return SampleInvoiceData(documentTypeCode);
        }

        private static System.Collections.Generic.Dictionary<string, object?> SampleInvoiceData(string documentTypeCode)
        {
            var documentoTipo = documentTypeCode switch
            {
                "NC" => "NOTA CRÉDITO ELECTRÓNICA",
                "ND" => "NOTA DÉBITO ELECTRÓNICA",
                "DE-POS" => "DOCUMENTO EQUIVALENTE - TIQUETE POS",
                _ => "FACTURA ELECTRÓNICA DE VENTA"
            };
            var esNota = documentTypeCode is "NC" or "ND";

            return new System.Collections.Generic.Dictionary<string, object?>
            {
                ["EmisorLogoUrl"] = "",
                ["EmisorRazonSocial"] = "Comercializadora Ejemplo S.A.S.",
                ["EmisorNombreComercial"] = "Tienda Ejemplo",
                ["EmisorNit"] = "900123456",
                ["EmisorDv"] = "7",
                ["EmisorDireccion"] = "Calle 10 # 20-30",
                ["EmisorCiudad"] = "Bogotá D.C.",
                ["EmisorTelefono"] = "601 555 1234",
                ["EmisorEmail"] = "facturacion@ejemplo.com",
                ["EmisorCalidadTributaria"] = "Responsable de IVA",

                ["AdquirenteNombre"] = "Juan Pérez Gómez",
                ["AdquirenteTipoIdentificacion"] = "CC",
                ["AdquirenteIdentificacion"] = "1234567890",
                ["AdquirenteDireccion"] = "Carrera 15 # 40-50",
                ["AdquirenteCiudad"] = "Bogotá D.C.",
                ["AdquirenteTelefono"] = "310 555 6789",
                ["AdquirenteEmail"] = "juan.perez@correo.com",

                ["DocumentoTipo"] = documentoTipo,
                ["DocumentoNumero"] = "SETP 1",
                ["ResolucionTexto"] = "Resolución DIAN 18760000001 · Rango SETP 1-5000 · Vigente hasta 31/12/2027",
                ["FechaGeneracion"] = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                ["FechaVencimiento"] = DateTime.Now.AddDays(30).ToString("dd/MM/yyyy"),
                ["Cufe"] = "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4",
                ["MedioPago"] = "10",
                ["FormaPago"] = "Contado",
                ["OrdenCompra"] = "OC-2026-001",
                ["Notas"] = "Documento de ejemplo — vista previa de diseño.",
                ["NotaReferencia"] = esNota ? "Ajusta el documento N° SETP 1 · CUFE a1b2c3d4e5f6...ejemplo · Motivo: Devolución parcial" : null,

                ["Subtotal"] = "168.000",
                ["Iva"] = "31.920",
                ["Descuento"] = "0",
                ["Cargo"] = "0",
                ["Total"] = "199.920",
                ["TotalEnLetras"] = "CIENTO NOVENTA Y NUEVE MIL NOVECIENTOS VEINTE PESOS M/CTE",

                ["FabricanteSoftwareNombre"] = "SoFactory S.A.S.",
                ["FabricanteSoftwareNit"] = "900.303.194-6",
                ["NombreSoftware"] = "Facil Factura",
                ["ProveedorTecnologicoNombre"] = null,
                ["ProveedorTecnologicoNit"] = null,

                ["DataSource"] = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object?>>
                {
                    new() { ["Codigo"] = "PRD001", ["Nombre"] = "Producto de ejemplo A", ["Cantidad"] = "2", ["Unidad"] = "Unidad", ["ValorUnitario"] = "50.000", ["PorcentajeIva"] = "19%", ["ValorIva"] = "19.000", ["TotalLinea"] = "119.000" },
                    new() { ["Codigo"] = "PRD002", ["Nombre"] = "Producto de ejemplo B", ["Cantidad"] = "1", ["Unidad"] = "Unidad", ["ValorUnitario"] = "68.000", ["PorcentajeIva"] = "19%", ["ValorIva"] = "12.920", ["TotalLinea"] = "80.920" }
                }
            };
        }

        private static System.Collections.Generic.Dictionary<string, object?> SampleSupportDocumentData(string documentTypeCode)
        {
            var esAjuste = documentTypeCode == "DS-AJUSTE";
            return new System.Collections.Generic.Dictionary<string, object?>
            {
                ["EmisorLogoUrl"] = "",
                ["EmisorRazonSocial"] = "Comercializadora Ejemplo S.A.S.",
                ["EmisorNombreComercial"] = "Tienda Ejemplo",
                ["EmisorNit"] = "900123456",
                ["EmisorDv"] = "7",
                ["EmisorDireccion"] = "Calle 10 # 20-30",
                ["EmisorCiudad"] = "Bogotá D.C.",
                ["EmisorTelefono"] = "601 555 1234",
                ["EmisorEmail"] = "facturacion@ejemplo.com",
                ["EmisorCalidadTributaria"] = "Responsable de IVA",

                ["ProveedorNombre"] = "Distribuidora Proveedor Ltda.",
                ["ProveedorTipoIdentificacion"] = "NIT",
                ["ProveedorIdentificacion"] = "800987654",
                ["ProveedorDireccion"] = "Avenida 30 # 5-15",
                ["ProveedorCiudad"] = "Medellín",
                ["ProveedorTelefono"] = "604 555 4321",
                ["ProveedorEmail"] = "ventas@proveedor.com",

                ["DocumentoTitulo"] = esAjuste ? "NOTA DE AJUSTE - DOCUMENTO SOPORTE" : "DOCUMENTO SOPORTE DE PAGO",
                ["DocumentoNumero"] = "DS 1",
                ["ResolucionTexto"] = "Resolución DIAN 18760000002 · Rango DS 1-5000 · Vigente hasta 31/12/2027",
                ["FechaGeneracion"] = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                ["Cufe"] = "b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5",
                ["MedioPago"] = "10",
                ["FormaPago"] = "Contado",
                ["OrdenCompra"] = "OC-2026-002",
                ["ReferenciaAjuste"] = esAjuste ? "Corrección de valores — documento de ejemplo" : null,
                ["Notas"] = "Documento de ejemplo — vista previa de diseño.",

                ["Subtotal"] = "120.000",
                ["Iva"] = "0",
                ["Descuento"] = "0",
                ["Total"] = "120.000",
                ["TotalEnLetras"] = "CIENTO VEINTE MIL PESOS M/CTE",

                ["FabricanteSoftwareNombre"] = "SoFactory S.A.S.",
                ["FabricanteSoftwareNit"] = "900.303.194-6",
                ["NombreSoftware"] = "Facil Factura",
                ["ProveedorTecnologicoNombre"] = null,
                ["ProveedorTecnologicoNit"] = null,

                ["DataSource"] = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object?>>
                {
                    new() { ["Codigo"] = "SRV001", ["Nombre"] = "Servicio de ejemplo", ["Cantidad"] = "1", ["Unidad"] = "Unidad", ["ValorUnitario"] = "120.000", ["PorcentajeIva"] = "0%", ["ValorIva"] = "0", ["TotalLinea"] = "120.000" }
                }
            };
        }

        private static System.Collections.Generic.Dictionary<string, object?> SampleNominaData(string documentTypeCode)
        {
            var esNota = documentTypeCode == "NE-AJUSTE";
            return new System.Collections.Generic.Dictionary<string, object?>
            {
                ["EmisorLogoUrl"] = "",
                ["EmisorRazonSocial"] = "Comercializadora Ejemplo S.A.S.",
                ["EmisorNit"] = "900123456",
                ["EmisorDireccion"] = "Calle 10 # 20-30",
                ["EmisorCiudad"] = "Bogotá D.C.",
                ["EmisorTelefono"] = "601 555 1234",
                ["EmisorEmail"] = "nomina@ejemplo.com",

                ["EmpleadoNombre"] = "María Rodríguez López",
                ["EmpleadoTipoIdentificacion"] = "CC",
                ["EmpleadoIdentificacion"] = "1098765432",
                ["EmpleadoDireccion"] = "Calle 80 # 10-20",
                ["EmpleadoCiudad"] = "Bogotá D.C.",

                ["DocumentoTitulo"] = esNota ? "NOTA DE AJUSTE - NÓMINA ELECTRÓNICA" : "NÓMINA ELECTRÓNICA",
                ["DocumentoNumero"] = "NE-1",
                ["ReferenciaAjuste"] = esNota ? "Corrección de devengados — documento de ejemplo" : null,
                ["FechaGeneracion"] = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                ["PeriodoTexto"] = $"{DateTime.Now.AddDays(-30):dd/MM/yyyy} — {DateTime.Now:dd/MM/yyyy}",
                ["FechaPago"] = DateTime.Now.ToString("dd/MM/yyyy"),
                ["MedioPago"] = "Consignación",
                ["Cune"] = "c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6",

                ["TotalDevengado"] = "2.500.000",
                ["TotalDeduccion"] = "200.000",
                ["NetoPagar"] = "2.300.000",
                ["NetoPagarEnLetras"] = "DOS MILLONES TRESCIENTOS MIL PESOS M/CTE",

                ["FabricanteSoftwareNombre"] = "SoFactory S.A.S.",
                ["FabricanteSoftwareNit"] = "900.303.194-6",
                ["NombreSoftware"] = "Facil Factura",
                ["SoftwareId"] = "SOFT-EJEMPLO-0001",
                ["ProveedorTecnologicoNombre"] = null,
                ["ProveedorTecnologicoNit"] = null,

                ["DataSource"] = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object?>>
                {
                    new() { ["Tipo"] = "DEVENGADOS", ["Codigo"] = "001", ["Descripcion"] = "Salario básico", ["Valor"] = "2.300.000" },
                    new() { ["Tipo"] = "DEVENGADOS", ["Codigo"] = "002", ["Descripcion"] = "Auxilio de transporte", ["Valor"] = "200.000" },
                    new() { ["Tipo"] = "DEDUCCIONES", ["Codigo"] = "101", ["Descripcion"] = "Salud", ["Valor"] = "100.000" },
                    new() { ["Tipo"] = "DEDUCCIONES", ["Codigo"] = "102", ["Descripcion"] = "Pensión", ["Valor"] = "100.000" }
                }
            };
        }

        public class CreateTemplateRequest
        {
            public string Name { get; set; } = string.Empty;
            public Guid DocumentTypeId { get; set; }
            public string RepxTemplateKey { get; set; } = string.Empty;
        }

        // POST: api/superadmin/templates
        [HttpPost]
        public async Task<IActionResult> CreateGlobalTemplate([FromBody] CreateTemplateRequest request)
        {
            try
            {
                var documentType = await _dbContext.DocumentTypes.FindAsync(request.DocumentTypeId);
                if (documentType == null) return NotFound("Tipo de documento no encontrado.");

                var newTemplate = new DocumentTemplate
                {
                    Id = Guid.NewGuid(),
                    Name = request.Name,
                    RepxTemplateKey = request.RepxTemplateKey,
                    Status = TemplateStatus.Draft,
                    VersionNumber = 1,
                    DocumentTypeId = request.DocumentTypeId,
                    TenantId = null,
                    ClientId = null,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.DocumentTemplates.Add(newTemplate);
                await _dbContext.SaveChangesAsync();

                return Ok(new { message = "Plantilla global creada en estado Borrador.", templateId = newTemplate.Id });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: api/superadmin/templates/upload — sube el archivo .repx directamente a Facil
        // Reports y crea la plantilla en un solo paso, en vez de exigir subirlo aparte (a mano,
        // por curl/Postman) y pegar aquí la clave resultante.
        [HttpPost("upload")]
        [RequestSizeLimit(20_000_000)]
        public async Task<IActionResult> UploadNewTemplate([FromForm] UploadTemplateForm form)
        {
            try
            {
                if (form.File == null || form.File.Length == 0)
                {
                    return BadRequest("Debes adjuntar un archivo .repx.");
                }

                if (!form.File.FileName.EndsWith(".repx", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest("El archivo debe tener extensión .repx.");
                }

                var documentType = await _dbContext.DocumentTypes.FindAsync(form.DocumentTypeId);
                if (documentType == null) return NotFound("Tipo de documento no encontrado.");

                var templateKey = $"facilfactura_{documentType.Code}_{Guid.NewGuid():N}".ToLowerInvariant();
                var bytes = await ReadBytesAsync(form.File);

                if (!await _facilReportsClient.UploadTemplateAsync(templateKey, bytes))
                {
                    return StatusCode(502, "No se pudo subir el archivo a Facil Reports. Revisa que el servicio esté configurado y disponible.");
                }

                var newTemplate = new DocumentTemplate
                {
                    Id = Guid.NewGuid(),
                    Name = form.Name,
                    RepxTemplateKey = templateKey,
                    Status = TemplateStatus.Draft,
                    VersionNumber = 1,
                    DocumentTypeId = form.DocumentTypeId,
                    TenantId = null,
                    ClientId = null,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.DocumentTemplates.Add(newTemplate);
                await _dbContext.SaveChangesAsync();

                return Ok(new { message = "Plantilla subida y creada en estado Borrador.", templateId = newTemplate.Id, templateKey });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        public class UploadTemplateForm
        {
            public IFormFile File { get; set; } = null!;
            public string Name { get; set; } = string.Empty;
            public Guid DocumentTypeId { get; set; }
        }

        public class PublishRequest
        {
            public string NewRepxTemplateKey { get; set; } = string.Empty;
        }

        // PUT: api/superadmin/templates/{id}/publish
        [HttpPut("{id:guid}/publish")]
        public async Task<IActionResult> PublishTemplate(Guid id, [FromBody] PublishRequest request)
        {
            try
            {
                var template = await _dbContext.DocumentTemplates
                    .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == null && t.ClientId == null);

                if (template == null)
                    return NotFound("Plantilla global no encontrada.");

                if (template.Status == TemplateStatus.Published)
                    return BadRequest("La plantilla ya está publicada.");

                if (template.Status == TemplateStatus.Archived)
                    return BadRequest("No puedes publicar una plantilla archivada.");

                if (!string.IsNullOrWhiteSpace(request.NewRepxTemplateKey))
                {
                    template.RepxTemplateKey = request.NewRepxTemplateKey;
                }

                // Si esta plantilla es una nueva versión de otra, archivar la vieja para evitar duplicados activos
                if (template.PreviousVersionId.HasValue)
                {
                    var previousTemplate = await _dbContext.DocumentTemplates.FindAsync(template.PreviousVersionId.Value);
                    if (previousTemplate != null && previousTemplate.Status == TemplateStatus.Published)
                    {
                        previousTemplate.Status = TemplateStatus.Archived;
                        previousTemplate.UpdatedAt = DateTime.UtcNow;
                    }
                }

                template.Status = TemplateStatus.Published;
                template.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                return Ok(new { message = "Plantilla global publicada correctamente. Ya es inmutable y visible para Tenants." });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        public class NewVersionRequest
        {
            public string NewRepxTemplateKey { get; set; } = string.Empty;
        }

        // POST: api/superadmin/templates/{id}/new-version
        [HttpPost("{id:guid}/new-version")]
        public async Task<IActionResult> CreateNewVersion(Guid id, [FromBody] NewVersionRequest request)
        {
            try
            {
                var sourceTemplate = await _dbContext.DocumentTemplates
                    .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == null && t.ClientId == null);

                if (sourceTemplate == null)
                    return NotFound("Plantilla global origen no encontrada.");

                if (sourceTemplate.Status != TemplateStatus.Published)
                    return BadRequest("Solo puedes versionar plantillas que estén Publicadas.");

                var newVersionTemplate = new DocumentTemplate
                {
                    Id = Guid.NewGuid(),
                    Name = sourceTemplate.Name, // Mantiene el nombre lógico
                    RepxTemplateKey = string.IsNullOrWhiteSpace(request.NewRepxTemplateKey) ? sourceTemplate.RepxTemplateKey : request.NewRepxTemplateKey,
                    Status = TemplateStatus.Draft,
                    VersionNumber = sourceTemplate.VersionNumber + 1,
                    PreviousVersionId = sourceTemplate.Id,
                    DocumentTypeId = sourceTemplate.DocumentTypeId,
                    TenantId = null,
                    ClientId = null,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.DocumentTemplates.Add(newVersionTemplate);
                await _dbContext.SaveChangesAsync();

                return Ok(new { message = $"Versión {newVersionTemplate.VersionNumber} creada en estado Borrador.", templateId = newVersionTemplate.Id });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: api/superadmin/templates/{id}/upload-new-version — mismo atajo que /upload, pero
        // versionando una plantilla global ya Publicada.
        [HttpPost("{id:guid}/upload-new-version")]
        [RequestSizeLimit(20_000_000)]
        public async Task<IActionResult> UploadNewVersion(Guid id, IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest("Debes adjuntar un archivo .repx.");
                }

                if (!file.FileName.EndsWith(".repx", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest("El archivo debe tener extensión .repx.");
                }

                var sourceTemplate = await _dbContext.DocumentTemplates
                    .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == null && t.ClientId == null);

                if (sourceTemplate == null)
                    return NotFound("Plantilla global origen no encontrada.");

                if (sourceTemplate.Status != TemplateStatus.Published)
                    return BadRequest("Solo puedes versionar plantillas que estén Publicadas.");

                var templateKey = $"facilfactura_v{sourceTemplate.VersionNumber + 1}_{Guid.NewGuid():N}".ToLowerInvariant();
                var bytes = await ReadBytesAsync(file);

                if (!await _facilReportsClient.UploadTemplateAsync(templateKey, bytes))
                {
                    return StatusCode(502, "No se pudo subir el archivo a Facil Reports. Revisa que el servicio esté configurado y disponible.");
                }

                var newVersionTemplate = new DocumentTemplate
                {
                    Id = Guid.NewGuid(),
                    Name = sourceTemplate.Name,
                    RepxTemplateKey = templateKey,
                    Status = TemplateStatus.Draft,
                    VersionNumber = sourceTemplate.VersionNumber + 1,
                    PreviousVersionId = sourceTemplate.Id,
                    DocumentTypeId = sourceTemplate.DocumentTypeId,
                    TenantId = null,
                    ClientId = null,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.DocumentTemplates.Add(newVersionTemplate);
                await _dbContext.SaveChangesAsync();

                return Ok(new { message = $"Versión {newVersionTemplate.VersionNumber} subida en estado Borrador.", templateId = newVersionTemplate.Id, templateKey });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // DELETE: api/superadmin/templates/{id} — solo borradores sin versiones posteriores; una
        // plantilla Publicada se retira archivándola al publicar la siguiente versión, no borrándola.
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteTemplate(Guid id)
        {
            try
            {
                var template = await _dbContext.DocumentTemplates
                    .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == null && t.ClientId == null);
                if (template == null) return NotFound();

                if (template.Status == TemplateStatus.Published)
                {
                    return BadRequest("No puedes eliminar una plantilla publicada. Súbele una nueva versión para reemplazarla.");
                }

                if (await _dbContext.DocumentTemplates.AnyAsync(t => t.PreviousVersionId == id))
                {
                    return BadRequest("No puedes eliminar esta plantilla porque tiene una versión posterior que depende de ella.");
                }

                _dbContext.DocumentTemplates.Remove(template);
                await _dbContext.SaveChangesAsync();

                return Ok(new { message = "Plantilla eliminada." });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
