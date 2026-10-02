import { useSyncExternalStore } from 'react';

// Formato numérico del portal según el ajuste del cliente (Client.DecimalSeparator):
//   "." → 1,234,567.89 (punto decimal, coma de miles — el valor por defecto)
//   "," → 1.234.567,89 (coma decimal, punto de miles)
// Antes cada pantalla usaba el idioma del navegador (Intl.NumberFormat() / <input type="number">) o un
// 'es-CO' fijo, y en la misma factura se mezclaban ambos formatos. Esto es solo presentación: los
// valores que viajan a la API siempre son números.

export type DecimalSeparator = '.' | ',';

export interface NumberFormat {
  decimal: DecimalSeparator;
  group: string;
  /** Número con miles; hasta `maxDecimals` decimales (los ceros sobrantes se omiten salvo `minDecimals`). */
  number: (n: number | null | undefined, maxDecimals?: number, minDecimals?: number) => string;
  /** Moneda sin símbolo: miles y 2 decimales fijos ("1,234.50"). */
  money: (n: number | null | undefined) => string;
  /** Moneda sin decimales ("1,235"). */
  moneyInt: (n: number | null | undefined) => string;
  /** Texto escrito (o pegado) → texto con miles ya aplicados + valor numérico. */
  typing: (raw: string, maxDecimals?: number) => { display: string; value: number };
  /** Texto con el formato del cliente → número. */
  parse: (text: string) => number;
}

export function buildNumberFormat(sep: DecimalSeparator): NumberFormat {
  const group = sep === '.' ? ',' : '.';

  const withGroups = (digits: string) => digits.replace(/\B(?=(\d{3})+(?!\d))/g, group);

  const number: NumberFormat['number'] = (n, maxDecimals = 2, minDecimals = 0) => {
    const v = Number.isFinite(n as number) ? (n as number) : 0;
    const fixed = Math.abs(v).toFixed(maxDecimals);
    const [int, decRaw = ''] = fixed.split('.');
    let dec = decRaw;
    while (dec.length > minDecimals && dec.endsWith('0')) dec = dec.slice(0, -1);
    const neg = v < 0 && /[1-9]/.test(fixed);
    return (neg ? '-' : '') + withGroups(int) + (dec ? sep + dec : '');
  };

  const typing: NumberFormat['typing'] = (raw, maxDecimals = 2) => {
    // El carácter "ajeno" (la coma en formato ".", el punto en formato ",") es el separador de miles
    // que este mismo campo escribe, así que solo se interpreta como decimal si es lo último que se
    // tecleó (el formato nunca deja un separador de miles al final) y todavía no hay decimal. Así se
    // puede usar el punto o la coma del teclado numérico sin importar el formato configurado.
    const alien = sep === '.' ? ',' : '.';
    let text = raw;
    if (text.endsWith(alien) && !text.includes(sep)) text = text.slice(0, -1) + sep;

    let cleaned = '';
    for (const ch of text) if (/\d/.test(ch) || ch === sep) cleaned += ch;
    const first = cleaned.indexOf(sep);
    if (first !== -1) cleaned = cleaned.slice(0, first + 1) + cleaned.slice(first + 1).split(sep).join('');
    const parts = cleaned.split(sep);
    const int = (parts[0] || '').replace(/^0+(?=\d)/, '');
    const dec = parts[1] !== undefined ? parts[1].slice(0, maxDecimals) : undefined;
    const display = dec !== undefined && maxDecimals > 0
      ? `${withGroups(int) || '0'}${sep}${dec}`
      : withGroups(int);
    const value = parseFloat((int || '0') + (dec ? '.' + dec : '')) || 0;
    return { display, value };
  };

  const parse: NumberFormat['parse'] = (text) => {
    const clean = text.split(group).join('').replace(sep, '.');
    return parseFloat(clean) || 0;
  };

  return {
    decimal: sep,
    group,
    number,
    money: (n) => number(n, 2, 2),
    moneyInt: (n) => number(n, 0, 0),
    typing,
    parse,
  };
}

let current: DecimalSeparator = '.';
let snapshot = buildNumberFormat(current);
const listeners = new Set<() => void>();

export function setDecimalSeparator(sep: string | null | undefined) {
  const next: DecimalSeparator = sep === ',' ? ',' : '.';
  if (next === current) return;
  current = next;
  snapshot = buildNumberFormat(next);
  listeners.forEach(l => l());
}

export function getNumberFormat(): NumberFormat {
  return snapshot;
}

/** Hook: devuelve el formateador vigente y re-renderiza el componente cuando cambia el ajuste. */
export function useNumberFormat(): NumberFormat {
  return useSyncExternalStore(
    (cb) => { listeners.add(cb); return () => { listeners.delete(cb); }; },
    getNumberFormat,
    getNumberFormat,
  );
}
