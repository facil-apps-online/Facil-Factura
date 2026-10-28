import { AlertCircle, Info } from 'lucide-react';
import Modal from './Modal';
import { Button } from './ui/button';

// dianResponseMessage guarda la respuesta cruda del integrador (Dataico o DIAN nativo) como texto: en rechazo, un JSON
// {"errors":[{"path":[...], "error":"..."}]}; en aprobación, un JSON con los datos del documento
// ya emitido (incluye "dian_status", el estado propio de la DIAN dentro de esa misma respuesta —
// no hay un campo separado nuestro para eso). Se parsea acá solo para mostrarlo legible en un
// modal en vez de como texto crudo; si no es JSON válido, se muestra tal cual.
//
// La respuesta del integrador trae mucho más de lo que vale la pena mostrarle al cliente (customer e
// items como objetos/arreglos completos, el SOAP de la DIAN en base64 dentro de "xml", uuid,
// numbering, etc.) — por eso esto es una lista blanca explícita, no un volcado de todas las
// claves; lo que no está acá simplemente no se muestra.
const DIAN_RESPONSE_DISPLAY_FIELDS: { key: string; label: string }[] = [
  { key: 'number', label: 'Número' },
  { key: 'issue_date', label: 'Fecha de emisión' },
  { key: 'dian_status', label: 'Estado DIAN' },
  { key: 'payment_means_type', label: 'Forma de pago' },
];

const parseDianResponse = (raw: string): { errors?: { path?: string[]; error: string }[] } | { fields: Record<string, any> } | null => {
  try {
    const parsed = JSON.parse(raw);
    if (Array.isArray(parsed?.errors)) return { errors: parsed.errors };
    if (parsed && typeof parsed === 'object') return { fields: parsed };
    return null;
  } catch {
    return null;
  }
};

// Un documento sobre el que se puede abrir el diálogo: rechazado, o aprobado con respuesta del integrador.
// Los listados no traen el texto (pesa mucho) y avisan con hasDianResponse; el detalle sí trae dianResponseMessage.
export const hasDianResponse = (doc: { status: string; dianResponseMessage?: string | null; hasDianResponse?: boolean }) =>
  (doc.status === 'REJECTED' || doc.status === 'APPROVED') && (!!doc.dianResponseMessage || !!doc.hasDianResponse);

// Diálogo con el motivo del rechazo o la respuesta de la DIAN. Lo comparten facturas, documentos soporte y nómina.
// `label` es lo que identifica al documento bajo el título (ej. "Factura FE-12").
export default function DianResponseDialog({ doc, label, onClose }: {
  doc: { status: string; dianResponseMessage: string };
  label: string;
  onClose: () => void;
}) {
  const parsedResponse = parseDianResponse(doc.dianResponseMessage);
  const rejected = doc.status === 'REJECTED';

  return (
    <Modal
      open
      onOpenChange={open => { if (!open) onClose(); }}
      title={
        <span className="flex items-center gap-2">
          {rejected ? <AlertCircle size={18} className="shrink-0 text-rose-600" /> : <Info size={18} className="shrink-0 text-primary" />}
          {rejected ? 'Motivo del rechazo' : 'Respuesta de la DIAN'}
        </span>
      }
      description={label}
      size="md"
      footer={<Button type="button" onClick={onClose}>Cerrar</Button>}
    >
      <div className="space-y-3">
        {parsedResponse && 'errors' in parsedResponse && parsedResponse.errors ? (
          parsedResponse.errors.map((e, idx) => (
            <div key={idx} className="p-3 bg-rose-50 border border-rose-100 rounded-xl">
              {e.path && e.path.length > 0 && (
                <p className="text-[10px] font-bold text-rose-400 uppercase mb-1 break-words">{e.path.join(' → ')}</p>
              )}
              <p className="text-sm text-rose-700 break-words">{e.error}</p>
            </div>
          ))
        ) : parsedResponse && 'fields' in parsedResponse ? (
          <>
            <div className="divide-y divide-slate-100">
              {DIAN_RESPONSE_DISPLAY_FIELDS.filter(f => parsedResponse.fields[f.key] != null && parsedResponse.fields[f.key] !== '').map(f => (
                <div key={f.key} className="flex justify-between items-start gap-4 py-2 text-sm">
                  <span className="text-slate-400 font-medium">{f.label}</span>
                  <span className="text-slate-700 font-medium text-right break-all">
                    {f.key === 'payment_means_type'
                      ? (parsedResponse.fields[f.key] === 'CREDITO' ? 'Crédito' : 'Contado')
                      : String(parsedResponse.fields[f.key])}
                  </span>
                </div>
              ))}
            </div>
            {Array.isArray(parsedResponse.fields.dian_messages) && parsedResponse.fields.dian_messages.length > 0 && (
              <div className="space-y-2 py-3 border-t border-slate-100">
                <p className="text-xs font-bold text-slate-400 uppercase">Mensajes de la DIAN</p>
                {[...new Set<string>(parsedResponse.fields.dian_messages.filter((m: unknown): m is string => typeof m === 'string' && m.trim() !== ''))].map(message => (
                  // Una regla de rechazo detiene el documento; las notificaciones solo informan.
                  <p key={message} className={`text-sm rounded-xl border px-3 py-2 break-words ${/Rechazo/i.test(message) ? 'bg-rose-50 border-rose-100 text-rose-700' : 'bg-amber-50 border-amber-100 text-amber-800'}`}>{message}</p>
                ))}
              </div>
            )}
            {parsedResponse.fields.cufe && (
              <div className="py-2 border-t border-slate-100">
                <p className="text-slate-400 font-medium text-sm mb-1">CUFE</p>
                <p className="text-xs font-mono text-slate-600 break-all">{parsedResponse.fields.cufe}</p>
              </div>
            )}
            {(parsedResponse.fields.pdf_url || parsedResponse.fields.xml_url) && (
              <div className="flex flex-col gap-2 pt-3 border-t border-slate-100 sm:flex-row">
                {parsedResponse.fields.pdf_url && (
                  <a href={parsedResponse.fields.pdf_url} target="_blank" rel="noreferrer" className="flex-1 text-center px-4 py-3 sm:py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded-xl text-sm font-bold transition-colors">
                    Ver PDF
                  </a>
                )}
                {parsedResponse.fields.xml_url && (
                  <a href={parsedResponse.fields.xml_url} target="_blank" rel="noreferrer" className="flex-1 text-center px-4 py-3 sm:py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded-xl text-sm font-bold transition-colors">
                    Descargar XML
                  </a>
                )}
              </div>
            )}
            {parsedResponse.fields.qrcode && (
              <div className="pt-3 border-t border-slate-100">
                <p className="text-xs font-bold text-slate-400 uppercase mb-1">Código QR (contenido)</p>
                <pre className="text-[11px] bg-slate-50 border border-slate-200 rounded-xl p-3 whitespace-pre-wrap break-all font-mono text-slate-600 max-h-32 overflow-y-auto">{parsedResponse.fields.qrcode}</pre>
              </div>
            )}
          </>
        ) : (
          <p className="text-sm text-slate-600 whitespace-pre-wrap break-words">{doc.dianResponseMessage}</p>
        )}
      </div>
    </Modal>
  );
}
