import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Clock, Loader2, ShieldAlert } from 'lucide-react';
import { errorText, formatCountdown, readSessionClaims } from './sessionTypes';
import type { SessionHttp } from './sessionTypes';

// Vigila la sesión del portal. El token dura lo que el usuario eligió de inactividad (30 minutos por defecto) y se renueva solo mientras
// hay actividad real (mouse, teclado, scroll o toque; las peticiones automáticas no cuentan). Un minuto antes de que venza sin actividad
// avisa con una cuenta regresiva y deja seguir conectado; al llegar a cero cierra la sesión y manda al login (onExpired).
//
// Además cada sesión tiene un tope absoluto de 12 horas. Cuando el token llega a ese tope no se corta de golpe: el aviso pide escribir la
// contraseña de nuevo, ahí mismo, y la sesión sigue con el trabajo intacto.
//
// Si el token cambia en otra pestaña (se renovó o se cerró la sesión), esta se entera por el evento "storage" y se sincroniza.

const WARNING_MS = 60_000;
const ACTIVITY_EVENTS = ['mousemove', 'mousedown', 'keydown', 'scroll', 'touchstart', 'wheel'] as const;

export interface SessionGuardProps {
  api: SessionHttp;
  // Clave de localStorage donde el portal guarda su token.
  tokenKey: string;
  // Prefijo de los endpoints de cuenta de este portal, por ejemplo "/tenant/auth".
  basePath: string;
  // Limpia la sesión del portal y manda a su login (el del cliente, con el slug del tenant).
  onExpired: () => void;
  theme?: 'light' | 'dark';
}

export default function SessionGuard({ api, tokenKey, basePath, onExpired, theme = 'light' }: SessionGuardProps) {
  const [token, setToken] = useState<string | null>(() => localStorage.getItem(tokenKey));
  const [now, setNow] = useState(() => Date.now());
  const [needsPassword, setNeedsPassword] = useState(false);
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const lastActivity = useRef(Date.now());
  const renewing = useRef(false);
  const expiredFired = useRef(false);

  // Actividad real del usuario.
  useEffect(() => {
    const mark = () => { lastActivity.current = Date.now(); };
    ACTIVITY_EVENTS.forEach(e => window.addEventListener(e, mark, { passive: true }));
    return () => ACTIVITY_EVENTS.forEach(e => window.removeEventListener(e, mark));
  }, []);

  // Reloj: cada segundo, y de inmediato al volver a la pestaña (los navegadores frenan los temporizadores de las pestañas ocultas).
  useEffect(() => {
    const tick = () => setNow(Date.now());
    const id = window.setInterval(tick, 1000);
    document.addEventListener('visibilitychange', tick);
    return () => { window.clearInterval(id); document.removeEventListener('visibilitychange', tick); };
  }, []);

  // Sincronización entre pestañas.
  useEffect(() => {
    const onStorage = (e: StorageEvent) => {
      if (e.key === tokenKey || e.key === null) {
        setToken(localStorage.getItem(tokenKey));
        setNeedsPassword(false);
      }
    };
    window.addEventListener('storage', onStorage);
    return () => window.removeEventListener('storage', onStorage);
  }, [tokenKey]);

  const claims = useMemo(() => readSessionClaims(token), [token]);
  const remainingMs = claims ? claims.exp * 1000 - now : -1;
  const lifetimeMs = claims ? (claims.exp - claims.iat) * 1000 : 0;
  // El token ya no puede extenderse más: vence justo en el tope absoluto de la sesión.
  const capBound = !!claims && claims.exp >= claims.cap;

  const store = useCallback((newToken: string) => {
    localStorage.setItem(tokenKey, newToken);
    setToken(newToken);
    setNeedsPassword(false);
    setPassword('');
    setError('');
  }, [tokenKey]);

  const renew = useCallback(async () => {
    if (renewing.current) return;
    renewing.current = true;
    try {
      const res = await api.post(`${basePath}/renew`);
      if (res.data?.token) store(res.data.token);
    } catch (err: any) {
      // 409: llegó al tope de 12 horas y hay que confirmar la contraseña. Un 401 lo atiende el interceptor del portal (cierra la sesión).
      if (err?.response?.status === 409) setNeedsPassword(true);
    } finally {
      renewing.current = false;
    }
  }, [api, basePath, store]);

  // Sin token, o vencido: se cierra la sesión (una sola vez).
  useEffect(() => {
    if ((!token || remainingMs <= 0) && !expiredFired.current) {
      expiredFired.current = true;
      onExpired();
    }
  }, [token, remainingMs, onExpired]);

  // Renovación silenciosa: a mitad de la vida del token, si hubo actividad desde que se emitió. Nunca durante el último minuto (ahí manda el aviso).
  useEffect(() => {
    if (!claims || capBound || renewing.current) return;
    if (remainingMs > WARNING_MS && remainingMs <= lifetimeMs / 2 && lastActivity.current > claims.iat * 1000) void renew();
  }, [now, claims, capBound, remainingMs, lifetimeMs, renew]);

  const showWarning = !!claims && remainingMs > 0 && remainingMs <= WARNING_MS;
  if (!showWarning) return null;
  const reauth = capBound || needsPassword;

  const submitPassword = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError('');
    try {
      const res = await api.post(`${basePath}/reauthenticate`, { password });
      store(res.data.token);
    } catch (err) {
      setError(errorText(err, 'No se pudo confirmar la contraseña.'));
    } finally {
      setBusy(false);
    }
  };

  const dark = theme === 'dark';
  const card = dark ? 'bg-slate-900 border-slate-700 text-white' : 'bg-white border-slate-200 text-slate-800';
  const muted = dark ? 'text-slate-400' : 'text-slate-500';
  const input = dark ? 'bg-slate-800 border-slate-600 text-white' : 'bg-slate-50 border-slate-200 text-slate-800';

  return (
    <div className="fixed inset-0 z-[10000] flex items-center justify-center bg-slate-900/50 p-4 backdrop-blur-sm" data-testid="session-warning">
      <div role="alertdialog" aria-modal="true" aria-labelledby="session-warning-title" className={`w-full max-w-md rounded-3xl border p-8 shadow-2xl ${card}`}>
        <div className="flex items-center gap-3">
          <div className={`flex h-11 w-11 items-center justify-center rounded-2xl ${reauth ? 'bg-blue-100 text-blue-600' : 'bg-amber-100 text-amber-600'}`}>
            {reauth ? <ShieldAlert size={22} /> : <Clock size={22} />}
          </div>
          <h2 id="session-warning-title" className="text-lg font-bold">{reauth ? 'Confirma tu contraseña' : 'Tu sesión está por cerrarse'}</h2>
        </div>

        <p className={`mt-4 text-sm ${muted}`}>
          {reauth
            ? 'Por seguridad, después de 12 horas hay que confirmar la contraseña. Escríbela para seguir donde estabas, sin perder tu trabajo.'
            : 'Llevas un rato sin actividad. Si no haces nada, cerraremos tu sesión.'}
        </p>

        <p className="mt-5 text-center text-4xl font-extrabold tabular-nums" data-testid="session-countdown" aria-live="polite">{formatCountdown(remainingMs)}</p>

        {reauth ? (
          <form onSubmit={submitPassword} className="mt-5 space-y-3">
            <input
              type="password"
              autoFocus
              autoComplete="current-password"
              value={password}
              onChange={e => setPassword(e.target.value)}
              placeholder="Tu contraseña"
              className={`w-full rounded-xl border px-4 py-3 outline-none focus:ring-2 focus:ring-blue-500 ${input}`}
            />
            {error && <p className="text-sm font-medium text-rose-500" role="alert">{error}</p>}
            <div className="flex gap-3">
              <button type="button" onClick={onExpired} className={`flex-1 rounded-xl px-4 py-3 text-sm font-semibold ${dark ? 'bg-slate-800 text-slate-300 hover:bg-slate-700' : 'bg-slate-100 text-slate-600 hover:bg-slate-200'}`}>
                Cerrar sesión
              </button>
              <button type="submit" disabled={busy || !password} className="flex flex-1 items-center justify-center gap-2 rounded-xl bg-blue-600 px-4 py-3 text-sm font-bold text-white hover:bg-blue-500 disabled:opacity-50">
                {busy && <Loader2 size={16} className="animate-spin" />} Confirmar
              </button>
            </div>
          </form>
        ) : (
          <div className="mt-6 flex gap-3">
            <button type="button" onClick={onExpired} className={`flex-1 rounded-xl px-4 py-3 text-sm font-semibold ${dark ? 'bg-slate-800 text-slate-300 hover:bg-slate-700' : 'bg-slate-100 text-slate-600 hover:bg-slate-200'}`}>
              Cerrar sesión
            </button>
            <button type="button" onClick={() => void renew()} autoFocus className="flex-1 rounded-xl bg-blue-600 px-4 py-3 text-sm font-bold text-white hover:bg-blue-500">
              Seguir conectado
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
