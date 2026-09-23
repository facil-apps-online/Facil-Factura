using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Fel.Core.Entities;
using Fel.Infrastructure.Services;

namespace Fel.Infrastructure.Dataico
{
    // Mismo contrato plano que Invoice/SupportDocumentReportDataMapper, adaptado a Nómina
    // Electrónica: no hay "adquirente" sino un empleado, y en vez de ítems con IVA hay conceptos
    // de devengo/deducción. Facil Reports solo soporta una lista (DataSource) por reporte, así que
    // devengos y deducciones van en la MISMA lista con un campo Tipo ("DEVENGADO"/"DEDUCCION") — el
    // .repx los separa visualmente con un GroupHeaderBand agrupado por ese campo. Los totales por
    // grupo (Total Devengado/Total Deducción/Neto) vienen ya calculados como Parameters aparte, no
    // como sumas calculadas dentro del reporte, para no depender de la configuración de Summary de
    // DevExpress (que no podemos validar sin el Report Designer real).
    //
    // Desacoplado de PayrollConceptItem (que vive en Fel.Api.Client, una capa por encima de
    // Fel.Infrastructure) a propósito: el llamador arma las tuplas simples.
    public static class NominaReportDataMapper
    {
        // Facil Factura es un producto de SoFactory S.A.S., el fabricante legal del software.
        private const string FabricanteSoftwareNombre = "SoFactory S.A.S.";
        private const string FabricanteSoftwareNit = "900.303.194-6";
        private const string NombreSoftware = "Facil Factura";

        private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");
        private static string Money(decimal value) => value.ToString("N0", Co);

        public static Dictionary<string, object?> Build(
            Document document, Customer employee, Client client,
            string prefix, long numeroConsecutivo,
            DateTime initialSettlement, DateTime finalSettlement, DateTime paymentDate,
            IEnumerable<(string? Codigo, string Descripcion, decimal Valor)> devengos,
            IEnumerable<(string? Codigo, string Descripcion, decimal Valor)> deducciones)
        {
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

            return new Dictionary<string, object?>
            {
                ["EmisorLogoUrl"] = client.LogoLightUrl,
                ["EmisorRazonSocial"] = client.CompanyName,
                ["EmisorNit"] = client.TaxId,
                ["EmisorDireccion"] = client.Address,
                ["EmisorCiudad"] = client.City,
                ["EmisorTelefono"] = client.Phone,
                ["EmisorEmail"] = client.Email,

                ["EmpleadoNombre"] = employee.Name,
                ["EmpleadoTipoIdentificacion"] = employee.IdentificationType,
                ["EmpleadoIdentificacion"] = employee.IdentificationNumber,
                ["EmpleadoDireccion"] = employee.Address,
                ["EmpleadoCiudad"] = employee.CityName,

                ["DocumentoTitulo"] = titulo,
                ["DocumentoNumero"] = $"{prefix}-{numeroConsecutivo}",
                ["ReferenciaAjuste"] = esNota ? document.ReferenceConcept : null,
                ["FechaGeneracion"] = document.CreatedAt.ToString("dd/MM/yyyy HH:mm:ss"),
                ["PeriodoTexto"] = $"{initialSettlement:dd/MM/yyyy} — {finalSettlement:dd/MM/yyyy}",
                ["FechaPago"] = paymentDate.ToString("dd/MM/yyyy"),
                ["MedioPago"] = medioPago,
                ["Cune"] = document.Cufe,

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
                ["ProveedorTecnologicoNit"] = esIntegradorExterno ? client.Integrator.Nit : null,

                ["DataSource"] = devengosList.Select(d => new Dictionary<string, object?>
                    {
                        ["Tipo"] = "DEVENGADOS",
                        ["Codigo"] = d.Codigo,
                        ["Descripcion"] = d.Descripcion,
                        ["Valor"] = Money(d.Valor)
                    })
                    .Concat(deduccionesList.Select(d => new Dictionary<string, object?>
                    {
                        ["Tipo"] = "DEDUCCIONES",
                        ["Codigo"] = d.Codigo,
                        ["Descripcion"] = d.Descripcion,
                        ["Valor"] = Money(d.Valor)
                    }))
                    .ToList()
            };
        }
    }
}
