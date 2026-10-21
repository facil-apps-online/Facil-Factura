import React, { useEffect, useState } from 'react';
import { ChevronDown, ChevronRight, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { api, getErrorMessage } from '../lib/api';
import SearchableSelect from '@shared/components/SearchableSelect';

interface Settings {
  alertsEnabled: boolean;
  warnPercent: number;
  emailTenant: boolean;
  emailClient: boolean;
  defaultExceedAction: 'Notify' | 'Block';
  tenantEmail: string;
  showUsageToClients: boolean;
}

export const EXCEED_LABELS: Record<string, string> = { Notify: 'Solo avisar', Block: 'Bloquear al cliente' };

function Check({ checked, onChange, children }: { checked: boolean; onChange: (v: boolean) => void; children: React.ReactNode }) {
  return (
    <label className="flex items-start gap-2 text-sm text-slate-700 cursor-pointer">
      <input type="checkbox" checked={checked} onChange={e => onChange(e.target.checked)} className="mt-0.5 h-4 w-4 accent-blue-600" />
      <span>{children}</span>
    </label>
  );
}

// Preferencias generales de alertas y límites de los planes. Cada plan, y cada cliente, pueden cambiar la acción al llegar al límite.
export default function PlanSettingsCard() {
  const [settings, setSettings] = useState<Settings | null>(null);
  const [open, setOpen] = useState(false);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    api.get<Settings>('/tenant/plan-settings')
      .then(res => setSettings(res.data))
      .catch(err => toast.error(getErrorMessage(err, 'No se pudieron cargar las preferencias de alertas')));
  }, []);

  const save = async () => {
    if (!settings) return;
    setSaving(true);
    try {
      const res = await api.put<Settings>('/tenant/plan-settings', settings);
      setSettings(res.data);
      toast.success('Preferencias guardadas');
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudieron guardar las preferencias'));
    } finally {
      setSaving(false);
    }
  };

  if (!settings) return <div className="mb-6 flex justify-center p-4"><Loader2 className="animate-spin text-slate-400" /></div>;
  const set = (patch: Partial<Settings>) => setSettings({ ...settings, ...patch });

  return (
    <div className="mb-6 rounded-2xl border border-slate-200 bg-white">
      <button type="button" onClick={() => setOpen(!open)} className="flex w-full items-center justify-between px-5 py-4 text-left">
        <span>
          <span className="block text-sm font-bold text-slate-800">Alertas y límites</span>
          <span className="block text-xs text-slate-500">
            Aviso al {settings.warnPercent}% y al 100% de lo incluido · al llegar al límite: {EXCEED_LABELS[settings.defaultExceedAction].toLowerCase()}
          </span>
        </span>
        {open ? <ChevronDown size={18} className="text-slate-400" /> : <ChevronRight size={18} className="text-slate-400" />}
      </button>

      {open && (
        <div className="space-y-5 border-t border-slate-100 px-5 py-5">
          <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
            <div className="space-y-3">
              <Check checked={settings.alertsEnabled} onChange={v => set({ alertsEnabled: v })}>Generar avisos de consumo en el tablero</Check>
              <div className="flex items-center gap-3 pl-6">
                <label className="text-sm text-slate-600">Avisar al</label>
                <input type="number" min={1} max={99} value={settings.warnPercent} onChange={e => set({ warnPercent: Math.min(99, Math.max(1, parseInt(e.target.value) || 80)) })}
                  className="w-20 rounded-xl border border-slate-200 bg-slate-50 px-3 py-2 text-sm font-mono outline-none focus:ring-2 focus:ring-blue-500" />
                <span className="text-sm text-slate-600">% de lo incluido (y siempre al 100%)</span>
              </div>
              <Check checked={settings.emailTenant} onChange={v => set({ emailTenant: v })}>
                Enviarme un correo con cada aviso{settings.tenantEmail ? <span className="text-slate-400"> ({settings.tenantEmail})</span> : null}
              </Check>
              <Check checked={settings.emailClient} onChange={v => set({ emailClient: v })}>Enviar también el aviso al correo del cliente</Check>
            </div>
            <div className="space-y-4">
              <div>
                <label className="mb-1 block text-sm font-bold text-slate-700">Al llegar al límite de documentos incluidos</label>
                <SearchableSelect value={settings.defaultExceedAction} onChange={v => v && set({ defaultExceedAction: v as Settings['defaultExceedAction'] })} placeholder="Acción..."
                  options={Object.entries(EXCEED_LABELS).map(([value, label]) => ({ value, label }))} />
                <p className="mt-1 text-xs text-slate-400">
                  Los documentos adicionales se cobran siempre al precio del plan (si el precio es 0, no se cobran). Bloquear impide que el cliente entre y emita hasta el siguiente ciclo, y se levanta solo.
                  Cada plan, y cada cliente, pueden elegir otra opción.
                </p>
              </div>
              <Check checked={settings.showUsageToClients} onChange={v => set({ showUsageToClients: v })}>
                Mostrar al cliente su consumo de documentos en su portal (sin valores en dinero)
              </Check>
            </div>
          </div>
          <div className="flex justify-end">
            <button onClick={save} disabled={saving} className="rounded-xl bg-blue-600 px-5 py-2.5 text-sm font-bold text-white hover:bg-blue-700 disabled:opacity-50">{saving ? 'Guardando...' : 'Guardar preferencias'}</button>
          </div>
        </div>
      )}
    </div>
  );
}
