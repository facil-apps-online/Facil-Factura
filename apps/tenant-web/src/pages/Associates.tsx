import React, { useState, useEffect } from 'react';
import { Plus, Edit2, Trash2, Briefcase } from 'lucide-react';
import { api } from '../lib/api';
import { toast } from 'sonner';

interface Associate {
  id: string;
  name: string;
  email: string;
  phone: string;
  isActive: boolean;
  createdAt: string;
  clientCount: number;
}

const initialForm = { name: '', email: '', phone: '', isActive: true };

export default function Associates() {
  const [associates, setAssociates] = useState<Associate[]>([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [formData, setFormData] = useState(initialForm);

  const loadAssociates = () => {
    setLoading(true);
    api.get('/tenant/associates')
      .then(res => setAssociates(res.data))
      .catch(() => toast.error('Error al cargar los asociados'))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    loadAssociates();
  }, []);

  const handleOpenModal = (associate?: Associate) => {
    if (associate) {
      setEditingId(associate.id);
      setFormData({ name: associate.name, email: associate.email, phone: associate.phone, isActive: associate.isActive });
    } else {
      setEditingId(null);
      setFormData(initialForm);
    }
    setShowModal(true);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      if (editingId) {
        await api.put(`/tenant/associates/${editingId}`, formData);
        toast.success('Asociado actualizado');
      } else {
        await api.post('/tenant/associates', formData);
        toast.success('Asociado creado');
      }
      setShowModal(false);
      loadAssociates();
    } catch (err: any) {
      toast.error(err.response?.data || 'Error al guardar el asociado');
    }
  };

  const handleDelete = async (id: string) => {
    if (!window.confirm('¿Eliminar este asociado? Los clientes que tenga asignados quedarán sin asociado.')) return;
    try {
      await api.delete(`/tenant/associates/${id}`);
      toast.success('Asociado eliminado');
      loadAssociates();
    } catch {
      toast.error('Error al eliminar el asociado');
    }
  };

  return (
    <div className="p-10 h-full overflow-y-auto animate-in fade-in slide-in-from-bottom-4 duration-500">
      <div className="flex justify-between items-center mb-8">
        <div>
          <h1 className="text-3xl font-bold text-slate-800">Asociados</h1>
          <p className="text-slate-500 mt-2">Tus comerciales — asígnales los clientes que gestionan cada uno.</p>
        </div>
        <button
          onClick={() => handleOpenModal()}
          className="bg-blue-600 hover:bg-blue-700 text-white px-5 py-2.5 rounded-xl font-medium shadow-lg shadow-blue-500/30 flex items-center gap-2 transition-all"
        >
          <Plus size={20} />
          <span>Nuevo Asociado</span>
        </button>
      </div>

      <div className="bg-white rounded-2xl shadow-sm border border-slate-100 overflow-hidden">
        <table className="w-full text-left">
          <thead className="bg-slate-50 text-slate-500 text-xs uppercase tracking-wider font-medium border-b border-slate-100">
            <tr>
              <th className="px-6 py-4">Nombre</th>
              <th className="px-6 py-4">Email</th>
              <th className="px-6 py-4">Teléfono</th>
              <th className="px-6 py-4 text-center">Clientes</th>
              <th className="px-6 py-4">Estado</th>
              <th className="px-6 py-4 text-right">Acciones</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100 text-sm">
            {associates.length === 0 && !loading && (
              <tr><td colSpan={6} className="text-center py-8 text-slate-500">No hay asociados registrados aún.</td></tr>
            )}
            {associates.map(a => (
              <tr key={a.id} className="hover:bg-slate-50/80 transition-colors">
                <td className="px-6 py-4 font-medium text-slate-800 flex items-center gap-2">
                  <Briefcase size={16} className="text-slate-400" /> {a.name}
                </td>
                <td className="px-6 py-4 text-slate-600">{a.email || '—'}</td>
                <td className="px-6 py-4 text-slate-600">{a.phone || '—'}</td>
                <td className="px-6 py-4 text-center text-slate-600">{a.clientCount}</td>
                <td className="px-6 py-4">
                  {a.isActive ? (
                    <span className="px-3 py-1 rounded-full text-xs font-semibold text-emerald-700 bg-emerald-100 border border-emerald-200">Activo</span>
                  ) : (
                    <span className="px-3 py-1 rounded-full text-xs font-semibold text-rose-700 bg-rose-100 border border-rose-200">Inactivo</span>
                  )}
                </td>
                <td className="px-6 py-4 flex items-center justify-end gap-3">
                  <button onClick={() => handleOpenModal(a)} className="p-1.5 text-slate-400 hover:text-blue-600 transition-colors bg-white rounded shadow-sm border border-slate-200">
                    <Edit2 size={16} />
                  </button>
                  <button onClick={() => handleDelete(a.id)} className="p-1.5 text-slate-400 hover:text-rose-600 transition-colors bg-white rounded shadow-sm border border-slate-200">
                    <Trash2 size={16} />
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {showModal && (
        <div className="fixed inset-0 bg-slate-900/50 backdrop-blur-sm z-50 flex items-center justify-center">
          <div className="bg-white rounded-2xl p-6 max-w-md w-full shadow-2xl">
            <h2 className="text-xl font-bold text-slate-800 mb-4">{editingId ? 'Editar Asociado' : 'Nuevo Asociado'}</h2>
            <form onSubmit={handleSubmit} className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-slate-600 mb-1">Nombre</label>
                <input required type="text" className="w-full px-4 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500 outline-none" value={formData.name} onChange={e => setFormData({ ...formData, name: e.target.value })} />
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-600 mb-1">Email</label>
                <input type="email" className="w-full px-4 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500 outline-none" value={formData.email} onChange={e => setFormData({ ...formData, email: e.target.value })} />
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-600 mb-1">Teléfono</label>
                <input type="text" className="w-full px-4 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500 outline-none" value={formData.phone} onChange={e => setFormData({ ...formData, phone: e.target.value })} />
              </div>
              {editingId && (
                <label className="flex items-center gap-2 text-sm font-medium text-slate-600">
                  <input type="checkbox" checked={formData.isActive} onChange={e => setFormData({ ...formData, isActive: e.target.checked })} className="w-4 h-4 accent-blue-600" />
                  Activo
                </label>
              )}
              <div className="flex gap-3 justify-end pt-4">
                <button type="button" onClick={() => setShowModal(false)} className="px-4 py-2 text-slate-500 hover:bg-slate-100 rounded-lg font-medium transition-colors">Cancelar</button>
                <button type="submit" className="bg-blue-600 hover:bg-blue-700 text-white px-5 py-2 rounded-lg font-medium shadow-md transition-colors">Guardar</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
