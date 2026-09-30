import React, { useEffect, useState } from 'react';
import { Plus, Save, Trash2, Cable, Power, PowerOff, Pencil } from 'lucide-react';
import { toast } from 'sonner';
import { api } from './api';

interface Integrator {
  id: string;
  code: string;
  name: string;
  nit: string;
  kind: number; // 0 = DirectDian, 1 = ThirdPartyIntegrator
  isActive: boolean;
}

const KIND_LABELS: Record<number, string> = {
  0: 'Directo DIAN',
  1: 'Integrador Externo'
};

export const Integrators = () => {
  const [integrators, setIntegrators] = useState<Integrator[]>([]);
  const [showModal, setShowModal] = useState(false);
  const [editing, setEditing] = useState<Integrator | null>(null);
  const [formData, setFormData] = useState({ code: '', name: '', nit: '', kind: 0 });

  const loadIntegrators = () => {
    api.get<Integrator[]>('/integrators')
      .then(res => setIntegrators(res.data))
      .catch(() => toast.error('Error al cargar los integradores'));
  };

  useEffect(() => {
    loadIntegrators();
  }, []);

  const openNew = () => {
    setEditing(null);
    setFormData({ code: '', name: '', nit: '', kind: 0 });
    setShowModal(true);
  };

  const openEdit = (integrator: Integrator) => {
    setEditing(integrator);
    setFormData({ code: integrator.code, name: integrator.name, nit: integrator.nit, kind: integrator.kind });
    setShowModal(true);
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      if (editing) {
        await api.put(`/integrators/${editing.id}`, formData);
        toast.success('Integrador actualizado');
      } else {
        await api.post('/integrators', formData);
        toast.success('Integrador creado');
      }
      setShowModal(false);
      loadIntegrators();
    } catch (err: any) {
      toast.error(err.response?.data || 'Error al guardar el integrador');
    }
  };

  const handleToggleActive = async (integrator: Integrator) => {
    try {
      await api.put(`/integrators/${integrator.id}/active`, { isActive: !integrator.isActive });
      loadIntegrators();
    } catch {
      toast.error('Error al actualizar el estado');
    }
  };

  const handleDelete = async (integrator: Integrator) => {
    if (!window.confirm(`¿Eliminar el integrador "${integrator.name}"? Esto fallará si ya tiene Clients asociados.`)) return;
    try {
      await api.delete(`/integrators/${integrator.id}`);
      toast.success('Integrador eliminado');
      loadIntegrators();
    } catch (err: any) {
      toast.error(err.response?.data || 'No se puede eliminar el integrador');
    }
  };

  return (
    <div className="p-10 animate-in fade-in slide-in-from-bottom-4 duration-500">
      <div className="flex justify-between items-center mb-10">
        <div>
          <h1 className="text-3xl font-extrabold text-white tracking-tight flex items-center gap-3">
            <Cable className="w-8 h-8 text-indigo-400" />
            Integradores
          </h1>
          <p className="text-slate-400 mt-2 text-lg font-medium">
            Catálogo de proveedores de documentos electrónicos (DIAN directa, Dataico, u otros que se agreguen a futuro).
          </p>
        </div>
        <button
          onClick={openNew}
          className="bg-indigo-600 hover:bg-indigo-700 text-white px-6 py-3 rounded-2xl font-bold flex items-center shadow-lg shadow-indigo-600/30 transition-all transform hover:-translate-y-1"
        >
          <Plus className="w-5 h-5 mr-2" />
          Nuevo Integrador
        </button>
      </div>

      <div className="glass-panel rounded-3xl overflow-hidden mt-8">
        <table className="w-full text-left">
          <thead className="bg-slate-900/50 border-b border-slate-700/50">
            <tr>
              <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Código</th>
              <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Nombre</th>
              <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">NIT</th>
              <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Tipo</th>
              <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Estado</th>
              <th className="px-6 py-4 text-right"></th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-800/50">
            {integrators.map(i => (
              <tr key={i.id} className="hover:bg-slate-800/30 transition-colors group">
                <td className="px-6 py-4">
                  <span className="inline-flex items-center px-3 py-1 rounded-lg text-sm font-bold bg-slate-800 text-slate-300 font-mono">
                    {i.code}
                  </span>
                </td>
                <td className="px-6 py-4 font-bold text-white">{i.name}</td>
                <td className="px-6 py-4 text-slate-400 font-mono text-sm">{i.nit || '—'}</td>
                <td className="px-6 py-4">
                  <span className={`inline-flex items-center px-3 py-1 rounded-full text-xs font-bold ${
                    i.kind === 1 ? 'bg-fuchsia-500/20 text-fuchsia-400 border border-fuchsia-500/30' : 'bg-indigo-500/20 text-indigo-400 border border-indigo-500/30'
                  }`}>
                    {KIND_LABELS[i.kind] ?? i.kind}
                  </span>
                </td>
                <td className="px-6 py-4">
                  <span className={`inline-flex items-center px-3 py-1 rounded-full text-xs font-bold ${i.isActive ? 'bg-emerald-100 text-emerald-700' : 'bg-slate-700 text-slate-300'}`}>
                    {i.isActive ? 'Activo' : 'Inactivo'}
                  </span>
                </td>
                <td className="px-6 py-4 text-right space-x-1">
                  <button onClick={() => openEdit(i)} className="p-2 text-slate-400 hover:bg-slate-700/40 rounded-xl opacity-0 group-hover:opacity-100 transition-all" title="Editar">
                    <Pencil className="w-5 h-5" />
                  </button>
                  <button onClick={() => handleToggleActive(i)} className="p-2 text-amber-400 hover:bg-amber-500/10 rounded-xl opacity-0 group-hover:opacity-100 transition-all" title={i.isActive ? 'Desactivar' : 'Reactivar'}>
                    {i.isActive ? <PowerOff className="w-5 h-5" /> : <Power className="w-5 h-5" />}
                  </button>
                  <button onClick={() => handleDelete(i)} className="p-2 text-red-500 hover:bg-red-500/10 rounded-xl opacity-0 group-hover:opacity-100 transition-all" title="Eliminar">
                    <Trash2 className="w-5 h-5" />
                  </button>
                </td>
              </tr>
            ))}
            {integrators.length === 0 && (
              <tr>
                <td colSpan={6} className="px-6 py-12 text-center text-slate-400 font-medium">
                  No hay integradores registrados.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {showModal && (
        <div className="fixed inset-0 bg-slate-900/80 backdrop-blur-md flex items-center justify-center z-50 animate-in fade-in duration-200">
          <div className="glass-panel rounded-3xl shadow-2xl p-8 w-full max-w-md animate-in zoom-in-95 duration-200 border-slate-700">
            <h2 className="text-2xl font-bold text-white mb-6">{editing ? 'Editar Integrador' : 'Nuevo Integrador'}</h2>
            <form onSubmit={handleSave} className="space-y-5">
              <div>
                <label className="block text-sm font-bold text-slate-300 mb-2">Código</label>
                <input
                  type="text"
                  required
                  disabled={!!editing}
                  className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500 font-mono uppercase placeholder:text-slate-500 disabled:opacity-50"
                  value={formData.code}
                  onChange={e => setFormData({ ...formData, code: e.target.value.toUpperCase() })}
                  placeholder="Ej: FACTUS"
                />
                {editing && <p className="text-xs text-slate-500 mt-1">El código no se puede cambiar después de crear el integrador.</p>}
              </div>
              <div>
                <label className="block text-sm font-bold text-slate-300 mb-2">Nombre</label>
                <input
                  type="text"
                  required
                  className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500 placeholder:text-slate-500"
                  value={formData.name}
                  onChange={e => setFormData({ ...formData, name: e.target.value })}
                  placeholder="Ej: Factus S.A.S."
                />
              </div>
              <div>
                <label className="block text-sm font-bold text-slate-300 mb-2">NIT</label>
                <input
                  type="text"
                  className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500 font-mono placeholder:text-slate-500"
                  value={formData.nit}
                  onChange={e => setFormData({ ...formData, nit: e.target.value })}
                  placeholder="900.XXX.XXX-X"
                />
              </div>
              <div>
                <label className="block text-sm font-bold text-slate-300 mb-2">Tipo</label>
                <select
                  required
                  className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500"
                  value={formData.kind}
                  onChange={e => setFormData({ ...formData, kind: parseInt(e.target.value) })}
                >
                  <option value={0}>Directo a la DIAN</option>
                  <option value={1}>Proveedor externo</option>
                </select>
              </div>
              <div className="flex gap-4 pt-4">
                <button type="button" onClick={() => setShowModal(false)} className="flex-1 py-3 px-4 bg-slate-100 hover:bg-slate-200 text-slate-700 font-bold rounded-xl transition-colors">
                  Cancelar
                </button>
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
