using System.Collections.Generic;
using System.Linq;
using Fel.Core.Entities;
using Fel.Core.Models;

namespace Fel.Infrastructure.Dian
{
    // Convierte las entidades de la factura del portal (Document/Customer/Client/Resolution) al
    // formato UblInvoiceData que necesita IUblGenerator — el equivalente para DIAN directa del
    // DataicoDocumentMapper. El nombre del departamento (y, si el emisor no tiene CityCode
    // cargado, el código) depende del catálogo DianMunicipalities: mientras esté vacío o
    // incompleto, esos campos quedan en blanco y el documento no pasaría la validación real de la
    // DIAN, pero el resto del mapeo (líneas, impuestos, resolución, terceros) es correcto ya.
    public static class DianDocumentMapper
    {
        // Código DIAN (tabla 13.3.4.2 Medios de Pago) para pago a crédito sin instrumento fijo
        // todavía definido — "ZZZ: Acuerdo mutuo". Fijo y no de catálogo: se usa siempre que la
        // venta es a crédito, sin importar qué Medio de Pago quedó seleccionado en el documento
        // (que solo aplica al de contado). Es el mismo criterio que ya usa Dataico
        // (DataicoDocumentMapper.CreditoDefaultPaymentMeans = "MUTUAL_AGREEMENT").
        private const string CreditoPaymentMeansCode = "ZZZ";

        public static UblInvoiceData BuildInvoiceData(
            Document document,
            Customer customer,
            IEnumerable<DocumentItem> items,
            Resolution resolution,
            Client client,
            IReadOnlyDictionary<string, DianMunicipality> municipalitiesByCode,
            IReadOnlyDictionary<string, string>? paymentMeansDianCodes = null,
            DocumentType? documentType = null,
            Document? aiuSource = null)
        {
            var itemsList = items as IReadOnlyList<DocumentItem> ?? items.ToList();

            // Contrato AIU (operación 09): los datos del contrato viven en el documento; una nota
            // crédito/débito sobre una factura AIU los toma de la factura original (aiuSource).
            var aiu = AiuContractData.TryRead((aiuSource ?? document).SectorExtensionData);
            var isAiu = aiu != null && (documentType?.OperationType == "09" || aiuSource != null);

            municipalitiesByCode.TryGetValue(client.CityCode ?? string.Empty, out var issuerMuni);
            municipalitiesByCode.TryGetValue(customer.CityCode ?? string.Empty, out var customerMuni);

            var data = new UblInvoiceData
            {
                DocumentNumber = document.Number,
                Prefix = resolution.Prefix,
                IssueDate = document.IssueDate,
                IssueTime = document.IssueDate,
                TechnicalKey = resolution.TechnicalKey,
                SoftwareId = client.SoftwareId,
                SoftwarePin = client.SoftwarePin,
                // "Production" es el único estado de habilitación que autoriza a facturar en el
                // ambiente real de la DIAN; cualquier otro (incluida habilitación en curso) va a pruebas.
                Environment = client.DianHabilitationStatus == "Production" ? "1" : "2",
                // DianCode/OperationType reales salen de DocumentType (Code="FE-STD"→DianCode="01",
                // "NC"→"91", "ND"→"92", etc. — ya seedeados completos en FelDbContext). document.TypeCode
                // NO es el código DIAN: es la plantilla de impresión (FE-STD, FE-QR-ARRIBA...) o el tipo
                // de nota, nada más. Antes se mandaba "01" fijo aquí, así que una nota crédito/débito
                // salía literalmente como factura — confirmado contra un envío real que la DIAN aceptó
                // como factura en vez de nota. "01"/"10" quedan solo como último respaldo si el caller
                // no tiene el DocumentType a mano.
                DianCode = documentType?.DianCode ?? "01",
                OperationType = documentType?.OperationType ?? "10",
                ResolutionNumber = resolution.ResolutionNumber,
                ResolutionValidFrom = resolution.ValidFrom,
                ResolutionValidTo = resolution.ValidTo,
                ResolutionNumberStart = resolution.NumberStart,
                ResolutionNumberEnd = resolution.NumberEnd,
                Currency = "COP",
                Issuer = new IssuerData
                {
                    TaxId = client.TaxId,
                    IdentificationCode = "31",
                    Name = client.CompanyName,
                    DepartmentCode = issuerMuni?.DepartmentCode ?? DeriveDepartmentCode(client.CityCode),
                    DepartmentName = issuerMuni?.DepartmentName ?? string.Empty,
                    CityCode = client.CityCode ?? string.Empty,
                    CityName = issuerMuni?.Name ?? client.City,
                    Address = client.Address,
                    Email = client.Email
                },
                Customer = new CustomerData
                {
                    TaxId = customer.IdentificationNumber,
                    IdentificationCode = customer.IdentificationType,
                    Name = customer.Name,
                    DepartmentCode = customerMuni?.DepartmentCode ?? DeriveDepartmentCode(customer.CityCode),
                    DepartmentName = customerMuni?.DepartmentName ?? string.Empty,
                    CityCode = customer.CityCode ?? string.Empty,
                    CityName = customerMuni?.Name ?? customer.CityName,
                    Address = customer.Address,
                    Email = customer.Email
                }
            };

            // Grupo obligatorio (numeral 6.5.10, FAN01-05): antes no se llenaba en este mapper (el
            // que arma el XML de facturas reales ya guardadas), a diferencia de BuildInvoiceDataFromRequest
            // (B2B/set de pruebas), que sí lo poblaba desde el request. Sin esto la DIAN rechazaba con
            // "Regla: ZB01, Rechazo: Fallo en el esquema XML del archivo" por faltar el grupo completo.
            // El ID "1"/"2" es un código fijo del estándar UBL/DIAN (Contado/Crédito, siempre esos dos
            // valores), no un dato de catálogo — igual que DianCode/OperationType/IdentificationCode
            // más arriba en este mismo método.
            //
            // PaymentMeansCode: a crédito siempre "ZZZ" (fijo, ver comentario en la constante) — el
            // Medio de Pago capturado en el documento no aplica ahí. De contado, el código real de
            // la DIAN sale de paymentMeansDianCodes (catálogo Medio de Pago, columna DianCode),
            // nunca del "Category" interno (ese es el vocabulario propio de Dataico, no un código
            // DIAN válido — causaba el mismo ZB01 con un valor igual de inválido).
            var paymentMeansCode = document.PaymentMeansType == "CREDITO"
                ? CreditoPaymentMeansCode
                : (paymentMeansDianCodes != null && document.PaymentMeans != null && paymentMeansDianCodes.TryGetValue(document.PaymentMeans, out var dianCode)
                    ? dianCode
                    : document.PaymentMeans ?? string.Empty);

            data.PaymentMeans.Add(new PaymentMeansData
            {
                Id = document.PaymentMeansType == "CREDITO" ? "2" : "1",
                PaymentMeansCode = paymentMeansCode,
                PaymentDueDate = document.PaymentMeansType == "CREDITO" && document.PaymentTermDays.HasValue
                    ? document.IssueDate.AddDays(document.PaymentTermDays.Value)
                    : null
            });

            // Agrupa las líneas por tarifa de IVA (mismo criterio que el desglose del resumen de
            // factura en el portal) para armar el TaxTotal del documento. Las retenciones no se
            // incluyen todavía: BaseUblStrategy no tiene un bloque WithholdingTaxTotal, así que por
            // ahora solo viajan con Dataico.
            //
            // Antes esto solo corría para Gravado con tarifa > 0, así que Exento/Excluido (tarifa 0)
            // no generaban NINGÚN TaxSubtotal, ni a nivel de cabecera ni de línea. La DIAN rechazaba
            // con "Regla: FAS01b, ... Debe existir un TaxTotal a nivel de la cabecera por cada tipo
            // de impuesto que se informa a nivel de línea" y "Regla: FAU04, Base Imponible es
            // distinto a la suma de los valores de las bases imponibles de todas líneas de detalle"
            // — la base imponible (TaxableAmount) debe reportarse para toda línea, tenga o no IVA
            // efectivo. Ahora se agrupa siempre, y cada línea también lleva su propio TaxSubtotal
            // (ver BaseUblStrategy.BuildLine), con Percent=0 para Exento/Excluido.
            var taxGroups = new Dictionary<decimal, (decimal Base, decimal Amount)>();
            decimal lineExtension = 0;

            foreach (var item in itemsList)
            {
                var lineBase = item.Quantity * item.UnitPrice * (1 - item.DiscountRate / 100);
                lineExtension += lineBase;

                // AIU: Administración e Imprevistos no hacen parte de la base gravable (solo la
                // Utilidad causa IVA), y el Anexo dice que a esos ítems NO se les informa TaxTotal
                // de línea — tampoco entran en la base imponible del encabezado.
                var outsideTaxBase = isAiu && item.IvaTreatment != IvaTreatment.Gravado;

                var line = new InvoiceLine
                {
                    ItemCode = item.Code,
                    Description = item.Name,
                    Quantity = item.Quantity,
                    UnitCode = item.UnitOfMeasureCode,
                    UnitPrice = item.UnitPrice,
                    LineExtensionAmount = lineBase
                };
                // La línea de Administración lleva la nota obligatoria con el objeto del contrato.
                if (isAiu && item.Code == AiuContractData.AdminCode) line.Note = aiu!.BuildAdminNote();

                if (!outsideTaxBase)
                {
                    line.Taxes.Add(new TaxSubtotal { TaxId = "01", TaxableAmount = lineBase, TaxAmount = item.TaxAmount, Percent = item.TaxRate });
                    var prev = taxGroups.TryGetValue(item.TaxRate, out var v) ? v : (Base: 0m, Amount: 0m);
                    taxGroups[item.TaxRate] = (prev.Base + lineBase, prev.Amount + item.TaxAmount);
                }
                data.Lines.Add(line);
            }

            foreach (var (rate, (baseAmount, amount)) in taxGroups)
            {
                data.Taxes.Add(new TaxSubtotal { TaxId = "01", TaxableAmount = baseAmount, TaxAmount = amount, Percent = rate });
            }

            var totalTax = taxGroups.Values.Sum(v => v.Amount);
            data.LineExtensionAmount = lineExtension;
            // Base imponible total: en un AIU es solo la de la Utilidad (LineExtensionAmount incluye A e I).
            data.TaxExclusiveAmount = isAiu ? taxGroups.Values.Sum(v => v.Base) : lineExtension;
            data.TaxInclusiveAmount = lineExtension + totalTax;

            // Descuento y cargo general del documento (no afectan las bases gravables: el IVA se
            // calcula sobre las líneas, igual que en el portal y en el envío a Dataico). Van como
            // cac:AllowanceCharge a nivel de factura (FAQ01-09). Se informan por el valor completo:
            // BaseAmount = Amount con 100 % — mismo criterio del "charges" que se manda a Dataico.
            // Código de descuento "09" (Descuento general, tabla 13.3.9 de la Caja de Herramientas).
            var generalDiscount = document.GeneralDiscountAmount ?? 0;
            if (generalDiscount > 0)
            {
                data.AllowanceCharges.Add(new AllowanceChargeData
                {
                    ChargeIndicator = false,
                    ReasonCode = "09",
                    Reason = string.IsNullOrWhiteSpace(document.GeneralDiscountReason) ? "Descuento general" : document.GeneralDiscountReason,
                    Percentage = 100m,
                    BaseAmount = generalDiscount,
                    Amount = generalDiscount
                });
            }
            var generalCharge = document.GeneralChargeAmount ?? 0;
            if (generalCharge > 0)
            {
                data.AllowanceCharges.Add(new AllowanceChargeData
                {
                    ChargeIndicator = true,
                    Reason = string.IsNullOrWhiteSpace(document.GeneralChargeReason) ? "Cargo general" : document.GeneralChargeReason,
                    Percentage = 100m,
                    BaseAmount = generalCharge,
                    Amount = generalCharge
                });
            }

            // FAU14: valor de la factura = TaxInclusiveAmount - descuentos + cargos.
            data.PayableAmount = data.TaxInclusiveAmount - generalDiscount + generalCharge;

            return data;
        }

        // Nota Crédito/Débito reales del portal de cliente (a diferencia de BuildCreditNoteDataFromRequest,
        // que arma todo desde un CreditNoteRequest del API B2B) — parte de un Document/DocumentItem ya
        // persistidos más su documento original (ReferenceDocumentId ya resuelto por el caller, mismo
        // patrón que InvoiceController.Resend). Reusa BuildInvoiceData para todo el cuerpo común (líneas,
        // impuestos, terceros, medio de pago) y solo agrega lo propio de la nota: DiscrepancyResponse y
        // la referencia a la factura original.
        //
        // discrepancyResponseCode/discrepancyDescription salen tal cual de Document — el frontend ya
        // los captura de un catálogo real (TaxCatalogKind.CreditNoteReason/DebitNoteReason), no hace
        // falta resolverlos de nuevo acá.
        public static UblInvoiceData BuildCreditNoteData(
            Document document,
            Document originalDocument,
            Customer customer,
            IEnumerable<DocumentItem> items,
            Resolution resolution,
            Client client,
            IReadOnlyDictionary<string, DianMunicipality> municipalitiesByCode,
            DocumentType documentType,
            IReadOnlyDictionary<string, string>? paymentMeansDianCodes = null)
        {
            var data = BuildInvoiceData(document, customer, items, resolution, client, municipalitiesByCode, paymentMeansDianCodes, documentType, originalDocument);
            data.DiscrepancyResponseCode = document.DiscrepancyResponseCode ?? string.Empty;
            data.DiscrepancyDescription = document.ReferenceConcept;
            data.BillingReferenceCufe = originalDocument.Cufe ?? string.Empty;
            data.BillingReferenceDocumentNumber = originalDocument.Number;
            data.BillingReferenceDate = originalDocument.IssueDate;
            return data;
        }

        // Nota Débito real del portal de cliente — mismo mapeo que BuildCreditNoteData, DianCode/
        // OperationType distintos ya vienen del DocumentType ("ND"→"92"/"30").
        public static UblInvoiceData BuildDebitNoteData(
            Document document,
            Document originalDocument,
            Customer customer,
            IEnumerable<DocumentItem> items,
            Resolution resolution,
            Client client,
            IReadOnlyDictionary<string, DianMunicipality> municipalitiesByCode,
            DocumentType documentType,
            IReadOnlyDictionary<string, string>? paymentMeansDianCodes = null)
        {
            return BuildCreditNoteData(document, originalDocument, customer, items, resolution, client, municipalitiesByCode, documentType, paymentMeansDianCodes);
        }

        // Equivalente de BuildInvoiceData para el API B2B (Fel.Api.Integration): el caller externo ya
        // manda Customer/líneas/impuestos en el mismo formato que UblInvoiceData espera (por eso se
        // asignan directo, sin reconstruirlos), pero el Issuer y los datos de habilitación
        // (Resolución, SoftwareId/Pin, Ambiente) siempre salen del Client autenticado por HMAC —
        // nunca de lo que el caller declare, igual que en BuildInvoiceData.
        public static UblInvoiceData BuildInvoiceDataFromRequest(
            InvoiceRequest request,
            Client client,
            Resolution resolution,
            IReadOnlyDictionary<string, DianMunicipality> municipalitiesByCode)
        {
            municipalitiesByCode.TryGetValue(client.CityCode ?? string.Empty, out var issuerMuni);

            var lineExtension = request.Lines.Sum(l => l.LineExtensionAmount);
            var totalTax = request.Taxes.Sum(t => t.TaxAmount);

            return new UblInvoiceData
            {
                DocumentNumber = request.DocumentNumber,
                Prefix = request.Prefix,
                IssueDate = request.IssueDate,
                IssueTime = request.IssueDate,
                TechnicalKey = resolution.TechnicalKey,
                SoftwareId = client.SoftwareId,
                SoftwarePin = client.SoftwarePin,
                Environment = client.DianHabilitationStatus == "Production" ? "1" : "2",
                DianCode = "01",
                OperationType = "10",
                ResolutionNumber = resolution.ResolutionNumber,
                ResolutionValidFrom = resolution.ValidFrom,
                ResolutionValidTo = resolution.ValidTo,
                ResolutionNumberStart = resolution.NumberStart,
                ResolutionNumberEnd = resolution.NumberEnd,
                Currency = request.Currency,
                ExchangeRate = request.ExchangeRate,
                Issuer = new IssuerData
                {
                    TaxId = client.TaxId,
                    IdentificationCode = "31",
                    Name = client.CompanyName,
                    DepartmentCode = issuerMuni?.DepartmentCode ?? DeriveDepartmentCode(client.CityCode),
                    DepartmentName = issuerMuni?.DepartmentName ?? string.Empty,
                    CityCode = client.CityCode ?? string.Empty,
                    CityName = issuerMuni?.Name ?? client.City,
                    Address = client.Address,
                    Email = client.Email
                },
                Customer = request.Customer,
                PaymentMeans = request.PaymentMeans,
                AllowanceCharges = request.AllowanceCharges,
                Taxes = request.Taxes,
                Lines = request.Lines,
                LineExtensionAmount = lineExtension,
                TaxExclusiveAmount = lineExtension,
                TaxInclusiveAmount = lineExtension + totalTax,
                PayableAmount = request.TotalAmount > 0 ? request.TotalAmount : lineExtension + totalTax
            };
        }

        // DianCode "91" = Nota Crédito (ver DocumentTypes seed en FelDbContext). CreditNoteRequest
        // hereda de InvoiceRequest, así que se reusa el mismo mapeo base y solo se suman los campos
        // propios de la nota — referenciada (CUFE + número + fecha de la factura original) o no.
        public static UblInvoiceData BuildCreditNoteDataFromRequest(
            CreditNoteRequest request,
            Client client,
            Resolution resolution,
            IReadOnlyDictionary<string, DianMunicipality> municipalitiesByCode)
        {
            var data = BuildInvoiceDataFromRequest(request, client, resolution, municipalitiesByCode);
            data.DianCode = "91";
            data.DiscrepancyResponseCode = request.DiscrepancyResponseCode;
            data.DiscrepancyDescription = request.DiscrepancyDescription;

            if (!string.IsNullOrWhiteSpace(request.BillingReferenceCufe))
            {
                data.BillingReferenceCufe = request.BillingReferenceCufe;
                data.BillingReferenceDocumentNumber = request.BillingReferenceDocumentNumber;
                data.BillingReferenceDate = request.BillingReferenceDate;
            }

            return data;
        }

        // DianCode "92" = Nota Débito. DebitNoteRequest hereda de CreditNoteRequest sin campos
        // propios, así que el mapeo es igual salvo el DianCode.
        public static UblInvoiceData BuildDebitNoteDataFromRequest(
            DebitNoteRequest request,
            Client client,
            Resolution resolution,
            IReadOnlyDictionary<string, DianMunicipality> municipalitiesByCode)
        {
            var data = BuildInvoiceDataFromRequest(request, client, resolution, municipalitiesByCode);
            data.DianCode = "92";
            data.DiscrepancyResponseCode = request.DiscrepancyResponseCode;
            data.DiscrepancyDescription = request.DiscrepancyDescription;

            if (!string.IsNullOrWhiteSpace(request.BillingReferenceCufe))
            {
                data.BillingReferenceCufe = request.BillingReferenceCufe;
                data.BillingReferenceDocumentNumber = request.BillingReferenceDocumentNumber;
                data.BillingReferenceDate = request.BillingReferenceDate;
            }

            return data;
        }

        // Documento Equivalente Electrónico — cualquiera de sus subtipos (ver InvoiceUblStrategy.
        // ResolveProfileId para el literal exacto que exige cada uno). Solo POS/SPD (dianCode="20")
        // usa el par 601/602 (normal/en sitio); el resto no distingue modo de operación.
        public static UblInvoiceData BuildEquivalentDocumentDataFromRequest(
            InvoiceRequest request,
            Client client,
            Resolution resolution,
            IReadOnlyDictionary<string, DianMunicipality> municipalitiesByCode,
            string dianCode,
            bool onSite = false)
        {
            var data = BuildInvoiceDataFromRequest(request, client, resolution, municipalitiesByCode);
            data.DianCode = dianCode;
            data.OperationType = dianCode == "20" ? (onSite ? "602" : "601") : "10";
            return data;
        }

        // Documento Soporte (DianCode 05): roles invertidos respecto a los demás mappers de esta
        // clase — el Client autenticado es el ADQUIRENTE (Customer/ABS), no el emisor. El vendedor
        // no obligado (Issuer/SNO) llega completo en el request porque no vive en nuestra base.
        public static UblInvoiceData BuildSupportDocumentDataFromRequest(
            SupportDocumentRequest request,
            Client client,
            Resolution resolution,
            IReadOnlyDictionary<string, DianMunicipality> municipalitiesByCode)
        {
            municipalitiesByCode.TryGetValue(client.CityCode ?? string.Empty, out var clientMuni);

            var lineExtension = request.Lines.Sum(l => l.LineExtensionAmount);
            var totalTax = request.Taxes.Sum(t => t.TaxAmount);

            return new UblInvoiceData
            {
                DocumentNumber = request.DocumentNumber,
                Prefix = request.Prefix,
                IssueDate = request.IssueDate,
                IssueTime = request.IssueDate,
                TechnicalKey = resolution.TechnicalKey,
                SoftwareId = client.SoftwareId,
                SoftwarePin = client.SoftwarePin,
                Environment = client.DianHabilitationStatus == "Production" ? "1" : "2",
                DianCode = "05",
                OperationType = request.SellerIsNonResident ? "11" : "10",
                ResolutionNumber = resolution.ResolutionNumber,
                ResolutionValidFrom = resolution.ValidFrom,
                ResolutionValidTo = resolution.ValidTo,
                ResolutionNumberStart = resolution.NumberStart,
                ResolutionNumberEnd = resolution.NumberEnd,
                Currency = request.Currency,
                Issuer = request.Seller, // Vendedor No Obligado (SNO) — declarado por el caller
                Customer = new CustomerData
                {
                    TaxId = client.TaxId,
                    IdentificationCode = "31",
                    Name = client.CompanyName,
                    DepartmentCode = clientMuni?.DepartmentCode ?? DeriveDepartmentCode(client.CityCode),
                    DepartmentName = clientMuni?.DepartmentName ?? string.Empty,
                    CityCode = client.CityCode ?? string.Empty,
                    CityName = clientMuni?.Name ?? client.City,
                    Address = client.Address,
                    Email = client.Email
                },
                PaymentMeans = request.PaymentMeans,
                AllowanceCharges = request.AllowanceCharges,
                Taxes = request.Taxes,
                Lines = request.Lines,
                LineExtensionAmount = lineExtension,
                TaxExclusiveAmount = lineExtension,
                TaxInclusiveAmount = lineExtension + totalTax,
                PayableAmount = lineExtension + totalTax
            };
        }

        public static UblInvoiceData BuildSupportDocumentAdjustmentDataFromRequest(
            SupportDocumentAdjustmentRequest request,
            Client client,
            Resolution resolution,
            IReadOnlyDictionary<string, DianMunicipality> municipalitiesByCode)
        {
            var data = BuildSupportDocumentDataFromRequest(request, client, resolution, municipalitiesByCode);
            data.DianCode = "95";
            data.DiscrepancyResponseCode = request.DiscrepancyResponseCode;
            data.DiscrepancyDescription = request.DiscrepancyDescription;

            if (!string.IsNullOrWhiteSpace(request.BillingReferenceCuds))
            {
                data.BillingReferenceCufe = request.BillingReferenceCuds;
                data.BillingReferenceDocumentNumber = request.BillingReferenceDocumentNumber;
                data.BillingReferenceDate = request.BillingReferenceDate;
            }

            return data;
        }

        // Factura de Transporte de Carga: reusa BuildInvoiceDataFromRequest tal cual (mismo
        // InvoiceTypeCode "01", mismo CUFE) — solo cambia el OperationType/CustomizationID a "12"
        // ("operaciones efectuadas por el sector transporte de carga", Guía Mintransporte v10, ver
        // docs/dian-transporte/). Los datos de remesa RNDC ya van en cada línea (InvoiceLine.Rndc*),
        // puestos ahí directamente por el caller.
        public static UblInvoiceData BuildTransportInvoiceDataFromRequest(
            TransportInvoiceRequest request,
            Client client,
            Resolution resolution,
            IReadOnlyDictionary<string, DianMunicipality> municipalitiesByCode)
        {
            var data = BuildInvoiceDataFromRequest(request, client, resolution, municipalitiesByCode);
            data.OperationType = "12";
            return data;
        }

        // Nuestro CityCode es el código DANE completo (2 de depto + 3 de municipio); si el
        // municipio todavía no está en el catálogo, al menos el departamento se puede derivar sin
        // necesitar el catálogo cargado.
        internal static string DeriveDepartmentCode(string? cityCode) =>
            !string.IsNullOrWhiteSpace(cityCode) && cityCode.Length >= 2 ? cityCode.Substring(0, 2) : string.Empty;
    }
}
