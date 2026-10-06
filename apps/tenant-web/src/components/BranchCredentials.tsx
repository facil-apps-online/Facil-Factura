import React, { useEffect, useState } from 'react';
import { BellRing, Mail, ShieldCheck, Stethoscope, Undo2 } from 'lucide-react';
import { toast } from 'sonner';
import SearchableSelect from '@shared/components/SearchableSelect';
import { CredentialLockButton, lockedInputClass, lockedInputProps, useCredentialLock } from '@shared/components/CredentialLock';
import { api, getErrorMessage } from '../lib/api';

// Credenciales que cada sucursal puede tener propias (MinSalud, IHCE, buzón de recepción y eventos automáticos). La sucursal principal es el
// valor por defecto: una sucursal sin credenciales propias hereda las de la principal; al guardar las suyas éstas las reemplazan por
// completo, y "Volver a heredar" las quita. Los formularios arrancan bloqueados (lapicito) para que el navegador no los autocomplete ni se
// cambien por accidente.

type Kind = 'minsalud' | 'ihce' | 'reception' | 'reception-events';

interface BranchOption { id: string; name: string; isMain: boolean; isActive: boolean }

const inputBase = 'w-full px-4 py-2.5 border border-slate-200 rounded-xl outline-none focus:ring-2 focus:ring-blue-500';
const labelClass = 'block text-xs font-bold text-slate-500 uppercase mb-1';
const selectInputClass = (unlocked: boolean) => lockedInputClass(unlocked, 'w-full px-4 py-2.5 pr-8 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none bg-white');

const endpoint = (clientId: string, kind: Kind, branchId: string) => `/tenant/clients/${clientId}/branches/${branchId}/credentials/${kind}`;

// Sucursales del cliente y la elegida (arranca en la principal).
function useScope(clientId: string) {
  const [branches, setBranches] = useState<BranchOption[]>([]);
  const [scope, setScope] = useState<string>('');
  useEffect(() => {
    api.get(`/tenant/clients/${clientId}/branches`).then(res => {
      setBranches(res.data);
      setScope(prev => prev || res.data.find((b: BranchOption) => b.isMain)?.id || res.data[0]?.id || '');
    }).catch(() => {});
  }, [clientId]);
  const isMain = branches.find(b => b.id === scope)?.isMain ?? true;
  return { branches, scope, setScope, isMain };
}

function Card({ icon, title, description, kind, children, header, lock }: {
  icon: React.ReactNode; title: string; description: string; kind: Kind;
  header: React.ReactNode; children: React.ReactNode; lock: React.ReactNode;
}) {
  return (
    <div className="bg-white rounded-3xl p-8 shadow-sm border border-slate-100" data-credential-card={kind}>
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

function ScopeBar({ branches, scope, setScope, isMain, isOwn, onRemove, disabled, noun }: {
  branches: BranchOption[]; scope: string; setScope: (s: string) => void; isMain: boolean; isOwn: boolean; onRemove: () => void; disabled: boolean; noun: string;
}) {
  if (branches.length <= 1) return null; // con una sola sucursal no hay a qué aplicarlo por separado
  return (
    <div className="mb-5 rounded-xl border border-slate-200 bg-slate-50/70 p-4 space-y-3">
      <div className="flex flex-wrap items-center gap-3">
        <label className="text-sm font-semibold text-slate-700">Aplica a</label>
        <select value={scope} onChange={e => setScope(e.target.value)} disabled={disabled}
          className="px-3 py-2 bg-white border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-blue-500 outline-none">
          {branches.map(b => <option key={b.id} value={b.id}>{b.name}{b.isMain ? ' (principal)' : ''}{b.isActive ? '' : ' — inactiva'}</option>)}
        </select>
        {isMain ? (
          <span className="text-xs font-bold px-2 py-0.5 rounded-full bg-blue-100 text-blue-700">Valor por defecto de las demás sucursales</span>
        ) : (
          <span className={`text-xs font-bold px-2 py-0.5 rounded-full ${isOwn ? 'bg-blue-100 text-blue-700' : 'bg-slate-200 text-slate-600'}`}>
            {isOwn ? `${noun} propios de la sucursal` : 'Hereda los de la principal'}
          </span>
        )}
        {!isMain && isOwn && (
          <button type="button" onClick={onRemove} className="flex items-center gap-1 text-xs font-semibold text-slate-500 hover:text-rose-600 transition-colors">
            <Undo2 size={14} /> Volver a heredar
          </button>
        )}
      </div>
      {!isMain && !isOwn && (
        <p className="text-xs text-slate-500">Estás viendo lo de la principal. Si guardas aquí, la sucursal tendrá lo suyo y deberás escribir también las claves: no se copian las de la principal.</p>
      )}
    </div>
  );
}

// ---------------- MinSalud ----------------

export function MinSaludCard({ clientId }: { clientId: string }) {
  const { branches, scope, setScope, isMain } = useScope(clientId);
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
    if (!scope) return;
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
      toast.success(isMain ? 'Configuración de MinSalud (RIPS) actualizada.' : 'Credenciales de MinSalud de la sucursal guardadas.');
      load();
    } catch (err) {
      toast.error(getErrorMessage(err, 'Error al guardar la configuración.'));
    } finally {
      setSaving(false);
    }
  };

  const remove = async () => {
    if (!window.confirm('¿Quitar las credenciales propias de esta sucursal? Volverá a usar las de la principal.')) return;
    try {
      await api.delete(endpoint(clientId, 'minsalud', scope));
      toast.success('La sucursal vuelve a heredar las credenciales de la principal.');
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
    <Card kind="minsalud" icon={<Stethoscope className="text-blue-600" size={20} />} title="MinSalud / RIPS (MUV-FEV-RIPS)"
      description="Credenciales del prestador ante SISPRO para el envío de RIPS al Ministerio de Salud. Solo aplica a emisores del sector salud."
      lock={<CredentialLockButton unlocked={unlocked} onToggle={toggle} />}
      header={<ScopeBar branches={branches} scope={scope} setScope={setScope} isMain={isMain} isOwn={meta.isBranchOwn} onRemove={remove} disabled={saving} noun="Credenciales" />}>
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
  const { branches, scope, setScope, isMain } = useScope(clientId);
  const { unlocked, lock, toggle } = useCredentialLock();
  const [form, setForm] = useState({ ihceClientId: '', ihceTenantId: '', ihceEndpoint: '', ihceEnvironment: 'Sandbox' });
  const [secrets, setSecrets] = useState({ clientSecret: '', apimKey: '' });
  const [meta, setMeta] = useState({ hasClientSecret: false, hasApimSubscriptionKey: false, isBranchOwn: false });
  const [saving, setSaving] = useState(false);

  const load = () => {
    if (!scope) return;
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
      toast.success(isMain ? 'Credenciales de IHCE actualizadas.' : 'Credenciales de IHCE de la sucursal guardadas.');
      load();
    } catch (err) {
      toast.error(getErrorMessage(err, 'Error al guardar la configuración de IHCE.'));
    } finally {
      setSaving(false);
    }
  };

  const remove = async () => {
    if (!window.confirm('¿Quitar las credenciales propias de esta sucursal? Volverá a usar las de la principal.')) return;
    try {
      await api.delete(endpoint(clientId, 'ihce', scope));
      toast.success('La sucursal vuelve a heredar las credenciales de la principal.');
      load();
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo quitar la configuración propia.'));
    }
  };

  return (
    <Card kind="ihce" icon={<ShieldCheck className="text-blue-600" size={20} />} title="IHCE (Historia Clínica Electrónica)"
      description="Llaves que la IPS o el profesional tramita en el Portal de Administración de Llaves de IHCE (Hércules) para interoperar su historia clínica."
      lock={<CredentialLockButton unlocked={unlocked} onToggle={toggle} />}
      header={<ScopeBar branches={branches} scope={scope} setScope={setScope} isMain={isMain} isOwn={meta.isBranchOwn} onRemove={remove} disabled={saving} noun="Credenciales" />}>
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
  const { branches, scope, setScope, isMain } = useScope(clientId);
  const { unlocked, lock, toggle } = useCredentialLock();
  const [form, setForm] = useState({ receptionEmailEnabled: false, receptionEmailHost: '', receptionEmailPort: 993, receptionEmailUseSsl: true, receptionEmailUser: '' });
  const [password, setPassword] = useState('');
  const [meta, setMeta] = useState({ hasPassword: false, isBranchOwn: false });
  const [saving, setSaving] = useState(false);
  const [testing, setTesting] = useState(false);

  const load = () => {
    if (!scope) return;
    api.get(endpoint(clientId, 'reception', scope)).then(res => {
      const d = res.data;
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
      await api.put(endpoint(clientId, 'reception', scope), { ...form, receptionEmailPassword: password });
      toast.success(isMain ? 'Configuración guardada.' : 'Buzón de la sucursal guardado.');
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
    if (!window.confirm('¿Quitar el buzón propio de esta sucursal? Volverá a usar el de la principal.')) return;
    try {
      await api.delete(endpoint(clientId, 'reception', scope));
      toast.success('La sucursal vuelve a heredar el buzón de la principal.');
      load();
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo quitar el buzón propio.'));
    }
  };

  return (
    <Card kind="reception" icon={<Mail className="text-blue-600" size={20} />} title="Correo de Facturación Electrónica"
      description="El buzón donde le llegan las facturas de sus proveedores; lo que llegue a él queda en la sucursal elegida. Para Gmail u Outlook usa una contraseña de aplicación."
      lock={<CredentialLockButton unlocked={unlocked} onToggle={toggle} />}
      header={<ScopeBar branches={branches} scope={scope} setScope={setScope} isMain={isMain} isOwn={meta.isBranchOwn} onRemove={remove} disabled={saving} noun="Buzón" />}>
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

// ---------------- Eventos automáticos de recepción ----------------

const EVENTS = [
  ['autoSendAcuseRecibo', 'Acuse de Recibo'],
  ['autoSendReciboBien', 'Recibo del Bien o Servicio'],
  ['autoSendAceptacion', 'Aceptación Expresa'],
  ['autoSendReclamo', 'Reclamo']
] as const;

export function ReceptionEventsCard({ clientId }: { clientId: string }) {
  const { branches, scope, setScope, isMain } = useScope(clientId);
  const [form, setForm] = useState<Record<string, boolean>>({ autoSendAcuseRecibo: false, autoSendReciboBien: false, autoSendAceptacion: false, autoSendReclamo: false });
  const [isOwn, setIsOwn] = useState(false);
  const [saving, setSaving] = useState(false);

  const load = () => {
    if (!scope) return;
    api.get(endpoint(clientId, 'reception-events', scope)).then(res => {
      setForm({ autoSendAcuseRecibo: !!res.data.autoSendAcuseRecibo, autoSendReciboBien: !!res.data.autoSendReciboBien, autoSendAceptacion: !!res.data.autoSendAceptacion, autoSendReclamo: !!res.data.autoSendReclamo });
      setIsOwn(!!res.data.isBranchOwn);
    }).catch(err => toast.error(getErrorMessage(err, 'No se pudo cargar la configuración de eventos.')));
  };
  useEffect(load, [clientId, scope]);

  const save = async () => {
    setSaving(true);
    try {
      await api.put(endpoint(clientId, 'reception-events', scope), form);
      toast.success(isMain ? 'Eventos automáticos guardados.' : 'Eventos automáticos de la sucursal guardados.');
      load();
    } catch (err) {
      toast.error(getErrorMessage(err, 'Error al guardar los eventos.'));
    } finally {
      setSaving(false);
    }
  };

  const remove = async () => {
    if (!window.confirm('¿Quitar los eventos propios de esta sucursal? Volverá a usar los de la principal.')) return;
    try {
      await api.delete(endpoint(clientId, 'reception-events', scope));
      toast.success('La sucursal vuelve a heredar los eventos de la principal.');
      load();
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo quitar la configuración propia.'));
    }
  };

  return (
    <Card kind="reception-events" icon={<BellRing className="text-blue-600" size={20} />} title="Eventos automáticos"
      description="Selecciona los eventos que se crearán automáticamente al recibir un documento en la sucursal."
      lock={null}
      header={<ScopeBar branches={branches} scope={scope} setScope={setScope} isMain={isMain} isOwn={isOwn} onRemove={remove} disabled={saving} noun="Eventos" />}>
      <div className="space-y-3">
        {EVENTS.map(([field, label]) => (
          <label key={field} className="flex items-center gap-3 p-3 bg-slate-50 rounded-xl border border-slate-100 cursor-pointer">
            <input type="checkbox" checked={form[field]} onChange={e => setForm({ ...form, [field]: e.target.checked })} className="w-5 h-5 rounded accent-blue-600" />
            <span className="font-semibold text-slate-700 text-sm">{label}</span>
          </label>
        ))}
      </div>
      <button onClick={save} disabled={saving} className="mt-6 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white px-6 py-2.5 rounded-xl font-bold shadow-md transition-all">
        {saving ? 'Guardando...' : 'Guardar eventos automáticos'}
      </button>
    </Card>
  );
}
