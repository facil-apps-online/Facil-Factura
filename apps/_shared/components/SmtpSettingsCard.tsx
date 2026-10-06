import React, { useEffect, useState } from 'react';
import { Mail } from 'lucide-react';
import { toast } from 'sonner';
import { CredentialLockButton, lockedInputClass, lockedInputProps, useCredentialLock } from './CredentialLock';

// Formulario del SMTP propio para reenviar documentos ya aprobados (emisión directa a la DIAN, sin Dataico). Lo usan los tres lugares donde
// se configura (el tenant para sí mismo, el tenant para uno de sus clientes y el cliente en su portal): los tres hablan el mismo contrato
// (GET/PUT {basePath} y POST {basePath}/test-connection), así que solo cambian la ruta y los textos. Es un formulario de credenciales:
// arranca bloqueado y sin autocompletado, y el lapicito lo habilita.

// Lo mínimo que se usa de axios, para no depender de su tipo desde el código compartido.
interface Http {
  get(url: string): Promise<{ data: any }>;
  put(url: string, body?: any): Promise<unknown>;
  post(url: string, body?: any): Promise<{ data: any }>;
}

interface SmtpSettings {
  smtpHost?: string;
  smtpPort?: number;
  smtpUseSsl?: boolean;
  smtpUser?: string;
  smtpFromEmail?: string;
  smtpFromName?: string;
  hasPassword?: boolean;
}

const input = 'w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl outline-none focus:ring-2 focus:ring-primary';
const label = 'block text-sm font-bold text-slate-700 mb-1.5';

function messageOf(err: any, fallback: string): string {
  const data = err?.response?.data;
  if (typeof data?.message === 'string') return data.message;
  if (typeof data === 'string' && data) return data;
  return fallback;
}

export default function SmtpSettingsCard({ api, basePath, title = 'Correo para Reenvío de Documentos', description, className = 'bg-white rounded-3xl p-8 shadow-sm border border-slate-100' }: {
  api: Http;
  basePath: string;
  title?: string;
  description: React.ReactNode;
  className?: string;
}) {
  const [smtp, setSmtp] = useState<SmtpSettings>({ smtpHost: '', smtpPort: 587, smtpUseSsl: true, smtpUser: '', smtpFromEmail: '', smtpFromName: '', hasPassword: false });
  const [passwordDraft, setPasswordDraft] = useState('');
  const [saving, setSaving] = useState(false);
  const [testing, setTesting] = useState(false);
  const { unlocked, lock, toggle } = useCredentialLock();

  const load = () => {
    api.get(basePath)
      .then(res => { setSmtp(res.data); lock(); })
      .catch(() => toast.error('No se pudo cargar la configuración SMTP'));
  };
  useEffect(load, [basePath]);

  const save = async () => {
    setSaving(true);
    try {
      await api.put(basePath, { ...smtp, smtpPassword: passwordDraft || undefined });
      toast.success('Configuración SMTP guardada');
      setPasswordDraft('');
      load();
    } catch (err) {
      toast.error(messageOf(err, 'Error al guardar la configuración SMTP'));
    } finally {
      setSaving(false);
    }
  };

  const test = async () => {
    setTesting(true);
    try {
      const res = await api.post(`${basePath}/test-connection`);
      if (res.data.success) toast.success(res.data.message);
      else toast.error(res.data.message);
    } catch (err) {
      toast.error(messageOf(err, 'Error al probar la conexión'));
    } finally {
      setTesting(false);
    }
  };

  const field = (type: string, value: string | number | undefined, onChange: (v: string) => void, extra: Record<string, string> = {}) => (
    <input type={type} {...lockedInputProps(unlocked)} {...extra} value={value ?? ''} onChange={e => onChange(e.target.value)} className={lockedInputClass(unlocked, input)} />
  );

  return (
    <div className={className} data-smtp-card>
      <div className="flex items-start justify-between gap-4 mb-1">
        <h2 className="text-xl font-bold text-slate-800 flex items-center gap-2">
          <Mail className="w-5 h-5 text-primary" /> {title}
        </h2>
        <CredentialLockButton unlocked={unlocked} onToggle={toggle} />
      </div>
      <p className="text-slate-500 text-sm mb-6">{description}</p>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-5 max-w-3xl">
        <div>
          <label className={label}>Servidor SMTP</label>
          {field('text', smtp.smtpHost, v => setSmtp({ ...smtp, smtpHost: v }), { placeholder: 'smtp.gmail.com' })}
        </div>
        <div>
          <label className={label}>Puerto</label>
          {field('number', smtp.smtpPort || 587, v => setSmtp({ ...smtp, smtpPort: parseInt(v) || 587 }))}
        </div>
        <div>
          <label className={label}>Usuario</label>
          {field('text', smtp.smtpUser, v => setSmtp({ ...smtp, smtpUser: v }))}
        </div>
        <div>
          <label className={label}>
            Contraseña {smtp.hasPassword && <span className="text-emerald-600 font-normal">(ya guardada — deja en blanco para no cambiarla)</span>}
          </label>
          {field('password', passwordDraft, setPasswordDraft, { placeholder: smtp.hasPassword ? '••••••••' : '' })}
        </div>
        <div>
          <label className={label}>Correo remitente</label>
          {field('email', smtp.smtpFromEmail, v => setSmtp({ ...smtp, smtpFromEmail: v }))}
        </div>
        <div>
          <label className={label}>Nombre remitente</label>
          {field('text', smtp.smtpFromName, v => setSmtp({ ...smtp, smtpFromName: v }))}
        </div>
      </div>

      <label className="flex items-center gap-2 mt-4 cursor-pointer">
        <input type="checkbox" disabled={!unlocked} checked={!!smtp.smtpUseSsl} onChange={e => setSmtp({ ...smtp, smtpUseSsl: e.target.checked })} className="w-4 h-4 rounded accent-primary" />
        <span className="text-sm text-slate-600">Usar conexión segura (SSL/TLS)</span>
      </label>

      <div className="flex flex-col gap-3 sm:flex-row mt-6">
        {unlocked && (
          <button onClick={save} disabled={saving} className="bg-primary hover:bg-primary-hover disabled:opacity-50 text-white px-6 py-2.5 rounded-xl font-bold shadow-md transition-all">
            {saving ? 'Guardando...' : 'Guardar configuración'}
          </button>
        )}
        <button onClick={test} disabled={testing} className="bg-slate-100 hover:bg-slate-200 disabled:opacity-50 text-slate-700 px-6 py-2.5 rounded-xl font-bold transition-all">
          {testing ? 'Probando...' : 'Probar conexión'}
        </button>
      </div>
    </div>
  );
}
