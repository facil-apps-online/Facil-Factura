import React, { useCallback, useEffect, useRef, useState } from 'react';
import { ChevronDown, LogOut, UserRound } from 'lucide-react';
import { ACCOUNT_CHANGED_EVENT } from './sessionTypes';
import type { AccountMe, SessionHttp } from './sessionTypes';

// Datos del usuario para el avatar. Se piden una vez y se refrescan cuando el perfil avisa de un cambio.
export function useAccountMe(api: SessionHttp, basePath: string) {
  const [me, setMe] = useState<AccountMe | null>(null);
  const reload = useCallback(() => {
    api.get(`${basePath}/me`).then(res => setMe(res.data)).catch(() => { /* un 401 lo atiende el interceptor del portal */ });
  }, [api, basePath]);
  useEffect(reload, [reload]);
  useEffect(() => {
    const onChanged = (e: Event) => setMe((e as CustomEvent<AccountMe>).detail);
    window.addEventListener(ACCOUNT_CHANGED_EVENT, onChanged);
    return () => window.removeEventListener(ACCOUNT_CHANGED_EVENT, onChanged);
  }, []);
  return { me, reload };
}

// "María Pérez" -> "MP"; "admin@x.co" -> "AD".
export function initialsOf(text: string): string {
  const words = text.trim().split(/\s+/).filter(Boolean);
  if (words.length >= 2) return (words[0][0] + words[1][0]).toUpperCase();
  return (words[0] ?? 'U').substring(0, 2).toUpperCase();
}

export interface UserMenuProps {
  me: AccountMe | null;
  // Mientras llegan los datos del servidor se usa este texto (por ejemplo, el nombre que guardó el login).
  fallbackName?: string;
  onProfile: () => void;
  onLogout: () => void;
  theme?: 'light' | 'dark';
}

// Avatar con el menú del usuario, como en Google, GitHub o Stripe: el botón muestra la inicial (y, en pantallas anchas, nombre y rol) y al
// abrirlo se ve toda la identidad (nombre, correo, rol y empresa) con "Mi perfil" y "Cerrar sesión".
export default function UserMenu({ me, fallbackName, onProfile, onLogout, theme = 'light' }: UserMenuProps) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  const name = me?.displayName || fallbackName || 'Usuario';
  const dark = theme === 'dark';

  useEffect(() => {
    if (!open) return;
    const onDown = (e: MouseEvent) => { if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false); };
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') setOpen(false); };
    document.addEventListener('mousedown', onDown);
    document.addEventListener('keydown', onKey);
    return () => { document.removeEventListener('mousedown', onDown); document.removeEventListener('keydown', onKey); };
  }, [open]);

  const text = dark ? 'text-slate-200' : 'text-slate-700';
  const subtle = dark ? 'text-slate-400' : 'text-slate-500';
  const panel = dark ? 'bg-slate-900 border-slate-700' : 'bg-white border-slate-200';
  const item = dark ? 'text-slate-200 hover:bg-slate-800' : 'text-slate-700 hover:bg-slate-50';

  return (
    <div ref={ref} className="relative" data-testid="user-menu">
      <button
        type="button"
        onClick={() => setOpen(o => !o)}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label="Menú de usuario"
        className="flex items-center gap-3 rounded-xl py-1 pl-1 pr-2 transition-colors hover:bg-slate-500/10"
      >
        <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-primary text-sm font-bold text-white shadow-md" data-testid="user-avatar">
          {initialsOf(name)}
        </span>
        <span className="hidden min-w-0 text-left leading-tight md:block">
          <span className={`block max-w-[11rem] truncate text-sm font-semibold ${text}`}>{name}</span>
          {me?.role && <span className={`block max-w-[11rem] truncate text-xs ${subtle}`}>{me.role}</span>}
        </span>
        <ChevronDown size={14} className={`hidden md:block ${subtle}`} />
      </button>

      {open && (
        <div role="menu" className={`absolute right-0 z-50 mt-2 w-72 max-w-[calc(100vw-2rem)] overflow-hidden rounded-2xl border shadow-xl ${panel}`}>
          <div className="px-4 py-4">
            <p className={`truncate text-sm font-bold ${text}`}>{name}</p>
            {me?.email && <p className={`truncate text-xs ${subtle}`}>{me.email}</p>}
            {(me?.role || me?.organization) && (
              <p className={`mt-1 truncate text-xs ${subtle}`}>{[me?.role, me?.organization].filter(Boolean).join(' · ')}</p>
            )}
          </div>
          <div className={`border-t py-1 ${dark ? 'border-slate-700' : 'border-slate-100'}`}>
            <button type="button" role="menuitem" onClick={() => { setOpen(false); onProfile(); }} className={`flex w-full items-center gap-3 px-4 py-2.5 text-sm font-medium ${item}`}>
              <UserRound size={16} /> Mi perfil
            </button>
            <button type="button" role="menuitem" onClick={() => { setOpen(false); onLogout(); }} className="flex w-full items-center gap-3 px-4 py-2.5 text-sm font-medium text-rose-500 hover:bg-rose-500/10">
              <LogOut size={16} /> Cerrar sesión
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
