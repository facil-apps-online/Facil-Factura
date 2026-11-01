using System;

namespace Fel.Core.Entities
{
    public enum PayrollConceptSection
    {
        Accrual = 0,
        Deduction = 1
    }

    // Qué significa la "cantidad" de un concepto: los días de un salario o unas vacaciones, las horas de un recargo, el porcentaje de una
    // deducción de salud.
    public enum PayrollQuantityKind
    {
        None = 0,
        Days = 1,
        Hours = 2,
        Percentage = 3
    }

    // Cómo va el concepto en el XML de nómina de la DIAN.
    public enum PayrollDianShape
    {
        Attributes = 0, // Un elemento único con atributos (Basico, Primas, Cesantias).
        List = 1,       // Una lista de elementos repetibles, cada uno con su rango de fechas u horas (HED, Incapacidad, VacacionesComunes).
        Value = 2       // Un elemento con un solo valor como texto (Comision, Dotacion, RetencionFuente).
    }

    // Datos que el concepto necesita además del monto y la cantidad, porque el integrador los exige (o el XML).
    public enum PayrollSpecialInput
    {
        None = 0,
        Dates = 1,             // Fecha inicial y final (licencia de paternidad, huelga legal).
        MedicalLeave = 2,      // Tipo de incapacidad (común o laboral).
        CesantiasInterest = 3, // Intereses sobre las cesantías.
        Compensation = 4,      // Compensación ordinaria y extraordinaria.
        Sanction = 5           // Sanción pública y privada.
    }

    // Catálogo global de conceptos de nómina (devengos y deducciones), administrado por Superadmin. Una sola fuente para los dos caminos de
    // emisión: Dataico lee de aquí los campos que acepta cada código, y el XML nativo de la DIAN lee el elemento y los atributos de destino.
    // También da el nombre que se muestra en el formulario, el PDF y la plantilla de Excel.
    public class PayrollConceptType
    {
        public Guid Id { get; set; }
        public PayrollConceptSection Section { get; set; }

        // Código del concepto en Dataico (BASICO, SALUD, HORA_EXTRA_DIURNA...). Único dentro de la sección.
        public string DataicoCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        // Equivalencia DIAN (anexo técnico de nómina, XSD NominaIndividualElectronica): elemento de destino y atributo que recibe cada dato.
        // Varios conceptos pueden compartir elemento con atributos distintos (AUXILIO_DE_TRANSPORTE y VIATICO van en Transporte).
        public string DianElement { get; set; } = string.Empty;
        public PayrollDianShape DianShape { get; set; }
        public string? DianAmountAttribute { get; set; }
        // Segundo valor del elemento: el no salarial (PagoNS) o, en compensaciones y sanciones, la segunda parte (CompensacionE, SancionPriv).
        public string? DianNonSalaryAttribute { get; set; }
        public string? DianQuantityAttribute { get; set; }
        public string? DianPercentageAttribute { get; set; }

        public PayrollQuantityKind QuantityKind { get; set; }
        // Además de las horas, el concepto lleva el porcentaje de recargo (horas extra y recargos).
        public bool HasPercentage { get; set; }
        public bool AllowsAmount { get; set; } = true;
        public bool AllowsNonSalary { get; set; }
        public bool RequiresDescription { get; set; }
        public PayrollSpecialInput SpecialInput { get; set; }

        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
