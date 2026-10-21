import { useRef, useState } from 'react';
import { CheckCircle2, Download, FileSpreadsheet, Loader2, Upload } from 'lucide-react';
import { toast } from 'sonner';
import SearchableSelect from '@shared/components/SearchableSelect';
import { api, getErrorMessage } from '../lib/api';
import { exportToCsv } from '../lib/exportCsv';
import Modal from './Modal';

interface Decision {
  field: string;
  label: string;
  options: Array<{ value: string; label: string }>;
  suggested?: string | null;
}

interface PreviewRow {
  row: number;
  status: 'create' | 'duplicate' | 'error' | 'decision' | 'skipped';
  errors: string[];
  warnings: string[];
  notes: string[];
  values: Record<string, string | null>;
  decision?: Decision | null;
}

interface Preview {
  totalRows: number;
  toCreate: number;
  duplicates: number;
  errors: number;
  needsDecision: number;
  skipped: number;
  withWarnings: number;
  fileErrors: string[];
  rows: PreviewRow[];
}

interface ImportOptions {
  addressMode: 'client' | 'default' | 'blank';
  decisions: Record<number, Record<string, string>>;
}

interface BulkImportModalProps {
  title: string;
  // "terceros" o "productos": se usa en los textos.
  entityLabel: string;
  // Ruta del portal que atiende la carga: /client/customers o /client/products.
  endpoint: string;
  // Parámetros fijos de la consulta (p. ej. scope=Invoice).
  query?: string;
  // Terceros: permite elegir qué hacer con las filas sin dirección o ciudad.
  addressOptions?: boolean;
  onDone: () => void;
  onClose: () => void;
}

const STATUS_BADGE: Record<PreviewRow['status'], { label: string; className: string }> = {
  create: { label: 'Se crea', className: 'bg-emerald-100 text-emerald-700' },
  duplicate: { label: 'Ya existe', className: 'bg-slate-100 text-slate-600' },
  error: { label: 'Error', className: 'bg-rose-100 text-rose-700' },
  decision: { label: 'Decidir', className: 'bg-amber-100 text-amber-700' },
  skipped: { label: 'Omitida', className: 'bg-slate-100 text-slate-500' }
};

type Filter = 'all' | 'create' | 'warning' | 'decision' | 'duplicate' | 'error';
const PAGE_SIZE = 50;
// El cliente HTTP del portal manda JSON por defecto: al subir un archivo hay que pedir multipart (sin esto el servidor responde 415).
const MULTIPART = { headers: { 'Content-Type': 'multipart/form-data' } };

const ADDRESS_MODES = [
  { value: 'blank', label: 'Dejar en blanco' },
  { value: 'client', label: 'Usar la dirección y ciudad del cliente' },
  { value: 'default', label: 'Usar la dirección por defecto para terceros nuevos' }
];

// Carga masiva desde Excel en tres pasos: descargar la plantilla y elegir el archivo, ver la vista previa (sin guardar nada) y confirmar.
// Lo usan terceros y productos. El servidor valida el archivo completo en la vista previa y lo vuelve a validar al confirmar.
export default function BulkImportModal({ title, entityLabel, endpoint, query, addressOptions, onDone, onClose }: BulkImportModalProps) {
  const fileInput = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [options, setOptions] = useState<ImportOptions>({ addressMode: 'blank', decisions: {} });
  const [preview, setPreview] = useState<Preview | null>(null);
  const [busy, setBusy] = useState<'preview' | 'confirm' | 'template' | null>(null);
  const [result, setResult] = useState<{ created: number; skipped: number } | null>(null);
  const [filter, setFilter] = useState<Filter>('all');
  const [page, setPage] = useState(0);

  const url = (path: string) => `${endpoint}${path}${query ? `?${query}` : ''}`;
  const formFor = (f: File, o: ImportOptions) => {
    const form = new FormData();
    form.append('file', f);
    form.append('options', JSON.stringify(o));
    return form;
  };

  const downloadTemplate = async () => {
    setBusy('template');
    try {
      const res = await api.get(url('/template'), { responseType: 'blob' });
      const href = window.URL.createObjectURL(new Blob([res.data]));
      const a = document.createElement('a');
      a.href = href;
      a.download = `plantilla_${entityLabel}.xlsx`;
      a.click();
      window.URL.revokeObjectURL(href);
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo descargar la plantilla'));
    } finally {
      setBusy(null);
    }
  };

  const runPreview = async (f: File, o: ImportOptions) => {
    setBusy('preview');
    try {
      const res = await api.post<Preview>(url('/import/preview'), formFor(f, o), MULTIPART);
      setPreview(res.data);
      setPage(0);
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo revisar el archivo'));
    } finally {
      setBusy(null);
    }
  };

  const onFile = (f: File | null) => {
    if (!f) return;
    const fresh: ImportOptions = { addressMode: options.addressMode, decisions: {} };
    setFile(f);
    setOptions(fresh);
    setResult(null);
    setFilter('all');
    runPreview(f, fresh);
  };

  const changeOptions = (next: ImportOptions) => {
    setOptions(next);
    if (file) runPreview(file, next);
  };

  const decide = (row: number, field: string, value: string) => {
    const decisions = { ...options.decisions };
    decisions[row] = { ...(decisions[row] || {}), [field]: value };
    changeOptions({ ...options, decisions });
  };

  const confirm = async () => {
    if (!file || !preview) return;
    setBusy('confirm');
    try {
      const res = await api.post<{ created: number; skipped: number }>(url('/import/confirm'), formFor(file, options), MULTIPART);
      setResult(res.data);
      onDone();
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo completar la carga'));
    } finally {
      setBusy(null);
    }
  };

  const rows = preview?.rows ?? [];
  const matches = (r: PreviewRow) => {
    switch (filter) {
      case 'create': return r.status === 'create';
      case 'warning': return r.status === 'create' && r.warnings.length > 0;
      case 'decision': return r.status === 'decision';
      case 'duplicate': return r.status === 'duplicate';
      case 'error': return r.status === 'error';
      default: return true;
    }
  };
  const filtered = rows.filter(matches);
  const pages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  const pageRows = filtered.slice(page * PAGE_SIZE, (page + 1) * PAGE_SIZE);
  const columns = rows.length > 0 ? Object.keys(rows[0].values) : [];

  const exportReport = () => {
    if (!preview) return;
    exportToCsv(`revision_carga_${entityLabel}.csv`,
      preview.rows.map(r => ({ fila: r.row, estado: STATUS_BADGE[r.status].label, ...r.values, detalle: [...r.errors, ...r.warnings, ...r.notes].join(' | ') })),
      [{ key: 'fila', label: 'Fila' }, { key: 'estado', label: 'Estado' }, ...columns.map(c => ({ key: c, label: c })), { key: 'detalle', label: 'Detalle' }]);
  };

  const chips: Array<{ key: Filter; label: string; count: number; className: string }> = preview ? [
    { key: 'all', label: 'Todas', count: preview.totalRows, className: 'border-slate-300 text-slate-700' },
    { key: 'create', label: 'Se crean', count: preview.toCreate, className: 'border-emerald-300 text-emerald-700' },
    { key: 'warning', label: 'Con aviso', count: preview.withWarnings, className: 'border-amber-300 text-amber-700' },
    { key: 'decision', label: 'Por decidir', count: preview.needsDecision, className: 'border-amber-300 text-amber-700' },
    { key: 'duplicate', label: 'Ya existen', count: preview.duplicates, className: 'border-slate-300 text-slate-600' },
    { key: 'error', label: 'Con error', count: preview.errors, className: 'border-rose-300 text-rose-700' }
  ] : [];

  const footer = result ? (
    <button onClick={onClose} className="rounded-xl bg-primary px-5 py-2.5 font-bold text-white hover:bg-primary/90">Cerrar</button>
  ) : (
    <div className="flex w-full flex-wrap items-center justify-between gap-3">
      <span className="text-sm text-slate-500">
        {preview && preview.fileErrors.length === 0 && preview.needsDecision > 0 && `${preview.needsDecision} fila(s) por decidir: si no eliges, se omiten.`}
      </span>
      <div className="flex gap-2">
        <button onClick={onClose} className="rounded-xl border border-slate-200 px-5 py-2.5 font-bold text-slate-600 hover:bg-slate-50">Cancelar</button>
        <button
          onClick={confirm}
          disabled={!preview || preview.fileErrors.length > 0 || preview.toCreate === 0 || busy !== null}
          className="flex items-center gap-2 rounded-xl bg-primary px-5 py-2.5 font-bold text-white hover:bg-primary/90 disabled:opacity-50"
        >
          {busy === 'confirm' && <Loader2 size={16} className="animate-spin" />}
          Cargar {preview?.toCreate ?? 0} {entityLabel}
        </button>
      </div>
    </div>
  );

  return (
    <Modal open onOpenChange={o => { if (!o) onClose(); }} title={title} size="xl" footer={footer} withFloatingPickers preventDismiss={!!preview && !result}>
      <div className="space-y-5">
        {result ? (
          <div className="flex flex-col items-center gap-3 py-8 text-center">
            <CheckCircle2 size={44} className="text-emerald-600" />
            <p className="text-lg font-bold text-slate-800">Se cargaron {result.created} {entityLabel}</p>
            {result.skipped > 0 && <p className="text-sm text-slate-500">{result.skipped} fila(s) se omitieron (ya existían, tenían errores o no se decidieron).</p>}
          </div>
        ) : (
          <>
            <div className="flex flex-wrap items-center gap-3">
              <button onClick={downloadTemplate} disabled={busy !== null} className="flex items-center gap-2 rounded-xl border border-slate-200 px-4 py-2.5 text-sm font-bold text-slate-700 hover:bg-slate-50 disabled:opacity-50">
                {busy === 'template' ? <Loader2 size={16} className="animate-spin" /> : <Download size={16} />} Descargar plantilla
              </button>
              <input ref={fileInput} type="file" accept=".xlsx" className="hidden" onChange={e => { onFile(e.target.files?.[0] ?? null); e.target.value = ''; }} />
              <button onClick={() => fileInput.current?.click()} disabled={busy !== null} className="flex items-center gap-2 rounded-xl bg-primary px-4 py-2.5 text-sm font-bold text-white hover:bg-primary/90 disabled:opacity-50">
                {busy === 'preview' ? <Loader2 size={16} className="animate-spin" /> : <Upload size={16} />} {file ? 'Cambiar archivo' : 'Elegir archivo Excel'}
              </button>
              {file && <span className="flex min-w-0 items-center gap-1.5 text-sm text-slate-600"><FileSpreadsheet size={16} className="shrink-0" /><span className="truncate">{file.name}</span></span>}
            </div>
            {!file && (
              <p className="text-sm text-slate-500">
                Descarga la plantilla, llénala (trae dos filas de ejemplo y una hoja de instrucciones) y súbela. Primero verás una vista previa de lo que se va a cargar; nada se guarda hasta que confirmes.
              </p>
            )}

            {preview && preview.fileErrors.length > 0 && (
              <div role="alert" className="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">
                {preview.fileErrors.map((e, i) => <p key={i}>{e}</p>)}
              </div>
            )}

            {preview && preview.fileErrors.length === 0 && (
              <>
                {addressOptions && (
                  <div className="max-w-md">
                    <label className="mb-1 block text-xs font-bold text-slate-500">Si a un tercero le falta la dirección o la ciudad</label>
                    <SearchableSelect
                      options={ADDRESS_MODES}
                      value={options.addressMode}
                      onChange={v => changeOptions({ ...options, addressMode: v as ImportOptions['addressMode'] })}
                      placeholder="Elige una opción"
                      inputClassName="w-full px-3 py-2 bg-white border border-slate-200 rounded-xl text-sm outline-none focus:ring-2 focus:ring-primary"
                    />
                  </div>
                )}

                <div className="flex flex-wrap items-center gap-2">
                  {chips.map(c => (
                    <button
                      key={c.key}
                      onClick={() => { setFilter(c.key); setPage(0); }}
                      className={`rounded-full border px-3 py-1 text-sm font-semibold ${c.className} ${filter === c.key ? 'bg-slate-100 ring-2 ring-primary/40' : 'bg-white'}`}
                    >
                      {c.label} <span className="font-mono">{c.count}</span>
                    </button>
                  ))}
                  <button onClick={exportReport} className="ml-auto flex items-center gap-1.5 text-sm font-medium text-slate-500 underline hover:text-slate-700"><Download size={14} /> Descargar revisión</button>
                </div>

                <div className="overflow-x-auto rounded-xl border border-slate-200">
                  <table className="w-full min-w-[900px] text-left text-sm">
                    <thead className="bg-slate-50 text-xs font-bold uppercase text-slate-500">
                      <tr>
                        <th className="px-3 py-2">Fila</th>
                        <th className="px-3 py-2">Estado</th>
                        {columns.map(c => <th key={c} className="px-3 py-2">{c}</th>)}
                        <th className="px-3 py-2">Detalle</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-100">
                      {pageRows.length === 0 && <tr><td colSpan={columns.length + 3} className="px-3 py-6 text-center text-slate-400">No hay filas en este filtro.</td></tr>}
                      {pageRows.map(r => (
                        <tr key={r.row} className={r.status === 'error' ? 'bg-rose-50/40' : r.status === 'decision' ? 'bg-amber-50/50' : ''}>
                          <td className="px-3 py-2 font-mono text-xs text-slate-400">{r.row}</td>
                          <td className="px-3 py-2"><span className={`whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-bold ${STATUS_BADGE[r.status].className}`}>{STATUS_BADGE[r.status].label}</span></td>
                          {columns.map(c => <td key={c} className="max-w-[240px] truncate px-3 py-2 text-slate-700" title={r.values[c] ?? ''}>{r.values[c] ?? <span className="text-slate-300">—</span>}</td>)}
                          <td className="min-w-[260px] px-3 py-2 text-xs">
                            {r.errors.map((m, i) => <p key={`e${i}`} className="text-rose-600">{m}</p>)}
                            {r.warnings.map((m, i) => <p key={`w${i}`} className="text-amber-700">{m}</p>)}
                            {r.notes.map((m, i) => <p key={`n${i}`} className="text-slate-500">{m}</p>)}
                            {r.decision && (
                              <div className="mt-1 w-64">
                                <p className="mb-1 font-semibold text-amber-800">{r.decision.label}: ¿qué hacemos?</p>
                                <SearchableSelect
                                  options={r.decision.options}
                                  value={options.decisions[r.row]?.[r.decision.field] ?? ''}
                                  onChange={v => decide(r.row, r.decision!.field, v)}
                                  placeholder="Elige una opción"
                                  inputClassName="w-full px-2 py-1.5 bg-white border border-amber-300 rounded-lg text-xs outline-none focus:ring-2 focus:ring-primary"
                                />
                              </div>
                            )}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                {pages > 1 && (
                  <div className="flex items-center justify-between text-sm text-slate-500">
                    <span>Página {page + 1} de {pages} · {filtered.length} filas</span>
                    <div className="flex gap-2">
                      <button disabled={page === 0} onClick={() => setPage(page - 1)} className="rounded-lg border border-slate-200 px-3 py-1 font-semibold disabled:opacity-40">Anterior</button>
                      <button disabled={page >= pages - 1} onClick={() => setPage(page + 1)} className="rounded-lg border border-slate-200 px-3 py-1 font-semibold disabled:opacity-40">Siguiente</button>
                    </div>
                  </div>
                )}
              </>
            )}
          </>
        )}
      </div>
    </Modal>
  );
}
