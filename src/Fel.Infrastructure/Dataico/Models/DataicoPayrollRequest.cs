using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Fel.Infrastructure.Dataico.Models
{
    public class DataicoEmployeeAddress
    {
        public string? city { get; set; }
        public string? line { get; set; }
        public string? department { get; set; }
    }

    public class DataicoPayrollEmployee
    {
        public string code { get; set; } = string.Empty;

        [JsonPropertyName("payment-means")]
        public string payment_means { get; set; } = string.Empty; // catálogo Dataico, ej. EFECTIVO

        [JsonPropertyName("worker-type")]
        public string worker_type { get; set; } = string.Empty; // catálogo Dataico (tipo de trabajador DIAN)

        [JsonPropertyName("sub-code")]
        public string sub_code { get; set; } = "NO_APLICA";

        [JsonPropertyName("start-date")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? start_date { get; set; }

        [JsonPropertyName("fire-date")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? fire_date { get; set; }

        [JsonPropertyName("high-risk")]
        public bool high_risk { get; set; }

        [JsonPropertyName("integral-salary")]
        public bool integral_salary { get; set; }

        [JsonPropertyName("contract-type")]
        public string contract_type { get; set; } = string.Empty; // catálogo Dataico, ej. TERMINO_INDEFINIDO

        [JsonPropertyName("identification-type")]
        public string identification_type { get; set; } = string.Empty; // ej. CEDULA_DE_CIUDADANIA

        public string identification { get; set; } = string.Empty;

        [JsonPropertyName("first-name")]
        public string first_name { get; set; } = string.Empty;

        [JsonPropertyName("other-names")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? other_names { get; set; }

        [JsonPropertyName("last-name")]
        public string last_name { get; set; } = string.Empty;

        [JsonPropertyName("second-last-name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? second_last_name { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? bank { get; set; }

        [JsonPropertyName("account-type-kw")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? account_type_kw { get; set; } // AHORROS | CORRIENTE

        [JsonPropertyName("account-number")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? account_number { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DataicoEmployeeAddress? address { get; set; }
    }

    // La captura manual simplificada solo llena code/description/amount (OTRO_CONCEPTO /
    // OTRA_DEDUCCION, sin cálculos automáticos); el importador de Excel (formato estándar de
    // carga masiva de Dataico) puede llenar cualquiera de estos campos según el código de
    // concepto (confirmado contra el Postman de Dataico: POST /payroll-api/v2/payroll-entries).
    public class DataicoPayrollConcept
    {
        public string code { get; set; } = "OTRO_CONCEPTO";

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? description { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? amount { get; set; }

        [JsonPropertyName("amount-ns")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? amount_ns { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? days { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? percentage { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? hours { get; set; }

        [JsonPropertyName("initial-date")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? initial_date { get; set; }

        [JsonPropertyName("final-date")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? final_date { get; set; }

        [JsonPropertyName("medical-leave-type")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? medical_leave_type { get; set; }

        [JsonPropertyName("cesantias-interest")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? cesantias_interest { get; set; }

        [JsonPropertyName("ordinary-compensation")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? ordinary_compensation { get; set; }

        [JsonPropertyName("extraordinary-compensation")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? extraordinary_compensation { get; set; }
    }

    public class DataicoPayrollSoftware
    {
        public string? pin { get; set; }

        [JsonPropertyName("test-set-id")]
        public string? test_set_id { get; set; }

        [JsonPropertyName("dian-id")]
        public string? dian_id { get; set; }
    }

    // Cuerpo real esperado por Dataico: POST /direct/payroll-api/v2/payroll-entries (objeto plano, sin envelope)
    public class DataicoPayrollRequest
    {
        public string env { get; set; } = "PRUEBAS";
        public string prefix { get; set; } = string.Empty;
        public long number { get; set; }
        public int salary { get; set; }
        public string periodicity { get; set; } = "MENSUAL";

        [JsonPropertyName("initial-settlement-date")]
        public string initial_settlement_date { get; set; } = string.Empty;

        [JsonPropertyName("final-settlement-date")]
        public string final_settlement_date { get; set; } = string.Empty;

        [JsonPropertyName("issue-date")]
        public string issue_date { get; set; } = string.Empty;

        [JsonPropertyName("payment-date")]
        public string payment_date { get; set; } = string.Empty;

        public DataicoPayrollEmployee employee { get; set; } = new();
        public List<DataicoPayrollConcept> accruals { get; set; } = new();
        public List<DataicoPayrollConcept> deductions { get; set; } = new();
        public DataicoPayrollSoftware software { get; set; } = new();
    }
}
