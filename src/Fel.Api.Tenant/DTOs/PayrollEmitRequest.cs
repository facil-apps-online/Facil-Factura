using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Fel.Api.Tenant.DTOs
{
    /// <summary>
    /// Estructura para emitir Nómina Electrónica (DIAN Código 102)
    /// </summary>
    public class PayrollEmitRequest
    {
        /// <summary>
        /// Consecutivo de la Nómina (Ej: NOM-123)
        /// </summary>
        /// <example>NOM-102</example>
        [Required]
        public string Number { get; set; } = string.Empty;

        /// <summary>
        /// Periodo de pago de la nómina
        /// </summary>
        [Required]
        public PayrollPeriodDto Period { get; set; } = new PayrollPeriodDto();

        /// <summary>
        /// Datos del empleado
        /// </summary>
        [Required]
        public EmployeeDto Employee { get; set; } = new EmployeeDto();

        /// <summary>
        /// Conceptos devengados (Sueldo, Horas Extras, Comisiones)
        /// </summary>
        [Required]
        public List<PayrollAccruedDto> Accrueds { get; set; } = new List<PayrollAccruedDto>();

        /// <summary>
        /// Conceptos deducidos (Salud, Pensión, Libranzas)
        /// </summary>
        [Required]
        public List<PayrollDeductionDto> Deductions { get; set; } = new List<PayrollDeductionDto>();

        /// <summary>
        /// Total a pagar neto al empleado
        /// </summary>
        /// <example>1910000.00</example>
        [Required]
        public decimal NetTotal { get; set; }
    }

    public class PayrollPeriodDto
    {
        /// <example>2023-10-01</example>
        public DateTime StartDate { get; set; }
        /// <example>2023-10-31</example>
        public DateTime EndDate { get; set; }
    }

    public class EmployeeDto
    {
        /// <example>1020304050</example>
        public string Identification { get; set; } = string.Empty;
        /// <example>Juan Pérez</example>
        public string FullName { get; set; } = string.Empty;
        /// <example>2000000.00</example>
        public decimal BaseSalary { get; set; }
    }

    public class PayrollAccruedDto
    {
        /// <example>Sueldo Básico</example>
        public string Concept { get; set; } = string.Empty;
        /// <example>2000000.00</example>
        public decimal Amount { get; set; }
    }

    public class PayrollDeductionDto
    {
        /// <example>Aporte Salud</example>
        public string Concept { get; set; } = string.Empty;
        /// <example>80000.00</example>
        public decimal Amount { get; set; }
    }
}
