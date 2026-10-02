// Hora legal de Colombia (UTC-5, sin horario de verano). Los documentos electrónicos se emiten con
// fecha colombiana sin importar la zona horaria o el reloj del equipo del usuario: con
// new Date().toISOString() (UTC), después de las 7 p. m. "hoy" ya era el día siguiente.
const COLOMBIA_OFFSET_MS = -5 * 60 * 60 * 1000;

/** Fecha de hoy en Colombia, formato yyyy-MM-dd. */
export function todayColombia(): string {
  return new Date(Date.now() + COLOMBIA_OFFSET_MS).toISOString().slice(0, 10);
}

/** Hoy en Colombia como Date local a medianoche (para operar con año/mes/día sin corrimientos). */
export function todayColombiaDate(): Date {
  const [y, m, d] = todayColombia().split('-').map(Number);
  return new Date(y, m - 1, d);
}

/** yyyy-MM-dd con los componentes locales de la fecha (toISOString la corre un día en zonas al este de UTC). */
export function toLocalIsoDate(d: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}
