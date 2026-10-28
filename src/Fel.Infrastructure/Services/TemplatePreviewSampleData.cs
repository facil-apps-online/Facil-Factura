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
        // mostrarRetenciones: ya no tiene efecto (las retenciones se muestran siempre); sigue en la firma mientras se retira el interruptor de las plantillas.
        public static Dictionary<string, object?> BuildSampleData(string documentTypeCode, bool mostrarRetenciones = true, string? documentTypeTitle = null)
        {
            if (documentTypeCode.StartsWith("DS")) return SampleSupportDocumentData(documentTypeCode, documentTypeTitle);
            if (documentTypeCode.StartsWith("NE")) return SampleNominaData(documentTypeCode);
            if (documentTypeCode == "FE-AIU") return SampleAiuInvoiceData(mostrarRetenciones);
            return SampleInvoiceData(documentTypeCode, mostrarRetenciones, documentTypeTitle);
        }

        // Factura AIU: parte de la factura estándar de muestra y cambia lo propio del contrato (mismos campos que InvoiceReportDataMapper.Build):
        // tipo de operación, objeto, las cuatro líneas con su porcentaje, el resumen AIU y los totales coherentes con ellas.
        private static Dictionary<string, object?> SampleAiuInvoiceData(bool mostrarRetenciones)
        {
            var data = SampleInvoiceData("FE-STD", mostrarRetenciones);
            var documento = (Dictionary<string, object?>)data["Documento"]!;
            const string objeto = "Obra civil de adecuación de bodega y cerramiento perimetral del predio, según propuesta comercial No. 045 de 2026";
            documento["TipoOperacion"] = "AIU";
            documento["AiuObjeto"] = "Objeto del contrato: " + objeto;
            documento["Subtotal"] = "1.200.000";
            documento["Iva"] = "199.500";
            documento["Total"] = "1.399.500";
            documento["TotalEnLetras"] = "UN MILLÓN TRESCIENTOS NOVENTA Y NUEVE MIL QUINIENTOS PESOS";
            documento["TotalRetenciones"] = null;
            documento["NetoAPagar"] = null;

            Dictionary<string, object?> Line(string ordinal, string code, string name, string value, string iva, string pct, string? aiuPct) => new()
            {
                ["Ordinal"] = ordinal, ["Codigo"] = code, ["Nombre"] = name, ["Cantidad"] = "1", ["Unidad"] = "94 - EA", ["ValorUnitario"] = value,
                ["PorcentajeDescuento"] = "0%", ["TratamientoIva"] = iva == "0%" ? "Excluido" : "Gravado", ["PorcentajeIva"] = iva,
                ["ValorIva"] = pct, ["TotalLinea"] = value, ["PorcentajeAiu"] = aiuPct,
                ["Marca"] = null, ["Modelo"] = null, ["PartidaArancelaria"] = null, ["MandanteNombre"] = null, ["MandanteIdentificacion"] = null, ["MandanteTexto"] = null
            };
            data["Items"] = new List<Dictionary<string, object?>>
            {
                Line("1", "AIU-A", "Administración (A.I.U.)", "100.000", "0%", "0", "10%"),
                Line("2", "AIU-I", "Imprevistos (A.I.U.)", "50.000", "0%", "0", "5%"),
                Line("3", "AIU-U", "Utilidad (A.I.U.)", "50.000", "19%", "9.500", "5%"),
                Line("4", "SERVICIO", objeto, "1.000.000", "19%", "190.000", null)
            };
            data["Aiu"] = new List<Dictionary<string, object?>>
            {
                new() { ["Concepto"] = "Valor del servicio (base del contrato)", ["Porcentaje"] = "", ["Valor"] = "1.000.000" },
                new() { ["Concepto"] = "Administración", ["Porcentaje"] = "10%", ["Valor"] = "100.000" },
                new() { ["Concepto"] = "Imprevistos", ["Porcentaje"] = "5%", ["Valor"] = "50.000" },
                new() { ["Concepto"] = "Utilidad", ["Porcentaje"] = "5%", ["Valor"] = "50.000" },
                new() { ["Concepto"] = "Base gravable del IVA", ["Porcentaje"] = "", ["Valor"] = "1.050.000" }
            };
            data["Impuestos"] = new List<Dictionary<string, object?>> { new() { ["Concepto"] = "IVA 19%", ["Valor"] = "199.500", ["Tipo"] = "Iva" } };
            return data;
        }

        private static Dictionary<string, object?> SampleInvoiceData(string documentTypeCode, bool mostrarRetenciones, string? documentTypeTitle = null)
        {
            var documentoTipo = string.IsNullOrWhiteSpace(documentTypeTitle) ? documentTypeCode switch
            {
                "NC" => "NOTA CRÉDITO ELECTRÓNICA",
                "ND" => "NOTA DÉBITO ELECTRÓNICA",
                "DE-POS" => "DOCUMENTO EQUIVALENTE - TIQUETE POS",
                _ => "FACTURA ELECTRÓNICA DE VENTA"
            } : documentTypeTitle;
            var esNota = documentTypeCode is "NC" or "ND";
            var esExportacion = documentTypeCode == "FE-EXP";
            var esMandato = documentTypeCode == "FE-MANDATO";

            // Mismo shape que InvoiceReportDataMapper.Build: "Documento" (campos de un solo valor)
            // más los arreglos anidados ("Items", "Impuestos") que JsonDataSource resuelve como
            // tablas separadas — no el modelo viejo de Parameters planos + un único DataSource.
            var data = new Dictionary<string, object?>
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
                    ["EmisorRegimenRenta"] = "Régimen Ordinario",
                    ["EmisorGranContribuyente"] = "No somos Gran Contribuyente",
                    ["EmisorAgenteRetenedorIva"] = "No somos Agente Retenedor del Impuesto sobre las Ventas - IVA",
                    ["EmisorAutorretenedorRenta"] = "No somos Autorretenedor del Impuesto sobre la Renta y Complementarios",

                    ["AdquirenteNombre"] = "Juan Pérez Gómez",
                    ["AdquirenteTipoIdentificacion"] = "CC",
                    ["AdquirenteDocumento"] = "CC 1234567890",
                    ["AdquirenteIdentificacion"] ="1234567890",
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
                    ["Notas"] = "Documento de ejemplo — vista previa de diseño.\nSegunda observación de ejemplo.\nTercera observación de ejemplo.",
                    ["TipoOperacion"] = esMandato ? "Mandatos" : esExportacion ? "Exportación" : "Estándar",
                    ["Moneda"] = esExportacion ? "USD" : "COP",
                    ["Trm"] = esExportacion ? "3,912.45" : null,
                    ["TrmFecha"] = esExportacion ? "15/01/2026" : null,
                    ["MonedaTexto"] = esExportacion ? "USD (3,912.45 - 15/01/2026)" : null,
                    ["Incoterm"] = esExportacion ? "CFR - Costo y flete" : null,
                    ["NotaReferencia"] = esNota ? "Ajusta el documento N° SETP 1 · CUFE a1b2c3d4e5f6...ejemplo · Motivo: Devolución parcial" : null,

                    ["Subtotal"] = "198.000",
                    ["Iva"] = "31.920",
                    ["Descuento"] = "0",
                    ["Cargo"] = "0",
                    ["TotalRetenciones"] = "6.862",
                    // Igual que un documento real: el TOTAL ya descuenta las retenciones y no hay fila "Neto a pagar".
                    ["NetoAPagar"] = null,
                    ["Total"] = "223.058",
                    ["TotalEnLetras"] = "DOSCIENTOS VEINTITRÉS MIL CINCUENTA Y OCHO PESOS",

                    ["FabricanteSoftwareNombre"] = "SoFactory S.A.S.",
                    ["FabricanteSoftwareNit"] = "900.303.194-6",
                    ["NombreSoftware"] = "Facil Factura",
                    ["ProveedorTecnologicoNombre"] = null,
                    ["ProveedorTecnologicoNit"] = null
                },

                ["Items"] = WithSampleMandante(new List<Dictionary<string, object?>>
                {
                    new() { ["Ordinal"] = "1", ["Codigo"] = "PRD001", ["Nombre"] = "Producto de ejemplo A", ["Cantidad"] = "2", ["Unidad"] = "94 - EA", ["ValorUnitario"] = "50.000", ["PorcentajeDescuento"] = "0%", ["TratamientoIva"] = "Gravado", ["PorcentajeIva"] = "19%", ["ValorIva"] = "19.000", ["TotalLinea"] = "119.000" },
                    new() { ["Ordinal"] = "2", ["Codigo"] = "PRD002", ["Nombre"] = "Servicio de asistencia técnica agropecuaria mediante la metodología de taller de fortalecimiento socioempresarial dirigido a los productores de la Asociación de Mujeres Campesinas — nombre de ejemplo intencionalmente largo para probar el crecimiento de la celda", ["Cantidad"] = "1", ["Unidad"] = "94 - EA", ["ValorUnitario"] = "68.000", ["PorcentajeDescuento"] = "10%", ["TratamientoIva"] = "Gravado", ["PorcentajeIva"] = "19%", ["ValorIva"] = "12.920", ["TotalLinea"] = "80.920" },
                    new() { ["Ordinal"] = "3", ["Codigo"] = "PRD003", ["Nombre"] = "Producto de ejemplo C (excluido)", ["Cantidad"] = "1", ["Unidad"] = "94 - EA", ["ValorUnitario"] = "30.000", ["PorcentajeDescuento"] = "0%", ["TratamientoIva"] = "Excluido", ["PorcentajeIva"] = "0%", ["ValorIva"] = "0", ["TotalLinea"] = "30.000" }
                }, esMandato, esExportacion),

                // Mismo shape que InvoiceReportDataMapper.Build: una fila por observación, en su orden.
                ["Observaciones"] = new List<Dictionary<string, object?>>
                {
                    new() { ["Numero"] = "1", ["Texto"] = "Documento de ejemplo — vista previa de diseño." },
                    new() { ["Numero"] = "2", ["Texto"] = "Segunda observación de ejemplo." },
                    new() { ["Numero"] = "3", ["Texto"] = "Tercera observación de ejemplo." }
                },

                ["Impuestos"] = BuildSampleImpuestos(mostrarRetenciones)
            };

            if (data["Documento"] is Dictionary<string, object?> documento)
            {
                foreach (var field in documento)
                    data.TryAdd(field.Key, field.Value);
            }

            return data;
        }

        // Factura de mandato: cada línea de ejemplo declara un mandante (mismos campos que InvoiceReportDataMapper); vacíos en el resto.
        private static List<Dictionary<string, object?>> WithSampleMandante(List<Dictionary<string, object?>> items, bool esMandato, bool esExportacion)
        {
            foreach (var item in items)
            {
                item["Marca"] = esExportacion ? "Marca de ejemplo" : null;
                item["Modelo"] = esExportacion ? "Modelo 2026" : null;
                item["PartidaArancelaria"] = esExportacion ? "3004909000" : null;
                item["MandanteNombre"] = esMandato ? "Mandante de Ejemplo S.A.S." : null;
                item["MandanteIdentificacion"] = esMandato ? "800168654" : null;
                item["MandanteTexto"] = esMandato ? "Mandante NIT: 800168654" : null;
            }
            return items;
        }

        // Mismo criterio que InvoiceReportDataMapper.Build: IVA discriminado por tarifa y retenciones
        // (Tipo="Retencion", en rojo en el .repx) siempre presentes.
        private static List<Dictionary<string, object?>> BuildSampleImpuestos(bool mostrarRetenciones)
        {
            var impuestos = new List<Dictionary<string, object?>>
            {
                new() { ["Concepto"] = "IVA 19%", ["Valor"] = "31.920", ["Tipo"] = "Iva" }
            };
            impuestos.Add(new() { ["Concepto"] = "RETE FUENTE 2.5%", ["Valor"] = "4.950", ["Tipo"] = "Retencion" });
            impuestos.Add(new() { ["Concepto"] = "RETE ICA 0.966%", ["Valor"] = "1.912", ["Tipo"] = "Retencion" });
            return impuestos;
        }

        private static Dictionary<string, object?> SampleSupportDocumentData(string documentTypeCode, string? documentTypeTitle = null)
        {
            var esAjuste = documentTypeCode == "DS-AJUSTE";
            // Mismo shape que SupportDocumentReportDataMapper.Build: "Documento" + "Items" + "Impuestos".
            var data = new Dictionary<string, object?>
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
                    ["EmisorRegimenRenta"] = "Régimen Ordinario",
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

                    ["DocumentoTipo"] = string.IsNullOrWhiteSpace(documentTypeTitle)
                        ? (esAjuste ? "NOTA DE AJUSTE - DOCUMENTO SOPORTE" : "DOCUMENTO SOPORTE DE PAGO")
                        : documentTypeTitle,
                    ["DocumentoNumero"] = "DS 1",
                    ["NotaReferencia"] = esAjuste ? $"Ajusta el documento soporte N° DS 1 del {DateTime.Now.AddDays(-3):dd/MM/yyyy} · Motivo: Corrección de valores\nCUDS 9f2c4e7a1b3d5f60718293a4b5c6d7e8f9012a3b4c5d6e7f8091a2b3c4d5e6f708192a3b4c5d6e7f8091a2b3c4d5e6f7 (ejemplo)" : null,
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
                    ["TotalEnLetras"] = "CIENTO VEINTE MIL PESOS",

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

            // Las tirillas POS del sistema y algunas plantillas publicadas usan todavía
            // Parameters.* en lugar de Documento.*. La vista previa debe entregar ambos
            // contratos, igual que el render de documentos reales.
            if (data["Documento"] is Dictionary<string, object?> documento)
            {
                foreach (var field in documento)
                    data.TryAdd(field.Key, field.Value);
            }

            return data;
        }

        private static Dictionary<string, object?> SampleNominaData(string documentTypeCode)
        {
            var esNota = documentTypeCode == "NE-AJUSTE";
            // Mismo shape que NominaReportDataMapper.Build: "Documento" + bloques de devengos y deducciones con su total.
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
                    ["EmisorActividadEconomica"] = "4711",
                    ["EmisorRegimenRenta"] = "Régimen Ordinario",
                    ["EmisorGranContribuyente"] = "No somos Gran Contribuyente",
                    ["EmisorAgenteRetenedorIva"] = "No somos Agente Retenedor del Impuesto sobre las Ventas - IVA",
                    ["EmisorAutorretenedorRenta"] = "No somos Autorretenedor del Impuesto sobre la Renta y Complementarios",

                    ["EmpleadoNombre"] = "María Rodríguez López",
                    ["EmpleadoTipoIdentificacion"] = "CC",
                    ["EmpleadoIdentificacion"] = "1098765432",
                    ["EmpleadoDireccion"] = "Calle 80 # 10-20",
                    ["EmpleadoCiudad"] = "Bogotá D.C.",
                    ["EmpleadoCodigoDane"] = "11001",
                    ["EmpleadoEmail"] = "maria.rodriguez@ejemplo.com",
                    ["EmpleadoContrato"] = "Término indefinido",
                    ["EmpleadoTipoTrabajador"] = "Dependiente",
                    ["EmpleadoSalarioBase"] = "2.300.000",

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

                    ["NetoPagar"] = "5.039.690",
                    ["NetoPagarEnLetras"] = "CINCO MILLONES TREINTA Y NUEVE MIL SEISCIENTOS NOVENTA PESOS",

                    ["FabricanteSoftwareNombre"] = "SoFactory S.A.S.",
                    ["FabricanteSoftwareNit"] = "900.303.194-6",
                    ["NombreSoftware"] = "Facil Factura",
                    ["SoftwareId"] = "SOFT-EJEMPLO-0001",
                    ["ProveedorTecnologicoNombre"] = null,
                    ["ProveedorTecnologicoNit"] = null
                },

                ["Devengos"] = new List<Dictionary<string, object?>>
                {
                    new() { ["Concepto"] = "Básico", ["Porcentaje"] = "", ["Dias"] = "30", ["Salarial"] = "1.423.500", ["NoSalarial"] = "", ["Total"] = "1.423.500" },
                    new() { ["Concepto"] = "Auxilio de transporte", ["Porcentaje"] = "", ["Dias"] = "30", ["Salarial"] = "200.000", ["NoSalarial"] = "", ["Total"] = "200.000" },
                    new() { ["Concepto"] = "Bonificación", ["Porcentaje"] = "", ["Dias"] = "", ["Salarial"] = "", ["NoSalarial"] = "400.000", ["Total"] = "400.000" },
                    new() { ["Concepto"] = "Prima", ["Porcentaje"] = "", ["Dias"] = "180", ["Salarial"] = "", ["NoSalarial"] = "811.750", ["Total"] = "811.750" },
                    new() { ["Concepto"] = "Cesantías", ["Porcentaje"] = "", ["Dias"] = "360", ["Salarial"] = "", ["NoSalarial"] = "1.623.500", ["Total"] = "1.623.500" },
                    new() { ["Concepto"] = "Intereses de cesantías", ["Porcentaje"] = "12", ["Dias"] = "", ["Salarial"] = "", ["NoSalarial"] = "194.820", ["Total"] = "194.820" },
                    new() { ["Concepto"] = "Auxilio", ["Porcentaje"] = "", ["Dias"] = "", ["Salarial"] = "", ["NoSalarial"] = "500.000", ["Total"] = "500.000" }
                },
                ["TotalDevengos"] = new List<Dictionary<string, object?>>
                {
                    new() { ["Etiqueta"] = "Total Devengados", ["Salarial"] = "1.623.500", ["NoSalarial"] = "3.530.070", ["Total"] = "5.153.570" }
                },
                ["Deducciones"] = new List<Dictionary<string, object?>>
                {
                    new() { ["Concepto"] = "Salud", ["Porcentaje"] = "4", ["Dias"] = "", ["Salarial"] = "56.940", ["NoSalarial"] = "", ["Total"] = "- 56.940" },
                    new() { ["Concepto"] = "Fondo de pensión", ["Porcentaje"] = "4", ["Dias"] = "", ["Salarial"] = "56.940", ["NoSalarial"] = "", ["Total"] = "- 56.940" }
                },
                ["TotalDeducciones"] = new List<Dictionary<string, object?>>
                {
                    new() { ["Etiqueta"] = "Total Deducciones", ["Salarial"] = "113.880", ["NoSalarial"] = "", ["Total"] = "- 113.880" }
                }
            };
        }
    }
}
