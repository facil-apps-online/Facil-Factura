using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Fel.Core.Entities;
using Fel.Core.Models;
using Fel.Infrastructure.Services;

namespace Fel.Infrastructure.Dataico
{
    // Mismo contrato que Invoice/SupportDocumentReportDataMapper ("Documento" + listas), adaptado a
    // Nómina Electrónica: no hay "adquirente" sino un empleado, no hay IVA ni retenciones, y el
    // detalle se discrimina por conceptos: dos listas, "Devengos" y "Deducciones", cada una con
    // {Codigo, Descripcion, Valor}. La plantilla estándar las imprime como dos secciones, cada una con
    // su título y encabezado de columnas (un DetailReportBand por lista). Los totales (Total
    // Devengado/Total Deducción/Neto) vienen ya calculados en "Documento", no como sumas dentro del
    // reporte, para no depender de la configuración de Summary de DevExpress. Antes devolvía campos
    // planos y una sola lista "DataSource" con un campo Tipo.
    //
    // Desacoplado de PayrollConceptItem (que vive en Fel.Api.Client, una capa por encima de
    // Fel.Infrastructure) a propósito: el llamador arma las tuplas simples.
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
            IEnumerable<(string? Codigo, string Descripcion, decimal Valor)> devengos,
            IEnumerable<(string? Codigo, string Descripcion, decimal Valor)> deducciones)
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

                    // Trabajador
                    ["EmpleadoNombre"] = employee.Name,
                    ["EmpleadoTipoIdentificacion"] = employee.IdentificationType,
                    ["EmpleadoIdentificacion"] = employee.IdentificationNumber,
                    ["EmpleadoDireccion"] = employee.Address,
                    ["EmpleadoCiudad"] = employee.CityName,

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

                ["Devengos"] = devengosList.Select(d => new Dictionary<string, object?>
                {
                    ["Codigo"] = d.Codigo,
                    ["Descripcion"] = d.Descripcion,
                    ["Valor"] = Money(d.Valor)
                }).ToList(),

                ["Deducciones"] = deduccionesList.Select(d => new Dictionary<string, object?>
                {
                    ["Codigo"] = d.Codigo,
                    ["Descripcion"] = d.Descripcion,
                    ["Valor"] = Money(d.Valor)
                }).ToList()
            };
        }
    }
}
