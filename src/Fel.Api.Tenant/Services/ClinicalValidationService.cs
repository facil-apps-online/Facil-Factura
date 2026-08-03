using Fel.Api.Tenant.DTOs;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Tenant.Services
{
    public class ClinicalValidationService : IClinicalValidationService
    {
        private readonly FelDbContext _context;

        public ClinicalValidationService(FelDbContext context)
        {
            _context = context;
        }

        public async Task<List<string>> ValidateRipsAsync(RipsEmitRequest request)
        {
            var errors = new List<string>();
            var patientAgeDays = (DateTime.Now - request.Patient.BirthDate).TotalDays;
            var patientAgeYears = (DateTime.Now.Year - request.Patient.BirthDate.Year);
            if (request.Patient.BirthDate.Date > DateTime.Now.AddYears(-patientAgeYears)) patientAgeYears--;

            var sex = request.Patient.BiologicalSex.ToUpper(); // M or F

            // Validate Consultations (Diagnoses)
            foreach (var cons in request.Consultations)
            {
                var rule = await _context.RipsCie10Rules.FirstOrDefaultAsync(r => r.Code == cons.MainDiagnosisCode);
                if (rule != null)
                {
                    if (rule.AllowedGender != "A" && rule.AllowedGender != sex)
                    {
                        errors.Add($"Incongruencia clínica: El diagnóstico {rule.Code} ({rule.Description}) exige sexo {rule.AllowedGender}, pero el paciente es {sex}.");
                    }

                    if (patientAgeYears < rule.MinAgeYears || patientAgeYears > rule.MaxAgeYears)
                    {
                        errors.Add($"Incongruencia clínica: El diagnóstico {rule.Code} es válido para edades entre {rule.MinAgeYears} y {rule.MaxAgeYears} años. El paciente tiene {patientAgeYears}.");
                    }
                }
            }

            // Validate Procedures (CUPS)
            foreach (var proc in request.Procedures)
            {
                var rule = await _context.RipsCupsRules.FirstOrDefaultAsync(r => r.Code == proc.ProcedureCode);
                if (rule != null)
                {
                    if (rule.AllowedGender != "A" && rule.AllowedGender != sex)
                    {
                        errors.Add($"Incongruencia clínica: El procedimiento CUPS {rule.Code} ({rule.Name}) exige sexo {rule.AllowedGender}, pero el paciente es {sex}.");
                    }

                    if (patientAgeDays < rule.MinAgeDays || patientAgeDays > rule.MaxAgeDays)
                    {
                        errors.Add($"Incongruencia clínica: El procedimiento CUPS {rule.Code} es válido para edades entre {rule.MinAgeDays} y {rule.MaxAgeDays} días. El paciente tiene {Math.Floor(patientAgeDays)} días.");
                    }
                }
            }

            return errors;
        }
    }
}
