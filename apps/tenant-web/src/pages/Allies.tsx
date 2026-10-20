import React, { useEffect, useState } from 'react';
import { Building2, Loader2, Lock, Network, Unlock } from 'lucide-react';
import { toast } from 'sonner';
import { api, getErrorMessage } from '../lib/api';

interface Ally {
  id: string;
  name: string;
  slug: string;
  isActive: boolean;
  clients: number;
  blocked: boolean;
  blockedAt: string | null;
  blockReason: string | null;
  platformBlocked: boolean;
}

// Aliados del tenant (los que lo tienen como padre). Bloquear a un aliado deja sin acceso al portal y a la API a todo su árbol: el propio
// aliado, sus clientes y los aliados de ese aliado. Al desbloquear todo vuelve. Es un bloqueo administrativo, reversible, con motivo opcional.
export default function Allies() {
  const [allies, setAllies] = useState<Ally[] | null>(null);
  const [confirmingId, setConfirmingId] = useState<string | null>(null);
  const [reason, setReason] = useState('');
  const [workingId, setWorkingId] = useState<string | null>(null);

  const load = () =>
    api.get('/tenant/allies')
      .then(res => setAllies(res.data))
      .catch(err => { toast.error(getErrorMessage(err, 'No se pudieron cargar los aliados')); setAllies([]); });

  useEffect(() => { load(); }, []);

  const setBlock = async (ally: Ally, blocked: boolean) => {
    setWorkingId(ally.id);
    try {
      await api.put(`/tenant/allies/${ally.id}/block`, { blocked, reason: blocked ? reason.trim() || null : null });
      toast.success(blocked ? `${ally.name} bloqueado: ni él ni sus clientes pueden entrar ni emitir.` : `${ally.name} desbloqueado.`);
      setConfirmingId(null);
      setReason('');
      await load();
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo cambiar el bloqueo'));
    } finally {
      setWorkingId(null);
    }
  };

  if (allies === null) {
    return <div className="flex h-full items-center justify-center p-8"><Loader2 className="w-8 h-8 animate-spin text-slate-400" /></div>;
  }

  return (
    <div className="p-8">
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-slate-900">Aliados</h1>
        <p className="text-slate-500 text-sm mt-1">
          Al bloquear un aliado, ni él ni sus clientes (ni los aliados de ese aliado) pueden entrar al portal ni emitir por API. Al desbloquearlo todo vuelve a funcionar.
        </p>
      </div>

      {allies.length === 0 ? (
        <div className="bg-white rounded-2xl border border-slate-200 p-8 text-center">
          <Network className="mx-auto mb-3 text-slate-300" size={40} />
          <p className="text-slate-600 font-medium">Aún no tienes aliados.</p>
          <p className="text-slate-400 text-sm mt-1">Cuando haya tenants asociados a tu grupo, aquí podrás bloquearlos o desbloquearlos.</p>
        </div>
      ) : (
        <div className="space-y-4">
          {allies.map(a => (
            <div key={a.id} className="bg-white rounded-2xl border border-slate-200 p-5">
              <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                <div className="flex items-start gap-3 min-w-0">
                  <div className="bg-slate-100 p-2 rounded-lg shrink-0"><Building2 size={18} className="text-slate-600" /></div>
                  <div className="min-w-0">
                    <p className="font-semibold text-slate-900 truncate">{a.name}</p>
                    <p className="text-xs text-slate-400">{a.clients} cliente(s) activo(s)</p>
                    <div className="mt-2 flex flex-wrap gap-2">
                      {a.blocked && <span className="px-2.5 py-1 rounded-full text-xs font-bold bg-red-600 text-white">Bloqueado</span>}
                      {a.platformBlocked && <span className="px-2.5 py-1 rounded-full text-xs font-bold bg-amber-100 text-amber-800" title="Lo suspendió la plataforma; solo ella lo levanta">Suspendido por la plataforma</span>}
                      {!a.isActive && <span className="px-2.5 py-1 rounded-full text-xs font-bold bg-slate-200 text-slate-600">Inactivo</span>}
                    </div>
                    {a.blocked && a.blockReason && <p className="mt-2 text-sm text-slate-500">Motivo: {a.blockReason}</p>}
                  </div>
                </div>

                {a.blocked ? (
                  <button disabled={workingId === a.id} onClick={() => setBlock(a, false)} className="shrink-0 inline-flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl text-sm font-bold bg-emerald-600 text-white hover:bg-emerald-700 disabled:opacity-50">
                    <Unlock size={16} /> Desbloquear
                  </button>
                ) : confirmingId !== a.id ? (
                  <button onClick={() => { setConfirmingId(a.id); setReason(''); }} className="shrink-0 inline-flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl text-sm font-bold bg-red-600 text-white hover:bg-red-700">
                    <Lock size={16} /> Bloquear
                  </button>
                ) : null}
              </div>

              {!a.blocked && confirmingId === a.id && (
                <div className="mt-4 flex flex-col gap-3 sm:flex-row">
                  <input type="text" value={reason} maxLength={500} onChange={e => setReason(e.target.value)} placeholder="Motivo (opcional, lo verá el aliado al intentar entrar)" className="flex-1 px-4 py-2.5 border border-slate-200 rounded-xl text-sm outline-none focus:border-red-400" />
                  <button disabled={workingId === a.id} onClick={() => setBlock(a, true)} className="px-4 py-2.5 rounded-xl text-sm font-bold bg-red-600 text-white hover:bg-red-700 disabled:opacity-50">Confirmar bloqueo</button>
                  <button onClick={() => { setConfirmingId(null); setReason(''); }} className="px-4 py-2.5 rounded-xl text-sm font-bold bg-slate-100 text-slate-600 hover:bg-slate-200">Cancelar</button>
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
