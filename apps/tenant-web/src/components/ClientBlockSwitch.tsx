import React, { useState } from 'react';
import { toast } from 'sonner';
import { api, getErrorMessage } from '../lib/api';

interface ClientBlockSwitchProps {
  clientId: string;
  clientName: string;
  blocked: boolean;
  reason?: string | null;
  onChanged: (blocked: boolean, reason: string | null) => void;
}

// Interruptor de bloqueo de un cliente (p. ej. por un pago atrasado): bloqueado, sus usuarios no entran al portal ni su API emite. Al encenderlo
// se pide un motivo opcional; al apagarlo vuelve todo. El cliente sigue activo y sigue contando en el cobro.
export default function ClientBlockSwitch({ clientId, clientName, blocked, reason, onChanged }: ClientBlockSwitchProps) {
  const [asking, setAsking] = useState(false);
  const [text, setText] = useState('');
  const [working, setWorking] = useState(false);

  const apply = async (next: boolean) => {
    setWorking(true);
    try {
      const res = await api.put(`/tenant/clients/${clientId}/block`, { blocked: next, reason: next ? text.trim() || null : null });
      onChanged(res.data.blocked, res.data.blockReason ?? null);
      toast.success(next ? `${clientName} bloqueado: sus usuarios no pueden entrar ni emitir.` : `${clientName} desbloqueado.`);
      setAsking(false);
      setText('');
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo cambiar el bloqueo'));
    } finally {
      setWorking(false);
    }
  };

  return (
    <>
      <button
        type="button"
        role="switch"
        aria-checked={blocked}
        aria-label={`${blocked ? 'Desbloquear' : 'Bloquear'} a ${clientName}`}
        title={blocked ? `Bloqueado${reason ? `: ${reason}` : ''}. Clic para desbloquear.` : 'Clic para bloquear el acceso del cliente'}
        disabled={working}
        onClick={e => { e.stopPropagation(); if (blocked) apply(false); else setAsking(true); }}
        className={`relative inline-flex h-6 w-11 shrink-0 items-center rounded-full transition-colors disabled:opacity-50 ${blocked ? 'bg-rose-600' : 'bg-slate-300'}`}
      >
        <span className={`inline-block h-5 w-5 transform rounded-full bg-white shadow transition-transform ${blocked ? 'translate-x-5' : 'translate-x-0.5'}`} />
      </button>

      {asking && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4" onClick={e => e.stopPropagation()}>
          <div className="w-full max-w-md rounded-2xl bg-white p-6 shadow-2xl">
            <h3 className="text-lg font-bold text-slate-900">Bloquear a {clientName}</h3>
            <p className="mt-2 text-sm text-slate-500">
              Sus usuarios no podrán entrar al portal ni emitir por API hasta que lo desbloquees. Verán un mensaje de servicio suspendido, sin el motivo. Tú sí puedes seguir entrando en modo soporte.
            </p>
            <input
              type="text"
              autoFocus
              value={text}
              maxLength={500}
              onChange={e => setText(e.target.value)}
              placeholder="Motivo (opcional, solo lo ves tú)"
              className="mt-4 w-full rounded-xl border border-slate-200 px-4 py-2.5 text-sm outline-none focus:border-rose-400"
            />
            <div className="mt-5 flex justify-end gap-3">
              <button type="button" onClick={() => { setAsking(false); setText(''); }} className="rounded-xl bg-slate-100 px-4 py-2.5 text-sm font-bold text-slate-600 hover:bg-slate-200">Cancelar</button>
              <button type="button" disabled={working} onClick={() => apply(true)} className="rounded-xl bg-rose-600 px-4 py-2.5 text-sm font-bold text-white hover:bg-rose-700 disabled:opacity-50">Bloquear</button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
