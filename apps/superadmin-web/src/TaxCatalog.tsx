import React, { useEffect, useState } from 'react';
import { Plus, Save, Trash2, Percent, ToggleLeft, ToggleRight, Pencil } from 'lucide-react';
import { toast } from 'sonner';
import { api } from './api';

type TaxCatalogKind =
  | 'Retention' | 'OtherTax' | 'IvaRate' | 'PaymentTerm' | 'PaymentMeans'
  | 'WorkerType' | 'ContractType' | 'PayrollPaymentMeans' | 'TaxLevelCode' | 'Regimen' | 'AccountType' | 'FormaPago';

interface TaxCatalogItem {
  id: string;
  category: string;
  name: string;
  kind: TaxCatalogKind;
  rate: number | null;
  isActive: boolean;
}

const KIND_LABELS: Record<string, string> = {
  Retention: 'Retención (pie de documento)',
  OtherTax: 'Otro impuesto (por ítem)',
  IvaRate: 'Tarifa de IVA',
  PaymentTerm: 'Plazo de pago',
  PaymentMeans: 'Medio de pago (Facturas/Doc. Soporte)',
  WorkerType: 'Tipo de trabajador (Nómina)',
  ContractType: 'Tipo de contrato (Nómina)',
  PayrollPaymentMeans: 'Medio de pago (Nómina)',
  TaxLevelCode: 'Nivel tributario (tercero)',
  Regimen: 'Régimen (tercero)',
  AccountType: 'Cuenta bancaria',
  FormaPago: 'Forma de Pago (Facturas)'
};

// Ayuda a que el campo "Código" de la modal tenga sentido según el tipo elegido.
const CATEGORY_FIELD_LABEL: Record<string, string> = {
  Retention: 'Código (Dataico)',
  OtherTax: 'Código (Dataico)',
  IvaRate: 'Tarifa (%)',
  PaymentTerm: 'Días',
  PaymentMeans: 'Código (Dataico)',
  WorkerType: 'Código (Dataico)',
  ContractType: 'Código (Dataico)',
  PayrollPaymentMeans: 'Código (Dataico)',
  TaxLevelCode: 'Código (Dataico)',
  Regimen: 'Código (Dataico)',
  AccountType: 'Código (Dataico)',
  FormaPago: 'Código interno'
};

const NUMERIC_KINDS: TaxCatalogKind[] = ['IvaRate', 'PaymentTerm'];

const KIND_BADGE_CLASSES: Record<string, string> = {
  Retention: 'bg-amber-500/20 text-amber-400 border border-amber-500/30',
  OtherTax: 'bg-indigo-500/20 text-indigo-400 border border-indigo-500/30',
  IvaRate: 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30',
  PaymentTerm: 'bg-sky-500/20 text-sky-400 border border-sky-500/30',
  PaymentMeans: 'bg-fuchsia-500/20 text-fuchsia-400 border border-fuchsia-500/30',
  WorkerType: 'bg-rose-500/20 text-rose-400 border border-rose-500/30',
  ContractType: 'bg-orange-500/20 text-orange-400 border border-orange-500/30',
  PayrollPaymentMeans: 'bg-teal-500/20 text-teal-400 border border-teal-500/30',
  TaxLevelCode: 'bg-violet-500/20 text-violet-400 border border-violet-500/30',
  Regimen: 'bg-cyan-500/20 text-cyan-400 border border-cyan-500/30',
  AccountType: 'bg-lime-500/20 text-lime-400 border border-lime-500/30',
  FormaPago: 'bg-blue-500/20 text-blue-400 border border-blue-500/30'
};

// Para filtrar la tabla dado el volumen creciente de catálogos (ej. 53 medios de pago de nómina).
const KIND_FILTER_OPTIONS = Object.keys(KIND_LABELS) as TaxCatalogKind[];

export const TaxCatalog = () => {
  const [items, setItems] = useState<TaxCatalogItem[]>([]);
  const [formData, setFormData] = useState({ category: '', name: '', kind: 'Retention', rate: '' });
  const [showModal, setShowModal] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [kindFilter, setKindFilter] = useState<string>('');

  const loadItems = () => {
    api.get<TaxCatalogItem[]>('/tax-catalog')
      .then(res => setItems(res.data))
      .catch(() => toast.error('Error al cargar el catálogo de impuestos'));
  };

  useEffect(() => {
    loadItems();
  }, []);

  const closeModal = () => {
    setShowModal(false);
    setEditingId(null);
    setFormData({ category: '', name: '', kind: 'Retention', rate: '' });
  };

  const openCreateModal = () => {
    setEditingId(null);
    setFormData({ category: '', name: '', kind: 'Retention', rate: '' });
    setShowModal(true);
  };

  const startEdit = (item: TaxCatalogItem) => {
    setEditingId(item.id);
    setFormData({
      category: item.category,
      name: item.name,
      kind: item.kind,
      rate: item.rate != null ? String(item.rate) : ''
    });
    setShowModal(true);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const payload = { ...formData, rate: formData.kind === 'Retention' && formData.rate !== '' ? parseFloat(formData.rate) : null };
    try {
      if (editingId) {
        await api.put(`/tax-catalog/${editingId}`, { ...payload, isActive: items.find(i => i.id === editingId)?.isActive ?? true });
        toast.success('Categoría actualizada exitosamente');
      } else {
        await api.post('/tax-catalog', payload);
        toast.success('Categoría creada exitosamente');
      }
      closeModal();
      loadItems();
    } catch (err: any) {
      toast.error(err.response?.data || (editingId ? 'Error al actualizar la categoría' : 'Error al crear la categoría'));
    }
  };

  const handleToggleActive = async (item: TaxCatalogItem) => {
    try {
      await api.put(`/tax-catalog/${item.id}`, { ...item, isActive: !item.isActive });
      loadItems();
    } catch {
      toast.error('Error al actualizar la categoría');
    }
  };

  const handleDelete = async (id: string) => {
    if (!window.confirm('¿Seguro que deseas eliminar esta categoría?')) return;
    try {
      await api.delete(`/tax-catalog/${id}`);
      toast.success('Eliminada correctamente');
      loadItems();
    } catch (err: any) {
      toast.error(err.response?.data || 'No se pudo eliminar la categoría');
    }
  };

  return (
    <div className="p-10 animate-in fade-in slide-in-from-bottom-4 duration-500">
      <div className="flex justify-between items-center mb-10">
        <div>
          <h1 className="text-3xl font-extrabold text-white tracking-tight flex items-center gap-3">
            <Percent className="w-8 h-8 text-indigo-400" />
            Catálogo de Impuestos (Dataico)
          </h1>
          <p className="text-slate-400 mt-2 text-lg font-medium">
            Retenciones (RET_FUENTE, RET_ICA, RET_IVA) y otros impuestos por ítem (IMP_CONSUMO, etc.) disponibles para que tenants y clientes seleccionen, en vez de escribirlos a mano.
          </p>
        </div>
        <button
          onClick={openCreateModal}
          className="bg-indigo-600 hover:bg-indigo-700 text-white px-6 py-3 rounded-2xl font-bold flex items-center shadow-lg shadow-indigo-600/30 transition-all transform hover:-translate-y-1"
        >
          <Plus className="w-5 h-5 mr-2" />
          Nueva Categoría
        </button>
      </div>

      <div className="mb-4">
        <select
          value={kindFilter}
          onChange={e => setKindFilter(e.target.value)}
          className="px-4 py-2.5 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500 text-sm"
        >
          <option value="">Todos los tipos ({items.length})</option>
          {KIND_FILTER_OPTIONS.map(k => (
            <option key={k} value={k}>{KIND_LABELS[k]} ({items.filter(i => i.kind === k).length})</option>
          ))}
        </select>
      </div>

      <div className="glass-panel rounded-3xl overflow-hidden mt-8">
        <table className="w-full text-left">
          <thead className="bg-slate-900/50 border-b border-slate-700/50">
            <tr>
              <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Tipo</th>
              <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Código (Dataico)</th>
              <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Tarifa</th>
              <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Nombre</th>
              <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Estado</th>
              <th className="px-6 py-4 text-right"></th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-800/50">
            {items.filter(i => !kindFilter || i.kind === kindFilter).map(item => (
              <tr key={item.id} className="hover:bg-slate-800/30 transition-colors group">
                <td className="px-6 py-4">
                  <span className={`inline-flex items-center px-3 py-1 rounded-full text-xs font-bold ${KIND_BADGE_CLASSES[item.kind] || 'bg-indigo-500/20 text-indigo-400 border border-indigo-500/30'}`}>
                    {KIND_LABELS[item.kind] || item.kind}
                  </span>
                </td>
                <td className="px-6 py-4">
                  <span className="inline-flex items-center px-3 py-1 rounded-lg text-sm font-bold bg-slate-800 text-slate-300 font-mono">
                    {item.category}
                  </span>
                </td>
                <td className="px-6 py-4 font-mono text-slate-300">{item.rate != null ? `${item.rate}%` : '—'}</td>
                <td className="px-6 py-4 font-bold text-white">{item.name}</td>
                <td className="px-6 py-4">
                  <button onClick={() => handleToggleActive(item)} className="flex items-center gap-1.5 text-xs font-bold">
                    {item.isActive ? (
                      <span className="inline-flex items-center gap-1 px-3 py-1 rounded-full bg-emerald-100 text-emerald-700"><ToggleRight className="w-4 h-4" /> Activo</span>
                    ) : (
                      <span className="inline-flex items-center gap-1 px-3 py-1 rounded-full bg-slate-700 text-slate-400"><ToggleLeft className="w-4 h-4" /> Inactivo</span>
                    )}
                  </button>
                </td>
                <td className="px-6 py-4 text-right">
                  <div className="flex items-center justify-end gap-1 opacity-0 group-hover:opacity-100 transition-all">
                    <button onClick={() => startEdit(item)} className="p-2 text-slate-300 hover:text-indigo-400 hover:bg-indigo-500/10 rounded-xl" title="Editar">
                      <Pencil className="w-5 h-5" />
                    </button>
                    <button onClick={() => handleDelete(item.id)} className="p-2 text-red-500 hover:bg-red-500/10 rounded-xl" title="Eliminar">
                      <Trash2 className="w-5 h-5" />
                    </button>
                  </div>
                </td>
              </tr>
            ))}
            {items.filter(i => !kindFilter || i.kind === kindFilter).length === 0 && (
              <tr>
                <td colSpan={6} className="px-6 py-12 text-center text-slate-400 font-medium">
                  No hay categorías registradas.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {showModal && (
        <div className="fixed inset-0 bg-slate-900/80 backdrop-blur-md flex items-center justify-center z-50 animate-in fade-in duration-200">
          <div className="glass-panel rounded-3xl shadow-2xl p-8 w-full max-w-md animate-in zoom-in-95 duration-200 border-slate-700">
            <h2 className="text-2xl font-bold text-white mb-6">{editingId ? 'Editar Categoría de Impuesto' : 'Nueva Categoría de Impuesto'}</h2>
            <form onSubmit={handleSubmit} className="space-y-5">
              <div>
                <label className="block text-sm font-bold text-slate-300 mb-2">Tipo</label>
                <select
                  required
                  className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500"
                  value={formData.kind}
                  onChange={e => setFormData({ ...formData, kind: e.target.value })}
                >
                  <option value="Retention">Retención (pie de documento: RET_FUENTE, RET_ICA, RET_IVA)</option>
                  <option value="OtherTax">Otro impuesto por ítem (IMP_CONSUMO, etc.)</option>
                  <option value="IvaRate">Tarifa de IVA (para tratamiento Gravado)</option>
                  <option value="PaymentTerm">Plazo de pago (calcula fecha de vencimiento)</option>
                  <option value="PaymentMeans">Medio de pago (Facturas/Doc. Soporte)</option>
                  <option value="WorkerType">Tipo de trabajador (Nómina)</option>
                  <option value="ContractType">Tipo de contrato (Nómina)</option>
                  <option value="PayrollPaymentMeans">Medio de pago (Nómina)</option>
                  <option value="TaxLevelCode">Nivel tributario (tercero)</option>
                  <option value="Regimen">Régimen (tercero)</option>
                  <option value="AccountType">Cuenta bancaria</option>
                  <option value="FormaPago">Forma de Pago (Facturas)</option>
                </select>
              </div>
              <div>
                <label className="block text-sm font-bold text-slate-300 mb-2">{CATEGORY_FIELD_LABEL[formData.kind] || 'Código'}</label>
                <input
                  type="text"
                  required
                  className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500 font-mono placeholder:text-slate-500"
                  value={formData.category}
                  onChange={e => setFormData({ ...formData, category: NUMERIC_KINDS.includes(formData.kind as TaxCatalogKind) ? e.target.value : e.target.value.toUpperCase() })}
                  placeholder={formData.kind === 'IvaRate' ? '19' : formData.kind === 'PaymentTerm' ? '30' : 'RET_FUENTE'}
                />
              </div>
              {formData.kind === 'Retention' && (
                <div>
                  <label className="block text-sm font-bold text-slate-300 mb-2">Tarifa (%)</label>
                  <input
                    type="number" step="0.001" min="0" required
                    className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500 font-mono placeholder:text-slate-500"
                    value={formData.rate}
                    onChange={e => setFormData({ ...formData, rate: e.target.value })}
                    placeholder="Ej. 0.966"
                  />
                  <p className="text-xs text-slate-500 mt-1">Define la tarifa para esta categoría.</p>
                </div>
              )}
              <div>
                <label className="block text-sm font-bold text-slate-300 mb-2">Nombre Descriptivo</label>
                <input
                  type="text"
                  required
                  className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500 placeholder:text-slate-500"
                  value={formData.name}
                  onChange={e => setFormData({ ...formData, name: e.target.value })}
                  placeholder="Retención en la Fuente"
                />
              </div>
              <div className="flex gap-4 pt-4">
                <button type="button" onClick={closeModal} className="flex-1 py-3 px-4 bg-slate-100 hover:bg-slate-200 text-slate-700 font-bold rounded-xl transition-colors">
                  Cancelar
                </button>
                <button type="submit" className="flex-1 py-3 px-4 bg-indigo-600 hover:bg-indigo-700 text-white font-bold rounded-xl shadow-lg shadow-indigo-600/30 transition-all flex justify-center items-center">
                  <Save className="w-5 h-5 mr-2" />
                  {editingId ? 'Guardar Cambios' : 'Guardar'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
