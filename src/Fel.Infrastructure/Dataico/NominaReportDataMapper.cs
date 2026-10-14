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
    // Salarial y NoSalarial son las dos columnas de valor; el total de la fila es su suma.
    public record NominaConcepto(string? Codigo, string Descripcion, decimal Salarial, decimal NoSalarial,
        int? Dias = null, decimal? Porcentaje = null, decimal? Horas = null);

    // Mismo contrato que Invoice/SupportDocumentReportDataMapper ("Documento" + listas), adaptado a
    // Nómina Electrónica: no hay "adquirente" sino un empleado, no hay IVA ni retenciones, y el detalle
    // son dos bloques de filas, "Devengos" y "Deducciones", con las columnas {Concepto, Porcentaje, Dias,
    // Salarial, NoSalarial, Total}. Cada bloque cierra con una fila de total ("TotalDevengos" y
    // "TotalDeducciones", vacías si el bloque no tiene filas). Los totales vienen ya calculados, no como
    // sumas dentro del reporte, para no depender de la configuración de Summary de DevExpress, y salen de
    // las mismas filas que se imprimen: lo impreso siempre cuadra.
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
            var totalDevengado = devengosList.Sum(d => d.Salarial + d.NoSalarial);
            var totalDeduccion = deduccionesList.Sum(d => d.Salarial + d.NoSalarial);
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

            // Una columna de valor en cero queda en blanco; el total de la fila siempre se imprime.
            string Opcional(decimal value) => value == 0 ? string.Empty : Money(value);

            // Las deducciones restan: su total sale con signo, como en el resto de la representación.
            Dictionary<string, object?> Fila(NominaConcepto c, bool deduccion)
            {
                var total = c.Salarial + c.NoSalarial;
                return new Dictionary<string, object?>
                {
                    ["Concepto"] = NombreConcepto(c, nf),
                    ["Porcentaje"] = c.Porcentaje.HasValue ? c.Porcentaje.Value.ToString("0.##", nf) : string.Empty,
                    ["Dias"] = c.Dias.HasValue ? c.Dias.Value.ToString(nf) : string.Empty,
                    ["Salarial"] = Opcional(c.Salarial),
                    ["NoSalarial"] = Opcional(c.NoSalarial),
                    ["Total"] = deduccion ? "- " + Money(total) : Money(total)
                };
            }

            List<Dictionary<string, object?>> FilaTotal(string etiqueta, List<NominaConcepto> conceptos, bool deduccion)
            {
                if (conceptos.Count == 0) return new();
                var total = conceptos.Sum(c => c.Salarial + c.NoSalarial);
                return new()
                {
                    new Dictionary<string, object?>
                    {
                        ["Etiqueta"] = etiqueta,
                        ["Salarial"] = Opcional(conceptos.Sum(c => c.Salarial)),
                        ["NoSalarial"] = Opcional(conceptos.Sum(c => c.NoSalarial)),
                        ["Total"] = deduccion ? "- " + Money(total) : Money(total)
                    }
                };
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

                ["Devengos"] = devengosList.Select(d => Fila(d, false)).ToList(),
                ["TotalDevengos"] = FilaTotal("Total Devengados", devengosList, false),
                ["Deducciones"] = deduccionesList.Select(d => Fila(d, true)).ToList(),
                ["TotalDeducciones"] = FilaTotal("Total Deducciones", deduccionesList, true)
            };
        }

        // Las palabras de los códigos de Dataico que llevan tilde; los códigos vienen sin ellas.
        private static readonly Dictionary<string, string> Tildes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["basico"] = "básico", ["bonificacion"] = "bonificación", ["comision"] = "comisión",
            ["compensacion"] = "compensación", ["cesantias"] = "cesantías", ["deduccion"] = "deducción",
            ["dotacion"] = "dotación", ["educacion"] = "educación", ["indemnizacion"] = "indemnización",
            ["pension"] = "pensión", ["practica"] = "práctica", ["retencion"] = "retención",
            ["sancion"] = "sanción", ["viatico"] = "viático", ["alimentacion"] = "alimentación",
            ["vacacion"] = "vacación", ["intereses"] = "intereses"
        };
        private static readonly HashSet<string> Articulos = new(StringComparer.OrdinalIgnoreCase) { "de", "del", "la", "el", "y", "en", "por" };

        // La descripción libre si el concepto la trae; si no, el código de Dataico vuelto legible
        // ("AUXILIO_DE_TRANSPORTE" -> "Auxilio de transporte"). Las horas, si las hay, van entre paréntesis.
        private static string NombreConcepto(NominaConcepto c, IFormatProvider nf)
        {
            string nombre;
            if (!string.IsNullOrWhiteSpace(c.Descripcion)) nombre = c.Descripcion.Trim();
            else if (string.IsNullOrWhiteSpace(c.Codigo)) nombre = string.Empty;
            else
            {
                var palabras = c.Codigo.ToLowerInvariant().Split('_', StringSplitOptions.RemoveEmptyEntries)
                    .Select(w => Tildes.TryGetValue(w, out var conTilde) ? conTilde : w).ToArray();
                if (palabras.Length > 0) palabras[0] = char.ToUpperInvariant(palabras[0][0]) + palabras[0][1..];
                nombre = string.Join(' ', palabras);
            }
            return c.Horas.HasValue ? $"{nombre} ({c.Horas.Value.ToString("0.##", nf)} h)" : nombre;
        }
    }
}
