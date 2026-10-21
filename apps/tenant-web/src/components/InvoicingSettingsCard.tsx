import React, { useEffect, useState } from 'react';
import { ChevronDown, ChevronRight, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { api, getErrorMessage } from '../lib/api';
import SearchableSelect from '@shared/components/SearchableSelect';

export interface InvoicingProduct {
  id: string;
  code: string;
  name: string;
  ivaTreatment: string;
  ivaRate: number;
  priceIncludesIva: boolean;
}

interface InvoicingSettings {
  ownerClientId: string | null;
  ownerClientName: string | null;
  generateDrafts: boolean;
  draftDay: number;
  feeProductId: string | null;
  extraProductId: string | null;
  autoSyncCustomers: boolean;
  tenantNit: string;
  eligibleClientId: string | null;
  eligibleClientName: string | null;
}

export const productLabel = (p: InvoicingProduct) =>
  `${p.code} · ${p.name} · ${p.ivaTreatment === 'Gravado' ? `IVA ${p.ivaRate}%` : p.ivaTreatment}${p.priceIncludesIva ? ' (precio con IVA)' : ''}`;

// Facturación automática de los planes (opcional): el tenant elige cuál de sus clientes es su empresa emisora y, si quiere, el sistema deja cada
// ciclo un borrador de factura por cliente en esa empresa. Nunca emite: la emisión la hace una persona desde el portal de la empresa emisora.
export default function InvoicingSettingsCard() {
  const [settings, setSettings] = useState<InvoicingSettings | null>(null);
  const [products, setProducts] = useState<InvoicingProduct[]>([]);
  const [open, setOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [generating, setGenerating] = useState(false);

  const loadProducts = () => api.get<InvoicingProduct[]>('/tenant/invoicing/products').then(res => setProducts(res.data)).catch(() => setProducts([]));

  useEffect(() => {
    api.get<InvoicingSettings>('/tenant/invoicing/settings')
      .then(s => {
        setSettings(s.data);
        // Sin empresa emisora todavía: el panel abre solo, para que se vea dónde se define.
        if (!s.data.ownerClientId) setOpen(true);
      })
      .catch(err => toast.error(getErrorMessage(err, 'No se pudo cargar la facturación de planes')));
    loadProducts();
  }, []);

  if (!settings) return <div className="mb-6 flex justify-center p-4"><Loader2 className="animate-spin text-slate-400" /></div>;
  const set = (patch: Partial<InvoicingSettings>) => setSettings({ ...settings, ...patch });

  const save = async () => {
    setSaving(true);
    try {
      const res = await api.put<InvoicingSettings>('/tenant/invoicing/settings', settings);
      setSettings(res.data);
      await loadProducts();
      toast.success('Facturación de planes guardada');
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo guardar'));
    } finally {
      setSaving(false);
    }
  };

  const generateNow = async () => {
    setGenerating(true);
    try {
      const res = await api.post('/tenant/invoicing/generate');
      const { created, skipped, messages } = res.data;
      if (created > 0) toast.success(`${created} borrador${created === 1 ? '' : 'es'} generado${created === 1 ? '' : 's'}.`);
      else toast.info(messages?.[0] ?? 'No había cobros nuevos por generar en este ciclo.');
      if (created === 0 && skipped > 0 && !messages?.length) toast.info(`${skipped} cliente(s) ya tenían su cobro de este ciclo o no tienen nada por cobrar.`);
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudieron generar los borradores'));
    } finally {
      setGenerating(false);
    }
  };

  const productOptions = products.map(p => ({ value: p.id, label: productLabel(p) }));

  return (
    <div className="mb-6 rounded-2xl border border-slate-200 bg-white">
      <button type="button" onClick={() => setOpen(!open)} className="flex w-full items-center justify-between px-5 py-4 text-left">
        <span>
          <span className="block text-sm font-bold text-slate-800">Facturación automática de planes</span>
          <span className="block text-xs text-slate-500">
            {settings.generateDrafts
              ? `Activa: un borrador por cliente el día ${settings.draftDay} de cada ciclo, en ${settings.ownerClientName ?? 'la empresa emisora'}`
              : 'Apagada: tú facturas a mano (opcional)'}
          </span>
        </span>
        {open ? <ChevronDown size={18} className="text-slate-400" /> : <ChevronRight size={18} className="text-slate-400" />}
      </button>

      {open && (
        <div className="space-y-5 border-t border-slate-100 px-5 py-5">
          <p className="text-sm text-slate-500">
            El sistema deja el borrador de factura de cada cliente con la cuota y el excedente de su plan. Tú los revisas y los emites desde la empresa emisora,
            con el botón o por lotes de 50. Nunca emite solo. Funciona con Dataico y con emisión directa a la DIAN, según cómo esté configurada la empresa emisora.
          </p>

          <div className="grid grid-cols-1 gap-5 md:grid-cols-2 xl:grid-cols-4">
            <div className="xl:col-span-2">
              <label className="mb-1 block text-sm font-bold text-slate-700">Empresa emisora (tu empresa)</label>
              {settings.eligibleClientId ? (
                <SearchableSelect value={settings.ownerClientId ?? ''} onChange={v => set({ ownerClientId: v || null })} placeholder="Sin empresa emisora"
                  options={[{ value: '', label: 'Sin empresa emisora' }, { value: settings.eligibleClientId, label: `${settings.eligibleClientName} · NIT ${settings.tenantNit}` }]} />
              ) : (
                <p className="rounded-xl border border-amber-200 bg-amber-50 px-4 py-2.5 text-sm text-amber-900">
                  {settings.tenantNit
                    ? `Aún no tienes tu empresa creada como cliente. Créala con el mismo NIT del tenant (${settings.tenantNit}) y vuelve aquí.`
                    : 'Completa el NIT de tu tenant para poder definir la empresa emisora.'}
                </p>
              )}
              <p className="mt-1 text-xs text-slate-400">Es tu propia empresa (el cliente con el mismo NIT del tenant) y solo puede haber una. Debe tener su certificado y su resolución.</p>
            </div>
            <div>
              <label className="mb-1 block text-sm font-bold text-slate-700">Día del ciclo</label>
              <input type="number" min={1} max={28} value={settings.draftDay} onChange={e => set({ draftDay: Math.min(28, Math.max(1, parseInt(e.target.value) || 1)) })}
                className="w-full rounded-xl border border-slate-200 bg-slate-50 px-4 py-2.5 text-sm font-mono outline-none focus:ring-2 focus:ring-blue-500" />
              <p className="mt-1 text-xs text-slate-400">Día en que se generan los borradores</p>
            </div>
            <label className="flex items-center gap-2 self-center text-sm font-bold text-slate-700">
              <input type="checkbox" checked={settings.generateDrafts} onChange={e => set({ generateDrafts: e.target.checked })} className="h-4 w-4 accent-blue-600" />
              Generar borradores cada ciclo
            </label>
          </div>

          <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
            <div>
              <label className="mb-1 block text-sm font-bold text-slate-700">Producto de la cuota</label>
              <SearchableSelect value={settings.feeProductId ?? ''} onChange={v => set({ feeProductId: v || null })}
                placeholder={settings.ownerClientId ? 'Producto del catálogo de la empresa emisora...' : 'Guarda primero la empresa emisora'} options={productOptions} />
            </div>
            <div>
              <label className="mb-1 block text-sm font-bold text-slate-700">Producto de los documentos adicionales (opcional)</label>
              <SearchableSelect value={settings.extraProductId ?? ''} onChange={v => set({ extraProductId: v || null })}
                placeholder="Si lo dejas vacío, usa el de la cuota" options={[{ value: '', label: 'Usar el de la cuota' }, ...productOptions]} />
            </div>
          </div>
          <label className="flex items-start gap-2 text-sm text-slate-700">
            <input type="checkbox" checked={settings.autoSyncCustomers} onChange={e => set({ autoSyncCustomers: e.target.checked })} className="mt-0.5 h-4 w-4 accent-blue-600" />
            <span>Crear automáticamente, en la empresa emisora, el tercero de cada cliente nuevo que registres (también puedes sincronizarlos a mano desde Terceros)</span>
          </label>

          <p className="text-xs text-slate-400">
            El producto da el nombre, el IVA y la unidad de la línea; el precio y la cantidad salen del plan. Cada plan puede traer sus propios productos.
            Si cambias de empresa emisora, guarda y luego elige los productos de su catálogo.
          </p>

          <div className="flex flex-wrap justify-end gap-3">
            <button onClick={generateNow} disabled={generating || !settings.ownerClientId || !settings.feeProductId}
              className="rounded-xl bg-white px-4 py-2.5 text-sm font-bold text-slate-700 shadow-sm ring-1 ring-slate-200 hover:bg-slate-50 disabled:opacity-40">
              {generating ? 'Generando...' : 'Generar borradores ahora'}
            </button>
            <button onClick={save} disabled={saving} className="rounded-xl bg-blue-600 px-5 py-2.5 text-sm font-bold text-white hover:bg-blue-700 disabled:opacity-50">
              {saving ? 'Guardando...' : 'Guardar'}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
