using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Fel.Core.Entities;
using Fel.Infrastructure.Services;

namespace Fel.Infrastructure.Dataico
{
    // Arma el diccionario de datos que Facil Reports bindea a los Parameters/DataSource de la
    // plantilla .repx del Paso 2 ("documentos personalizados"). Los nombres de clave usados aquí
    // SON el contrato de datos que debe seguir cualquier plantilla diseñada para facturas/notas —
    // documentarlos aquí es la única fuente de verdad mientras no exista el diseñador embebido en
    // el sitio.
    //
    // Claves planas (sin punto): ReportGenerator.ApplyData en Facil Reports aplana objetos
    // anidados a "padre.hijo" y bindea por nombre exacto a report.Parameters — pero DevExpress usa
    // el punto en sus expresiones para acceso a miembros ([Tabla.Campo]), así que un Parameter
    // llamado "emisor.razon_social" referenciado como [emisor.razon_social] se presta a
    // ambigüedad. Por eso aquí todo es plano (EmisorRazonSocial, DocumentoNumero, etc.).
    //
    // Todos los valores van como texto ya formateado (incluidos los montos) — así el .repx no
    // necesita Parameters tipados ni funciones de formato en sus expresiones (ambos mecanismos que
    // no podemos confirmar sin abrir el archivo en el Report Designer real de DevExpress); solo
    // referencia el Parameter como texto plano.
    public static class InvoiceReportDataMapper
    {
        // Facil Factura es un producto de SoFactory S.A.S., el fabricante legal del software.
        private const string FabricanteSoftwareNombre = "SoFactory S.A.S.";
        private const string FabricanteSoftwareNit = "900.303.194-6";
        private const string NombreSoftware = "Facil Factura";

        private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

        private static string Money(decimal value) => value.ToString("N0", Co);
        private static string Qty(decimal value) => value.ToString("0.##", Co);
        private static string Pct(decimal value) => value.ToString("0.##", Co) + "%";

        // originalDocument: el documento referenciado por una Nota Crédito/Débito
        // (invoice.ReferenceDocumentId) — null para una factura normal. Notas Crédito/Débito
        // reutilizan el mismo .repx de Factura porque manejan casi la misma información; lo único
        // que cambia es el título (DocumentoTipo) y esta referencia al documento que ajustan.
        public static Dictionary<string, object?> Build(Document invoice, Customer? customer, Client client, Resolution? resolution, IReadOnlyList<DocumentItem> items, Document? originalDocument = null)
        {
            var esIntegradorExterno = client.Integrator.Kind == IntegratorKind.ThirdPartyIntegrator;

            var documentoTipo = invoice.TypeCode switch
            {
                "NC" => "NOTA CRÉDITO ELECTRÓNICA",
                "ND" => "NOTA DÉBITO ELECTRÓNICA",
                "DE-POS" => "DOCUMENTO EQUIVALENTE - TIQUETE POS",
                _ => "FACTURA ELECTRÓNICA DE VENTA"
            };

            string? notaReferencia = null;
            if (originalDocument != null)
            {
                var motivo = string.IsNullOrWhiteSpace(invoice.ReferenceConcept) ? "" : $" · Motivo: {invoice.ReferenceConcept}";
                notaReferencia = $"Ajusta el documento N° {originalDocument.Number} · CUFE {originalDocument.Cufe}{motivo}";
            }

            return new Dictionary<string, object?>
            {
                // Emisor
                ["EmisorLogoUrl"] = client.LogoLightUrl,
                ["EmisorRazonSocial"] = client.CompanyName,
                ["EmisorNombreComercial"] = client.CommercialName,
                ["EmisorNit"] = client.TaxId,
                ["EmisorDv"] = client.VerificationDigit,
                ["EmisorDireccion"] = client.Address,
                ["EmisorCiudad"] = client.City,
                ["EmisorTelefono"] = client.Phone,
                ["EmisorEmail"] = client.Email,
                ["EmisorCalidadTributaria"] = client.TaxRegime,

                // Adquirente
                ["AdquirenteNombre"] = customer?.Name ?? "Consumidor Final",
                ["AdquirenteTipoIdentificacion"] = customer?.IdentificationType,
                ["AdquirenteIdentificacion"] = customer?.IdentificationNumber,
                ["AdquirenteDireccion"] = customer?.Address,
                ["AdquirenteCiudad"] = customer?.CityName,
                ["AdquirenteTelefono"] = customer?.Phone,
                ["AdquirenteEmail"] = customer?.Email,

                // Documento
                ["DocumentoTipo"] = documentoTipo,
                ["DocumentoNumero"] = $"{resolution?.Prefix} {invoice.Number}".Trim(),
                ["NotaReferencia"] = notaReferencia,
                ["ResolucionTexto"] = resolution == null ? null :
                    $"Resolución DIAN {resolution.ResolutionNumber} · Rango {resolution.Prefix} {resolution.NumberStart}-{resolution.NumberEnd}" +
                    $" · Vigente hasta {resolution.ValidTo:dd/MM/yyyy}",
                ["FechaGeneracion"] = invoice.CreatedAt.ToString("dd/MM/yyyy HH:mm:ss"),
                ["FechaVencimiento"] = invoice.PaymentTermDays.HasValue ? invoice.IssueDate.AddDays(invoice.PaymentTermDays.Value).ToString("dd/MM/yyyy") : null,
                ["Cufe"] = invoice.Cufe,
                ["MedioPago"] = invoice.PaymentMeans,
                ["FormaPago"] = invoice.PaymentMeansType,
                ["OrdenCompra"] = invoice.PurchaseOrderReference,
                ["Notas"] = invoice.Notes,

                // Totales — ya formateados como texto (miles con punto, sin decimales, es-CO).
                ["Subtotal"] = Money(invoice.Subtotal),
                ["Iva"] = Money(invoice.TaxAmount),
                ["Descuento"] = Money(invoice.GeneralDiscountAmount ?? 0),
                ["Cargo"] = Money(invoice.GeneralChargeAmount ?? 0),
                ["Total"] = Money(invoice.TotalAmount),
                ["TotalEnLetras"] = NumberToWordsEs.ConvertirPesos(invoice.TotalAmount),

                // Identificación de software (numeral 18, Art. 11 Res. 000165 de 2023) — el
                // Proveedor Tecnológico solo aplica si el Client factura a través de un integrador
                // externo (catálogo Integrator, Kind=ThirdPartyIntegrator); en DIAN directa el
                // Cliente tiene su propio SoftwareId/SoftwarePin y estos campos quedan vacíos (la
                // plantilla debe ocultar la línea cuando vengan en blanco).
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
                    // DocumentItem no tiene todavía un campo propio de unidad de medida (numeral 8,
                    // Art. 11 Res. 000165 de 2023) — "Unidad" es el default genérico DIAN (código
                    // 94) hasta que se agregue esa columna.
                    ["Unidad"] = "Unidad",
                    ["ValorUnitario"] = Money(i.UnitPrice),
                    ["PorcentajeIva"] = Pct(i.TaxRate),
                    ["ValorIva"] = Money(i.TaxAmount),
                    // "TotalLinea", no "Total": el .repx tiene un Parameter separado llamado
                    // "Total" (el gran total de la factura) — DevExpress resuelve mal la
                    // ambigüedad si una columna del DataSource comparte el nombre exacto de un
                    // Parameter, incluso calificándolo como [Parameters.Total].
                    ["TotalLinea"] = Money(i.TotalAmount)
                }).ToList()
            };
        }
    }
}
