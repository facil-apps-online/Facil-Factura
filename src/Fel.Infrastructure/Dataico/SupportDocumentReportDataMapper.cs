using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Fel.Core.Entities;
using Fel.Core.Models;
using Fel.Infrastructure.Services;

namespace Fel.Infrastructure.Dataico
{
    // Mismo contrato que InvoiceReportDataMapper ("Documento" + "Items" + "Impuestos"), pero con
    // "Proveedor" en vez de "Adquirente" (en Documento Soporte, el Cliente le compra al tercero, no
    // le vende) y sin forma de pago/medio de pago obligatorios en la nota de ajuste. Mismos
    // numerales DIAN (1-5, 8-13, 15, 16, 18) que Factura, adaptados a este tipo de documento.
    // Así el Documento Soporte refleja la factura y usa las mismas plantillas estándar (con
    // Proveedor* en lugar de Adquirente*). Antes devolvía campos planos y un único "DataSource".
    public static class SupportDocumentReportDataMapper
    {
        // Facil Factura es un producto de SoFactory S.A.S., el fabricante legal del software.
        private const string FabricanteSoftwareNombre = "SoFactory S.A.S.";
        private const string FabricanteSoftwareNit = "900.303.194-6";
        private const string NombreSoftware = "Facil Factura";


        // Redacción idéntica a InvoiceReportDataMapper para el encabezado de responsabilidades.
        private static string ResponsabilidadTexto(bool activo, string etiqueta) =>
            (activo ? "Somos " : "No somos ") + etiqueta;

        // Ver el comentario equivalente en InvoiceReportDataMapper: client.TaxRegime guarda el
        // código crudo (48/49), la representación gráfica necesita la etiqueta legible.
        private static readonly Dictionary<string, string> CalidadTributariaLabels = new()
        {
            ["48"] = "Responsable de IVA",
            ["49"] = "No responsable de IVA"
        };

        private static string CalidadTributaria(string? code) =>
            code != null && CalidadTributariaLabels.TryGetValue(code, out var label) ? label : code ?? string.Empty;

        // Ver el comentario equivalente en InvoiceReportDataMapper: ambos catálogos (Kind=FormaPago
        // y Kind=PaymentMeans) los resuelve el llamador desde TaxCatalogItem, no se duplican acá.
        private static string CatalogLabel(string? category, IReadOnlyDictionary<string, string>? catalog) =>
            !string.IsNullOrEmpty(category) && catalog != null && catalog.TryGetValue(category, out var label) ? label : category ?? string.Empty;

        // Ver el comentario equivalente en InvoiceReportDataMapper.
        private static string FormatUnidad(DocumentItem item, Client client)
        {
            var format = client.UnitOfMeasureDisplayOverride ?? item.UnitOfMeasureDisplayFormat;
            return format switch
            {
                "CodeOnly" => item.UnitOfMeasureCode,
                "AbbreviationOnly" => item.UnitOfMeasureAbbreviation,
                _ => item.UnitOfMeasureCode == item.UnitOfMeasureAbbreviation
                    ? item.UnitOfMeasureCode
                    : $"{item.UnitOfMeasureCode} - {item.UnitOfMeasureAbbreviation}"
            };
        }

        public static Dictionary<string, object?> Build(Document document, Customer? proveedor, Client client, Resolution? resolution, IReadOnlyList<DocumentItem> items, IReadOnlyDictionary<string, string>? paymentMeansCatalog = null, IReadOnlyDictionary<string, string>? formaPagoCatalog = null)
        {
            // Separadores del cliente (Client.DecimalSeparator): punto decimal y coma de miles por defecto.
            var nf = ReportNumberFormat.For(client.DecimalSeparator);
            string Money(decimal value) => value.ToString("N0", nf);
            string Qty(decimal value) => value.ToString("0.##", nf);
            string Pct(decimal value) => value.ToString("0.##", nf) + "%";
            var esIntegradorExterno = client.Integrator.Kind == IntegratorKind.ThirdPartyIntegrator;
            var esAjuste = document.TypeCode == "DS-AJUSTE";

            // IVA discriminado por tarifa + descuento/cargo general con su motivo, en la misma tabla
            // del pie que usa la factura (subreporte "Impuestos"). Retenciones: este documento no las
            // discrimina hoy, así que no se agregan filas de retención.
            var ivaPorTarifa = new Dictionary<decimal, decimal>();
            foreach (var item in items)
            {
                if (item.TaxRate <= 0) continue;
                ivaPorTarifa[item.TaxRate] = ivaPorTarifa.GetValueOrDefault(item.TaxRate) + item.TaxAmount;
            }
            var impuestos = new List<Dictionary<string, object?>>();
            foreach (var kv in ivaPorTarifa.OrderBy(kv => kv.Key))
                impuestos.Add(new Dictionary<string, object?> { ["Concepto"] = $"IVA {Pct(kv.Key)}", ["Valor"] = Money(kv.Value), ["Tipo"] = "Iva" });
            if ((document.GeneralDiscountAmount ?? 0) > 0)
                impuestos.Add(new Dictionary<string, object?>
                {
                    ["Concepto"] = string.IsNullOrWhiteSpace(document.GeneralDiscountReason) ? "Descuento general" : document.GeneralDiscountReason,
                    ["Valor"] = Money(document.GeneralDiscountAmount!.Value),
                    ["Tipo"] = "Descuento"
                });
            if ((document.GeneralChargeAmount ?? 0) > 0)
                impuestos.Add(new Dictionary<string, object?>
                {
                    ["Concepto"] = string.IsNullOrWhiteSpace(document.GeneralChargeReason) ? "Cargo general" : document.GeneralChargeReason,
                    ["Valor"] = Money(document.GeneralChargeAmount!.Value),
                    ["Tipo"] = "Cargo"
                });

            return new Dictionary<string, object?>
            {
                ["Documento"] = new Dictionary<string, object?>
                {
                    // Emisor (quien expide el documento soporte: el Cliente)
                    ["EmisorLogoUrl"] = client.LogoLightUrl,
                    ["EmisorRazonSocial"] = client.CompanyName,
                    ["EmisorNombreComercial"] = client.CommercialName,
                    ["EmisorNit"] = client.TaxId,
                    ["EmisorDv"] = client.VerificationDigit,
                    ["EmisorDireccion"] = client.Address,
                    ["EmisorCiudad"] = client.City,
                    ["EmisorTelefono"] = client.Phone,
                    ["EmisorEmail"] = client.Email,
                    ["EmisorCalidadTributaria"] = CalidadTributaria(client.TaxRegime),
                    ["EmisorActividadEconomica"] = client.EconomicActivity,
                    ["EmisorGranContribuyente"] = ResponsabilidadTexto(client.IsGranContribuyente, "Gran Contribuyente"),
                    ["EmisorAgenteRetenedorIva"] = ResponsabilidadTexto(client.IsAgenteRetenedorIva, "Agente Retenedor del Impuesto sobre las Ventas - IVA"),
                    ["EmisorAutorretenedorRenta"] = ResponsabilidadTexto(client.IsAutorretenedorRenta, "Autorretenedor del Impuesto sobre la Renta y Complementarios"),

                    // Proveedor (el tercero a quien se le compra)
                    ["ProveedorNombre"] = proveedor?.Name,
                    ["ProveedorTipoIdentificacion"] = proveedor?.IdentificationType,
                    ["ProveedorIdentificacion"] = proveedor?.IdentificationNumber,
                    ["ProveedorDireccion"] = proveedor?.Address,
                    ["ProveedorCiudad"] = proveedor?.CityName,
                    ["ProveedorTelefono"] = proveedor?.Phone,
                    ["ProveedorEmail"] = proveedor?.Email,

                    // Documento ("DocumentoTipo", no "DocumentoTitulo": mismo nombre que Factura)
                    ["DocumentoTipo"] = esAjuste ? "NOTA DE AJUSTE - DOCUMENTO SOPORTE" : "DOCUMENTO SOPORTE DE PAGO",
                    ["DocumentoNumero"] = $"{resolution?.Prefix} {document.Number}".Trim(),
                    ["NotaReferencia"] = esAjuste && !string.IsNullOrWhiteSpace(document.ReferenceConcept) ? $"Motivo del ajuste: {document.ReferenceConcept}" : null,
                    ["ResolucionTexto"] = resolution == null ? null :
                        $"Resolución DIAN {resolution.ResolutionNumber} · Rango {resolution.Prefix} {resolution.NumberStart}-{resolution.NumberEnd}" +
                        $" · Vigente hasta {resolution.ValidTo:dd/MM/yyyy}",
                    ["FechaGeneracion"] = document.CreatedAt.ToString("dd/MM/yyyy HH:mm:ss"),
                    ["FechaVencimiento"] = document.PaymentTermDays.HasValue ? document.IssueDate.AddDays(document.PaymentTermDays.Value).ToString("dd/MM/yyyy") : null,
                    ["Cufe"] = document.Cufe,
                    ["QrCode"] = document.QrCode ?? document.Cufe,
                    // URL del QR (ver InvoiceReportDataMapper): Facil Reports la reconoce y genera el QR
                    // localmente; con el motor DevExpress se descarga de ese servicio.
                    ["QrImageUrl"] = "https://api.qrserver.com/v1/create-qr-code/?size=150x150&data=" + Uri.EscapeDataString(document.QrCode ?? document.Cufe ?? string.Empty),
                    ["MedioPago"] = CatalogLabel(document.PaymentMeans, paymentMeansCatalog),
                    ["FormaPago"] = CatalogLabel(document.PaymentMeansType, formaPagoCatalog),
                    ["OrdenCompra"] = document.PurchaseOrderReference,
                    ["Notas"] = document.Notes,

                    ["Subtotal"] = Money(document.Subtotal),
                    ["Iva"] = Money(document.TaxAmount),
                    ["Descuento"] = Money(document.GeneralDiscountAmount ?? 0),
                    ["Cargo"] = Money(document.GeneralChargeAmount ?? 0),
                    ["TotalRetenciones"] = null,
                    ["NetoAPagar"] = null,
                    ["Total"] = Money(document.TotalAmount),
                    ["TotalEnLetras"] = NumberToWordsEs.ConvertirPesos(document.TotalAmount),

                    ["FabricanteSoftwareNombre"] = FabricanteSoftwareNombre,
                    ["FabricanteSoftwareNit"] = FabricanteSoftwareNit,
                    ["NombreSoftware"] = NombreSoftware,
                    ["ProveedorTecnologicoNombre"] = esIntegradorExterno ? client.Integrator.Name : null,
                    ["ProveedorTecnologicoNit"] = esIntegradorExterno ? client.Integrator.Nit : null
                },

                ["Items"] = items.Select((i, idx) => new Dictionary<string, object?>
                {
                    ["Ordinal"] = (idx + 1).ToString(),
                    ["Codigo"] = i.Code,
                    ["Nombre"] = i.Name,
                    ["Cantidad"] = Qty(i.Quantity),
                    ["Unidad"] = FormatUnidad(i, client),
                    ["ValorUnitario"] = Money(i.UnitPrice),
                    ["PorcentajeDescuento"] = Pct(i.DiscountRate),
                    ["TratamientoIva"] = i.IvaTreatment.ToString(),
                    ["PorcentajeIva"] = Pct(i.TaxRate),
                    ["ValorIva"] = Money(i.TaxAmount),
                    // "TotalLinea", no "Total": ver el comentario en InvoiceReportDataMapper.
                    ["TotalLinea"] = Money(i.TotalAmount)
                }).ToList(),

                ["Impuestos"] = impuestos
            };
        }
    }
}
