import React, { useState, useEffect } from 'react';
import { FileText, CheckCircle, XCircle, Clock, Loader2, TrendingDown, TrendingUp, Users } from 'lucide-react';
import { api } from '../lib/api';
import { toast } from 'sonner';

interface TenantBillingMetrics {
  year: number;
  month: number;
  totalDocuments: number;
  amountDueToSuperadmin: number;
  superadminTariffApplied: number;
  amountDueFromClients: number;
  clientBreakdown: any[];
}

export default function Dashboard() {
  const [metrics, setMetrics] = useState({ totalIssued: 0, totalApproved: 0, totalRejected: 0, totalProcessing: 0 });
  const [recentDocs, setRecentDocs] = useState<any[]>([]);
  const [billingMetrics, setBillingMetrics] = useState<TenantBillingMetrics | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const today = new Date();
    Promise.all([
      api.get('/tenant/dashboard/metrics'),
      api.get('/tenant/dashboard/recent-documents'),
      api.get(`/tenant/dashboard/billing-metrics?year=${today.getFullYear()}&month=${today.getMonth() + 1}`)
    ]).then(([resMetrics, resDocs, resBilling]) => {
      setMetrics(resMetrics.data);
      setRecentDocs(resDocs.data);
      setBillingMetrics(resBilling.data);
    }).catch(err => {
      toast.error("Error al conectar con la bóveda de datos");
    }).finally(() => {
      setLoading(false);
    });
  }, []);

  const stats = [
    { title: 'Facturas Emitidas', value: metrics.totalIssued, icon: <FileText size={24} className="text-blue-500" />, trend: 'Acumulado histórico' },
    { title: 'Aprobadas DIAN', value: metrics.totalApproved, icon: <CheckCircle size={24} className="text-emerald-500" />, trend: 'Certificadas' },
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
        <h1 className="text-3xl font-bold text-slate-800">Dashboard Analítico</h1>
        <p className="text-slate-500 mt-2">Monitorea el estado de tus comprobantes electrónicos y cortes de facturación en tiempo real.</p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-8 mb-8">
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

      <h2 className="text-2xl font-bold text-slate-800 mb-4 mt-12 border-b pb-2">Resumen Financiero (Mes Actual)</h2>
      
      <div className="grid grid-cols-1 md:grid-cols-2 gap-8 mb-8">
        <div className="bg-emerald-50 rounded-3xl p-8 border border-emerald-100 shadow-sm relative overflow-hidden group hover:shadow-md transition-shadow">
          <div className="absolute -right-6 -top-6 text-emerald-500/10 group-hover:scale-110 transition-transform">
            <TrendingUp size={120} />
          </div>
          <div className="relative z-10">
            <h3 className="text-emerald-800 font-bold uppercase tracking-wider text-sm mb-2">Ingresos Esperados (Cuentas por Cobrar)</h3>
            <p className="text-4xl font-black text-emerald-600 mb-2">${billingMetrics.amountDueFromClients.toLocaleString('es-CO')}</p>
            <p className="text-emerald-700 text-sm font-medium">Suma del consumo de tus clientes basado en sus tarifas individuales.</p>
          </div>
        </div>

        <div className="bg-rose-50 rounded-3xl p-8 border border-rose-100 shadow-sm relative overflow-hidden group hover:shadow-md transition-shadow">
          <div className="absolute -right-6 -top-6 text-rose-500/10 group-hover:scale-110 transition-transform">
            <TrendingDown size={120} />
          </div>
          <div className="relative z-10">
            <h3 className="text-rose-800 font-bold uppercase tracking-wider text-sm mb-2">Deuda a Plataforma (Cuentas por Pagar)</h3>
            <p className="text-4xl font-black text-rose-600 mb-2">${billingMetrics.amountDueToSuperadmin.toLocaleString('es-CO')}</p>
            <p className="text-rose-700 text-sm font-medium">Volumen del mes: <span className="font-bold">{billingMetrics.totalDocuments}</span> docs x Tarifa Nivel Aplicada: <span className="font-bold">${billingMetrics.superadminTariffApplied.toLocaleString('es-CO')}</span></p>
          </div>
        </div>
      </div>

      <div className="bg-white rounded-2xl shadow-sm border border-slate-100 overflow-hidden mb-12">
        <div className="p-6 border-b border-slate-100 flex items-center bg-slate-50">
          <Users className="w-5 h-5 text-slate-500 mr-2" />
          <h2 className="text-lg font-bold text-slate-800">Desglose de Consumo por Cliente</h2>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse">
            <thead>
              <tr className="border-b border-slate-200 bg-slate-50 text-xs text-slate-500 uppercase tracking-wider">
                <th className="p-4 font-bold">Cliente</th>
                <th className="p-4 font-bold text-center">Docs. Emitidos</th>
                <th className="p-4 font-bold text-right">Tarifa Configurada</th>
                <th className="p-4 font-bold text-right">Subtotal Adeudado</th>
              </tr>
            </thead>
            <tbody className="text-sm">
              {billingMetrics.clientBreakdown.length === 0 && (
                <tr>
                  <td colSpan={4} className="py-8 text-center text-slate-500 font-medium">
                    No hay clientes emitiendo documentos este mes.
                  </td>
                </tr>
              )}
              {billingMetrics.clientBreakdown.map((cb, i) => (
                <tr key={i} className="border-b border-slate-100 hover:bg-slate-50 transition-colors">
                  <td className="p-4 font-bold text-slate-700">{cb.clientName}</td>
                  <td className="p-4 text-center text-slate-600 font-medium">
                    <span className="bg-blue-50 text-blue-700 px-3 py-1 rounded-full">{cb.documentsEmitted}</span>
                  </td>
                  <td className="p-4 text-right text-slate-500 font-mono">${cb.priceApplied.toLocaleString('es-CO')}</td>
                  <td className="p-4 text-right font-bold text-emerald-600 text-base">
                    ${cb.amountDueToTenant.toLocaleString('es-CO')}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      <div className="bg-white rounded-2xl shadow-sm border border-slate-100 overflow-hidden">
        <div className="p-6 border-b border-slate-100 bg-slate-50">
          <h2 className="text-lg font-bold text-slate-800">Últimos Documentos Enviados</h2>
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
