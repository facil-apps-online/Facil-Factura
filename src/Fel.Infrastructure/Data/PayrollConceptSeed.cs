using System;
using System.Collections.Generic;
using Fel.Core.Entities;

namespace Fel.Infrastructure.Data
{
    // Datos iniciales de PayrollConceptTypes. Los campos que acepta cada código en Dataico son los que ya tenía el mapper (ConceptFields) y la
    // equivalencia DIAN sale del XSD NominaIndividualElectronicaXSDV1.0.6 de la caja de herramientas (docs/dian-nomina). Después de la primera
    // carga el catálogo lo administra Superadmin.
    public static class PayrollConceptSeed
    {
        public static IReadOnlyList<PayrollConceptType> All { get; } = Build();

        private static List<PayrollConceptType> Build()
        {
            var rows = new List<PayrollConceptType>();
            var n = 0;

            void Add(PayrollConceptSection section, string code, string name, string dianElement, PayrollDianShape shape,
                string? amount = null, string? nonSalary = null, string? quantity = null, string? percentage = null,
                PayrollQuantityKind kind = PayrollQuantityKind.None, bool withPercentage = false, bool allowsAmount = true, bool allowsNonSalary = false,
                bool description = false, PayrollSpecialInput special = PayrollSpecialInput.None)
            {
                n++;
                rows.Add(new PayrollConceptType
                {
                    Id = Guid.Parse($"b0000000-0000-0000-0000-{n:D12}"),
                    Section = section,
                    DataicoCode = code,
                    Name = name,
                    DianElement = dianElement,
                    DianShape = shape,
                    DianAmountAttribute = amount,
                    DianNonSalaryAttribute = nonSalary,
                    DianQuantityAttribute = quantity,
                    DianPercentageAttribute = percentage,
                    QuantityKind = kind,
                    HasPercentage = withPercentage,
                    AllowsAmount = allowsAmount,
                    AllowsNonSalary = allowsNonSalary,
                    RequiresDescription = description,
                    SpecialInput = special,
                    SortOrder = n,
                    IsActive = true
                });
            }

            const PayrollConceptSection A = PayrollConceptSection.Accrual;
            const PayrollConceptSection D = PayrollConceptSection.Deduction;
            const PayrollDianShape Attrs = PayrollDianShape.Attributes;
            const PayrollDianShape List = PayrollDianShape.List;
            const PayrollDianShape Value = PayrollDianShape.Value;
            const PayrollQuantityKind Days = PayrollQuantityKind.Days;
            const PayrollQuantityKind Hours = PayrollQuantityKind.Hours;
            const PayrollQuantityKind Pct = PayrollQuantityKind.Percentage;

            // ---- Devengos ----
            Add(A, "BASICO", "Salario básico", "Basico", Attrs, amount: "SueldoTrabajado", quantity: "DiasTrabajados", kind: Days);
            Add(A, "AUXILIO_DE_TRANSPORTE", "Auxilio de transporte", "Transporte", Attrs, amount: "AuxilioTransporte");
            Add(A, "HORA_EXTRA_DIURNA", "Hora extra diurna", "HED", List, amount: "Pago", quantity: "Cantidad", percentage: "Porcentaje", kind: Hours, withPercentage: true);
            Add(A, "HORA_EXTRA_NOCTURNA", "Hora extra nocturna", "HEN", List, amount: "Pago", quantity: "Cantidad", percentage: "Porcentaje", kind: Hours, withPercentage: true);
            Add(A, "HORA_EXTRA_DIURNA_DF", "Hora extra diurna dominical/festiva", "HEDDF", List, amount: "Pago", quantity: "Cantidad", percentage: "Porcentaje", kind: Hours, withPercentage: true);
            Add(A, "HORA_EXTRA_NOCTURNA_DF", "Hora extra nocturna dominical/festiva", "HENDF", List, amount: "Pago", quantity: "Cantidad", percentage: "Porcentaje", kind: Hours, withPercentage: true);
            Add(A, "HORA_RECARGO_NOCTURNO", "Recargo nocturno", "HRN", List, amount: "Pago", quantity: "Cantidad", percentage: "Porcentaje", kind: Hours, withPercentage: true);
            Add(A, "HORA_RECARGO_DIURNA_DF", "Recargo diurno dominical/festivo", "HRDDF", List, amount: "Pago", quantity: "Cantidad", percentage: "Porcentaje", kind: Hours, withPercentage: true);
            Add(A, "HORA_RECARGO_NOCTURNO_DF", "Recargo nocturno dominical/festivo", "HRNDF", List, amount: "Pago", quantity: "Cantidad", percentage: "Porcentaje", kind: Hours, withPercentage: true);
            Add(A, "VACACION", "Vacaciones disfrutadas", "VacacionesComunes", List, amount: "Pago", quantity: "Cantidad", kind: Days);
            Add(A, "VACACION_COMPENSADA", "Vacaciones compensadas", "VacacionesCompensadas", Attrs, amount: "Pago", quantity: "Cantidad", kind: Days);
            Add(A, "PRIMA", "Prima de servicios", "Primas", Attrs, amount: "Pago", nonSalary: "PagoNS", quantity: "Cantidad", kind: Days, allowsNonSalary: true);
            Add(A, "CESANTIAS", "Cesantías", "Cesantias", Attrs, amount: "Pago", percentage: "Porcentaje", kind: Pct, allowsNonSalary: true, special: PayrollSpecialInput.CesantiasInterest);
            Add(A, "INCAPACIDAD", "Incapacidad", "Incapacidad", List, amount: "Pago", quantity: "Cantidad", kind: Days, special: PayrollSpecialInput.MedicalLeave);
            Add(A, "LICENCIA_PATERNIDAD", "Licencia de maternidad o paternidad", "LicenciaMP", List, amount: "Pago", quantity: "Cantidad", kind: Days, special: PayrollSpecialInput.Dates);
            Add(A, "LICENCIA_REMUNERADA", "Licencia remunerada", "LicenciaR", List, amount: "Pago", quantity: "Cantidad", kind: Days);
            Add(A, "LICENCIA_NO_REMUNERADA", "Licencia no remunerada", "LicenciaNR", List, quantity: "Cantidad", kind: Days, allowsAmount: false);
            Add(A, "HUELGA_LEGAL", "Huelga legal", "HuelgaLegal", List, quantity: "Cantidad", kind: Days, allowsAmount: false, special: PayrollSpecialInput.Dates);
            Add(A, "BONIFICACION", "Bonificación", "Bonificacion", Attrs, amount: "BonificacionS", nonSalary: "BonificacionNS", allowsNonSalary: true);
            Add(A, "AUXILIO", "Auxilio", "Auxilio", Attrs, amount: "AuxilioS", nonSalary: "AuxilioNS", allowsNonSalary: true);
            Add(A, "VIATICO", "Viático", "Transporte", Attrs, amount: "ViaticoManuAlojS", nonSalary: "ViaticoManuAlojNS", allowsNonSalary: true);
            Add(A, "COMISION", "Comisión", "Comision", Value);
            Add(A, "BONO_EPCTV", "Bono electrónico o de papel", "BonoEPCTV", Attrs, amount: "PagoS", nonSalary: "PagoNS", allowsNonSalary: true);
            Add(A, "BONO_EPCTV_ALIMENTACION", "Bono de alimentación", "BonoEPCTV", Attrs, amount: "PagoAlimentacionS", nonSalary: "PagoAlimentacionNS", allowsNonSalary: true);
            Add(A, "DOTACION", "Dotación", "Dotacion", Value, allowsNonSalary: true);
            Add(A, "APOYO_PRACTICA", "Apoyo de sostenimiento", "ApoyoSost", Value, allowsNonSalary: true);
            Add(A, "TELETRABAJO", "Teletrabajo", "Teletrabajo", Value);
            Add(A, "BONIFICACION_RETIRO", "Bonificación de retiro", "BonifRetiro", Value, allowsNonSalary: true);
            Add(A, "INDEMNIZACION", "Indemnización", "Indemnizacion", Value, allowsNonSalary: true);
            Add(A, "PAGO_TERCERO", "Pago a terceros", "PagoTercero", Value);
            Add(A, "ANTICIPO", "Anticipo", "Anticipo", Value);
            Add(A, "REINTEGRO", "Reintegro", "Reintegro", Value);
            Add(A, "COMPENSACION", "Compensación", "Compensacion", Attrs, amount: "CompensacionO", nonSalary: "CompensacionE", allowsAmount: false, special: PayrollSpecialInput.Compensation);
            Add(A, "OTRO_CONCEPTO", "Otro concepto", "OtroConcepto", List, amount: "ConceptoS", nonSalary: "ConceptoNS", kind: Days, allowsNonSalary: true, description: true);

            // ---- Deducciones ----
            Add(D, "SALUD", "Salud", "Salud", Attrs, amount: "Deduccion", percentage: "Porcentaje", kind: Pct);
            Add(D, "FONDO_PENSION", "Fondo de pensión", "FondoPension", Attrs, amount: "Deduccion", percentage: "Porcentaje", kind: Pct);
            Add(D, "FONDO_SOLIDARIDAD_PENSIONAL", "Fondo de solidaridad pensional", "FondoSP", Attrs, amount: "DeduccionSP", percentage: "Porcentaje", kind: Pct);
            Add(D, "FONDO_SUBSISTENCIA", "Fondo de subsistencia", "FondoSP", Attrs, amount: "DeduccionSub", percentage: "PorcentajeSub", kind: Pct);
            Add(D, "SINDICATO", "Sindicato", "Sindicato", List, amount: "Deduccion", percentage: "Porcentaje", kind: Pct);
            Add(D, "RETENCION_FUENTE", "Retención en la fuente", "RetencionFuente", Value);
            Add(D, "LIBRANZA", "Libranza", "Libranza", List, amount: "Deduccion", description: true);
            Add(D, "PENSION_VOLUNTARIA", "Pensión voluntaria", "PensionVoluntaria", Value);
            Add(D, "AFC", "Ahorro AFC", "AFC", Value);
            Add(D, "COOPERATIVA", "Cooperativa", "Cooperativa", Value);
            Add(D, "EMBARGO_FISCAL", "Embargo fiscal", "EmbargoFiscal", Value);
            Add(D, "PLANES_COMPLEMENTARIOS", "Planes complementarios", "PlanComplementarios", Value);
            Add(D, "EDUCACION", "Educación", "Educacion", Value);
            Add(D, "REINTEGRO", "Reintegro", "Reintegro", Value);
            Add(D, "DEUDA", "Deuda", "Deuda", Value);
            Add(D, "PAGO_TERCERO", "Pago a terceros", "PagoTercero", Value);
            Add(D, "ANTICIPO", "Anticipo", "Anticipo", Value);
            Add(D, "SANCION", "Sanción", "Sancion", List, amount: "SancionPublic", nonSalary: "SancionPriv", allowsAmount: false, special: PayrollSpecialInput.Sanction);
            Add(D, "OTRA_DEDUCCION", "Otra deducción", "OtraDeduccion", Value, description: true);

            return rows;
        }
    }
}
