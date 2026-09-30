import React, { useEffect, useState } from 'react';
import { Plus, Save, Trash2, Coins, Pencil } from 'lucide-react';
import { toast } from 'sonner';
import { api } from './api';

interface Tier {
  id: string;
  name: string;
  minDocuments: number;
  maxDocuments: number | null;
  pricePerDocument: number;
  isActive: boolean;
  integratorId: string | null;
  integratorName: string | null;
}

interface IntegratorOption {
  id: string;
  code: string;
  name: string;
}

export const TariffTiers = () => {
  const [tiers, setTiers] = useState<Tier[]>([]);
  const [integrators, setIntegrators] = useState<IntegratorOption[]>([]);
  const [showModal, setShowModal] = useState(false);
  const [editing, setEditing] = useState<Tier | null>(null);
  const [formData, setFormData] = useState({ name: '', minDocuments: 1, maxDocuments: '' as number | '', pricePerDocument: 0, isActive: true, integratorId: '' });

  const loadTiers = () => {
    api.get<Tier[]>('/tariff-tiers')
      .then(res => setTiers(res.data))
      .catch(() => toast.error('Error al cargar el tarifario'));
  };

  useEffect(() => {
    loadTiers();
    api.get<IntegratorOption[]>('/integrators').then(res => setIntegrators(res.data)).catch(() => {});
  }, []);

  const openNew = () => {
    setEditing(null);
    setFormData({ name: '', minDocuments: 1, maxDocuments: '', pricePerDocument: 0, isActive: true, integratorId: '' });
    setShowModal(true);
  };

  const openEdit = (tier: Tier) => {
    setEditing(tier);
    setFormData({
      name: tier.name,
      minDocuments: tier.minDocuments,
      maxDocuments: tier.maxDocuments ?? '',
      pricePerDocument: tier.pricePerDocument,
      isActive: tier.isActive,
      integratorId: tier.integratorId ?? ''
    });
    setShowModal(true);
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    const payload = {
      name: formData.name,
      minDocuments: formData.minDocuments,
      maxDocuments: formData.maxDocuments === '' ? null : formData.maxDocuments,
      pricePerDocument: formData.pricePerDocument,
      isActive: formData.isActive,
      integratorId: formData.integratorId || null
    };
    try {
      if (editing) {
        await api.put(`/tariff-tiers/${editing.id}`, payload);
      } else {
        await api.post('/tariff-tiers', payload);
      }
      toast.success('Tier guardado');
      setShowModal(false);
      loadTiers();
    } catch (err: any) {
      toast.error(err.response?.data || 'Error al guardar el tier');
    }
  };

  const handleDelete = async (tier: Tier) => {
    if (!window.confirm(`¿Eliminar el tier "${tier.name}"?`)) return;
    try {
      await api.delete(`/tariff-tiers/${tier.id}`);
      toast.success('Tier eliminado');
      loadTiers();
    } catch {
      toast.error('Error al eliminar el tier');
    }
  };

  return (
    <div className="p-10 animate-in fade-in slide-in-from-bottom-4 duration-500">
      <div className="flex justify-between items-center mb-10">
        <div>
          <h1 className="text-3xl font-extrabold text-white tracking-tight flex items-center gap-3">
            <Coins className="w-8 h-8 text-indigo-400" />
            Tarifario por Volumen
          </h1>
          <p className="text-slate-400 mt-2 text-lg font-medium">
            Lo que Superadmin le cobra a los Tenants por documento, según volumen mensual. Un tier sin integrador es el default global; uno con integrador solo aplica a ese integrador y tiene prioridad sobre el global.
          </p>
        </div>
        <button
          onClick={openNew}
          className="bg-indigo-600 hover:bg-indigo-700 text-white px-6 py-3 rounded-2xl font-bold flex items-center shadow-lg shadow-indigo-600/30 transition-all transform hover:-translate-y-1"
        >
          <Plus className="w-5 h-5 mr-2" />
          Nuevo Tier
        </button>
      </div>

      <div className="glass-panel rounded-3xl overflow-hidden mt-8">
        <table className="w-full text-left">
          <thead className="bg-slate-900/50 border-b border-slate-700/50">
            <tr>
              <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Nombre</th>
              <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Integrador</th>
              <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Rango</th>
              <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Tarifa / documento</th>
              <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Estado</th>
              <th className="px-6 py-4 text-right"></th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-800/50">
            {tiers.map(t => (
              <tr key={t.id} className="hover:bg-slate-800/30 transition-colors group">
                <td className="px-6 py-4 font-bold text-white">{t.name}</td>
                <td className="px-6 py-4">
                  {t.integratorName ? (
                    <span className="inline-flex items-center px-3 py-1 rounded-full text-xs font-bold bg-fuchsia-500/20 text-fuchsia-400 border border-fuchsia-500/30">{t.integratorName}</span>
                  ) : (
                    <span className="inline-flex items-center px-3 py-1 rounded-full text-xs font-bold bg-slate-700 text-slate-300">Global</span>
                  )}
                </td>
                <td className="px-6 py-4 text-slate-300 text-sm">{t.minDocuments.toLocaleString('es-CO')} — {t.maxDocuments ? t.maxDocuments.toLocaleString('es-CO') : '∞'}</td>
                <td className="px-6 py-4 text-slate-300 font-mono text-sm">${t.pricePerDocument.toLocaleString('es-CO')}</td>
                <td className="px-6 py-4">
                  <span className={`px-2 py-1 rounded-md text-xs font-bold ${t.isActive ? 'bg-emerald-100 text-emerald-700' : 'bg-slate-700 text-slate-300'}`}>
                    {t.isActive ? 'Activo' : 'Inactivo'}
                  </span>
                </td>
                <td className="px-6 py-4 text-right space-x-1">
                  <button onClick={() => openEdit(t)} className="p-2 text-slate-400 hover:bg-slate-700/40 rounded-xl opacity-0 group-hover:opacity-100 transition-all" title="Editar">
                    <Pencil className="w-5 h-5" />
                  </button>
                  <button onClick={() => handleDelete(t)} className="p-2 text-red-500 hover:bg-red-500/10 rounded-xl opacity-0 group-hover:opacity-100 transition-all" title="Eliminar">
                    <Trash2 className="w-5 h-5" />
                  </button>
                </td>
              </tr>
            ))}
            {tiers.length === 0 && (
              <tr>
                <td colSpan={6} className="px-6 py-12 text-center text-slate-400 font-medium">Sin tiers registrados.</td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {showModal && (
        <div className="fixed inset-0 bg-slate-900/80 backdrop-blur-md flex items-center justify-center z-50 animate-in fade-in duration-200">
          <div className="glass-panel rounded-3xl shadow-2xl p-8 w-full max-w-md animate-in zoom-in-95 duration-200 border-slate-700">
            <h2 className="text-2xl font-bold text-white mb-6">{editing ? 'Editar Tier' : 'Nuevo Tier'}</h2>
            <form onSubmit={handleSave} className="space-y-5">
              <div>
                <label className="block text-sm font-bold text-slate-300 mb-2">Nombre</label>
                <input required type="text" className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500" value={formData.name} onChange={e => setFormData({ ...formData, name: e.target.value })} placeholder="Ej: Nivel 1" />
              </div>
              <div>
                <label className="block text-sm font-bold text-slate-300 mb-2">Integrador (opcional)</label>
                <select
                  disabled={!!editing}
                  className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500 disabled:opacity-50"
                  value={formData.integratorId}
                  onChange={e => setFormData({ ...formData, integratorId: e.target.value })}
                >
                  <option value="">Global</option>
                  {integrators.map(i => <option key={i.id} value={i.id}>{i.name}</option>)}
                </select>
                {editing && <p className="text-xs text-slate-500 mt-1">El integrador no se puede cambiar una vez creado.</p>}
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-bold text-slate-300 mb-2">Desde (documentos)</label>
                  <input required type="number" min="0" className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500" value={formData.minDocuments} onChange={e => setFormData({ ...formData, minDocuments: parseInt(e.target.value) || 0 })} />
                </div>
                <div>
                  <label className="block text-sm font-bold text-slate-300 mb-2">Hasta (opcional)</label>
                  <input type="number" min="0" className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500" value={formData.maxDocuments} onChange={e => setFormData({ ...formData, maxDocuments: e.target.value === '' ? '' : parseInt(e.target.value) || 0 })} />
                </div>
              </div>
              <div>
                <label className="block text-sm font-bold text-slate-300 mb-2">Tarifa por documento (COP)</label>
                <input required type="number" min="1" step="1" className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500" value={formData.pricePerDocument || ''} onChange={e => setFormData({ ...formData, pricePerDocument: parseFloat(e.target.value) || 0 })} />
              </div>
              <div className="flex gap-4 pt-4">
                <button type="button" onClick={() => setShowModal(false)} className="flex-1 py-3 px-4 bg-slate-100 hover:bg-slate-200 text-slate-700 font-bold rounded-xl transition-colors">Cancelar</button>
                <button type="submit" className="flex-1 py-3 px-4 bg-indigo-600 hover:bg-indigo-700 text-white font-bold rounded-xl shadow-lg shadow-indigo-600/30 transition-all flex justify-center items-center">
                  <Save className="w-5 h-5 mr-2" />
                  Guardar
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
