using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Fel.Core.Entities;
using Fel.Core.Models;
using Fel.Infrastructure.Services;

namespace Fel.Infrastructure.Dataico
{
    // Arma el JSON que Facil Reports bindea a un JsonDataSource en la plantilla .repx de Factura/
    // Nota Crédito/Nota Débito. Reemplaza el modelo anterior (~30 Parameters planos + un único
    // DataSource) por un único objeto con tres ramas: "Documento" (los campos de un solo valor,
    // como antes), "Items" e "Impuestos" (IVA discriminado por tarifa + retenciones, esta última
    // solo si la plantilla lo activa) — cada arreglo anidado se vuelve su propia tabla navegable
    // en el Field List del Diseñador Web (JsonDataSource lo resuelve solo). El nombre y forma de
    // cada campo dentro de "Documento" es el mismo de antes, así que una plantilla vieja que ya
    // referenciaba [EmisorNit] etc. solo necesita recalificar la ruta a [Documento.EmisorNit] al
    // migrar el binding, no reaprender los nombres.
    //
    // Todos los valores van como texto ya formateado (incluidos los montos) — así el .repx no
    // necesita Parameters tipados ni funciones de formato en sus expresiones.
    public static class InvoiceReportDataMapper
    {
        // Facil Factura es un producto de SoFactory S.A.S., el fabricante legal del software.
        private const string FabricanteSoftwareNombre = "SoFactory S.A.S.";
        private const string FabricanteSoftwareNit = "900.303.194-6";
        private const string NombreSoftware = "Facil Factura";


        private static readonly Dictionary<string, string> RetentionCategoryLabels = new()
        {
            ["RET_FUENTE"] = "RETE FUENTE",
            ["RET_ICA"] = "RETE ICA",
            ["RET_IVA"] = "RETE IVA"
        };

        // client.TaxRegime guarda el código crudo de responsabilidad tributaria (RUT casilla 53:
        // 48 = responsable de IVA, 49 = no responsable) — la representación gráfica debe mostrar
        // la etiqueta legible, no el código, que sí se usa tal cual en el resto del sistema.
        private static readonly Dictionary<string, string> CalidadTributariaLabels = new()
        {
            ["48"] = "Responsable de IVA",
            ["49"] = "No responsable de IVA"
        };

        private static string CalidadTributaria(string? code) =>
            code != null && CalidadTributariaLabels.TryGetValue(code, out var label) ? label : code ?? string.Empty;

        // Redacción idéntica a la que usan otros software de facturación (ej. Dataico) para
        // declarar Gran Contribuyente / Agente Retenedor de IVA / Autorretenedor en el header.
        private static string ResponsabilidadTexto(bool activo, string etiqueta) =>
            (activo ? "Somos " : "No somos ") + etiqueta;

        // invoice.PaymentMeansType ("CREDITO"/"DEBITO") e invoice.PaymentMeans (ej. "DEBIT_CARD")
        // guardan el Category crudo de sendos catálogos TaxCatalogItem (Kind=FormaPago y
        // Kind=PaymentMeans, ambos administrados desde Superadmin) — el llamador (InvoiceController)
        // resuelve los catálogos reales desde la base de datos y los pasa acá; no se duplica su
        // contenido en este archivo, para que un cambio en Superadmin se refleje sin tocar código.
        private static string CatalogLabel(string? category, IReadOnlyDictionary<string, string>? catalog) =>
            !string.IsNullOrEmpty(category) && catalog != null && catalog.TryGetValue(category, out var label) ? label : category ?? string.Empty;

        // client.UnitOfMeasureDisplayOverride, si está seteado, manda sobre el formato que trae
        // cada fila del catálogo — así un Client puede forzar un único criterio (código, sigla o
        // ambos) para todas sus facturas sin importar qué unidad sea.
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

        private static string RetentionLabel(string category) => RetentionCategoryLabels.GetValueOrDefault(category, category);

        // originalDocument: el documento referenciado por una Nota Crédito/Débito
        // (invoice.ReferenceDocumentId) — null para una factura normal. Notas Crédito/Débito
        // reutilizan el mismo .repx de Factura porque manejan casi la misma información; lo único
        // que cambia es el título (DocumentoTipo) y esta referencia al documento que ajustan.
        //
        // mostrarRetenciones: viene de DocumentTemplate.MostrarRetenciones de la plantilla
        // resuelta para este documento — decide si las filas de retención entran a "Impuestos" y
        // si se calcula "NetoAPagar". El IVA discriminado se arma siempre, independiente de este
        // flag.
        public static Dictionary<string, object?> Build(Document invoice, Customer? customer, Client client, Resolution? resolution, IReadOnlyList<DocumentItem> items, Document? originalDocument = null, bool mostrarRetenciones = true, IReadOnlyDictionary<string, string>? paymentMeansCatalog = null, IReadOnlyDictionary<string, string>? formaPagoCatalog = null)
        {
            // Separadores del cliente (Client.DecimalSeparator): punto decimal y coma de miles por defecto.
            var nf = ReportNumberFormat.For(client.DecimalSeparator);
            string Money(decimal value) => value.ToString("N2", nf);
            string Qty(decimal value) => value.ToString("0.##", nf);
            string Pct(decimal value) => value.ToString("0.##", nf) + "%";
            // Las tarifas de retención llevan hasta 3 decimales (ReteICA 0,414 % / 0,966 %): con "0.##" se
            // imprimían recortadas ("0,97%" para 0,966 %).
            string RetPct(decimal value) => value.ToString("0.###", nf) + "%";
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

            // Agrupado por categoría+tarifa (ej. "RETE FUENTE 2.5%"), igual que el resumen que ya
            // ve el cliente en el portal — antes esta información no llegaba al PDF impreso en
            // absoluto porque el modelo de un solo DataSource no daba para una segunda tabla. El
            // total de la factura (Total/ValTolFac) NO se descuenta por esto: la retención es una
            // constancia informativa de lo que el adquirente debe declarar ante la DIAN a nombre
            // del emisor, no una rebaja del valor legal del documento.
            var retencionesPorCategoria = new Dictionary<(string Categoria, decimal Tarifa), decimal>();
            foreach (var item in items)
            {
                foreach (var r in item.Retentions)
                {
                    var key = (r.TaxCategory, r.Rate);
                    // Dataico calcula estas retenciones por su cuenta (solo recibe la tarifa), con la base
                    // redondeada de la línea; se imprime el mismo valor con la misma regla (DianRounding).
                    retencionesPorCategoria[key] = retencionesPorCategoria.GetValueOrDefault(key)
                        + DianRounding.Round2(DianRounding.Round2(r.BaseAmount) * r.Rate / 100);
                }
            }
            foreach (var gr in invoice.GeneralRetentions)
            {
                // Las retenciones generales viajan a Dataico al nivel del documento (solo categoría y
                // tarifa) y Dataico las calcula sobre los totales: base redondeada por la tarifa.
                var baseAmount = gr.TaxCategory == "RET_IVA" ? invoice.TaxAmount : invoice.Subtotal;
                var key = (gr.TaxCategory, gr.Rate);
                retencionesPorCategoria[key] = retencionesPorCategoria.GetValueOrDefault(key)
                    + DianRounding.Round2(DianRounding.Round2(baseAmount) * gr.Rate / 100);
            }
            var totalRetenciones = retencionesPorCategoria.Values.Sum();

            // IVA discriminado por tarifa, para la misma tabla de pie que las retenciones — solo
            // ítems Gravados con tarifa > 0 generan línea (un Excluido/Exento con 0% no aporta IVA
            // que discriminar).
            var ivaPorTarifa = new Dictionary<decimal, decimal>();
            foreach (var item in items)
            {
                if (item.TaxRate <= 0) continue;
                ivaPorTarifa[item.TaxRate] = ivaPorTarifa.GetValueOrDefault(item.TaxRate) + item.TaxAmount;
            }

            // Tabla unificada para el subreporte del pie: IVA (suma, texto normal) y — solo si la
            // plantilla activa MostrarRetenciones — retenciones (restan, en rojo vía [Tipo] en el
            // .repx). El signo real de "cuánto queda" lo expresa NetoAPagar más abajo, no esta
            // tabla: aquí cada Valor va sin signo, igual que el resto del contrato.
            var impuestos = new List<Dictionary<string, object?>>();
            foreach (var kv in ivaPorTarifa.OrderBy(kv => kv.Key))
            {
                impuestos.Add(new Dictionary<string, object?>
                {
                    ["Concepto"] = $"IVA {Pct(kv.Key)}",
                    ["Valor"] = Money(kv.Value),
                    ["Tipo"] = "Iva"
                });
            }
            if (mostrarRetenciones)
            {
                foreach (var kv in retencionesPorCategoria)
                {
                    impuestos.Add(new Dictionary<string, object?>
                    {
                        ["Concepto"] = $"{RetentionLabel(kv.Key.Categoria)} {RetPct(kv.Key.Tarifa)}",
                        ["Valor"] = Money(kv.Value),
                        ["Tipo"] = "Retencion"
                    });
                }
            }

            // Descuento/Cargo general van en la misma tabla del pie, después de impuestos y
            // retenciones — no como campos fijos aparte (así el motivo queda visible junto al
            // valor, igual que ya pasa con cada retención, y la posición en la representación
            // gráfica sale gratis por ser la misma lista repetida).
            if ((invoice.GeneralDiscountAmount ?? 0) > 0)
            {
                impuestos.Add(new Dictionary<string, object?>
                {
                    ["Concepto"] = string.IsNullOrWhiteSpace(invoice.GeneralDiscountReason) ? "Descuento general" : invoice.GeneralDiscountReason,
                    ["Valor"] = Money(invoice.GeneralDiscountAmount!.Value),
                    ["Tipo"] = "Descuento"
                });
            }
            if ((invoice.GeneralChargeAmount ?? 0) > 0)
            {
                impuestos.Add(new Dictionary<string, object?>
                {
                    ["Concepto"] = string.IsNullOrWhiteSpace(invoice.GeneralChargeReason) ? "Cargo general" : invoice.GeneralChargeReason,
                    ["Valor"] = Money(invoice.GeneralChargeAmount!.Value),
                    ["Tipo"] = "Cargo"
                });
            }

            return new Dictionary<string, object?>
            {
                ["Documento"] = new Dictionary<string, object?>
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
                    ["EmisorCalidadTributaria"] = CalidadTributaria(client.TaxRegime),
                    ["EmisorActividadEconomica"] = client.EconomicActivity,
                    ["EmisorGranContribuyente"] = ResponsabilidadTexto(client.IsGranContribuyente, "Gran Contribuyente"),
                    ["EmisorAgenteRetenedorIva"] = ResponsabilidadTexto(client.IsAgenteRetenedorIva, "Agente Retenedor del Impuesto sobre las Ventas - IVA"),
                    ["EmisorAutorretenedorRenta"] = ResponsabilidadTexto(client.IsAutorretenedorRenta, "Autorretenedor del Impuesto sobre la Renta y Complementarios"),

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
                    ["FechaGeneracion"] = Fel.Core.Models.ColombiaTime.FromUtc(invoice.CreatedAt).ToString("dd/MM/yyyy HH:mm:ss"),
                    ["FechaVencimiento"] = invoice.PaymentTermDays.HasValue ? invoice.IssueDate.AddDays(invoice.PaymentTermDays.Value).ToString("dd/MM/yyyy") : null,
                    ["Cufe"] = invoice.Cufe,
                    // Contenido completo para el código QR de la representación gráfica (no solo
                    // el CUFE) — invoice.QrCode es null en documentos emitidos antes de que este
                    // campo existiera, así que cae de vuelta al CUFE solo para esos casos
                    // históricos.
                    ["QrCode"] = invoice.QrCode ?? invoice.Cufe,
                    // URL ya lista para el XRPictureBox del QR — se arma aquí (no en la
                    // plantilla) porque el contenido del QR trae saltos de línea y otros
                    // caracteres que romperían el query string si se concatenan sin escapar.
                    ["QrImageUrl"] = "https://api.qrserver.com/v1/create-qr-code/?size=150x150&data=" + Uri.EscapeDataString(invoice.QrCode ?? invoice.Cufe ?? string.Empty),
                    ["MedioPago"] = CatalogLabel(invoice.PaymentMeans, paymentMeansCatalog),
                    ["FormaPago"] = CatalogLabel(invoice.PaymentMeansType, formaPagoCatalog),
                    ["OrdenCompra"] = invoice.PurchaseOrderReference,
                    ["Notas"] = invoice.Notes,

                    // Totales — ya formateados como texto (separadores según Client.DecimalSeparator, 2 decimales).
                    ["Subtotal"] = Money(invoice.Subtotal),
                    ["Iva"] = Money(invoice.TaxAmount),
                    ["Descuento"] = Money(invoice.GeneralDiscountAmount ?? 0),
                    ["Cargo"] = Money(invoice.GeneralChargeAmount ?? 0),
                    // Ambos null cuando la plantilla tiene MostrarRetenciones=false o el documento
                    // no tiene retenciones — así la plantilla oculta la línea sin más lógica.
                    ["TotalRetenciones"] = mostrarRetenciones && totalRetenciones > 0 ? Money(totalRetenciones) : null,
                    ["NetoAPagar"] = mostrarRetenciones && totalRetenciones > 0 ? Money(invoice.TotalAmount - totalRetenciones) : null,
                    ["Total"] = Money(invoice.TotalAmount),
                    ["TotalEnLetras"] = NumberToWordsEs.ConvertirPesos(invoice.TotalAmount),

                    // Identificación de software (numeral 18, Art. 11 Res. 000165 de 2023) — el
                    // Proveedor Tecnológico solo aplica si el Client factura a través de un
                    // integrador externo (catálogo Integrator, Kind=ThirdPartyIntegrator); en DIAN
                    // directa el Cliente tiene su propio SoftwareId/SoftwarePin y estos campos
                    // quedan vacíos (la plantilla debe ocultar la línea cuando vengan en blanco).
                    ["FabricanteSoftwareNombre"] = FabricanteSoftwareNombre,
                    ["FabricanteSoftwareNit"] = FabricanteSoftwareNit,
                    ["NombreSoftware"] = NombreSoftware,
                    ["ProveedorTecnologicoNombre"] = esIntegradorExterno ? client.Integrator.Name : null,
                    ["ProveedorTecnologicoNit"] = esIntegradorExterno ? client.Integrator.Nit : null
                },

                ["Items"] = items.Select((i, idx) => new Dictionary<string, object?>
                {
                    // Numeral exigido por la DIAN para el detalle de la factura (cada línea
                    // debe ir enumerada secuencialmente en la representación gráfica).
                    ["Ordinal"] = (idx + 1).ToString(),
                    ["Codigo"] = i.Code,
                    ["Nombre"] = i.Name,
                    ["Cantidad"] = Qty(i.Quantity),
                    ["Unidad"] = FormatUnidad(i, client),
                    ["ValorUnitario"] = Money(i.UnitPrice),
                    ["PorcentajeDescuento"] = Pct(i.DiscountRate),
                    // Gravado/Exento/Excluido — sin esto, un ítem Excluido (0% IVA, fuera del
                    // ámbito del impuesto) y uno Gravado casualmente al 0% se imprimían idénticos
                    // (ambos con "0%" y sin IVA), sin forma de distinguirlos en el comprobante.
                    ["TratamientoIva"] = i.IvaTreatment.ToString(),
                    ["PorcentajeIva"] = Pct(i.TaxRate),
                    ["ValorIva"] = Money(i.TaxAmount),
                    ["TotalLinea"] = Money(i.TotalAmount)
                }).ToList(),

                ["Impuestos"] = impuestos
            };
        }
    }
}
