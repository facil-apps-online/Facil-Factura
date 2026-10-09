import React, { useEffect, useState } from 'react';
import { KeyRound, Loader2, LogOut, Timer, UserRound } from 'lucide-react';
import { toast } from 'sonner';
import PasswordStrength, { evaluatePassword } from '../PasswordStrength';
import { ACCOUNT_CHANGED_EVENT, errorText, minutesLabel } from './sessionTypes';
import type { AccountMe, SessionHttp } from './sessionTypes';

// "Mi perfil": datos de la cuenta (el nombre para mostrar se edita; correo, rol y empresa solo se ven), cambio de contraseña y duración de
// la sesión por inactividad. Lo comparten los cuatro portales; cada uno solo indica su API, su clave de token y cómo cerrar sesión.
export interface ProfilePageProps {
  api: SessionHttp;
  // Prefijo de los endpoints de cuenta de este portal, por ejemplo "/tenant/auth".
  basePath: string;
  tokenKey: string;
  theme?: 'light' | 'dark';
}

export default function ProfilePage({ api, basePath, tokenKey, theme = 'light' }: ProfilePageProps) {
  const [me, setMe] = useState<AccountMe | null>(null);
  const [name, setName] = useState('');
  const [savingName, setSavingName] = useState(false);
  const [current, setCurrent] = useState('');
  const [next, setNext] = useState('');
  const [confirm, setConfirm] = useState('');
  const [savingPassword, setSavingPassword] = useState(false);
  const [minutes, setMinutes] = useState(30);
  const [savingSession, setSavingSession] = useState(false);
  const [signingOut, setSigningOut] = useState(false);
  const dark = theme === 'dark';

  const apply = (value: AccountMe) => { setMe(value); setName(value.name); setMinutes(value.sessionMinutes); window.dispatchEvent(new CustomEvent(ACCOUNT_CHANGED_EVENT, { detail: value })); };

  useEffect(() => {
    api.get(`${basePath}/me`).then(res => apply(res.data)).catch(err => toast.error(errorText(err, 'No se pudo cargar tu perfil.')));
  }, [api, basePath]);

  const storeToken = (token?: string | null) => { if (token) localStorage.setItem(tokenKey, token); };

  const saveName = async (e: React.FormEvent) => {
    e.preventDefault();
    setSavingName(true);
    try {
      const res = await api.put(`${basePath}/me`, { name });
      apply(res.data);
      toast.success('Perfil actualizado.');
    } catch (err) {
      toast.error(errorText(err, 'No se pudo guardar el nombre.'));
    } finally {
      setSavingName(false);
    }
  };

  const strength = evaluatePassword(next);
  const mismatch = confirm.length > 0 && confirm !== next;
  const canChange = !!current && strength.meetsPolicy && next === confirm && !savingPassword;

  const changePassword = async (e: React.FormEvent) => {
    e.preventDefault();
    setSavingPassword(true);
    try {
      const res = await api.post(`${basePath}/change-password`, { currentPassword: current, newPassword: next });
      storeToken(res.data.token);
      setCurrent(''); setNext(''); setConfirm('');
      toast.success('Contraseña actualizada. Cerramos tus otras sesiones abiertas.');
    } catch (err) {
      toast.error(errorText(err, 'No se pudo cambiar la contraseña.'));
    } finally {
      setSavingPassword(false);
    }
  };

  const saveSession = async (e: React.FormEvent) => {
    e.preventDefault();
    setSavingSession(true);
    try {
      const res = await api.put(`${basePath}/session-preference`, { minutes });
      storeToken(res.data.token);
      apply(res.data.me);
      toast.success(`Tu sesión se cerrará tras ${minutesLabel(minutes)} de inactividad.`);
    } catch (err) {
      toast.error(errorText(err, 'No se pudo guardar la duración.'));
    } finally {
      setSavingSession(false);
    }
  };

  const signOutEverywhere = async () => {
    if (!window.confirm('¿Cerrar tu sesión en todos los demás dispositivos y navegadores? Esta sesión sigue abierta.')) return;
    setSigningOut(true);
    try {
      const res = await api.post(`${basePath}/sign-out-everywhere`);
      storeToken(res.data.token);
      toast.success('Cerramos tus otras sesiones.');
    } catch (err) {
      toast.error(errorText(err, 'No se pudieron cerrar las otras sesiones.'));
    } finally {
      setSigningOut(false);
    }
  };

  const card = dark ? 'bg-slate-900 border-slate-800 text-white' : 'bg-white border-slate-100 text-slate-800';
  const heading = dark ? 'text-white' : 'text-slate-800';
  const muted = dark ? 'text-slate-400' : 'text-slate-500';
  const label = `mb-1.5 block text-xs font-bold uppercase ${muted}`;
  const input = `w-full rounded-xl border px-4 py-2.5 outline-none focus:ring-2 focus:ring-blue-500 ${dark ? 'border-slate-700 bg-slate-800 text-white' : 'border-slate-200 bg-slate-50 text-slate-800'}`;
  const readonly = `w-full rounded-xl border px-4 py-2.5 text-sm ${dark ? 'border-slate-800 bg-slate-950 text-slate-300' : 'border-slate-100 bg-slate-100 text-slate-600'}`;
  const primary = 'rounded-xl bg-blue-600 px-6 py-2.5 text-sm font-bold text-white shadow-md transition-colors hover:bg-blue-500 disabled:opacity-50';

  if (!me) {
    return <div className="flex justify-center py-24"><Loader2 className="animate-spin text-blue-500" /></div>;
  }

  return (
    <div className="mx-auto max-w-3xl space-y-6 p-4 sm:p-6 lg:p-8" data-testid="profile-page">
      <div>
        <h1 className={`text-2xl font-extrabold tracking-tight sm:text-3xl ${heading}`}>Mi perfil</h1>
        <p className={`mt-1 text-sm ${muted}`}>Tu cuenta, tu contraseña y la seguridad de tu sesión.</p>
      </div>

      {/* Mi cuenta */}
      <form onSubmit={saveName} className={`space-y-5 rounded-3xl border p-6 shadow-sm sm:p-8 ${card}`}>
        <div className="flex items-center gap-2"><UserRound size={20} className="text-blue-500" /><h2 className={`text-lg font-bold ${heading}`}>Mi cuenta</h2></div>
        <div>
          <label className={label} htmlFor="profile-name">Nombre para mostrar</label>
          <input id="profile-name" type="text" value={name} maxLength={150} onChange={e => setName(e.target.value)} placeholder={me.email} className={input} />
          <p className={`mt-1 text-xs ${muted}`}>Si lo dejas vacío, se muestra tu correo.</p>
        </div>
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
          <div><span className={label}>Correo</span><div className={`${readonly} truncate`}>{me.email}</div></div>
          <div><span className={label}>Rol</span><div className={readonly}>{me.role}</div></div>
          <div><span className={label}>Empresa</span><div className={`${readonly} truncate`}>{me.organization || '—'}</div></div>
        </div>
        <div className="flex justify-end">
          <button type="submit" disabled={savingName || name === me.name} className={primary}>{savingName ? 'Guardando...' : 'Guardar nombre'}</button>
        </div>
      </form>

      {/* Contraseña */}
      <form onSubmit={changePassword} className={`space-y-5 rounded-3xl border p-6 shadow-sm sm:p-8 ${card}`} autoComplete="off">
        <div className="flex items-center gap-2"><KeyRound size={20} className="text-blue-500" /><h2 className={`text-lg font-bold ${heading}`}>Contraseña</h2></div>
        <div>
          <label className={label} htmlFor="profile-current">Contraseña actual</label>
          <input id="profile-current" type="password" autoComplete="current-password" value={current} onChange={e => setCurrent(e.target.value)} className={input} />
        </div>
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <div>
            <label className={label} htmlFor="profile-new">Nueva contraseña</label>
            <input id="profile-new" type="password" autoComplete="new-password" value={next} onChange={e => setNext(e.target.value)} className={input} />
            <PasswordStrength password={next} />
          </div>
          <div>
            <label className={label} htmlFor="profile-confirm">Confirmar nueva contraseña</label>
            <input id="profile-confirm" type="password" autoComplete="new-password" value={confirm} onChange={e => setConfirm(e.target.value)} className={input} />
            {mismatch && <p className="mt-1 text-xs font-medium text-rose-500">Las contraseñas no coinciden.</p>}
          </div>
        </div>
        <p className={`text-xs ${muted}`}>Al cambiarla cerramos tus sesiones en los demás dispositivos; esta sigue abierta.</p>
        <div className="flex justify-end">
          <button type="submit" disabled={!canChange} className={primary}>{savingPassword ? 'Guardando...' : 'Cambiar contraseña'}</button>
        </div>
      </form>

      {/* Mi sesión */}
      <form onSubmit={saveSession} className={`space-y-5 rounded-3xl border p-6 shadow-sm sm:p-8 ${card}`}>
        <div className="flex items-center gap-2"><Timer size={20} className="text-blue-500" /><h2 className={`text-lg font-bold ${heading}`}>Mi sesión</h2></div>
        <div>
          <label className={label} htmlFor="profile-minutes">Cerrar mi sesión tras</label>
          <select id="profile-minutes" value={minutes} onChange={e => setMinutes(Number(e.target.value))} className={input}>
            {me.allowedSessionMinutes.map(m => <option key={m} value={m}>{minutesLabel(m)} de inactividad{m === 30 ? ' (recomendado)' : ''}</option>)}
          </select>
          <p className={`mt-1 text-xs ${muted}`}>
            Un minuto antes te avisamos para que puedas seguir conectado. Por seguridad, cada {me.absoluteCapHours} horas te pediremos confirmar tu contraseña, sin cerrar lo que estás haciendo.
          </p>
        </div>
        <div className="flex flex-wrap items-center justify-between gap-3">
          <button type="button" onClick={signOutEverywhere} disabled={signingOut} className="flex items-center gap-2 text-sm font-semibold text-rose-500 hover:text-rose-400 disabled:opacity-50">
            <LogOut size={16} /> Cerrar sesión en todos los dispositivos
          </button>
          <button type="submit" disabled={savingSession || minutes === me.sessionMinutes} className={primary}>{savingSession ? 'Guardando...' : 'Guardar duración'}</button>
        </div>
      </form>
    </div>
  );
}
