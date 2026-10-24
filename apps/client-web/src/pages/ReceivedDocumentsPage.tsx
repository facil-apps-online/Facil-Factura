import React, { useRef, useState, useEffect } from 'react';
import { Inbox, Upload, Loader2, CheckCircle2, XCircle, Send, Info, Download, FileWarning } from 'lucide-react';
import { api, getErrorMessage } from '../lib/api';
import { toast } from 'sonner';
import { useNumberFormat } from '../lib/numberFormat';
import DataTable, { type DataTableColumn } from '@shared/components/DataTable';
import Modal from '../components/Modal';

const EVENT_BUTTONS = [{ code: '030', label: 'Acuse' }, { code: '032', label: 'Recibo' }, { code: '033', label: 'Aceptación' }, { code: '031', label: 'Reclamo' }] as const;
const EVENT_STATUS_LABELS: Record<string, string> = { SENT: 'Enviado', DUPLICATE: 'Recibido', REJECTED: 'Rechazado', PENDING: 'Enviando' };

export default function ReceivedDocumentsPage() {
  const fmt = useNumberFormat();
  const [loading, setLoading] = useState(true);
  const [documents, setDocuments] = useState<any[]>([]);
  const [uploading, setUploading] = useState(false);
  const [sendingEvents, setSendingEvents] = useState<Set<string>>(() => new Set());
  const sendingEventsRef = useRef(new Set<string>());
  const uploadInputRef = useRef<HTMLInputElement>(null);
  const [eventHistory, setEventHistory] = useState<{ document: any; code: string; label: string } | null>(null);
  const [fileDownloading, setFileDownloading] = useState<'xml' | 'pdf' | null>(null);
  const loadDocuments = () => { api.get('/client/received-documents').then(res => setDocuments(res.data)).catch(() => toast.error('Error al cargar los documentos recibidos')).finally(() => setLoading(false)); };
  useEffect(() => { loadDocuments(); }, []);
  const handleUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]; if (!file) return; e.target.blur(); setUploading(true);
    const formData = new FormData(); formData.append('file', file);
    try { await api.post('/client/received-documents/upload', formData, { headers: { 'Content-Type': 'multipart/form-data' } }); toast.success('Documento cargado'); loadDocuments(); }
    catch (err: any) { toast.error(getErrorMessage(err, 'Error al cargar el documento')); }
    finally { setUploading(false); e.target.value = ''; }
  };
  const triggerEvent = async (docId: string, eventCode: string) => {
    const key = `${docId}:${eventCode}`; if (sendingEventsRef.current.has(key)) return;
    sendingEventsRef.current.add(key); setSendingEvents(previous => new Set(previous).add(key));
    try {
      const res = await api.post(`/client/received-documents/${docId}/events/${eventCode}`);
      if (res.data.status === 'SENT') toast.success('Evento enviado a la DIAN');
      else if (res.data.status === 'DUPLICATE') toast.warning(res.data.dianResponseMessage || 'Dataico ya tenía este evento');
      else toast.error(res.data.dianResponseMessage || 'El proveedor rechazó el evento');
      loadDocuments();
    } catch (err: any) { toast.error(getErrorMessage(err, 'Error al enviar el evento')); }
    finally { setSendingEvents(previous => { const next = new Set(previous); next.delete(key); return next; }); sendingEventsRef.current.delete(key); }
  };
  const downloadDocumentFile = async (documentId: string, type: 'xml' | 'pdf', name: string) => {
    setFileDownloading(type);
    try {
      const response = await api.get(`/client/received-documents/${documentId}/${type}`, { responseType: 'blob' });
      const url = URL.createObjectURL(new Blob([response.data], { type: type === 'pdf' ? 'application/pdf' : 'application/xml' }));
      const anchor = document.createElement('a'); anchor.href = url; anchor.download = `${name}.${type}`; anchor.click(); URL.revokeObjectURL(url);
    } catch (err) { toast.error(getErrorMessage(err, `No se pudo descargar el ${type.toUpperCase()}`)); }
    finally { setFileDownloading(null); }
  };
  const columns: DataTableColumn<any>[] = [
    { key: 'issuer', header: 'Proveedor', primary: true, cellClassName: 'font-semibold text-slate-700', render: doc => doc.issuerName || doc.issuerTaxId, sortValue: doc => doc.issuerName || doc.issuerTaxId, searchValue: doc => `${doc.issuerName ?? ''} ${doc.issuerTaxId ?? ''}` },
    { key: 'documentId', header: 'Documento', cellClassName: 'font-mono text-sm text-slate-600', render: doc => doc.documentId, sortValue: doc => doc.documentId, searchValue: doc => doc.documentId },
    { key: 'issueDate', header: 'Fecha', cellClassName: 'text-sm text-slate-500', render: doc => new Date(doc.issueDate).toLocaleDateString('es-CO'), sortValue: doc => doc.issueDate },
    { key: 'total', header: 'Total', align: 'right', cellClassName: 'font-mono text-sm', render: doc => fmt.number(Number(doc.totalAmount), 3), sortValue: doc => Number(doc.totalAmount) },
    { key: 'source', header: 'Origen', hideBelow: 'ultra', cellClassName: 'text-xs text-slate-400', render: doc => doc.sourceType === 'Email' ? 'Correo' : 'Manual', sortValue: doc => doc.sourceType === 'Email' ? 'Correo' : 'Manual' },
    { key: 'events', header: 'Eventos', cardFullWidth: true, render: doc => {
      const latestByCode = new Map<string, any>(); for (const event of doc.events ?? []) if (!latestByCode.has(event.eventCode)) latestByCode.set(event.eventCode, event);
        if (doc.isSupportDocument) return <span title="Los eventos RADIAN solo aplican a facturas electrónicas a crédito" className="inline-flex items-center gap-1 rounded-lg bg-amber-50 px-2 py-1 text-xs font-bold text-amber-700"><FileWarning size={13} /> Documento soporte · no aplica RADIAN</span>;
        if (!doc.isCredit) return <span className="inline-flex items-center gap-1 rounded-lg bg-slate-100 px-2 py-1 text-xs font-bold text-slate-500">Eventos no aplican · contado</span>;
      return <div className="flex flex-wrap gap-2 md:gap-1.5">{EVENT_BUTTONS.map(({ code, label }) => {
        const event = latestByCode.get(code); const detail = event?.dianResponseMessage || '';
        const historyButton = event ? <button type="button" title="Ver historial de envíos" onClick={() => setEventHistory({ document: doc, code, label })} className="inline-flex min-h-11 w-8 items-center justify-center rounded-lg text-slate-400 hover:bg-slate-100 hover:text-primary lg:min-h-0" aria-label={`Ver historial de ${label}`}><Info size={14} /></button> : null;
        const withInfo = (content: React.ReactNode) => <div key={code} className="inline-flex items-center gap-0.5">{content}{historyButton}</div>;
        if (sendingEvents.has(`${doc.id}:${code}`)) return withInfo(<span className="inline-flex items-center gap-1 px-2 py-1 rounded-lg bg-blue-50 text-blue-700 text-xs font-bold"><Loader2 size={12} className="animate-spin" /> {label} · enviando</span>);
        if (event?.status === 'DUPLICATE') return withInfo(<span title={detail || 'Dataico confirmó que el evento ya estaba recibido'} className="inline-flex items-center gap-1 px-2 py-1 rounded-lg bg-emerald-50 text-emerald-700 text-xs font-bold"><CheckCircle2 size={12} /> {label}{event.eventNumber ? ` · ${event.eventNumber}` : ''}</span>);
        if (event?.status === 'SENT') return withInfo(<span title={detail || 'Enviado'} className="inline-flex items-center gap-1 px-2 py-1 rounded-lg bg-emerald-50 text-emerald-700 text-xs font-bold"><CheckCircle2 size={12} /> {label}{event.eventNumber ? ` · ${event.eventNumber}` : ''}</span>);
        if (event?.status === 'PENDING') return withInfo(<span className="inline-flex items-center gap-1 px-2 py-1 rounded-lg bg-blue-50 text-blue-700 text-xs font-bold"><Loader2 size={12} className="animate-spin" /> {label} · enviando</span>);
        if (event?.status === 'REJECTED') return withInfo(<button title={detail || 'El proveedor rechazó el evento'} onClick={() => triggerEvent(doc.id, code)} className="inline-flex min-h-11 items-center gap-1 px-3 rounded-lg bg-rose-50 hover:bg-rose-100 text-rose-700 text-xs font-bold lg:min-h-0 lg:px-2 lg:py-1"><XCircle size={12} /> {label} · rechazado</button>);
        return <button key={code} onClick={() => triggerEvent(doc.id, code)} className="inline-flex min-h-11 items-center gap-1 px-3 rounded-lg bg-slate-100 hover:bg-slate-200 text-slate-600 text-xs font-bold lg:min-h-0 lg:px-2 lg:py-1"><Send size={12} /> {label}</button>;
      })}</div>;
    } }
  ];
  if (loading) return <div className="flex h-full items-center justify-center"><Loader2 className="w-10 h-10 animate-spin text-blue-500" /></div>;
  return <div className="p-3 sm:p-4 space-y-6 lg:space-y-8">
    <div><h1 className="text-2xl sm:text-3xl font-extrabold text-slate-800 tracking-tight">Documentos recibidos</h1><p className="text-slate-500 mt-2">Consulta documentos de tus proveedores y revisa el resultado de sus eventos.</p></div>
    <div className="bg-white rounded-3xl p-4 sm:p-6 lg:p-8 shadow-sm border border-slate-100">
      <div className="mb-6 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between"><div className="flex items-center gap-2"><Inbox className="text-primary" size={20} /><h2 className="text-xl font-bold text-slate-800">Documentos recibidos</h2></div><><button type="button" onClick={() => uploadInputRef.current?.click()} disabled={uploading} className="cursor-pointer bg-slate-900 hover:bg-black disabled:opacity-50 text-white px-5 py-3 rounded-xl text-sm font-bold shadow-sm flex items-center justify-center gap-2 focus-visible:ring-2 focus-visible:ring-primary/40">{uploading ? <Loader2 className="w-4 h-4 animate-spin" /> : <Upload size={16} />}{uploading ? 'Cargando...' : 'Cargar ZIP/XML'}</button><input ref={uploadInputRef} type="file" accept=".zip,.xml" className="hidden" onChange={handleUpload} disabled={uploading} /></></div>
      {documents.length === 0 ? <div className="flex flex-col items-center justify-center text-center h-48 border-2 border-dashed border-slate-200 rounded-2xl bg-slate-50/50"><Inbox className="text-slate-300 mb-3" size={32} /><p className="text-slate-500">Aún no tienes documentos recibidos.</p></div> : <DataTable rows={documents} columns={columns} rowKey={doc => doc.id} tableFrom="2xl" search="Buscar por proveedor, NIT o número de documento..." defaultSort={{ key: 'issueDate', dir: 'desc' }} />}
    </div>
    {eventHistory && (() => {
      const latest = (eventHistory.document.events ?? []).filter((item: any) => item.eventCode === eventHistory.code).sort((a: any, b: any) => new Date(b.sentAt).getTime() - new Date(a.sentAt).getTime())[0];
      const downloadEventFile = async (item: any, type: 'xml' | 'pdf') => {
        setFileDownloading(type);
        try {
          const response = await api.get(`/client/received-documents/${eventHistory.document.id}/events/${item.id}/file?type=${type}`, { responseType: 'blob' });
          const url = URL.createObjectURL(new Blob([response.data], { type: type === 'pdf' ? 'application/pdf' : 'application/xml' }));
          const anchor = document.createElement('a'); anchor.href = url; anchor.download = `${eventHistory.document.documentId}-${eventHistory.code}.${type}`; anchor.click(); URL.revokeObjectURL(url);
        } catch (err) { toast.error(getErrorMessage(err, `No se pudo descargar el ${type.toUpperCase()} de Dataico`)); }
        finally { setFileDownloading(null); }
      };
      return <Modal open onOpenChange={open => { if (!open) setEventHistory(null); }} title={<span className="flex items-center gap-2"><Info size={18} className="text-primary" /> Último envío de {eventHistory.label}</span>} description={`${eventHistory.document.issuerName || eventHistory.document.issuerTaxId || ''} · ${eventHistory.document.documentId}`} size="lg">
        <div className="space-y-4">
          <div className="flex flex-wrap gap-2">
            <button type="button" onClick={() => downloadDocumentFile(eventHistory.document.id, 'xml', eventHistory.document.documentId)} disabled={fileDownloading !== null} className="flex items-center gap-2 rounded-xl border border-slate-200 px-3 py-2 text-sm font-bold text-slate-700 hover:bg-slate-50 disabled:opacity-50"><Download size={15} /> {fileDownloading === 'xml' ? 'Descargando...' : 'XML recibido'}</button>
            <button type="button" onClick={() => downloadDocumentFile(eventHistory.document.id, 'pdf', eventHistory.document.documentId)} disabled={fileDownloading !== null} className="flex items-center gap-2 rounded-xl border border-slate-200 px-3 py-2 text-sm font-bold text-slate-700 hover:bg-slate-50 disabled:opacity-50"><Download size={15} /> {fileDownloading === 'pdf' ? 'Descargando...' : 'Representación gráfica'}</button>
          </div>
          <div className="space-y-3">{!latest ? <p className="text-sm text-slate-500">No hay intentos registrados para este evento.</p> : <div className="rounded-xl border border-slate-200 bg-slate-50/70 p-3 space-y-2">
          <div className="flex flex-wrap items-center justify-between gap-2"><span className={`rounded-full px-2 py-1 text-xs font-bold ${latest.status === 'REJECTED' ? 'bg-rose-100 text-rose-700' : latest.status === 'PENDING' ? 'bg-blue-100 text-blue-700' : 'bg-emerald-100 text-emerald-700'}`}>{EVENT_STATUS_LABELS[latest.status] || latest.status}</span><span className="text-xs text-slate-500">{latest.sentAt ? new Date(latest.sentAt).toLocaleString('es-CO') : 'Sin fecha'}</span></div>
          <div className="grid gap-1 text-xs text-slate-600 sm:grid-cols-2"><span><b>Consecutivo:</b> {latest.eventNumber || '—'}</span>{latest.providerUid && <span className="break-all"><b>Id proveedor:</b> {latest.providerUid}</span>}</div>
          {latest.cude && <div className="text-xs text-slate-600"><b>CUDE:</b> <span className="break-all font-mono">{latest.cude}</span></div>}
          {latest.dianResponseMessage && <div className="rounded-lg bg-white p-2 text-xs text-slate-600 break-words"><b>Respuesta:</b> {latest.dianResponseMessage}</div>}
          {(latest.providerXmlUrl || latest.providerPdfUrl) && <div className="flex flex-wrap gap-2 pt-1">{latest.providerXmlUrl && <button type="button" onClick={() => downloadEventFile(latest, 'xml')} disabled={fileDownloading !== null} className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-bold text-slate-700 hover:bg-slate-100 disabled:opacity-50">{fileDownloading === 'xml' ? 'Descargando...' : 'Ver XML de Dataico'}</button>}{latest.providerPdfUrl && <button type="button" onClick={() => downloadEventFile(latest, 'pdf')} disabled={fileDownloading !== null} className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-bold text-slate-700 hover:bg-slate-100 disabled:opacity-50">{fileDownloading === 'pdf' ? 'Descargando...' : 'Representación gráfica de Dataico'}</button>}</div>}
          {latest.providerResponse && <details className="rounded-lg bg-slate-900 text-slate-100"><summary className="cursor-pointer px-2 py-1.5 text-xs font-bold">Respuesta técnica de Dataico</summary><pre className="max-h-52 overflow-auto whitespace-pre-wrap break-all p-2 text-[11px]">{latest.providerResponse}</pre></details>}
        </div>}</div></div>
      </Modal>;
    })()}
  </div>;
}
