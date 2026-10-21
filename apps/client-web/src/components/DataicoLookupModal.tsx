import { useState } from 'react';
import { Download, Loader2, Search } from 'lucide-react';
import { toast } from 'sonner';
import { api, getErrorMessage } from '../lib/api';
import Modal from './Modal';

interface LookupResult {
  number: string;
  uuid?: string | null;
  cufe?: string | null;
  status?: string | null;
  issueDate?: string | null;
  customer?: string | null;
  total?: string | null;
  hasXml: boolean;
  hasPdf: boolean;
}

interface DataicoLookupModalProps {
  onClose: () => void;
}

// Consulta una factura en Dataico por su número con prefijo (por ejemplo PE1) y deja bajar su XML y su representación gráfica.
// Solo lee: no guarda nada. Los archivos pasan por nuestro servidor para no exponer el token de la cuenta de Dataico.
export default function DataicoLookupModal({ onClose }: DataicoLookupModalProps) {
  const [number, setNumber] = useState('');
  const [searching, setSearching] = useState(false);
  const [result, setResult] = useState<LookupResult | null>(null);
  const [downloading, setDownloading] = useState<'xml' | 'pdf' | null>(null);

  // Con responseType blob el mensaje de error del servidor llega como Blob: se lee para poder mostrarlo.
  const blobErrorMessage = async (err: unknown, fallback: string) => {
    const data = (err as { response?: { data?: unknown } })?.response?.data;
    if (data instanceof Blob) {
      try { return JSON.parse(await data.text())?.message || fallback; } catch { return fallback; }
    }
    return getErrorMessage(err, fallback);
  };

  const search = async () => {
    const value = number.trim();
    if (!value) { toast.error('Escribe el número de la factura con su prefijo, por ejemplo PE1'); return; }
    setSearching(true);
    setResult(null);
    try {
      const res = await api.get<LookupResult>('/client/invoices/dataico-lookup', { params: { number: value } });
      setResult(res.data);
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo consultar la factura'));
    } finally {
      setSearching(false);
    }
  };

  const download = async (type: 'xml' | 'pdf') => {
    if (!result) return;
    setDownloading(type);
    try {
      const res = await api.get('/client/invoices/dataico-lookup/file', { params: { number: result.number, type }, responseType: 'blob' });
      const url = window.URL.createObjectURL(new Blob([res.data], { type: type === 'pdf' ? 'application/pdf' : 'application/xml' }));
      const a = document.createElement('a');
      a.href = url;
      a.download = `${result.number.replace(/[^A-Za-z0-9]/g, '')}.${type}`;
      a.click();
      window.URL.revokeObjectURL(url);
    } catch (err) {
      toast.error(await blobErrorMessage(err, 'No se pudo descargar el archivo'));
    } finally {
      setDownloading(null);
    }
  };

  const rows: Array<[string, string | null | undefined]> = result ? [
    ['Número', result.number], ['Estado', result.status], ['Fecha', result.issueDate],
    ['Adquirente', result.customer], ['Total', result.total], ['CUFE', result.cufe]
  ] : [];

  return (
    <Modal
      open
      onOpenChange={o => { if (!o) onClose(); }}
      title="Consultar factura en Dataico"
      description="Busca por el número completo, con prefijo."
      size="sm"
    >
      <div className="space-y-4">
        <div className="flex gap-2">
          <input
            type="text"
            value={number}
            onChange={e => setNumber(e.target.value)}
            onKeyDown={e => { if (e.key === 'Enter') search(); }}
            placeholder="Ej. PE1"
            autoFocus
            className="flex-1 px-3 py-2.5 bg-white border border-slate-200 rounded-xl text-sm outline-none focus:ring-2 focus:ring-primary"
          />
          <button onClick={search} disabled={searching} className="bg-primary hover:bg-primary/90 disabled:opacity-60 text-white px-4 py-2.5 rounded-xl font-bold flex items-center gap-2">
            {searching ? <Loader2 size={16} className="animate-spin" /> : <Search size={16} />} Consultar
          </button>
        </div>

        {result && (
          <div className="space-y-3">
            <dl className="divide-y divide-slate-100 rounded-xl border border-slate-200 text-sm">
              {rows.filter(([, v]) => v).map(([label, value]) => (
                <div key={label} className="flex gap-3 px-3 py-2">
                  <dt className="w-24 shrink-0 font-semibold text-slate-500">{label}</dt>
                  <dd className="min-w-0 break-all text-slate-800">{value}</dd>
                </div>
              ))}
            </dl>
            <div className="flex flex-wrap gap-2">
              <button onClick={() => download('pdf')} disabled={!result.hasPdf || downloading !== null} className="flex items-center gap-2 rounded-xl border border-slate-200 px-4 py-2 text-sm font-bold text-slate-700 hover:bg-slate-50 disabled:opacity-50">
                {downloading === 'pdf' ? <Loader2 size={16} className="animate-spin" /> : <Download size={16} />} Representación gráfica
              </button>
              <button onClick={() => download('xml')} disabled={!result.hasXml || downloading !== null} className="flex items-center gap-2 rounded-xl border border-slate-200 px-4 py-2 text-sm font-bold text-slate-700 hover:bg-slate-50 disabled:opacity-50">
                {downloading === 'xml' ? <Loader2 size={16} className="animate-spin" /> : <Download size={16} />} XML
              </button>
            </div>
          </div>
        )}
      </div>
    </Modal>
  );
}
