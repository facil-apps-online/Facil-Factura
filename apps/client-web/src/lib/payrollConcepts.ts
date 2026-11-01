// Catálogo de conceptos de la captura de nómina. Lo administra Superadmin (tabla PayrollConceptTypes) y es el mismo que usan Dataico y el XML
// de la DIAN: el formulario solo ofrece lo que cada concepto admite, porque el integrador rechaza una llave que no le corresponde.

export type ConceptList = 'accruals' | 'deductions';

export type QuantityKind = 'None' | 'Days' | 'Hours' | 'Percentage';
export type SpecialInput = 'None' | 'Dates' | 'MedicalLeave' | 'CesantiasInterest' | 'Compensation' | 'Sanction';

export interface ConceptType {
  section: 'Accrual' | 'Deduction';
  code: string;
  name: string;
  // Qué significa la cantidad del concepto: días, horas o porcentaje.
  quantityKind: QuantityKind;
  // Además de las horas, lleva el porcentaje de recargo (horas extra y recargos).
  hasPercentage: boolean;
  allowsAmount: boolean;
  allowsNonSalary: boolean;
  requiresDescription: boolean;
  specialInput: SpecialInput;
}

export const sectionOf = (list: ConceptList): ConceptType['section'] => (list === 'accruals' ? 'Accrual' : 'Deduction');

export const OTHER_CODE: Record<ConceptList, string> = { accruals: 'OTRO_CONCEPTO', deductions: 'OTRA_DEDUCCION' };

export const QUANTITY_LABELS: Record<Exclude<QuantityKind, 'None'>, string> = { Days: 'Días', Hours: 'Horas', Percentage: '%' };

// Campo del concepto donde se guarda la cantidad según su significado.
export const QUANTITY_FIELD = { Days: 'days', Hours: 'hours', Percentage: 'percentage' } as const;

export const findConceptType = (types: ConceptType[], list: ConceptList, code?: string | null) =>
  code ? types.find(t => t.section === sectionOf(list) && t.code === code.toUpperCase()) : undefined;
