import { useEffect, useState } from 'react';
import { Loader2, Save, ShieldCheck, TestTube2, Lock, Unlock } from 'lucide-react';
import { toast } from 'sonner';
import { api } from './api';

type Provider = {
  key: string;
  name: string;
  isActive: boolean;
  sandboxBaseUrl: string;
  productionBaseUrl: string;
  downloadBaseUrl: string;
  raCode: string;
  sandboxCredentialsConfigured: boolean;
  productionCredentialsConfigured: boolean;
};

const initial = {
  name: 'Viafirma RA Colombia',
  isActive: true,
  sandboxEnabled: false,
  sandboxBaseUrl: 'https://sandbox.viafirma.com/ra/api/v2',
  productionBaseUrl: 'https://ecd.viafirma.com/ra/api/v2',
  downloadBaseUrl: 'https://sandbox.viafirma.com',
  raCode: 'viafirmaco',
  sandboxConsumerKey: '',
  sandboxConsumerSecret: '',
  productionConsumerKey: '',
  productionConsumerSecret: '',
  requestTimeoutSeconds: 30,
  maxRetryAttempts: 5
};

export function CertificateProviders() {
  const [form, setForm] = useState(initial);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [testing, setTesting] = useState(false);
  const [syncing, setSyncing] = useState(false);
  const [credentialsUnlocked, setCredentialsUnlocked] = useState(false);

  useEffect(() => {
    api.get<Provider[]>('/certificates/providers')
      .then(({ data }) => {
        const provider = data.find(item => item.key === 'viafirma') ?? data[0];
        if (provider) setForm(current => ({ ...current, ...provider }));
      })
      .catch(() => toast.error('No fue posible cargar la configuración del proveedor.'))
      .finally(() => setLoading(false));
  }, []);

  const update = (key: string, value: string | boolean | number) => setForm(current => ({ ...current, [key]: value }));
  const save = async () => {
    setSaving(true);
    try {
      await api.put('/certificates/providers/viafirma', form);
      toast.success('Configuración guardada. Las credenciales quedaron cifradas.');
      setForm(current => ({ ...current, sandboxConsumerKey: '', sandboxConsumerSecret: '', productionConsumerKey: '', productionConsumerSecret: '' }));
    } catch (error: any) {
      toast.error(error.response?.data?.message ?? 'No fue posible guardar la configuración.');
    } finally { setSaving(false); }
  };

  const testSandbox = async () => {
    setTesting(true);
    try {
      const { data } = await api.post('/certificates/providers/viafirma/test?environment=Sandbox');
      toast.success(`Conexión correcta. Perfiles disponibles: ${data.profiles}.`);
    } catch (error: any) {
      toast.error(error.response?.data?.message ?? 'La conexión con Sandbox falló.');
    } finally { setTesting(false); }
  };

  const syncSandboxProfiles = async () => {
    setSyncing(true);
    try {
      const { data } = await api.post('/certificates/providers/viafirma/profiles/sync?environment=Sandbox');
      toast.success(`Perfiles sincronizados correctamente: ${data.profiles}.`);
    } catch (error: any) {
      toast.error(error.response?.data?.message ?? 'No fue posible sincronizar los perfiles de Sandbox.');
    } finally { setSyncing(false); }
  };

  if (loading) return <div className="p-10 text-slate-400"><Loader2 className="animate-spin" /></div>;
  const input = (key: keyof typeof form, label: string, type = 'text') => (
    <label className="block text-sm text-slate-300">
      {label}
      <input autoComplete="off" type={type} value={String(form[key])} onChange={event => update(key, type === 'number' ? Number(event.target.value) : event.target.value)} className="mt-2 w-full rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 text-white" />
    </label>
  );
  const credentialInput = (key: keyof typeof form, label: string, type = 'text') => (
    <label className="block text-sm text-slate-300">
      {label}
      <input autoComplete="new-password" readOnly={!credentialsUnlocked} type={type} value={String(form[key])} onChange={event => update(key, event.target.value)} className={`mt-2 w-full rounded-lg border px-3 py-2 text-white ${credentialsUnlocked ? 'border-slate-700 bg-slate-900' : 'border-slate-800 bg-slate-950 text-slate-500 cursor-not-allowed'}`} />
    </label>
  );

  return <div className="p-10 max-w-5xl">
    <div className="flex items-center justify-between mb-8">
      <div><h1 className="text-3xl font-extrabold text-white">Proveedor de certificados</h1><p className="text-slate-400 mt-2">Configura Viafirma por ambiente. Las credenciales nunca se muestran después de guardarlas.</p></div>
      <ShieldCheck className="w-10 h-10 text-emerald-400" />
    </div>
    <div className="grid gap-6 md:grid-cols-2">
      <section className="rounded-2xl border border-slate-800 bg-slate-950/60 p-6 space-y-4"><h2 className="text-lg font-bold text-white">Proveedor</h2>{input('name', 'Nombre')}{input('raCode', 'Código RA')}{input('sandboxBaseUrl', 'URL API Sandbox')}{input('productionBaseUrl', 'URL API Producción')}{input('downloadBaseUrl', 'URL de descarga')}
        <label className="flex items-center gap-3 text-sm text-slate-300"><input type="checkbox" checked={form.isActive} onChange={event => update('isActive', event.target.checked)} /> Proveedor activo</label>
        <label className="flex items-center gap-3 text-sm text-slate-300"><input type="checkbox" checked={form.sandboxEnabled} onChange={event => update('sandboxEnabled', event.target.checked)} /> Permitir solicitudes Sandbox a los tenants</label>
      </section>
        <section className="rounded-2xl border border-slate-800 bg-slate-950/60 p-6 space-y-4"><div className="flex items-center justify-between"><h2 className="text-lg font-bold text-white">Credenciales Sandbox</h2><button type="button" onClick={() => setCredentialsUnlocked(value => !value)} className="flex items-center gap-2 rounded-lg border border-slate-700 px-3 py-2 text-xs text-slate-300">{credentialsUnlocked ? <Unlock className="w-4" /> : <Lock className="w-4" />} {credentialsUnlocked ? 'Bloquear' : 'Desbloquear credenciales'}</button></div>{credentialInput('sandboxConsumerKey', 'Consumer key')}{credentialInput('sandboxConsumerSecret', 'Consumer secret', 'password')}<p className="text-xs text-slate-500">Los campos permanecen bloqueados y no aceptan autocompletado hasta desbloquearlos.</p><div className="flex flex-wrap gap-3"><button onClick={testSandbox} disabled={testing} className="flex items-center gap-2 rounded-lg border border-emerald-700 px-4 py-2 text-emerald-300">{testing ? <Loader2 className="animate-spin" /> : <TestTube2 className="w-4" />} Probar Sandbox</button><button onClick={syncSandboxProfiles} disabled={syncing} className="flex items-center gap-2 rounded-lg border border-blue-700 px-4 py-2 text-blue-300">{syncing ? <Loader2 className="animate-spin" /> : <ShieldCheck className="w-4" />} Sincronizar perfiles</button></div></section>
      <section className="rounded-2xl border border-slate-800 bg-slate-950/60 p-6 space-y-4"><h2 className="text-lg font-bold text-white">Credenciales Producción</h2>{credentialInput('productionConsumerKey', 'Consumer key')}{credentialInput('productionConsumerSecret', 'Consumer secret', 'password')}<p className="text-xs text-slate-500">La configuración queda separada de Sandbox y cifrada en la base de datos.</p></section>
      <section className="rounded-2xl border border-slate-800 bg-slate-950/60 p-6 space-y-4"><h2 className="text-lg font-bold text-white">Operación</h2>{input('requestTimeoutSeconds', 'Timeout en segundos', 'number')}{input('maxRetryAttempts', 'Máximo de reintentos', 'number')}</section>
    </div>
    <button onClick={save} disabled={saving} className="mt-6 flex items-center gap-2 rounded-lg bg-blue-600 px-5 py-3 font-semibold text-white hover:bg-blue-500">{saving ? <Loader2 className="animate-spin" /> : <Save className="w-4" />} Guardar configuración</button>
  </div>;
}
