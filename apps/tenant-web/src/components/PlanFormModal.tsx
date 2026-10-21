import React, { useEffect, useState } from 'react';
import { X } from 'lucide-react';
import { toast } from 'sonner';
import { api, getErrorMessage } from '../lib/api';
import SearchableSelect from '@shared/components/SearchableSelect';

export interface Plan {
  id: string;
  name: string;
  description: string | null;
  cycle: 'Monthly' | 'Quarterly' | 'Annual';
  fixedFee: number;
  feeTiming: 'Advance' | 'Arrears';
  includedDocuments: number | null;
  pricePerDocument: number;
  scope: 'PerClient' | 'PerBranch';
  isActive: boolean;
  countApproved: boolean;
  countRejected: boolean;
  countInProcess: boolean;
  countInvoices: boolean;
  countNotes: boolean;
  countSupportDocuments: boolean;
  countPayroll: boolean;
  exceedAction: 'Notify' | 'Block' | null;
  clients: number;
}

export const CYCLE_LABELS: Record<string, string> = { Monthly: 'Mensual', Quarterly: 'Trimestral', Annual: 'Anual' };
export const SCOPE_LABELS: Record<string, string> = { PerClient: 'Toda la empresa', PerBranch: 'Cada sucursal' };

const EMPTY: Omit<Plan, 'id' | 'isActive' | 'clients'> = {
  name: '', description: '', cycle: 'Monthly', fixedFee: 0, feeTiming: 'Advance', includedDocuments: 0, pricePerDocument: 0, scope: 'PerClient',
  countApproved: true, countRejected: true, countInProcess: false, countInvoices: true, countNotes: true, countSupportDocuments: true, countPayroll: true, exceedAction: null,
};

// Plantillas para empezar: cada una solo cambia las reglas de documentos; todo se puede ajustar después.
const TEMPLATES = [
  { key: 'use', title: 'Por documento', hint: 'Se cobra cada documento a un precio.', values: { includedDocuments: 0, fixedFee: 0 } },
  { key: 'included', title: 'Cuota con documentos incluidos', hint: 'Una cuota incluye N documentos; el resto se cobra aparte.', values: { includedDocuments: 300 } },
  { key: 'unlimited', title: 'Ilimitado', hint: 'Una cuota y sin límite de documentos.', values: { includedDocuments: null } },
];

const labelClass = 'block text-sm font-bold text-slate-700 mb-1';
const inputClass = 'w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none text-sm';

interface PlanFormModalProps {
  open: boolean;
  plan: Plan | null;
  onClose: () => void;
  onSaved: () => void;
}

function Check({ checked, onChange, children }: { checked: boolean; onChange: (v: boolean) => void; children: React.ReactNode }) {
  return (
    <label className="flex items-center gap-2 text-sm text-slate-700 cursor-pointer">
      <input type="checkbox" checked={checked} onChange={e => onChange(e.target.checked)} className="w-4 h-4 accent-blue-600" />
      {children}
    </label>
  );
}

// Alta y edición de un plan comercial del tenant.
export default function PlanFormModal({ open, plan, onClose, onSaved }: PlanFormModalProps) {
  const [form, setForm] = useState({ ...EMPTY });
  const [unlimited, setUnlimited] = useState(false);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!open) return;
    if (plan) {
      const { id, isActive, clients, ...rest } = plan;
      setForm({ ...EMPTY, ...rest, description: rest.description ?? '' });
      setUnlimited(plan.includedDocuments === null);
    } else {
      setForm({ ...EMPTY });
      setUnlimited(false);
    }
  }, [open, plan]);

  if (!open) return null;

  const set = (patch: Partial<typeof form>) => setForm(prev => ({ ...prev, ...patch }));
  const included = unlimited ? null : (form.includedDocuments ?? 0);

  const applyTemplate = (values: Partial<typeof form>) => {
    setUnlimited(values.includedDocuments === null);
    set(values);
  };

  const save = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    try {
      const body = { ...form, description: form.description?.trim() || null, includedDocuments: included, pricePerDocument: unlimited ? 0 : form.pricePerDocument };
      if (plan) await api.put(`/tenant/plans/${plan.id}`, body);
      else await api.post('/tenant/plans', body);
      toast.success(plan ? 'Plan actualizado' : 'Plan creado');
      onSaved();
      onClose();
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo guardar el plan'));
    } finally {
      setSaving(false);
    }
  };

  const priceLabel = unlimited ? '' : included === 0 ? 'Precio por documento' : 'Precio por documento adicional';

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4">
      <form onSubmit={save} className="flex max-h-[92vh] w-full max-w-3xl flex-col overflow-hidden rounded-2xl bg-white shadow-2xl">
        <div className="flex items-center justify-between border-b border-slate-100 px-6 py-4">
          <h2 className="text-lg font-bold text-slate-900">{plan ? 'Editar plan' : 'Nuevo plan'}</h2>
          <button type="button" onClick={onClose} aria-label="Cerrar" className="rounded-lg p-1.5 text-slate-400 hover:bg-slate-100"><X size={18} /></button>
        </div>

        <div className="space-y-5 overflow-y-auto px-6 py-5">
          {!plan && (
            <div>
              <p className={labelClass}>Empieza con una plantilla</p>
              <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
                {TEMPLATES.map(t => (
                  <button key={t.key} type="button" onClick={() => applyTemplate(t.values)} className="rounded-xl border border-slate-200 p-3 text-left hover:border-blue-400 hover:bg-blue-50/40">
                    <span className="block text-sm font-bold text-slate-800">{t.title}</span>
                    <span className="mt-1 block text-xs text-slate-500">{t.hint}</span>
                  </button>
                ))}
              </div>
            </div>
          )}

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <label className={labelClass}>Nombre</label>
              <input required maxLength={100} value={form.name} onChange={e => set({ name: e.target.value })} placeholder="Ej. Plan 300 documentos" className={inputClass} />
            </div>
            <div>
              <label className={labelClass}>Descripción (opcional)</label>
              <input maxLength={500} value={form.description ?? ''} onChange={e => set({ description: e.target.value })} className={inputClass} />
            </div>
            <div>
              <label className={labelClass}>Ciclo de cobro</label>
              <SearchableSelect value={form.cycle} onChange={v => v && set({ cycle: v as Plan['cycle'] })} placeholder="Ciclo..."
                options={Object.entries(CYCLE_LABELS).map(([value, label]) => ({ value, label }))} />
            </div>
            <div>
              <label className={labelClass}>El plan aplica a</label>
              <SearchableSelect value={form.scope} onChange={v => v && set({ scope: v as Plan['scope'] })} placeholder="Aplica a..."
                options={Object.entries(SCOPE_LABELS).map(([value, label]) => ({ value, label }))} />
            </div>
          </div>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <label className={labelClass}>Cuota fija por ciclo</label>
              <input type="number" min={0} step="any" value={form.fixedFee} onChange={e => set({ fixedFee: Number(e.target.value) || 0 })} className={`${inputClass} font-mono`} />
            </div>
            <div>
              <label className={labelClass}>Cuándo se cobra la cuota</label>
              <SearchableSelect value={form.feeTiming} onChange={v => v && set({ feeTiming: v as Plan['feeTiming'] })} placeholder="Momento..."
                options={[{ value: 'Advance', label: 'Al inicio del ciclo (por adelantado)' }, { value: 'Arrears', label: 'Al final del ciclo (vencida)' }]} />
            </div>
          </div>

          <div>
            <p className={labelClass}>Documentos</p>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
              <div className="flex gap-2 sm:col-span-1">
                <button type="button" onClick={() => setUnlimited(false)} className={`flex-1 rounded-xl px-3 py-2.5 text-sm font-bold ${!unlimited ? 'bg-blue-600 text-white' : 'bg-slate-100 text-slate-600'}`}>Con límite</button>
                <button type="button" onClick={() => setUnlimited(true)} className={`flex-1 rounded-xl px-3 py-2.5 text-sm font-bold ${unlimited ? 'bg-blue-600 text-white' : 'bg-slate-100 text-slate-600'}`}>Ilimitados</button>
              </div>
              {!unlimited && (
                <>
                  <div>
                    <input type="number" min={0} aria-label="Documentos incluidos" value={form.includedDocuments ?? 0} onChange={e => set({ includedDocuments: Math.max(0, parseInt(e.target.value) || 0) })} className={`${inputClass} font-mono`} />
                    <p className="mt-1 text-xs text-slate-400">Incluidos por ciclo (0 = todo se cobra por documento)</p>
                  </div>
                  <div>
                    <input type="number" min={0} step="any" aria-label={priceLabel} value={form.pricePerDocument} onChange={e => set({ pricePerDocument: Number(e.target.value) || 0 })} className={`${inputClass} font-mono`} />
                    <p className="mt-1 text-xs text-slate-400">{priceLabel}</p>
                  </div>
                </>
              )}
            </div>
          </div>

          {!unlimited && (included ?? 0) > 0 && (
            <div>
              <label className={labelClass}>Al llegar al límite de documentos incluidos</label>
              <SearchableSelect value={form.exceedAction ?? ''} onChange={v => set({ exceedAction: (v || null) as Plan['exceedAction'] })} placeholder="Usar la opción general"
                options={[{ value: '', label: 'Usar la opción general' }, { value: 'Notify', label: 'Solo avisar' }, { value: 'Block', label: 'Bloquear al cliente' }]} />
              <p className="mt-1 text-xs text-slate-400">Los adicionales se cobran siempre al precio del plan. Bloquear impide que el cliente entre y emita hasta el siguiente ciclo.</p>
            </div>
          )}

          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
            <div>
              <p className={labelClass}>Estados que cuentan</p>
              <div className="space-y-2">
                <Check checked={form.countApproved} onChange={v => set({ countApproved: v })}>Emitidos (aprobados por la DIAN)</Check>
                <Check checked={form.countRejected} onChange={v => set({ countRejected: v })}>Rechazados</Check>
                <Check checked={form.countInProcess} onChange={v => set({ countInProcess: v })}>En proceso</Check>
                <p className="text-xs text-slate-400">Los borradores nunca cuentan.</p>
              </div>
            </div>
            <div>
              <p className={labelClass}>Tipos de documento que cuentan</p>
              <div className="space-y-2">
                <Check checked={form.countInvoices} onChange={v => set({ countInvoices: v })}>Facturas</Check>
                <Check checked={form.countNotes} onChange={v => set({ countNotes: v })}>Notas crédito y débito</Check>
                <Check checked={form.countSupportDocuments} onChange={v => set({ countSupportDocuments: v })}>Documento soporte</Check>
                <Check checked={form.countPayroll} onChange={v => set({ countPayroll: v })}>Nómina electrónica</Check>
              </div>
            </div>
          </div>
        </div>

        <div className="flex justify-end gap-3 border-t border-slate-100 px-6 py-4">
          <button type="button" onClick={onClose} className="rounded-xl bg-slate-100 px-4 py-2.5 text-sm font-bold text-slate-600 hover:bg-slate-200">Cancelar</button>
          <button type="submit" disabled={saving} className="rounded-xl bg-blue-600 px-5 py-2.5 text-sm font-bold text-white hover:bg-blue-700 disabled:opacity-50">{saving ? 'Guardando...' : 'Guardar plan'}</button>
        </div>
      </form>
    </div>
  );
}
