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
        public static UblInvoiceData BuildInvoiceData(
            Document document,
            Customer customer,
            IEnumerable<DocumentItem> items,
            Resolution resolution,
            Client client,
            IReadOnlyDictionary<string, DianMunicipality> municipalitiesByCode)
        {
            var itemsList = items as IReadOnlyList<DocumentItem> ?? items.ToList();

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
                DianCode = string.IsNullOrWhiteSpace(document.TypeCode) ? "01" : document.TypeCode,
                OperationType = "10",
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

            // Agrupa las líneas por tarifa de IVA (mismo criterio que el desglose del resumen de
            // factura en el portal) para armar el TaxTotal del documento. Las retenciones no se
            // incluyen todavía: BaseUblStrategy no tiene un bloque WithholdingTaxTotal, así que por
            // ahora solo viajan con Dataico.
            var taxGroups = new Dictionary<decimal, (decimal Base, decimal Amount)>();
            decimal lineExtension = 0;

            foreach (var item in itemsList)
            {
                var lineBase = item.Quantity * item.UnitPrice * (1 - item.DiscountRate / 100);
                lineExtension += lineBase;

                data.Lines.Add(new InvoiceLine
                {
                    ItemCode = item.Code,
                    Description = item.Name,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    LineExtensionAmount = lineBase
                });

                if (item.IvaTreatment == IvaTreatment.Gravado && item.TaxRate > 0)
                {
                    var prev = taxGroups.TryGetValue(item.TaxRate, out var v) ? v : (Base: 0m, Amount: 0m);
                    taxGroups[item.TaxRate] = (prev.Base + lineBase, prev.Amount + item.TaxAmount);
                }
            }

            foreach (var (rate, (baseAmount, amount)) in taxGroups)
            {
                data.Taxes.Add(new TaxSubtotal { TaxId = "01", TaxableAmount = baseAmount, TaxAmount = amount, Percent = rate });
            }

            var totalTax = taxGroups.Values.Sum(v => v.Amount);
            data.LineExtensionAmount = lineExtension;
            data.TaxExclusiveAmount = lineExtension;
            data.TaxInclusiveAmount = lineExtension + totalTax;
            data.PayableAmount = lineExtension + totalTax;

            return data;
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
