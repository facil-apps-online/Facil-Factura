using System.Linq;
using System.Xml.Linq;
using Fel.Core.Models;
using Fel.Core.Interfaces;

namespace Fel.Infrastructure.Ubl.Strategies
{
    public abstract class BaseUblStrategy
    {
        protected readonly ICryptoService _cryptoService;

        protected static readonly XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
        protected static readonly XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
        protected static readonly XNamespace ext = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";
        protected static readonly XNamespace sts = "http://www.dian.gov.co/contratos/facturaelectronica/v1/Structures";
        protected static readonly XNamespace xades = "http://uri.etsi.org/01903/v1.3.2#";
        protected static readonly XNamespace xades141 = "http://uri.etsi.org/01903/v1.4.1#";
        protected static readonly XNamespace ds = "http://www.w3.org/2000/09/xmldsig#";
        
        public BaseUblStrategy(ICryptoService cryptoService)
        {
            _cryptoService = cryptoService;
        }

        public abstract XElement GenerateXml(UblInvoiceData data, string cufe);

        protected XElement BuildExtensions(UblInvoiceData data)
        {
            var extensions = new XElement(ext + "UBLExtensions",
                new XElement(ext + "UBLExtension",
                    new XElement(ext + "ExtensionContent",
                        new XElement(sts + "DianExtensions",
                            new XElement(sts + "InvoiceControl",
                                new XElement(sts + "InvoiceAuthorization", "18760000001"),
                                new XElement(sts + "AuthorizationPeriod",
                                    new XElement(cbc + "StartDate", "2024-01-01"),
                                    new XElement(cbc + "EndDate", "2025-01-01")
                                ),
                                new XElement(sts + "AuthorizedInvoices",
                                    new XElement(sts + "Prefix", data.Prefix),
                                    new XElement(sts + "From", "1"),
                                    new XElement(sts + "To", "5000000")
                                )
                            ),
                            new XElement(sts + "SoftwareProvider",
                                new XElement(sts + "ProviderID", new XAttribute("schemeID", "4"), new XAttribute("schemeName", "31"), "NITPROVEEDOR"),
                                new XElement(sts + "SoftwareID", new XAttribute("schemeAgencyID", "195"), new XAttribute("schemeAgencyName", "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)"), "SOFTWAREID_DIAN")
                            ),
                            new XElement(sts + "SoftwareSecurityCode", new XAttribute("schemeAgencyID", "195"), new XAttribute("schemeAgencyName", "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)"), 
                                _cryptoService.GenerateCufeSha384($"SOFTWAREID_DIAN{data.SoftwarePin}"))
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
            return new XElement(cac + "AccountingSupplierParty",
                new XElement(cbc + "AdditionalAccountID", "1"),
                new XElement(cac + "Party",
                    new XElement(cac + "PartyName", new XElement(cbc + "Name", data.Issuer.Name)),
                    new XElement(cac + "PhysicalLocation", 
                        new XElement(cac + "Address", 
                            new XElement(cbc + "ID", data.Issuer.CityCode),
                            new XElement(cbc + "CityName", "Ciudad"),
                            new XElement(cbc + "CountrySubentity", "Departamento"),
                            new XElement(cbc + "CountrySubentityCode", data.Issuer.DepartmentCode),
                            new XElement(cac + "AddressLine", new XElement(cbc + "Line", data.Issuer.Address)),
                            new XElement(cac + "Country", new XElement(cbc + "IdentificationCode", "CO"), new XElement(cbc + "Name", "Colombia"))
                        )
                    ),
                    new XElement(cac + "PartyTaxScheme",
                        new XElement(cbc + "RegistrationName", data.Issuer.Name),
                        new XElement(cbc + "CompanyID", new XAttribute("schemeID", "1"), new XAttribute("schemeName", "31"), data.Issuer.TaxId),
                        new XElement(cbc + "TaxLevelCode", string.Join(";", data.Issuer.TaxLevelCodes)),
                        new XElement(cac + "TaxScheme", new XElement(cbc + "ID", data.Issuer.TaxSchemeId), new XElement(cbc + "Name", "IVA"))
                    )
                )
            );
        }

        protected XElement BuildAccountingCustomerParty(UblInvoiceData data)
        {
            return new XElement(cac + "AccountingCustomerParty",
                new XElement(cbc + "AdditionalAccountID", "1"),
                new XElement(cac + "Party",
                    new XElement(cac + "PartyName", new XElement(cbc + "Name", data.Customer.Name)),
                    new XElement(cac + "PhysicalLocation", 
                        new XElement(cac + "Address", 
                            new XElement(cbc + "ID", data.Customer.CityCode),
                            new XElement(cbc + "CityName", "Ciudad"),
                            new XElement(cbc + "CountrySubentity", "Departamento"),
                            new XElement(cbc + "CountrySubentityCode", data.Customer.DepartmentCode),
                            new XElement(cac + "AddressLine", new XElement(cbc + "Line", data.Customer.Address)),
                            new XElement(cac + "Country", new XElement(cbc + "IdentificationCode", "CO"), new XElement(cbc + "Name", "Colombia"))
                        )
                    ),
                    new XElement(cac + "PartyTaxScheme",
                        new XElement(cbc + "RegistrationName", data.Customer.Name),
                        new XElement(cbc + "CompanyID", new XAttribute("schemeID", "1"), new XAttribute("schemeName", "13"), data.Customer.TaxId),
                        new XElement(cbc + "TaxLevelCode", string.Join(";", data.Customer.TaxLevelCodes)),
                        new XElement(cac + "TaxScheme", new XElement(cbc + "ID", data.Customer.TaxSchemeId), new XElement(cbc + "Name", "No aplica"))
                    )
                )
            );
        }

        protected XElement BuildTaxTotals(UblInvoiceData data)
        {
            var taxTotals = new XElement(cac + "TaxTotal",
                new XElement(cbc + "TaxAmount", new XAttribute("currencyID", data.Currency), data.Taxes.Sum(t => t.TaxAmount).ToString("0.00").Replace(",", "."))
            );
            foreach (var tax in data.Taxes)
            {
                taxTotals.Add(new XElement(cac + "TaxSubtotal",
                    new XElement(cbc + "TaxableAmount", new XAttribute("currencyID", data.Currency), tax.TaxableAmount.ToString("0.00").Replace(",", ".")),
                    new XElement(cbc + "TaxAmount", new XAttribute("currencyID", data.Currency), tax.TaxAmount.ToString("0.00").Replace(",", ".")),
                    new XElement(cac + "TaxCategory",
                        new XElement(cbc + "Percent", tax.Percent.ToString("0.00").Replace(",", ".")),
                        new XElement(cac + "TaxScheme", new XElement(cbc + "ID", tax.TaxId), new XElement(cbc + "Name", "IVA"))
                    )
                ));
            }
            return taxTotals;
        }

        protected XElement BuildLegalMonetaryTotal(UblInvoiceData data)
        {
            return new XElement(cac + "LegalMonetaryTotal",
                new XElement(cbc + "LineExtensionAmount", new XAttribute("currencyID", data.Currency), data.LineExtensionAmount.ToString("0.00").Replace(",", ".")),
                new XElement(cbc + "TaxExclusiveAmount", new XAttribute("currencyID", data.Currency), data.TaxExclusiveAmount.ToString("0.00").Replace(",", ".")),
                new XElement(cbc + "TaxInclusiveAmount", new XAttribute("currencyID", data.Currency), data.TaxInclusiveAmount.ToString("0.00").Replace(",", ".")),
                new XElement(cbc + "PayableAmount", new XAttribute("currencyID", data.Currency), data.PayableAmount.ToString("0.00").Replace(",", "."))
            );
        }

        protected XElement BuildLine(string lineElementName, string quantityElementName, InvoiceLine line, string currency)
        {
            return new XElement(cac + lineElementName,
                new XElement(cbc + "ID", "1"),
                new XElement(cbc + quantityElementName, new XAttribute("unitCode", "94"), line.Quantity.ToString("0.00").Replace(",", ".")),
                new XElement(cbc + "LineExtensionAmount", new XAttribute("currencyID", currency), line.LineExtensionAmount.ToString("0.00").Replace(",", ".")),
                new XElement(cac + "Item",
                    new XElement(cbc + "Description", line.Description),
                    new XElement(cac + "StandardItemIdentification", new XElement(cbc + "ID", new XAttribute("schemeID", "999"), line.ItemCode))
                ),
                new XElement(cac + "Price",
                    new XElement(cbc + "PriceAmount", new XAttribute("currencyID", currency), line.UnitPrice.ToString("0.00").Replace(",", "."))
                )
            );
        }
    }
}
