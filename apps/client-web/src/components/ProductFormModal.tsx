import React, { useEffect, useState } from 'react';
import { Plus, Trash2 } from 'lucide-react';
import { toast } from 'sonner';
import { api, getErrorMessage } from '../lib/api';
import Modal from './Modal';
import { Button } from './ui/button';
import DecimalInput from './DecimalInput';
import SearchableSelect from '@shared/components/SearchableSelect';

const IVA_TREATMENTS = [
  { value: 'Gravado', label: 'Gravado' },
  { value: 'Exento', label: 'Exento (tarifa 0%)' },
  { value: 'Excluido', label: 'Excluido (no aplica IVA)' }
];

export type ProductScope = 'Invoice' | 'Support' | 'Payroll';

export const PRODUCT_SCOPES: { value: ProductScope; label: string }[] = [
  { value: 'Invoice', label: 'Facturas' },
  { value: 'Support', label: 'Documentos soporte' },
  { value: 'Payroll', label: 'Nómina' },
];

interface ProductTax {
  taxCategory: string;
  rate: number;
}

const DEFAULT_UNIT_OF_MEASURE_ID = '30000000-0000-0000-0000-000000000001';

const emptyForm = (scope: ProductScope) => ({
  scope, code: '', name: '', unitPrice: 0, ivaTreatment: scope === 'Support' ? 'Exento' : 'Gravado', ivaRate: scope === 'Support' ? 0 : 19.0, priceIncludesIva: false,
  taxes: [] as ProductTax[], unitOfMeasureId: DEFAULT_UNIT_OF_MEASURE_ID, standardCode: ''
});

interface ProductFormModalProps {
  open: boolean;
  // Producto que se edita; null crea uno nuevo.
  product: any | null;
  // Familia con la que se crea un producto nuevo.
  scope: ProductScope;
  // Fija la familia (documento en curso): no se ofrece cambiarla.
  lockScope?: boolean;
  // Datos con los que arranca un producto nuevo (lo escrito en el buscador de la línea).
  prefill?: { code?: string; name?: string };
  onClose: () => void;
  // Recibe el producto completo (con impuestos y unidad de medida) tal como quedó guardado.
  onSaved: (product: any, created: boolean) => void;
}

// Formulario único de producto: lo usan la pantalla de Productos y las líneas de los documentos (crear y editar sin salir del documento).
export default function ProductFormModal({ open, product, scope, lockScope, prefill, onClose, onSaved }: ProductFormModalProps) {
  const [formData, setFormData] = useState(emptyForm(scope));
  const [saving, setSaving] = useState(false);
  const [otherTaxCatalog, setOtherTaxCatalog] = useState<{ id: string, category: string, name: string }[]>([]);
  const [ivaRateCatalog, setIvaRateCatalog] = useState<{ id: string, category: string, name: string }[]>([]);
  const [unitOfMeasureCatalog, setUnitOfMeasureCatalog] = useState<{ id: string, dianCode: string, abbreviation: string, name: string }[]>([]);

  useEffect(() => {
    if (!open) return;
    setFormData(product
      ? { ...emptyForm(scope), ...product, taxes: product.taxes?.length ? product.taxes : [] }
      : { ...emptyForm(scope), ...prefill });
    // prefill solo cuenta al abrir: si cambiara con el cuadro abierto no debe borrar lo que se está escribiendo.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, product, scope]);

  useEffect(() => {
    if (!open) return;
    api.get('/client/tax-catalog?kind=OtherTax').then(res => setOtherTaxCatalog(res.data)).catch(() => {});
    api.get('/client/tax-catalog?kind=IvaRate').then(res => setIvaRateCatalog(res.data)).catch(() => {});
    api.get('/client/units-of-measure').then(res => setUnitOfMeasureCatalog(res.data)).catch(() => {});
  }, [open]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    try {
      const saved = product
        ? (await api.put(`/client/products/${product.id}`, formData)).data
        : (await api.post('/client/products', formData)).data;
      // La respuesta de guardar no trae la unidad de medida: se vuelve a leer completo.
      const full = (await api.get(`/client/products/${saved.id}`)).data;
      toast.success(product ? 'Producto actualizado' : 'Producto creado');
      onSaved(full, !product);
    } catch (err: any) {
      toast.error(getErrorMessage(err, 'Error guardando producto'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal
      open={open}
      onOpenChange={o => { if (!o) onClose(); }}
      title={product ? 'Editar Producto' : 'Nuevo Producto'}
      size="md"
      // SearchableSelect pinta su lista en un portal sobre <body>.
      withFloatingPickers
      footer={
        <>
          <Button type="button" variant="ghost" onClick={onClose}>Cancelar</Button>
          <Button type="submit" form="product-form" disabled={saving}>{saving ? 'Guardando...' : 'Guardar Producto'}</Button>
        </>
      }
    >
      <form id="product-form" onSubmit={handleSubmit}>
        <div className="flex flex-col gap-4">
          {product && formData.scope !== 'Support' && (
            <p className="rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
              El cambio queda en tu catálogo y aplica a los documentos que armes desde ahora. Los documentos ya emitidos no se modifican.
            </p>
          )}
          {!lockScope && (
            <div>
              <label className="block text-sm font-bold text-slate-700 mb-1">Catálogo</label>
              <div className="grid grid-cols-1 gap-2 sm:grid-cols-3">
                {PRODUCT_SCOPES.map(option => (
                  <button
                    key={option.value}
                    type="button"
                    onClick={() => setFormData({ ...formData, scope: option.value, ...(option.value === 'Support' ? { ivaTreatment: 'Exento', ivaRate: 0, taxes: [] } : {}) })}
                    className={`px-3 py-2.5 rounded-xl border text-sm font-semibold transition-colors ${formData.scope === option.value ? 'border-primary bg-primary/10 text-primary' : 'border-slate-200 text-slate-500 hover:bg-slate-50'}`}
                  >
                    {option.label}
                  </button>
                ))}
              </div>
            </div>
          )}
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <label className="block text-sm font-bold text-slate-700 mb-1">Código (SKU)</label>
              <input type="text" required value={formData.code} onChange={e => setFormData({ ...formData, code: e.target.value })} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none font-mono" />
            </div>
            <div>
              <label className="block text-sm font-bold text-slate-700 mb-1">Unidad de Medida (DIAN)</label>
              <SearchableSelect
                inputClassName="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none"
                value={formData.unitOfMeasureId}
                onChange={v => setFormData({ ...formData, unitOfMeasureId: v })}
                placeholder="Unidad..."
                options={unitOfMeasureCatalog.map(u => ({ value: u.id, label: `${u.name} (${u.dianCode})`, shortLabel: u.abbreviation }))}
              />
            </div>
          </div>

          <div>
            <label className="block text-sm font-bold text-slate-700 mb-1">Nombre o Descripción</label>
            <input type="text" required value={formData.name} onChange={e => setFormData({ ...formData, name: e.target.value })} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none" />
          </div>

          <div>
            <label className="block text-sm font-bold text-slate-700 mb-1">{formData.priceIncludesIva && formData.ivaTreatment === 'Gravado' ? 'Precio (con IVA incluido)' : 'Precio base'}</label>
            <DecimalInput value={formData.unitPrice} onValueChange={v => setFormData({ ...formData, unitPrice: v })} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none font-mono" />
          </div>

          {formData.scope !== 'Support' && <>
            <div>
              <label className="block text-sm font-bold text-slate-700 mb-1">Tratamiento de IVA</label>
              <div className="grid grid-cols-1 gap-2 sm:grid-cols-3">
                {IVA_TREATMENTS.map(t => (
                  <button
                    key={t.value}
                    type="button"
                    onClick={() => setFormData({ ...formData, ivaTreatment: t.value, ivaRate: t.value === 'Gravado' ? (formData.ivaRate || parseFloat(ivaRateCatalog[0]?.category) || 19) : 0, priceIncludesIva: t.value === 'Gravado' && formData.priceIncludesIva })}
                    className={`px-3 py-2.5 rounded-xl font-bold text-sm transition-colors ${
                      formData.ivaTreatment === t.value ? 'bg-primary text-white shadow-sm' : 'bg-slate-100 text-slate-500 hover:bg-slate-200'
                    }`}
                  >
                    {t.label}
                  </button>
                ))}
              </div>
              {formData.ivaTreatment === 'Gravado' && (
                <div className="mt-3">
                  <label className="block text-xs font-bold text-slate-500 mb-1">Tarifa de IVA</label>
                  <SearchableSelect
                    required
                    className="w-full sm:w-48"
                    value={String(formData.ivaRate)}
                    onChange={v => setFormData({ ...formData, ivaRate: parseFloat(v) || 0 })}
                    placeholder="Buscar tarifa..."
                    options={ivaRateCatalog.length === 0
                      ? [{ value: String(formData.ivaRate), label: `${formData.ivaRate}%` }]
                      : ivaRateCatalog.map(c => ({ value: c.category, label: c.name }))}
                  />
                  <label className="mt-3 flex items-center gap-2 text-sm font-bold text-slate-700">
                    <input type="checkbox" checked={formData.priceIncludesIva} onChange={e => setFormData({ ...formData, priceIncludesIva: e.target.checked })} className="w-4 h-4 accent-primary" />
                    El precio incluye IVA
                  </label>
                </div>
              )}
            </div>

            <div>
              <div className="flex justify-between items-center mb-2 gap-2">
                <label className="block text-sm font-bold text-slate-700">Otros Impuestos (opcional)</label>
                <button type="button" disabled={otherTaxCatalog.length === 0} onClick={() => setFormData({ ...formData, taxes: [...formData.taxes, { taxCategory: otherTaxCatalog[0]?.category || '', rate: 0 }] })} className="shrink-0 px-2 py-2.5 text-xs font-bold text-primary hover:underline flex items-center gap-1 disabled:opacity-50 disabled:no-underline">
                  <Plus size={14} /> Agregar impuesto
                </button>
              </div>
              {otherTaxCatalog.length === 0 && (
                <p className="text-xs text-amber-600 mb-2">No hay impuestos adicionales configurados.</p>
              )}
              <div className="space-y-3 sm:space-y-2">
                {formData.taxes.map((tax, idx) => (
                  // En móvil el selector ocupa toda la fila y debajo van tarifa y botón de quitar.
                  <div key={idx} className="flex flex-wrap items-center gap-2">
                    <SearchableSelect
                      className="w-full sm:w-auto sm:flex-1"
                      inputClassName="w-full px-3 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none bg-white text-sm"
                      value={tax.taxCategory}
                      onChange={v => setFormData({ ...formData, taxes: formData.taxes.map((t, i) => i === idx ? { ...t, taxCategory: v } : t) })}
                      placeholder="Buscar impuesto..."
                      options={otherTaxCatalog.map(c => ({ value: c.category, label: `${c.name} (${c.category})` }))}
                    />
                    <DecimalInput
                      placeholder="%" maxDecimals={3}
                      value={tax.rate}
                      onValueChange={v => setFormData({ ...formData, taxes: formData.taxes.map((t, i) => i === idx ? { ...t, rate: v } : t) })}
                      className="min-w-0 flex-1 px-3 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none font-mono text-sm sm:w-24 sm:flex-none"
                    />
                    <button
                      type="button"
                      aria-label="Quitar impuesto"
                      onClick={() => setFormData({ ...formData, taxes: formData.taxes.filter((_, i) => i !== idx) })}
                      className="flex h-11 w-11 shrink-0 items-center justify-center rounded-lg text-slate-400 hover:bg-rose-50 hover:text-rose-600 md:h-9 md:w-9"
                    >
                      <Trash2 size={16} />
                    </button>
                  </div>
                ))}
              </div>
            </div>
          </>}
        </div>
      </form>
    </Modal>
  );
}
