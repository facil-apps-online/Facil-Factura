import { useCallback, useRef, useState } from 'react';
import { postNdjson } from './api';

// Las acciones masivas del portal (emitir o eliminar varios documentos a la vez): el servidor las procesa de a uno, en orden, y va
// avisando cada resultado apenas termina. Lo usan facturas, documentos soporte y nómina.
export type BatchKind = 'publish' | 'delete';

// Mismo tope que el servidor (ClientPortalControllerBase.MaxBatchSize).
export const MAX_BATCH_SIZE = 50;

// Un documento solo se emite o se elimina mientras sea borrador o haya sido rechazado.
export const isBatchEditable = (status: string) => status === 'DRAFT' || status === 'REJECTED';

export interface BatchItemResult {
  id: string;
  ok: boolean;
  message: string;
  detail?: string | null;
}

export interface BatchProgress {
  kind: BatchKind;
  total: number;
  items: BatchItemResult[];
  finished: boolean;
  // Falla de la conexión o del servidor antes de terminar (no un rechazo de un documento).
  error?: string;
}

type BatchEvent =
  | { type: 'start'; total: number }
  | ({ type: 'item' } & BatchItemResult)
  | { type: 'done'; succeeded: number; failed: number };

export function useBatchRunner(basePath: string, onFinished: (failedIds: string[]) => void) {
  const [progress, setProgress] = useState<BatchProgress | null>(null);
  const running = useRef(false);

  const run = useCallback(async (kind: BatchKind, ids: string[]) => {
    if (running.current) return;
    running.current = true;
    const failed: string[] = [];
    setProgress({ kind, total: ids.length, items: [], finished: false });
    try {
      await postNdjson<BatchEvent>(`${basePath}/batch/${kind}`, { ids }, event => {
        if (event.type === 'start') {
          setProgress(p => (p ? { ...p, total: event.total } : p));
        } else if (event.type === 'item') {
          if (!event.ok) failed.push(event.id);
          setProgress(p => (p ? { ...p, items: [...p.items, event] } : p));
        }
      });
      setProgress(p => (p ? { ...p, finished: true } : p));
    } catch (err) {
      setProgress(p => (p ? { ...p, finished: true, error: err instanceof Error ? err.message : 'No se pudo completar el proceso.' } : p));
    } finally {
      running.current = false;
      onFinished(failed);
    }
  }, [basePath, onFinished]);

  return { progress, run, close: () => setProgress(null) };
}

// Selección de filas para las acciones masivas. Se conserva al cambiar de página; "todas" aplica a las filas visibles.
export function useBatchSelection() {
  const [selected, setSelected] = useState<Set<string>>(new Set());

  const toggle = useCallback((id: string) => {
    setSelected(prev => {
      const next = new Set(prev);
      if (!next.delete(id)) next.add(id);
      return next;
    });
  }, []);

  const toggleMany = useCallback((ids: string[]) => {
    setSelected(prev => {
      const next = new Set(prev);
      const allIn = ids.length > 0 && ids.every(id => next.has(id));
      ids.forEach(id => (allIn ? next.delete(id) : next.add(id)));
      return next;
    });
  }, []);

  const replace = useCallback((ids: string[]) => setSelected(new Set(ids)), []);
  const clear = useCallback(() => setSelected(new Set()), []);

  return { selected, toggle, toggleMany, replace, clear };
}
