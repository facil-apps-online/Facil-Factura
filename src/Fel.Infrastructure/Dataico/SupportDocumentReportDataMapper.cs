using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Fel.Core.Entities;
using Fel.Infrastructure.Services;

namespace Fel.Infrastructure.Dataico
{
    // Igual contrato de nombres planos que InvoiceReportDataMapper, pero con "Proveedor" en vez de
    // "Adquirente" (en Documento Soporte, el Cliente le compra al tercero, no le vende) y sin forma
    // de pago/medio de pago obligatorios en la nota de ajuste. Mismos numerales DIAN (1-5, 8-13,
    // 15, 16, 18) que Factura, adaptados a este tipo de documento.
    public static class SupportDocumentReportDataMapper
    {
        // Facil Factura es un producto de SoFactory S.A.S., el fabricante legal del software.
        private const string FabricanteSoftwareNombre = "SoFactory S.A.S.";
        private const string FabricanteSoftwareNit = "900.303.194-6";
        private const string NombreSoftware = "Facil Factura";

        private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");
        private static string Money(decimal value) => value.ToString("N0", Co);
        private static string Qty(decimal value) => value.ToString("0.##", Co);
        private static string Pct(decimal value) => value.ToString("0.##", Co) + "%";

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
            var esIntegradorExterno = client.Integrator.Kind == IntegratorKind.ThirdPartyIntegrator;
            var esAjuste = document.TypeCode == "DS-AJUSTE";

            return new Dictionary<string, object?>
            {
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

                ["ProveedorNombre"] = proveedor?.Name,
                ["ProveedorTipoIdentificacion"] = proveedor?.IdentificationType,
                ["ProveedorIdentificacion"] = proveedor?.IdentificationNumber,
                ["ProveedorDireccion"] = proveedor?.Address,
                ["ProveedorCiudad"] = proveedor?.CityName,
                ["ProveedorTelefono"] = proveedor?.Phone,
                ["ProveedorEmail"] = proveedor?.Email,

                ["DocumentoTitulo"] = esAjuste ? "NOTA DE AJUSTE - DOCUMENTO SOPORTE" : "DOCUMENTO SOPORTE DE PAGO",
                ["DocumentoNumero"] = $"{resolution?.Prefix} {document.Number}".Trim(),
                ["ResolucionTexto"] = resolution == null ? null :
                    $"Resolución DIAN {resolution.ResolutionNumber} · Rango {resolution.Prefix} {resolution.NumberStart}-{resolution.NumberEnd}" +
                    $" · Vigente hasta {resolution.ValidTo:dd/MM/yyyy}",
                ["FechaGeneracion"] = document.CreatedAt.ToString("dd/MM/yyyy HH:mm:ss"),
                ["Cufe"] = document.Cufe,
                ["QrCode"] = document.QrCode ?? document.Cufe,
                ["MedioPago"] = CatalogLabel(document.PaymentMeans, paymentMeansCatalog),
                ["FormaPago"] = CatalogLabel(document.PaymentMeansType, formaPagoCatalog),
                ["OrdenCompra"] = document.PurchaseOrderReference,
                ["ReferenciaAjuste"] = esAjuste ? document.ReferenceConcept : null,
                ["Notas"] = document.Notes,

                ["Subtotal"] = Money(document.Subtotal),
                ["Iva"] = Money(document.TaxAmount),
                ["Descuento"] = Money(document.GeneralDiscountAmount ?? 0),
                ["Total"] = Money(document.TotalAmount),
                ["TotalEnLetras"] = NumberToWordsEs.ConvertirPesos(document.TotalAmount),

                ["FabricanteSoftwareNombre"] = FabricanteSoftwareNombre,
                ["FabricanteSoftwareNit"] = FabricanteSoftwareNit,
                ["NombreSoftware"] = NombreSoftware,
                ["ProveedorTecnologicoNombre"] = esIntegradorExterno ? client.Integrator.Name : null,
                ["ProveedorTecnologicoNit"] = esIntegradorExterno ? client.Integrator.Nit : null,

                ["DataSource"] = items.Select(i => new Dictionary<string, object?>
                {
                    ["Codigo"] = i.Code,
                    ["Nombre"] = i.Name,
                    ["Cantidad"] = Qty(i.Quantity),
                    ["Unidad"] = FormatUnidad(i, client),
                    ["ValorUnitario"] = Money(i.UnitPrice),
                    ["PorcentajeIva"] = Pct(i.TaxRate),
                    ["ValorIva"] = Money(i.TaxAmount),
                    // "TotalLinea", no "Total": ver el comentario en InvoiceReportDataMapper.
                    ["TotalLinea"] = Money(i.TotalAmount)
                }).ToList()
            };
        }
    }
}
