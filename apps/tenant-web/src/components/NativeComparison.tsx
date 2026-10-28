import React, { useEffect, useState } from 'react';
import { Calculator, Loader2 } from 'lucide-react';
import { api } from '../lib/api';

interface TenantRow { tenantId: string; tenantName: string; isSelf: boolean; documents: number; amount: number }
interface TypeRow { typeCode: string; documents: number; price: number; amount: number }
interface Summary { documents: number; amount: number; tenants: TenantRow[]; types: TypeRow[] }
interface Comparison {
  isGroup: boolean;
  year?: number;
  month?: number;
  hasRates?: boolean;
  missingPriceTypes?: string[];
  since?: string | null;
  period?: Summary;
  accumulated?: Summary;
}

const money = (value: number) => `$${value.toLocaleString('es-CO')}`;
const monthName = (year: number, month: number) => new Date(year, month - 1, 1).toLocaleDateString('es-CO', { month: 'long', year: 'numeric' });

// Para el tenant que agrupa a otros: cuánto costaría haber emitido con el servicio nativo lo que su grupo emitió por Dataico, con la tarifa por documento que le fijó la
// administración de la plataforma. Solo aparece si el tenant tiene aliados. `month` es AAAA-MM (el último mes del período elegido arriba).
export default function NativeComparison({ month }: { month: string }) {
  const [data, setData] = useState<Comparison | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const [year, m] = month.split('-').map(Number);
    setLoading(true);
    api.get<Comparison>(`/tenant/dashboard/native-comparison?year=${year}&month=${m}`)
      .then(res => setData(res.data))
      .catch(() => setData(null))
      .finally(() => setLoading(false));
  }, [month]);

  if (!data?.isGroup || !data.period || !data.accumulated) return null;
  const period = data.period;
  const accumulated = data.accumulated;

  return (
    <div className={`mb-8 rounded-3xl border border-indigo-100 bg-indigo-50/60 p-6 shadow-sm ${loading ? 'opacity-60' : ''}`} data-testid="native-comparison">
      <div className="mb-4 flex items-center gap-2">
        <Calculator className="h-5 w-5 text-indigo-600" />
        <h2 className="text-lg font-bold text-slate-800">Comparativo con el servicio nativo</h2>
        {loading && <Loader2 className="h-4 w-4 animate-spin text-indigo-500" />}
      </div>

      {!data.hasRates ? (
        <p className="rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
          Todavía no hay una tarifa por documento definida para tu tenant. La define la administración de la plataforma; cuando esté, aquí verás cuánto costaría emitir con el servicio nativo lo que tu grupo emite por Dataico.
        </p>
      ) : (
        <>
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div className="rounded-2xl bg-white p-5 shadow-sm">
              <p className="text-xs font-bold uppercase tracking-wider text-slate-400 capitalize">{monthName(data.year!, data.month!)}</p>
              <p className="mt-2 text-sm text-slate-600">Tu grupo emitió <b className="text-slate-800">{period.documents.toLocaleString('es-CO')}</b> documentos por Dataico.</p>
              <p className="mt-1 text-sm text-slate-600">Con el servicio nativo habrían costado</p>
              <p className="text-3xl font-black text-indigo-700" data-testid="comparison-period-amount">{money(period.amount)}</p>
            </div>
            <div className="rounded-2xl bg-white p-5 shadow-sm">
              <p className="text-xs font-bold uppercase tracking-wider text-slate-400">
                {data.since ? `Acumulado desde ${new Date(data.since).toLocaleDateString('es-CO', { day: 'numeric', month: 'long', year: 'numeric' })}` : 'Acumulado'}
              </p>
              <p className="mt-2 text-sm text-slate-600"><b className="text-slate-800">{accumulated.documents.toLocaleString('es-CO')}</b> documentos por Dataico.</p>
              <p className="mt-1 text-sm text-slate-600">Con el servicio nativo habrían costado</p>
              <p className="text-3xl font-black text-indigo-700" data-testid="comparison-accumulated-amount">{money(accumulated.amount)}</p>
            </div>
          </div>

          <div className="mt-5 overflow-x-auto">
            <table className="w-full border-collapse text-left text-sm">
              <thead>
                <tr className="border-b border-indigo-100 text-xs uppercase tracking-wider text-slate-500">
                  <th className="p-2 font-bold">Empresa del grupo</th>
                  <th className="p-2 text-right font-bold">Documentos del mes</th>
                  <th className="p-2 text-right font-bold">Costaría en nativo</th>
                </tr>
              </thead>
              <tbody>
                {period.tenants.map(t => (
                  <tr key={t.tenantId} className="border-b border-indigo-50">
                    <td className="p-2 font-medium text-slate-700">{t.tenantName}{t.isSelf && <span className="ml-2 text-xs text-slate-400">(tu empresa)</span>}</td>
                    <td className="p-2 text-right">{t.documents.toLocaleString('es-CO')}</td>
                    <td className="p-2 text-right font-mono">{money(t.amount)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {(data.missingPriceTypes?.length ?? 0) > 0 && (
            <p className="mt-4 rounded-xl border border-amber-200 bg-amber-50 px-4 py-2 text-xs text-amber-800">
              Estos tipos de documento no tienen tarifa definida y no suman al costo: {data.missingPriceTypes!.join(', ')}.
            </p>
          )}
        </>
      )}
    </div>
  );
}
