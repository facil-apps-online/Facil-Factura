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
        internal static string ResponsabilidadTexto(bool activo, string etiqueta) =>
            (activo ? "Somos " : "No somos ") + etiqueta;

        // invoice.PaymentMeansType ("CREDITO"/"DEBITO") e invoice.PaymentMeans (ej. "DEBIT_CARD")
        // guardan el Category crudo de sendos catálogos TaxCatalogItem (Kind=FormaPago y
        // Kind=PaymentMeans, ambos administrados desde Superadmin) — el llamador (InvoiceController)
        // resuelve los catálogos reales desde la base de datos y los pasa acá; no se duplica su
        // contenido en este archivo, para que un cambio en Superadmin se refleje sin tocar código.
        internal static string CatalogLabel(string? category, IReadOnlyDictionary<string, string>? catalog) =>
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
        // mostrarRetenciones: ya no tiene efecto. Las retenciones se imprimen siempre que las haya y el TOTAL las descuenta; el parámetro sigue
        // en la firma mientras se retira el interruptor "Mostrar retenciones" de las plantillas.
        public static Dictionary<string, object?> Build(Document invoice, Customer? customer, Client client, EmitterLocation location, Resolution? resolution, IReadOnlyList<DocumentItem> items, Document? originalDocument = null, bool mostrarRetenciones = true, IReadOnlyDictionary<string, string>? paymentMeansCatalog = null, IReadOnlyDictionary<string, string>? formaPagoCatalog = null, string? leyenda = null, IReadOnlyDictionary<string, string>? currencyWords = null, string? documentTypeTitle = null)
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

            var calculatedDocumentoTipo = invoice.TypeCode switch
            {
                "NC" => "NOTA CRÉDITO ELECTRÓNICA",
                "ND" => "NOTA DÉBITO ELECTRÓNICA",
                "DE-POS" => "DOCUMENTO EQUIVALENTE - TIQUETE POS",
                _ => "FACTURA ELECTRÓNICA DE VENTA"
            };

            // Mandato: la lista de observaciones; cualquier otra factura o nota: la observación general como única fila.
            var documentoTipo = string.IsNullOrWhiteSpace(documentTypeTitle)
                ? (string.IsNullOrWhiteSpace(invoice.DocumentType?.ReportTitle) ? calculatedDocumentoTipo : invoice.DocumentType!.ReportTitle)
                : documentTypeTitle!;

            var observaciones = DocumentNotes.Texts(invoice).Select((text, idx) => (Position: idx + 1, Text: text)).ToList();
            // Una factura de mandato declara un mandante en cada línea; así se reconoce sin depender del tipo de documento.
            var esMandato = items.Any(i => !string.IsNullOrWhiteSpace(i.MandanteIdentification));
            // Factura AIU: los datos del contrato viven en el documento; la plantilla imprime el objeto, el porcentaje de cada línea A/I/U y el resumen.
            var aiu = invoice.TypeCode == "FE-AIU" ? AiuContractData.TryRead(invoice.SectorExtensionData) : null;
            var aiuResumen = aiu == null ? new List<Dictionary<string, object?>>() : BuildAiuSummary(aiu, items, Money, Pct);
            // NIT lleva su dígito de verificación (el del tercero o, si no lo tiene guardado, el calculado).
            var documentoAdquirente = customer == null ? null
                : $"{DataicoMapper.MapIdentificationType(customer.IdentificationType)} {customer.IdentificationNumber}"
                  + (customer.IdentificationType == "31"
                      ? "-" + (string.IsNullOrWhiteSpace(customer.VerificationDigit) ? Fel.Infrastructure.Dian.NitValidation.CalculateCheckDigit(customer.IdentificationNumber).ToString() : customer.VerificationDigit)
                      : string.Empty);
            var ciudadAdquirente = customer == null ? null
                : !string.IsNullOrWhiteSpace(customer.CountryCode) && customer.CountryCode != "CO"
                    ? $"{customer.CityName}{(string.IsNullOrWhiteSpace(customer.StateName) ? string.Empty : ", " + customer.StateName)} ({customer.CountryCode})"
                    : customer.CityName;
            var esMonedaExtranjera =!string.IsNullOrWhiteSpace(invoice.Currency) && invoice.Currency != "COP" && invoice.ExchangeRate > 0 && invoice.ExchangeRateDate.HasValue;

            string? notaReferencia = null;
            if (originalDocument != null)
            {
                var motivo = string.IsNullOrWhiteSpace(invoice.ReferenceConcept) ? "" : $" · Motivo: {invoice.ReferenceConcept}";
                notaReferencia = $"Ajusta el documento N° {originalDocument.Number} · CUFE {originalDocument.Cufe}{motivo}";
            }

            // Agrupado por categoría+tarifa (ej. "RETE FUENTE 2.5%"), igual que el resumen que ya
            // ve el cliente en el portal — antes esta información no llegaba al PDF impreso en
            // absoluto porque el modelo de un solo DataSource no daba para una segunda tabla. El
            // TOTAL impreso sí las descuenta (ver totalAPagar), como el total del portal; el XML
            // de la DIAN conserva el valor legal del documento sin descontarlas.
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
            var totalNeto = invoice.TotalAmount - (invoice.GeneralDiscountAmount ?? 0) - (invoice.RoundingAdjustmentAmount ?? 0) + (invoice.GeneralChargeAmount ?? 0);
            // Lo que se imprime como TOTAL (y en letras): el total de la factura menos las retenciones. El XML de la DIAN conserva el total legal sin
            // descontarlas; este valor es solo el de la representación gráfica.
            var totalAPagar = totalNeto - totalRetenciones;

            // IVA discriminado por tarifa, para la misma tabla de pie que las retenciones — solo
            // ítems Gravados con tarifa > 0 generan línea (un Excluido/Exento con 0% no aporta IVA
            // que discriminar).
            var ivaPorTarifa = new Dictionary<decimal, decimal>();
            var impuestosAdicionales = new Dictionary<(string Categoria, decimal Tarifa), decimal>();
            foreach (var item in items)
            {
                if (item.TaxRate <= 0) continue;
                ivaPorTarifa[item.TaxRate] = ivaPorTarifa.GetValueOrDefault(item.TaxRate) + item.TaxAmount;
                foreach (var tax in item.AdditionalTaxes)
                {
                    var key = (tax.TaxCategory, tax.Rate);
                    impuestosAdicionales[key] = impuestosAdicionales.GetValueOrDefault(key) + tax.Amount;
                }
            }
            // Los impuestos adicionales se imprimen en la misma tabla que el IVA; no se tratan como retenciones.
            foreach (var item in items.Where(i => i.TaxRate <= 0))
            {
                foreach (var tax in item.AdditionalTaxes)
                {
                    var key = (tax.TaxCategory, tax.Rate);
                    impuestosAdicionales[key] = impuestosAdicionales.GetValueOrDefault(key) + tax.Amount;
                }
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
            foreach (var kv in impuestosAdicionales.OrderBy(kv => kv.Key.Categoria).ThenBy(kv => kv.Key.Tarifa))
            {
                impuestos.Add(new Dictionary<string, object?>
                {
                    ["Concepto"] = $"{kv.Key.Categoria} {Pct(kv.Key.Tarifa)}",
                    ["Valor"] = Money(kv.Value),
                    ["Tipo"] = "Iva"
                });
            }
            // Las retenciones se imprimen siempre que las haya, sea cual sea la plantilla.
            foreach (var kv in retencionesPorCategoria)
            {
                impuestos.Add(new Dictionary<string, object?>
                {
                    ["Concepto"] = $"{RetentionLabel(kv.Key.Categoria)} {RetPct(kv.Key.Tarifa)}",
                    ["Valor"] = Money(kv.Value),
                    ["Tipo"] = "Retencion"
                });
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
            if ((invoice.RoundingAdjustmentAmount ?? 0) > 0)
            {
                impuestos.Add(new Dictionary<string, object?>
                {
                    ["Concepto"] = string.IsNullOrWhiteSpace(invoice.RoundingAdjustmentReason) ? "Ajuste de centavos" : invoice.RoundingAdjustmentReason,
                    ["Valor"] = Money(invoice.RoundingAdjustmentAmount!.Value),
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

            var report = new Dictionary<string, object?>
            {
                ["Documento"] = new Dictionary<string, object?>
                {
                    // Emisor
                    ["EmisorLogoUrl"] = client.LogoLightUrl,
                    ["EmisorRazonSocial"] = client.CompanyName,
                    ["EmisorNombreComercial"] = client.CommercialName,
                    ["EmisorNit"] = client.TaxId,
                    ["EmisorDv"] = client.VerificationDigit,
                    ["EmisorDireccion"] = location.Address,
                    ["EmisorCiudad"] = location.City,
                    ["EmisorTelefono"] = location.Phone,
                    ["EmisorEmail"] = location.Email,
                    ["SucursalNombre"] = location.BranchName,
                    ["SucursalCodigo"] = location.BranchCode,
                    ["EmisorCalidadTributaria"] = CalidadTributaria(client.TaxRegime),
                    ["EmisorActividadEconomica"] = client.EconomicActivity,
                    ["EmisorRegimenRenta"] = IncomeTaxRegimes.Label(client.IncomeTaxRegime),
                    ["EmisorGranContribuyente"] = ResponsabilidadTexto(client.IsGranContribuyente, "Gran Contribuyente"),
                    ["EmisorAgenteRetenedorIva"] = ResponsabilidadTexto(client.IsAgenteRetenedorIva, "Agente Retenedor del Impuesto sobre las Ventas - IVA"),
                    ["EmisorAutorretenedorRenta"] = ResponsabilidadTexto(client.IsAutorretenedorRenta, "Autorretenedor del Impuesto sobre la Renta y Complementarios"),

                    // Adquirente
                    ["AdquirenteNombre"] = customer?.Name ?? "Consumidor Final",
                    ["AdquirenteTipoIdentificacion"] = customer?.IdentificationType,
                    ["AdquirenteIdentificacion"] = customer?.IdentificationNumber,
                    // Tipo y número del documento listos para imprimir: "NIT 900108281-3", "CC 41556867", "IE J070080752".
                    ["AdquirenteDocumento"] = documentoAdquirente,
                    ["AdquirenteDireccion"] = customer?.Address,
                    // Fuera de Colombia la ciudad lleva el estado y el país: "Lima, Lima (PE)".
                    ["AdquirenteCiudad"] = ciudadAdquirente,
                    ["AdquirentePais"] = customer?.CountryCode,
                    ["AdquirenteTelefono"] = customer?.Phone,
                    ["AdquirenteEmail"] = customer?.Email,

                    // Documento
                    ["DocumentoTipo"] = documentoTipo,
                    ["DocumentoNumero"] = $"{(string.IsNullOrWhiteSpace(invoice.Prefix) ? resolution?.Prefix : invoice.Prefix)} {invoice.Number}".Trim(),
                    ["NotaReferencia"] = notaReferencia,
                    ["ResolucionTexto"] = resolution == null ? null :
                        $"Resolución de facturación: {resolution.ResolutionNumber} vigente desde {resolution.ValidFrom:dd/MM/yyyy} hasta: {resolution.ValidTo:dd/MM/yyyy}. Del {resolution.Prefix} {resolution.NumberStart} al {resolution.Prefix} {resolution.NumberEnd}.",
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
                    ["QrImageUrl"] = QrImageDataUri.FromText(invoice.QrCode ?? invoice.Cufe),
                    ["MedioPago"] = CatalogLabel(invoice.PaymentMeans, paymentMeansCatalog),
                    ["FormaPago"] = CatalogLabel(invoice.PaymentMeansType, formaPagoCatalog),
                    ["OrdenCompra"] = invoice.PurchaseOrderReference,
                    // Todas las observaciones en un solo texto, una por línea: lo que leen las plantillas que todavía usan un
                    // solo campo. Las plantillas que quieran una fila por observación usan la lista "Observaciones".
                    ["Notas"] = string.Join("\n", observaciones.Select(o => o.Text)),
                    ["TipoOperacion"] = esMandato ? "Mandatos" : invoice.TypeCode == "FE-EXP" ? "Exportación" : aiu != null ? "AIU" : "Estándar",
                    // Factura AIU: objeto del contrato ya listo para imprimir; vacío en cualquier otra factura.
                    ["AiuObjeto"] = aiu == null ? null : $"Objeto del contrato: {aiu.ContractObject.Trim()}",
                    // Factura de exportación: condición de entrega lista para imprimir ("CFR - Costo y flete"); vacía en otra factura.
                    ["Incoterm"] = string.IsNullOrWhiteSpace(invoice.DeliveryTermsCode) ? null
                        : string.IsNullOrWhiteSpace(invoice.DeliveryTermsName) ? invoice.DeliveryTermsCode : $"{invoice.DeliveryTermsCode} - {invoice.DeliveryTermsName}",
                    // Moneda del documento: todos los valores del PDF están en esa moneda. En COP, Trm y TrmFecha van vacíos y
                    // MonedaTexto queda en blanco para que la plantilla oculte la línea.
                    ["Moneda"] = string.IsNullOrWhiteSpace(invoice.Currency) ? "COP" : invoice.Currency,
                    ["Trm"] = esMonedaExtranjera ? invoice.ExchangeRate!.Value.ToString("N2", nf) : null,
                    ["TrmFecha"] = esMonedaExtranjera ? invoice.ExchangeRateDate!.Value.ToString("dd/MM/yyyy") : null,
                    ["MonedaTexto"] = esMonedaExtranjera
                        ? $"{invoice.Currency} ({invoice.ExchangeRate!.Value.ToString("N2", nf)} - {invoice.ExchangeRateDate!.Value:dd/MM/yyyy})"
                        : null,
                    ["Leyenda"] = leyenda,

                    // Totales — ya formateados como texto (separadores según Client.DecimalSeparator, 2 decimales).
                    ["Subtotal"] = Money(invoice.Subtotal),
                    ["Iva"] = Money(invoice.TaxAmount),
                    ["Descuento"] = Money((invoice.GeneralDiscountAmount ?? 0) + (invoice.RoundingAdjustmentAmount ?? 0)),
                    ["Cargo"] = Money(invoice.GeneralChargeAmount ?? 0),
                    // Ambos null cuando la plantilla tiene MostrarRetenciones=false o el documento
                    // no tiene retenciones — así la plantilla oculta la línea sin más lógica.
                    ["TotalRetenciones"] = totalRetenciones > 0 ? Money(totalRetenciones) : null,
                    // TotalAmount es el bruto (subtotal + IVA): el total de la factura aplica además el
                    // descuento y el cargo general, y el neto a pagar resta las retenciones a ese total.
                    // El TOTAL ya descuenta las retenciones: la fila "Neto a pagar" de los formatos queda oculta (se muestra solo con valor).
                    ["NetoAPagar"] = null,
                    ["Total"] = Money(totalAPagar),
                    ["TotalEnLetras"] = NumberToWordsEs.Convertir(totalAPagar, CurrencyWords.Resolve(currencyWords, invoice.Currency)),

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
                    ["TotalLinea"] = Money(i.TotalAmount),
                    // Factura de mandato: mandante de la línea, y el texto que se imprime bajo la descripción (como en la
                    // representación gráfica de Dataico: "Mandante NIT: 800168654"). Vacíos en cualquier otra factura.
                    // Exportación: marca, modelo y subpartida arancelaria del ítem; vacíos en otra factura.
                    ["Marca"] = i.BrandName,
                    ["Modelo"] = i.ModelName,
                    ["PartidaArancelaria"] = i.TariffCode,
                    ["MandanteNombre"] = i.MandanteName,
                    ["MandanteIdentificacion"] = i.MandanteIdentification,
                    ["MandanteTexto"] = string.IsNullOrWhiteSpace(i.MandanteIdentification) ? null
                        : $"Mandante {DataicoMapper.MapIdentificationType(i.MandanteIdentificationType ?? string.Empty)}: {i.MandanteIdentification}",
                    // Factura AIU: porcentaje de las líneas de Administración, Imprevistos y Utilidad; vacío en las demás.
                    ["PorcentajeAiu"] = aiu == null ? null
                        : i.Code == AiuContractData.AdminCode ? Pct(aiu.AdminPercent)
                        : i.Code == AiuContractData.UnforeseenCode ? Pct(aiu.UnforeseenPercent)
                        : i.Code == AiuContractData.ProfitCode ? Pct(aiu.ProfitPercent)
                        : null
                }).ToList(),

                // Resumen del contrato AIU (valor del servicio, A, I, U y base gravable del IVA): la plantilla lo pinta tras los ítems. Vacío fuera de AIU.
                ["Aiu"] = aiuResumen,

                // Una fila por observación, en su orden: la plantilla la pinta como un grupo de detalle, justo después de los ítems.
                ["Observaciones"] = observaciones.Select(o => new Dictionary<string, object?>
                {
                    ["Numero"] = o.Position.ToString(),
                    ["Texto"] = o.Text
                }).ToList(),

                ["Impuestos"] = impuestos
            };

            // Compatibilidad con las plantillas REPX antiguas (incluidas las tirillas POS
            // propias del sistema) que todavía leen los campos como Parameters.EmisorLogoUrl,
            // Parameters.EmisorRazonSocial, etc. Las plantillas nuevas usan Documento.*.
            // Mantener ambos contratos permite publicar una plantilla personalizada sin exigir
            // que se rediseñen las existentes.
            if (report["Documento"] is Dictionary<string, object?> documento)
            {
                foreach (var field in documento)
                    report.TryAdd(field.Key, field.Value);
            }

            return report;
        }

        // Filas del resumen AIU. Los valores salen de las líneas del documento (lo que de verdad se factura), no se recalculan con los
        // porcentajes. La base gravable del IVA es la suma de las líneas gravadas: la Utilidad y, si lleva IVA, el servicio.
        private static List<Dictionary<string, object?>> BuildAiuSummary(
            AiuContractData aiu, IReadOnlyList<DocumentItem> items, Func<decimal, string> money, Func<decimal, string> pct)
        {
            decimal LineBase(DocumentItem i) => i.Quantity * i.UnitPrice * (1 - i.DiscountRate / 100m);
            Dictionary<string, object?> Row(string concept, string percent, decimal value) =>
                new() { ["Concepto"] = concept, ["Porcentaje"] = percent, ["Valor"] = money(value) };

            var rows = new List<Dictionary<string, object?>>();
            var service = items.FirstOrDefault(i => i.Code == AiuContractData.ServiceCode);
            // Los borradores AIU anteriores no tienen línea de servicio: en ese caso la fila no se imprime.
            if (service != null) rows.Add(Row("Valor del servicio (base del contrato)", string.Empty, LineBase(service)));
            void Add(string concept, string code, decimal percent)
            {
                var line = items.FirstOrDefault(i => i.Code == code);
                if (line != null) rows.Add(Row(concept, pct(percent), LineBase(line)));
            }
            Add("Administración", AiuContractData.AdminCode, aiu.AdminPercent);
            Add("Imprevistos", AiuContractData.UnforeseenCode, aiu.UnforeseenPercent);
            Add("Utilidad", AiuContractData.ProfitCode, aiu.ProfitPercent);
            rows.Add(Row("Base gravable del IVA", string.Empty, items.Where(i => i.IvaTreatment == IvaTreatment.Gravado).Sum(LineBase)));
            return rows;
        }
    }
}
