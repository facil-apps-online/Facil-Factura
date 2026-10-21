import React, { useEffect, useMemo, useState } from 'react';
import { Loader2, X } from 'lucide-react';
import { toast } from 'sonner';
import { api, getErrorMessage } from '../lib/api';

interface SyncItem {
  clientId: string;
  name: string;
  taxId: string;
  state: 'ToCreate' | 'ToComplete' | 'UpToDate' | 'Review';
  fills: string[];
  missing: string[];
}

interface SyncPreview {
  items: SyncItem[];
  summary: { toCreate: number; toComplete: number; upToDate: number; review: number; incomplete: number };
}

const STATE_LABELS: Record<SyncItem['state'], { text: string; style: string }> = {
  ToCreate: { text: 'Por crear', style: 'bg-emerald-50 text-emerald-700' },
  ToComplete: { text: 'Por completar', style: 'bg-blue-50 text-blue-700' },
  UpToDate: { text: 'Al día', style: 'bg-slate-100 text-slate-500' },
  Review: { text: 'Revisar', style: 'bg-amber-50 text-amber-700' },
};

interface TenantSyncModalProps {
  open: boolean;
  onClose: () => void;
  onDone: () => void;
}

// Sincroniza los clientes del tenant como terceros tipo Cliente de esta empresa (la propietaria del tenant). Muestra primero qué haría: crear los que
// faltan, completar solo lo vacío de los que ya existen, y dejar para revisión los que tienen el mismo NIT con otro nombre. Nunca pisa lo ya escrito.
export default function TenantSyncModal({ open, onClose, onDone }: TenantSyncModalProps) {
  const [preview, setPreview] = useState<SyncPreview | null>(null);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [applying, setApplying] = useState(false);
  const [filter, setFilter] = useState<'all' | SyncItem['state'] | 'incomplete'>('all');

  useEffect(() => {
    if (!open) return;
    setPreview(null);
    setFilter('all');
    api.get<SyncPreview>('/client/customers/tenant-sync/preview')
      .then(res => {
        setPreview(res.data);
        setSelected(new Set(res.data.items.filter(i => i.state === 'ToCreate' || i.state === 'ToComplete').map(i => i.clientId)));
      })
      .catch(err => { toast.error(getErrorMessage(err, 'No se pudo cargar la vista previa')); onClose(); });
  }, [open]);

  const rows = useMemo(() => (preview?.items ?? []).filter(i => filter === 'all' ? true : filter === 'incomplete' ? i.missing.length > 0 : i.state === filter), [preview, filter]);

  if (!open) return null;

  const toggle = (id: string) => setSelected(prev => { const next = new Set(prev); next.has(id) ? next.delete(id) : next.add(id); return next; });
  const selectable = rows.filter(i => i.state === 'ToCreate' || i.state === 'ToComplete');
  const allSelected = selectable.length > 0 && selectable.every(i => selected.has(i.clientId));
  const toggleAll = () => setSelected(prev => {
    const next = new Set(prev);
    selectable.forEach(i => (allSelected ? next.delete(i.clientId) : next.add(i.clientId)));
    return next;
  });

  const apply = async () => {
    if (selected.size === 0) return;
    setApplying(true);
    try {
      const res = await api.post('/client/customers/tenant-sync/apply', { clientIds: Array.from(selected) });
      toast.success(`${res.data.created} tercero(s) creado(s) y ${res.data.completed} completado(s).`);
      onDone();
      onClose();
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo sincronizar'));
    } finally {
      setApplying(false);
    }
  };

  const s = preview?.summary;
  const chip = (key: typeof filter, label: string, count: number) => (
    <button key={key} onClick={() => setFilter(key)} className={`rounded-full px-3 py-1.5 text-xs font-bold ${filter === key ? 'bg-slate-900 text-white' : 'bg-slate-100 text-slate-600 hover:bg-slate-200'}`}>
      {label} ({count})
    </button>
  );

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-3 sm:p-4">
      <div className="flex max-h-[92dvh] w-full max-w-5xl flex-col overflow-hidden rounded-2xl bg-white shadow-2xl">
        <div className="flex items-center justify-between border-b border-slate-100 px-5 py-4">
          <div>
            <h2 className="text-lg font-bold text-slate-900">Sincronizar clientes del tenant</h2>
            <p className="text-sm text-slate-500">Crea como terceros tipo Cliente a los clientes de tu tenant y completa solo lo que esté vacío. No cambia lo que ya escribiste.</p>
          </div>
          <button onClick={onClose} aria-label="Cerrar" className="rounded-lg p-1.5 text-slate-400 hover:bg-slate-100"><X size={18} /></button>
        </div>

        {!preview ? (
          <div className="flex justify-center p-12"><Loader2 className="animate-spin text-slate-400" /></div>
        ) : (
          <>
            <div className="flex flex-wrap items-center gap-2 border-b border-slate-100 px-5 py-3">
              {chip('all', 'Todos', preview.items.length)}
              {chip('ToCreate', 'Por crear', s!.toCreate)}
              {chip('ToComplete', 'Por completar', s!.toComplete)}
              {chip('UpToDate', 'Al día', s!.upToDate)}
              {chip('Review', 'Revisar', s!.review)}
              {chip('incomplete', 'Con datos incompletos', s!.incomplete)}
            </div>

            <div className="flex-1 overflow-auto">
              <table className="w-full text-left text-sm">
                <thead className="sticky top-0 bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
                  <tr>
                    <th className="w-10 px-4 py-2"><input type="checkbox" checked={allSelected} onChange={toggleAll} aria-label="Seleccionar todos" className="h-4 w-4 accent-blue-600" /></th>
                    <th className="px-3 py-2 font-bold">Cliente</th>
                    <th className="px-3 py-2 font-bold">NIT</th>
                    <th className="px-3 py-2 font-bold">Estado</th>
                    <th className="px-3 py-2 font-bold">Detalle</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.length === 0 && <tr><td colSpan={5} className="px-5 py-10 text-center text-slate-500">No hay clientes en este filtro.</td></tr>}
                  {rows.map(i => {
                    const canSelect = i.state === 'ToCreate' || i.state === 'ToComplete';
                    return (
                      <tr key={i.clientId} className="border-t border-slate-100">
                        <td className="px-4 py-2">{canSelect && <input type="checkbox" checked={selected.has(i.clientId)} onChange={() => toggle(i.clientId)} aria-label={`Seleccionar ${i.name}`} className="h-4 w-4 accent-blue-600" />}</td>
                        <td className="px-3 py-2 font-semibold text-slate-800">{i.name}</td>
                        <td className="whitespace-nowrap px-3 py-2 font-mono text-slate-600">{i.taxId}</td>
                        <td className="px-3 py-2"><span className={`rounded-full px-2.5 py-1 text-xs font-semibold ${STATE_LABELS[i.state].style}`}>{STATE_LABELS[i.state].text}</span></td>
                        <td className="px-3 py-2 text-xs text-slate-500">
                          {i.state === 'ToComplete' && <span>Se llenará: {i.fills.join(', ')}. </span>}
                          {i.state === 'Review' && <span className="text-amber-700">Ya existe un tercero con este NIT y otro nombre: revísalo a mano, no se toca. </span>}
                          {i.missing.length > 0 && <span className="font-semibold text-amber-700">Falta: {i.missing.join(', ')}.</span>}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>

            <div className="flex flex-wrap items-center justify-between gap-3 border-t border-slate-100 px-5 py-4">
              <span className="text-sm text-slate-500">{selected.size} seleccionado(s)</span>
              <div className="flex gap-3">
                <button onClick={onClose} className="rounded-xl bg-slate-100 px-4 py-2.5 text-sm font-bold text-slate-600 hover:bg-slate-200">Cerrar</button>
                <button onClick={apply} disabled={applying || selected.size === 0} className="rounded-xl bg-blue-600 px-5 py-2.5 text-sm font-bold text-white hover:bg-blue-700 disabled:opacity-50">
                  {applying ? 'Sincronizando...' : `Sincronizar ${selected.size}`}
                </button>
              </div>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
