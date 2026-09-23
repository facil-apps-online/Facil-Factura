using System;
using System.Collections.Generic;
using System.Linq;
using Fel.Core.Entities;
using Fel.Infrastructure.Dataico.Models;

namespace Fel.Infrastructure.Dataico
{
    public static class DataicoDocumentMapper
    {
        // Gravado: se envía el IVA con su tarifa. Exento: se envía el IVA en tarifa 0.
        // Excluido: no se envía impuesto de IVA para el ítem (confirmado contra ejemplos reales de Dataico).
        private static DataicoTax? BuildIvaTax(IvaTreatment treatment, decimal rate) => treatment switch
        {
            IvaTreatment.Gravado => new DataicoTax { tax_category = "IVA", tax_rate = rate },
            IvaTreatment.Exento => new DataicoTax { tax_category = "IVA", tax_rate = 0 },
            _ => null
        };

        private static List<DataicoTax>? BuildRetentions(IEnumerable<DocumentRetention> retentions, IEnumerable<DataicoTax>? extra = null)
        {
            var list = retentions.Select(r => new DataicoTax { tax_category = r.TaxCategory, tax_rate = r.Rate }).ToList();
            if (extra != null) list.AddRange(extra);
            return list.Count > 0 ? list : null;
        }

        // Retenciones definidas una sola vez para todo el documento (ReteICA, ReteIVA, u otras —
        // lista libre de agregar/quitar, no solo esas dos) en vez de por línea, pero Dataico exige
        // las retenciones por ítem en su API. Se reparte (prorratea) cada monto global entre los
        // ítems según su peso en la base correspondiente — RET_IVA se prorratea sobre el IVA
        // generado de cada línea (es una retención sobre el impuesto, no sobre la venta); cualquier
        // otra categoría se prorratea sobre la base gravable (subtotal) de cada línea — y el último
        // ítem con peso > 0 absorbe el ajuste de redondeo para que la suma prorrateada cuadre exacto
        // con el monto global calculado sobre el documento completo.
        private static Dictionary<Guid, List<DataicoTax>> BuildGeneralRetentionsPerItem(Document document, IReadOnlyList<DocumentItem> items)
        {
            var result = items.ToDictionary(i => i.Id, i => new List<DataicoTax>());
            if (items.Count == 0) return result;

            decimal LineBase(DocumentItem i) => i.Quantity * i.UnitPrice * (1 - i.DiscountRate / 100);

            foreach (var generalRetention in document.GeneralRetentions)
            {
                if (generalRetention.TaxCategory == "RET_IVA")
                {
                    AddProrated(result, items, generalRetention.TaxCategory, generalRetention.Rate, i => i.TaxAmount, document.TaxAmount);
                }
                else
                {
                    AddProrated(result, items, generalRetention.TaxCategory, generalRetention.Rate, LineBase, document.Subtotal);
                }
            }

            return result;
        }

        private static void AddProrated(
            Dictionary<Guid, List<DataicoTax>> result,
            IReadOnlyList<DocumentItem> items,
            string category,
            decimal rate,
            Func<DocumentItem, decimal> weightOf,
            decimal totalWeight)
        {
            if (string.IsNullOrWhiteSpace(category) || rate <= 0 || totalWeight <= 0) return;

            var totalAmount = Math.Round(totalWeight * rate / 100, 2);
            var weights = items.Select(weightOf).ToList();
            var lastIndexWithWeight = -1;
            for (var i = 0; i < items.Count; i++)
            {
                if (weights[i] > 0) lastIndexWithWeight = i;
            }
            if (lastIndexWithWeight < 0) return;

            decimal assigned = 0;
            for (var i = 0; i < items.Count; i++)
            {
                if (weights[i] <= 0) continue;

                var itemAmount = i == lastIndexWithWeight
                    ? totalAmount - assigned
                    : Math.Round(totalAmount * weights[i] / totalWeight, 2);
                if (i != lastIndexWithWeight) assigned += itemAmount;

                result[items[i].Id].Add(new DataicoTax
                {
                    tax_category = category,
                    tax_rate = rate,
                    base_amount = weights[i],
                    tax_amount = itemAmount
                });
            }
        }

        // Descuento y cargo general del documento completo (ej. "Pronto pago" / "Flete"),
        // independientes del descuento por ítem — Dataico los espera ambos como "charges",
        // distinguidos por discount=true/false.
        private static List<DataicoCharge>? BuildCharges(Document document)
        {
            var charges = new List<DataicoCharge>();

            if (document.GeneralDiscountAmount.HasValue && document.GeneralDiscountAmount.Value > 0)
            {
                charges.Add(new DataicoCharge
                {
                    reason = string.IsNullOrWhiteSpace(document.GeneralDiscountReason) ? "DESCUENTO" : document.GeneralDiscountReason,
                    base_amount = document.GeneralDiscountAmount.Value,
                    discount = true
                });
            }

            if (document.GeneralChargeAmount.HasValue && document.GeneralChargeAmount.Value > 0)
            {
                charges.Add(new DataicoCharge
                {
                    reason = string.IsNullOrWhiteSpace(document.GeneralChargeReason) ? "CARGO" : document.GeneralChargeReason,
                    base_amount = document.GeneralChargeAmount.Value,
                    discount = false
                });
            }

            return charges.Count > 0 ? charges : null;
        }

        // Fecha de vencimiento = fecha de factura + plazo de pago en días; si no hay plazo, no se envía.
        private static string? BuildPaymentDate(Document document) =>
            document.PaymentTermDays.HasValue ? document.IssueDate.AddDays(document.PaymentTermDays.Value).ToString("dd/MM/yyyy") : null;

        public static DataicoCredentials ToCredentials(Client client, Fel.Core.Interfaces.ICryptoService cryptoService) => new()
        {
            ApiUser = client.DataicoApiUser,
            ApiPassword = string.IsNullOrEmpty(client.DataicoApiPasswordEncrypted) ? string.Empty : cryptoService.Decrypt(client.DataicoApiPasswordEncrypted),
            AuthToken = string.IsNullOrEmpty(client.DataicoAuthTokenEncrypted) ? string.Empty : cryptoService.Decrypt(client.DataicoAuthTokenEncrypted),
            AccountId = client.DataicoAccountId,
            Environment = client.DataicoEnvironment
        };

        public static DataicoInvoiceRequest BuildInvoiceRequest(
            Document document,
            Customer customer,
            IEnumerable<DocumentItem> items,
            Resolution resolution,
            Client client,
            string paymentMeans,
            string paymentMeansType)
        {
            var request = new DataicoInvoiceRequest
            {
                env = client.DataicoEnvironment,
                dataico_account_id = string.IsNullOrEmpty(client.DataicoAccountId) ? null : client.DataicoAccountId,
                number = document.Number,
                issue_date = document.IssueDate.ToString("dd/MM/yyyy"),
                payment_date = BuildPaymentDate(document),
                order_reference = document.PurchaseOrderReference,
                payment_means = paymentMeans,
                payment_means_type = paymentMeansType,
                numbering = new DataicoNumbering
                {
                    prefix = resolution.Prefix,
                    resolution_number = resolution.ResolutionNumber,
                    flexible = true
                },
                customer = DataicoMapper.ToDataicoParty(customer),
                charges = BuildCharges(document)
            };

            var itemsList = items as IReadOnlyList<DocumentItem> ?? items.ToList();
            var generalRetentions = BuildGeneralRetentionsPerItem(document, itemsList);

            foreach (var item in itemsList)
            {
                var dataicoItem = new DataicoInvoiceItem
                {
                    sku = item.Code,
                    quantity = item.Quantity,
                    description = item.Name,
                    price = item.UnitPrice,
                    discount_rate = item.DiscountRate,
                    retentions = BuildRetentions(item.Retentions, generalRetentions[item.Id])
                };

                var ivaTax = BuildIvaTax(item.IvaTreatment, item.TaxRate);
                if (ivaTax != null)
                {
                    dataicoItem.taxes.Add(ivaTax);
                }

                request.items.Add(dataicoItem);
            }

            return request;
        }

        // Catálogo cerrado de Dataico confirmado con ejemplos reales (DEVOLUCION, ANULACION,
        // OTROS); el motivo que captura el portal es texto libre, así que se mapea por
        // coincidencia de palabra clave y cualquier caso no reconocido cae en OTROS.
        private static string MapReasonToDataico(string? referenceConcept)
        {
            var text = (referenceConcept ?? string.Empty).ToUpperInvariant();
            if (text.Contains("DEVOLU")) return "DEVOLUCION";
            if (text.Contains("ANULA")) return "ANULACION";
            return "OTROS";
        }

        public static DataicoCreditNoteRequest BuildCreditNoteRequest(
            Document document, Resolution resolution, Client client, string originalInvoiceDataicoId, IEnumerable<DocumentItem> items)
        {
            var request = new DataicoCreditNoteRequest
            {
                env = client.DataicoEnvironment,
                dataico_account_id = string.IsNullOrEmpty(client.DataicoAccountId) ? null : client.DataicoAccountId,
                invoice_id = originalInvoiceDataicoId,
                issue_date = document.IssueDate.ToString("dd/MM/yyyy"),
                reason = MapReasonToDataico(document.ReferenceConcept),
                number = document.Number,
                numbering = new DataicoNumbering { prefix = resolution.Prefix, resolution_number = resolution.ResolutionNumber, flexible = true },
                charges = BuildCharges(document)
            };

            var creditNoteItemsList = items as IReadOnlyList<DocumentItem> ?? items.ToList();
            var creditNoteGeneralRetentions = BuildGeneralRetentionsPerItem(document, creditNoteItemsList);

            foreach (var item in creditNoteItemsList)
            {
                var dataicoItem = new DataicoInvoiceItem
                {
                    sku = item.Code,
                    quantity = item.Quantity,
                    description = item.Name,
                    price = item.UnitPrice,
                    discount_rate = item.DiscountRate,
                    retentions = BuildRetentions(item.Retentions, creditNoteGeneralRetentions[item.Id])
                };

                var ivaTax = BuildIvaTax(item.IvaTreatment, item.TaxRate);
                if (ivaTax != null) dataicoItem.taxes.Add(ivaTax);

                request.items.Add(dataicoItem);
            }

            return request;
        }

        public static DataicoDebitNoteRequest BuildDebitNoteRequest(
            Document document, Resolution resolution, Client client, string originalInvoiceDataicoId, IEnumerable<DocumentItem> items)
        {
            var request = new DataicoDebitNoteRequest
            {
                env = client.DataicoEnvironment,
                dataico_account_id = string.IsNullOrEmpty(client.DataicoAccountId) ? null : client.DataicoAccountId,
                invoice_id = originalInvoiceDataicoId,
                issue_date = document.IssueDate.ToString("dd/MM/yyyy"),
                reason = MapReasonToDataico(document.ReferenceConcept),
                number = document.Number,
                numbering = new DataicoNumbering { prefix = resolution.Prefix, resolution_number = resolution.ResolutionNumber, flexible = true },
                charges = BuildCharges(document)
            };

            var debitNoteItemsList = items as IReadOnlyList<DocumentItem> ?? items.ToList();
            var debitNoteGeneralRetentions = BuildGeneralRetentionsPerItem(document, debitNoteItemsList);

            foreach (var item in debitNoteItemsList)
            {
                var dataicoItem = new DataicoInvoiceItem
                {
                    sku = item.Code,
                    quantity = item.Quantity,
                    description = item.Name,
                    price = item.UnitPrice,
                    discount_rate = item.DiscountRate,
                    retentions = BuildRetentions(item.Retentions, debitNoteGeneralRetentions[item.Id])
                };

                var ivaTax = BuildIvaTax(item.IvaTreatment, item.TaxRate);
                if (ivaTax != null) dataicoItem.taxes.Add(ivaTax);

                request.items.Add(dataicoItem);
            }

            return request;
        }

        // Nota de Ajuste sobre un Documento Soporte ya emitido — referencia al original por su
        // CUDS (guardado en Document.Cufe), no por un id interno de Dataico como la Nota Crédito.
        public static DataicoSupportDocAdjustmentRequest BuildSupportDocAdjustmentRequest(
            Document document, Document originalDocument, Customer provider, Resolution resolution,
            Client client, string paymentMeans, string paymentMeansType, IEnumerable<DocumentItem> items)
        {
            var request = new DataicoSupportDocAdjustmentRequest
            {
                env = client.DataicoEnvironment,
                number = document.Number,
                issue_date = document.IssueDate.ToString("dd/MM/yyyy"),
                payment_date = BuildPaymentDate(document),
                payment_means = paymentMeans,
                payment_means_type = paymentMeansType,
                numbering = new DataicoNumbering { prefix = resolution.Prefix, resolution_number = resolution.ResolutionNumber, flexible = true },
                discrepancy_description = string.IsNullOrWhiteSpace(document.ReferenceConcept) ? "Ajuste de precio" : document.ReferenceConcept,
                source_document_cuds = originalDocument.Cufe ?? string.Empty,
                source_document_issue_date = originalDocument.IssueDate.ToString("dd/MM/yyyy"),
                source_document_number = originalDocument.Number,
                customer = DataicoMapper.ToDataicoParty(provider)
            };

            foreach (var item in items)
            {
                request.items.Add(new DataicoInvoiceItem
                {
                    sku = item.Code,
                    quantity = item.Quantity,
                    description = item.Name,
                    price = item.UnitPrice,
                    discount_rate = item.DiscountRate
                });
            }

            return request;
        }

        public static DataicoSupportDocumentRequest BuildSupportDocumentRequest(
            Document document,
            Customer provider,
            IEnumerable<DocumentItem> items,
            Resolution resolution,
            Client client,
            string paymentMeans,
            string paymentMeansType)
        {
            var request = new DataicoSupportDocumentRequest
            {
                env = client.DataicoEnvironment,
                number = document.Number,
                issue_date = document.IssueDate.ToString("dd/MM/yyyy"),
                payment_date = BuildPaymentDate(document),
                order_reference = document.PurchaseOrderReference,
                payment_means = paymentMeans,
                payment_means_type = paymentMeansType,
                numbering = new DataicoNumbering
                {
                    prefix = resolution.Prefix,
                    resolution_number = resolution.ResolutionNumber,
                    flexible = true
                },
                customer = DataicoMapper.ToDataicoParty(provider),
                charges = BuildCharges(document)
            };

            var itemsList = items as IReadOnlyList<DocumentItem> ?? items.ToList();
            var generalRetentions = BuildGeneralRetentionsPerItem(document, itemsList);

            foreach (var item in itemsList)
            {
                var dataicoItem = new DataicoSupportDocItem
                {
                    sku = item.Code,
                    quantity = item.Quantity,
                    description = item.Name,
                    price = item.UnitPrice,
                    discount_rate = item.DiscountRate,
                    retentions = BuildRetentions(item.Retentions, generalRetentions[item.Id])
                };

                var ivaTax = BuildIvaTax(item.IvaTreatment, item.TaxRate);
                if (ivaTax != null)
                {
                    dataicoItem.taxes.Add(ivaTax);
                }

                request.items.Add(dataicoItem);
            }

            return request;
        }

        // Entrada genérica de un concepto de nómina (devengo o deducción), usada tanto por la
        // captura manual simplificada (solo Description+Amount) como por el importador de Excel
        // (formato estándar de carga masiva de Dataico, con código y campos reales por concepto).
        public class PayrollConceptInput
        {
            public string? Code { get; set; }
            public string? Description { get; set; }
            public decimal? Amount { get; set; }
            public decimal? AmountNs { get; set; }
            public int? Days { get; set; }
            public decimal? Percentage { get; set; }
            public decimal? Hours { get; set; }
            public string? InitialDate { get; set; }
            public string? FinalDate { get; set; }
            public string? MedicalLeaveType { get; set; }
            public decimal? CesantiasInterest { get; set; }
            public decimal? OrdinaryCompensation { get; set; }
            public decimal? ExtraordinaryCompensation { get; set; }
        }

        private static DataicoPayrollConcept ToDataicoConcept(PayrollConceptInput input, string defaultCode) => new()
        {
            code = string.IsNullOrWhiteSpace(input.Code) ? defaultCode : input.Code!,
            description = input.Description,
            amount = input.Amount,
            amount_ns = input.AmountNs,
            days = input.Days,
            percentage = input.Percentage,
            hours = input.Hours,
            initial_date = input.InitialDate,
            final_date = input.FinalDate,
            medical_leave_type = input.MedicalLeaveType,
            cesantias_interest = input.CesantiasInterest,
            ordinary_compensation = input.OrdinaryCompensation,
            extraordinary_compensation = input.ExtraordinaryCompensation
        };

        public static DataicoPayrollRequest BuildPayrollRequest(
            Customer employee,
            Client client,
            string prefix,
            long number,
            DateTime initialSettlement,
            DateTime finalSettlement,
            DateTime issueDate,
            DateTime paymentDate,
            IEnumerable<PayrollConceptInput> accruals,
            IEnumerable<PayrollConceptInput> deductions)
        {
            var (department, city) = DataicoMapper.SplitCityCode(employee.CityCode);

            var request = new DataicoPayrollRequest
            {
                env = client.DataicoEnvironment,
                prefix = prefix,
                number = number,
                salary = (int)(employee.BaseSalary ?? 0),
                initial_settlement_date = initialSettlement.ToString("dd/MM/yyyy"),
                final_settlement_date = finalSettlement.ToString("dd/MM/yyyy"),
                issue_date = issueDate.ToString("dd/MM/yyyy"),
                payment_date = paymentDate.ToString("dd/MM/yyyy"),
                employee = new DataicoPayrollEmployee
                {
                    code = employee.IdentificationNumber,
                    payment_means = employee.PaymentMeans ?? string.Empty,
                    worker_type = employee.WorkerType ?? string.Empty,
                    contract_type = employee.ContractType ?? string.Empty,
                    identification_type = DataicoMapper.MapIdentificationType(employee.IdentificationType),
                    identification = employee.IdentificationNumber,
                    first_name = string.IsNullOrWhiteSpace(employee.FirstName) ? employee.Name : employee.FirstName,
                    other_names = employee.SecondName,
                    last_name = employee.FirstLastName ?? string.Empty,
                    second_last_name = employee.SecondLastName,
                    high_risk = employee.HighRisk,
                    integral_salary = employee.IntegralSalary,
                    start_date = employee.StartDate?.ToString("dd/MM/yyyy"),
                    fire_date = employee.FireDate?.ToString("dd/MM/yyyy"),
                    bank = employee.Bank,
                    account_type_kw = employee.AccountType,
                    account_number = employee.AccountNumber,
                    address = new DataicoEmployeeAddress { department = department, city = city, line = employee.Address }
                },
                software = new DataicoPayrollSoftware
                {
                    pin = client.SoftwarePin,
                    test_set_id = client.TestSetId,
                    dian_id = client.SoftwareId
                }
            };

            request.accruals.AddRange(accruals.Select(a => ToDataicoConcept(a, "OTRO_CONCEPTO")));
            request.deductions.AddRange(deductions.Select(d => ToDataicoConcept(d, "OTRA_DEDUCCION")));

            return request;
        }

        private static DataicoPayrollDocumentReference BuildPayrollReference(string prefix, long number, string cune, DateTime issueDate) => new()
        {
            prefix = prefix,
            number = number,
            cune = cune,
            issue_date = issueDate.ToString("dd/MM/yyyy")
        };

        private static List<DataicoPayrollNote> BuildPayrollNotes(string? text) =>
            string.IsNullOrWhiteSpace(text) ? new List<DataicoPayrollNote>() : new List<DataicoPayrollNote> { new() { text = text } };

        // Nota de Eliminación: solo anula el comprobante original, referenciado por su CUNE — no
        // reenvía datos de nómina.
        public static DataicoPayrollDeletionRequest BuildPayrollDeletionRequest(
            Client client, string prefix, long number, DateTime issueDate,
            string originalPrefix, long originalNumber, string originalCune, DateTime originalIssueDate, string? noteText)
        {
            return new DataicoPayrollDeletionRequest
            {
                env = client.DataicoEnvironment,
                prefix = prefix,
                number = number,
                issue_date = issueDate.ToString("dd/MM/yyyy"),
                entry = BuildPayrollReference(originalPrefix, originalNumber, originalCune, originalIssueDate),
                notes = BuildPayrollNotes(noteText),
                software = new DataicoPayrollSoftware { pin = client.SoftwarePin, test_set_id = client.TestSetId, dian_id = client.SoftwareId }
            };
        }

        // Nota de Reemplazo: reenvía la nómina completa corregida (mismo payload que un comprobante
        // normal) más la referencia al comprobante que reemplaza.
        public static DataicoPayrollReplacementRequest BuildPayrollReplacementRequest(
            Customer employee, Client client, string prefix, long number,
            DateTime initialSettlement, DateTime finalSettlement, DateTime issueDate, DateTime paymentDate,
            IEnumerable<PayrollConceptInput> accruals, IEnumerable<PayrollConceptInput> deductions,
            string originalPrefix, long originalNumber, string originalCune, DateTime originalIssueDate, string? noteText)
        {
            var basePayload = BuildPayrollRequest(employee, client, prefix, number, initialSettlement, finalSettlement, issueDate, paymentDate, accruals, deductions);

            return new DataicoPayrollReplacementRequest
            {
                env = basePayload.env,
                prefix = basePayload.prefix,
                number = basePayload.number,
                salary = basePayload.salary,
                periodicity = basePayload.periodicity,
                initial_settlement_date = basePayload.initial_settlement_date,
                final_settlement_date = basePayload.final_settlement_date,
                issue_date = basePayload.issue_date,
                payment_date = basePayload.payment_date,
                employee = basePayload.employee,
                accruals = basePayload.accruals,
                deductions = basePayload.deductions,
                software = basePayload.software,
                notes = BuildPayrollNotes(noteText),
                replacement_for = BuildPayrollReference(originalPrefix, originalNumber, originalCune, originalIssueDate)
            };
        }
    }
}
