import React from 'react';
import { Download, Loader2 } from 'lucide-react';

export interface MonthRangeValue { from: string; to: string }

const pad = (n: number) => String(n).padStart(2, '0');
const key = (y: number, m: number) => `${y}-${pad(m)}`;

// Atajos de rango (por meses completos: el cobro se calcula mes a mes).
export function monthPresets(today = new Date()): { label: string; value: MonthRangeValue }[] {
  const y = today.getFullYear();
  const m = today.getMonth() + 1;
  const back = (months: number) => { const d = new Date(y, m - 1 - months, 1); return key(d.getFullYear(), d.getMonth() + 1); };
  return [
    { label: 'Este mes', value: { from: key(y, m), to: key(y, m) } },
    { label: 'Mes anterior', value: { from: back(1), to: back(1) } },
    { label: 'Últimos 3 meses', value: { from: back(2), to: key(y, m) } },
    { label: 'Este año', value: { from: key(y, 1), to: key(y, m) } },
    { label: 'Año anterior', value: { from: key(y - 1, 1), to: key(y - 1, 12) } },
  ];
}

export const currentMonthRange = (): MonthRangeValue => monthPresets()[0].value;

interface MonthRangeBarProps {
  value: MonthRangeValue;
  onChange: (value: MonthRangeValue) => void;
  onExport?: () => void;
  exporting?: boolean;
  // 'dark' para el portal de superadmin.
  variant?: 'light' | 'dark';
}

// Selector de rango por meses ("Desde" y "Hasta", más atajos) con botón opcional para exportar a Excel.
export default function MonthRangeBar({ value, onChange, onExport, exporting, variant = 'light' }: MonthRangeBarProps) {
  const dark = variant === 'dark';
  const input = dark
    ? 'rounded-xl border border-slate-700 bg-slate-900/60 px-3 py-2 text-sm text-white outline-none focus:border-indigo-400'
    : 'rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 outline-none focus:border-blue-500';
  const chip = dark
    ? 'rounded-full border border-slate-700 px-3 py-1.5 text-xs font-semibold text-slate-300 hover:bg-slate-700/60'
    : 'rounded-full border border-slate-200 bg-white px-3 py-1.5 text-xs font-semibold text-slate-600 hover:bg-slate-50';
  const label = dark ? 'text-xs font-bold uppercase tracking-wide text-slate-400' : 'text-xs font-bold uppercase tracking-wide text-slate-500';

  const setFrom = (from: string) => onChange({ from, to: value.to < from ? from : value.to });
  const setTo = (to: string) => onChange({ from: value.from > to ? to : value.from, to });

  return (
    <div className="flex flex-wrap items-end gap-4">
      <label className="flex flex-col gap-1">
        <span className={label}>Desde</span>
        <input type="month" value={value.from} max={value.to} onChange={e => e.target.value && setFrom(e.target.value)} className={input} />
      </label>
      <label className="flex flex-col gap-1">
        <span className={label}>Hasta</span>
        <input type="month" value={value.to} min={value.from} onChange={e => e.target.value && setTo(e.target.value)} className={input} />
      </label>
      <div className="flex flex-wrap gap-2 pb-0.5">
        {monthPresets().map(p => (
          <button key={p.label} type="button" onClick={() => onChange(p.value)} className={chip}>{p.label}</button>
        ))}
      </div>
      {onExport && (
        <button
          type="button"
          onClick={onExport}
          disabled={exporting}
          className={`ml-auto inline-flex items-center gap-2 rounded-xl px-4 py-2.5 text-sm font-bold disabled:opacity-50 ${dark ? 'bg-indigo-600 text-white hover:bg-indigo-500' : 'bg-slate-900 text-white hover:bg-slate-700'}`}
        >
          {exporting ? <Loader2 size={16} className="animate-spin" /> : <Download size={16} />} Exportar a Excel
        </button>
      )}
    </div>
  );
}

// Descarga un archivo que responde un endpoint autenticado (axios con responseType blob).
export async function downloadBlob(api: { get: (url: string, config?: any) => Promise<any> }, url: string, fallbackName: string) {
  const res = await api.get(url, { responseType: 'blob' });
  const disposition: string = res.headers?.['content-disposition'] ?? '';
  const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
  const name = match ? decodeURIComponent(match[1]) : fallbackName;
  const href = URL.createObjectURL(res.data);
  const a = document.createElement('a');
  a.href = href;
  a.download = name;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(href);
}
