import { useEffect, useState } from 'react';
import { Loader2, MailCheck, RefreshCw } from 'lucide-react';
import { toast } from 'sonner';
import { api } from './api';

type Message = {
  id: string; kind: string; status: string; deliveryStatus: string; recipientEmail: string; subject: string; channel: string;
  attempts: number; lastError: string; deliveryDetail: string; createdAt: string; sentAt: string | null; openedAt: string | null; clientName: string | null;
};
type Page = { total: number; page: number; pageSize: number; items: Message[] };

const KIND_LABELS: Record<string, string> = {
  Document: 'Documento', Certificate: 'Certificado', CertificatePassword: 'Contraseña de certificado', Notification: 'Aviso', Platform: 'Plataforma',
};
const CHANNEL_LABELS: Record<string, string> = { 'client-smtp': 'SMTP del cliente', 'tenant-smtp': 'SMTP del tenant', platform: 'SendPulse' };
const FILTERS: [string, string][] = [
  ['', 'Todos'], ['Pending', 'En cola'], ['Held', 'Retenidos'], ['Failed', 'Reintentando'], ['Dead', 'Sin enviar'],
  ['Sent', 'Enviados'], ['Delivered', 'Entregados'], ['Opened', 'Abiertos'], ['Bounced', 'Rebotados'],
];

// Estado que ve el superadmin: lo que pasó con el envío y, para lo que sale por SendPulse, lo que SendPulse reporta después.
function statusOf(m: Message): { label: string; className: string; detail: string } {
  if (m.deliveryStatus === 'Bounced') return { label: 'Rebotó', className: 'bg-rose-500/15 text-rose-300', detail: m.deliveryDetail };
  if (m.openedAt) return { label: 'Abierto', className: 'bg-emerald-500/15 text-emerald-300', detail: '' };
  if (m.deliveryStatus === 'Delivered') return { label: 'Entregado', className: 'bg-emerald-500/15 text-emerald-300', detail: '' };
  switch (m.status) {
    case 'Sent': return { label: 'Enviado', className: 'bg-blue-500/15 text-blue-300', detail: m.channel === 'platform' ? (m.deliveryDetail || 'A la espera de que SendPulse reporte la entrega.') : '' };
    case 'Pending': return { label: 'En cola', className: 'bg-slate-500/20 text-slate-300', detail: '' };
    case 'Sending': return { label: 'Enviando', className: 'bg-slate-500/20 text-slate-300', detail: '' };
    case 'Held': return { label: 'Retenido', className: 'bg-slate-500/20 text-slate-300', detail: 'Espera a que salga el correo del que depende.' };
    case 'Failed': return { label: 'Reintentando', className: 'bg-amber-500/15 text-amber-300', detail: m.lastError };
    case 'Dead': return { label: 'Sin enviar', className: 'bg-rose-500/15 text-rose-300', detail: m.lastError };
    case 'Cancelled': return { label: 'Cancelado', className: 'bg-slate-500/20 text-slate-300', detail: m.lastError };
    default: return { label: m.status, className: 'bg-slate-500/20 text-slate-300', detail: '' };
  }
}

const formatDate = (value: string | null) => (value ? new Date(value).toLocaleString('es-CO') : '—');

// Correos enviados por la plataforma con su estado: el envío, la entrega (aceptado o rechazado por el servidor del destinatario) y la apertura.
export function EmailLog() {
  const [data, setData] = useState<Page | null>(null);
  const [status, setStatus] = useState('');
  const [kind, setKind] = useState('');
  const [search, setSearch] = useState('');
  const [query, setQuery] = useState('');
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(false);

  const load = () => {
    setLoading(true);
    api.get<Page>('/email-settings/messages', { params: { status: status || undefined, kind: kind || undefined, search: query || undefined, page } })
      .then(res => setData(res.data))
      .catch(() => toast.error('No fue posible cargar los correos.'))
      .finally(() => setLoading(false));
  };

  useEffect(load, [status, kind, query, page]);

  const select = 'rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 text-white';
  const pages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1;

  return (
    <div className="p-10 max-w-7xl">
      <div className="flex items-center justify-between mb-8">
        <div>
          <h1 className="text-3xl font-extrabold text-white">Correos enviados</h1>
          <p className="text-slate-400 mt-2">Todo lo que sale de Facil Factura, con su estado. Para los que salen por SendPulse se consulta cada pocos minutos si el servidor del destinatario lo aceptó o lo rechazó y si se abrió.</p>
        </div>
        <MailCheck className="w-10 h-10 text-blue-400 shrink-0" />
      </div>

      <div className="mb-5 flex flex-wrap items-center gap-3">
        <select value={status} onChange={e => { setPage(1); setStatus(e.target.value); }} className={select} aria-label="Estado">
          {FILTERS.map(([value, label]) => <option key={value} value={value}>{label}</option>)}
        </select>
        <select value={kind} onChange={e => { setPage(1); setKind(e.target.value); }} className={select} aria-label="Tipo">
          <option value="">Todos los tipos</option>
          {Object.entries(KIND_LABELS).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
        </select>
        <form className="flex gap-2" onSubmit={e => { e.preventDefault(); setPage(1); setQuery(search); }}>
          <input value={search} onChange={e => setSearch(e.target.value)} placeholder="Buscar por destinatario o asunto" className={`${select} w-72`} />
          <button className="rounded-lg border border-slate-600 px-4 py-2 font-semibold text-slate-200 hover:bg-slate-800">Buscar</button>
        </form>
        <button onClick={load} title="Actualizar" aria-label="Actualizar" className="ml-auto rounded-lg border border-slate-700 p-2.5 text-slate-300 hover:bg-slate-800">
          <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
        </button>
      </div>

      {!data ? (
        <div className="text-slate-400"><Loader2 className="animate-spin" /></div>
      ) : data.items.length === 0 ? (
        <p className="text-sm text-slate-500">No hay correos con ese filtro.</p>
      ) : (
        <div className="overflow-x-auto rounded-2xl border border-slate-800 bg-slate-950/60">
          <table className="w-full text-sm">
            <thead className="text-left text-slate-400">
              <tr>
                <th className="p-3">Creado</th><th className="p-3">Tipo</th><th className="p-3">Destinatario</th><th className="p-3">Asunto</th>
                <th className="p-3">Canal</th><th className="p-3">Estado</th><th className="p-3">Enviado</th><th className="p-3">Abierto</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800 text-slate-200">
              {data.items.map(m => {
                const st = statusOf(m);
                return (
                  <tr key={m.id} className="align-top">
                    <td className="p-3 whitespace-nowrap">{formatDate(m.createdAt)}</td>
                    <td className="p-3">{KIND_LABELS[m.kind] ?? m.kind}{m.clientName && <span className="block text-xs text-slate-500">{m.clientName}</span>}</td>
                    <td className="p-3 break-all">{m.recipientEmail}</td>
                    <td className="p-3 max-w-xs truncate" title={m.subject}>{m.subject}</td>
                    <td className="p-3 whitespace-nowrap">{m.channel ? (CHANNEL_LABELS[m.channel] ?? m.channel) : '—'}</td>
                    <td className="p-3">
                      <span className={`inline-flex rounded-full px-2.5 py-1 text-xs font-semibold ${st.className}`}>{st.label}</span>
                      {st.detail && <span className="mt-1 block max-w-xs text-xs text-slate-400">{st.detail}</span>}
                    </td>
                    <td className="p-3 whitespace-nowrap">{formatDate(m.sentAt)}</td>
                    <td className="p-3 whitespace-nowrap">{formatDate(m.openedAt)}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      {data && data.total > data.pageSize && (
        <div className="mt-4 flex items-center justify-between text-sm text-slate-400">
          <span>{data.total} correos</span>
          <div className="flex items-center gap-3">
            <button disabled={page <= 1} onClick={() => setPage(page - 1)} className="rounded-lg border border-slate-700 px-3 py-1.5 hover:bg-slate-800 disabled:opacity-40">Anterior</button>
            <span>Página {page} de {pages}</span>
            <button disabled={page >= pages} onClick={() => setPage(page + 1)} className="rounded-lg border border-slate-700 px-3 py-1.5 hover:bg-slate-800 disabled:opacity-40">Siguiente</button>
          </div>
        </div>
      )}
    </div>
  );
}
