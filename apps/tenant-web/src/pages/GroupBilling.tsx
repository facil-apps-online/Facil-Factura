import React, { useState, useEffect } from 'react';
import { Building2, Loader2, Layers } from 'lucide-react';
import { api } from '../lib/api';
import { toast } from 'sonner';

interface TenantBillingRow {
  year: number;
  month: number;
  totalAmount: number;
  currency: string;
  status: string;
  paidAt: string | null;
}

interface GroupTenant {
  tenantId: string;
  tenantName: string;
  isSelf: boolean;
  totalAmount: number;
  billings: TenantBillingRow[];
}

interface GroupBillingData {
  isGroup: boolean;
  totalAmount: number;
  currency: string;
  byTenant: GroupTenant[];
}

const formatCurrency = (value: number, currency: string) =>
  new Intl.NumberFormat('es-CO', { style: 'currency', currency, maximumFractionDigits: 0 }).format(value);

const MONTH_NAMES = ['Ene', 'Feb', 'Mar', 'Abr', 'May', 'Jun', 'Jul', 'Ago', 'Sep', 'Oct', 'Nov', 'Dic'];

export default function GroupBilling() {
  const [data, setData] = useState<GroupBillingData | null>(null);
  const [loading, setLoading] = useState(true);
  const [year, setYear] = useState<number>(new Date().getFullYear());

  useEffect(() => {
    setLoading(true);
    api.get(`/tenant/dashboard/group-billing?year=${year}`)
      .then(res => setData(res.data))
      .catch(() => toast.error('Error al cargar la facturación del grupo'))
      .finally(() => setLoading(false));
  }, [year]);

  if (loading || !data) {
    return (
      <div className="flex h-full w-full items-center justify-center p-8">
        <Loader2 className="animate-spin text-primary w-10 h-10" />
      </div>
    );
  }

  if (!data.isGroup) {
    return (
      <div className="p-8">
        <h1 className="text-2xl font-bold text-slate-900 mb-2">Facturación de mi grupo</h1>
        <div className="bg-white rounded-2xl border border-slate-200 p-8 text-center mt-6">
          <Layers className="mx-auto mb-3 text-slate-300" size={40} />
          <p className="text-slate-600 font-medium">Este tenant no tiene otros tenants asociados todavía.</p>
          <p className="text-slate-400 text-sm mt-1">Cuando otro tenant se registre bajo tu grupo empresarial, aquí verás el consolidado de lo que se les ha emitido.</p>
        </div>
      </div>
    );
  }

  return (
    <div className="p-8">
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-slate-900">Facturación de mi grupo</h1>
          <p className="text-slate-500 text-sm mt-1">Total emitido a tu tenant y a los tenants asociados a tu grupo empresarial.</p>
        </div>
        <select
          value={year}
          onChange={e => setYear(Number(e.target.value))}
          className="px-4 py-2 bg-white border border-slate-200 rounded-xl text-slate-700 font-medium"
        >
          {[0, 1, 2].map(offset => {
            const y = new Date().getFullYear() - offset;
            return <option key={y} value={y}>{y}</option>;
          })}
        </select>
      </div>

      <div className="bg-primary text-white rounded-2xl p-6 mb-8 shadow-lg shadow-primary/20">
        <p className="text-sm opacity-80 mb-1">Total emitido al grupo en {year}</p>
        <p className="text-4xl font-extrabold">{formatCurrency(data.totalAmount, data.currency)}</p>
      </div>

      <div className="space-y-6">
        {data.byTenant.map(t => (
          <div key={t.tenantId} className="bg-white rounded-2xl border border-slate-200 overflow-hidden">
            <div className="flex items-center justify-between px-6 py-4 border-b border-slate-100">
              <div className="flex items-center gap-3">
                <div className="bg-slate-100 p-2 rounded-lg"><Building2 size={18} className="text-slate-600" /></div>
                <div>
                  <p className="font-semibold text-slate-900">{t.tenantName}{t.isSelf && <span className="ml-2 text-xs font-normal text-slate-400">(este tenant)</span>}</p>
                  <p className="text-xs text-slate-400">{t.billings.length} corte(s) en {year}</p>
                </div>
              </div>
              <p className="text-lg font-bold text-slate-900">{formatCurrency(t.totalAmount, data.currency)}</p>
            </div>

            {t.billings.length > 0 && (
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-left text-slate-400 text-xs uppercase">
                    <th className="px-6 py-2 font-medium">Periodo</th>
                    <th className="px-6 py-2 font-medium">Monto</th>
                    <th className="px-6 py-2 font-medium">Estado</th>
                  </tr>
                </thead>
                <tbody>
                  {t.billings.map((b, i) => (
                    <tr key={i} className="border-t border-slate-50">
                      <td className="px-6 py-2 text-slate-700">{MONTH_NAMES[b.month - 1]} {b.year}</td>
                      <td className="px-6 py-2 text-slate-700">{formatCurrency(b.totalAmount, b.currency)}</td>
                      <td className="px-6 py-2">
                        <span className={`px-2 py-0.5 rounded-full text-xs font-medium ${
                          b.status === 'Paid' ? 'bg-emerald-50 text-emerald-600' :
                          b.status === 'Overdue' ? 'bg-rose-50 text-rose-600' : 'bg-amber-50 text-amber-600'
                        }`}>{b.status}</span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}
