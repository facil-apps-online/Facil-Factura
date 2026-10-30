import { useEffect, useState } from 'react';
import { Loader2, Plus, Wallet } from 'lucide-react';
import { toast } from 'sonner';
import { api } from './api';

type Movement = { id: string; type: string; amount: number; reference: string; notes: string; occurredAt: string; createdBy: string; clientName: string | null };
type Balance = { available: number; reserved: number; consumed: number; deposited: number; isBillingExempt: boolean; movements: Movement[] };

const money = (value: number) => `$${Number(value ?? 0).toLocaleString('es-CO', { maximumFractionDigits: 2 })}`;
const MOVEMENT_LABELS: Record<string, string> = { Deposit: 'Abono', Adjustment: 'Ajuste', Reserve: 'Reserva', Release: 'Devolución' };

// Saldo prepagado de certificados de un tenant: aquí el superadmin registra los pagos recibidos (abonos) y las correcciones (ajustes). Las
// reservas y devoluciones las genera el sistema con cada solicitud.
export function TenantCertificateBalance({ tenantId }: { tenantId: string }) {
  const [balance, setBalance] = useState<Balance | null>(null);
  const [saving, setSaving] = useState(false);
  const [form, setForm] = useState({ type: 'Deposit', amount: '', reference: '', notes: '', occurredAt: '' });

  const load = () => {
    api.get<Balance>(`/certificates/balances/${tenantId}`)
      .then(res => setBalance(res.data))
      .catch(() => toast.error('Error cargando el saldo de certificados'));
  };

  useEffect(load, [tenantId]);

  const save = async () => {
    const amount = Number(form.amount || 0);
    if (!amount || !form.reference.trim()) {
      toast.error('Indica el valor y la referencia.');
      return;
    }
    setSaving(true);
    try {
      await api.post(`/certificates/balances/${tenantId}/movements`, {
        type: form.type,
        amount,
        reference: form.reference,
        notes: form.notes,
        occurredAt: form.occurredAt ? new Date(`${form.occurredAt}T12:00:00-05:00`).toISOString() : null,
      });
      toast.success(form.type === 'Deposit' ? 'Abono registrado' : 'Ajuste registrado');
      setForm({ type: form.type, amount: '', reference: '', notes: '', occurredAt: '' });
      load();
    } catch (error: any) {
      toast.error(error.response?.data?.message ?? 'No fue posible registrar el movimiento.');
    } finally {
      setSaving(false);
    }
  };

  const field = 'mt-1 w-full px-3 py-2.5 bg-slate-800 border border-slate-700 rounded-xl text-white text-sm';

  return (
    <div className="mb-8 p-6 rounded-3xl border border-slate-700/50 glass-panel">
      <div className="flex items-center gap-2"><Wallet className="w-5 h-5 text-emerald-400" /><h2 className="text-lg font-bold text-white">Saldo de certificados</h2></div>
      <p className="text-sm text-slate-400 mt-1">Prepagado: cada certificado de producción que solicita el tenant reserva su precio de este saldo; si Viafirma lo rechaza se devuelve y al emitirse queda consumido. Registra aquí cada pago recibido.</p>
      {balance?.isBillingExempt && <p className="text-sm text-amber-300 mt-2">Este tenant está exento de cobro: sus certificados no descuentan saldo.</p>}

      {!balance ? (
        <div className="py-6 text-slate-400"><Loader2 className="animate-spin" /></div>
      ) : (
        <>
          <div className="mt-4 grid grid-cols-2 gap-3 md:grid-cols-4">
            {[
              ['Disponible', balance.available, 'text-emerald-300'],
              ['Reservado', balance.reserved, 'text-amber-300'],
              ['Consumido', balance.consumed, 'text-slate-200'],
              ['Abonado', balance.deposited, 'text-slate-200'],
            ].map(([label, value, color]) => (
              <div key={label as string} className="rounded-2xl border border-slate-700/50 bg-slate-800/40 p-4">
                <p className="text-xs uppercase tracking-wide text-slate-500">{label}</p>
                <p className={`mt-1 text-xl font-bold ${color}`}>{money(value as number)}</p>
              </div>
            ))}
          </div>

          <div className="mt-6 grid grid-cols-1 gap-3 md:grid-cols-6 items-end">
            <label className="block text-sm text-slate-300">Tipo
              <select value={form.type} onChange={e => setForm({ ...form, type: e.target.value })} className={field}>
                <option value="Deposit">Abono</option>
                <option value="Adjustment">Ajuste (+/-)</option>
              </select>
            </label>
            <label className="block text-sm text-slate-300">Valor (COP)
              <input type="number" step="100" value={form.amount} onChange={e => setForm({ ...form, amount: e.target.value })} placeholder={form.type === 'Adjustment' ? 'Ej. -50000' : ''} className={field} />
            </label>
            <label className="block text-sm text-slate-300 md:col-span-2">Referencia
              <input value={form.reference} maxLength={200} onChange={e => setForm({ ...form, reference: e.target.value })} placeholder="N.º de transferencia o motivo" className={field} />
            </label>
            <label className="block text-sm text-slate-300">Fecha del pago
              <input type="date" value={form.occurredAt} onChange={e => setForm({ ...form, occurredAt: e.target.value })} className={field} />
            </label>
            <button type="button" onClick={save} disabled={saving} className="flex items-center justify-center gap-2 rounded-xl bg-emerald-600 px-4 py-2.5 text-sm font-bold text-white hover:bg-emerald-500 disabled:opacity-50">
              {saving ? <Loader2 className="w-4 animate-spin" /> : <Plus className="w-4" />} Registrar
            </button>
            <label className="block text-sm text-slate-300 md:col-span-6">Notas (opcional)
              <input value={form.notes} maxLength={1000} onChange={e => setForm({ ...form, notes: e.target.value })} className={field} />
            </label>
          </div>

          <div className="mt-6 overflow-x-auto">
            {balance.movements.length === 0 ? (
              <p className="text-sm text-slate-500">Sin movimientos todavía.</p>
            ) : (
              <table className="w-full text-sm">
                <thead className="text-left text-slate-400">
                  <tr><th className="py-2 pr-4">Fecha</th><th className="py-2 pr-4">Tipo</th><th className="py-2 pr-4">Detalle</th><th className="py-2 pr-4">Registró</th><th className="py-2 text-right">Valor</th></tr>
                </thead>
                <tbody className="divide-y divide-slate-800 text-slate-200">
                  {balance.movements.map(m => (
                    <tr key={m.id}>
                      <td className="py-2 pr-4 whitespace-nowrap">{new Date(m.occurredAt).toLocaleDateString('es-CO')}</td>
                      <td className="py-2 pr-4">{MOVEMENT_LABELS[m.type] ?? m.type}</td>
                      <td className="py-2 pr-4">{[m.clientName, m.reference, m.notes].filter(Boolean).join(' · ')}</td>
                      <td className="py-2 pr-4 text-slate-400">{m.createdBy}</td>
                      <td className={`py-2 text-right font-semibold ${m.amount >= 0 ? 'text-emerald-300' : 'text-rose-300'}`}>{m.amount >= 0 ? '+' : '-'}{money(Math.abs(m.amount))}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </>
      )}
    </div>
  );
}
