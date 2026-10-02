// Rangos de fecha pensados para cierre contable: anclados al mes calendario, no a días corridos
// (30 días corridos mezcla dos periodos fiscales para quien revisa a mitad de mes). Usado por las
// listas de Facturas, Documento Soporte y Nómina.
import { todayColombiaDate, toLocalIsoDate } from './colombiaTime';

export type DateRangePreset = 'this-month' | 'last-month' | 'last-2-months' | 'last-3-months' | 'this-year' | 'custom';

export const DATE_RANGE_PRESET_OPTIONS = [
  { value: 'this-month', label: 'Este mes' },
  { value: 'last-month', label: 'Mes anterior' },
  { value: 'last-2-months', label: 'Últimos 2 meses' },
  { value: 'last-3-months', label: 'Últimos 3 meses' },
  { value: 'this-year', label: 'Este año' },
  { value: 'custom', label: 'Rango personalizado' }
];

function toIsoDate(d: Date): string {
  return toLocalIsoDate(d);
}

// Devuelve {from, to} en formato yyyy-MM-dd para el preset dado, o null si es 'custom' (el
// llamador debe usar sus propios inputs de fecha en ese caso).
export function getDateRangeForPreset(preset: DateRangePreset): { from: string, to: string } | null {
  // "Hoy" siempre en hora de Colombia, no la del equipo.
  const today = todayColombiaDate();
  const startOfMonth = (monthsAgo: number) => new Date(today.getFullYear(), today.getMonth() - monthsAgo, 1);

  switch (preset) {
    case 'this-month':
      return { from: toIsoDate(startOfMonth(0)), to: toIsoDate(today) };
    case 'last-month': {
      const start = startOfMonth(1);
      const end = new Date(today.getFullYear(), today.getMonth(), 0); // último día del mes anterior
      return { from: toIsoDate(start), to: toIsoDate(end) };
    }
    case 'last-2-months':
      return { from: toIsoDate(startOfMonth(1)), to: toIsoDate(today) };
    case 'last-3-months':
      return { from: toIsoDate(startOfMonth(2)), to: toIsoDate(today) };
    case 'this-year':
      return { from: toIsoDate(new Date(today.getFullYear(), 0, 1)), to: toIsoDate(today) };
    case 'custom':
      return null;
  }
}
