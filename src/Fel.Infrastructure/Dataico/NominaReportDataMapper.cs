using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Fel.Core.Entities;
using Fel.Core.Models;
using Fel.Infrastructure.Services;

namespace Fel.Infrastructure.Dataico
{
    // Concepto de nómina tal como lo recibe el mapper. Desacoplado de PayrollConceptItem (que vive en
    // Fel.Api.Client, una capa por encima de Fel.Infrastructure) a propósito: el llamador lo arma.
    public record NominaConcepto(string? Codigo, string Descripcion, decimal Valor, int? Dias = null, decimal? Porcentaje = null);

    // Mismo contrato que Invoice/SupportDocumentReportDataMapper ("Documento" + lista), adaptado a
    // Nómina Electrónica: no hay "adquirente" sino un empleado, no hay IVA ni retenciones, y el
    // detalle es una sola tabla de conceptos, "Conceptos", con {Concepto, Devengado, Deduccion}: cada
    // renglón llena una de las dos columnas de valor (devengos primero, luego deducciones). Los totales
    // (Total Devengado/Total Deducción/Neto) vienen ya calculados en "Documento", no como sumas dentro
    // del reporte, para no depender de la configuración de Summary de DevExpress.
    public static class NominaReportDataMapper
    {
        // Facil Factura es un producto de SoFactory S.A.S., el fabricante legal del software.
        private const string FabricanteSoftwareNombre = "SoFactory S.A.S.";
        private const string FabricanteSoftwareNit = "900.303.194-6";
        private const string NombreSoftware = "Facil Factura";


        public static Dictionary<string, object?> Build(
            Document document, Customer employee, Client client, EmitterLocation location,
            string prefix, long numeroConsecutivo,
            DateTime initialSettlement, DateTime finalSettlement, DateTime paymentDate,
            IEnumerable<NominaConcepto> devengos,
            IEnumerable<NominaConcepto> deducciones,
            IReadOnlyDictionary<string, string>? workerTypeCatalog = null,
            IReadOnlyDictionary<string, string>? contractTypeCatalog = null)
        {
            // Separadores del cliente (Client.DecimalSeparator): punto decimal y coma de miles por defecto.
            var nf = ReportNumberFormat.For(client.DecimalSeparator);
            string Money(decimal value) => value.ToString("N0", nf);
            var esIntegradorExterno = client.Integrator.Kind == IntegratorKind.ThirdPartyIntegrator;
            var devengosList = devengos.ToList();
            var deduccionesList = deducciones.ToList();
            var totalDevengado = devengosList.Sum(d => d.Valor);
            var totalDeduccion = deduccionesList.Sum(d => d.Valor);
            var neto = totalDevengado - totalDeduccion;

            var esNota = document.TypeCode is "NE-ELIMINACION" or "NE-REEMPLAZO";
            // Denominación exacta exigida por el Art. 5 num. 1 de la Resolución 000013 de 2021
            // (Anexo Técnico del Documento Soporte de Pago de Nómina Electrónica) — no es un
            // nombre libre, es el texto literal que exige la norma.
            var titulo = document.TypeCode switch
            {
                "NE-ELIMINACION" => "NOTA DE ELIMINACIÓN - DOCUMENTO SOPORTE DE PAGO DE NÓMINA ELECTRÓNICA",
                "NE-REEMPLAZO" => "NOTA DE REEMPLAZO - DOCUMENTO SOPORTE DE PAGO DE NÓMINA ELECTRÓNICA",
                _ => "DOCUMENTO SOPORTE DE PAGO DE NÓMINA ELECTRÓNICA"
            };
            var medioPago = string.Join(" · ", new[] { employee.PaymentMeans, employee.Bank, employee.AccountType, employee.AccountNumber }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

            // "Salud (4 %)", "Salario (30 días)": el código y los detalles que ya guarda el concepto.
            string Texto(NominaConcepto c)
            {
                var detalle = new List<string>();
                if (c.Dias.HasValue) detalle.Add($"{c.Dias} días");
                if (c.Porcentaje.HasValue) detalle.Add($"{c.Porcentaje.Value.ToString("0.##", nf)} %");
                var nombre = string.IsNullOrWhiteSpace(c.Codigo) ? c.Descripcion : $"{c.Codigo} · {c.Descripcion}";
                return detalle.Count == 0 ? nombre : $"{nombre} ({string.Join(" · ", detalle)})";
            }

            return new Dictionary<string, object?>
            {
                ["Documento"] = new Dictionary<string, object?>
                {
                    // Empleador
                    ["EmisorLogoUrl"] = client.LogoLightUrl,
                    ["EmisorRazonSocial"] = client.CompanyName,
                    ["EmisorNit"] = client.TaxId,
                    ["EmisorDireccion"] = location.Address,
                    ["EmisorCiudad"] = location.City,
                    ["EmisorTelefono"] = location.Phone,
                    ["EmisorEmail"] = location.Email,
                    ["SucursalNombre"] = location.BranchName,
                    ["SucursalCodigo"] = location.BranchCode,
                    ["EmisorActividadEconomica"] = client.EconomicActivity,
                    ["EmisorRegimenRenta"] = IncomeTaxRegimes.Label(client.IncomeTaxRegime),
                    ["EmisorGranContribuyente"] = InvoiceReportDataMapper.ResponsabilidadTexto(client.IsGranContribuyente, "Gran Contribuyente"),
                    ["EmisorAgenteRetenedorIva"] = InvoiceReportDataMapper.ResponsabilidadTexto(client.IsAgenteRetenedorIva, "Agente Retenedor del Impuesto sobre las Ventas - IVA"),
                    ["EmisorAutorretenedorRenta"] = InvoiceReportDataMapper.ResponsabilidadTexto(client.IsAutorretenedorRenta, "Autorretenedor del Impuesto sobre la Renta y Complementarios"),

                    // Trabajador
                    ["EmpleadoNombre"] = employee.Name,
                    ["EmpleadoTipoIdentificacion"] = employee.IdentificationType,
                    ["EmpleadoIdentificacion"] = employee.IdentificationNumber,
                    ["EmpleadoDireccion"] = employee.Address,
                    ["EmpleadoCiudad"] = employee.CityName,
                    ["EmpleadoCodigoDane"] = employee.CityCode,
                    ["EmpleadoEmail"] = employee.Email,
                    ["EmpleadoContrato"] = InvoiceReportDataMapper.CatalogLabel(employee.ContractType, contractTypeCatalog),
                    ["EmpleadoTipoTrabajador"] = InvoiceReportDataMapper.CatalogLabel(employee.WorkerType, workerTypeCatalog),
                    ["EmpleadoSalarioBase"] = employee.BaseSalary.HasValue ? Money(employee.BaseSalary.Value) : string.Empty,

                    // Documento ("DocumentoTipo", no "DocumentoTitulo": mismo nombre que Factura)
                    ["DocumentoTipo"] = titulo,
                    ["DocumentoNumero"] = $"{prefix}-{numeroConsecutivo}",
                    ["NotaReferencia"] = esNota && !string.IsNullOrWhiteSpace(document.ReferenceConcept) ? $"Motivo: {document.ReferenceConcept}" : null,
                    ["FechaGeneracion"] = Fel.Core.Models.ColombiaTime.FromUtc(document.CreatedAt).ToString("dd/MM/yyyy HH:mm:ss"),
                    ["PeriodoTexto"] = $"{initialSettlement:dd/MM/yyyy} — {finalSettlement:dd/MM/yyyy}",
                    ["FechaPago"] = paymentDate.ToString("dd/MM/yyyy"),
                    ["MedioPago"] = medioPago,
                    ["Cune"] = document.Cufe,
                    ["QrCode"] = document.QrCode ?? document.Cufe,
                    // URL del QR (ver InvoiceReportDataMapper): Facil Reports la reconoce y genera el QR
                    // localmente; con el motor DevExpress se descarga de ese servicio.
                    ["QrImageUrl"] = QrImageDataUri.FromText(document.QrCode ?? document.Cufe),

                    ["TotalDevengado"] = Money(totalDevengado),
                    ["TotalDeduccion"] = Money(totalDeduccion),
                    ["NetoPagar"] = Money(neto),
                    ["NetoPagarEnLetras"] = NumberToWordsEs.ConvertirPesos(neto),

                    ["FabricanteSoftwareNombre"] = FabricanteSoftwareNombre,
                    ["FabricanteSoftwareNit"] = FabricanteSoftwareNit,
                    ["NombreSoftware"] = NombreSoftware,
                    // Art. 5 num. 13 de la Resolución 000013 de 2021 pide además el "software ID" —
                    // en DIAN directa es el SoftwareId propio del Cliente (su habilitación); con un
                    // integrador externo no aplica un id propio del Cliente, así que queda vacío.
                    ["SoftwareId"] = esIntegradorExterno ? null : client.SoftwareId,
                    ["ProveedorTecnologicoNombre"] = esIntegradorExterno ? client.Integrator.Name : null,
                    ["ProveedorTecnologicoNit"] = esIntegradorExterno ? client.Integrator.Nit : null
                },

                ["Conceptos"] = devengosList
                    .Select(d => new Dictionary<string, object?> { ["Concepto"] = Texto(d), ["Devengado"] = Money(d.Valor), ["Deduccion"] = string.Empty })
                    .Concat(deduccionesList.Select(d => new Dictionary<string, object?> { ["Concepto"] = Texto(d), ["Devengado"] = string.Empty, ["Deduccion"] = Money(d.Valor) }))
                    .ToList()
            };
        }
    }
}
