import React, { useEffect, useState } from 'react';
import { FileText, XCircle, TrendingUp, Users } from 'lucide-react';
import { toast } from 'sonner';
import { api } from './api';
import DataTable, { type DataTableColumn } from '@shared/components/DataTable';
import MonthRangeBar, { currentMonthRange, downloadBlob, type MonthRangeValue } from '@shared/components/MonthRangeBar';

interface RangeMonth {
  year: number;
  month: number;
  documentsEmitted: number;
  documentsRejected: number;
  amountDue: number;
}

interface RangeTenant {
  tenantId: string;
  name: string;
  months: RangeMonth[];
  documentsEmitted: number;
  documentsRejected: number;
  amountDue: number;
}

interface BillingRange {
  from: string;
  to: string;
  months: RangeMonth[];
  tenants: RangeTenant[];
  totalDocumentsEmitted: number;
  totalDocumentsRejected: number;
  totalAmountDue: number;
}

const MONTH_NAMES = ['enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio', 'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre'];
const monthLabel = (year: number, month: number) => `${MONTH_NAMES[month - 1]} ${year}`;
const money = (value: number) => `$ ${value.toLocaleString('es-CO')}`;

const breakdownColumns: DataTableColumn<RangeTenant>[] = [
  { key: 'tenant', header: 'Tenant', primary: true, cellClassName: 'font-bold text-slate-200 text-lg', render: t => t.name, sortValue: t => t.name, searchValue: t => t.name },
  {
    key: 'documents',
    header: 'Emitidas',
    align: 'right',
    render: t => (
      <div className="flex items-center justify-end text-slate-300 font-medium">
        <FileText className="w-4 h-4 mr-2 text-slate-500" /> {t.documentsEmitted}
      </div>
    ),
    sortValue: t => t.documentsEmitted,
  },
  {
    key: 'rejected',
    header: 'Rechazadas',
    align: 'right',
    render: t => <span className={t.documentsRejected > 0 ? 'text-rose-400 font-medium' : 'text-slate-500'}>{t.documentsRejected}</span>,
    sortValue: t => t.documentsRejected,
  },
  { key: 'subtotal', header: 'A cobrar', align: 'right', cellClassName: 'font-bold text-indigo-400 text-lg', render: t => money(t.amountDue), sortValue: t => t.amountDue },
];

// Consumo por tenant en un rango de meses. El cobro se calcula mes a mes (tarifa por volumen de cada mes, prorrateo por días en el cobro por usuario),
// así que el total del rango es la suma de sus meses.
export const Billing = () => {
  const [data, setData] = useState<BillingRange | null>(null);
  const [loading, setLoading] = useState(false);
  const [range, setRange] = useState<MonthRangeValue>(currentMonthRange());
  const [exporting, setExporting] = useState(false);

  useEffect(() => {
    setLoading(true);
    api.get<BillingRange>(`/dashboard/billing-range?from=${range.from}&to=${range.to}`)
      .then(res => setData(res.data))
      .catch(() => toast.error("Error al cargar las métricas de facturación"))
      .finally(() => setLoading(false));
  }, [range.from, range.to]);

  const exportRange = async () => {
    setExporting(true);
    try {
      await downloadBlob(api, `/dashboard/billing-range/export?from=${range.from}&to=${range.to}`, `consumo_${range.from}_a_${range.to}.xlsx`);
    } catch {
      toast.error('No se pudo exportar');
    } finally {
      setExporting(false);
    }
  };

  if (!data) {
    return (
      <div className="p-10 flex justify-center items-center h-full">
        <div className="animate-spin rounded-full h-12 w-12 border-t-2 border-b-2 border-blue-500"></div>
      </div>
    );
  }

  return (
    <div className={`p-10 animate-in fade-in slide-in-from-bottom-4 duration-500 ${loading ? 'opacity-70' : ''}`}>
      <div className="mb-8">
        <h1 className="text-3xl font-extrabold text-white tracking-tight">Resumen de facturación</h1>
        <p className="text-slate-400 mt-2 text-lg font-medium">Consumo por cuenta, mes a mes</p>
      </div>

      <div className="mb-8">
        <MonthRangeBar value={range} onChange={setRange} onExport={exportRange} exporting={exporting} variant="dark" />
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-8">
        <div className="glass-panel p-6 rounded-2xl border border-blue-500/20 bg-blue-500/5">
          <div className="flex items-center justify-between">
            <p className="text-blue-400 text-sm font-bold uppercase tracking-wider">Documentos emitidos</p>
            <FileText className="w-6 h-6 text-blue-400" />
          </div>
          <p className="text-5xl font-black text-white mt-4">{data.totalDocumentsEmitted}</p>
        </div>
        <div className="glass-panel p-6 rounded-2xl border border-rose-500/20 bg-rose-500/5">
          <div className="flex items-center justify-between">
            <p className="text-rose-400 text-sm font-bold uppercase tracking-wider">Documentos rechazados</p>
            <XCircle className="w-6 h-6 text-rose-400" />
          </div>
          <p className="text-5xl font-black text-white mt-4">{data.totalDocumentsRejected}</p>
        </div>
        <div className="glass-panel p-6 rounded-2xl border border-indigo-500/20 bg-indigo-500/5">
          <div className="flex items-center justify-between">
            <p className="text-indigo-400 text-sm font-bold uppercase tracking-wider">Cuentas por cobrar</p>
            <TrendingUp className="w-6 h-6 text-indigo-400" />
          </div>
          <p className="text-5xl font-black text-indigo-400 mt-4">{money(data.totalAmountDue)}</p>
        </div>
      </div>

      {data.months.length > 1 && (
        <div className="glass-panel rounded-2xl border border-slate-700/50 overflow-hidden mb-8">
          <div className="p-4 border-b border-slate-700/50 text-sm font-bold text-slate-200">Mes a mes</div>
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead>
                <tr className="text-xs uppercase tracking-wider text-slate-400 border-b border-slate-700/50">
                  <th className="p-3 font-bold">Mes</th>
                  <th className="p-3 font-bold text-right">Emitidas</th>
                  <th className="p-3 font-bold text-right">Rechazadas</th>
                  <th className="p-3 font-bold text-right">A cobrar</th>
                </tr>
              </thead>
              <tbody>
                {data.months.map(x => (
                  <tr key={`${x.year}-${x.month}`} className="border-b border-slate-800 text-slate-300">
                    <td className="p-3 capitalize">{monthLabel(x.year, x.month)}</td>
                    <td className="p-3 text-right">{x.documentsEmitted}</td>
                    <td className="p-3 text-right">{x.documentsRejected}</td>
                    <td className="p-3 text-right font-mono">{money(x.amountDue)}</td>
                  </tr>
                ))}
                <tr className="font-bold text-white">
                  <td className="p-3">Total</td>
                  <td className="p-3 text-right">{data.totalDocumentsEmitted}</td>
                  <td className="p-3 text-right">{data.totalDocumentsRejected}</td>
                  <td className="p-3 text-right font-mono">{money(data.totalAmountDue)}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      )}

      <h2 className="text-xl font-bold text-white mb-4 flex items-center">
        <Users className="w-5 h-5 mr-2 text-slate-400" />
        Desglose por Tenant
      </h2>
      <DataTable
        theme="dark"
        rows={data.tenants}
        rowKey={t => t.tenantId}
        tableFrom="md"
        search="Buscar tenant..."
        defaultSort={{ key: 'tenant', dir: 'asc' }}
        emptyMessage="No hay consumo registrado en este período."
        columns={breakdownColumns}
      />
    </div>
  );
};
