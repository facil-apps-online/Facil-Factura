import React, { useEffect, useState } from 'react';
import { Mail, ShieldCheck, Stethoscope, Undo2 } from 'lucide-react';
import { toast } from 'sonner';
import SearchableSelect from '@shared/components/SearchableSelect';
import { CredentialLockButton, lockedInputClass, lockedInputProps, useCredentialLock } from '@shared/components/CredentialLock';
import { api, getErrorMessage } from '../lib/api';

// Credenciales que cada sucursal puede tener propias (MinSalud, IHCE y buzón de recepción). Cada tarjeta se edita para "todo el cliente"
// (el valor por defecto) o para una sucursal: sin credenciales propias la sucursal hereda las del cliente; al guardar las suyas las
// reemplazan por completo, y "Volver a heredar" las quita. Los formularios arrancan bloqueados (lapicito) para que el navegador no los
// autocomplete ni se cambien por accidente.

type Scope = 'client' | string;
type Kind = 'minsalud' | 'ihce' | 'reception';

interface BranchOption { id: string; name: string; isMain: boolean; isActive: boolean }

const inputBase = 'w-full px-4 py-2.5 border border-slate-200 rounded-xl outline-none focus:ring-2 focus:ring-blue-500';
const labelClass = 'block text-xs font-bold text-slate-500 uppercase mb-1';
const selectInputClass = (unlocked: boolean) => lockedInputClass(unlocked, 'w-full px-4 py-2.5 pr-8 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none bg-white');

function endpoint(clientId: string, kind: Kind, scope: Scope) {
  if (scope === 'client') {
    return { minsalud: `/tenant/clients/${clientId}/minsalud-config`, ihce: `/tenant/clients/${clientId}/ihce-config`, reception: `/tenant/clients/${clientId}/reception-settings` }[kind];
  }
  return `/tenant/clients/${clientId}/branches/${scope}/credentials/${kind}`;
}

function useScope(clientId: string) {
  const [branches, setBranches] = useState<BranchOption[]>([]);
  const [scope, setScope] = useState<Scope>('client');
  useEffect(() => {
    api.get(`/tenant/clients/${clientId}/branches`).then(res => setBranches(res.data)).catch(() => {});
  }, [clientId]);
  return { branches, scope, setScope };
}

function Card({ icon, title, description, clientId, kind, children, header, lock }: {
  icon: React.ReactNode; title: string; description: string; clientId: string; kind: Kind;
  header: React.ReactNode; children: React.ReactNode; lock: React.ReactNode;
}) {
  return (
    <div className="bg-white rounded-3xl p-8 shadow-sm border border-slate-100" data-credential-card={kind} data-client={clientId}>
      <div className="flex items-start justify-between gap-4 mb-1">
        <div className="flex items-center gap-2">{icon}<h2 className="text-xl font-bold text-slate-800">{title}</h2></div>
        {lock}
      </div>
      <p className="text-slate-500 text-sm mb-5">{description}</p>
      {header}
      {children}
    </div>
  );
}

function ScopeBar({ branches, scope, setScope, isOwn, onRemove, disabled }: {
  branches: BranchOption[]; scope: Scope; setScope: (s: Scope) => void; isOwn: boolean; onRemove: () => void; disabled: boolean;
}) {
  if (branches.length <= 1) return null; // con una sola sucursal no hay a qué aplicarlo por separado
  return (
    <div className="mb-5 rounded-xl border border-slate-200 bg-slate-50/70 p-4 space-y-3">
      <div className="flex flex-wrap items-center gap-3">
        <label className="text-sm font-semibold text-slate-700">Aplica a</label>
        <select value={scope} onChange={e => setScope(e.target.value)} disabled={disabled}
          className="px-3 py-2 bg-white border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-blue-500 outline-none">
          <option value="client">Todo el cliente (valor por defecto)</option>
          {branches.map(b => <option key={b.id} value={b.id}>{b.name}{b.isMain ? ' (principal)' : ''}{b.isActive ? '' : ' — inactiva'}</option>)}
        </select>
        {scope !== 'client' && (
          <span className={`text-xs font-bold px-2 py-0.5 rounded-full ${isOwn ? 'bg-blue-100 text-blue-700' : 'bg-slate-200 text-slate-600'}`}>
            {isOwn ? 'Credenciales propias de la sucursal' : 'Hereda las del cliente'}
          </span>
        )}
        {scope !== 'client' && isOwn && (
          <button type="button" onClick={onRemove} className="flex items-center gap-1 text-xs font-semibold text-slate-500 hover:text-rose-600 transition-colors">
            <Undo2 size={14} /> Volver a heredar
          </button>
        )}
      </div>
      {scope !== 'client' && !isOwn && (
        <p className="text-xs text-slate-500">Estás viendo las del cliente. Si guardas aquí, la sucursal tendrá las suyas y deberás escribir también las claves: no se copian las del cliente.</p>
      )}
    </div>
  );
}

// ---------------- MinSalud ----------------

export function MinSaludCard({ clientId }: { clientId: string }) {
  const { branches, scope, setScope } = useScope(clientId);
  const { unlocked, lock, toggle } = useCredentialLock();
  const [form, setForm] = useState<any>({ minSaludEnvironment: 'Test', minSaludUserType: '', minSaludIdentificationType: 'CC', minSaludIdentificationNumber: '', minSaludTestIdentificationType: 'CC', minSaludTestIdentificationNumber: '' });
  const [passwords, setPasswords] = useState({ prod: '', test: '' });
  const [meta, setMeta] = useState({ hasPassword: false, hasTestPassword: false, isBranchOwn: false });
  const [catalogs, setCatalogs] = useState<{ documentTypes: any[]; userTypes: any[]; environments: any[] }>({ documentTypes: [], userTypes: [], environments: [] });
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    api.get('/tenant/clients/minsalud-catalogs').then(res => setCatalogs(res.data)).catch(() => toast.error('No se pudieron cargar los catálogos de MinSalud.'));
  }, []);

  const load = () => {
    api.get(endpoint(clientId, 'minsalud', scope)).then(res => {
      const d = res.data;
      setForm({
        minSaludEnvironment: d.minSaludEnvironment || 'Test', minSaludUserType: d.minSaludUserType || '',
        minSaludIdentificationType: d.minSaludIdentificationType || 'CC', minSaludIdentificationNumber: d.minSaludIdentificationNumber || '',
        minSaludTestIdentificationType: d.minSaludTestIdentificationType || 'CC', minSaludTestIdentificationNumber: d.minSaludTestIdentificationNumber || ''
      });
      setMeta({ hasPassword: !!d.hasPassword, hasTestPassword: !!d.hasTestPassword, isBranchOwn: !!d.isBranchOwn });
      setPasswords({ prod: '', test: '' });
      lock();
    }).catch(err => toast.error(getErrorMessage(err, 'No se pudo cargar la configuración de MinSalud.')));
  };
  useEffect(load, [clientId, scope]);

  const save = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    try {
      await api.put(endpoint(clientId, 'minsalud', scope), { ...form, minSaludPassword: passwords.prod, minSaludTestPassword: passwords.test });
      toast.success(scope === 'client' ? 'Configuración de MinSalud (RIPS) actualizada.' : 'Credenciales de MinSalud de la sucursal guardadas.');
      load();
    } catch (err) {
      toast.error(getErrorMessage(err, 'Error al guardar la configuración.'));
    } finally {
      setSaving(false);
    }
  };

  const remove = async () => {
    if (!window.confirm('¿Quitar las credenciales propias de esta sucursal? Volverá a usar las del cliente.')) return;
    try {
      await api.delete(endpoint(clientId, 'minsalud', scope));
      toast.success('La sucursal vuelve a heredar las credenciales del cliente.');
      load();
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo quitar la configuración propia.'));
    }
  };

  const docTypeOptions = catalogs.documentTypes.map(o => ({ value: o.code, label: `${o.code} - ${o.name}`, shortLabel: o.code }));
  const block = (title: string, active: boolean, idType: string, idNumber: string, hasPassword: boolean, passwordKey: 'prod' | 'test') => (
    <div className={`rounded-xl border p-4 ${active ? 'border-blue-200 bg-blue-50/40' : 'border-slate-200 bg-slate-50/60'}`}>
      <h3 className="text-sm font-bold text-slate-700 mb-3">{title}{active && <span className="ml-2 text-xs font-medium text-blue-600">(en uso)</span>}</h3>
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <div>
          <label className={labelClass}>Tipo de Documento</label>
          <SearchableSelect options={docTypeOptions} value={form[idType]} disabled={!unlocked} onChange={v => setForm({ ...form, [idType]: v })} inputClassName={selectInputClass(unlocked)} />
        </div>
        <div>
          <label className={labelClass}>Número de Documento</label>
          <input type="text" {...lockedInputProps(unlocked)} value={form[idNumber]} onChange={e => setForm({ ...form, [idNumber]: e.target.value })} className={lockedInputClass(unlocked, `${inputBase} font-mono text-sm`)} />
        </div>
        <div>
          <label className={labelClass}>Contraseña {hasPassword && <span className="text-emerald-600 normal-case font-normal">(configurada)</span>}</label>
          <input type="password" {...lockedInputProps(unlocked)} placeholder={hasPassword ? 'Dejar vacío para no cambiar' : ''} value={passwords[passwordKey]}
            onChange={e => setPasswords({ ...passwords, [passwordKey]: e.target.value })} className={lockedInputClass(unlocked, inputBase)} />
        </div>
      </div>
    </div>
  );

  return (
    <Card clientId={clientId} kind="minsalud" icon={<Stethoscope className="text-blue-600" size={20} />} title="MinSalud / RIPS (MUV-FEV-RIPS)"
      description="Credenciales del prestador ante SISPRO para el envío de RIPS al Ministerio de Salud. Solo aplica a emisores del sector salud."
      lock={<CredentialLockButton unlocked={unlocked} onToggle={toggle} />}
      header={<ScopeBar branches={branches} scope={scope} setScope={setScope} isOwn={meta.isBranchOwn} onRemove={remove} disabled={saving} />}>
      <form onSubmit={save} className="space-y-5" autoComplete="off">
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label className={labelClass}>Ambiente</label>
            <SearchableSelect options={catalogs.environments.map(o => ({ value: o.code, label: o.name }))} value={form.minSaludEnvironment} disabled={!unlocked}
              onChange={v => setForm({ ...form, minSaludEnvironment: v })} inputClassName={selectInputClass(unlocked)} />
            <p className="text-xs text-slate-400 mt-1">{form.minSaludEnvironment === 'Production' ? 'Lo que se emita cuenta como reporte real ante el Ministerio.' : 'Emite contra el ambiente de pruebas del Ministerio.'}</p>
          </div>
          <div>
            <label className={labelClass}>Tipo de Usuario</label>
            <SearchableSelect options={catalogs.userTypes.map(o => ({ value: o.code, label: `${o.code} - ${o.name}`, shortLabel: o.code }))} value={form.minSaludUserType} disabled={!unlocked}
              onChange={v => setForm({ ...form, minSaludUserType: v })} placeholder="(no informar)" inputClassName={selectInputClass(unlocked)} />
            <p className="text-xs text-slate-400 mt-1">Para PSS y PTS el manual exige RE.</p>
          </div>
        </div>
        {block('Credenciales de producción', form.minSaludEnvironment === 'Production', 'minSaludIdentificationType', 'minSaludIdentificationNumber', meta.hasPassword, 'prod')}
        {block('Credenciales de pruebas', form.minSaludEnvironment === 'Test', 'minSaludTestIdentificationType', 'minSaludTestIdentificationNumber', meta.hasTestPassword, 'test')}
        {unlocked && (
          <div className="flex justify-end pt-2">
            <button type="submit" disabled={saving} className="px-5 py-2.5 bg-blue-600 text-white rounded-lg font-medium hover:bg-blue-500 transition-colors disabled:opacity-50">
              {saving ? 'Guardando...' : 'Guardar MinSalud'}
            </button>
          </div>
        )}
      </form>
    </Card>
  );
}

// ---------------- IHCE ----------------

export function IhceCard({ clientId }: { clientId: string }) {
  const { branches, scope, setScope } = useScope(clientId);
  const { unlocked, lock, toggle } = useCredentialLock();
  const [form, setForm] = useState({ ihceClientId: '', ihceTenantId: '', ihceEndpoint: '', ihceEnvironment: 'Sandbox' });
  const [secrets, setSecrets] = useState({ clientSecret: '', apimKey: '' });
  const [meta, setMeta] = useState({ hasClientSecret: false, hasApimSubscriptionKey: false, isBranchOwn: false });
  const [saving, setSaving] = useState(false);

  const load = () => {
    api.get(endpoint(clientId, 'ihce', scope)).then(res => {
      const d = res.data;
      setForm({ ihceClientId: d.ihceClientId || '', ihceTenantId: d.ihceTenantId || '', ihceEndpoint: d.ihceEndpoint || '', ihceEnvironment: d.ihceEnvironment || 'Sandbox' });
      setMeta({ hasClientSecret: !!d.hasClientSecret, hasApimSubscriptionKey: !!d.hasApimSubscriptionKey, isBranchOwn: !!d.isBranchOwn });
      setSecrets({ clientSecret: '', apimKey: '' });
      lock();
    }).catch(err => toast.error(getErrorMessage(err, 'No se pudo cargar la configuración de IHCE.')));
  };
  useEffect(load, [clientId, scope]);

  const save = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    try {
      await api.put(endpoint(clientId, 'ihce', scope), { ...form, ihceClientSecret: secrets.clientSecret, ihceApimSubscriptionKey: secrets.apimKey });
      toast.success(scope === 'client' ? 'Credenciales de IHCE actualizadas.' : 'Credenciales de IHCE de la sucursal guardadas.');
      load();
    } catch (err) {
      toast.error(getErrorMessage(err, 'Error al guardar la configuración de IHCE.'));
    } finally {
      setSaving(false);
    }
  };

  const remove = async () => {
    if (!window.confirm('¿Quitar las credenciales propias de esta sucursal? Volverá a usar las del cliente.')) return;
    try {
      await api.delete(endpoint(clientId, 'ihce', scope));
      toast.success('La sucursal vuelve a heredar las credenciales del cliente.');
      load();
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo quitar la configuración propia.'));
    }
  };

  return (
    <Card clientId={clientId} kind="ihce" icon={<ShieldCheck className="text-blue-600" size={20} />} title="IHCE (Historia Clínica Electrónica)"
      description="Llaves que la IPS o el profesional tramita en el Portal de Administración de Llaves de IHCE (Hércules) para interoperar su historia clínica."
      lock={<CredentialLockButton unlocked={unlocked} onToggle={toggle} />}
      header={<ScopeBar branches={branches} scope={scope} setScope={setScope} isOwn={meta.isBranchOwn} onRemove={remove} disabled={saving} />}>
      <form onSubmit={save} className="space-y-4" autoComplete="off">
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label className={labelClass}>Ambiente</label>
            <select disabled={!unlocked} value={form.ihceEnvironment} onChange={e => setForm({ ...form, ihceEnvironment: e.target.value })} className={lockedInputClass(unlocked, `${inputBase} bg-white`)}>
              <option value="Sandbox">Sandbox (Preproducción)</option>
              <option value="Production">Producción</option>
            </select>
          </div>
          <div>
            <label className={labelClass}>Endpoint</label>
            <input type="url" {...lockedInputProps(unlocked)} placeholder="https://..." value={form.ihceEndpoint} onChange={e => setForm({ ...form, ihceEndpoint: e.target.value })} className={lockedInputClass(unlocked, `${inputBase} font-mono text-sm`)} />
          </div>
          <div>
            <label className={labelClass}>Client ID</label>
            <input type="text" {...lockedInputProps(unlocked)} value={form.ihceClientId} onChange={e => setForm({ ...form, ihceClientId: e.target.value })} className={lockedInputClass(unlocked, `${inputBase} font-mono text-sm`)} />
          </div>
          <div>
            <label className={labelClass}>Tenant ID</label>
            <input type="text" {...lockedInputProps(unlocked)} value={form.ihceTenantId} onChange={e => setForm({ ...form, ihceTenantId: e.target.value })} className={lockedInputClass(unlocked, `${inputBase} font-mono text-sm`)} />
          </div>
          <div>
            <label className={labelClass}>Client Secret {meta.hasClientSecret && <span className="text-emerald-600 normal-case font-normal">(configurado)</span>}</label>
            <input type="password" {...lockedInputProps(unlocked)} placeholder={meta.hasClientSecret ? 'Dejar vacío para no cambiar' : ''} value={secrets.clientSecret}
              onChange={e => setSecrets({ ...secrets, clientSecret: e.target.value })} className={lockedInputClass(unlocked, `${inputBase} font-mono text-sm`)} />
          </div>
          <div>
            <label className={labelClass}>Llave de suscripción APIM {meta.hasApimSubscriptionKey && <span className="text-emerald-600 normal-case font-normal">(configurada)</span>}</label>
            <input type="password" {...lockedInputProps(unlocked)} placeholder={meta.hasApimSubscriptionKey ? 'Dejar vacío para no cambiar' : ''} value={secrets.apimKey}
              onChange={e => setSecrets({ ...secrets, apimKey: e.target.value })} className={lockedInputClass(unlocked, `${inputBase} font-mono text-sm`)} />
          </div>
        </div>
        {unlocked && (
          <div className="flex justify-end pt-2">
            <button type="submit" disabled={saving} className="px-5 py-2.5 bg-blue-600 text-white rounded-lg font-medium hover:bg-blue-500 transition-colors disabled:opacity-50">
              {saving ? 'Guardando...' : 'Guardar IHCE'}
            </button>
          </div>
        )}
      </form>
    </Card>
  );
}

// ---------------- Buzón de recepción ----------------

export function ReceptionMailboxCard({ clientId }: { clientId: string }) {
  const { branches, scope, setScope } = useScope(clientId);
  const { unlocked, lock, toggle } = useCredentialLock();
  // En el alcance del cliente el PUT también lleva los eventos automáticos (son del cliente): se conservan tal como vinieron.
  const [raw, setRaw] = useState<any>({});
  const [form, setForm] = useState({ receptionEmailEnabled: false, receptionEmailHost: '', receptionEmailPort: 993, receptionEmailUseSsl: true, receptionEmailUser: '' });
  const [password, setPassword] = useState('');
  const [meta, setMeta] = useState({ hasPassword: false, isBranchOwn: false });
  const [saving, setSaving] = useState(false);
  const [testing, setTesting] = useState(false);

  const load = () => {
    api.get(endpoint(clientId, 'reception', scope)).then(res => {
      const d = res.data;
      setRaw(d);
      setForm({ receptionEmailEnabled: !!d.receptionEmailEnabled, receptionEmailHost: d.receptionEmailHost || '', receptionEmailPort: d.receptionEmailPort || 993, receptionEmailUseSsl: d.receptionEmailUseSsl !== false, receptionEmailUser: d.receptionEmailUser || '' });
      setMeta({ hasPassword: !!d.hasPassword, isBranchOwn: !!d.isBranchOwn });
      setPassword('');
      lock();
    }).catch(err => toast.error(getErrorMessage(err, 'No se pudo cargar la configuración del correo.')));
  };
  useEffect(load, [clientId, scope]);

  const save = async () => {
    setSaving(true);
    try {
      const body = scope === 'client'
        ? { autoSendAcuseRecibo: raw.autoSendAcuseRecibo, autoSendReciboBien: raw.autoSendReciboBien, autoSendAceptacion: raw.autoSendAceptacion, autoSendReclamo: raw.autoSendReclamo, ...form, receptionEmailPassword: password }
        : { ...form, receptionEmailPassword: password };
      await api.put(endpoint(clientId, 'reception', scope), body);
      toast.success(scope === 'client' ? 'Configuración guardada.' : 'Buzón de la sucursal guardado.');
      load();
    } catch (err) {
      toast.error(getErrorMessage(err, 'Error al guardar la configuración.'));
    } finally {
      setSaving(false);
    }
  };

  const test = async () => {
    setTesting(true);
    try {
      const res = await api.post(`${endpoint(clientId, 'reception', scope)}/test-connection`);
      if (res.data.success) toast.success(res.data.message);
      else toast.error(res.data.message);
    } catch (err) {
      toast.error(getErrorMessage(err, 'Error al probar la conexión.'));
    } finally {
      setTesting(false);
    }
  };

  const remove = async () => {
    if (!window.confirm('¿Quitar el buzón propio de esta sucursal? Volverá a usar el del cliente.')) return;
    try {
      await api.delete(endpoint(clientId, 'reception', scope));
      toast.success('La sucursal vuelve a heredar el buzón del cliente.');
      load();
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo quitar el buzón propio.'));
    }
  };

  return (
    <Card clientId={clientId} kind="reception" icon={<Mail className="text-blue-600" size={20} />} title="Correo de Facturación Electrónica"
      description="El buzón donde le llegan las facturas de sus proveedores. Lo que llegue al buzón del cliente queda en la sucursal principal; lo que llegue al de una sucursal queda en ella. Para Gmail u Outlook usa una contraseña de aplicación."
      lock={<CredentialLockButton unlocked={unlocked} onToggle={toggle} />}
      header={<ScopeBar branches={branches} scope={scope} setScope={setScope} isOwn={meta.isBranchOwn} onRemove={remove} disabled={saving} />}>
      <div>
        <label className="flex items-center gap-3 mb-5 cursor-pointer">
          <input type="checkbox" disabled={!unlocked} checked={form.receptionEmailEnabled} onChange={e => setForm({ ...form, receptionEmailEnabled: e.target.checked })} className="w-5 h-5 rounded accent-blue-600" />
          <span className="font-semibold text-slate-700">Activar conexión de correo</span>
        </label>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
          <div>
            <label className="block text-sm font-bold text-slate-700 mb-1.5">Servidor IMAP</label>
            <input type="text" {...lockedInputProps(unlocked)} placeholder="imap.gmail.com" value={form.receptionEmailHost} onChange={e => setForm({ ...form, receptionEmailHost: e.target.value })} className={lockedInputClass(unlocked, inputBase)} />
          </div>
          <div>
            <label className="block text-sm font-bold text-slate-700 mb-1.5">Puerto</label>
            <input type="number" {...lockedInputProps(unlocked)} value={form.receptionEmailPort} onChange={e => setForm({ ...form, receptionEmailPort: parseInt(e.target.value) || 993 })} className={lockedInputClass(unlocked, inputBase)} />
          </div>
          <div>
            <label className="block text-sm font-bold text-slate-700 mb-1.5">Usuario / Correo</label>
            <input type="email" {...lockedInputProps(unlocked)} value={form.receptionEmailUser} onChange={e => setForm({ ...form, receptionEmailUser: e.target.value })} className={lockedInputClass(unlocked, inputBase)} />
          </div>
          <div>
            <label className="block text-sm font-bold text-slate-700 mb-1.5">Contraseña {meta.hasPassword && <span className="text-emerald-600 font-normal">(ya guardada)</span>}</label>
            <input type="password" {...lockedInputProps(unlocked)} placeholder={meta.hasPassword ? '••••••••' : ''} value={password} onChange={e => setPassword(e.target.value)} className={lockedInputClass(unlocked, inputBase)} />
          </div>
        </div>
        <label className="flex items-center gap-2 mt-4 cursor-pointer">
          <input type="checkbox" disabled={!unlocked} checked={form.receptionEmailUseSsl} onChange={e => setForm({ ...form, receptionEmailUseSsl: e.target.checked })} className="w-4 h-4 rounded accent-blue-600" />
          <span className="text-sm text-slate-600">Usar SSL/TLS (recomendado)</span>
        </label>
        <div className="flex gap-3 mt-6">
          {unlocked && (
            <button onClick={save} disabled={saving} className="bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white px-6 py-2.5 rounded-xl font-bold shadow-md transition-all">
              {saving ? 'Guardando...' : 'Guardar configuración'}
            </button>
          )}
          <button onClick={test} disabled={testing} className="bg-slate-100 hover:bg-slate-200 disabled:opacity-50 text-slate-700 px-6 py-2.5 rounded-xl font-bold transition-all">
            {testing ? 'Probando...' : 'Probar conexión'}
          </button>
        </div>
      </div>
    </Card>
  );
}
