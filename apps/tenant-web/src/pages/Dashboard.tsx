import React, { useState, useEffect } from 'react';
import { CheckCircle, XCircle, Clock, Loader2, TrendingDown, TrendingUp, Users } from 'lucide-react';
import { api, getErrorMessage } from '../lib/api';
import { toast } from 'sonner';
import DataTable, { type DataTableColumn } from '@shared/components/DataTable';
import MonthRangeBar, { currentMonthRange, downloadBlob, type MonthRangeValue } from '@shared/components/MonthRangeBar';

interface RangeMonth {
  year: number;
  month: number;
  documentsEmitted: number;
  documentsRejected: number;
  amountDueToPlatform: number;
  amountDueFromClients: number;
}

interface ClientConsumption {
  name: string;
  documentsEmitted: number;
  documentsRejected: number;
  amountDueToTenant: number;
  rowId?: string;
}

interface BillingRange {
  from: string;
  to: string;
  platformBillingHidden: boolean;
  months: RangeMonth[];
  totalDocumentsEmitted: number;
  totalDocumentsRejected: number;
  totalAmountDueToPlatform: number;
  totalAmountDueFromClients: number;
  clients: ClientConsumption[];
}

const MONTH_NAMES = ['enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio', 'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre'];
const monthLabel = (year: number, month: number) => `${MONTH_NAMES[month - 1]} ${year}`;

const money = (value: number) => `$${value.toLocaleString('es-CO')}`;

const consumptionColumns: DataTableColumn<ClientConsumption>[] = [
  { key: 'client', header: 'Cliente', primary: true, cellClassName: 'font-bold text-slate-700', render: c => c.name, sortValue: c => c.name, searchValue: c => c.name },
  {
    key: 'documents',
    header: 'Emitidas',
    align: 'right',
    render: c => <span className="bg-blue-50 text-blue-700 px-3 py-1 rounded-full font-medium">{c.documentsEmitted}</span>,
    sortValue: c => c.documentsEmitted,
  },
  {
    key: 'rejected',
    header: 'Rechazadas',
    align: 'right',
    render: c => <span className={c.documentsRejected > 0 ? 'bg-rose-50 text-rose-700 px-3 py-1 rounded-full font-medium' : 'text-slate-400 px-3 py-1'}>{c.documentsRejected}</span>,
    sortValue: c => c.documentsRejected,
  },
  { key: 'subtotal', header: 'Valor a cobrar', align: 'right', cellClassName: 'font-bold text-emerald-600', render: c => money(c.amountDueToTenant), sortValue: c => c.amountDueToTenant },
];

export default function Dashboard() {
  const [metrics, setMetrics] = useState({ totalIssued: 0, totalApproved: 0, totalRejected: 0, totalProcessing: 0 });
  const [recentDocs, setRecentDocs] = useState<any[]>([]);
  const [billingMetrics, setBillingMetrics] = useState<BillingRange | null>(null);
  const [range, setRange] = useState<MonthRangeValue>(currentMonthRange());
  const [loadingRange, setLoadingRange] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Promise.all([
      api.get('/tenant/dashboard/metrics'),
      api.get('/tenant/dashboard/recent-documents'),
    ]).then(([resMetrics, resDocs]) => {
      setMetrics(resMetrics.data);
      setRecentDocs(resDocs.data);
    }).catch(err => {
      toast.error("Error al conectar con la bóveda de datos");
    }).finally(() => {
      setLoading(false);
    });
  }, []);

  // El consumo y el cobro se calculan mes a mes: un rango es la suma de sus meses.
  useEffect(() => {
    setLoadingRange(true);
    api.get<BillingRange>(`/tenant/dashboard/billing-range?from=${range.from}&to=${range.to}`)
      .then(res => setBillingMetrics(res.data))
      .catch(err => {
        toast.error(getErrorMessage(err, 'No se pudo cargar el consumo del período'));
        // Si falla la primera carga, la pantalla se dibuja igual, con ceros.
        setBillingMetrics(prev => prev ?? { from: range.from, to: range.to, platformBillingHidden: false, months: [], totalDocumentsEmitted: 0, totalDocumentsRejected: 0, totalAmountDueToPlatform: 0, totalAmountDueFromClients: 0, clients: [] });
      })
      .finally(() => setLoadingRange(false));
  }, [range.from, range.to]);

  const exportRange = async () => {
    setExporting(true);
    try {
      await downloadBlob(api, `/tenant/dashboard/billing-range/export?from=${range.from}&to=${range.to}`, `consumo_${range.from}_a_${range.to}.xlsx`);
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo exportar'));
    } finally {
      setExporting(false);
    }
  };

  const stats = [
    { title: 'Facturas Emitidas', value: metrics.totalIssued, icon: <CheckCircle size={24} className="text-emerald-500" />, trend: 'Aprobadas por la DIAN · acumulado histórico' },
    { title: 'Rechazadas DIAN', value: metrics.totalRejected, icon: <XCircle size={24} className="text-rose-500" />, trend: 'Revisión requerida' },
    { title: 'En Proceso', value: metrics.totalProcessing, icon: <Clock size={24} className="text-amber-500" />, trend: 'Encoladas / Radian' },
  ];

  if (loading || !billingMetrics) {
    return (
      <div className="flex h-full w-full items-center justify-center p-8">
        <Loader2 className="animate-spin text-primary w-10 h-10" />
      </div>
    );
  }

  return (
    <div className="p-10 h-full overflow-y-auto animate-in fade-in slide-in-from-bottom-4 duration-500">
      <div className="mb-10">
        <h1 className="text-3xl font-bold text-slate-800">Resumen</h1>
        <p className="text-slate-500 mt-2">Consulta tus documentos y el estado de tu facturación.</p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-8 mb-8">
        {stats.map((stat, i) => (
          <div key={i} className="bg-white/80 backdrop-blur-md rounded-[1.5rem] p-6 shadow-sm border border-slate-200/50 flex flex-col hover:shadow-xl hover:shadow-primary/5 hover:-translate-y-1 transition-all duration-300">
            <div className="flex items-center justify-between mb-4">
              <div className="w-12 h-12 rounded-xl bg-slate-50 flex items-center justify-center">
                {stat.icon}
              </div>
            </div>
            <h3 className="text-slate-500 text-sm font-medium">{stat.title}</h3>
            <p className="text-3xl font-bold text-slate-800 mt-1">{stat.value}</p>
            <p className="text-xs text-slate-400 mt-4 font-medium">{stat.trend}</p>
          </div>
        ))}
      </div>

      <h2 className="text-2xl font-bold text-slate-800 mb-4 mt-12 border-b pb-2">Consumo y cobro por período</h2>
      <div className="mb-6">
        <MonthRangeBar value={range} onChange={setRange} onExport={exportRange} exporting={exporting} />
      </div>
      
      <div className={`grid grid-cols-1 ${billingMetrics.platformBillingHidden ? "" : "md:grid-cols-2 "}gap-8 mb-8`}>
        <div className="bg-emerald-50 rounded-3xl p-8 border border-emerald-100 shadow-sm relative overflow-hidden group hover:shadow-md transition-shadow">
          <div className="absolute -right-6 -top-6 text-emerald-500/10 group-hover:scale-110 transition-transform">
            <TrendingUp size={120} />
          </div>
          <div className="relative z-10">
            <h3 className="text-emerald-800 font-bold uppercase tracking-wider text-sm mb-2">Ingresos esperados</h3>
            <p className="text-4xl font-black text-emerald-600 mb-2">${billingMetrics.totalAmountDueFromClients.toLocaleString('es-CO')}</p>
            <p className="text-emerald-700 text-sm font-medium">Total según el consumo de tus clientes en el período.</p>
          </div>
        </div>

        {/* Un aliado solo ve lo que debe a la plataforma si su tenant padre se lo activó. */}
        {!billingMetrics.platformBillingHidden && <div className="bg-rose-50 rounded-3xl p-8 border border-rose-100 shadow-sm relative overflow-hidden group hover:shadow-md transition-shadow">
          <div className="absolute -right-6 -top-6 text-rose-500/10 group-hover:scale-110 transition-transform">
            <TrendingDown size={120} />
          </div>
          <div className="relative z-10">
            <h3 className="text-rose-800 font-bold uppercase tracking-wider text-sm mb-2">Pago a la plataforma</h3>
            <p className="text-4xl font-black text-rose-600 mb-2">${billingMetrics.totalAmountDueToPlatform.toLocaleString('es-CO')}</p>
            <p className="text-rose-700 text-sm font-medium">Documentos emitidos en el período: <span className="font-bold">{billingMetrics.totalDocumentsEmitted}</span></p>
          </div>
        </div>}
      </div>

      {billingMetrics.months.length > 1 && (
        <div className="mb-8 bg-white rounded-2xl shadow-sm border border-slate-100 overflow-hidden">
          <div className="p-4 border-b border-slate-100 bg-slate-50 text-sm font-bold text-slate-700">Mes a mes</div>
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse text-sm">
              <thead>
                <tr className="border-b border-slate-200 text-xs text-slate-500 uppercase tracking-wider">
                  <th className="p-3 font-bold">Mes</th>
                  <th className="p-3 font-bold text-right">Emitidas</th>
                  <th className="p-3 font-bold text-right">Rechazadas</th>
                  {!billingMetrics.platformBillingHidden && <th className="p-3 font-bold text-right">A pagar a la plataforma</th>}
                  <th className="p-3 font-bold text-right">A cobrar a clientes</th>
                </tr>
              </thead>
              <tbody>
                {billingMetrics.months.map(x => (
                  <tr key={`${x.year}-${x.month}`} className="border-b border-slate-100">
                    <td className="p-3 font-medium text-slate-700 capitalize">{monthLabel(x.year, x.month)}</td>
                    <td className="p-3 text-right">{x.documentsEmitted}</td>
                    <td className="p-3 text-right">{x.documentsRejected}</td>
                    {!billingMetrics.platformBillingHidden && <td className="p-3 text-right font-mono">{money(x.amountDueToPlatform)}</td>}
                    <td className="p-3 text-right font-mono">{money(x.amountDueFromClients)}</td>
                  </tr>
                ))}
                <tr className="font-bold bg-slate-50">
                  <td className="p-3">Total</td>
                  <td className="p-3 text-right">{billingMetrics.totalDocumentsEmitted}</td>
                  <td className="p-3 text-right">{billingMetrics.totalDocumentsRejected}</td>
                  {!billingMetrics.platformBillingHidden && <td className="p-3 text-right font-mono">{money(billingMetrics.totalAmountDueToPlatform)}</td>}
                  <td className="p-3 text-right font-mono">{money(billingMetrics.totalAmountDueFromClients)}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* La lista de consumo por cliente siempre se ve; sus valores en dinero siguen la configuración del cobro. */}
      <div className={`mb-12 ${loadingRange ? 'opacity-60' : ''}`}>
        <div className="flex items-center mb-4">
          <Users className="w-5 h-5 text-slate-500 mr-2" />
          <h2 className="text-lg font-bold text-slate-800">Consumo por cliente</h2>
        </div>
        <DataTable
          rows={billingMetrics.clients.map((c, i) => ({ ...c, rowId: String(i) }))}
          rowKey={c => c.rowId ?? c.name}
          tableFrom="md"
          search="Buscar cliente..."
          defaultSort={{ key: 'client', dir: 'asc' }}
          emptyMessage="No hay clientes con documentos en este período."
          columns={billingMetrics.platformBillingHidden ? consumptionColumns.filter(c => c.key !== 'subtotal') : consumptionColumns}
        />
      </div>

      <div className="bg-white rounded-2xl shadow-sm border border-slate-100 overflow-hidden">
        <div className="p-6 border-b border-slate-100 bg-slate-50">
          <h2 className="text-lg font-bold text-slate-800">Últimos documentos enviados</h2>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse">
            <thead>
              <tr className="border-b border-slate-200 bg-slate-50 text-xs text-slate-500 uppercase tracking-wider">
                <th className="p-4 font-bold">Documento</th>
                <th className="p-4 font-bold">Fecha</th>
                <th className="p-4 font-bold">Cliente Emisor</th>
                <th className="p-4 font-bold">Estado DIAN</th>
              </tr>
            </thead>
            <tbody className="text-sm">
              {recentDocs.length === 0 && (
                <tr>
                  <td colSpan={4} className="py-8 text-center text-slate-500 font-medium">
                    No hay documentos procesados recientemente.
                  </td>
                </tr>
              )}
              {recentDocs.map((doc, i) => {
                let color = "text-slate-600 bg-slate-50";
                if (doc.status === "APPROVED") color = 'text-emerald-600 bg-emerald-50 border border-emerald-200';
                if (doc.status === "REJECTED") color = 'text-rose-600 bg-rose-50 border border-rose-200';
                if (doc.status === "PENDING" || doc.status === "PROCESSING") color = 'text-amber-600 bg-amber-50 border border-amber-200';
                
                return (
                <tr key={i} className="border-b border-slate-100 hover:bg-slate-50 transition-colors group">
                  <td className="p-4 font-medium text-slate-700">{doc.id}</td>
                  <td className="p-4 text-slate-500">{doc.date}</td>
                  <td className="p-4 text-slate-600 font-medium group-hover:text-primary transition-colors">{doc.client}</td>
                  <td className="p-4">
                    <span className={`px-3 py-1.5 rounded-full text-[11px] uppercase tracking-wider font-bold shadow-sm ${color}`}>
                      {doc.status}
                    </span>
                  </td>
                </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
