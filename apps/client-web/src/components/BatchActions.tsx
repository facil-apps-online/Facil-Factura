import React from 'react';
import { AlertCircle, CheckCircle2, Loader2, Send, Trash2, X } from 'lucide-react';

import Modal from './Modal';
import { MAX_BATCH_SIZE, type BatchProgress } from '../lib/batch';

interface BatchActionBarProps {
  count: number;
  // Cada botón se habilita solo si todos los seleccionados pueden hacer esa acción.
  canPublish: boolean;
  canDelete: boolean;
  // Por qué no se puede, cuando algún botón queda deshabilitado.
  blockedReason?: string;
  disabled?: boolean;
  onPublish: () => void;
  onDelete: () => void;
  onClear: () => void;
}

// Barra de acciones masivas: aparece al seleccionar filas de la lista.
export function BatchActionBar({ count, canPublish, canDelete, blockedReason, disabled, onPublish, onDelete, onClear }: BatchActionBarProps) {
  if (count === 0) return null;
  const tooMany = count > MAX_BATCH_SIZE;
  const reason = tooMany ? `Puedes procesar hasta ${MAX_BATCH_SIZE} a la vez; quita ${count - MAX_BATCH_SIZE}.` : blockedReason;
  const button = 'flex min-h-11 items-center gap-2 rounded-xl px-4 text-sm font-bold transition-colors disabled:cursor-not-allowed disabled:opacity-50 md:min-h-10';

  return (
    <div className="mb-3 flex flex-wrap items-center gap-3 rounded-2xl border border-primary/30 bg-primary/5 px-4 py-3" role="region" aria-label="Acciones masivas">
      <span className="text-sm font-bold text-slate-700">{count} seleccionado{count === 1 ? '' : 's'}</span>
      <button type="button" onClick={onPublish} disabled={disabled || tooMany || !canPublish} className={`${button} bg-primary text-white shadow-sm hover:bg-primary/90`}>
        <Send size={16} /> Emitir ({count})
      </button>
      <button type="button" onClick={onDelete} disabled={disabled || tooMany || !canDelete} className={`${button} bg-rose-50 text-rose-600 hover:bg-rose-100`}>
        <Trash2 size={16} /> Eliminar ({count})
      </button>
      <button type="button" onClick={onClear} className={`${button} text-slate-500 hover:bg-slate-100`}>
        <X size={16} /> Quitar selección
      </button>
      {reason && <p className="w-full text-xs text-slate-500">{reason}</p>}
    </div>
  );
}

interface BatchProgressDialogProps {
  progress: BatchProgress | null;
  // Texto para identificar cada documento (número, empleado...).
  labelOf: (id: string) => string;
  // Singular y plural del documento: "factura" / "facturas".
  noun: { singular: string; plural: string };
  onClose: () => void;
}

// Avance y resultado del lote: un renglón por documento, a medida que el servidor lo termina.
export function BatchProgressDialog({ progress, labelOf, noun, onClose }: BatchProgressDialogProps) {
  if (!progress) return null;
  const deleting = progress.kind === 'delete';
  const done = progress.items.length;
  const ok = progress.items.filter(i => i.ok).length;
  const failed = done - ok;
  const percent = progress.total > 0 ? Math.round((done / progress.total) * 100) : 0;
  const verb = deleting ? 'eliminad' : 'emitid';
  const title = progress.finished ? 'Resultado' : deleting ? 'Eliminando' : 'Emitiendo';

  return (
    <Modal
      open
      onOpenChange={open => { if (!open) onClose(); }}
      title={`${title} ${progress.total === 1 ? noun.singular : noun.plural}`}
      description={progress.finished ? undefined : 'Se procesan de a uno. Puedes cerrar esta ventana: el proceso sigue en el servidor.'}
      size="md"
      preventDismiss={!progress.finished}
      footer={<button type="button" onClick={onClose} className="min-h-11 rounded-xl bg-slate-100 px-5 text-sm font-bold text-slate-700 hover:bg-slate-200 md:min-h-10">{progress.finished ? 'Cerrar' : 'Cerrar y seguir en segundo plano'}</button>}
    >
      <div className="space-y-4">
        <div>
          <div className="mb-1 flex items-center justify-between text-sm font-semibold text-slate-700">
            <span>{done} de {progress.total}</span>
            {!progress.finished && <Loader2 size={16} className="animate-spin text-primary" />}
          </div>
          <div className="h-2 overflow-hidden rounded-full bg-slate-100" role="progressbar" aria-valuemin={0} aria-valuemax={100} aria-valuenow={percent}>
            <div className="h-full bg-primary transition-all" style={{ width: `${percent}%` }} />
          </div>
        </div>

        {progress.error && (
          <p className="rounded-xl bg-rose-50 p-3 text-sm text-rose-600">
            {progress.error} Revisa la lista: los documentos que alcanzaron a procesarse ya muestran su estado.
          </p>
        )}

        {progress.finished && !progress.error && (
          <p className="text-sm font-semibold text-slate-700">
            {ok} {verb}{ok === 1 ? 'o' : 'os'}{failed > 0 && <span className="text-rose-600"> · {failed} con problema{failed === 1 ? '' : 's'}</span>}
          </p>
        )}

        <ul className="divide-y divide-slate-100 rounded-xl border border-slate-200">
          {progress.items.map(item => (
            <li key={item.id} className="flex items-start gap-3 p-3 text-sm">
              {item.ok ? <CheckCircle2 size={18} className="mt-0.5 shrink-0 text-emerald-600" /> : <AlertCircle size={18} className="mt-0.5 shrink-0 text-rose-500" />}
              <div className="min-w-0">
                <p className="break-words font-semibold text-slate-700">{labelOf(item.id)}</p>
                <p className={`break-words ${item.ok ? 'text-slate-500' : 'text-rose-600'}`}>{item.message}</p>
                {item.detail && <p className="mt-1 break-words text-xs text-slate-500">{item.detail}</p>}
              </div>
            </li>
          ))}
          {progress.items.length === 0 && <li className="p-3 text-sm text-slate-400">Esperando al servidor...</li>}
        </ul>
      </div>
    </Modal>
  );
}
