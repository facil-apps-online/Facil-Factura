import { useState } from 'react';
import { Pencil, Lock } from 'lucide-react';

// Los formularios de credenciales arrancan bloqueados: el navegador no puede autocompletarlos con claves guardadas ni se pueden
// cambiar por accidente. El lapicito los habilita (mismo patrón que las credenciales de certificados del portal de superadmin).
export function useCredentialLock() {
  const [unlocked, setUnlocked] = useState(false);
  return { unlocked, unlock: () => setUnlocked(true), lock: () => setUnlocked(false), toggle: () => setUnlocked(value => !value) };
}

// Atributos para cada input de credencial: solo lectura mientras está bloqueado y sin autocompletado (new-password evita que
// Chrome rellene usuario y clave guardados).
export function lockedInputProps(unlocked: boolean) {
  return { readOnly: !unlocked, autoComplete: 'new-password' } as const;
}

export function lockedInputClass(unlocked: boolean, base: string) {
  return `${base} ${unlocked ? '' : 'bg-slate-100 text-slate-500 cursor-not-allowed'}`;
}

export function CredentialLockButton({ unlocked, onToggle }: { unlocked: boolean; onToggle: () => void }) {
  return (
    <button
      type="button"
      onClick={onToggle}
      title={unlocked ? 'Bloquear la edición' : 'Habilitar la edición'}
      aria-pressed={unlocked}
      className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-semibold border transition-colors ${
        unlocked ? 'bg-amber-50 text-amber-700 border-amber-200 hover:bg-amber-100' : 'bg-white text-slate-600 border-slate-200 hover:bg-slate-50'
      }`}
    >
      {unlocked ? <Lock size={14} /> : <Pencil size={14} />}
      {unlocked ? 'Bloquear' : 'Editar'}
    </button>
  );
}
