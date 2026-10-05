using System;
using System.Collections.Generic;
using System.Linq;
using Fel.Core.Entities;
using Fel.Infrastructure.Dataico.Models;

namespace Fel.Infrastructure.Dataico
{
    public static class DataicoDocumentMapper
    {
        // Dataico exige payment_means incluso en CREDITO, aunque el formulario nunca lo captura ahí
        // (solo pide plazo de pago). "MUTUAL_AGREEMENT" ("Acuerdo Mutuo") es la categoría que el
        // cliente definió en el catálogo de medios de pago para representar ese caso. Se fuerza
        // siempre (no solo cuando viene vacío): un documento que empezó como Contado y se cambió a
        // Crédito puede traer pegado un medio de pago viejo (ej. "EFECTIVO") que Dataico rechaza
        // como inválido para una transacción a crédito.
        private const string CreditoDefaultPaymentMeans = "MUTUAL_AGREEMENT";

        private static string ResolvePaymentMeans(string paymentMeans, string paymentMeansType) =>
            paymentMeansType == "CREDITO" ? CreditoDefaultPaymentMeans : paymentMeans;

        // Gravado: se envía el IVA con su tarifa. Exento: se envía el IVA en tarifa 0.
        // Excluido: no se envía impuesto de IVA para el ítem (confirmado contra ejemplos reales de Dataico).
        private static DataicoTax? BuildIvaTax(IvaTreatment treatment, decimal rate) => treatment switch
        {
            IvaTreatment.Gravado => new DataicoTax { tax_category = "IVA", tax_rate = rate },
            IvaTreatment.Exento => new DataicoTax { tax_category = "IVA", tax_rate = 0 },
            _ => null
        };

        // Retenciones por ítem (las que se eligen a mano en cada línea): solo categoría y tarifa, Dataico
        // calcula base y valor. "extra" solo lo usa el documento soporte: las retenciones generales del
        // documento, que se agregan a cada ítem (ver BuildSupportDocumentRequest).
        private static List<DataicoTax>? BuildRetentions(IEnumerable<DocumentRetention> retentions, IEnumerable<DataicoTax>? extra = null)
        {
            var list = retentions.Select(r => new DataicoTax { tax_category = r.TaxCategory, tax_rate = r.Rate }).ToList();
            if (extra != null) list.AddRange(extra);
            return list.Count > 0 ? list : null;
        }

        // Retenciones generales del documento tal como las espera Dataico en factura y notas: una lista
        // al nivel del documento con categoría y tarifa, sin base ni valor — Dataico los calcula. Antes
        // se repartían entre los ítems con un valor calculado por nosotros (truncado), y Dataico
        // rechazaba por un centavo ("tax_rate x base_amount = tax_amount", "base_amount =
        // rondeo_dian(...)"). Con el valor en manos de Dataico no hay nada que descuadrar.
        private static List<DataicoTax>? BuildDocumentRetentions(Document document)
        {
            var list = document.GeneralRetentions
                .Where(r => !string.IsNullOrWhiteSpace(r.TaxCategory) && r.Rate > 0)
                .Select(r => new DataicoTax { tax_category = r.TaxCategory, tax_rate = r.Rate })
                .ToList();
            return list.Count > 0 ? list : null;
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
            string paymentMeansType,
            IReadOnlyDictionary<string, string>? identificationTypeOverrides = null)
        {
            var request = new DataicoInvoiceRequest
            {
                env = client.DataicoEnvironment,
                dataico_account_id = string.IsNullOrEmpty(client.DataicoAccountId) ? null : client.DataicoAccountId,
                number = document.Number,
                issue_date = document.IssueDate.ToString("dd/MM/yyyy"),
                payment_date = BuildPaymentDate(document),
                order_reference = document.PurchaseOrderReference,
                payment_means = ResolvePaymentMeans(paymentMeans, paymentMeansType),
                payment_means_type = paymentMeansType,
                numbering = new DataicoNumbering
                {
                    prefix = resolution.Prefix,
                    resolution_number = resolution.ResolutionNumber,
                    flexible = true
                },
                customer = DataicoMapper.ToDataicoParty(customer, identificationTypeOverrides),
                charges = BuildCharges(document)
            };

            // Retenciones generales al nivel del documento, solo categoría y tarifa (ver BuildDocumentRetentions).
            request.retentions = BuildDocumentRetentions(document);

            var itemsList = items as IReadOnlyList<DocumentItem> ?? items.ToList();

            foreach (var item in itemsList)
            {
                var dataicoItem = new DataicoInvoiceItem
                {
                    sku = item.Code,
                    quantity = item.Quantity,
                    description = item.Name,
                    price = item.UnitPrice,
                    discount_rate = item.DiscountRate,
                    retentions = BuildRetentions(item.Retentions)
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

        // Catálogo de Dataico (DEVOLUCION/ANULACION/OTROS para nota crédito; para nota débito nunca
        // se confirmó contra un ejemplo real) mapeado desde el código DIAN real de la nota
        // (TaxCatalogKind.CreditNoteReason/DebitNoteReason, columna DianCode) — antes se adivinaba
        // buscando "DEVOLU"/"ANULA" como substring del texto libre del portal, lo que fallaba para
        // cualquier motivo redactado distinto (ej. "Rebaja" o "Ajuste de precio" caían siempre en
        // OTROS aunque el usuario hubiera elegido otro motivo).
        private static string MapReasonToDataico(string? discrepancyResponseCode, IReadOnlyDictionary<string, string>? reasonDianCodes)
        {
            if (!string.IsNullOrWhiteSpace(discrepancyResponseCode) && reasonDianCodes != null
                && reasonDianCodes.TryGetValue(discrepancyResponseCode, out var reason))
                return reason;
            return "OTROS";
        }

        public static DataicoCreditNoteRequest BuildCreditNoteRequest(
            Document document, Resolution resolution, Client client, string originalInvoiceDataicoId, IEnumerable<DocumentItem> items,
            IReadOnlyDictionary<string, string>? reasonDianCodes = null)
        {
            var request = new DataicoCreditNoteRequest
            {
                env = client.DataicoEnvironment,
                dataico_account_id = string.IsNullOrEmpty(client.DataicoAccountId) ? null : client.DataicoAccountId,
                invoice_id = originalInvoiceDataicoId,
                issue_date = document.IssueDate.ToString("dd/MM/yyyy"),
                reason = MapReasonToDataico(document.DiscrepancyResponseCode, reasonDianCodes),
                number = document.Number,
                // Dataico no maneja numeración registrada para notas: a diferencia de las facturas,
                // acá solo se envía el prefijo (confirmado contra la integración de escritorio ya en
                // producción, que nunca incluye resolution_number para notas).
                numbering = new DataicoNumbering { prefix = string.IsNullOrWhiteSpace(document.Prefix) ? resolution.Prefix : document.Prefix, flexible = true },
                charges = BuildCharges(document)
            };

            request.retentions = BuildDocumentRetentions(document);

            var creditNoteItemsList = items as IReadOnlyList<DocumentItem> ?? items.ToList();

            foreach (var item in creditNoteItemsList)
            {
                var dataicoItem = new DataicoInvoiceItem
                {
                    sku = item.Code,
                    quantity = item.Quantity,
                    description = item.Name,
                    price = item.UnitPrice,
                    discount_rate = item.DiscountRate,
                    retentions = BuildRetentions(item.Retentions)
                };

                var ivaTax = BuildIvaTax(item.IvaTreatment, item.TaxRate);
                if (ivaTax != null) dataicoItem.taxes.Add(ivaTax);

                request.items.Add(dataicoItem);
            }

            return request;
        }

        public static DataicoDebitNoteRequest BuildDebitNoteRequest(
            Document document, Resolution resolution, Client client, string originalInvoiceDataicoId, IEnumerable<DocumentItem> items,
            IReadOnlyDictionary<string, string>? reasonDianCodes = null)
        {
            var request = new DataicoDebitNoteRequest
            {
                env = client.DataicoEnvironment,
                dataico_account_id = string.IsNullOrEmpty(client.DataicoAccountId) ? null : client.DataicoAccountId,
                invoice_id = originalInvoiceDataicoId,
                issue_date = document.IssueDate.ToString("dd/MM/yyyy"),
                reason = MapReasonToDataico(document.DiscrepancyResponseCode, reasonDianCodes),
                number = document.Number,
                // Ver comentario equivalente en BuildCreditNoteRequest: las notas no llevan
                // resolution_number en Dataico.
                numbering = new DataicoNumbering { prefix = string.IsNullOrWhiteSpace(document.Prefix) ? resolution.Prefix : document.Prefix, flexible = true },
                charges = BuildCharges(document)
            };

            request.retentions = BuildDocumentRetentions(document);

            var debitNoteItemsList = items as IReadOnlyList<DocumentItem> ?? items.ToList();

            foreach (var item in debitNoteItemsList)
            {
                var dataicoItem = new DataicoInvoiceItem
                {
                    sku = item.Code,
                    quantity = item.Quantity,
                    description = item.Name,
                    price = item.UnitPrice,
                    discount_rate = item.DiscountRate,
                    retentions = BuildRetentions(item.Retentions)
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
            Client client, string paymentMeans, string paymentMeansType, IEnumerable<DocumentItem> items,
            IReadOnlyDictionary<string, string>? identificationTypeOverrides = null)
        {
            var request = new DataicoSupportDocAdjustmentRequest
            {
                env = client.DataicoEnvironment,
                number = document.Number,
                issue_date = document.IssueDate.ToString("dd/MM/yyyy"),
                payment_date = BuildPaymentDate(document),
                payment_means = ResolvePaymentMeans(paymentMeans, paymentMeansType),
                payment_means_type = paymentMeansType,
                // Prefijo del documento (ya fijado al publicarlo): para la nota de ajuste es el de la numeración de ajustes
                // de la cuenta de Dataico, configurable por Client. Sin resolution_number, como las notas.
                numbering = new DataicoNumbering { prefix = string.IsNullOrWhiteSpace(document.Prefix) ? resolution.Prefix : document.Prefix, flexible = true },
                discrepancy_description = string.IsNullOrWhiteSpace(document.ReferenceConcept) ? "Ajuste de precio" : document.ReferenceConcept,
                source_document_cuds = originalDocument.Cufe ?? string.Empty,
                source_document_issue_date = originalDocument.IssueDate.ToString("dd/MM/yyyy"),
                source_document_number = originalDocument.Number,
                customer = DataicoMapper.ToDataicoParty(provider, identificationTypeOverrides)
            };

            // La nota de ajuste es el reflejo del documento soporte que ajusta (como la nota crédito con la
            // factura): IVA y retenciones por ítem, descuento/cargo generales y ReteICA, con la misma lógica.
            var itemsList = items as IReadOnlyList<DocumentItem> ?? items.ToList();
            var generalRetentions = BuildSupportGeneralRetentions(document);

            foreach (var item in itemsList)
            {
                var adjustmentItem = new DataicoInvoiceItem
                {
                    sku = item.Code,
                    quantity = item.Quantity,
                    description = item.Name,
                    price = item.UnitPrice,
                    discount_rate = item.DiscountRate,
                    retentions = BuildSupportItemRetentions(item, generalRetentions)
                };

                var ivaTax = BuildIvaTax(item.IvaTreatment, item.TaxRate);
                if (ivaTax != null)
                {
                    adjustmentItem.taxes.Add(ivaTax);
                }

                request.items.Add(adjustmentItem);
            }

            request.charges = BuildSupportCharges(document, itemsList);

            return request;
        }

        public static DataicoSupportDocumentRequest BuildSupportDocumentRequest(
            Document document,
            Customer provider,
            IEnumerable<DocumentItem> items,
            Resolution resolution,
            Client client,
            string paymentMeans,
            string paymentMeansType,
            IReadOnlyDictionary<string, string>? identificationTypeOverrides = null)
        {
            var request = new DataicoSupportDocumentRequest
            {
                env = client.DataicoEnvironment,
                number = document.Number,
                issue_date = document.IssueDate.ToString("dd/MM/yyyy"),
                payment_date = BuildPaymentDate(document),
                order_reference = document.PurchaseOrderReference,
                payment_means = ResolvePaymentMeans(paymentMeans, paymentMeansType),
                payment_means_type = paymentMeansType,
                numbering = new DataicoNumbering
                {
                    prefix = string.IsNullOrWhiteSpace(document.Prefix) ? resolution.Prefix : document.Prefix,
                    resolution_number = resolution.ResolutionNumber,
                    flexible = true
                },
                customer = DataicoMapper.ToDataicoParty(provider, identificationTypeOverrides)
            };

            var itemsList = items as IReadOnlyList<DocumentItem> ?? items.ToList();

            // Dataico y la app de escritorio anterior (FEL, en producción) reciben las retenciones del documento soporte
            // por ítem, solo con categoría y tarifa: las del propio ítem más las generales del documento,
            // que aplican a todas las líneas. Verificado contra Dataico en un envío real (octubre 2026):
            // este formato fue aceptado.
            //
            // RET_ICA es la excepción: Dataico la rechaza como retención en cualquier posición ("No se
            // puede usar RET_ICA en Documentos de Soporte") y, según Dataico, en el soporte la ReteICA va
            // como un charge a nivel de documento (ver BuildReteIcaCharge). Por eso se saca de las retenciones.
            var generalRetentions = BuildSupportGeneralRetentions(document);

            foreach (var item in itemsList)
            {
                var dataicoItem = new DataicoSupportDocItem
                {
                    sku = item.Code,
                    quantity = item.Quantity,
                    description = item.Name,
                    price = item.UnitPrice,
                    discount_rate = item.DiscountRate,
                    retentions = BuildSupportItemRetentions(item, generalRetentions)
                };

                var ivaTax = BuildIvaTax(item.IvaTreatment, item.TaxRate);
                if (ivaTax != null)
                {
                    dataicoItem.taxes.Add(ivaTax);
                }

                request.items.Add(dataicoItem);
            }

            request.charges = BuildSupportCharges(document, itemsList);

            return request;
        }

        // Retenciones y cargos del documento soporte y de su nota de ajuste: la nota es el reflejo del
        // documento, así que las dos usan exactamente la misma lógica.
        private static List<DataicoTax>? BuildSupportGeneralRetentions(Document document) =>
            BuildDocumentRetentions(document)?.Where(t => !IsReteIca(t.tax_category)).ToList();

        private static List<DataicoTax>? BuildSupportItemRetentions(DocumentItem item, IEnumerable<DataicoTax>? generalRetentions) =>
            BuildRetentions(item.Retentions.Where(r => !IsReteIca(r.TaxCategory)), generalRetentions);

        // Descuento y cargo generales más la ReteICA (que Dataico no admite como retención en el soporte).
        private static List<DataicoCharge>? BuildSupportCharges(Document document, IReadOnlyList<DocumentItem> items)
        {
            var charges = BuildCharges(document);
            var reteIca = BuildReteIcaCharge(document, items);
            if (reteIca != null)
            {
                (charges ??= new List<DataicoCharge>()).Add(reteIca);
            }
            return charges;
        }

        private static bool IsReteIca(string? taxCategory) =>
            string.Equals(taxCategory, "RET_ICA", StringComparison.OrdinalIgnoreCase);

        // La ReteICA del documento soporte viaja como un descuento a nivel de documento (charge con
        // discount=true y reason RETEICA). La ReteICA general del documento se calcula UNA vez sobre el
        // subtotal del documento (suma de las bases de las líneas: cantidad x precio menos el descuento del
        // ítem), igual que el formulario del portal; la que se eligió en un ítem concreto se calcula sobre
        // la base de esa línea. Se redondea según la DIAN (DianRounding).
        // SUPUESTO por confirmar con Dataico: que base_amount sea el VALOR en pesos de la retención —los
        // charges no tienen campo de tarifa— y no el subtotal sobre el que se calcula.
        private static DataicoCharge? BuildReteIcaCharge(Document document, IReadOnlyList<DocumentItem> items)
        {
            decimal LineBase(DocumentItem i) => DianRounding.Round2(i.Quantity * i.UnitPrice * (1 - i.DiscountRate / 100));

            var total = 0m;

            var subtotal = items.Sum(LineBase);
            foreach (var general in document.GeneralRetentions.Where(r => IsReteIca(r.TaxCategory) && r.Rate > 0))
            {
                total += DianRounding.Round2(subtotal * general.Rate / 100);
            }

            foreach (var item in items)
            {
                foreach (var retention in item.Retentions.Where(r => IsReteIca(r.TaxCategory) && r.Rate > 0))
                {
                    total += DianRounding.Round2(LineBase(item) * retention.Rate / 100);
                }
            }

            return total > 0
                ? new DataicoCharge { reason = "RETEICA", base_amount = total, discount = true }
                : null;
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
