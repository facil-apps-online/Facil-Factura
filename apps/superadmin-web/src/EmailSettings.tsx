import { useEffect, useState } from 'react';
import { Loader2, Mail, Save, Zap } from 'lucide-react';
import { toast } from 'sonner';
import { api } from './api';

type Problem = { id: string; kind: string; status: string; recipientEmail: string; subject: string; attempts: number; channel: string; lastError: string; updatedAt: string };
type Settings = {
  apiKeyConfigured: boolean; documentsFromEmail: string; certificatesFromEmail: string; notificationsFromEmail: string;
  platformFromEmail: string; platformFromName: string; supportEmail: string; updatedAt: string; updatedBy: string;
  queue: { pending: number; held: number; failed: number; dead: number };
  recentProblems: Problem[];
};

const KIND_LABELS: Record<string, string> = {
  Document: 'Documento', Certificate: 'Certificado', CertificatePassword: 'Contraseña de certificado', Notification: 'Aviso', Platform: 'Plataforma',
};

const errorMessage = (err: unknown, fallback: string) =>
  (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? fallback;

// Correo de la plataforma: la API key de Brevo (se guarda cifrada y nunca se vuelve a mostrar), los remitentes por tipo de correo y el
// estado de la bandeja de salida. Un correo sale por el SMTP del cliente, el del tenant o, si no hay, por Brevo con esta configuración.
export function EmailSettings() {
  const [data, setData] = useState<Settings | null>(null);
  const [apiKey, setApiKey] = useState('');
  const [form, setForm] = useState({ documentsFromEmail: '', certificatesFromEmail: '', notificationsFromEmail: '', platformFromEmail: '', platformFromName: '', supportEmail: '' });
  const [saving, setSaving] = useState(false);
  const [testing, setTesting] = useState(false);
  const [testResult, setTestResult] = useState<{ success: boolean; message: string } | null>(null);

  const load = () => api.get<Settings>('/email-settings')
    .then(res => {
      setData(res.data);
      setForm({
        documentsFromEmail: res.data.documentsFromEmail, certificatesFromEmail: res.data.certificatesFromEmail,
        notificationsFromEmail: res.data.notificationsFromEmail, platformFromEmail: res.data.platformFromEmail,
        platformFromName: res.data.platformFromName, supportEmail: res.data.supportEmail,
      });
    })
    .catch(() => toast.error('No fue posible cargar la configuración de correo.'));

  useEffect(() => { void load(); }, []);

  const save = async () => {
    setSaving(true);
    try {
      await api.put('/email-settings', { brevoApiKey: apiKey || null, ...form });
      setApiKey('');
      toast.success('Configuración de correo guardada.');
      await load();
    } catch (err: unknown) {
      toast.error(errorMessage(err, 'No fue posible guardar la configuración.'));
    } finally {
      setSaving(false);
    }
  };

  const test = async () => {
    setTesting(true);
    setTestResult(null);
    try {
      const res = await api.post<{ success: boolean; message: string }>('/email-settings/test');
      setTestResult(res.data);
    } catch (err: unknown) {
      setTestResult({ success: false, message: errorMessage(err, 'No fue posible probar la conexión.') });
    } finally {
      setTesting(false);
    }
  };

  if (!data) return <div className="p-10 text-slate-400"><Loader2 className="animate-spin" /></div>;

  const field = 'mt-1 w-full rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 text-white';
  const set = (key: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement>) => setForm({ ...form, [key]: e.target.value });

  return (
    <div className="p-10 max-w-5xl">
      <div className="flex items-center justify-between mb-8">
        <div>
          <h1 className="text-3xl font-extrabold text-white">Correo de la plataforma</h1>
          <p className="text-slate-400 mt-2">Todos los correos de Facil Factura salen de aquí, nunca por Core. Cada correo usa el SMTP del cliente, si no tiene el del tenant y, si tampoco, Brevo con esta configuración.</p>
        </div>
        <Mail className="w-10 h-10 text-blue-400 shrink-0" />
      </div>

      <section className="rounded-2xl border border-slate-800 bg-slate-950/60 p-6 mb-8">
        <h2 className="text-lg font-bold text-white mb-1">Brevo</h2>
        <p className="text-sm text-slate-400 mb-4">
          La API key se guarda cifrada y no se vuelve a mostrar. {data.apiKeyConfigured ? 'Ya hay una guardada: déjala vacía para conservarla.' : 'Todavía no hay ninguna guardada: sin ella los correos que no tengan SMTP propio no se pueden enviar.'}
        </p>
        <label className="block text-sm text-slate-300">API key de Brevo
          <input type="password" autoComplete="off" value={apiKey} onChange={e => setApiKey(e.target.value)} placeholder={data.apiKeyConfigured ? '•••••••• (guardada)' : 'xkeysib-…'} className={field} />
        </label>
        <div className="mt-4 flex items-center gap-4">
          <button onClick={test} disabled={testing || !data.apiKeyConfigured} className="flex items-center gap-2 rounded-lg border border-slate-600 px-4 py-2 font-semibold text-slate-200 hover:bg-slate-800 disabled:opacity-40">
            {testing ? <Loader2 className="w-4 animate-spin" /> : <Zap className="w-4" />} Probar conexión
          </button>
          {testResult && <span className={`text-sm ${testResult.success ? 'text-emerald-300' : 'text-rose-300'}`}>{testResult.message}</span>}
        </div>
        {apiKey && <p className="mt-2 text-xs text-amber-300">La prueba usa la key guardada: guarda primero la nueva.</p>}
      </section>

      <section className="rounded-2xl border border-slate-800 bg-slate-950/60 p-6 mb-8">
        <h2 className="text-lg font-bold text-white mb-1">Remitentes</h2>
        <p className="text-sm text-slate-400 mb-4">Solo aplican cuando el correo sale por Brevo; con SMTP propio sale desde ese buzón. Las direcciones deben ser del dominio autenticado en Brevo. El nombre visible es el del cliente o tenant que origina el correo.</p>
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
          <label className="block text-sm text-slate-300">Documentos y reenvíos
            <input value={form.documentsFromEmail} onChange={set('documentsFromEmail')} className={field} />
          </label>
          <label className="block text-sm text-slate-300">Certificados
            <input value={form.certificatesFromEmail} onChange={set('certificatesFromEmail')} className={field} />
          </label>
          <label className="block text-sm text-slate-300">Portal de clientes (avisos de plan)
            <input value={form.notificationsFromEmail} onChange={set('notificationsFromEmail')} className={field} />
          </label>
          <label className="block text-sm text-slate-300">Plataforma (usuarios de tenant y superadmin)
            <input value={form.platformFromEmail} onChange={set('platformFromEmail')} className={field} />
          </label>
          <label className="block text-sm text-slate-300">Nombre visible de la plataforma
            <input value={form.platformFromName} onChange={set('platformFromName')} className={field} />
          </label>
          <label className="block text-sm text-slate-300">Correo de soporte (respuestas de la plataforma)
            <input value={form.supportEmail} onChange={set('supportEmail')} className={field} />
          </label>
        </div>
        <div className="mt-5 flex items-center justify-between">
          <span className="text-xs text-slate-500">{data.updatedBy ? `Última actualización: ${new Date(data.updatedAt).toLocaleString('es-CO')} · ${data.updatedBy}` : ''}</span>
          <button onClick={save} disabled={saving} className="flex items-center gap-2 rounded-lg bg-blue-600 px-5 py-2.5 font-semibold text-white hover:bg-blue-500 disabled:opacity-50">
            {saving ? <Loader2 className="w-4 animate-spin" /> : <Save className="w-4" />} Guardar
          </button>
        </div>
      </section>

      <section className="rounded-2xl border border-slate-800 bg-slate-950/60 p-6">
        <h2 className="text-lg font-bold text-white mb-4">Bandeja de salida</h2>
        <div className="grid grid-cols-2 gap-4 md:grid-cols-4 mb-5">
          {([['En cola', data.queue.pending, 'text-slate-200'], ['Retenidos', data.queue.held, 'text-slate-200'], ['Reintentando', data.queue.failed, 'text-amber-300'], ['Sin enviar', data.queue.dead, 'text-rose-300']] as const).map(([label, value, color]) => (
            <div key={label} className="rounded-xl border border-slate-800 p-4">
              <p className="text-xs uppercase tracking-wider text-slate-500">{label}</p>
              <p className={`text-2xl font-bold ${color}`}>{value}</p>
            </div>
          ))}
        </div>
        {data.recentProblems.length === 0 ? (
          <p className="text-sm text-slate-500">No hay correos con problemas.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="text-left text-slate-400">
                <tr><th className="py-2 pr-4">Fecha</th><th className="py-2 pr-4">Tipo</th><th className="py-2 pr-4">Destinatario</th><th className="py-2 pr-4">Intentos</th><th className="py-2">Último error</th></tr>
              </thead>
              <tbody className="divide-y divide-slate-800 text-slate-200">
                {data.recentProblems.map(p => (
                  <tr key={p.id}>
                    <td className="py-2 pr-4 whitespace-nowrap">{new Date(p.updatedAt).toLocaleString('es-CO')}</td>
                    <td className="py-2 pr-4">{KIND_LABELS[p.kind] ?? p.kind}</td>
                    <td className="py-2 pr-4">{p.recipientEmail}</td>
                    <td className="py-2 pr-4">{p.attempts}{p.status === 'Dead' ? ' (agotados)' : ''}</td>
                    <td className="py-2 text-rose-300">{p.lastError}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}
