import { useCallback, useEffect, useState } from 'react';
import * as AlertDialog from '@radix-ui/react-alert-dialog';
import { Loader2 } from 'lucide-react';

export type DataicoResolutionState = 'loaded' | 'outdated' | 'removed' | 'new' | 'expired';

export interface DataicoResolution {
  documentType: string;
  prefix: string;
  number: string;
  start: number;
  end: number;
  startDate: string;
  endDate: string;
  state: DataicoResolutionState;
}

export interface DataicoResolutionsList {
  items: DataicoResolution[];
  errors: string[];
  notes: string[];
}

interface DataicoResolutionsDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  // Lo que Dataico tiene vinculado a la cuenta, cada resolución con su estado frente a FacilFactura.
  load: () => Promise<DataicoResolutionsList>;
  // Carga, actualiza o recarga una resolución; devuelve el mensaje de lo hecho o lanza con el motivo.
  loadOne: (item: DataicoResolution) => Promise<string>;
  // Se llama tras cargar algo, para que la pantalla recargue su listado de resoluciones.
  onChanged: () => void;
}

const TYPE_LABEL: Record<string, string> = { FE: 'Factura', POS: 'Factura POS', DS: 'Documento soporte' };

const STATE: Record<DataicoResolutionState, { label: string; badge: string; action?: string }> = {
  loaded: { label: 'Cargada', badge: 'bg-emerald-50 text-emerald-700' },
  outdated: { label: 'Distinta en Dataico', badge: 'bg-amber-50 text-amber-700', action: 'Actualizar' },
  removed: { label: 'Eliminada', badge: 'bg-rose-50 text-rose-700', action: 'Recargar' },
  new: { label: 'Nueva', badge: 'bg-blue-50 text-blue-700', action: 'Cargar' },
  expired: { label: 'Vencida', badge: 'bg-slate-100 text-slate-500' },
};

const BUTTON = 'inline-flex min-h-10 items-center justify-center gap-2 rounded-xl px-4 py-2 text-sm font-semibold transition-colors disabled:cursor-not-allowed disabled:opacity-50';
const dateText = (iso: string) => new Date(iso).toLocaleDateString('es-CO');
const keyOf = (r: DataicoResolution) => `${r.documentType}|${r.prefix}|${r.number}`;

// Resoluciones que Dataico tiene vinculadas a la cuenta del cliente: cada una con su estado en FacilFactura y, si hace falta, el botón para cargarla,
// recargarla (estaba eliminada) o actualizarla (Dataico trae otro rango o vigencia). En vez de rechazar las que ya se habían eliminado, se pueden recuperar.
export default function DataicoResolutionsDialog({ open, onOpenChange, load, loadOne, onChanged }: DataicoResolutionsDialogProps) {
  const [list, setList] = useState<DataicoResolutionsList | null>(null);
  const [loading, setLoading] = useState(false);
  const [working, setWorking] = useState<string | null>(null);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');

  const refresh = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      setList(await load());
    } catch (err: any) {
      setList(null);
      setError(err?.response?.data?.message || (typeof err?.response?.data === 'string' ? err.response.data : '') || 'No se pudo consultar Dataico.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (!open) return;
    setMessage('');
    refresh();
  }, [open, refresh]);

  const run = async (item: DataicoResolution) => {
    setWorking(keyOf(item));
    setError('');
    setMessage('');
    try {
      setMessage(await loadOne(item));
      onChanged();
      await refresh();
    } catch (err: any) {
      setError(err?.response?.data?.message || (typeof err?.response?.data === 'string' ? err.response.data : '') || 'No se pudo cargar la resolución.');
    } finally {
      setWorking(null);
    }
  };

  const items = list?.items ?? [];

  return (
    <AlertDialog.Root open={open} onOpenChange={v => { if (!working) onOpenChange(v); }}>
      <AlertDialog.Portal>
        <AlertDialog.Overlay className="fixed inset-0 z-[100] bg-slate-900/40 backdrop-blur-sm" />
        <AlertDialog.Content className="fixed left-1/2 top-1/2 z-[101] flex max-h-[85vh] w-[calc(100%-2rem)] max-w-2xl -translate-x-1/2 -translate-y-1/2 flex-col gap-4 rounded-2xl border border-slate-200 bg-white p-6 text-slate-800 shadow-2xl">
          <div className="space-y-1.5">
            <AlertDialog.Title className="text-lg font-bold text-slate-800">Resoluciones en Dataico</AlertDialog.Title>
            <AlertDialog.Description className="text-sm text-slate-500">
              Las resoluciones vigentes que Dataico tiene vinculadas a la cuenta de este cliente. Carga las nuevas y recarga las que se eliminaron por error.
            </AlertDialog.Description>
          </div>

          {message && <p role="status" className="rounded-xl border border-emerald-200 bg-emerald-50 px-3 py-2 text-sm text-emerald-800">{message}</p>}
          {error && <p role="alert" className="rounded-xl border border-rose-200 bg-rose-50 px-3 py-2 text-sm font-medium text-rose-700">{error}</p>}
          {list?.errors.map(e => <p key={e} role="alert" className="rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">{e}</p>)}

          {loading && !list ? (
            <div className="flex justify-center py-10"><Loader2 className="animate-spin" size={22} /></div>
          ) : items.length === 0 ? (
            !error && <p className="rounded-xl border border-dashed border-slate-200 py-8 text-center text-sm text-slate-500">Dataico no tiene resoluciones vigentes vinculadas a esta cuenta.</p>
          ) : (
            <ul className="min-h-0 flex-1 divide-y divide-slate-100 overflow-y-auto rounded-xl border border-slate-200">
              {items.map(item => {
                const state = STATE[item.state];
                const busy = working === keyOf(item);
                return (
                  <li key={keyOf(item)} className="flex flex-wrap items-center gap-x-4 gap-y-2 px-3 py-3" data-testid={`dataico-resolution-${item.prefix}-${item.number}`}>
                    <div className="min-w-0 flex-1">
                      <p className="text-sm font-semibold text-slate-800">
                        <span className="font-mono font-black">{item.prefix || '—'}</span> <span className="font-normal text-slate-500">· {TYPE_LABEL[item.documentType] ?? item.documentType}</span>
                      </p>
                      <p className="break-words text-xs text-slate-500">Resolución {item.number} · {item.start} a {item.end} · vence el {dateText(item.endDate)}</p>
                    </div>
                    <span className={`rounded-full px-2.5 py-1 text-xs font-bold ${state.badge}`}>{state.label}</span>
                    {state.action && (
                      <button
                        type="button" disabled={working !== null} onClick={() => run(item)}
                        aria-label={`${state.action} la resolución ${item.prefix} ${item.number}`}
                        className={`${BUTTON} border border-slate-200 bg-white text-slate-700 hover:bg-slate-50`}
                      >
                        {busy && <Loader2 className="animate-spin" size={16} />} {state.action}
                      </button>
                    )}
                  </li>
                );
              })}
            </ul>
          )}

          {list && list.notes.length > 0 && (
            <ul className="space-y-1 text-xs text-slate-500">{list.notes.map(n => <li key={n}>{n}</li>)}</ul>
          )}

          <div className="flex justify-end">
            <AlertDialog.Cancel asChild>
              <button type="button" disabled={working !== null} className={`${BUTTON} border border-slate-200 bg-white text-slate-700 hover:bg-slate-50`}>Cerrar</button>
            </AlertDialog.Cancel>
          </div>
        </AlertDialog.Content>
      </AlertDialog.Portal>
    </AlertDialog.Root>
  );
}
