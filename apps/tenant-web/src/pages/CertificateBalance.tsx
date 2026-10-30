import { useEffect, useState } from 'react';
import { Loader2, Wallet } from 'lucide-react';
import { toast } from 'sonner';
import { api, getErrorMessage } from '../lib/api';
import { MOVEMENT_LABELS, money } from '../lib/certificateFormat';

interface BalanceData {
  available: number;
  reserved: number;
  consumed: number;
  deposited: number;
  exempt: boolean;
  movements: { id: string; type: string; amount: number; reference: string; notes: string; occurredAt: string; clientName: string | null }[];
  prices: { profileId: string; title: string; netAmount: number | null; taxRate: number | null; total: number | null }[];
}

// Saldo prepagado de certificados: lo que el tenant tiene disponible para solicitar certificados de producción, lo reservado por las
// solicitudes en curso, lo ya consumido, los precios vigentes y el detalle de movimientos. Los abonos los registra el proveedor.
export default function CertificateBalance() {
  const [data, setData] = useState<BalanceData | null>(null);

  useEffect(() => {
    api.get<BalanceData>('/tenant/certificates/balance')
      .then(res => setData(res.data))
      .catch(err => toast.error(getErrorMessage(err, 'No se pudo cargar el saldo de certificados')));
  }, []);

  if (!data) return <div className="p-8 text-slate-400"><Loader2 className="animate-spin" /></div>;

  const cards: [string, number, string, string][] = [
    ['Disponible', data.available, 'text-emerald-600', 'Para nuevas solicitudes'],
    ['Reservado', data.reserved, 'text-amber-600', 'Solicitudes en curso; se devuelve si Viafirma las rechaza'],
    ['Consumido', data.consumed, 'text-slate-800', 'Certificados ya emitidos'],
    ['Abonado', data.deposited, 'text-slate-800', 'Total de pagos registrados'],
  ];

  return (
    <div className="p-8 max-w-6xl">
      <div className="mb-8">
        <h1 className="text-3xl font-bold text-slate-800">Saldo de certificados</h1>
        <p className="text-slate-500 mt-2">Los certificados de producción se pagan por adelantado: al solicitar uno se reserva su valor de este saldo y queda cobrado cuando se emite. Para abonar, comunícate con tu proveedor. Los de Sandbox (pruebas) no se cobran.</p>
      </div>

      {data.exempt && (
        <div className="mb-6 rounded-2xl border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-800">Tu cuenta está exenta de cobro: tus solicitudes no descuentan saldo.</div>
      )}

      <div className="mb-8 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {cards.map(([label, value, color, help]) => (
          <div key={label} className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <p className="text-xs font-bold uppercase tracking-wider text-slate-400">{label}</p>
            <p className={`mt-1 text-2xl font-bold ${color}`}>{money(value)}</p>
            <p className="mt-1 text-xs text-slate-500">{help}</p>
          </div>
        ))}
      </div>

      <div className="grid grid-cols-1 gap-8 lg:grid-cols-3">
        <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
          <h2 className="mb-4 flex items-center gap-2 font-bold text-slate-800"><Wallet size={18} className="text-blue-600" /> Precios vigentes</h2>
          {data.prices.length === 0 ? (
            <p className="text-sm text-slate-500">No hay certificados disponibles todavía.</p>
          ) : (
            <ul className="space-y-3">
              {data.prices.map(p => (
                <li key={p.profileId} className="text-sm">
                  <p className="font-semibold text-slate-700">{p.title}</p>
                  {p.total == null
                    ? <p className="text-xs text-rose-600">Sin precio vigente: no se puede solicitar. Consulta a tu proveedor.</p>
                    : <p className="text-slate-600">{money(p.total)} <span className="text-xs text-slate-400">({money(p.netAmount)} + IVA {p.taxRate}%)</span></p>}
                </li>
              ))}
            </ul>
          )}
        </section>

        <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm lg:col-span-2">
          <h2 className="mb-4 font-bold text-slate-800">Movimientos</h2>
          {data.movements.length === 0 ? (
            <p className="text-sm text-slate-500">Aún no hay movimientos.</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead className="text-left text-xs uppercase tracking-wider text-slate-400">
                  <tr><th className="py-2 pr-4">Fecha</th><th className="py-2 pr-4">Tipo</th><th className="py-2 pr-4">Detalle</th><th className="py-2 text-right">Valor</th></tr>
                </thead>
                <tbody className="divide-y divide-slate-100 text-slate-700">
                  {data.movements.map(m => (
                    <tr key={m.id}>
                      <td className="py-2 pr-4 whitespace-nowrap">{new Date(m.occurredAt).toLocaleDateString('es-CO')}</td>
                      <td className="py-2 pr-4">{MOVEMENT_LABELS[m.type] ?? m.type}</td>
                      <td className="py-2 pr-4 text-slate-500">{[m.clientName, m.reference, m.notes].filter(Boolean).join(' · ')}</td>
                      <td className={`py-2 text-right font-semibold whitespace-nowrap ${m.amount >= 0 ? 'text-emerald-600' : 'text-rose-600'}`}>{m.amount >= 0 ? '+' : '-'}{money(Math.abs(m.amount))}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
      </div>
    </div>
  );
}
