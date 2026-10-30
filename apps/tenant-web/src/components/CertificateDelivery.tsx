import { useState } from 'react';
import { Copy, Download, Eye, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { api, getErrorMessage } from '../lib/api';
import { money } from '../lib/certificateFormat';

// Descarga del .p12 (con la clave privada) y, aparte, su contraseña. Cada acción queda registrada en la plataforma.
// fileName: nombre sugerido si el navegador no deja leer el del servidor (Content-Disposition no siempre se expone por CORS).
export function CertificateDeliveryButtons({ certificateId, fileName, compact = false }: { certificateId: string; fileName?: string; compact?: boolean }) {
  const [downloading, setDownloading] = useState(false);
  const [password, setPassword] = useState<string | null>(null);
  const [loadingPassword, setLoadingPassword] = useState(false);

  const download = async () => {
    setDownloading(true);
    try {
      const res = await api.get(`/tenant/certificates/${certificateId}/download`, { responseType: 'blob' });
      const disposition = String(res.headers['content-disposition'] ?? '');
      const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
      const name = match ? decodeURIComponent(match[1]) : `${(fileName || 'certificado').replace(/[^\p{L}\p{N}-]+/gu, '_')}.p12`;
      const url = URL.createObjectURL(res.data);
      const link = document.createElement('a');
      link.href = url;
      link.download = name;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
    } catch (err) {
      toast.error(getErrorMessage(err, 'No fue posible descargar el certificado'));
    } finally {
      setDownloading(false);
    }
  };

  const showPassword = async () => {
    if (password) { setPassword(null); return; }
    setLoadingPassword(true);
    try {
      const res = await api.get(`/tenant/certificates/${certificateId}/password`);
      setPassword(res.data.password);
    } catch (err) {
      toast.error(getErrorMessage(err, 'No fue posible consultar la contraseña'));
    } finally {
      setLoadingPassword(false);
    }
  };

  const copy = async () => {
    if (!password) return;
    try {
      await navigator.clipboard.writeText(password);
      toast.success('Contraseña copiada');
    } catch {
      toast.error('No se pudo copiar; selecciónala y cópiala a mano.');
    }
  };

  const button = `inline-flex items-center gap-1.5 rounded-lg border border-slate-200 bg-white font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-50 ${compact ? 'px-2.5 py-1.5 text-xs' : 'px-3 py-2 text-sm'}`;
  return (
    <div className="flex flex-wrap items-center gap-2">
      <button type="button" onClick={download} disabled={downloading} className={button} title="Descargar el archivo .p12 (incluye la clave privada)">
        {downloading ? <Loader2 size={14} className="animate-spin" /> : <Download size={14} />} .p12
      </button>
      <button type="button" onClick={showPassword} disabled={loadingPassword} className={button} title="Ver la contraseña del archivo .p12">
        {loadingPassword ? <Loader2 size={14} className="animate-spin" /> : <Eye size={14} />} {password ? 'Ocultar' : 'Contraseña'}
      </button>
      {password && (
        <span className="inline-flex items-center gap-1.5 rounded-lg bg-slate-900 px-2.5 py-1.5 font-mono text-xs text-white">
          {password}
          <button type="button" onClick={copy} className="text-slate-300 hover:text-white" aria-label="Copiar contraseña"><Copy size={12} /></button>
        </span>
      )}
    </div>
  );
}

// Antes de solicitar un certificado de producción: cuánto cuesta y si el saldo prepagado alcanza.
export function CertificatePriceNotice({ price, available }: { price: number | null | undefined; available: number | null | undefined }) {
  if (price == null) {
    return <div className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-800">Este certificado no tiene precio vigente, así que todavía no se puede solicitar en producción. Consulta a tu proveedor.</div>;
  }
  const enough = (available ?? 0) >= price;
  return (
    <div className={`rounded-xl border p-4 text-sm ${enough ? 'border-emerald-200 bg-emerald-50 text-emerald-900' : 'border-rose-200 bg-rose-50 text-rose-800'}`}>
      Valor: <strong>{money(price)}</strong> (IVA incluido), se descuenta de tu saldo al solicitar y se devuelve si Viafirma lo rechaza. Saldo disponible: <strong>{money(available)}</strong>.
      {!enough && ' No alcanza: abona saldo con tu proveedor para continuar.'}
    </div>
  );
}
