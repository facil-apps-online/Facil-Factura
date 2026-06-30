using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace Fel.Core.Ubl21.Base
{
    [XmlRoot("Invoice", Namespace = UblNamespaces.Main)]
    public class BaseInvoice
    {
        [XmlElement("UBLExtensions", Namespace = UblNamespaces.Ext)]
        public UBLExtensions Extensions { get; set; } = new UBLExtensions();

        [XmlElement("UBLVersionID", Namespace = UblNamespaces.Cbc)]
        public string UblVersionId { get; set; } = "UBL 2.1";

        [XmlElement("CustomizationID", Namespace = UblNamespaces.Cbc)]
        public string CustomizationId { get; set; } = string.Empty;

        [XmlElement("ProfileExecutionID", Namespace = UblNamespaces.Cbc)]
        public string ProfileExecutionId { get; set; } = "1"; // 1: Producción, 2: Pruebas

        [XmlElement("ID", Namespace = UblNamespaces.Cbc)]
        public string Id { get; set; } = string.Empty;

        [XmlElement("UUID", Namespace = UblNamespaces.Cbc)]
        public string Uuid { get; set; } = string.Empty; // CUFE

        [XmlElement("IssueDate", Namespace = UblNamespaces.Cbc)]
        public string IssueDate { get; set; } = string.Empty;

        [XmlElement("IssueTime", Namespace = UblNamespaces.Cbc)]
        public string IssueTime { get; set; } = string.Empty;

        [XmlElement("InvoiceTypeCode", Namespace = UblNamespaces.Cbc)]
        public string InvoiceTypeCode { get; set; } = "01";

        [XmlElement("DocumentCurrencyCode", Namespace = UblNamespaces.Cbc)]
        public string DocumentCurrencyCode { get; set; } = "COP";

        [XmlElement("AccountingSupplierParty", Namespace = UblNamespaces.Cac)]
        public AccountingSupplierParty SupplierParty { get; set; } = new AccountingSupplierParty();

        [XmlElement("AccountingCustomerParty", Namespace = UblNamespaces.Cac)]
        public AccountingCustomerParty CustomerParty { get; set; } = new AccountingCustomerParty();

        // Totals, Lines, etc. (Simplified for this boilerplate)
    }

    public class UBLExtensions
    {
        [XmlElement("UBLExtension", Namespace = UblNamespaces.Ext)]
        public List<UBLExtension> ExtensionList { get; set; } = new List<UBLExtension>();
    }

    public class UBLExtension
    {
        [XmlElement("ExtensionContent", Namespace = UblNamespaces.Ext)]
        public ExtensionContent Content { get; set; } = new ExtensionContent();
    }

    public class ExtensionContent
    {
        // This is where sector-specific details or DianExtensions go.
        // We use XmlAnyElement or specific properties depending on the parser strategy.
        [XmlAnyElement]
        public System.Xml.XmlElement? Any { get; set; }
    }

    public class AccountingSupplierParty
    {
        [XmlElement("Party", Namespace = UblNamespaces.Cac)]
        public Party Party { get; set; } = new Party();
    }

    public class AccountingCustomerParty
    {
        [XmlElement("Party", Namespace = UblNamespaces.Cac)]
        public Party Party { get; set; } = new Party();
    }

    public class Party
    {
        [XmlElement("PartyName", Namespace = UblNamespaces.Cac)]
        public PartyName PartyName { get; set; } = new PartyName();
    }

    public class PartyName
    {
        [XmlElement("Name", Namespace = UblNamespaces.Cbc)]
        public string Name { get; set; } = string.Empty;
    }
}
