import React, { useEffect, useState } from 'react';
import { Play, FileText, CheckCircle, Clock, TrendingUp, Users } from 'lucide-react';
import { toast } from 'sonner';
import { api } from './api';

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
          <h1 className="text-3xl font-extrabold text-white tracking-tight">Análisis de Facturación - Plataforma</h1>
          <p className="text-slate-400 mt-2 text-lg font-medium">Desglose en tiempo real de consumo por Tenant (Mes Actual)</p>
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6 mb-8">
        <div className="glass-panel p-6 rounded-2xl border border-blue-500/20 bg-blue-500/5">
          <div className="flex items-center justify-between">
            <p className="text-blue-400 text-sm font-bold uppercase tracking-wider">Total Documentos Emitidos</p>
            <FileText className="w-6 h-6 text-blue-400" />
          </div>
          <p className="text-5xl font-black text-white mt-4">{metrics.totalDocuments}</p>
        </div>
        <div className="glass-panel p-6 rounded-2xl border border-indigo-500/20 bg-indigo-500/5">
          <div className="flex items-center justify-between">
            <p className="text-indigo-400 text-sm font-bold uppercase tracking-wider">Cuentas por Cobrar Estimadas</p>
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
      <div className="glass-panel rounded-3xl overflow-hidden shadow-2xl">
        <table className="w-full text-left">
          <thead className="bg-slate-900/50 border-b border-slate-700/50">
            <tr>
              <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Tenant</th>
              <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Volumen Docs</th>
              <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Tarifa de Nivel Aplicada</th>
              <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Subtotal Adeudado</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-800/50">
            {metrics.tenantBreakdown.map((t, idx) => (
              <tr key={idx} className="hover:bg-slate-800/30 transition-colors">
                <td className="px-6 py-4 font-bold text-slate-200 text-lg">
                  {t.tenantName}
                </td>
                <td className="px-6 py-4">
                  <div className="flex items-center text-slate-300 font-medium">
                    <FileText className="w-4 h-4 mr-2 text-slate-500" /> {t.documentsEmitted} docs
                  </div>
                </td>
                <td className="px-6 py-4 text-slate-400 font-mono">
                  ${t.tariffApplied.toLocaleString('es-CO')} c/u
                </td>
                <td className="px-6 py-4 font-bold text-indigo-400 text-lg">
                  $ {t.amountDueToSuperadmin.toLocaleString('es-CO')}
                </td>
              </tr>
            ))}
            {metrics.tenantBreakdown.length === 0 && (
              <tr>
                <td colSpan={4} className="px-6 py-12 text-center text-slate-500 font-medium">
                  No se ha registrado emisión de documentos este mes.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
};
