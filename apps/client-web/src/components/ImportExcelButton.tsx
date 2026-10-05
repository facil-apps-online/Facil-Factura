import React, { useState } from 'react';
import { Upload, Download, CheckCircle2, XCircle, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { api, getErrorMessage } from '../lib/api';
import Modal from './Modal';
import { Button } from './ui/button';

interface ImportRowResult {
  row: number;
  success: boolean;
  message: string;
}

interface ImportSummary {
  totalRows: number;
  succeeded: number;
  failed: number;
  results: ImportRowResult[];
}

export default function ImportExcelButton({ endpoint, label, onDone, templateEndpoint }: { endpoint: string, label: string, onDone: () => void, templateEndpoint?: string }) {
  const [uploading, setUploading] = useState(false);
  const [downloadingTemplate, setDownloadingTemplate] = useState(false);
  const [summary, setSummary] = useState<ImportSummary | null>(null);

  const handleDownloadTemplate = async () => {
    if (!templateEndpoint) return;
    setDownloadingTemplate(true);
    try {
      const res = await api.get(templateEndpoint, { responseType: 'blob' });
      const disposition = res.headers?.['content-disposition'] as string | undefined;
      const match = disposition?.match(/filename="?([^"]+)"?/);
      const filename = match?.[1] || 'plantilla.xlsx';
      const url = window.URL.createObjectURL(new Blob([res.data]));
      const a = document.createElement('a');
      a.href = url;
      a.download = filename;
      document.body.appendChild(a);
      a.click();
      a.remove();
      window.URL.revokeObjectURL(url);
    } catch (err) {
      toast.error('Error al descargar la plantilla');
    } finally {
      setDownloadingTemplate(false);
    }
  };

  const handleFile = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setUploading(true);
    try {
      const formData = new FormData();
      formData.append('file', file);
      const res = await api.post<ImportSummary>(endpoint, formData, {
        headers: { 'Content-Type': 'multipart/form-data' }
      });
      setSummary(res.data);
      if (res.data.succeeded > 0) {
        toast.success(`${res.data.succeeded} de ${res.data.totalRows} filas importadas correctamente`);
        onDone();
      }
      if (res.data.failed > 0) {
        toast.error(`${res.data.failed} filas con errores`);
      }
    } catch (err: any) {
      toast.error(getErrorMessage(err, 'Error al importar el archivo'));
    } finally {
      setUploading(false);
      e.target.value = '';
    }
  };

  return (
    <>
      {templateEndpoint && (
        <button
          type="button"
          onClick={handleDownloadTemplate}
          disabled={downloadingTemplate}
          className="px-4 py-2.5 bg-white border border-slate-200 text-slate-500 rounded-xl font-bold flex items-center gap-2 shadow-sm hover:bg-slate-50 transition-all disabled:opacity-50"
        >
          {downloadingTemplate ? <Loader2 size={18} className="animate-spin" /> : <Download size={18} />}
          Descargar plantilla
        </button>
      )}
      <label className={`px-4 py-2.5 bg-white border border-slate-200 text-slate-600 rounded-xl font-bold flex items-center gap-2 shadow-sm cursor-pointer hover:bg-slate-50 transition-all focus-within:ring-2 focus-within:ring-primary/40 ${uploading ? 'opacity-50 pointer-events-none' : ''}`}>
        {uploading ? <Loader2 size={18} className="animate-spin" /> : <Upload size={18} />}
        {label}
        <input type="file" accept=".xlsx" className="sr-only" onChange={handleFile} disabled={uploading} />
      </label>

      {summary && (
        <Modal
          open
          onOpenChange={open => { if (!open) setSummary(null); }}
          title="Resultado de la importación"
          size="md"
          footer={<Button type="button" onClick={() => setSummary(null)}>Cerrar</Button>}
        >
          <p className="text-sm text-slate-600 mb-4">
            {summary.succeeded} de {summary.totalRows} filas importadas correctamente
            {summary.failed > 0 && `, ${summary.failed} con errores`}.
          </p>
          <div className="space-y-2">
            {summary.results.map((r, i) => (
              <div key={i} className={`flex items-start gap-2 p-3 rounded-xl text-sm ${r.success ? 'bg-emerald-50 text-emerald-700' : 'bg-rose-50 text-rose-700'}`}>
                {r.success ? <CheckCircle2 size={16} className="mt-0.5 shrink-0" /> : <XCircle size={16} className="mt-0.5 shrink-0" />}
                <span className="min-w-0 break-words">Fila {r.row}: {r.message}</span>
              </div>
            ))}
          </div>
        </Modal>
      )}
    </>
  );
}
