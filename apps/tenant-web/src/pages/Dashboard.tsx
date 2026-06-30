import React, { useState, useEffect } from 'react';
import { FileText, CheckCircle, XCircle, Clock, Loader2 } from 'lucide-react';
import { api } from '../lib/api';
import { toast } from 'sonner';

export default function Dashboard() {
  const [metrics, setMetrics] = useState({ totalIssued: 0, totalApproved: 0, totalRejected: 0, totalProcessing: 0 });
  const [recentDocs, setRecentDocs] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Promise.all([
      api.get('/tenant/dashboard/metrics'),
      api.get('/tenant/dashboard/recent-documents')
    ]).then(([resMetrics, resDocs]) => {
      setMetrics(resMetrics.data);
      setRecentDocs(resDocs.data);
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

  if (loading) {
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
        <p className="text-slate-500 mt-2">Monitorea el estado de tus comprobantes electrónicos en tiempo real.</p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-8">
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

      <div className="mt-8 bg-white rounded-2xl p-6 shadow-sm border border-slate-100">
        <h2 className="text-lg font-bold text-slate-800 mb-6">Últimos Documentos Enviados</h2>
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse">
            <thead>
              <tr className="border-b border-slate-100 text-sm text-slate-500">
                <th className="pb-4 font-medium">Documento</th>
                <th className="pb-4 font-medium">Fecha</th>
                <th className="pb-4 font-medium">Cliente/Receptor</th>
                <th className="pb-4 font-medium">Total</th>
                <th className="pb-4 font-medium">Estado DIAN</th>
              </tr>
            </thead>
            <tbody className="text-sm">
              {recentDocs.length === 0 && (
                <tr>
                  <td colSpan={5} className="py-8 text-center text-slate-500">
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
                <tr key={i} className="border-b border-slate-50 hover:bg-primary/5 transition-colors group">
                  <td className="py-4 px-2 font-medium text-slate-700">{doc.id}</td>
                  <td className="py-4 text-slate-500">{doc.date}</td>
                  <td className="py-4 text-slate-600 font-medium group-hover:text-primary transition-colors">{doc.client}</td>
                  <td className="py-4 font-semibold text-slate-700">{doc.total}</td>
                  <td className="py-4">
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
