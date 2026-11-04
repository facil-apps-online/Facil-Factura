using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Fel.Core.Models
{
    // Datos del sector salud de una factura (Documento Técnico 2 del Ministerio de Salud, julio de 2026): la extensión
    // "Sector Salud" del XML, el período facturado y los recaudos (cac:PrepaidPayment).
    // Se guarda como JSON en Document.SectorExtensionData bajo la clave "health", igual que el contrato AIU bajo "aiu" y la caja del POS bajo "pos".
    public class HealthSectorData
    {
        // Tipo de operación de la factura (CustomizationID): SS-CUFE, SS-CUDE, SS-POS, SS-SNum, SS-Recaudo, SS-Reporte o SS-SinAporte.
        public string OperationType { get; set; } = "SS-CUFE";

        private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        private class Envelope { public HealthSectorData? Health { get; set; } }

        public string ToExtensionData() => JsonSerializer.Serialize(new Envelope { Health = this }, Options);

        // null si el documento no trae datos de salud (o el JSON no es válido).
        public static HealthSectorData? TryRead(string? sectorExtensionData)
        {
            if (string.IsNullOrWhiteSpace(sectorExtensionData)) return null;
            try { return JsonSerializer.Deserialize<Envelope>(sectorExtensionData, Options)?.Health; }
            catch (JsonException) { return null; }
        }

        // CODIGO_PRESTADOR: código de habilitación del prestador (10 dígitos en el XML; en el RIPS lleva además 2 de la sede).
        public string ProviderCode { get; set; } = string.Empty;
        // MODALIDAD_PAGO y COBERTURA_PLAN_BENEFICIOS: códigos de las tablas modalidadPago y coberturaPlan de SISPRO.
        public string PaymentModality { get; set; } = string.Empty;
        public string Coverage { get; set; } = string.Empty;
        // NUMERO_CONTRATO (código CUCON) o NUMERO_POLIZA: se informa uno solo.
        public string? ContractNumber { get; set; }
        public string? PolicyNumber { get; set; }
        // FACTURA_SIN_CONTRATO: tabla facturaSinContrato; solo cuando no hay contrato.
        public string? NoContractReason { get; set; }

        // Período de prestación de los servicios (cac:InvoicePeriod): obligatorio en facturas, notas crédito y notas débito de salud.
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }

        // Recaudos del usuario (copago, cuota moderadora, pagos compartidos, anticipo): un cac:PrepaidPayment por concepto.
        public List<HealthPrepaid> Prepaid { get; set; } = new List<HealthPrepaid>();
    }

    public class HealthPrepaid
    {
        public string ConceptCode { get; set; } = string.Empty; // tabla conceptoRecaudo: 01..04
        public decimal Amount { get; set; }
        public DateTime ReceivedDate { get; set; }
    }

    // Tablas de referencia de SISPRO que usa el XML de salud (consultadas en web.sispro.gov.co, noviembre de 2026).
    public static class HealthCatalogs
    {
        // Operaciones de la factura (CustomizationID). Las notas crédito y débito usan los tipos de la DIAN, no estos.
        public static readonly string[] OperationTypes = { "SS-CUFE", "SS-CUDE", "SS-POS", "SS-SNum", "SS-Recaudo", "SS-Reporte", "SS-SinAporte" };
        // Las que llevan la extensión del sector (SS-Recaudo no la lleva; SS-Reporte y SS-SinAporte solo si reportan valores).
        public static readonly string[] OperationsWithExtension = { "SS-CUFE", "SS-CUDE", "SS-POS", "SS-SNum" };

        // modalidadPago (la 05 "Otra modalidad" está deshabilitada).
        public static readonly string[] PaymentModalities = { "01", "02", "03", "04" };
        // coberturaPlan: 01 a 15 según SISPRO y el diccionario del Ministerio (SISPRO informa 17 filas; falta confirmar la 16 y la 17).
        public static readonly string[] Coverages =
            { "01", "02", "03", "04", "05", "06", "07", "08", "09", "10", "11", "12", "13", "14", "15" };
        // Coberturas que exigen póliza en lugar de contrato: SOAT y otras pólizas / planes voluntarios.
        public static readonly string[] CoveragesWithPolicy = { "04", "12" };
        // conceptoRecaudo: 01 copago, 02 cuota moderadora, 03 pagos compartidos en planes voluntarios, 04 anticipo.
        public static readonly string[] CollectionConcepts = { "01", "02", "03", "04" };
        // facturaSinContrato.
        public static readonly string[] NoContractReasons = { "01", "02", "03", "04", "05", "06", "07" };

        // Reglas del bloque de salud de una factura que lleva la extensión. Devuelve los errores en español (vacío si es válido).
        public static List<string> Validate(string operationType, HealthSectorData? health, decimal invoiceTotal)
        {
            var errors = new List<string>();
            if (!OperationsWithExtension.Contains(operationType)) return errors;
            if (health == null)
            {
                errors.Add($"El tipo de operación {operationType} exige el bloque de salud (health).");
                return errors;
            }

            if (health.ProviderCode.Length != 10 || !health.ProviderCode.All(char.IsDigit))
                errors.Add("provider_code debe ser el código de habilitación del prestador, de 10 dígitos.");
            if (!PaymentModalities.Contains(health.PaymentModality))
                errors.Add($"payment_modality '{health.PaymentModality}' no es válida (01 a 04, tabla modalidadPago).");
            if (!Coverages.Contains(health.Coverage))
                errors.Add($"coverage '{health.Coverage}' no es válida (tabla coberturaPlan).");

            var hasContract = !string.IsNullOrWhiteSpace(health.ContractNumber);
            var hasPolicy = !string.IsNullOrWhiteSpace(health.PolicyNumber);
            if (hasContract && hasPolicy)
                errors.Add("Se informa contract_number o policy_number, no los dos.");
            if (hasPolicy && !CoveragesWithPolicy.Contains(health.Coverage))
                errors.Add("policy_number solo aplica a las coberturas 04 (SOAT) y 12 (otras pólizas en salud).");
            if (!hasContract && !hasPolicy && string.IsNullOrWhiteSpace(health.NoContractReason))
                errors.Add("Sin contract_number ni policy_number se debe informar no_contract_reason (tabla facturaSinContrato).");
            if (!string.IsNullOrWhiteSpace(health.NoContractReason))
            {
                if (hasContract) errors.Add("no_contract_reason va vacío cuando existe contract_number.");
                else if (!NoContractReasons.Contains(health.NoContractReason))
                    errors.Add($"no_contract_reason '{health.NoContractReason}' no es válido (01 a 07, tabla facturaSinContrato).");
            }

            if (health.PeriodStart == default || health.PeriodEnd == default)
                errors.Add("period_start y period_end son obligatorios.");
            else if (health.PeriodEnd < health.PeriodStart)
                errors.Add("period_end no puede ser anterior a period_start.");

            if (health.Prepaid.Count > 4) errors.Add("Máximo 4 recaudos, uno por concepto.");
            foreach (var group in health.Prepaid.GroupBy(p => p.ConceptCode))
            {
                if (!CollectionConcepts.Contains(group.Key)) errors.Add($"Concepto de recaudo '{group.Key}' no válido (01 a 04, tabla conceptoRecaudo).");
                else if (group.Count() > 1) errors.Add($"El concepto de recaudo {group.Key} se informa una sola vez (suma los valores).");
            }
            if (health.Prepaid.Any(p => p.Amount <= 0)) errors.Add("Cada recaudo debe ser mayor que cero.");
            if (health.Prepaid.Sum(p => p.Amount) > invoiceTotal)
                errors.Add("La suma de los recaudos no puede superar el valor total de la factura.");
            return errors;
        }
    }
}
