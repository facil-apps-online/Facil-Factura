using System;
using System.Collections.Generic;

namespace Fel.Infrastructure.Services
{
    // Datos ficticios pero realistas — mismo contrato ("Documento" + listas) que
    // InvoiceReportDataMapper/SupportDocumentReportDataMapper/NominaReportDataMapper — para que la
    // vista previa de un diseño (sin un documento real emitido) se vea igual de completa que uno
    // real. Compartido entre Superadmin, Tenant y Client: los tres portales pueden tener plantillas
    // propias y necesitan la misma vista previa "con datos de muestra" sobre ellas.
    public static class TemplatePreviewSampleData
    {
        // mostrarRetenciones: refleja DocumentTemplate.MostrarRetenciones de la plantilla que se
        // está previsualizando, para que el diseñador vea exactamente el mismo comportamiento
        // (filas de retención + "Neto a pagar" presentes o no) que tendrá un documento real.
        public static Dictionary<string, object?> BuildSampleData(string documentTypeCode, bool mostrarRetenciones = true)
        {
            if (documentTypeCode.StartsWith("DS")) return SampleSupportDocumentData(documentTypeCode);
            if (documentTypeCode.StartsWith("NE")) return SampleNominaData(documentTypeCode);
            return SampleInvoiceData(documentTypeCode, mostrarRetenciones);
        }

        private static Dictionary<string, object?> SampleInvoiceData(string documentTypeCode, bool mostrarRetenciones)
        {
            var documentoTipo = documentTypeCode switch
            {
                "NC" => "NOTA CRÉDITO ELECTRÓNICA",
                "ND" => "NOTA DÉBITO ELECTRÓNICA",
                "DE-POS" => "DOCUMENTO EQUIVALENTE - TIQUETE POS",
                _ => "FACTURA ELECTRÓNICA DE VENTA"
            };
            var esNota = documentTypeCode is "NC" or "ND";

            // Mismo shape que InvoiceReportDataMapper.Build: "Documento" (campos de un solo valor)
            // más los arreglos anidados ("Items", "Impuestos") que JsonDataSource resuelve como
            // tablas separadas — no el modelo viejo de Parameters planos + un único DataSource.
            return new Dictionary<string, object?>
            {
                ["Documento"] = new Dictionary<string, object?>
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
                    ["EmisorActividadEconomica"] = "4711",
                    ["EmisorGranContribuyente"] = "No somos Gran Contribuyente",
                    ["EmisorAgenteRetenedorIva"] = "No somos Agente Retenedor del Impuesto sobre las Ventas - IVA",
                    ["EmisorAutorretenedorRenta"] = "No somos Autorretenedor del Impuesto sobre la Renta y Complementarios",

                    ["AdquirenteNombre"] = "Juan Pérez Gómez",
                    ["AdquirenteTipoIdentificacion"] = "CC",
                    ["AdquirenteIdentificacion"] = "1234567890",
                    ["AdquirenteDireccion"] = "Carrera 15 # 40-50",
                    ["AdquirenteCiudad"] = "Bogotá D.C.",
                    ["AdquirenteTelefono"] = "310 555 6789",
                    ["AdquirenteEmail"] = "juan.perez@correo.com",

                    ["DocumentoTipo"] = documentoTipo,
                    ["DocumentoNumero"] = "SETP 1",
                    ["ResolucionTexto"] = "Resolución de facturación: 18760000001 vigente desde 01/01/2026 hasta: 31/12/2027. Del SETP 1 al SETP 100000.",
                    ["Leyenda"] = "Consignar el valor en la cuenta bancaria indicada por el emisor.",
                    ["FechaGeneracion"] = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                    ["FechaVencimiento"] = DateTime.Now.AddDays(30).ToString("dd/MM/yyyy"),
                    ["Cufe"] = "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4",
                    ["QrCode"] = "NumFac=SETP1\nFecFac=2026-01-15\nHorFac=10:30:00-05:00\nNitFac=900123456\nDocAdq=1234567890\nValFac=198.000\nValIva=31.920\nValOtroIm=0.00\nValTolFac=229.920\nCUFE=a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4\nQRCode=https://catalogo-vpfe.dian.gov.co/document/searchqr?documentkey=a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4",
                    ["QrImageUrl"] = "https://api.qrserver.com/v1/create-qr-code/?size=600x600&data=" + Uri.EscapeDataString("NumFac=SETP1\nFecFac=2026-01-15\nHorFac=10:30:00-05:00\nNitFac=900123456\nDocAdq=1234567890\nValFac=198.000\nValIva=31.920\nValOtroIm=0.00\nValTolFac=229.920\nCUFE=a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4\nQRCode=https://catalogo-vpfe.dian.gov.co/document/searchqr?documentkey=a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4"),
                    ["MedioPago"] = "10",
                    ["FormaPago"] = "Contado",
                    ["OrdenCompra"] = "OC-2026-001",
                    ["Notas"] = "Documento de ejemplo — vista previa de diseño.",
                    ["NotaReferencia"] = esNota ? "Ajusta el documento N° SETP 1 · CUFE a1b2c3d4e5f6...ejemplo · Motivo: Devolución parcial" : null,

                    ["Subtotal"] = "198.000",
                    ["Iva"] = "31.920",
                    ["Descuento"] = "0",
                    ["Cargo"] = "0",
                    ["TotalRetenciones"] = mostrarRetenciones ? "6.862" : null,
                    ["NetoAPagar"] = mostrarRetenciones ? "223.058" : null,
                    ["Total"] = "229.920",
                    ["TotalEnLetras"] = "DOSCIENTOS VEINTINUEVE MIL NOVECIENTOS VEINTE PESOS M/CTE",

                    ["FabricanteSoftwareNombre"] = "SoFactory S.A.S.",
                    ["FabricanteSoftwareNit"] = "900.303.194-6",
                    ["NombreSoftware"] = "Facil Factura",
                    ["ProveedorTecnologicoNombre"] = null,
                    ["ProveedorTecnologicoNit"] = null
                },

                ["Items"] = new List<Dictionary<string, object?>>
                {
                    new() { ["Ordinal"] = "1", ["Codigo"] = "PRD001", ["Nombre"] = "Producto de ejemplo A", ["Cantidad"] = "2", ["Unidad"] = "94 - EA", ["ValorUnitario"] = "50.000", ["PorcentajeDescuento"] = "0%", ["TratamientoIva"] = "Gravado", ["PorcentajeIva"] = "19%", ["ValorIva"] = "19.000", ["TotalLinea"] = "119.000" },
                    new() { ["Ordinal"] = "2", ["Codigo"] = "PRD002", ["Nombre"] = "Servicio de asistencia técnica agropecuaria mediante la metodología de taller de fortalecimiento socioempresarial dirigido a los productores de la Asociación de Mujeres Campesinas — nombre de ejemplo intencionalmente largo para probar el crecimiento de la celda", ["Cantidad"] = "1", ["Unidad"] = "94 - EA", ["ValorUnitario"] = "68.000", ["PorcentajeDescuento"] = "10%", ["TratamientoIva"] = "Gravado", ["PorcentajeIva"] = "19%", ["ValorIva"] = "12.920", ["TotalLinea"] = "80.920" },
                    new() { ["Ordinal"] = "3", ["Codigo"] = "PRD003", ["Nombre"] = "Producto de ejemplo C (excluido)", ["Cantidad"] = "1", ["Unidad"] = "94 - EA", ["ValorUnitario"] = "30.000", ["PorcentajeDescuento"] = "0%", ["TratamientoIva"] = "Excluido", ["PorcentajeIva"] = "0%", ["ValorIva"] = "0", ["TotalLinea"] = "30.000" }
                },

                ["Impuestos"] = BuildSampleImpuestos(mostrarRetenciones)
            };
        }

        // Mismo criterio que InvoiceReportDataMapper.Build: IVA discriminado por tarifa siempre
        // presente, retenciones (Tipo="Retencion", en rojo en el .repx) solo si la plantilla
        // activa MostrarRetenciones.
        private static List<Dictionary<string, object?>> BuildSampleImpuestos(bool mostrarRetenciones)
        {
            var impuestos = new List<Dictionary<string, object?>>
            {
                new() { ["Concepto"] = "IVA 19%", ["Valor"] = "31.920", ["Tipo"] = "Iva" }
            };
            if (mostrarRetenciones)
            {
                impuestos.Add(new() { ["Concepto"] = "RETE FUENTE 2.5%", ["Valor"] = "4.950", ["Tipo"] = "Retencion" });
                impuestos.Add(new() { ["Concepto"] = "RETE ICA 0.966%", ["Valor"] = "1.912", ["Tipo"] = "Retencion" });
            }
            return impuestos;
        }

        private static Dictionary<string, object?> SampleSupportDocumentData(string documentTypeCode)
        {
            var esAjuste = documentTypeCode == "DS-AJUSTE";
            // Mismo shape que SupportDocumentReportDataMapper.Build: "Documento" + "Items" + "Impuestos".
            return new Dictionary<string, object?>
            {
                ["Documento"] = new Dictionary<string, object?>
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
                    ["EmisorActividadEconomica"] = "4711",
                    ["EmisorGranContribuyente"] = "No somos Gran Contribuyente",
                    ["EmisorAgenteRetenedorIva"] = "No somos Agente Retenedor del Impuesto sobre las Ventas - IVA",
                    ["EmisorAutorretenedorRenta"] = "No somos Autorretenedor del Impuesto sobre la Renta y Complementarios",

                    ["ProveedorNombre"] = "Distribuidora Proveedor Ltda.",
                    ["ProveedorTipoIdentificacion"] = "NIT",
                    ["ProveedorIdentificacion"] = "800987654",
                    ["ProveedorDireccion"] = "Avenida 30 # 5-15",
                    ["ProveedorCiudad"] = "Medellín",
                    ["ProveedorTelefono"] = "604 555 4321",
                    ["ProveedorEmail"] = "ventas@proveedor.com",

                    ["DocumentoTipo"] = esAjuste ? "NOTA DE AJUSTE - DOCUMENTO SOPORTE" : "DOCUMENTO SOPORTE DE PAGO",
                    ["DocumentoNumero"] = "DS 1",
                    ["NotaReferencia"] = esAjuste ? "Motivo del ajuste: Corrección de valores — documento de ejemplo" : null,
                    ["ResolucionTexto"] = "Resolución de facturación: 18760000002 vigente desde 01/01/2026 hasta: 31/12/2027. Del DS 1 al DS 100000.",
                    ["Leyenda"] = "Documento soporte de ejemplo.",
                    ["FechaGeneracion"] = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                    ["FechaVencimiento"] = DateTime.Now.AddDays(30).ToString("dd/MM/yyyy"),
                    ["Cufe"] = "b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5",
                    ["QrCode"] = "NumFac=DS1\nFecFac=2026-01-15\nHorFac=10:30:00-05:00\nNitFac=900123456\nDocAdq=800987654\nValFac=120.000\nValIva=0.00\nValOtroIm=0.00\nValTolFac=120.000\nCUFE=b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5",
                    ["QrImageUrl"] = "https://api.qrserver.com/v1/create-qr-code/?size=150x150&data=" + Uri.EscapeDataString("NumFac=DS1\nFecFac=2026-01-15\nNitFac=900123456\nDocAdq=800987654\nValTolFac=120.000\nCUFE=b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5"),
                    ["MedioPago"] = "10",
                    ["FormaPago"] = "Contado",
                    ["OrdenCompra"] = "OC-2026-002",
                    ["Notas"] = "Documento de ejemplo — vista previa de diseño.",

                    ["Subtotal"] = "120.000",
                    ["Iva"] = "0",
                    ["Descuento"] = "0",
                    ["Cargo"] = "0",
                    ["TotalRetenciones"] = null,
                    ["NetoAPagar"] = null,
                    ["Total"] = "120.000",
                    ["TotalEnLetras"] = "CIENTO VEINTE MIL PESOS M/CTE",

                    ["FabricanteSoftwareNombre"] = "SoFactory S.A.S.",
                    ["FabricanteSoftwareNit"] = "900.303.194-6",
                    ["NombreSoftware"] = "Facil Factura",
                    ["ProveedorTecnologicoNombre"] = null,
                    ["ProveedorTecnologicoNit"] = null
                },

                ["Items"] = new List<Dictionary<string, object?>>
                {
                    new() { ["Ordinal"] = "1", ["Codigo"] = "SRV001", ["Nombre"] = "Servicio de ejemplo", ["Cantidad"] = "1", ["Unidad"] = "94 - EA", ["ValorUnitario"] = "120.000", ["PorcentajeDescuento"] = "0%", ["TratamientoIva"] = "Gravado", ["PorcentajeIva"] = "0%", ["ValorIva"] = "0", ["TotalLinea"] = "120.000" }
                },

                ["Impuestos"] = new List<Dictionary<string, object?>>()
            };
        }

        private static Dictionary<string, object?> SampleNominaData(string documentTypeCode)
        {
            var esNota = documentTypeCode == "NE-AJUSTE";
            // Mismo shape que NominaReportDataMapper.Build: "Documento" + "Devengos" + "Deducciones".
            return new Dictionary<string, object?>
            {
                ["Documento"] = new Dictionary<string, object?>
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

                    ["DocumentoTipo"] = esNota ? "NOTA DE AJUSTE - NÓMINA ELECTRÓNICA" : "NÓMINA ELECTRÓNICA",
                    ["DocumentoNumero"] = "NE-1",
                    ["NotaReferencia"] = esNota ? "Motivo: Corrección de devengados — documento de ejemplo" : null,
                    ["FechaGeneracion"] = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                    ["PeriodoTexto"] = $"{DateTime.Now.AddDays(-30):dd/MM/yyyy} — {DateTime.Now:dd/MM/yyyy}",
                    ["FechaPago"] = DateTime.Now.ToString("dd/MM/yyyy"),
                    ["MedioPago"] = "Consignación",
                    ["Cune"] = "c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6",
                    ["QrCode"] = "c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6",
                    ["QrImageUrl"] = "https://api.qrserver.com/v1/create-qr-code/?size=150x150&data=" + Uri.EscapeDataString("c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6"),

                    ["TotalDevengado"] = "2.500.000",
                    ["TotalDeduccion"] = "200.000",
                    ["NetoPagar"] = "2.300.000",
                    ["NetoPagarEnLetras"] = "DOS MILLONES TRESCIENTOS MIL PESOS M/CTE",

                    ["FabricanteSoftwareNombre"] = "SoFactory S.A.S.",
                    ["FabricanteSoftwareNit"] = "900.303.194-6",
                    ["NombreSoftware"] = "Facil Factura",
                    ["SoftwareId"] = "SOFT-EJEMPLO-0001",
                    ["ProveedorTecnologicoNombre"] = null,
                    ["ProveedorTecnologicoNit"] = null
                },

                ["Devengos"] = new List<Dictionary<string, object?>>
                {
                    new() { ["Codigo"] = "001", ["Descripcion"] = "Salario básico", ["Valor"] = "2.300.000" },
                    new() { ["Codigo"] = "002", ["Descripcion"] = "Auxilio de transporte", ["Valor"] = "200.000" }
                },

                ["Deducciones"] = new List<Dictionary<string, object?>>
                {
                    new() { ["Codigo"] = "101", ["Descripcion"] = "Salud", ["Valor"] = "100.000" },
                    new() { ["Codigo"] = "102", ["Descripcion"] = "Pensión", ["Valor"] = "100.000" }
                }
            };
        }
    }
}
