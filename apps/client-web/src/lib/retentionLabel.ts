import type { SearchableSelectOption } from '@shared/components/SearchableSelect';

// Cómo se ve cada retención en los selectores del formulario: el nombre completo del concepto (Full) o solo el nombre corto con la tarifa (Short),
// "RETE FUENTE 2.5%". Los nombres cortos los parametriza la administración de la plataforma; el estilo, aparte para las retenciones de cada línea y las del pie,
// lo elige el cliente o su tenant. Sin los nombres cortos del servidor se usan estos, que son los mismos con los que se siembran.
export type RetentionLabelStyle = 'Full' | 'Short';
export type RetentionShortNames = Record<string, string>;

export const DEFAULT_RETENTION_STYLES = { line: 'Short' as RetentionLabelStyle, footer: 'Short' as RetentionLabelStyle };

const FALLBACK_SHORT_NAMES: RetentionShortNames = { RET_FUENTE: 'RETE FUENTE', RET_ICA: 'RETE ICA', RET_IVA: 'RETE IVA' };

export const shortRetentionName = (names: RetentionShortNames, taxCategory: string) => names[taxCategory] || FALLBACK_SHORT_NAMES[taxCategory] || taxCategory;

// "RETE FUENTE 2.5%"
export const shortRetentionText = (names: RetentionShortNames, taxCategory: string, rate: number) => `${shortRetentionName(names, taxCategory)} ${rate}%`;

// Opción de un selector de retenciones. En el estilo corto la fila y el valor elegido muestran "RETE FUENTE 2.5%", y la búsqueda sigue encontrando también por el
// nombre completo del concepto ("compras", "honorarios"). En el completo queda como siempre: el nombre del concepto con su categoría y tarifa, y elegido solo la tarifa.
export function retentionOption(id: string, fullName: string, taxCategory: string, rate: number, style: RetentionLabelStyle, names: RetentionShortNames): SearchableSelectOption {
  if (style === 'Short') {
    const short = shortRetentionText(names, taxCategory, rate);
    return { value: id, label: `${short} ${fullName}`, displayLabel: short, shortLabel: short };
  }
  return { value: id, label: `${fullName} (${taxCategory} ${rate}%)`, shortLabel: `${rate}%` };
}
