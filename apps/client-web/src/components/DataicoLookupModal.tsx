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
  messages?: string[];
  hasXml: boolean;
  hasPdf: boolean;
}

interface DataicoLookupModalProps {
  // Ruta del portal que atiende la consulta: /client/invoices, /client/support-documents o /client/payroll.
  endpoint: string;
  title: string;
  placeholder: string;
  // Tipos de documento que se pueden consultar con este formulario (p. ej. nómina, eliminación y reemplazo). Sin lista, es un solo tipo.
  kinds?: Array<{ value: string; label: string }>;
  onClose: () => void;
}

// Consulta un documento ya emitido en Dataico por su número con prefijo (por ejemplo PE1, DS201 o ESP1) y deja bajar su XML y su representación gráfica.
// Lo usan facturas, documento soporte y nómina. Solo lee: no guarda nada. Los archivos pasan por nuestro servidor para no exponer el token de la cuenta.
export default function DataicoLookupModal({ endpoint, title, placeholder, kinds, onClose }: DataicoLookupModalProps) {
  const [number, setNumber] = useState('');
  const [kind, setKind] = useState(kinds?.[0]?.value ?? '');
  const [searching, setSearching] = useState(false);
  const [result, setResult] = useState<LookupResult | null>(null);
  const [downloading, setDownloading] = useState<'xml' | 'pdf' | null>(null);
  // El resultado que se muestra es el del tipo y número con que se consultó, aunque después se cambie el selector.
  const [searched, setSearched] = useState<{ number: string; kind: string } | null>(null);

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
    if (!value) { toast.error(`Escribe el número del documento con su prefijo, por ejemplo ${placeholder.replace(/^Ej\.\s*/, '')}`); return; }
    setSearching(true);
    setResult(null);
    try {
      const res = await api.get<LookupResult>(`${endpoint}/dataico-lookup`, { params: { number: value, ...(kinds ? { kind } : {}) } });
      setResult(res.data);
      setSearched({ number: value, kind });
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo consultar el documento'));
    } finally {
      setSearching(false);
    }
  };

  const download = async (type: 'xml' | 'pdf') => {
    if (!result || !searched) return;
    setDownloading(type);
    try {
      const res = await api.get(`${endpoint}/dataico-lookup/file`, {
        params: { number: searched.number, type, ...(kinds ? { kind: searched.kind } : {}) },
        responseType: 'blob'
      });
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
    ['Tercero', result.customer], ['Total', result.total], ['CUFE / CUNE', result.cufe]
  ] : [];

  return (
    <Modal
      open
      onOpenChange={o => { if (!o) onClose(); }}
      title={title}
      description="Busca por el número completo, con prefijo."
      size="sm"
    >
      <div className="space-y-4">
        {kinds && kinds.length > 1 && (
          <div className="flex flex-wrap gap-2" role="group" aria-label="Tipo de documento">
            {kinds.map(k => (
              <button
                key={k.value}
                type="button"
                onClick={() => setKind(k.value)}
                className={`rounded-xl border px-3 py-1.5 text-sm font-semibold transition ${kind === k.value ? 'border-primary bg-primary/10 text-primary' : 'border-slate-200 text-slate-600 hover:bg-slate-50'}`}
              >
                {k.label}
              </button>
            ))}
          </div>
        )}
        <div className="flex gap-2">
          <input
            type="text"
            value={number}
            onChange={e => setNumber(e.target.value)}
            onKeyDown={e => { if (e.key === 'Enter') search(); }}
            placeholder={placeholder}
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
            {result.messages && result.messages.length > 0 && (
              <ul className="space-y-1 rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
                {result.messages.map((m, i) => <li key={i} className="break-words">{m}</li>)}
              </ul>
            )}
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
