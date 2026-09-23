using System.Xml.Linq;
using Fel.Core.Models;
using Fel.Core.Interfaces;

namespace Fel.Infrastructure.Ubl.Strategies
{
    // Evento de Recepción RADIAN (Acuse de Recibo=030, Reclamo=031, Recibo del bien=032,
    // Aceptación expresa=033) — Anexo Técnico de Factura Electrónica v1.9, numeral 6.5. Esquema
    // propio (no hereda BaseUblStrategy, que es para el árbol de UBL Invoice/CreditNote).
    public class ApplicationResponseUblStrategy
    {
        private static readonly XNamespace ar = "urn:oasis:names:specification:ubl:schema:xsd:ApplicationResponse-2";
        private static readonly XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
        private static readonly XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
        private static readonly XNamespace ext = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";
        // Namespace real confirmado contra el targetNamespace del XSD oficial (DIAN_UBL_Structures.xsd)
        // y contra el ejemplo real de la DIAN (Generica.xml).
        private static readonly XNamespace sts = "dian:gov:co:facturaelectronica:Structures-2-1";
        private static readonly XNamespace xades = "http://uri.etsi.org/01903/v1.3.2#";
        private static readonly XNamespace xades141 = "http://uri.etsi.org/01903/v1.4.1#";
        private static readonly XNamespace ds = "http://www.w3.org/2000/09/xmldsig#";

        // NIT de la DIAN, fijo (numeral AAB31 del anexo) — no depende del emisor/adquirente.
        private const string DianNit = "800197268";

        private readonly ICryptoService _cryptoService;

        public ApplicationResponseUblStrategy(ICryptoService cryptoService)
        {
            _cryptoService = cryptoService;
        }

        public XElement GenerateXml(UblEventData data, string cude)
        {
            return new XElement(ar + "ApplicationResponse",
                new XAttribute(XNamespace.Xmlns + "cac", cac),
                new XAttribute(XNamespace.Xmlns + "cbc", cbc),
                new XAttribute(XNamespace.Xmlns + "ext", ext),
                new XAttribute(XNamespace.Xmlns + "sts", sts),
                new XAttribute(XNamespace.Xmlns + "xades", xades),
                new XAttribute(XNamespace.Xmlns + "xades141", xades141),
                new XAttribute(XNamespace.Xmlns + "ds", ds),
                new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),

                new XElement(ext + "UBLExtensions",
                    new XElement(ext + "UBLExtension",
                        new XElement(ext + "ExtensionContent",
                            new XElement(sts + "DianExtensions",
                                new XElement(sts + "InvoiceSource", new XAttribute("schemeAgencyID", "6"), new XAttribute("schemeAgencyName", "United Nations Economic Commission for Europe"), new XElement(cbc + "IdentificationCode", "CO")),
                                new XElement(sts + "SoftwareProvider",
                                    new XElement(sts + "ProviderID", new XAttribute("schemeID", "4"), new XAttribute("schemeAgencyID", "195"), new XAttribute("schemeAgencyName", "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)"), new XAttribute("schemeName", "31"), data.SenderTaxId),
                                    new XElement(sts + "SoftwareID", new XAttribute("schemeAgencyID", "195"), new XAttribute("schemeAgencyName", "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)"), data.SenderTaxId)
                                ),
                                new XElement(sts + "SoftwareSecurityCode", new XAttribute("schemeAgencyID", "195"), new XAttribute("schemeAgencyName", "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)"),
                                    _cryptoService.GenerateCufeSha384($"{data.SenderTaxId}{data.SoftwarePin}")),
                                new XElement(sts + "AuthorizationProvider",
                                    new XElement(sts + "AuthorizationProviderID", new XAttribute("schemeAgencyID", "195"), new XAttribute("schemeAgencyName", "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)"), new XAttribute("schemeName", "31"), DianNit)
                                ),
                                // El QR referencia el CUFE del documento AJENO sobre el que se emite el evento, no el CUDE propio del evento.
                                new XElement(sts + "QRCode", $"https://catalogo-vpfe{(data.Environment == "1" ? "" : "-hab")}.dian.gov.co/document/searchqr?documentkey={data.ReferencedCufe}")
                            )
                        )
                    ),
                    new XElement(ext + "UBLExtension",
                        new XElement(ext + "ExtensionContent",
                            new XElement("SignaturePlaceholder")
                        )
                    )
                ),

                new XElement(cbc + "UBLVersionID", "UBL 2.1"),
                new XElement(cbc + "CustomizationID", "1"),
                new XElement(cbc + "ProfileID", "DIAN 2.1: ApplicationResponse de Factura Electrónica de Venta"),
                new XElement(cbc + "ProfileExecutionID", data.Environment),
                new XElement(cbc + "ID", data.DocumentNumber),
                new XElement(cbc + "UUID", new XAttribute("schemeID", data.Environment), new XAttribute("schemeName", "CUDE-SHA384"), cude),
                new XElement(cbc + "IssueDate", data.IssueDate.ToString("yyyy-MM-dd")),
                new XElement(cbc + "IssueTime", data.IssueTime.ToString("HH:mm:sszzz")),

                new XElement(cac + "SenderParty",
                    new XElement(cac + "PartyTaxScheme",
                        new XElement(cbc + "RegistrationName", data.SenderName),
                        new XElement(cbc + "CompanyID", new XAttribute("schemeAgencyID", "195"), new XAttribute("schemeAgencyName", "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)"), new XAttribute("schemeName", "31"), data.SenderTaxId),
                        new XElement(cac + "TaxScheme", new XElement(cbc + "ID", "01"), new XElement(cbc + "Name", "IVA"))
                    )
                ),
                new XElement(cac + "ReceiverParty",
                    new XElement(cac + "PartyTaxScheme",
                        new XElement(cbc + "RegistrationName", data.ReceiverName),
                        new XElement(cbc + "CompanyID", new XAttribute("schemeAgencyID", "195"), new XAttribute("schemeAgencyName", "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)"), new XAttribute("schemeName", "31"), data.ReceiverTaxId),
                        new XElement(cac + "TaxScheme", new XElement(cbc + "ID", "01"), new XElement(cbc + "Name", "IVA"))
                    )
                ),

                new XElement(cac + "DocumentResponse",
                    new XElement(cac + "Response",
                        new XElement(cbc + "ResponseCode", data.ResponseCode),
                        new XElement(cbc + "Description", data.ResponseDescription)
                    ),
                    new XElement(cac + "DocumentReference",
                        new XElement(cbc + "ID", data.ReferencedDocumentId),
                        new XElement(cbc + "UUID", new XAttribute("schemeName", "CUFE-SHA384"), data.ReferencedCufe),
                        new XElement(cbc + "DocumentTypeCode", data.ReferencedDocumentTypeCode)
                    )
                )
            );
        }
    }
}
