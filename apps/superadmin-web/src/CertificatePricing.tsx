import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Coins, Loader2, Plus, Wallet } from 'lucide-react';
import { toast } from 'sonner';
import { api } from './api';

type Profile = { id: string; title: string; description: string; providerValidityDays: number | null };
type TenantOption = { id: string; commercialName: string; name: string };
type Price = {
  id: string; profileId: string; profileTitle: string; tenantId: string | null; tenantName: string | null;
  netAmount: number; taxRate: number; total: number; effectiveFrom: string; effectiveTo: string | null; isActive: boolean; isCurrent: boolean;
};
type BalanceRow = {
  tenantId: string; tenantName: string; isBillingExempt: boolean; certificatesOnly: boolean;
  available: number; reserved: number; consumed: number; deposited: number;
};

const money = (value: number | null | undefined) => `$${Number(value ?? 0).toLocaleString('es-CO', { maximumFractionDigits: 2 })}`;
const date = (value: string | null) => value ? new Date(value).toLocaleDateString('es-CO', { day: 'numeric', month: 'short', year: 'numeric' }) : '—';

function priceState(price: Price): { label: string; className: string } {
  if (price.isCurrent) return { label: 'Vigente', className: 'bg-emerald-500/20 text-emerald-300 border-emerald-500/30' };
  if (price.isActive && new Date(price.effectiveFrom) > new Date()) return { label: 'Programado', className: 'bg-blue-500/20 text-blue-300 border-blue-500/30' };
  return { label: 'Cerrado', className: 'bg-slate-700 text-slate-400 border-slate-600' };
}

// Precios de los certificados (por perfil de producción, generales o especiales por tenant) y saldo prepagado de cada tenant. Los abonos se
// registran en la ficha de cada tenant.
export function CertificatePricing() {
  const [profiles, setProfiles] = useState<Profile[]>([]);
  const [tenants, setTenants] = useState<TenantOption[]>([]);
  const [prices, setPrices] = useState<Price[]>([]);
  const [balances, setBalances] = useState<BalanceRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [showClosed, setShowClosed] = useState(false);
  const [form, setForm] = useState({ profileId: '', tenantId: '', netAmount: '', taxRate: '19', effectiveFrom: '' });

  const load = () => {
    setLoading(true);
    Promise.all([
      api.get<Profile[]>('/certificates/profiles?environment=Production').then(res => setProfiles(res.data)),
      api.get<Price[]>('/certificates/prices').then(res => setPrices(res.data)),
      api.get<BalanceRow[]>('/certificates/balances').then(res => setBalances(res.data)),
      api.get<TenantOption[]>('/tenants').then(res => setTenants(res.data)),
    ])
      .catch(() => toast.error('No fue posible cargar los precios y saldos de certificados.'))
      .finally(() => setLoading(false));
  };

  useEffect(load, []);

  const net = Number(form.netAmount || 0);
  const tax = Number(form.taxRate || 0);
  const previewTotal = net + Math.round(net * tax) / 100;

  const createPrice = async () => {
    if (!form.profileId || net <= 0) {
      toast.error('Elige el perfil e indica un precio mayor a cero.');
      return;
    }
    setSaving(true);
    try {
      await api.post('/certificates/prices', {
        profileId: form.profileId,
        tenantId: form.tenantId || null,
        netAmount: net,
        taxRate: tax,
        effectiveFrom: form.effectiveFrom ? new Date(`${form.effectiveFrom}T00:00:00-05:00`).toISOString() : null,
      });
      toast.success('Precio guardado. El anterior del mismo perfil y alcance quedó cerrado.');
      setForm(current => ({ ...current, netAmount: '', effectiveFrom: '' }));
      load();
    } catch (error: any) {
      toast.error(error.response?.data?.message ?? 'No fue posible guardar el precio.');
    } finally {
      setSaving(false);
    }
  };

  const closePrice = async (price: Price) => {
    try {
      await api.post(`/certificates/prices/${price.id}/close`);
      toast.success('Precio cerrado. Sin precio vigente ese perfil no se puede solicitar en producción.');
      load();
    } catch {
      toast.error('No fue posible cerrar el precio.');
    }
  };

  const visiblePrices = prices.filter(p => showClosed || p.isCurrent || (p.isActive && new Date(p.effectiveFrom) > new Date()));
  const field = 'mt-1 w-full rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 text-white';

  if (loading && prices.length === 0 && profiles.length === 0) return <div className="p-10 text-slate-400"><Loader2 className="animate-spin" /></div>;

  return (
    <div className="p-10 max-w-6xl">
      <div className="flex items-center justify-between mb-8">
        <div>
          <h1 className="text-3xl font-extrabold text-white">Precios y saldos de certificados</h1>
          <p className="text-slate-400 mt-2">Lo que FacilFactura le cobra a cada tenant por certificado de producción. Se paga por adelantado: cada solicitud reserva su precio del saldo del tenant, se devuelve si Viafirma la rechaza y queda consumida al emitirse. Sandbox no se cobra.</p>
        </div>
        <Coins className="w-10 h-10 text-emerald-400 shrink-0" />
      </div>

      <section className="rounded-2xl border border-slate-800 bg-slate-950/60 p-6 mb-8">
        <h2 className="text-lg font-bold text-white mb-1">Nuevo precio</h2>
        <p className="text-sm text-slate-400 mb-4">Sin tenant es el precio general; con tenant es un precio especial que manda sobre el general. Un precio nuevo cierra el anterior del mismo perfil y alcance; las solicitudes ya hechas conservan su valor.</p>
        {profiles.length === 0 ? (
          <p className="text-sm text-amber-300">No hay perfiles de producción. Sincronízalos en <Link to="/certificate-provider" className="underline">Proveedor de Certificados</Link>.</p>
        ) : (
          <div className="grid grid-cols-1 gap-4 md:grid-cols-6 items-end">
            <label className="block text-sm text-slate-300 md:col-span-2">Perfil
              <select value={form.profileId} onChange={e => setForm({ ...form, profileId: e.target.value })} className={field}>
                <option value="">Selecciona un perfil</option>
                {profiles.map(p => <option key={p.id} value={p.id}>{p.title}{p.providerValidityDays ? ` · ${p.providerValidityDays} días` : ''}</option>)}
              </select>
            </label>
            <label className="block text-sm text-slate-300 md:col-span-2">Tenant (opcional)
              <select value={form.tenantId} onChange={e => setForm({ ...form, tenantId: e.target.value })} className={field}>
                <option value="">Precio general (todos)</option>
                {[...tenants].sort((a, b) => (a.commercialName || a.name).localeCompare(b.commercialName || b.name)).map(t => <option key={t.id} value={t.id}>{t.commercialName || t.name}</option>)}
              </select>
            </label>
            <label className="block text-sm text-slate-300">Precio antes de IVA (COP)
              <input type="number" min="0" step="100" value={form.netAmount} onChange={e => setForm({ ...form, netAmount: e.target.value })} className={field} />
            </label>
            <label className="block text-sm text-slate-300">IVA %
              <input type="number" min="0" max="100" step="0.01" value={form.taxRate} onChange={e => setForm({ ...form, taxRate: e.target.value })} className={field} />
            </label>
            <label className="block text-sm text-slate-300 md:col-span-2">Vigente desde (vacío = hoy)
              <input type="date" value={form.effectiveFrom} onChange={e => setForm({ ...form, effectiveFrom: e.target.value })} className={field} />
            </label>
            <div className="md:col-span-2 text-sm text-slate-300">Total a descontar del saldo: <span className="font-bold text-white">{money(previewTotal)}</span></div>
            <div className="md:col-span-2 flex md:justify-end">
              <button onClick={createPrice} disabled={saving} className="flex items-center gap-2 rounded-lg bg-blue-600 px-5 py-2.5 font-semibold text-white hover:bg-blue-500 disabled:opacity-50">
                {saving ? <Loader2 className="w-4 animate-spin" /> : <Plus className="w-4" />} Guardar precio
              </button>
            </div>
          </div>
        )}
      </section>

      <section className="rounded-2xl border border-slate-800 bg-slate-950/60 p-6 mb-8">
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-lg font-bold text-white">Precios</h2>
          <label className="flex items-center gap-2 text-sm text-slate-400"><input type="checkbox" checked={showClosed} onChange={e => setShowClosed(e.target.checked)} /> Ver también los cerrados</label>
        </div>
        {visiblePrices.length === 0 ? (
          <p className="text-sm text-slate-500">Aún no hay precios. Sin precio vigente los tenants no pueden solicitar certificados de producción.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="text-left text-slate-400">
                <tr><th className="py-2 pr-4">Perfil</th><th className="py-2 pr-4">Alcance</th><th className="py-2 pr-4 text-right">Antes de IVA</th><th className="py-2 pr-4 text-right">IVA</th><th className="py-2 pr-4 text-right">Total</th><th className="py-2 pr-4">Desde</th><th className="py-2 pr-4">Hasta</th><th className="py-2 pr-4">Estado</th><th></th></tr>
              </thead>
              <tbody className="divide-y divide-slate-800 text-slate-200">
                {visiblePrices.map(price => {
                  const state = priceState(price);
                  return (
                    <tr key={price.id}>
                      <td className="py-2 pr-4">{price.profileTitle}</td>
                      <td className="py-2 pr-4">{price.tenantName ?? <span className="text-slate-400">General</span>}</td>
                      <td className="py-2 pr-4 text-right">{money(price.netAmount)}</td>
                      <td className="py-2 pr-4 text-right">{price.taxRate}%</td>
                      <td className="py-2 pr-4 text-right font-semibold">{money(price.total)}</td>
                      <td className="py-2 pr-4">{date(price.effectiveFrom)}</td>
                      <td className="py-2 pr-4">{date(price.effectiveTo)}</td>
                      <td className="py-2 pr-4"><span className={`inline-flex rounded-full border px-2.5 py-0.5 text-xs font-bold ${state.className}`}>{state.label}</span></td>
                      <td className="py-2 text-right">{state.label !== 'Cerrado' && <button onClick={() => closePrice(price)} className="text-xs font-semibold text-rose-300 hover:text-rose-200">Cerrar</button>}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section className="rounded-2xl border border-slate-800 bg-slate-950/60 p-6">
        <div className="flex items-center gap-2 mb-1"><Wallet className="w-5 h-5 text-emerald-400" /><h2 className="text-lg font-bold text-white">Saldos de los tenants</h2></div>
        <p className="text-sm text-slate-400 mb-4">Tenants con el módulo de certificados. Los abonos se registran en la ficha de cada tenant.</p>
        {balances.length === 0 ? (
          <p className="text-sm text-slate-500">Ningún tenant tiene el módulo de certificados.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="text-left text-slate-400">
                <tr><th className="py-2 pr-4">Tenant</th><th className="py-2 pr-4 text-right">Abonado</th><th className="py-2 pr-4 text-right">Reservado</th><th className="py-2 pr-4 text-right">Consumido</th><th className="py-2 pr-4 text-right">Disponible</th><th></th></tr>
              </thead>
              <tbody className="divide-y divide-slate-800 text-slate-200">
                {balances.map(row => (
                  <tr key={row.tenantId}>
                    <td className="py-2 pr-4">
                      {row.tenantName}
                      {row.certificatesOnly && <span className="ml-2 rounded-full bg-blue-500/20 px-2 py-0.5 text-xs text-blue-300">Solo certificados</span>}
                      {row.isBillingExempt && <span className="ml-2 rounded-full bg-slate-700 px-2 py-0.5 text-xs text-slate-300">Exento</span>}
                    </td>
                    <td className="py-2 pr-4 text-right">{money(row.deposited)}</td>
                    <td className="py-2 pr-4 text-right text-amber-300">{money(row.reserved)}</td>
                    <td className="py-2 pr-4 text-right">{money(row.consumed)}</td>
                    <td className={`py-2 pr-4 text-right font-bold ${row.available > 0 ? 'text-emerald-300' : 'text-slate-400'}`}>{money(row.available)}</td>
                    <td className="py-2 text-right"><Link to={`/tenants/edit/${row.tenantId}`} className="text-xs font-semibold text-blue-400 hover:text-blue-300">Abonar &rarr;</Link></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}
