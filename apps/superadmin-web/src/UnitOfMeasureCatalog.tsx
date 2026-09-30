import React, { useEffect, useState } from 'react';
import { Plus, Save, Trash2, Ruler, ToggleLeft, ToggleRight, Pencil } from 'lucide-react';
import { toast } from 'sonner';
import { api } from './api';

interface UnitOfMeasureItem {
  id: string;
  dianCode: string;
  abbreviation: string;
  name: string;
  displayFormat: 'Combined' | 'CodeOnly' | 'AbbreviationOnly';
  isActive: boolean;
}

const DISPLAY_FORMAT_LABELS: Record<string, string> = {
  Combined: 'Código y sigla ("94 - EA")',
  CodeOnly: 'Solo código DIAN ("94")',
  AbbreviationOnly: 'Solo sigla ("EA")'
};

const initialForm = { dianCode: '', abbreviation: '', name: '', displayFormat: 'Combined' };

export const UnitOfMeasureCatalog = () => {
  const [items, setItems] = useState<UnitOfMeasureItem[]>([]);
  const [formData, setFormData] = useState(initialForm);
  const [showModal, setShowModal] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);

  const loadItems = () => {
    api.get<UnitOfMeasureItem[]>('/units-of-measure')
      .then(res => setItems(res.data))
      .catch(() => toast.error('Error al cargar el catálogo de unidades de medida'));
  };

  useEffect(() => {
    loadItems();
  }, []);

  const closeModal = () => {
    setShowModal(false);
    setEditingId(null);
    setFormData(initialForm);
  };

  const openCreateModal = () => {
    setEditingId(null);
    setFormData(initialForm);
    setShowModal(true);
  };

  const startEdit = (item: UnitOfMeasureItem) => {
    setEditingId(item.id);
    setFormData({ dianCode: item.dianCode, abbreviation: item.abbreviation, name: item.name, displayFormat: item.displayFormat });
    setShowModal(true);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      if (editingId) {
        await api.put(`/units-of-measure/${editingId}`, { ...formData, isActive: items.find(i => i.id === editingId)?.isActive ?? true });
        toast.success('Unidad actualizada exitosamente');
      } else {
        await api.post('/units-of-measure', formData);
        toast.success('Unidad creada exitosamente');
      }
      closeModal();
      loadItems();
    } catch (err: any) {
      toast.error(err.response?.data || (editingId ? 'Error al actualizar la unidad' : 'Error al crear la unidad'));
    }
  };

  const handleToggleActive = async (item: UnitOfMeasureItem) => {
    try {
      await api.put(`/units-of-measure/${item.id}`, { ...item, isActive: !item.isActive });
      loadItems();
    } catch {
      toast.error('Error al actualizar la unidad');
    }
  };

  const handleDelete = async (id: string) => {
    if (!window.confirm('¿Seguro que deseas eliminar esta unidad?')) return;
    try {
      await api.delete(`/units-of-measure/${id}`);
      toast.success('Eliminada correctamente');
      loadItems();
    } catch (err: any) {
      toast.error(err.response?.data || 'No se pudo eliminar la unidad');
    }
  };

  return (
    <div className="p-10 animate-in fade-in slide-in-from-bottom-4 duration-500">
      <div className="flex justify-between items-center mb-10">
        <div>
          <h1 className="text-3xl font-extrabold text-white tracking-tight flex items-center gap-3">
            <Ruler className="w-8 h-8 text-indigo-400" />
            Catálogo de Unidades de Medida
          </h1>
          <p className="text-slate-400 mt-2 text-lg font-medium">
            Unidades DIAN (UN/CEFACT Rec. 20) disponibles al crear productos — el código y la sigla se imprimen en la representación gráfica según el formato elegido.
          </p>
        </div>
        <button
          onClick={openCreateModal}
          className="bg-indigo-600 hover:bg-indigo-700 text-white px-6 py-3 rounded-2xl font-bold flex items-center shadow-lg shadow-indigo-600/30 transition-all transform hover:-translate-y-1"
        >
          <Plus className="w-5 h-5 mr-2" />
          Nueva Unidad
        </button>
      </div>

      <div className="glass-panel rounded-3xl overflow-hidden mt-8">
        <table className="w-full text-left">
          <thead className="bg-slate-900/50 border-b border-slate-700/50">
            <tr>
              <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Código DIAN</th>
              <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Sigla de unidad</th>
              <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Nombre</th>
              <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Formato por defecto</th>
              <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Estado</th>
              <th className="px-6 py-4 text-right"></th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-800/50">
            {items.map(item => (
              <tr key={item.id} className="hover:bg-slate-800/30 transition-colors group">
                <td className="px-6 py-4">
                  <span className="inline-flex items-center px-3 py-1 rounded-lg text-sm font-bold bg-slate-800 text-slate-300 font-mono">
                    {item.dianCode}
                  </span>
                </td>
                <td className="px-6 py-4 font-mono text-slate-300">{item.abbreviation}</td>
                <td className="px-6 py-4 font-bold text-white">{item.name}</td>
                <td className="px-6 py-4 text-slate-300 text-sm">{DISPLAY_FORMAT_LABELS[item.displayFormat] || item.displayFormat}</td>
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
            {items.length === 0 && (
              <tr>
                <td colSpan={6} className="px-6 py-12 text-center text-slate-400 font-medium">
                  No hay unidades registradas.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {showModal && (
        <div className="fixed inset-0 bg-slate-900/80 backdrop-blur-md flex items-center justify-center z-50 animate-in fade-in duration-200">
          <div className="glass-panel rounded-3xl shadow-2xl p-8 w-full max-w-md animate-in zoom-in-95 duration-200 border-slate-700">
            <h2 className="text-2xl font-bold text-white mb-6">{editingId ? 'Editar Unidad de Medida' : 'Nueva Unidad de Medida'}</h2>
            <form onSubmit={handleSubmit} className="space-y-5">
              <div>
                <label className="block text-sm font-bold text-slate-300 mb-2">Código DIAN</label>
                <input
                  type="text"
                  required
                  className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500 font-mono placeholder:text-slate-500"
                  value={formData.dianCode}
                  onChange={e => setFormData({ ...formData, dianCode: e.target.value.toUpperCase() })}
                  placeholder="94"
                />
                <p className="text-xs text-slate-500 mt-1">Código de unidad usado en la factura.</p>
              </div>
              <div>
                <label className="block text-sm font-bold text-slate-300 mb-2">Sigla de unidad</label>
                <input
                  type="text"
                  required
                  className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500 font-mono placeholder:text-slate-500"
                  value={formData.abbreviation}
                  onChange={e => setFormData({ ...formData, abbreviation: e.target.value.toUpperCase() })}
                  placeholder="EA"
                />
              </div>
              <div>
                <label className="block text-sm font-bold text-slate-300 mb-2">Nombre</label>
                <input
                  type="text"
                  required
                  className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500 placeholder:text-slate-500"
                  value={formData.name}
                  onChange={e => setFormData({ ...formData, name: e.target.value })}
                  placeholder="Unidad"
                />
              </div>
              <div>
                <label className="block text-sm font-bold text-slate-300 mb-2">Formato por defecto en la factura</label>
                <select
                  required
                  className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500"
                  value={formData.displayFormat}
                  onChange={e => setFormData({ ...formData, displayFormat: e.target.value })}
                >
                  <option value="Combined">{DISPLAY_FORMAT_LABELS.Combined}</option>
                  <option value="CodeOnly">{DISPLAY_FORMAT_LABELS.CodeOnly}</option>
                  <option value="AbbreviationOnly">{DISPLAY_FORMAT_LABELS.AbbreviationOnly}</option>
                </select>
                <p className="text-xs text-slate-500 mt-1">El cliente puede definir este formato para todas sus unidades.</p>
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
