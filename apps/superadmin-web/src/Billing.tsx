import React, { useEffect, useState } from 'react';
import { Play, FileText, CheckCircle, Clock, TrendingUp, Users } from 'lucide-react';
import { toast } from 'sonner';
import { api } from './api';
import DataTable, { type DataTableColumn } from '@shared/components/DataTable';

interface TenantUsageBreakdown {
  tenantId: string;
  tenantName: string;
  documentsEmitted: number;
  tariffApplied: number;
  amountDueToSuperadmin: number;
}

interface SuperadminBillingMetrics {
  year: number;
  month: number;
  totalDocuments: number;
  totalAmountDueFromTenants: number;
  tenantBreakdown: TenantUsageBreakdown[];
}

const breakdownColumns: DataTableColumn<TenantUsageBreakdown>[] = [
  { key: 'tenant', header: 'Tenant', primary: true, cellClassName: 'font-bold text-slate-200 text-lg', render: t => t.tenantName, sortValue: t => t.tenantName, searchValue: t => t.tenantName },
  {
    key: 'documents',
    header: 'Documentos',
    align: 'right',
    render: t => (
      <div className="flex items-center justify-end text-slate-300 font-medium">
        <FileText className="w-4 h-4 mr-2 text-slate-500" /> {t.documentsEmitted} docs
      </div>
    ),
    sortValue: t => t.documentsEmitted,
  },
  { key: 'tariff', header: 'Tarifa aplicada', align: 'right', cellClassName: 'text-slate-400 font-mono', render: t => `$${t.tariffApplied.toLocaleString('es-CO')} c/u`, sortValue: t => t.tariffApplied },
  { key: 'subtotal', header: 'Subtotal', align: 'right', cellClassName: 'font-bold text-indigo-400 text-lg', render: t => `$ ${t.amountDueToSuperadmin.toLocaleString('es-CO')}`, sortValue: t => t.amountDueToSuperadmin },
];

export const Billing = () => {
  const [metrics, setMetrics] = useState<SuperadminBillingMetrics | null>(null);
  const [loading, setLoading] = useState(false);

  const loadMetrics = () => {
    const today = new Date();
    setLoading(true);
    api.get<SuperadminBillingMetrics>(`/dashboard/billing-metrics?year=${today.getFullYear()}&month=${today.getMonth() + 1}`)
      .then(res => setMetrics(res.data))
      .catch(() => toast.error("Error al cargar las métricas de facturación"))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    loadMetrics();
  }, []);

  if (loading || !metrics) {
    return (
      <div className="p-10 flex justify-center items-center h-full">
        <div className="animate-spin rounded-full h-12 w-12 border-t-2 border-b-2 border-blue-500"></div>
      </div>
    );
  }

  return (
    <div className="p-10 animate-in fade-in slide-in-from-bottom-4 duration-500">
      <div className="flex justify-between items-center mb-10">
        <div>
          <h1 className="text-3xl font-extrabold text-white tracking-tight">Resumen de facturación</h1>
          <p className="text-slate-400 mt-2 text-lg font-medium">Consumo por cuenta durante el mes actual</p>
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6 mb-8">
        <div className="glass-panel p-6 rounded-2xl border border-blue-500/20 bg-blue-500/5">
          <div className="flex items-center justify-between">
            <p className="text-blue-400 text-sm font-bold uppercase tracking-wider">Documentos emitidos</p>
            <FileText className="w-6 h-6 text-blue-400" />
          </div>
          <p className="text-5xl font-black text-white mt-4">{metrics.totalDocuments}</p>
        </div>
        <div className="glass-panel p-6 rounded-2xl border border-indigo-500/20 bg-indigo-500/5">
          <div className="flex items-center justify-between">
            <p className="text-indigo-400 text-sm font-bold uppercase tracking-wider">Cuentas por cobrar</p>
            <TrendingUp className="w-6 h-6 text-indigo-400" />
          </div>
          <p className="text-5xl font-black text-indigo-400 mt-4">
            ${metrics.totalAmountDueFromTenants.toLocaleString('es-CO')}
          </p>
        </div>
      </div>

      <h2 className="text-xl font-bold text-white mb-4 flex items-center">
        <Users className="w-5 h-5 mr-2 text-slate-400" />
        Desglose por Tenant
      </h2>
      <DataTable
        theme="dark"
        rows={metrics.tenantBreakdown}
        rowKey={t => t.tenantId}
        tableFrom="md"
        search="Buscar tenant..."
        defaultSort={{ key: 'tenant', dir: 'asc' }}
        emptyMessage="No se ha registrado emisión de documentos este mes."
        columns={breakdownColumns}
      />
    </div>
  );
};
