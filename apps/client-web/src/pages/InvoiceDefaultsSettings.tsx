import React, { useEffect, useState } from 'react';
import { Loader2, Save } from 'lucide-react';
import { toast } from 'sonner';
import { api, getErrorMessage } from '../lib/api';
import { Button } from '../components/ui/button';
import SearchableSelect from '@shared/components/SearchableSelect';
import { useSession } from '../context/SessionContext';
import { DEFAULT_RETENTION_STYLES, type RetentionLabelStyle } from '../lib/retentionLabel';

interface CatalogItem { id: string; category: string; name: string }
interface DocumentTypeOption { id: string; name: string }

interface DefaultsForm {
  documentTypeId: string;
  paymentMeans: string;
  paymentMeansType: string;
  paymentTermDays: number | '';
  currency: string;
  notes: string;
  customerAddress: string;
  customerCityCode: string;
  customerCityName: string;
  customerPostalCode: string;
  customerPhone: string;
  customerRegimen: string;
  customerTaxLevelCode: string;
  customerCountryCode: string;
}

const EMPTY: DefaultsForm = { documentTypeId: '', paymentMeans: '', paymentMeansType: '', paymentTermDays: '', currency: '', notes: '',
  customerAddress: '', customerCityCode: '', customerCityName: '', customerPostalCode: '', customerPhone: '',
  customerRegimen: '', customerTaxLevelCode: '', customerCountryCode: '' };
const NOTES_MAX = 500;

const RETENTION_STYLE_OPTIONS = [
  { value: 'Short', label: 'Nombre corto (RETE FUENTE 2.5%)' },
  { value: 'Full', label: 'Nombre completo del concepto' }
];

const labelClass = 'block text-sm font-bold text-slate-700 mb-2';

// Valores con los que arranca "Nueva factura". Vacío = sin valor por defecto. El usuario puede cambiarlos en cada factura.
export default function InvoiceDefaultsSettings() {
  const { features, setRetentionLabelStyles } = useSession();
  // Cómo se ven las retenciones en los selectores de los formularios: las de cada línea y las del pie, por separado.
  const [retentionStyles, setRetentionStyles] = useState<{ line: RetentionLabelStyle; footer: RetentionLabelStyle }>(features.retentionLabelStyles ?? DEFAULT_RETENTION_STYLES);
  const [form, setForm] = useState<DefaultsForm>(EMPTY);
  const [documentTypes, setDocumentTypes] = useState<DocumentTypeOption[]>([]);
  const [paymentMeans, setPaymentMeans] = useState<CatalogItem[]>([]);
  const [formaPago, setFormaPago] = useState<CatalogItem[]>([]);
  const [paymentTerms, setPaymentTerms] = useState<CatalogItem[]>([]);
  const [currencies, setCurrencies] = useState<CatalogItem[]>([]);
  const [regimens, setRegimens] = useState<CatalogItem[]>([]);
  const [taxLevels, setTaxLevels] = useState<CatalogItem[]>([]);
  const [countries, setCountries] = useState<CatalogItem[]>([]);
  const [termCustom, setTermCustom] = useState(false);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    (async () => {
      try {
        const [defaultsRes, typesRes, meansRes, formaRes, termsRes, currencyRes, regimenRes, taxLevelRes, countryRes] = await Promise.all([
          api.get('/client/invoice-defaults'),
          api.get('/client/invoices/document-types'),
          api.get('/client/tax-catalog?kind=PaymentMeans'),
          api.get('/client/tax-catalog?kind=FormaPago'),
          api.get('/client/tax-catalog?kind=PaymentTerm'),
          features.foreignCurrency ? api.get('/client/tax-catalog?kind=Currency') : Promise.resolve({ data: [] }),
          api.get('/client/tax-catalog?kind=Regimen'),
          api.get('/client/tax-catalog?kind=TaxLevelCode'),
          api.get('/client/tax-catalog?kind=Country'),
        ]);
        const d = defaultsRes.data;
        setDocumentTypes(typesRes.data);
        setPaymentMeans(meansRes.data);
        setFormaPago(formaRes.data);
        setPaymentTerms(termsRes.data);
        setCurrencies(currencyRes.data);
        setRegimens(regimenRes.data);
        setTaxLevels(taxLevelRes.data);
        setCountries(countryRes.data);
        setForm({
          documentTypeId: d.documentTypeId ?? '',
          paymentMeans: d.paymentMeans ?? '',
          paymentMeansType: d.paymentMeansType ?? '',
          paymentTermDays: d.paymentTermDays ?? '',
          currency: d.currency ?? '',
          notes: d.notes ?? '',
          customerAddress: d.customerAddress ?? '',
          customerCityCode: d.customerCityCode ?? '',
          customerCityName: d.customerCityName ?? '',
          customerPostalCode: d.customerPostalCode ?? '',
          customerPhone: d.customerPhone ?? '',
          customerRegimen: d.customerRegimen ?? '',
          customerTaxLevelCode: d.customerTaxLevelCode ?? '',
          customerCountryCode: d.customerCountryCode ?? '',
        });
        setTermCustom(d.paymentTermDays != null && !termsRes.data.some((c: CatalogItem) => c.category === String(d.paymentTermDays)));
      } catch (err) {
        toast.error(getErrorMessage(err, 'No se pudieron cargar los valores por defecto'));
      } finally {
        setLoading(false);
      }
    })();
  }, [features.foreignCurrency]);

  // Fuera de Colombia no hay código DANE: la ciudad es texto libre.
  const foreignCountry = form.customerCountryCode !== '' && form.customerCountryCode !== 'CO';

  // Al salir del nombre de la ciudad se busca su código DANE en el catálogo (igual que en el formulario de tercero).
  const matchCity = async () => {
    const name = form.customerCityName.trim();
    if (foreignCountry || !name || form.customerCityCode) return;
    try {
      const res = await api.get('/client/customers/municipality-match', { params: { name } });
      setForm(prev => ({ ...prev, customerCityCode: res.data.code, customerCityName: res.data.name }));
    } catch {
      toast.info('No se encontró un municipio con ese nombre. Escriba el código DANE.');
    }
  };

  const save = async () => {
    setSaving(true);
    try {
      await api.put('/client/invoice-defaults', {
        documentTypeId: form.documentTypeId || null,
        paymentMeans: form.paymentMeans || null,
        paymentMeansType: form.paymentMeansType || null,
        paymentTermDays: form.paymentMeansType === 'CREDITO' && form.paymentTermDays !== '' ? form.paymentTermDays : null,
        currency: form.currency || null,
        notes: form.notes.trim() || null,
        customerAddress: form.customerAddress.trim() || null,
        customerCityCode: foreignCountry ? null : form.customerCityCode.trim() || null,
        customerCityName: foreignCountry ? form.customerCityName.trim() || null : null,
        customerRegimen: form.customerRegimen || null,
        customerTaxLevelCode: form.customerTaxLevelCode || null,
        customerCountryCode: form.customerCountryCode || null,
        customerPostalCode: form.customerPostalCode.trim() || null,
        customerPhone: form.customerPhone.trim() || null,
      });
      const styles = await api.put('/client/retention-concepts/label-style', retentionStyles);
      setRetentionLabelStyles(styles.data);
      toast.success('Valores por defecto guardados');
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudieron guardar los valores por defecto'));
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="flex h-full items-center justify-center">
        <Loader2 className="w-10 h-10 animate-spin text-blue-500" />
      </div>
    );
  }

  return (
    <div className="p-3 sm:p-4">
      <div className="mb-6 sm:mb-8">
        <h1 className="text-2xl sm:text-3xl font-extrabold text-slate-800 tracking-tight">Valores por defecto</h1>
        <p className="text-slate-500 mt-2">Configure los campos con los que arrancan una nueva factura y un tercero nuevo. Cada usuario puede cambiarlos en lo que esté creando.</p>
      </div>

      <div className="grid grid-cols-1 gap-5 rounded-2xl border border-slate-200 bg-white p-4 sm:p-5 md:grid-cols-2 xl:grid-cols-3">
        <div>
          <label className={labelClass}>Tipo de documento</label>
          <SearchableSelect
            value={form.documentTypeId}
            onChange={v => setForm({ ...form, documentTypeId: v })}
            placeholder="Factura de venta estándar (predeterminado)"
            emptyLabel="Sin valor por defecto"
            options={documentTypes.map(t => ({ value: t.id, label: t.name }))}
          />
        </div>

        <div>
          <label className={labelClass}>Condiciones de pago</label>
          <SearchableSelect
            value={form.paymentMeansType}
            onChange={v => {
              setForm({ ...form, paymentMeansType: v, paymentTermDays: v === 'CREDITO' ? form.paymentTermDays : '' });
              if (v !== 'CREDITO') setTermCustom(false);
            }}
            placeholder="Contado o crédito..."
            emptyLabel="Sin valor por defecto"
            options={formaPago.map(c => ({ value: c.category, label: c.name }))}
          />
        </div>

        {form.paymentMeansType === 'CREDITO' && (
          <div>
            <label className={labelClass}>Plazo de pago</label>
            <div className="flex flex-col gap-3 sm:flex-row">
              <div className="flex-1">
                <SearchableSelect
                  value={termCustom ? 'OTHER' : (form.paymentTermDays === '' ? '' : String(form.paymentTermDays))}
                  onChange={v => {
                    if (v === 'OTHER') { setTermCustom(true); return; }
                    setTermCustom(false);
                    setForm({ ...form, paymentTermDays: v === '' ? '' : parseInt(v) });
                  }}
                  placeholder="Seleccione el plazo..."
                  emptyLabel="Sin plazo por defecto"
                  options={[...paymentTerms.map(c => ({ value: c.category, label: c.name })), { value: 'OTHER', label: 'Otro (personalizado)' }]}
                />
              </div>
              {termCustom && (
                <input
                  type="number"
                  min={0}
                  max={3650}
                  value={form.paymentTermDays}
                  onChange={e => setForm({ ...form, paymentTermDays: e.target.value === '' ? '' : parseInt(e.target.value) })}
                  placeholder="Días"
                  aria-label="Días de plazo"
                  className="w-full rounded-xl border border-slate-200 px-4 py-2.5 text-sm outline-none focus:border-blue-500 sm:w-32"
                />
              )}
            </div>
          </div>
        )}

        <div>
          <label className={labelClass}>Medio de pago</label>
          <SearchableSelect
            value={form.paymentMeans}
            onChange={v => setForm({ ...form, paymentMeans: v })}
            placeholder="Efectivo, consignación, transferencia..."
            emptyLabel="Sin valor por defecto"
            options={[...paymentMeans].sort((a, b) => a.name.localeCompare(b.name, 'es')).map(c => ({ value: c.category, label: c.name }))}
          />
        </div>

        {features.foreignCurrency && (
          <div>
            <label className={labelClass}>Moneda</label>
            <SearchableSelect
              value={form.currency}
              onChange={v => setForm({ ...form, currency: v })}
              placeholder="Pesos colombianos (COP)"
              emptyLabel="Pesos colombianos (COP)"
              options={currencies.filter(c => c.category !== 'COP').map(c => ({ value: c.category, label: `${c.category} - ${c.name}` }))}
            />
          </div>
        )}

        <div className="md:col-span-2 xl:col-span-3">
          <label className={labelClass}>Observación general</label>
          <input type="text"
            value={form.notes}
            onChange={e => setForm({ ...form, notes: e.target.value })}
            maxLength={NOTES_MAX}
            placeholder="Texto que se precarga en cada factura nueva"
            className="w-full rounded-xl border border-slate-200 px-4 py-2.5 text-sm outline-none focus:border-blue-500"
          />
        </div>

        <div className="border-t border-slate-100 pt-5 md:col-span-2 xl:col-span-3">
          <h2 className="text-lg font-extrabold text-slate-800">Tercero nuevo (tipo Cliente)</h2>
          <p className="mt-1 mb-4 text-sm text-slate-500">Con estos datos arranca el formulario al crear un Cliente. Se pueden cambiar en cada tercero.</p>
          <div className="space-y-4">
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
              <div>
                <label className={labelClass}>País</label>
                <SearchableSelect
                  value={form.customerCountryCode || 'CO'}
                  onChange={v => setForm({ ...form, customerCountryCode: !v || v === 'CO' ? '' : v, customerCityCode: '', customerCityName: '' })}
                  placeholder="Buscar país..."
                  options={(countries.length === 0 ? [{ id: 'CO', category: 'CO', name: 'Colombia' }] : countries).map(c => ({ value: c.category, label: `${c.name} (${c.category})`, shortLabel: c.category }))}
                />
              </div>
              <div>
                <label className={labelClass}>Régimen fiscal</label>
                <SearchableSelect
                  value={form.customerRegimen}
                  onChange={v => setForm({ ...form, customerRegimen: v })}
                  placeholder="Buscar régimen..."
                  emptyLabel="Sin valor por defecto"
                  options={regimens.map(c => ({ value: c.category, label: c.name }))}
                />
              </div>
              <div>
                <label className={labelClass}>Nivel tributario</label>
                <SearchableSelect
                  value={form.customerTaxLevelCode}
                  onChange={v => setForm({ ...form, customerTaxLevelCode: v })}
                  placeholder="Buscar nivel tributario..."
                  emptyLabel="Sin valor por defecto"
                  options={taxLevels.map(c => ({ value: c.category, label: c.name }))}
                />
              </div>
            </div>
            <div>
              <label className={labelClass}>Dirección</label>
              <input type="text" value={form.customerAddress} maxLength={200} onChange={e => setForm({ ...form, customerAddress: e.target.value })} className="w-full rounded-xl border border-slate-200 px-4 py-2.5 text-sm outline-none focus:border-blue-500" />
            </div>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
              <div>
                <label className={labelClass}>Ciudad</label>
                <input type="text" value={form.customerCityName} maxLength={100}
                  onChange={e => setForm({ ...form, customerCityName: e.target.value, customerCityCode: '' })}
                  onBlur={matchCity}
                  className="w-full rounded-xl border border-slate-200 px-4 py-2.5 text-sm outline-none focus:border-blue-500" />
              </div>
              {!foreignCountry && <div>
                <label className={labelClass}>Código DANE</label>
                <input type="text" placeholder="Ej. 11001" value={form.customerCityCode} maxLength={10} onChange={e => setForm({ ...form, customerCityCode: e.target.value })} className="w-full rounded-xl border border-slate-200 px-4 py-2.5 text-sm font-mono outline-none focus:border-blue-500" />
              </div>}
              <div>
                <label className={labelClass}>Código postal</label>
                <input type="text" value={form.customerPostalCode} maxLength={20} onChange={e => setForm({ ...form, customerPostalCode: e.target.value })} className="w-full rounded-xl border border-slate-200 px-4 py-2.5 text-sm font-mono outline-none focus:border-blue-500" />
              </div>
              <div>
                <label className={labelClass}>Teléfono</label>
                <input type="text" value={form.customerPhone} maxLength={50} onChange={e => setForm({ ...form, customerPhone: e.target.value })} className="w-full rounded-xl border border-slate-200 px-4 py-2.5 text-sm outline-none focus:border-blue-500" />
              </div>
            </div>
          </div>
        </div>

        <div className="md:col-span-2 xl:col-span-3 border-t border-slate-100 pt-5">
          <h2 className="text-lg font-extrabold text-slate-800">Retenciones en los formularios</h2>
          <p className="mb-4 mt-1 text-sm text-slate-500">Cómo se ven las retenciones al elegirlas: con el nombre corto y la tarifa (RETE FUENTE 2.5%) o con el nombre completo del concepto. Se configura por separado para las retenciones de cada línea y para las del pie del documento.</p>
          <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
            <div>
              <label className={labelClass}>Retenciones de cada línea</label>
              <SearchableSelect
                value={retentionStyles.line}
                onChange={v => setRetentionStyles({ ...retentionStyles, line: (v || 'Short') as RetentionLabelStyle })}
                options={RETENTION_STYLE_OPTIONS}
                placeholder="Estilo"
                inputClassName="w-full rounded-xl border border-slate-200 px-4 py-2.5 text-sm outline-none focus:border-blue-500"
              />
            </div>
            <div>
              <label className={labelClass}>Retenciones del pie</label>
              <SearchableSelect
                value={retentionStyles.footer}
                onChange={v => setRetentionStyles({ ...retentionStyles, footer: (v || 'Short') as RetentionLabelStyle })}
                options={RETENTION_STYLE_OPTIONS}
                placeholder="Estilo"
                inputClassName="w-full rounded-xl border border-slate-200 px-4 py-2.5 text-sm outline-none focus:border-blue-500"
              />
            </div>
          </div>
        </div>

        <div className="flex justify-end md:col-span-2 xl:col-span-3">
          <Button onClick={save} disabled={saving}>
            {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
            Guardar
          </Button>
        </div>
      </div>
    </div>
  );
}
