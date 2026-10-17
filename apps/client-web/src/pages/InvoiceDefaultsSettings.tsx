import React, { useEffect, useState } from 'react';
import { Loader2, Save } from 'lucide-react';
import { toast } from 'sonner';
import { api, getErrorMessage } from '../lib/api';
import { Button } from '../components/ui/button';
import SearchableSelect from '@shared/components/SearchableSelect';
import { useSession } from '../context/SessionContext';

interface CatalogItem { id: string; category: string; name: string }
interface DocumentTypeOption { id: string; name: string }

interface DefaultsForm {
  documentTypeId: string;
  paymentMeans: string;
  paymentMeansType: string;
  paymentTermDays: number | '';
  currency: string;
  notes: string;
}

const EMPTY: DefaultsForm = { documentTypeId: '', paymentMeans: '', paymentMeansType: '', paymentTermDays: '', currency: '', notes: '' };
const NOTES_MAX = 500;

const labelClass = 'block text-sm font-bold text-slate-700 mb-2';

// Valores con los que arranca "Nueva factura". Vacío = sin valor por defecto. El usuario puede cambiarlos en cada factura.
export default function InvoiceDefaultsSettings() {
  const { features } = useSession();
  const [form, setForm] = useState<DefaultsForm>(EMPTY);
  const [documentTypes, setDocumentTypes] = useState<DocumentTypeOption[]>([]);
  const [paymentMeans, setPaymentMeans] = useState<CatalogItem[]>([]);
  const [formaPago, setFormaPago] = useState<CatalogItem[]>([]);
  const [paymentTerms, setPaymentTerms] = useState<CatalogItem[]>([]);
  const [currencies, setCurrencies] = useState<CatalogItem[]>([]);
  const [termCustom, setTermCustom] = useState(false);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    (async () => {
      try {
        const [defaultsRes, typesRes, meansRes, formaRes, termsRes, currencyRes] = await Promise.all([
          api.get('/client/invoice-defaults'),
          api.get('/client/invoices/document-types'),
          api.get('/client/tax-catalog?kind=PaymentMeans'),
          api.get('/client/tax-catalog?kind=FormaPago'),
          api.get('/client/tax-catalog?kind=PaymentTerm'),
          features.foreignCurrency ? api.get('/client/tax-catalog?kind=Currency') : Promise.resolve({ data: [] }),
        ]);
        const d = defaultsRes.data;
        setDocumentTypes(typesRes.data);
        setPaymentMeans(meansRes.data);
        setFormaPago(formaRes.data);
        setPaymentTerms(termsRes.data);
        setCurrencies(currencyRes.data);
        setForm({
          documentTypeId: d.documentTypeId ?? '',
          paymentMeans: d.paymentMeans ?? '',
          paymentMeansType: d.paymentMeansType ?? '',
          paymentTermDays: d.paymentTermDays ?? '',
          currency: d.currency ?? '',
          notes: d.notes ?? '',
        });
        setTermCustom(d.paymentTermDays != null && !termsRes.data.some((c: CatalogItem) => c.category === String(d.paymentTermDays)));
      } catch (err) {
        toast.error(getErrorMessage(err, 'No se pudieron cargar los valores por defecto'));
      } finally {
        setLoading(false);
      }
    })();
  }, [features.foreignCurrency]);

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
      });
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
    <div className="p-4 sm:p-6 lg:p-8">
      <div className="mb-6 sm:mb-8">
        <h1 className="text-2xl sm:text-3xl font-extrabold text-slate-800 tracking-tight">Valores por defecto de factura</h1>
        <p className="text-slate-500 mt-2">Configure los campos con los que arranca una nueva factura. Cada usuario puede cambiarlos en la factura que esté creando.</p>
      </div>

      <div className="max-w-2xl space-y-5 rounded-2xl border border-slate-200 bg-white p-5 sm:p-6">
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

        <div>
          <label className={labelClass}>Observación general</label>
          <input type="text"
            value={form.notes}
            onChange={e => setForm({ ...form, notes: e.target.value })}
            maxLength={NOTES_MAX}
            placeholder="Texto que se precarga en cada factura nueva"
            className="w-full rounded-xl border border-slate-200 px-4 py-2.5 text-sm outline-none focus:border-blue-500"
          />
        </div>

        <div className="flex justify-end">
          <Button onClick={save} disabled={saving}>
            {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
            Guardar
          </Button>
        </div>
      </div>
    </div>
  );
}
