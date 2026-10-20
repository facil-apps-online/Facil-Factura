import React, { useState, useEffect } from 'react';
import { Building2, Loader2, Layers } from 'lucide-react';
import { api, getErrorMessage } from '../lib/api';
import { toast } from 'sonner';
import MonthRangeBar, { currentMonthRange, downloadBlob, type MonthRangeValue } from '@shared/components/MonthRangeBar';

interface RangeMonth {
  year: number;
  month: number;
  documentsEmitted: number;
  documentsRejected: number;
  amountDueToPlatform: number;
  cutStatus: string | null;
}

interface GroupTenant {
  tenantId: string;
  name: string;
  isSelf: boolean;
  platformBillingHidden: boolean;
  months: RangeMonth[];
  documentsEmitted: number;
  documentsRejected: number;
  amountDueToPlatform: number;
}

interface GroupRange {
  from: string;
  to: string;
  isGroup: boolean;
  months: RangeMonth[];
  tenants: GroupTenant[];
  totalDocumentsEmitted: number;
  totalDocumentsRejected: number;
  totalAmountDueToPlatform: number;
}

const formatCurrency = (value: number) =>
  new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 }).format(value);

const BILLING_STATUS: Record<string, string> = { Pending: 'Pendiente', Paid: 'Pagada', Overdue: 'Vencida' };

const MONTH_NAMES = ['Ene', 'Feb', 'Mar', 'Abr', 'May', 'Jun', 'Jul', 'Ago', 'Sep', 'Oct', 'Nov', 'Dic'];

// Consumo del grupo (este tenant y sus aliados) por rango de meses: documentos emitidos y rechazados y lo que se paga a la plataforma, mes a mes.
// Los meses con corte ya generado muestran lo facturado y su estado de pago; el mes en curso se calcula en vivo.
export default function GroupBilling() {
  const [data, setData] = useState<GroupRange | null>(null);
  const [loading, setLoading] = useState(true);
  const [range, setRange] = useState<MonthRangeValue>(currentMonthRange());
  const [exporting, setExporting] = useState(false);

  useEffect(() => {
    setLoading(true);
    api.get<GroupRange>(`/tenant/dashboard/group-billing-range?from=${range.from}&to=${range.to}`)
      .then(res => setData(res.data))
      .catch(err => toast.error(getErrorMessage(err, 'Error al cargar la facturación del grupo')))
      .finally(() => setLoading(false));
  }, [range.from, range.to]);

  const exportRange = async () => {
    setExporting(true);
    try {
      await downloadBlob(api, `/tenant/dashboard/group-billing-range/export?from=${range.from}&to=${range.to}`, `consumo_grupo_${range.from}_a_${range.to}.xlsx`);
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo exportar'));
    } finally {
      setExporting(false);
    }
  };

  if (!data) {
    return (
      <div className="flex h-full w-full items-center justify-center p-8">
        <Loader2 className="animate-spin text-primary w-10 h-10" />
      </div>
    );
  }

  if (!data.isGroup) {
    return (
      <div className="p-8">
        <h1 className="text-2xl font-bold text-slate-900 mb-2">Facturación del grupo</h1>
        <div className="bg-white rounded-2xl border border-slate-200 p-8 text-center mt-6">
          <Layers className="mx-auto mb-3 text-slate-300" size={40} />
          <p className="text-slate-600 font-medium">Aún no hay otras cuentas asociadas.</p>
          <p className="text-slate-400 text-sm mt-1">Cuando haya cuentas asociadas, aquí verás su facturación consolidada.</p>
        </div>
      </div>
    );
  }

  return (
    <div className={`p-8 ${loading ? 'opacity-70' : ''}`}>
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-slate-900">Facturación del grupo</h1>
        <p className="text-slate-500 text-sm mt-1">Consumo de tu tenant y de los tenants asociados a tu grupo empresarial, mes a mes.</p>
      </div>

      <div className="mb-6">
        <MonthRangeBar value={range} onChange={setRange} onExport={exportRange} exporting={exporting} />
      </div>

      <div className="grid grid-cols-1 gap-6 mb-8 md:grid-cols-3">
        <div className="bg-primary text-white rounded-2xl p-6 shadow-lg shadow-primary/20 md:col-span-1">
          <p className="text-sm opacity-80 mb-1">Total a pagar a la plataforma en el período</p>
          <p className="text-4xl font-extrabold">{formatCurrency(data.totalAmountDueToPlatform)}</p>
        </div>
        <div className="bg-white rounded-2xl border border-slate-200 p-6">
          <p className="text-sm text-slate-500 mb-1">Documentos emitidos</p>
          <p className="text-3xl font-bold text-slate-800">{data.totalDocumentsEmitted}</p>
        </div>
        <div className="bg-white rounded-2xl border border-slate-200 p-6">
          <p className="text-sm text-slate-500 mb-1">Documentos rechazados</p>
          <p className="text-3xl font-bold text-slate-800">{data.totalDocumentsRejected}</p>
        </div>
      </div>

      <div className="space-y-6">
        {data.tenants.map(t => (
          <div key={t.tenantId} className="bg-white rounded-2xl border border-slate-200 overflow-hidden">
            <div className="flex items-center justify-between px-6 py-4 border-b border-slate-100">
              <div className="flex items-center gap-3">
                <div className="bg-slate-100 p-2 rounded-lg"><Building2 size={18} className="text-slate-600" /></div>
                <div>
                  <p className="font-semibold text-slate-900">{t.name}{t.isSelf && <span className="ml-2 text-xs font-normal text-slate-400">(este tenant)</span>}</p>
                  <p className="text-xs text-slate-400">{t.documentsEmitted} emitidas · {t.documentsRejected} rechazadas</p>
                </div>
              </div>
              {t.platformBillingHidden
                ? <p className="text-xs text-slate-400">Cobro no visible</p>
                : <p className="text-lg font-bold text-slate-900">{formatCurrency(t.amountDueToPlatform)}</p>}
            </div>

            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-slate-400 text-xs uppercase">
                  <th className="px-6 py-2 font-medium">Periodo</th>
                  <th className="px-6 py-2 font-medium text-right">Emitidas</th>
                  <th className="px-6 py-2 font-medium text-right">Rechazadas</th>
                  {!t.platformBillingHidden && <th className="px-6 py-2 font-medium text-right">Monto</th>}
                  {!t.platformBillingHidden && <th className="px-6 py-2 font-medium">Estado</th>}
                </tr>
              </thead>
              <tbody>
                {t.months.map(b => (
                  <tr key={`${b.year}-${b.month}`} className="border-t border-slate-50">
                    <td className="px-6 py-2 text-slate-700">{MONTH_NAMES[b.month - 1]} {b.year}</td>
                    <td className="px-6 py-2 text-slate-700 text-right">{b.documentsEmitted}</td>
                    <td className="px-6 py-2 text-slate-700 text-right">{b.documentsRejected}</td>
                    {!t.platformBillingHidden && <td className="px-6 py-2 text-slate-700 text-right">{formatCurrency(b.amountDueToPlatform)}</td>}
                    {!t.platformBillingHidden && (
                      <td className="px-6 py-2">
                        {b.cutStatus ? (
                          <span className={`px-2 py-0.5 rounded-full text-xs font-medium ${
                            b.cutStatus === 'Paid' ? 'bg-emerald-50 text-emerald-600' :
                            b.cutStatus === 'Overdue' ? 'bg-rose-50 text-rose-600' : 'bg-amber-50 text-amber-600'
                          }`}>{BILLING_STATUS[b.cutStatus] ?? b.cutStatus}</span>
                        ) : (
                          <span className="text-xs text-slate-400">Sin corte (en vivo)</span>
                        )}
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ))}
      </div>
    </div>
  );
}
