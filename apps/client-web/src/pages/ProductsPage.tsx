import React, { useState, useEffect } from 'react';
import { Plus, Edit2, Trash2, X, Loader2, Search } from 'lucide-react';
import { toast } from 'sonner';
import { api, getErrorMessage } from '../lib/api';
import { useConfirm } from '@shared/components/ConfirmDialog';
import ImportExcelButton from '../components/ImportExcelButton';
import SearchableSelect from '@shared/components/SearchableSelect';

const IVA_TREATMENTS = [
  { value: 'Gravado', label: 'Gravado' },
  { value: 'Exento', label: 'Exento (tarifa 0%)' },
  { value: 'Excluido', label: 'Excluido (no aplica IVA)' }
];

interface ProductTax {
  taxCategory: string;
  rate: number;
}

export default function ProductsPage() {
  const confirm = useConfirm();
  const [products, setProducts] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingProduct, setEditingProduct] = useState<any>(null);
  const [search, setSearch] = useState('');
  const [otherTaxCatalog, setOtherTaxCatalog] = useState<{ id: string, category: string, name: string }[]>([]);
  const [ivaRateCatalog, setIvaRateCatalog] = useState<{ id: string, category: string, name: string }[]>([]);
  const [unitOfMeasureCatalog, setUnitOfMeasureCatalog] = useState<{ id: string, dianCode: string, abbreviation: string, name: string }[]>([]);
  const DEFAULT_UNIT_OF_MEASURE_ID = '30000000-0000-0000-0000-000000000001';

  const initialForm = {
    code: '', name: '', unitPrice: 0, ivaTreatment: 'Gravado', ivaRate: 19.00,
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
  }, []);

  const loadProducts = () => {
    api.get('/client/products')
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
      setFormData(initialForm);
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

  return (
    <div className="p-8">
      <div className="flex justify-between items-center mb-8">
        <div>
          <h1 className="text-3xl font-extrabold text-slate-800">Productos y servicios</h1>
          <p className="text-slate-500 mt-1">Catálogo de productos y servicios</p>
        </div>
        <div className="flex gap-3">
          <ImportExcelButton endpoint="/client/products/import" templateEndpoint="/client/products/template" label="Importar Excel" onDone={loadProducts} />
          <button
            onClick={() => handleOpenModal()}
            className="bg-primary hover:bg-primary/90 text-white px-5 py-2.5 rounded-xl font-bold flex items-center gap-2 shadow-sm transition-all"
          >
            <Plus size={20} /> Nuevo Producto
          </button>
        </div>
      </div>

      <div className="relative w-80 mb-6">
        <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
        <input
          type="text"
          placeholder="Buscar por código o nombre..."
          value={search}
          onChange={e => setSearch(e.target.value)}
          className="w-full pl-9 pr-3 py-2 bg-white border border-slate-200 rounded-xl text-sm outline-none focus:ring-2 focus:ring-primary"
        />
      </div>

      <div className="bg-white rounded-2xl shadow-sm border border-slate-200 overflow-hidden">
        <table className="w-full text-left border-collapse">
          <thead>
            <tr className="bg-slate-50 border-b border-slate-200 text-sm font-bold text-slate-500 uppercase tracking-wider">
              <th className="p-4">Código (SKU)</th>
              <th className="p-4">Nombre o descripción</th>
              <th className="p-4 text-right">Precio Base</th>
              <th className="p-4 text-right">IVA</th>
              <th className="p-4 text-right">Otros Impuestos</th>
              <th className="p-4 text-right">Acciones</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {filteredProducts.map(p => (
              <tr key={p.id} className="hover:bg-slate-50/50 transition-colors">
                <td className="p-4 font-mono text-sm font-medium text-slate-500">{p.code}</td>
                <td className="p-4 text-slate-900 font-bold">{p.name}</td>
                <td className="p-4 text-right font-medium">${p.unitPrice.toLocaleString('es-CO')}</td>
                <td className="p-4 text-right text-slate-500 text-sm">
                  {p.ivaTreatment === 'Gravado' ? `${p.ivaRate}%` : IVA_TREATMENTS.find(t => t.value === p.ivaTreatment)?.label || p.ivaTreatment}
                </td>
                <td className="p-4 text-right text-slate-500 text-sm">
                  {p.taxes?.length
                    ? p.taxes.map((t: ProductTax) => `${t.taxCategory} ${t.rate}%`).join(', ')
                    : '—'}
                </td>
                <td className="p-4 flex items-center justify-end gap-2">
                  <button onClick={() => handleOpenModal(p)} className="p-2 text-slate-400 hover:text-blue-600 hover:bg-blue-50 rounded-lg transition-colors">
                    <Edit2 size={18} />
                  </button>
                  <button onClick={() => handleDelete(p.id)} className="p-2 text-slate-400 hover:text-rose-600 hover:bg-rose-50 rounded-lg transition-colors">
                    <Trash2 size={18} />
                  </button>
                </td>
              </tr>
            ))}
            {filteredProducts.length === 0 && (
              <tr>
                <td colSpan={6} className="p-8 text-center text-slate-500">
                  {q ? 'Ningún resultado para tu búsqueda.' : 'No tienes productos registrados.'}
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {isModalOpen && (
        <div className="fixed inset-0 bg-slate-900/50 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-3xl w-full max-w-xl shadow-2xl overflow-hidden animate-in zoom-in-95 duration-200">
            <div className="px-6 py-4 border-b border-slate-100 flex justify-between items-center bg-slate-50/50">
              <h3 className="text-xl font-bold text-slate-800">{editingProduct ? 'Editar Producto' : 'Nuevo Producto'}</h3>
              <button onClick={() => setIsModalOpen(false)} className="text-slate-400 hover:text-slate-600 p-2"><X size={20} /></button>
            </div>
            <form onSubmit={handleSubmit} className="p-6">
              <div className="flex flex-col gap-4">
                <div className="grid grid-cols-2 gap-4">
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
                  <input type="number" step="0.01" required value={formData.unitPrice} onChange={e => setFormData({...formData, unitPrice: parseFloat(e.target.value)})} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none font-mono" />
                </div>

                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-1">Tratamiento de IVA</label>
                  <div className="grid grid-cols-3 gap-2">
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
                        className="w-48"
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

                <div>
                  <div className="flex justify-between items-center mb-2">
                    <label className="block text-sm font-bold text-slate-700">Otros Impuestos (opcional)</label>
                    <button type="button" disabled={otherTaxCatalog.length === 0} onClick={() => setFormData({ ...formData, taxes: [...formData.taxes, { taxCategory: otherTaxCatalog[0]?.category || '', rate: 0 }] })} className="text-xs font-bold text-primary hover:underline flex items-center gap-1 disabled:opacity-50 disabled:no-underline">
                      <Plus size={14} /> Agregar impuesto
                    </button>
                  </div>
                  {otherTaxCatalog.length === 0 && (
                    <p className="text-xs text-amber-600 mb-2">No hay impuestos adicionales configurados.</p>
                  )}
                  <div className="space-y-2">
                    {formData.taxes.map((tax, idx) => (
                      <div key={idx} className="flex items-center gap-2">
                        <SearchableSelect
                          className="flex-1"
                          inputClassName="w-full px-3 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none bg-white text-sm"
                          value={tax.taxCategory}
                          onChange={v => setFormData({ ...formData, taxes: formData.taxes.map((t, i) => i === idx ? { ...t, taxCategory: v } : t) })}
                          placeholder="Buscar impuesto..."
                          options={otherTaxCatalog.map(c => ({ value: c.category, label: `${c.name} (${c.category})` }))}
                        />
                        <input
                          type="number" step="0.01" placeholder="%"
                          value={tax.rate}
                          onChange={e => setFormData({ ...formData, taxes: formData.taxes.map((t, i) => i === idx ? { ...t, rate: parseFloat(e.target.value) || 0 } : t) })}
                          className="w-24 px-3 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none font-mono text-sm"
                        />
                        <button type="button" onClick={() => setFormData({ ...formData, taxes: formData.taxes.filter((_, i) => i !== idx) })} className="p-2 text-slate-400 hover:text-rose-600">
                          <Trash2 size={16} />
                        </button>
                      </div>
                    ))}
                  </div>
                </div>
              </div>
              <div className="mt-8 flex justify-end gap-3 pt-4 border-t border-slate-100">
                <button type="button" onClick={() => setIsModalOpen(false)} className="px-5 py-2 text-slate-600 font-bold hover:bg-slate-100 rounded-xl transition-colors">Cancelar</button>
                <button type="submit" className="px-5 py-2 bg-primary text-white font-bold rounded-xl hover:bg-primary/90 transition-colors shadow-md">Guardar Producto</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
