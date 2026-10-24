using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Fel.Core.Models;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Dian;

namespace Fel.Infrastructure.Ubl.Strategies
{
    public abstract class BaseUblStrategy
    {
        protected readonly ICryptoService _cryptoService;

        protected static readonly XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
        protected static readonly XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
        protected static readonly XNamespace ext = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";
        // Namespace real confirmado contra el targetNamespace del XSD oficial (DIAN_UBL_Structures.xsd)
        // y contra el ejemplo real de la DIAN (Generica.xml).
        protected static readonly XNamespace sts = "dian:gov:co:facturaelectronica:Structures-2-1";
        protected static readonly XNamespace xades = "http://uri.etsi.org/01903/v1.3.2#";
        protected static readonly XNamespace xades141 = "http://uri.etsi.org/01903/v1.4.1#";
        protected static readonly XNamespace ds = "http://www.w3.org/2000/09/xmldsig#";

        // Cantidad y precio unitario de la línea: la DIAN admite hasta 6 decimales y el valor de la línea (LineExtensionAmount) sale de
        // cantidad x precio sin redondear antes. Con 2 decimales fijos, un precio de 1.234,5678 viajaba como 1.234,57 y la línea no cuadraba.
        // Mínimo 2 decimales para que lo que ya salía con 2 siga igual.
        internal static string QuantityOrPrice(decimal value) => value.ToString("0.00####", System.Globalization.CultureInfo.InvariantCulture);

        public const int DianItemCodeMaxLength = 50;

        public static string DianItemCode(string? code)
        {
            var value = code ?? string.Empty;
            return value.Length <= DianItemCodeMaxLength ? value : value[..DianItemCodeMaxLength].TrimEnd();
        }

        public BaseUblStrategy(ICryptoService cryptoService)
        {
            _cryptoService = cryptoService;
        }

        public abstract XElement GenerateXml(UblInvoiceData data, string cufe);

        protected XElement BuildExtensions(UblInvoiceData data, string cufe)
        {
            // NIT + DV de la propia DIAN, fijos según el anexo técnico (numeral 6.5.10 / FAB31-35):
            // 800197268, DV 4, tipo de documento 31 (NIT), agencia 195 "CO, DIAN...".
            var qrBaseUrl = data.Environment == "1"
                ? "https://catalogo-vpfe.dian.gov.co/document/searchqr?documentkey="
                : "https://catalogo-vpfe-hab.dian.gov.co/document/searchqr?documentkey=";
            var issuerDv = NitValidation.CalculateCheckDigit(data.Issuer.TaxId).ToString();

            // sts:InvoiceControl (autorización/rango de numeración) es exclusivo de Factura — Nota
            // Crédito/Débito NO lo llevan (confirmado contra el ejemplo oficial CreditNote.xml de la
            // Caja de Herramientas: no tiene ese grupo en absoluto, salta directo de InvoiceSource a
            // SoftwareProvider) porque la DIAN no autoriza una resolución propia para notas — solo
            // usan el Prefijo de la factura del emisor (ver CorporateRegistrationScheme más abajo).
            var esNotaCreditoODebito = data.DianCode is "91" or "92";

            var dianExtensionsChildren = new List<XElement>();
            if (!esNotaCreditoODebito)
            {
                dianExtensionsChildren.Add(
                    new XElement(sts + "InvoiceControl",
                        new XElement(sts + "InvoiceAuthorization", data.ResolutionNumber),
                        new XElement(sts + "AuthorizationPeriod",
                            new XElement(cbc + "StartDate", data.ResolutionValidFrom.ToString("yyyy-MM-dd")),
                            new XElement(cbc + "EndDate", data.ResolutionValidTo.ToString("yyyy-MM-dd"))
                        ),
                        new XElement(sts + "AuthorizedInvoices",
                            new XElement(sts + "Prefix", data.Prefix),
                            new XElement(sts + "From", data.ResolutionNumberStart.ToString()),
                            new XElement(sts + "To", data.ResolutionNumberEnd.ToString())
                        )
                    ));
            }

            var extensions = new XElement(ext + "UBLExtensions",
                new XElement(ext + "UBLExtension",
                    new XElement(ext + "ExtensionContent",
                        new XElement(sts + "DianExtensions",
                            dianExtensionsChildren.ToArray(),
                            // País de origen del documento (numeral 6.5.10) — confirmado contra una
                            // factura real aceptada por la DIAN (Comcel/Claro) y contra la propia
                            // ApplicationResponse que la DIAN nos devuelve: faltaba por completo, lo
                            // que explica las notificaciones FAB14/15/16/17 ("No informado el literal
                            // CO/6/urn:oasis:.../United Nations...") en cada envío de prueba.
                            new XElement(sts + "InvoiceSource",
                                new XElement(cbc + "IdentificationCode",
                                    new XAttribute("listAgencyID", "6"),
                                    new XAttribute("listAgencyName", "United Nations Economic Commission for Europe"),
                                    new XAttribute("listSchemeURI", "urn:oasis:names:specification:ubl:codelist:gc:CountryIdentificationCode-2.1"),
                                    "CO")
                            ),
                            // Cada cliente hace su propia habilitación ante la DIAN como dueño de su
                            // software (modelo autogestionado); FacilFactura solo opera la integración
                            // a su nombre, por eso el ProviderID es el NIT del emisor, no el nuestro.
                            new XElement(sts + "SoftwareProvider",
                                // El schemeID es el DV del NIT DEL EMISOR (antes iba fijo en "4", que
                                // es el DV de la propia DIAN — se confundió con AuthorizationProviderID
                                // más abajo). La DIAN rechazaba con "DV del NIT del Prestador de
                                // Servicios no está correctamente calculado" (FAB22b).
                                // schemeAgencyID/schemeAgencyName (confirmados contra la factura real
                                // de Comcel Y contra un firmador Python de referencia, en producción
                                // activa) faltaban aquí por completo.
                                new XElement(sts + "ProviderID",
                                    new XAttribute("schemeID", issuerDv), new XAttribute("schemeName", "31"),
                                    new XAttribute("schemeAgencyID", "195"), new XAttribute("schemeAgencyName", "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)"),
                                    data.Issuer.TaxId),
                                new XElement(sts + "SoftwareID", new XAttribute("schemeAgencyID", "195"), new XAttribute("schemeAgencyName", "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)"), data.SoftwareId)
                            ),
                            // SHA-384(SoftwareID + PIN + NumFac) — Anexo Técnico numeral 11.3. Antes le
                            // faltaba el número de factura (NumFac), lo que producía un código inválido
                            // para todo documento.
                            new XElement(sts + "SoftwareSecurityCode", new XAttribute("schemeAgencyID", "195"), new XAttribute("schemeAgencyName", "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)"),
                                _cryptoService.GenerateCufeSha384($"{data.SoftwareId}{data.SoftwarePin}{data.Prefix}{data.DocumentNumber}")),
                            // Grupo obligatorio del Proveedor Autorizado — la propia DIAN, NIT fijo
                            // 800197268 (numeral 6.5.10, FAB30-35). Faltaba por completo: la DIAN
                            // rechazaba con "AuthorizationProviderID no corresponde al NIT de la DIAN".
                            new XElement(sts + "AuthorizationProvider",
                                new XElement(sts + "AuthorizationProviderID",
                                    new XAttribute("schemeAgencyID", "195"), new XAttribute("schemeAgencyName", "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)"),
                                    new XAttribute("schemeID", "4"), new XAttribute("schemeName", "31"),
                                    "800197268")
                            ),
                            // URL de consulta del documento (numeral 11.7.1) — antes faltaba por
                            // completo ("No se encuentra informado el QR Code").
                            new XElement(sts + "QRCode", qrBaseUrl + cufe)
                        )
                    )
                ),
                new XElement(ext + "UBLExtension",
                    new XElement(ext + "ExtensionContent",
                        new XElement("SignaturePlaceholder")
                    )
                )
            );
            return extensions;
        }

        protected XElement BuildAccountingSupplierParty(UblInvoiceData data)
        {
            var issuerDv = NitValidation.CalculateCheckDigit(data.Issuer.TaxId).ToString();
            return new XElement(cac + "AccountingSupplierParty",
                new XElement(cbc + "AdditionalAccountID", "1"),
                new XElement(cac + "Party",
                    new XElement(cac + "PartyName", new XElement(cbc + "Name", data.Issuer.Name)),
                    new XElement(cac + "PhysicalLocation",
                        new XElement(cac + "Address",
                            new XElement(cbc + "ID", data.Issuer.CityCode),
                            new XElement(cbc + "CityName", data.Issuer.CityName),
                            new XElement(cbc + "CountrySubentity", data.Issuer.DepartmentName),
                            new XElement(cbc + "CountrySubentityCode", data.Issuer.DepartmentCode),
                            new XElement(cac + "AddressLine", new XElement(cbc + "Line", data.Issuer.Address)),
                            // languageID="es" en Name (numeral 6.5.10) — confirmado contra la factura
                            // real de Comcel; faltaba, explicaba la notificación FAJ18 ("Debe
                            // contener el literal 'es'") en cada envío.
                            new XElement(cac + "Country", new XElement(cbc + "IdentificationCode", "CO"), new XElement(cbc + "Name", new XAttribute("languageID", "es"), "Colombia"))
                        )
                    ),
                    // Orden exigido por el schema UBL (cac:PartyType): PartyTaxScheme va ANTES que
                    // PartyLegalEntity — al revés truena con "cvc-complex-type.2.4.a: Invalid content
                    // was found starting with element PartyTaxScheme".
                    new XElement(cac + "PartyTaxScheme",
                        new XElement(cbc + "RegistrationName", data.Issuer.Name),
                        // El schemeID es el DV del NIT (antes iba fijo en "1", la DIAN lo rechazaba).
                        // schemeAgencyID/schemeAgencyName (confirmados contra la factura real de
                        // Comcel) faltaban aquí — explicaban las notificaciones FAJ22/FAJ23
                        // ("No informado el literal 195/CO, DIAN...").
                        new XElement(cbc + "CompanyID",
                            new XAttribute("schemeID", issuerDv), new XAttribute("schemeName", "31"),
                            new XAttribute("schemeAgencyID", "195"), new XAttribute("schemeAgencyName", "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)"),
                            data.Issuer.TaxId),
                        // listName="04" (confirmado contra el ejemplo oficial Generica.xml de la
                        // propia Caja de Herramientas de la DIAN, y contra un firmador Python de
                        // referencia en producción activa — la factura de Comcel trae "48", un
                        // valor no estándar de ese proveedor específico) faltaba por completo.
                        new XElement(cbc + "TaxLevelCode", new XAttribute("listName", "04"), string.Join(";", data.Issuer.TaxLevelCodes)),
                        new XElement(cac + "TaxScheme", new XElement(cbc + "ID", data.Issuer.TaxSchemeId), new XElement(cbc + "Name", "IVA"))
                    ),
                    // Grupo obligatorio (numeral 6.5.10, FAJ42-48) — antes faltaba por completo, la
                    // DIAN rechazaba con "No se encuentra el grupo PartyLegalEntity del emisor".
                    new XElement(cac + "PartyLegalEntity",
                        new XElement(cbc + "RegistrationName", data.Issuer.Name),
                        new XElement(cbc + "CompanyID",
                            new XAttribute("schemeAgencyID", "195"), new XAttribute("schemeAgencyName", "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)"),
                            new XAttribute("schemeID", issuerDv), new XAttribute("schemeName", "31"),
                            data.Issuer.TaxId),
                        // El código de la "sucursal" (CorporateRegistrationScheme/ID) debe ser igual
                        // al prefijo de la resolución (numeral 6.5.10, FAB10a) — antes faltaba, la
                        // DIAN rechazaba con "El prefijo de numeración no es igual al código de la
                        // sucursal correspondiente a este punto de facturación".
                        // cbc:Name (confirmado contra el ejemplo oficial Generica.xml y contra un
                        // firmador Python de referencia en producción activa) también faltaba — es
                        // el código de matrícula mercantil del establecimiento; no tenemos ese dato
                        // real para clientes, se deja un valor de relleno.
                        new XElement(cac + "CorporateRegistrationScheme",
                            new XElement(cbc + "ID", data.Prefix),
                            new XElement(cbc + "Name", "0000000")
                        )
                    ),
                    // Correo de recepción de documentos electrónicos (numeral 6.5.10, FAJ71) —
                    // faltaba por completo, la DIAN rechazaba con "No corresponde al correo
                    // electrónico para la recepción de documentos e instrumentos electrónicos no
                    // informado". Confirmado contra la factura real de Comcel: va al final de
                    // cac:Party, después de PartyLegalEntity.
                    new XElement(cac + "Contact", new XElement(cbc + "ElectronicMail", data.Issuer.Email))
                )
            );
        }

        // Adquirente. El tipo de documento (schemeName) sale del documento del tercero (13 cédula, 31 NIT, 42 extranjero, 50 NIT de otro país...)
        // y no fijo; con NIT lleva el dígito de verificación como schemeID (reglas HD04b/HD05 del schematron). El tipo de organización
        // (AdditionalAccountID) es 1 para personas jurídicas (NIT y NIT de otro país) y 2 para las naturales. La dirección usa los códigos
        // DANE solo en Colombia; en otro país van la ciudad y el estado como texto, con el país del tercero.
        protected XElement BuildAccountingCustomerParty(UblInvoiceData data)
        {
            var customer = data.Customer;
            var identificationCode = string.IsNullOrWhiteSpace(customer.IdentificationCode) ? "13" : customer.IdentificationCode;
            var isNit = identificationCode == "31";
            var isForeign = !string.IsNullOrWhiteSpace(customer.CountryCode) && customer.CountryCode != "CO";
            var organizationType = identificationCode is "31" or "50" ? "1" : "2";

            XElement CustomerId(XName element, bool withTaxSchemeId)
            {
                var id = new XElement(element,
                    new XAttribute("schemeAgencyID", "195"),
                    new XAttribute("schemeAgencyName", "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)"));
                if (isNit) id.Add(new XAttribute("schemeID", NitValidation.CalculateCheckDigit(customer.TaxId)));
                else if (withTaxSchemeId) id.Add(new XAttribute("schemeID", "1"));
                id.Add(new XAttribute("schemeName", identificationCode), customer.TaxId);
                return id;
            }

            var address = new XElement(cac + "Address");
            if (!isForeign) address.Add(new XElement(cbc + "ID", customer.CityCode));
            address.Add(
                new XElement(cbc + "CityName", customer.CityName),
                new XElement(cbc + "CountrySubentity", customer.DepartmentName));
            if (!isForeign) address.Add(new XElement(cbc + "CountrySubentityCode", customer.DepartmentCode));
            address.Add(
                new XElement(cac + "AddressLine", new XElement(cbc + "Line", customer.Address)),
                new XElement(cac + "Country",
                    new XElement(cbc + "IdentificationCode", isForeign ? customer.CountryCode : "CO"),
                    new XElement(cbc + "Name", new XAttribute("languageID", "es"), isForeign ? customer.CountryName : "Colombia")));

            return new XElement(cac + "AccountingCustomerParty",
                new XElement(cbc + "AdditionalAccountID", organizationType),
                new XElement(cac + "Party",
                    new XElement(cac + "PartyIdentification", CustomerId(cbc + "ID", false)),
                    new XElement(cac + "PartyName", new XElement(cbc + "Name", customer.Name)),
                    new XElement(cac + "PhysicalLocation", address),
                    new XElement(cac + "PartyTaxScheme",
                        new XElement(cbc + "RegistrationName", customer.Name),
                        CustomerId(cbc + "CompanyID", true),
                        new XElement(cbc + "TaxLevelCode", new XAttribute("listName", "04"), string.Join(";", customer.TaxLevelCodes)),
                        new XElement(cac + "TaxScheme", new XElement(cbc + "ID", customer.TaxSchemeId), new XElement(cbc + "Name", "No aplica"))
                    ),
                    // PartyLegalEntity (confirmado contra el ejemplo oficial Generica.xml): a diferencia del emisor, no lleva
                    // CorporateRegistrationScheme.
                    new XElement(cac + "PartyLegalEntity",
                        new XElement(cbc + "RegistrationName", customer.Name),
                        CustomerId(cbc + "CompanyID", true)
                    ),
                    // Igual que en el emisor (FAJ71): el adquirente también lleva su Contact/ElectronicMail al final de Party.
                    new XElement(cac + "Contact", new XElement(cbc + "ElectronicMail", customer.Email))
                )
            );
        }

        // Grupo obligatorio (numeral 6.5.10, FAN01-05) — antes faltaba por completo, la DIAN
        // rechazaba con "Rechazo si grupo no informado".
        protected IEnumerable<XElement> BuildPaymentMeans(UblInvoiceData data)
        {
            foreach (var pm in data.PaymentMeans)
            {
                var el = new XElement(cac + "PaymentMeans",
                    new XElement(cbc + "ID", pm.Id),
                    new XElement(cbc + "PaymentMeansCode", pm.PaymentMeansCode)
                );
                // FAN04: obligatorio si PaymentMeans/ID = "2" (venta a crédito).
                if (pm.Id == "2" && pm.PaymentDueDate.HasValue)
                {
                    el.Add(new XElement(cbc + "PaymentDueDate", pm.PaymentDueDate.Value.ToString("yyyy-MM-dd")));
                }
                yield return el;
            }
        }

        // Un cac:TaxTotal por cada tipo de impuesto (IVA, INC...), con la suma de sus TaxSubtotal (una por tarifa): así lo piden la regla FAS01b y los
        // XML de ejemplo de la DIAN (en cada uno el IVA y el INC van en TaxTotal distintos). Sirve para el encabezado y para cada línea.
        protected IEnumerable<XElement> BuildTaxTotals(UblInvoiceData data) => BuildTaxTotals(data.Taxes, data.Currency);

        protected IEnumerable<XElement> BuildTaxTotals(IEnumerable<TaxSubtotal> taxes, string currency)
        {
            foreach (var scheme in taxes.GroupBy(t => t.TaxId))
            {
                var taxTotal = new XElement(cac + "TaxTotal",
                    new XElement(cbc + "TaxAmount", new XAttribute("currencyID", currency), scheme.Sum(t => t.TaxAmount).ToString("0.00").Replace(",", "."))
                );
                foreach (var tax in scheme)
                {
                    taxTotal.Add(new XElement(cac + "TaxSubtotal",
                        new XElement(cbc + "TaxableAmount", new XAttribute("currencyID", currency), tax.TaxableAmount.ToString("0.00").Replace(",", ".")),
                        new XElement(cbc + "TaxAmount", new XAttribute("currencyID", currency), tax.TaxAmount.ToString("0.00").Replace(",", ".")),
                        new XElement(cac + "TaxCategory",
                            new XElement(cbc + "Percent", tax.Percent.ToString("0.00").Replace(",", ".")),
                            new XElement(cac + "TaxScheme", new XElement(cbc + "ID", tax.TaxId), new XElement(cbc + "Name", tax.TaxName))
                        )
                    ));
                }
                yield return taxTotal;
            }
        }

        // Descuentos y cargos a nivel de factura (FAQ01-FAQ09): los que NO afectan las bases gravables
        // (los que sí la afectan van por ítem). Solo las usan Factura y Notas Crédito/Débito; el
        // resto de documentos que comparten BuildLegalMonetaryTotal nunca los han emitido.
        // ID consecutivo desde 1; AllowanceChargeReasonCode solo aplica (y es obligatorio) para
        // descuentos (FAQ04); el Amount no puede superar el BaseAmount.
        protected IEnumerable<XElement> BuildAllowanceCharges(UblInvoiceData data)
        {
            var index = 1;
            foreach (var ac in data.AllowanceCharges)
            {
                var el = new XElement(cac + "AllowanceCharge",
                    new XElement(cbc + "ID", index++.ToString()),
                    new XElement(cbc + "ChargeIndicator", ac.ChargeIndicator ? "true" : "false")
                );
                if (!ac.ChargeIndicator)
                {
                    el.Add(new XElement(cbc + "AllowanceChargeReasonCode", ac.ReasonCode));
                }
                el.Add(
                    new XElement(cbc + "AllowanceChargeReason", ac.Reason),
                    new XElement(cbc + "MultiplierFactorNumeric", ac.Percentage.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)),
                    new XElement(cbc + "Amount", new XAttribute("currencyID", data.Currency), ac.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)),
                    new XElement(cbc + "BaseAmount", new XAttribute("currencyID", data.Currency), ac.BaseAmount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
                );
                yield return el;
            }
        }

        // includeAllowances=true agrega AllowanceTotalAmount/ChargeTotalAmount (FAU09/FAU10: deben
        // ser la suma de los AllowanceCharge de la factura, así que solo se piden junto con
        // BuildAllowanceCharges). PayableAmount debe ser TaxInclusiveAmount - descuentos + cargos (FAU14).
        protected XElement BuildLegalMonetaryTotal(UblInvoiceData data, bool includeAllowances = false)
        {
            var total = new XElement(cac + "LegalMonetaryTotal",
                new XElement(cbc + "LineExtensionAmount", new XAttribute("currencyID", data.Currency), data.LineExtensionAmount.ToString("0.00").Replace(",", ".")),
                new XElement(cbc + "TaxExclusiveAmount", new XAttribute("currencyID", data.Currency), data.TaxExclusiveAmount.ToString("0.00").Replace(",", ".")),
                new XElement(cbc + "TaxInclusiveAmount", new XAttribute("currencyID", data.Currency), data.TaxInclusiveAmount.ToString("0.00").Replace(",", "."))
            );
            if (includeAllowances)
            {
                var allowances = data.AllowanceCharges.Where(a => !a.ChargeIndicator).Sum(a => a.Amount);
                var charges = data.AllowanceCharges.Where(a => a.ChargeIndicator).Sum(a => a.Amount);
                if (allowances > 0)
                    total.Add(new XElement(cbc + "AllowanceTotalAmount", new XAttribute("currencyID", data.Currency), allowances.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)));
                if (charges > 0)
                    total.Add(new XElement(cbc + "ChargeTotalAmount", new XAttribute("currencyID", data.Currency), charges.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)));
            }
            total.Add(new XElement(cbc + "PayableAmount", new XAttribute("currencyID", data.Currency), data.PayableAmount.ToString("0.00").Replace(",", ".")));
            return total;
        }

        // Condición de entrega de una factura de exportación (cac:DeliveryTerms): código Incoterm y su significado. Va antes de PaymentMeans
        // (secuencia UBL; igual que el ejemplo oficial Exportacion.xml). Solo se emite si el documento trae la condición.
        protected static IEnumerable<XElement> BuildDeliveryTerms(UblInvoiceData data)
        {
            if (string.IsNullOrWhiteSpace(data.DeliveryTermsCode)) yield break;

            var terms = new XElement(cac + "DeliveryTerms", new XElement(cbc + "LossRiskResponsibilityCode", data.DeliveryTermsCode));
            if (!string.IsNullOrWhiteSpace(data.DeliveryTermsName)) terms.Add(new XElement(cbc + "LossRisk", data.DeliveryTermsName));
            yield return terms;
        }

        // Tasa de cambio del documento en moneda extranjera (reglas M01, M03 y M05 de la DIAN): la moneda origen es la del documento, la
        // destino es COP y la TRM es obligatoria; solo se emite cuando el documento no está en COP. Va después de los descuentos/cargos
        // y antes de los totales de impuestos (secuencia UBL; igual que el ejemplo oficial Exportacion.xml).
        protected static IEnumerable<XElement> BuildPaymentExchangeRate(UblInvoiceData data)
        {
            if (data.Currency == "COP" || data.ExchangeRate == null || data.ExchangeRate.CalculationRate <= 0) yield break;

            var rate = data.ExchangeRate;
            yield return new XElement(cac + "PaymentExchangeRate",
                new XElement(cbc + "SourceCurrencyCode", data.Currency),
                new XElement(cbc + "SourceCurrencyBaseRate", "1.00"),
                new XElement(cbc + "TargetCurrencyCode", "COP"),
                new XElement(cbc + "TargetCurrencyBaseRate", "1.00"),
                new XElement(cbc + "CalculationRate", rate.CalculationRate.ToString("0.00####", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement(cbc + "Date", rate.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)));
        }

        // Observaciones del documento: un cbc:Note por cada una, en su orden. Va justo después del tipo de documento y antes
        // de DocumentCurrencyCode (secuencia UBL; igual que en el ejemplo oficial Mandatos.xml de la Caja de Herramientas).
        protected static IEnumerable<XElement> BuildNotes(UblInvoiceData data) =>
            data.Notes.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => new XElement(cbc + "Note", n));

        // Mandante de la línea (factura de mandato): cac:Item/cac:InformationContentProviderParty/cac:PowerOfAttorney/
        // cac:AgentParty/cac:PartyIdentification/cbc:ID, como en el ejemplo oficial Mandatos.xml. Con NIT (schemeName 31) el
        // schemeID es el dígito de verificación (reglas HD04b/HD05 del schematron); con otros documentos va sin schemeID.
        private static XElement? BuildMandante(InvoiceLine line)
        {
            if (string.IsNullOrWhiteSpace(line.MandanteIdentification) || string.IsNullOrWhiteSpace(line.MandanteIdentificationCode)) return null;

            var id = new XElement(cbc + "ID", new XAttribute("schemeAgencyID", "195"));
            if (line.MandanteIdentificationCode == "31")
                id.Add(new XAttribute("schemeID", NitValidation.CalculateCheckDigit(line.MandanteIdentification)));
            id.Add(new XAttribute("schemeName", line.MandanteIdentificationCode), line.MandanteIdentification);

            return new XElement(cac + "InformationContentProviderParty",
                new XElement(cac + "PowerOfAttorney",
                    new XElement(cac + "AgentParty",
                        new XElement(cac + "PartyIdentification", id))));
        }

        // "index" es el consecutivo real de la línea (1, 2, 3...) — antes quedaba fijo en "1" para
        // todas, lo que hace inválida ante la DIAN cualquier factura con más de una línea (IDs
        // duplicados).
        protected XElement BuildLine(string lineElementName, string quantityElementName, InvoiceLine line, string currency, int index)
        {
            var idElement = line.IsRndcRemittance.HasValue
                ? new XElement(cbc + "ID", new XAttribute("schemeID", line.IsRndcRemittance.Value ? "1" : "0"), index.ToString("00"))
                : new XElement(cbc + "ID", index.ToString());

            var itemElement = new XElement(cac + "Item",
                new XElement(cbc + "Description", line.Description),
                // Confirmado contra el ejemplo oficial Generica.xml y contra un firmador Python de
                // referencia en producción activa — presente en cada línea de ambos, antes de
                // StandardItemIdentification. Faltaba por completo. No tenemos un código de
                // vendedor distinto del código de ítem, se reusa el mismo valor.
                // El código del producto admite 100 caracteres en el sistema, pero el Anexo Técnico lo limita a 50 en el XML: a la DIAN van los primeros 50.
                new XElement(cac + "SellersItemIdentification", new XElement(cbc + "ID", DianItemCode(line.ItemCode))),
                new XElement(cac + "StandardItemIdentification", new XElement(cbc + "ID", new XAttribute("schemeID", "999"), DianItemCode(line.ItemCode)))
            );

            // Factura de exportación (opcionales): marca y modelo van justo después de la descripción (secuencia UBL, como en el ejemplo oficial
            // Exportacion.xml); la subpartida arancelaria reemplaza al estándar del ítem con schemeID 020 "Partida Arancelarias" (tabla 13.3.5).
            var descriptionElement = itemElement.Element(cbc + "Description")!;
            if (!string.IsNullOrWhiteSpace(line.ModelName)) descriptionElement.AddAfterSelf(new XElement(cbc + "ModelName", line.ModelName));
            if (!string.IsNullOrWhiteSpace(line.BrandName)) descriptionElement.AddAfterSelf(new XElement(cbc + "BrandName", line.BrandName));
            if (!string.IsNullOrWhiteSpace(line.TariffCode))
            {
                itemElement.Element(cac + "StandardItemIdentification")!.ReplaceWith(new XElement(cac + "StandardItemIdentification",
                    new XElement(cbc + "ID",
                        new XAttribute("schemeID", "020"), new XAttribute("schemeName", "Partida Arancelarias"), new XAttribute("schemeAgencyID", "195"),
                        line.TariffCode)));
            }

            // Sector Transporte de Carga (Guía de Factura Electrónica de Transporte, Mintransporte —
            // ver docs/dian-transporte/): cada línea marcada como remesa RNDC lleva estos 3 datos
            // obligatorios como AdditionalItemProperty dentro de Item.
            if (line.IsRndcRemittance == true)
            {
                itemElement.Add(
                    new XElement(cac + "AdditionalItemProperty",
                        new XElement(cbc + "Name", "01"),
                        new XElement(cbc + "Value", line.RndcRemittanceRadicado)
                    ),
                    new XElement(cac + "AdditionalItemProperty",
                        new XElement(cbc + "Name", "02"),
                        new XElement(cbc + "Value", line.RndcRemittanceConsecutive)
                    ),
                    new XElement(cac + "AdditionalItemProperty",
                        new XElement(cbc + "Name", "03"),
                        new XElement(cbc + "Value", (line.RndcFreightValue ?? 0).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)),
                        new XElement(cbc + "ValueQuantity", new XAttribute("unitCode", line.RndcTransportedUnitCode ?? "KGM"), (line.RndcTransportedQuantity ?? 0).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture))
                    )
                );
            }

            // Orden UBL dentro de Item: el mandante va después de las propiedades adicionales.
            var mandante = BuildMandante(line);
            if (mandante != null) itemElement.Add(mandante);

            var lineElement = new XElement(cac + lineElementName,
                idElement);
            // Orden UBL: ID, Note, cantidad... — la nota solo se emite cuando la línea la trae.
            if (!string.IsNullOrWhiteSpace(line.Note)) lineElement.Add(new XElement(cbc + "Note", line.Note));
            lineElement.Add(
                new XElement(cbc + quantityElementName, new XAttribute("unitCode", line.UnitCode), QuantityOrPrice(line.Quantity)),
                new XElement(cbc + "LineExtensionAmount", new XAttribute("currencyID", currency), line.LineExtensionAmount.ToString("0.00").Replace(",", ".")),
                // Confirmado contra el ejemplo oficial Generica.xml y contra un firmador Python de
                // referencia en producción activa — presente en cada línea de ambos. Faltaba por
                // completo.
                new XElement(cbc + "FreeOfChargeIndicator", "false")
            );

            // Cada línea debe declarar su propio TaxTotal (numeral 6.5.10, FAS01b) — antes faltaba
            // por completo, la DIAN rechazaba con "Tributo IVA/INC informado no coincide... Debe
            // existir un TaxTotal a nivel del cabecera por cada tipo de impuesto que se informa a
            // nivel de línea".
            // Igual que en el encabezado: un TaxTotal por tipo de impuesto de la línea.
            foreach (var lineTaxTotal in BuildTaxTotals(line.Taxes, currency)) lineElement.Add(lineTaxTotal);

            lineElement.Add(
                itemElement,
                // BaseQuantity (confirmado contra la factura real de Comcel) faltaba — es la
                // cantidad sobre la que aplica PriceAmount (normalmente 1, precio unitario).
                // Explicaba la notificación FBB04 ("No se informó la cantidad real sobre la cual
                // el precio aplica").
                new XElement(cac + "Price",
                    new XElement(cbc + "PriceAmount", new XAttribute("currencyID", currency), QuantityOrPrice(line.UnitPrice)),
                    new XElement(cbc + "BaseQuantity", new XAttribute("unitCode", line.UnitCode), "1.0")
                )
            );

            return lineElement;
        }
    }
}
