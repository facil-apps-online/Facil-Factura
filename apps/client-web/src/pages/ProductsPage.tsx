import React, { useState, useEffect } from 'react';
import { Plus, Trash2, Loader2, Search } from 'lucide-react';
import { toast } from 'sonner';
import { api, getErrorMessage } from '../lib/api';
import { useConfirm } from '@/components/ConfirmDialog';
import ImportExcelButton from '../components/ImportExcelButton';
import Modal from '../components/Modal';
import ResponsiveList, { type ResponsiveListColumn } from '../components/ResponsiveList';
import RowIconButton from '../components/RowIconButton';
import { Button } from '../components/ui/button';
import SearchableSelect from '@shared/components/SearchableSelect';
import { useNumberFormat } from '../lib/numberFormat';
import DecimalInput from '../components/DecimalInput';

const IVA_TREATMENTS = [
  { value: 'Gravado', label: 'Gravado' },
  { value: 'Exento', label: 'Exento (tarifa 0%)' },
  { value: 'Excluido', label: 'Excluido (no aplica IVA)' }
];

interface ProductTax {
  taxCategory: string;
  rate: number;
}

type ProductScope = 'Invoice' | 'Support' | 'Payroll';

const PRODUCT_SCOPES: { value: ProductScope; label: string }[] = [
  { value: 'Invoice', label: 'Facturas' },
  { value: 'Support', label: 'Documentos soporte' },
  { value: 'Payroll', label: 'Nómina' },
];

export default function ProductsPage() {
  const fmt = useNumberFormat();
  const confirm = useConfirm();
  const [products, setProducts] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingProduct, setEditingProduct] = useState<any>(null);
  const [search, setSearch] = useState('');
  const [otherTaxCatalog, setOtherTaxCatalog] = useState<{ id: string, category: string, name: string }[]>([]);
  const [ivaRateCatalog, setIvaRateCatalog] = useState<{ id: string, category: string, name: string }[]>([]);
  const [unitOfMeasureCatalog, setUnitOfMeasureCatalog] = useState<{ id: string, dianCode: string, abbreviation: string, name: string }[]>([]);
  const [scope, setScope] = useState<ProductScope>('Invoice');
  const DEFAULT_UNIT_OF_MEASURE_ID = '30000000-0000-0000-0000-000000000001';

  const initialForm = {
    scope: 'Invoice' as ProductScope, code: '', name: '', unitPrice: 0, ivaTreatment: 'Gravado', ivaRate: 19.00,
    taxes: [] as ProductTax[], unitOfMeasureId: DEFAULT_UNIT_OF_MEASURE_ID, standardCode: ''
  };
  const [formData, setFormData] = useState(initialForm);

  useEffect(() => {
    loadProducts();
    api.get('/client/tax-catalog?kind=OtherTax')
      .then(res => setOtherTaxCatalog(res.data))
      .catch(() => {});
    api.get('/client/tax-catalog?kind=IvaRate')
      .then(res => setIvaRateCatalog(res.data))
      .catch(() => {});
    api.get('/client/units-of-measure')
      .then(res => setUnitOfMeasureCatalog(res.data))
      .catch(() => {});
  }, [scope]);

  const loadProducts = () => {
    api.get(`/client/products?scope=${scope}`)
      .then(res => {
        setProducts(res.data);
        setLoading(false);
      })
      .catch(() => {
        toast.error('Error al cargar productos');
        setLoading(false);
      });
  };

  const handleOpenModal = (product?: any) => {
    if (product) {
      setEditingProduct(product);
      setFormData({ ...initialForm, ...product, taxes: product.taxes?.length ? product.taxes : [] });
    } else {
      setEditingProduct(null);
      setFormData({ ...initialForm, scope });
    }
    setIsModalOpen(true);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      if (editingProduct) {
        await api.put(`/client/products/${editingProduct.id}`, formData);
        toast.success('Producto actualizado');
      } else {
        await api.post('/client/products', formData);
        toast.success('Producto creado');
      }
      setIsModalOpen(false);
      loadProducts();
    } catch (err: any) {
      toast.error(getErrorMessage(err, 'Error guardando producto'));
    }
  };

  const handleDelete = async (id: string) => {
    if (!(await confirm('¿Estás seguro de eliminar este producto?'))) return;
    try {
      await api.delete(`/client/products/${id}`);
      toast.success('Producto eliminado');
      loadProducts();
    } catch (err: any) {
      toast.error('Error al eliminar');
    }
  };

  if (loading) return <div className="flex justify-center p-12"><Loader2 className="animate-spin w-8 h-8 text-primary" /></div>;

  const q = search.trim().toLowerCase();
  const filteredProducts = q
    ? products.filter(p => [p.code, p.name, p.standardCode].some((f: string) => f?.toLowerCase().includes(q)))
    : products;

  const columns: ResponsiveListColumn<any>[] = [
    {
      key: 'code',
      header: 'Código (SKU)',
      cellClassName: 'font-mono text-sm font-medium text-slate-500',
      render: p => p.code
    },
    {
      key: 'name',
      header: 'Nombre o descripción',
      primary: true,
      cellClassName: 'font-bold text-slate-900',
      render: p => p.name
    },
    {
      key: 'price',
      header: 'Precio Base',
      align: 'right',
      cellClassName: 'font-medium',
      render: p => `$${fmt.number(p.unitPrice, 3)}`
    },
    {
      key: 'iva',
      header: 'IVA',
      align: 'right',
      cellClassName: 'text-sm text-slate-500',
      render: p => (p.ivaTreatment === 'Gravado' ? `${p.ivaRate}%` : IVA_TREATMENTS.find(t => t.value === p.ivaTreatment)?.label || p.ivaTreatment)
    },
    {
      key: 'otherTaxes',
      header: 'Otros Impuestos',
      align: 'right',
      cellClassName: 'text-sm text-slate-500',
      render: p => (p.taxes?.length ? p.taxes.map((t: ProductTax) => `${t.taxCategory} ${t.rate}%`).join(', ') : '—')
    }
  ];

  return (
    <div className="p-4 sm:p-6 lg:p-8">
      <div className="mb-6 flex flex-col gap-4 sm:mb-8 sm:flex-row sm:items-center sm:justify-between">
        <div className="min-w-0">
          <h1 className="text-2xl font-extrabold text-slate-800 sm:text-3xl">Productos y servicios</h1>
          <p className="mt-1 text-slate-500">Catálogo de productos y servicios</p>
        </div>
        <div className="flex flex-wrap gap-3">
          <ImportExcelButton endpoint={`/client/products/import?scope=${scope}`} templateEndpoint={`/client/products/template?scope=${scope}`} label="Importar Excel" onDone={loadProducts} />
          <button
            onClick={() => handleOpenModal()}
            className="bg-primary hover:bg-primary/90 text-white px-5 py-2.5 rounded-xl font-bold flex items-center gap-2 shadow-sm transition-all"
          >
            <Plus size={20} /> Nuevo Producto
          </button>
        </div>
      </div>

      {/* Pestañas: si no caben en el ancho (320 px) la tira se desplaza dentro de sí misma, sin mover la página. */}
      <div className="mb-6 flex overflow-x-auto border-b border-slate-200">
        {PRODUCT_SCOPES.map(option => (
          <button
            key={option.value}
            onClick={() => setScope(option.value)}
            className={`shrink-0 whitespace-nowrap px-4 py-3 font-bold text-sm border-b-2 transition-colors ${scope === option.value ? 'border-primary text-primary' : 'border-transparent text-slate-500 hover:text-slate-700'}`}
          >
            {option.label}
          </button>
        ))}
      </div>

      <div className="relative mb-6 w-full sm:w-80">
        <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
        <input
          type="text"
          placeholder="Buscar por código o nombre..."
          aria-label="Buscar productos"
          value={search}
          onChange={e => setSearch(e.target.value)}
          className="w-full pl-9 pr-3 py-2.5 bg-white border border-slate-200 rounded-xl text-sm outline-none focus:ring-2 focus:ring-primary"
        />
      </div>

      <ResponsiveList
        rows={filteredProducts}
        columns={columns}
        rowKey={p => p.id}
        tableFrom="wide"
        emptyMessage={q ? 'Ningún resultado para tu búsqueda.' : 'No tienes productos registrados.'}
        actions={p => (
          <>
            <RowIconButton action="edit" label={`Editar ${p.name}`} onClick={() => handleOpenModal(p)} />
            <RowIconButton action="delete" label={`Eliminar ${p.name}`} onClick={() => handleDelete(p.id)} />
          </>
        )}
      />

      <Modal
        open={isModalOpen}
        onOpenChange={setIsModalOpen}
        title={editingProduct ? 'Editar Producto' : 'Nuevo Producto'}
        size="md"
        // SearchableSelect pinta su lista en un portal sobre <body>.
        withFloatingPickers
        footer={
          <>
            <Button type="button" variant="ghost" onClick={() => setIsModalOpen(false)}>Cancelar</Button>
            <Button type="submit" form="product-form">Guardar Producto</Button>
          </>
        }
      >
        <form id="product-form" onSubmit={handleSubmit}>
          <div className="flex flex-col gap-4">
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
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-1">Código (SKU)</label>
                <input type="text" required value={formData.code} onChange={e => setFormData({...formData, code: e.target.value})} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none font-mono" />
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
              <input type="text" required value={formData.name} onChange={e => setFormData({...formData, name: e.target.value})} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none" />
            </div>

            <div>
              <label className="block text-sm font-bold text-slate-700 mb-1">Precio base</label>
              <DecimalInput value={formData.unitPrice} onValueChange={v => setFormData({...formData, unitPrice: v})} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none font-mono" />
            </div>

            {formData.scope !== 'Support' && <>
            <div>
              <label className="block text-sm font-bold text-slate-700 mb-1">Tratamiento de IVA</label>
              <div className="grid grid-cols-1 gap-2 sm:grid-cols-3">
                {IVA_TREATMENTS.map(t => (
                  <button
                    key={t.value}
                    type="button"
                    onClick={() => setFormData({ ...formData, ivaTreatment: t.value, ivaRate: t.value === 'Gravado' ? (formData.ivaRate || parseFloat(ivaRateCatalog[0]?.category) || 19) : 0 })}
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
                </div>
              )}
            </div>
            </>}

            {formData.scope !== 'Support' && <div>
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
            </div>}
          </div>
        </form>
      </Modal>
    </div>
  );
}
