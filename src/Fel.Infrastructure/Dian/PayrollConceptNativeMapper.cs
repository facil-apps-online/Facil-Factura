using System;
using System.Collections.Generic;
using System.Linq;
using Fel.Core.Entities;
using Fel.Core.Models;

namespace Fel.Infrastructure.Dian
{
    // Convierte la lista de conceptos de nómina (código del catálogo PayrollConceptTypes + valores) al modelo estructurado del XML nativo de la
    // DIAN (PayrollEarningsData / PayrollDeductionsData). El catálogo dice a qué elemento y a qué atributo del XML va cada concepto; este mapper
    // conoce el esquema fijo de la DIAN (NominaIndividualElectronica) y ubica el valor en la propiedad del modelo que corresponde a ese elemento.
    public static class PayrollConceptNativeMapper
    {
        public sealed class Result
        {
            public PayrollEarningsData Earnings { get; } = new PayrollEarningsData();
            public PayrollDeductionsData Deductions { get; } = new PayrollDeductionsData();
            public decimal EarningsTotal { get; set; }
            public decimal DeductionsTotal { get; set; }
            public List<string> Errors { get; } = new List<string>();
        }

        public static Result Build(
            IEnumerable<PayrollConceptLine>? accruals,
            IEnumerable<PayrollConceptLine>? deductions,
            IReadOnlyCollection<PayrollConceptType> types,
            DateTime periodStart,
            DateTime periodEnd)
        {
            var result = new Result();

            foreach (var line in accruals ?? Enumerable.Empty<PayrollConceptLine>())
            {
                var type = Find(types, PayrollConceptSection.Accrual, line.Code);
                if (type == null) { result.Errors.Add($"El devengo '{line.Code}' no existe en el catálogo de conceptos de nómina."); continue; }
                if (!ApplyAccrual(result, type, line, periodStart, periodEnd))
                    result.Errors.Add($"El devengo '{line.Code}' va en el elemento '{type.DianElement}' del XML, que este emisor todavía no soporta.");
            }

            foreach (var line in deductions ?? Enumerable.Empty<PayrollConceptLine>())
            {
                var type = Find(types, PayrollConceptSection.Deduction, line.Code);
                if (type == null) { result.Errors.Add($"La deducción '{line.Code}' no existe en el catálogo de conceptos de nómina."); continue; }
                if (!ApplyDeduction(result, type, line))
                    result.Errors.Add($"La deducción '{line.Code}' va en el elemento '{type.DianElement}' del XML, que este emisor todavía no soporta.");
            }

            return result;
        }

        private static PayrollConceptType? Find(IReadOnlyCollection<PayrollConceptType> types, PayrollConceptSection section, string code) =>
            types.FirstOrDefault(t => t.IsActive && t.Section == section && string.Equals(t.DataicoCode, code?.Trim(), StringComparison.OrdinalIgnoreCase));

        private static bool Is(string? value, string expected) => string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);

        // Código de tipo de incapacidad del XML (1 común, 2 profesional, 3 laboral).
        private static string IncapacityType(string? type) => type?.Trim().ToUpperInvariant() switch
        {
            "PROFESIONAL" => "2",
            "LABORAL" => "3",
            _ => "1"
        };

        private static bool ApplyAccrual(Result result, PayrollConceptType type, PayrollConceptLine l, DateTime periodStart, DateTime periodEnd)
        {
            var e = result.Earnings;
            var amount = l.Amount ?? 0m;
            var nonSalary = l.AmountNs ?? 0m;
            var days = (int)(l.Days ?? 0m);
            var start = l.InitialDate ?? periodStart;
            var end = l.FinalDate ?? periodEnd;

            PayrollTimeRange Range() => new PayrollTimeRange { Start = start, End = end, Quantity = l.Hours ?? 0m, Percentage = l.Percentage ?? 0m, Amount = amount };
            PayrollDateRangeAmount DateRange() => new PayrollDateRangeAmount { Start = start, End = end, Quantity = days, Amount = amount };

            switch (type.DianElement)
            {
                case "Basico":
                    e.WorkedDays += days; e.BasicSalaryPaid += amount; break;
                case "Transporte":
                    if (Is(type.DianAmountAttribute, "AuxilioTransporte")) e.TransportAllowance += amount;
                    else { e.PerDiemTaxable += amount; e.PerDiemNonTaxable += nonSalary; }
                    break;
                case "HED": e.ExtraDiurnas.Add(Range()); break;
                case "HEN": e.ExtraNocturnas.Add(Range()); break;
                case "HRN": e.RecargoNocturno.Add(Range()); break;
                case "HEDDF": e.ExtraDiurnaFestiva.Add(Range()); break;
                case "HRDDF": e.RecargoDiurnoFestivo.Add(Range()); break;
                case "HENDF": e.ExtraNocturnaFestiva.Add(Range()); break;
                case "HRNDF": e.RecargoNocturnoFestivo.Add(Range()); break;
                case "VacacionesComunes": e.VacacionesComunes.Add(DateRange()); break;
                case "VacacionesCompensadas":
                    e.VacacionesCompensadasCantidad += days; e.VacacionesCompensadasPago += amount; break;
                case "Primas":
                    e.PrimasCantidad += days; e.PrimasTaxable += amount; e.PrimasNonTaxable += nonSalary; break;
                case "Cesantias":
                    e.CesantiasPago += amount; e.CesantiasPorcentaje = l.Percentage ?? e.CesantiasPorcentaje; e.CesantiasPagoIntereses += l.CesantiasInterest ?? 0m; break;
                case "Incapacidad":
                    e.Incapacidades.Add(new PayrollIncapacity { Start = start, End = end, Quantity = days, TypeCode = IncapacityType(l.MedicalLeaveType), Amount = amount });
                    break;
                case "LicenciaMP": e.LicenciaMaternidadPaternidad.Add(DateRange()); break;
                case "LicenciaR": e.LicenciaRemunerada.Add(DateRange()); break;
                case "LicenciaNR": e.LicenciaNoRemunerada.Add((start, end, days)); break;
                case "HuelgaLegal": e.HuelgasLegales.Add((start, end, days)); break;
                case "Bonificacion": e.BonificacionesTaxable += amount; e.BonificacionesNonTaxable += nonSalary; break;
                case "Auxilio": e.AuxiliosTaxable += amount; e.AuxiliosNonTaxable += nonSalary; break;
                case "OtroConcepto":
                    e.OtrosConceptos.Add(new PayrollOtherConcept { Description = l.Description ?? string.Empty, TaxableAmount = amount, NonTaxableAmount = nonSalary });
                    break;
                case "Compensacion":
                    e.CompensacionOrdinaria += l.OrdinaryCompensation ?? 0m; e.CompensacionExtraordinaria += l.ExtraordinaryCompensation ?? 0m; break;
                case "BonoEPCTV":
                    if (Is(type.DianAmountAttribute, "PagoAlimentacionS")) { e.BonoEPCTVAlimentacionTaxable += amount; e.BonoEPCTVAlimentacionNonTaxable += nonSalary; }
                    else { e.BonoEPCTVTaxable += amount; e.BonoEPCTVNonTaxable += nonSalary; }
                    break;
                // Elementos de un solo valor: la suma de lo salarial y lo no salarial.
                case "Comision": e.Comisiones += amount + nonSalary; break;
                case "PagoTercero": e.PagosTerceros += amount + nonSalary; break;
                case "Anticipo": e.Anticipos += amount + nonSalary; break;
                case "Dotacion": e.Dotacion += amount + nonSalary; break;
                case "ApoyoSost": e.ApoyoSostenimiento += amount + nonSalary; break;
                case "Teletrabajo": e.Teletrabajo += amount + nonSalary; break;
                case "BonifRetiro": e.BonificacionRetiro += amount + nonSalary; break;
                case "Indemnizacion": e.Indemnizacion += amount + nonSalary; break;
                case "Reintegro": e.Reintegro += amount + nonSalary; break;
                default: return false;
            }

            result.EarningsTotal += amount + nonSalary
                + (l.CesantiasInterest ?? 0m)
                + (l.OrdinaryCompensation ?? 0m) + (l.ExtraordinaryCompensation ?? 0m);
            return true;
        }

        private static bool ApplyDeduction(Result result, PayrollConceptType type, PayrollConceptLine l)
        {
            var d = result.Deductions;
            var amount = l.Amount ?? 0m;
            var percentage = l.Percentage ?? 0m;

            switch (type.DianElement)
            {
                case "Salud": d.SaludPorcentaje = percentage; d.SaludDeduccion += amount; break;
                case "FondoPension": d.FondoPensionPorcentaje = percentage; d.FondoPensionDeduccion += amount; break;
                case "FondoSP":
                    if (Is(type.DianAmountAttribute, "DeduccionSub")) { d.FondoSubsistenciaPorcentaje = percentage; d.FondoSubsistenciaDeduccion += amount; }
                    else { d.FondoSPPorcentaje = percentage; d.FondoSPDeduccion += amount; }
                    break;
                case "Sindicato": d.Sindicatos += amount; break;
                case "Sancion":
                    d.SancionesPublicas += l.PublicSanction ?? 0m; d.SancionesPrivadas += l.PrivateSanction ?? 0m;
                    amount = (l.PublicSanction ?? 0m) + (l.PrivateSanction ?? 0m);
                    break;
                case "Libranza": d.Libranzas += amount; break;
                case "PagoTercero": d.PagosTerceros += amount; break;
                case "Anticipo": d.Anticipos += amount; break;
                case "OtraDeduccion": d.OtrasDeducciones += amount; break;
                case "PensionVoluntaria": d.PensionVoluntaria += amount; break;
                case "RetencionFuente": d.RetencionFuente += amount; break;
                case "AFC": d.AFC += amount; break;
                case "Cooperativa": d.Cooperativa += amount; break;
                case "EmbargoFiscal": d.EmbargoFiscal += amount; break;
                case "PlanComplementarios": d.PlanComplementarios += amount; break;
                case "Educacion": d.Educacion += amount; break;
                case "Reintegro": d.Reintegro += amount; break;
                case "Deuda": d.Deuda += amount; break;
                default: return false;
            }

            result.DeductionsTotal += amount;
            return true;
        }
    }
}
