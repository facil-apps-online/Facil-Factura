import React, { useEffect, useState } from 'react';
import { Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { api, getErrorMessage } from '../lib/api';
import SearchableSelect from '@shared/components/SearchableSelect';
import { CYCLE_LABELS, SCOPE_LABELS, type Plan } from './PlanFormModal';
import { EXCEED_LABELS } from './PlanSettingsCard';

interface UsageUnit {
  branchId: string | null;
  name: string;
  documents: number;
  included: number | null;
  extra: number;
  fee: number;
  extraAmount: number;
  total: number;
}

interface PlanUsage {
  planName: string;
  cycle: string;
  feeTiming: string;
  scope: string;
  periodStart: string;
  periodEnd: string;
  units: UsageUnit[];
  documents: number;
  extra: number;
  feeAmount: number;
  extraAmount: number;
  total: number;
}

interface Assignment {
  planId: string;
  planName: string | null;
  startDate: string;
  fixedFeeOverride: number | null;
  pricePerDocumentOverride: number | null;
  exceedActionOverride: 'Notify' | 'Block' | null;
}

interface PlanAlertRow {
  id: string;
  kind: string;
  message: string;
  createdAt: string;
}

interface PlanState {
  assignment: Assignment | null;
  usage: PlanUsage | null;
  history: PlanUsage[];
  alerts: PlanAlertRow[];
}

const ALERT_LABELS: Record<string, { text: string; style: string }> = {
  Warning: { text: 'Aviso', style: 'bg-amber-50 text-amber-700' },
  LimitReached: { text: 'Límite', style: 'bg-rose-50 text-rose-700' },
  Blocked: { text: 'Bloqueado', style: 'bg-rose-100 text-rose-800' },
  Unblocked: { text: 'Desbloqueado', style: 'bg-emerald-50 text-emerald-700' },
};

const money = (value: number) => `$${value.toLocaleString('es-CO')}`;
const day = (iso: string) => new Date(iso).toLocaleDateString('es-CO', { day: '2-digit', month: 'short', year: 'numeric' });
const inputClass = 'w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none text-sm';
const labelClass = 'block text-sm font-bold text-slate-700 mb-1';

// Plan y consumo de un cliente: el plan vigente, el uso del ciclo frente a lo incluido, el excedente, el valor a cobrar y el historial por ciclo.
export default function ClientPlanTab({ clientId }: { clientId: string }) {
  const [state, setState] = useState<PlanState | null>(null);
  const [plans, setPlans] = useState<Plan[]>([]);
  const [planId, setPlanId] = useState('');
  const [startDate, setStartDate] = useState(new Date().toISOString().slice(0, 10));
  const [feeOverride, setFeeOverride] = useState('');
  const [priceOverride, setPriceOverride] = useState('');
  const [exceedOverride, setExceedOverride] = useState('');
  const [saving, setSaving] = useState(false);

  const load = async () => {
    try {
      const [res, plansRes] = await Promise.all([api.get<PlanState>(`/tenant/clients/${clientId}/plan`), api.get<Plan[]>('/tenant/plans')]);
      setState(res.data);
      setPlans(plansRes.data);
      const a = res.data.assignment;
      setPlanId(a?.planId ?? '');
      setFeeOverride(a?.fixedFeeOverride != null ? String(a.fixedFeeOverride) : '');
      setPriceOverride(a?.pricePerDocumentOverride != null ? String(a.pricePerDocumentOverride) : '');
      setExceedOverride(a?.exceedActionOverride ?? '');
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo cargar el plan del cliente'));
      setState({ assignment: null, usage: null, history: [], alerts: [] });
    }
  };

  useEffect(() => { load(); }, [clientId]);

  const assign = async () => {
    if (!planId) { toast.error('Elige un plan'); return; }
    setSaving(true);
    try {
      await api.put(`/tenant/clients/${clientId}/plan`, {
        planId, startDate,
        fixedFeeOverride: feeOverride === '' ? null : Number(feeOverride),
        pricePerDocumentOverride: priceOverride === '' ? null : Number(priceOverride),
        exceedActionOverride: exceedOverride === '' ? null : exceedOverride,
      });
      toast.success('Plan asignado');
      await load();
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo asignar el plan'));
    } finally {
      setSaving(false);
    }
  };

  const unassign = async () => {
    if (!window.confirm('¿Quitar el plan de este cliente? Queda sin plan desde hoy; el historial se conserva.')) return;
    setSaving(true);
    try {
      await api.delete(`/tenant/clients/${clientId}/plan`);
      toast.success('Plan retirado');
      await load();
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo quitar el plan'));
    } finally {
      setSaving(false);
    }
  };

  if (!state) return <div className="flex justify-center p-10"><Loader2 className="animate-spin text-slate-400" /></div>;

  const { assignment, usage, history, alerts } = state;

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-bold text-slate-800">Plan y consumo</h2>
        <p className="mt-1 text-sm text-slate-500">
          {assignment ? <>Plan vigente: <b>{assignment.planName}</b>, desde el {day(assignment.startDate)}.</> : 'Este cliente no tiene un plan asignado.'}
        </p>
      </div>

      <div className="rounded-2xl border border-slate-200 bg-white p-5">
        <h3 className="mb-4 text-sm font-bold uppercase tracking-wide text-slate-500">{assignment ? 'Cambiar plan' : 'Asignar plan'}</h3>
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-4">
          <div className="xl:col-span-2">
            <label className={labelClass}>Plan</label>
            <SearchableSelect
              value={planId}
              onChange={setPlanId}
              placeholder="Buscar plan..."
              options={plans.map(p => ({ value: p.id, label: `${p.name} · ${CYCLE_LABELS[p.cycle]}` }))}
            />
          </div>
          <div>
            <label className={labelClass}>Rige desde</label>
            <input type="date" value={startDate} onChange={e => setStartDate(e.target.value)} className={inputClass} />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className={labelClass}>Cuota especial</label>
              <input type="number" min={0} step="any" value={feeOverride} onChange={e => setFeeOverride(e.target.value)} placeholder="Del plan" className={`${inputClass} font-mono`} />
            </div>
            <div>
              <label className={labelClass}>Precio especial</label>
              <input type="number" min={0} step="any" value={priceOverride} onChange={e => setPriceOverride(e.target.value)} placeholder="Del plan" className={`${inputClass} font-mono`} />
            </div>
          </div>
        </div>
        <div className="mt-4 max-w-md">
          <label className={labelClass}>Al llegar al límite de documentos</label>
          <SearchableSelect value={exceedOverride} onChange={setExceedOverride} placeholder="La del plan"
            options={[{ value: '', label: 'La del plan (o la opción general)' }, ...Object.entries(EXCEED_LABELS).map(([value, label]) => ({ value, label }))]} />
        </div>
        <p className="mt-3 text-xs text-slate-400">La cuota, el precio y la acción especiales pisan los del plan solo para este cliente. Déjalos vacíos para usar los del plan.</p>
        <div className="mt-4 flex gap-3">
          <button onClick={assign} disabled={saving || !planId} className="rounded-xl bg-blue-600 px-5 py-2.5 text-sm font-bold text-white hover:bg-blue-700 disabled:opacity-50">
            {assignment ? 'Guardar cambio' : 'Asignar plan'}
          </button>
          {assignment && <button onClick={unassign} disabled={saving} className="rounded-xl bg-slate-100 px-5 py-2.5 text-sm font-bold text-slate-600 hover:bg-slate-200 disabled:opacity-50">Quitar plan</button>}
        </div>
      </div>

      {usage && (
        <div className="rounded-2xl border border-slate-200 bg-white p-5">
          <div className="mb-4 flex flex-wrap items-baseline justify-between gap-2">
            <h3 className="text-sm font-bold uppercase tracking-wide text-slate-500">Ciclo actual · {CYCLE_LABELS[usage.cycle]} · {SCOPE_LABELS[usage.scope]}</h3>
            <span className="text-sm text-slate-500">{day(usage.periodStart)} – {day(usage.periodEnd)}</span>
          </div>

          <div className="space-y-4">
            {usage.units.map(u => {
              const pct = u.included && u.included > 0 ? Math.min(100, Math.round((u.documents / u.included) * 100)) : null;
              return (
                <div key={u.branchId ?? 'all'} className="rounded-xl bg-slate-50 p-4">
                  <div className="mb-2 flex flex-wrap items-center justify-between gap-2">
                    <span className="font-semibold text-slate-800">{u.name}</span>
                    <span className="font-mono font-bold text-slate-900">{money(u.total)}</span>
                  </div>
                  <div className="text-sm text-slate-600">
                    {u.documents} documentos{u.included === null ? ' · ilimitados' : u.included === 0 ? ' · todos se cobran por documento' : ` de ${u.included} incluidos`}
                    {u.extra > 0 && <span className="font-semibold text-rose-600"> · {u.extra} adicionales</span>}
                  </div>
                  {pct !== null && (
                    <div className="mt-2 h-2 overflow-hidden rounded-full bg-slate-200">
                      <div className={`h-full rounded-full ${pct >= 100 ? 'bg-rose-500' : pct >= 80 ? 'bg-amber-500' : 'bg-emerald-500'}`} style={{ width: `${pct}%` }} />
                    </div>
                  )}
                  <div className="mt-2 text-xs text-slate-500">Cuota {money(u.fee)} · Excedente {money(u.extraAmount)}</div>
                </div>
              );
            })}
          </div>

          <div className="mt-4 flex flex-wrap items-center justify-between gap-2 border-t border-slate-100 pt-4">
            <span className="text-sm text-slate-500">Cuota {usage.feeTiming === 'Advance' ? 'por adelantado' : 'vencida'} · excedente al cierre del ciclo</span>
            <span className="text-lg font-extrabold text-slate-900">Total del ciclo: {money(usage.total)}</span>
          </div>
        </div>
      )}

      {alerts.length > 0 && (
        <div className="overflow-hidden rounded-2xl border border-slate-200 bg-white">
          <div className="border-b border-slate-100 bg-slate-50 px-5 py-3 text-sm font-bold text-slate-700">Avisos recientes</div>
          <ul>
            {alerts.map(a => (
              <li key={a.id} className="flex items-start gap-3 border-t border-slate-50 px-5 py-2.5 text-sm first:border-t-0">
                <span className={`mt-0.5 shrink-0 rounded-full px-2 py-0.5 text-xs font-semibold ${ALERT_LABELS[a.kind]?.style ?? 'bg-slate-100 text-slate-600'}`}>{ALERT_LABELS[a.kind]?.text ?? a.kind}</span>
                <span className="text-slate-700">{a.message}</span>
                <span className="ml-auto shrink-0 text-xs text-slate-400">{day(a.createdAt)}</span>
              </li>
            ))}
          </ul>
        </div>
      )}

      {history.length > 1 && (
        <div className="overflow-hidden rounded-2xl border border-slate-200 bg-white">
          <div className="border-b border-slate-100 bg-slate-50 px-5 py-3 text-sm font-bold text-slate-700">Historial por ciclo</div>
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead>
                <tr className="text-xs uppercase tracking-wide text-slate-400">
                  <th className="px-5 py-2 font-medium">Ciclo</th>
                  <th className="px-5 py-2 text-right font-medium">Documentos</th>
                  <th className="px-5 py-2 text-right font-medium">Adicionales</th>
                  <th className="px-5 py-2 text-right font-medium">Cuota</th>
                  <th className="px-5 py-2 text-right font-medium">Excedente</th>
                  <th className="px-5 py-2 text-right font-medium">Total</th>
                </tr>
              </thead>
              <tbody>
                {history.map(h => (
                  <tr key={h.periodStart} className="border-t border-slate-50">
                    <td className="px-5 py-2 text-slate-700">{day(h.periodStart)} – {day(h.periodEnd)}</td>
                    <td className="px-5 py-2 text-right">{h.documents}</td>
                    <td className="px-5 py-2 text-right">{h.extra}</td>
                    <td className="px-5 py-2 text-right font-mono">{money(h.feeAmount)}</td>
                    <td className="px-5 py-2 text-right font-mono">{money(h.extraAmount)}</td>
                    <td className="px-5 py-2 text-right font-mono font-bold">{money(h.total)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}
