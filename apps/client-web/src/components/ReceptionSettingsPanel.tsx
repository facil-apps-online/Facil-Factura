import React, { useEffect, useState } from 'react';
import { Mail, Undo2 } from 'lucide-react';
import { toast } from 'sonner';
import { api, getErrorMessage } from '../lib/api';
import { useConfirm } from '@/components/ConfirmDialog';
import { ALL_BRANCHES, useSession } from '../context/SessionContext';
import { CredentialLockButton, lockedInputClass, lockedInputProps, useCredentialLock } from '@shared/components/CredentialLock';

const EVENT_INFO = [
  ['autoSendAcuseRecibo', 'Acuse de recibo', 'Confirma que la factura fue recibida.'],
  ['autoSendReciboBien', 'Recibo del bien o servicio', 'Confirma que el bien o servicio facturado fue recibido.'],
  ['autoSendAceptacion', 'Aceptación expresa', 'Declara que aceptas la factura como está.'],
  ['autoSendReclamo', 'Reclamo', 'Declara que existe un problema con la factura.'],
] as const;

export default function ReceptionSettingsPanel() {
  const confirm = useConfirm();
  const { selectedBranchId, hasMultipleBranches } = useSession();
  const { unlocked, lock, toggle } = useCredentialLock();
  const editingMainByAll = selectedBranchId === ALL_BRANCHES;
  const [settings, setSettings] = useState<any>({
    receptionEmailEnabled: false, receptionEmailHost: '', receptionEmailPort: 993, receptionEmailUseSsl: true,
    receptionEmailUser: '', hasPassword: false, autoSendAcuseRecibo: false, autoSendReciboBien: false,
    autoSendAceptacion: false, autoSendReclamo: false,
  });
  const [emailPasswordDraft, setEmailPasswordDraft] = useState('');
  const [saving, setSaving] = useState(false);
  const [testing, setTesting] = useState(false);

  const loadSettings = () => {
    api.get('/client/reception-settings')
      .then(res => { setSettings(res.data); lock(); })
      .catch(() => toast.error('Error al cargar la configuración'));
  };

  useEffect(() => { loadSettings(); }, []);

  const save = async () => {
    setSaving(true);
    try {
      await api.put('/client/reception-settings', { ...settings, onlyAutoSend: true });
      toast.success('Eventos automáticos guardados');
      loadSettings();
    } catch (err: any) {
      toast.error(getErrorMessage(err, 'Error al guardar los eventos'));
    } finally { setSaving(false); }
  };

  const saveMailbox = async () => {
    setSaving(true);
    try {
      await api.put('/client/reception-settings', { ...settings, onlyAutoSend: false, receptionEmailPassword: emailPasswordDraft || undefined });
      toast.success('Buzón guardado');
      setEmailPasswordDraft('');
      loadSettings();
    } catch (err: any) { toast.error(getErrorMessage(err, 'Error al guardar el buzón')); }
    finally { setSaving(false); }
  };

  const removeMailbox = async () => {
    if (!(await confirm('¿Quitar el buzón propio de esta sucursal? Volverá a usar el de la principal.', { title: 'Quitar buzón propio', confirmText: 'Quitar' }))) return;
    try { await api.delete('/client/reception-settings'); toast.success('La sucursal vuelve a usar el buzón principal'); loadSettings(); }
    catch (err: any) { toast.error(getErrorMessage(err, 'No se pudo quitar el buzón propio')); }
  };

  const removeEvents = async () => {
    if (!(await confirm('¿Quitar los eventos propios de esta sucursal? Volverá a usar los de la principal.', { title: 'Quitar eventos propios', confirmText: 'Quitar' }))) return;
    try { await api.delete('/client/reception-settings/events'); toast.success('La sucursal vuelve a usar los eventos principales'); loadSettings(); }
    catch (err: any) { toast.error(getErrorMessage(err, 'No se pudo quitar la configuración propia')); }
  };

  const testConnection = async () => {
    setTesting(true);
    try { const res = await api.post('/client/reception-settings/test-connection'); res.data.success ? toast.success(res.data.message) : toast.error(res.data.message); }
    catch (err: any) { toast.error(getErrorMessage(err, 'Error al probar la conexión')); }
    finally { setTesting(false); }
  };

  return <div className="space-y-6 lg:space-y-8">
    <div className="bg-white rounded-3xl p-4 sm:p-6 lg:p-8 shadow-sm border border-slate-100">
      <div className="flex items-center gap-2 mb-2"><Mail className="text-primary" size={20} /><h2 className="text-xl font-bold text-slate-800">Correo de facturación electrónica</h2><div className="ml-auto"><CredentialLockButton unlocked={unlocked} onToggle={toggle} /></div></div>
      {hasMultipleBranches && <div className="mb-5 rounded-xl border border-slate-200 bg-slate-50/70 p-4 text-sm text-slate-600">{settings.isMain ? <p>Estás editando el buzón de la <strong>sucursal principal</strong>, que es el valor por defecto de las demás{editingMainByAll ? ' (con “Todas” se trabaja sobre la principal)' : ''}.</p> : settings.isBranchOwn ? <div className="flex flex-wrap items-center gap-3"><span>Esta sucursal tiene un <strong>buzón propio</strong>.</span><button type="button" onClick={removeMailbox} className="flex items-center gap-1 text-xs font-semibold text-slate-500 hover:text-rose-600"><Undo2 size={14} /> Volver al buzón principal</button></div> : <p>Esta sucursal usa el buzón de la principal. Si guardas aquí, tendrá el suyo propio.</p>}</div>}
      <p className="text-slate-500 mb-6 text-sm">Conecta el buzón donde llegan las facturas de tus proveedores para procesarlas automáticamente.</p>
      <label className="flex items-center gap-3 mb-5 cursor-pointer"><input type="checkbox" checked={settings.receptionEmailEnabled} disabled={!unlocked} onChange={e => setSettings({ ...settings, receptionEmailEnabled: e.target.checked })} className="w-5 h-5 rounded accent-primary" /><span className="font-semibold text-slate-700">Activar conexión de correo</span></label>
      <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
        <div><label className="block text-sm font-bold text-slate-700 mb-1.5">Servidor IMAP</label><input type="text" placeholder="imap.gmail.com" {...lockedInputProps(unlocked)} className={lockedInputClass(unlocked, 'w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl outline-none focus:ring-2 focus:ring-primary')} value={settings.receptionEmailHost} onChange={e => setSettings({ ...settings, receptionEmailHost: e.target.value })} /></div>
        <div><label className="block text-sm font-bold text-slate-700 mb-1.5">Puerto</label><input type="number" {...lockedInputProps(unlocked)} className={lockedInputClass(unlocked, 'w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl outline-none focus:ring-2 focus:ring-primary')} value={settings.receptionEmailPort} onChange={e => setSettings({ ...settings, receptionEmailPort: Number(e.target.value) || 993 })} /></div>
        <div><label className="block text-sm font-bold text-slate-700 mb-1.5">Usuario / correo</label><input type="email" {...lockedInputProps(unlocked)} className={lockedInputClass(unlocked, 'w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl outline-none focus:ring-2 focus:ring-primary')} value={settings.receptionEmailUser} onChange={e => setSettings({ ...settings, receptionEmailUser: e.target.value })} /></div>
        <div><label className="block text-sm font-bold text-slate-700 mb-1.5">Contraseña {settings.hasPassword && <span className="text-emerald-600 font-normal">(ya guardada)</span>}</label><input type="password" {...lockedInputProps(unlocked)} className={lockedInputClass(unlocked, 'w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl outline-none focus:ring-2 focus:ring-primary')} value={emailPasswordDraft} onChange={e => setEmailPasswordDraft(e.target.value)} /></div>
      </div>
      <label className="flex items-center gap-2 mt-4 cursor-pointer"><input type="checkbox" disabled={!unlocked} checked={settings.receptionEmailUseSsl} onChange={e => setSettings({ ...settings, receptionEmailUseSsl: e.target.checked })} className="w-4 h-4 rounded accent-primary" /><span className="text-sm text-slate-600">Usar SSL/TLS (recomendado)</span></label>
      <div className="mt-6 flex flex-wrap gap-3"><button onClick={saveMailbox} disabled={!unlocked || saving} className="bg-primary hover:bg-primary-hover disabled:opacity-50 text-white px-6 py-2.5 rounded-xl font-bold">{saving ? 'Guardando...' : 'Guardar buzón'}</button><button onClick={testConnection} disabled={testing} className="bg-slate-100 hover:bg-slate-200 disabled:opacity-50 text-slate-700 px-6 py-2.5 rounded-xl font-bold">{testing ? 'Probando...' : 'Probar conexión'}</button></div>
    </div>

    <div className="bg-white rounded-3xl p-4 sm:p-6 lg:p-8 shadow-sm border border-slate-100">
      <h2 className="text-xl font-bold text-slate-800 mb-2">Eventos automáticos</h2><p className="text-slate-500 mb-6 text-sm">Selecciona qué eventos se enviarán automáticamente al recibir un documento.</p>
      {hasMultipleBranches && !settings.isMain && <div className="mb-5 rounded-xl border border-slate-200 bg-slate-50/70 p-4 text-sm text-slate-600">{settings.isEventsOwn ? <div className="flex flex-wrap items-center gap-3"><span>Esta sucursal tiene <strong>eventos propios</strong>.</span><button type="button" onClick={removeEvents} className="flex items-center gap-1 text-xs font-semibold text-slate-500 hover:text-rose-600"><Undo2 size={14} /> Volver a los eventos principales</button></div> : <p>Esta sucursal usa los eventos de la principal.</p>}</div>}
      <div className="space-y-4">{EVENT_INFO.map(([field, label, description]) => <label key={field} className="flex items-start gap-3 p-4 bg-slate-50 rounded-2xl border border-slate-100 cursor-pointer"><input type="checkbox" checked={settings[field]} onChange={e => setSettings({ ...settings, [field]: e.target.checked })} className="w-5 h-5 mt-0.5 rounded accent-primary" /><div><span className="font-bold text-slate-800">{label}</span><p className="text-xs text-slate-500 mt-0.5">{description}</p></div></label>)}</div>
      <p className="text-xs text-slate-500 mt-5">El prefijo y el consecutivo de los eventos se administran en <strong>Resoluciones y consecutivos</strong>.</p>
      <button onClick={save} disabled={saving} className="mt-6 bg-primary hover:bg-primary-hover disabled:opacity-50 text-white px-6 py-2.5 rounded-xl font-bold">{saving ? 'Guardando...' : 'Guardar eventos automáticos'}</button>
    </div>
  </div>;
}
