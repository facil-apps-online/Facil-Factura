import React, { useEffect, useRef, useState } from 'react';
import { Download, FileText, Loader2, Paperclip, Trash2, Upload } from 'lucide-react';
import { toast } from 'sonner';
import { api, getErrorMessage } from '../lib/api';

// PDF que se mandan por correo con la factura o la nota. Dataico acepta unos 7 MB por petición y el base64 agrega cerca de un tercio, así que el tope
// es de 5 MB de archivos sumados por documento (el servidor repite estas reglas).
export const ATTACHMENT_MAX_TOTAL_BYTES = 5 * 1024 * 1024;

interface AttachmentInfo { id: string; fileName: string; sizeBytes: number; createdAt: string }

const mb = (bytes: number) => `${(bytes / 1024 / 1024).toFixed(1)} MB`;

// Primer motivo por el que un archivo nuevo no entra, o null. used: lo que el documento ya pesa en adjuntos.
function rejectionReason(file: File, used: number): string | null {
  if (!file.name.toLowerCase().endsWith('.pdf')) return `«${file.name}» no es un PDF.`;
  if (file.size === 0) return `«${file.name}» está vacío.`;
  if (used + file.size > ATTACHMENT_MAX_TOTAL_BYTES) return `Con «${file.name}» los adjuntos sumarían ${mb(used + file.size)} y el máximo es ${mb(ATTACHMENT_MAX_TOTAL_BYTES)}.`;
  return null;
}

// Sube los archivos que se eligieron en un documento todavía sin guardar, ya con su id. Devuelve los motivos de los que no se pudieron subir.
export async function uploadPendingAttachments(apiBase: string, documentId: string, files: File[]): Promise<string[]> {
  const failures: string[] = [];
  for (const file of files) {
    const form = new FormData();
    form.append('file', file);
    try {
      await api.post(`${apiBase}/${documentId}/attachments`, form, { headers: { 'Content-Type': 'multipart/form-data' } });
    } catch (err) {
      failures.push(getErrorMessage(err, `No se pudo subir «${file.name}»`));
    }
  }
  return failures;
}

interface Props {
  apiBase: string;
  // Id del documento si ya está guardado; null en uno nuevo (los archivos quedan en pantalla hasta guardarlo).
  documentId: string | null;
  // Se pueden agregar y quitar mientras el documento es borrador o rechazado; emitido, solo se consultan.
  editable: boolean;
  pending?: File[];
  onPendingChange?: (files: File[]) => void;
}

export default function DocumentAttachments({ apiBase, documentId, editable, pending = [], onPendingChange }: Props) {
  const [saved, setSaved] = useState<AttachmentInfo[]>([]);
  const [loading, setLoading] = useState(false);
  const [busy, setBusy] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);

  const loadSaved = () => {
    if (!documentId) { setSaved([]); return; }
    setLoading(true);
    api.get(`${apiBase}/${documentId}/attachments`)
      .then(res => setSaved(res.data))
      .catch(() => toast.error('No se pudieron cargar los adjuntos'))
      .finally(() => setLoading(false));
  };
  useEffect(loadSaved, [documentId]); // eslint-disable-line react-hooks/exhaustive-deps

  const used = saved.reduce((sum, a) => sum + a.sizeBytes, 0) + pending.reduce((sum, f) => sum + f.size, 0);

  const addFiles = async (list: FileList | null) => {
    if (!list || list.length === 0) return;
    let usedNow = used;
    const accepted: File[] = [];
    for (const file of Array.from(list)) {
      const reason = rejectionReason(file, usedNow);
      if (reason) { toast.error(reason); continue; }
      accepted.push(file);
      usedNow += file.size;
    }
    if (inputRef.current) inputRef.current.value = '';
    if (accepted.length === 0) return;

    if (!documentId) { onPendingChange?.([...pending, ...accepted]); return; }
    setBusy(true);
    const failures = await uploadPendingAttachments(apiBase, documentId, accepted);
    failures.forEach(message => toast.error(message));
    setBusy(false);
    loadSaved();
  };

  const removeSaved = async (attachment: AttachmentInfo) => {
    setBusy(true);
    try {
      await api.delete(`${apiBase}/${documentId}/attachments/${attachment.id}`);
      loadSaved();
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo quitar el adjunto'));
    } finally {
      setBusy(false);
    }
  };

  const download = async (attachment: AttachmentInfo) => {
    try {
      const res = await api.get(`${apiBase}/${documentId}/attachments/${attachment.id}`, { responseType: 'blob' });
      const url = URL.createObjectURL(new Blob([res.data], { type: 'application/pdf' }));
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = attachment.fileName;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo descargar el adjunto'));
    }
  };

  // Documento emitido sin adjuntos: no hay nada que mostrar.
  if (!editable && !loading && saved.length === 0) return null;

  return (
    <div className="rounded-2xl border border-slate-200 bg-slate-50/60 p-4" data-testid="document-attachments">
      <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
        <h3 className="flex items-center gap-2 text-lg font-bold text-slate-800"><Paperclip size={18} /> Adjuntos (PDF){editable ? ' (opcional)' : ''}</h3>
        {editable && <span className="text-xs text-slate-500">{mb(used)} de {mb(ATTACHMENT_MAX_TOTAL_BYTES)}</span>}
      </div>

      {editable && (
        <p className="mb-3 text-sm text-slate-500">Estos PDF viajan por correo con el documento al emitirlo, y también cuando lo reenvíes.</p>
      )}

      {loading ? (
        <div className="flex items-center gap-2 text-sm text-slate-500"><Loader2 size={16} className="animate-spin" /> Cargando adjuntos…</div>
      ) : (
        <ul className="space-y-2">
          {saved.map(a => (
            <li key={a.id} className="flex items-center gap-3 rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm">
              <FileText size={16} className="shrink-0 text-rose-500" />
              <span className="min-w-0 flex-1 break-words font-medium text-slate-700">{a.fileName}</span>
              <span className="shrink-0 text-xs text-slate-400">{mb(a.sizeBytes)}</span>
              <button type="button" onClick={() => download(a)} title="Descargar" aria-label={`Descargar ${a.fileName}`} className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg text-slate-400 hover:bg-slate-100 hover:text-primary"><Download size={16} /></button>
              {editable && (
                <button type="button" onClick={() => removeSaved(a)} disabled={busy} title="Quitar" aria-label={`Quitar ${a.fileName}`} className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg text-slate-400 hover:bg-rose-50 hover:text-rose-600 disabled:opacity-50"><Trash2 size={16} /></button>
              )}
            </li>
          ))}
          {pending.map((f, i) => (
            <li key={`${f.name}-${i}`} className="flex items-center gap-3 rounded-xl border border-dashed border-slate-300 bg-white px-3 py-2 text-sm">
              <FileText size={16} className="shrink-0 text-rose-500" />
              <span className="min-w-0 flex-1 break-words font-medium text-slate-700">{f.name}</span>
              <span className="shrink-0 text-xs text-slate-400">{mb(f.size)} · se sube al guardar</span>
              <button type="button" onClick={() => onPendingChange?.(pending.filter((_, j) => j !== i))} title="Quitar" aria-label={`Quitar ${f.name}`} className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg text-slate-400 hover:bg-rose-50 hover:text-rose-600"><Trash2 size={16} /></button>
            </li>
          ))}
        </ul>
      )}

      {editable && (
        <div className="mt-3">
          <input ref={inputRef} type="file" accept="application/pdf,.pdf" multiple className="hidden" onChange={e => addFiles(e.target.files)} data-testid="attachments-input" />
          <button
            type="button"
            onClick={() => inputRef.current?.click()}
            disabled={busy}
            className="flex items-center gap-2 rounded-xl border border-slate-200 bg-white px-4 py-2 text-sm font-bold text-slate-700 hover:bg-slate-50 disabled:opacity-50"
          >
            {busy ? <Loader2 size={16} className="animate-spin" /> : <Upload size={16} />} Agregar PDF
          </button>
        </div>
      )}
    </div>
  );
}
