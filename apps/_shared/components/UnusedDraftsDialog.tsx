import { useEffect, useState } from 'react';
import * as AlertDialog from '@radix-ui/react-alert-dialog';
import { Archive, Loader2, Trash2 } from 'lucide-react';

export interface UnusedDraft {
  id: string;
  name: string;
  documentType: string;
  createdAt: string;
}

interface UnusedDraftsDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  // 'dark' para el portal de superadmin; los demás son claros.
  theme?: 'light' | 'dark';
  load: () => Promise<UnusedDraft[]>;
  onDelete: (ids: string[]) => Promise<number>;
  onArchive: (ids: string[]) => Promise<number>;
  // Se llama al terminar con lo que se hizo, para que la pantalla avise y recargue su listado.
  onDone: (action: 'delete' | 'archive', count: number) => void;
}

const THEMES = {
  light: {
    overlay: 'bg-slate-900/40',
    content: 'bg-white text-slate-800 border border-slate-200',
    title: 'text-slate-800',
    muted: 'text-slate-500',
    list: 'border-slate-200 divide-slate-100',
    row: 'hover:bg-slate-50',
    cancel: 'border border-slate-200 bg-white text-slate-700 hover:bg-slate-50',
    secondary: 'border border-slate-200 bg-white text-slate-700 hover:bg-slate-50',
    warn: 'bg-amber-50 border-amber-200 text-amber-800',
  },
  dark: {
    overlay: 'bg-black/60',
    content: 'bg-slate-900 text-slate-100 border border-slate-700',
    title: 'text-slate-100',
    muted: 'text-slate-400',
    list: 'border-slate-700 divide-slate-800',
    row: 'hover:bg-slate-800/60',
    cancel: 'border border-slate-700 bg-slate-800 text-slate-200 hover:bg-slate-700',
    secondary: 'border border-slate-700 bg-slate-800 text-slate-200 hover:bg-slate-700',
    warn: 'bg-amber-500/10 border-amber-500/30 text-amber-300',
  },
} as const;

const BUTTON = 'inline-flex min-h-10 items-center justify-center gap-2 rounded-xl px-4 py-2 text-sm font-semibold transition-colors disabled:cursor-not-allowed disabled:opacity-50';

// Limpieza de borradores de plantilla que nunca se usaron: lista lo que se va a tocar (con casillas para quitar alguno) y ofrece
// Archivar (el registro queda pero no se puede asignar de nuevo) o Eliminar (definitivo). Antes de actuar pide una confirmación
// dentro del mismo cuadro. Sin ventanas del navegador.
export default function UnusedDraftsDialog({ open, onOpenChange, theme = 'light', load, onDelete, onArchive, onDone }: UnusedDraftsDialogProps) {
  const k = THEMES[theme];
  const [drafts, setDrafts] = useState<UnusedDraft[]>([]);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [loading, setLoading] = useState(false);
  const [working, setWorking] = useState(false);
  const [error, setError] = useState('');
  const [confirming, setConfirming] = useState<'delete' | 'archive' | null>(null);

  useEffect(() => {
    if (!open) return;
    let cancelled = false;
    setLoading(true);
    setError('');
    setConfirming(null);
    load()
      .then(list => {
        if (cancelled) return;
        setDrafts(list);
        setSelected(new Set(list.map(d => d.id)));
      })
      .catch(() => { if (!cancelled) setError('No se pudo consultar los borradores.'); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  const toggle = (id: string) => {
    setConfirming(null);
    setSelected(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
  };
  const allSelected = drafts.length > 0 && selected.size === drafts.length;
  const toggleAll = () => {
    setConfirming(null);
    setSelected(allSelected ? new Set() : new Set(drafts.map(d => d.id)));
  };

  const run = async (action: 'delete' | 'archive') => {
    setWorking(true);
    setError('');
    try {
      const ids = Array.from(selected);
      const count = await (action === 'delete' ? onDelete(ids) : onArchive(ids));
      onOpenChange(false);
      onDone(action, count);
    } catch {
      setError(action === 'delete' ? 'No se pudieron eliminar los borradores.' : 'No se pudieron archivar los borradores.');
    } finally {
      setWorking(false);
      setConfirming(null);
    }
  };

  const count = selected.size;
  const plural = (n: number) => (n === 1 ? 'plantilla' : 'plantillas');

  return (
    <AlertDialog.Root open={open} onOpenChange={v => { if (!working) onOpenChange(v); }}>
      <AlertDialog.Portal>
        <AlertDialog.Overlay className={`fixed inset-0 z-[100] backdrop-blur-sm ${k.overlay}`} />
        <AlertDialog.Content className={`fixed left-1/2 top-1/2 z-[101] flex max-h-[85vh] w-[calc(100%-2rem)] max-w-lg -translate-x-1/2 -translate-y-1/2 flex-col gap-4 rounded-2xl p-6 shadow-2xl ${k.content}`}>
          <div className="space-y-1.5">
            <AlertDialog.Title className={`text-lg font-bold ${k.title}`}>Limpiar borradores sin uso</AlertDialog.Title>
            <AlertDialog.Description className={`text-sm ${k.muted}`}>
              Borradores que ningún cliente tiene seleccionado, con los que no se ha emitido ningún documento y de los que no depende otra plantilla.
              Archivarlos los deja fuera de las asignaciones; eliminarlos los borra del todo.
            </AlertDialog.Description>
          </div>

          {loading ? (
            <div className="flex justify-center py-10"><Loader2 className="animate-spin" size={22} /></div>
          ) : drafts.length === 0 ? (
            <p className={`rounded-xl border border-dashed py-8 text-center text-sm ${k.list} ${k.muted}`}>No hay borradores sin uso.</p>
          ) : (
            <div className="min-h-0 flex-1 space-y-2 overflow-y-auto">
              <label className={`flex cursor-pointer items-center gap-2 px-1 text-xs font-semibold ${k.muted}`}>
                <input type="checkbox" checked={allSelected} onChange={toggleAll} />
                Seleccionar todos ({selected.size} de {drafts.length})
              </label>
              <ul className={`divide-y rounded-xl border ${k.list}`}>
                {drafts.map(d => (
                  <li key={d.id}>
                    <label className={`flex cursor-pointer items-start gap-3 px-3 py-2.5 ${k.row}`}>
                      <input type="checkbox" className="mt-1" checked={selected.has(d.id)} onChange={() => toggle(d.id)} />
                      <span className="min-w-0 flex-1">
                        <span className="block break-words text-sm font-medium">{d.name}</span>
                        <span className={`block text-xs ${k.muted}`}>{d.documentType} · creada el {new Date(d.createdAt).toLocaleDateString('es-CO')}</span>
                      </span>
                    </label>
                  </li>
                ))}
              </ul>
            </div>
          )}

          {error && <p role="alert" className="text-sm font-medium text-rose-500">{error}</p>}

          {confirming && count > 0 && (
            <div className={`rounded-xl border px-3 py-2.5 text-sm ${k.warn}`} role="alert">
              {confirming === 'delete'
                ? <>¿Eliminar {count} {plural(count)} de forma definitiva? <b>No se puede deshacer.</b></>
                : <>¿Archivar {count} {plural(count)}? Quedarán fuera de las asignaciones y <b>no se podrán publicar de nuevo</b>.</>}
            </div>
          )}

          <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
            {confirming ? (
              <>
                <button type="button" className={`${BUTTON} ${k.cancel}`} disabled={working} onClick={() => setConfirming(null)}>Volver</button>
                <button
                  type="button"
                  disabled={working}
                  onClick={() => run(confirming)}
                  className={`${BUTTON} ${confirming === 'delete' ? 'bg-rose-600 text-white hover:bg-rose-700' : 'bg-blue-600 text-white hover:bg-blue-700'}`}
                >
                  {working && <Loader2 className="animate-spin" size={16} />}
                  {confirming === 'delete' ? 'Sí, eliminar' : 'Sí, archivar'}
                </button>
              </>
            ) : (
              <>
                <AlertDialog.Cancel asChild>
                  <button type="button" className={`${BUTTON} ${k.cancel}`}>Cancelar</button>
                </AlertDialog.Cancel>
                <button type="button" disabled={loading || count === 0} onClick={() => setConfirming('archive')} className={`${BUTTON} ${k.secondary}`}>
                  <Archive size={16} /> Archivar{count > 0 ? ` (${count})` : ''}
                </button>
                <button type="button" disabled={loading || count === 0} onClick={() => setConfirming('delete')} className={`${BUTTON} bg-rose-600 text-white hover:bg-rose-700`}>
                  <Trash2 size={16} /> Eliminar{count > 0 ? ` (${count})` : ''}
                </button>
              </>
            )}
          </div>
        </AlertDialog.Content>
      </AlertDialog.Portal>
    </AlertDialog.Root>
  );
}
