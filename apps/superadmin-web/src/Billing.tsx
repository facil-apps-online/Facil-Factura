import React, { useEffect, useState } from 'react';
import { Play, FileText, CheckCircle, Clock } from 'lucide-react';
import { toast } from 'sonner';
import { api } from './api';

interface Invoice {
  id: string;
  tenantName: string;
  month: number;
  year: number;
  totalDocuments: number;
  totalAmount: number;
  currency: string;
  status: string;
  createdAt: string;
}

export const Billing = () => {
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const [calculating, setCalculating] = useState(false);

  const loadInvoices = () => {
    api.get<Invoice[]>('/billing/invoices')
      .then(res => setInvoices(res.data))
      .catch(() => toast.error("Error al cargar los recibos de cobro"));
  };

  useEffect(() => {
    loadInvoices();
  }, []);

  const handleCalculate = async () => {
    const today = new Date();
    setCalculating(true);
    try {
      const res = await api.post(`/billing/calculate/${today.getFullYear()}/${today.getMonth() + 1}`);
      toast.success(res.data.message || "Corte generado exitosamente");
      loadInvoices();
    } catch (err: any) {
      toast.error(err.response?.data || "Ocurrió un error al generar el corte.");
    } finally {
      setCalculating(false);
    }
  };

  const handleMarkAsPaid = async (id: string) => {
    try {
      await api.post(`/billing/invoices/${id}/pay`);
      toast.success("Recibo marcado como PAGADO");
      loadInvoices();
    } catch (err: any) {
      toast.error("Error al actualizar el estado");
    }
  };

  const getMonthName = (month: number) => {
    const months = ['Ene', 'Feb', 'Mar', 'Abr', 'May', 'Jun', 'Jul', 'Ago', 'Sep', 'Oct', 'Nov', 'Dic'];
    return months[month - 1] || month;
  };

  return (
    <div className="p-10 animate-in fade-in slide-in-from-bottom-4 duration-500">
      <div className="flex justify-between items-center mb-10">
        <div>
          <h1 className="text-3xl font-extrabold text-white tracking-tight">Cortes de Facturación</h1>
          <p className="text-slate-400 mt-2 text-lg font-medium">Revisa y liquida los consumos de cada Tenant.</p>
        </div>
        <button 
          onClick={handleCalculate}
          disabled={calculating}
          className="bg-slate-900 hover:bg-slate-800 text-white px-6 py-3 rounded-2xl font-bold flex items-center shadow-lg shadow-slate-900/20 transition-all transform hover:-translate-y-1 disabled:opacity-50"
        >
          <Play className="w-5 h-5 mr-2" />
          {calculating ? 'Calculando...' : 'Generar Corte (Mes Actual)'}
        </button>
      </div>

      <div className="glass-panel rounded-3xl overflow-hidden mt-8">
        <table className="w-full text-left">
          <thead className="bg-slate-900/50 border-b border-slate-700/50">
            <tr>
              <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Período</th>
              <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Tenant (Cliente)</th>
              <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Documentos Empleados</th>
              <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Total a Cobrar</th>
              <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Estado</th>
              <th className="px-6 py-4 text-right">Acciones</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-800/50">
            {invoices.map(inv => (
              <tr key={inv.id} className="hover:bg-slate-800/30 transition-colors">
                <td className="px-6 py-4">
                  <span className="font-bold text-white">{getMonthName(inv.month)} {inv.year}</span>
                </td>
                <td className="px-6 py-4 font-bold text-slate-300">
                  {inv.tenantName}
                </td>
                <td className="px-6 py-4 text-slate-400">
                  <div className="flex items-center">
                    <FileText className="w-4 h-4 mr-2 text-slate-400" /> {inv.totalDocuments}
                  </div>
                </td>
                <td className="px-6 py-4 font-bold text-emerald-600">
                  $ {inv.totalAmount.toLocaleString('es-CO')} <span className="text-xs text-slate-400">{inv.currency}</span>
                </td>
                <td className="px-6 py-4">
                  {inv.status === 'PAID' ? (
                    <span className="inline-flex items-center px-3 py-1 rounded-full text-xs font-bold bg-emerald-100 text-emerald-700">
                      <CheckCircle className="w-3 h-3 mr-1" /> Pagado
                    </span>
                  ) : (
                    <span className="inline-flex items-center px-3 py-1 rounded-full text-xs font-bold bg-amber-100 text-amber-700">
                      <Clock className="w-3 h-3 mr-1" /> Pendiente
                    </span>
                  )}
                </td>
                <td className="px-6 py-4 text-right">
                  {inv.status !== 'PAID' && (
                    <button 
                      onClick={() => handleMarkAsPaid(inv.id)}
                      className="text-indigo-600 font-bold hover:text-indigo-800 text-sm"
                    >
                      Marcar Pagado
                    </button>
                  )}
                </td>
              </tr>
            ))}
            {invoices.length === 0 && (
              <tr>
                <td colSpan={6} className="px-6 py-12 text-center text-slate-500 font-medium">
                  No hay cortes generados. Ejecuta el proceso para liquidar el mes actual.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
};
