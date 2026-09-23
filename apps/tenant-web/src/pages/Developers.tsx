import React, { useState, useEffect } from 'react';
import { Plus, Trash2, Code2, Mail, RotateCcw } from 'lucide-react';
import { api } from '../lib/api';
import { toast } from 'sonner';

interface Developer {
  id: string;
  name: string;
  email: string;
  isActive: boolean;
  createdAt: string;
}

const initialForm = { name: '', email: '' };

export default function Developers() {
  const [developers, setDevelopers] = useState<Developer[]>([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [formData, setFormData] = useState(initialForm);
  const [saving, setSaving] = useState(false);

  const loadDevelopers = () => {
    setLoading(true);
    api.get('/tenant/developers')
      .then(res => setDevelopers(res.data))
      .catch(() => toast.error('Error al cargar los developers'))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    loadDevelopers();
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    try {
      await api.post('/tenant/developers', formData);
      toast.success('Invitación enviada');
      setShowModal(false);
      setFormData(initialForm);
      loadDevelopers();
    } catch (err: any) {
      toast.error(err.response?.data || 'Error al invitar al developer');
    } finally {
      setSaving(false);
    }
  };

  const handleResendInvitation = async (id: string) => {
    try {
      await api.post(`/tenant/developers/${id}/resend-invitation`);
      toast.success('Invitación reenviada');
    } catch (err: any) {
      toast.error(err.response?.data || 'Error al reenviar la invitación');
    }
  };

  const handleRevoke = async (id: string) => {
    if (!window.confirm('¿Revocar el acceso de este developer al portal?')) return;
    try {
      await api.delete(`/tenant/developers/${id}`);
      toast.success('Acceso revocado');
      loadDevelopers();
    } catch {
      toast.error('Error al revocar el acceso');
    }
  };

  const handleReactivate = async (id: string) => {
    try {
      await api.post(`/tenant/developers/${id}/reactivate`);
      toast.success('Acceso reactivado');
      loadDevelopers();
    } catch {
      toast.error('Error al reactivar el acceso');
    }
  };

  return (
    <div className="p-10 h-full overflow-y-auto animate-in fade-in slide-in-from-bottom-4 duration-500">
      <div className="flex justify-between items-center mb-8">
        <div>
          <h1 className="text-3xl font-bold text-slate-800">Developers</h1>
          <p className="text-slate-500 mt-2">
            Invita a tu equipo técnico al portal de integración (developers.facil-factura.pro). Todos comparten un único
            Client de prueba, aislado de tus clientes reales.
          </p>
        </div>
        <button
          onClick={() => setShowModal(true)}
          className="bg-blue-600 hover:bg-blue-700 text-white px-5 py-2.5 rounded-xl font-medium shadow-lg shadow-blue-500/30 flex items-center gap-2 transition-all"
        >
          <Plus size={20} />
          <span>Invitar Developer</span>
        </button>
      </div>

      <div className="bg-white rounded-2xl shadow-sm border border-slate-100 overflow-hidden">
        <table className="w-full text-left">
          <thead className="bg-slate-50 text-slate-500 text-xs uppercase tracking-wider font-medium border-b border-slate-100">
            <tr>
              <th className="px-6 py-4">Nombre</th>
              <th className="px-6 py-4">Email</th>
              <th className="px-6 py-4">Estado</th>
              <th className="px-6 py-4 text-right">Acciones</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100 text-sm">
            {developers.length === 0 && !loading && (
              <tr><td colSpan={4} className="text-center py-8 text-slate-500">Aún no has invitado a ningún developer.</td></tr>
            )}
            {developers.map(d => (
              <tr key={d.id} className="hover:bg-slate-50/80 transition-colors">
                <td className="px-6 py-4 font-medium text-slate-800 flex items-center gap-2">
                  <Code2 size={16} className="text-slate-400" /> {d.name}
                </td>
                <td className="px-6 py-4 text-slate-600">{d.email}</td>
                <td className="px-6 py-4">
                  {d.isActive ? (
                    <span className="px-3 py-1 rounded-full text-xs font-semibold text-emerald-700 bg-emerald-100 border border-emerald-200">Activo</span>
                  ) : (
                    <span className="px-3 py-1 rounded-full text-xs font-semibold text-rose-700 bg-rose-100 border border-rose-200">Revocado</span>
                  )}
                </td>
                <td className="px-6 py-4 flex items-center justify-end gap-3">
                  {d.isActive ? (
                    <>
                      <button onClick={() => handleResendInvitation(d.id)} className="p-1.5 text-slate-400 hover:text-blue-600 transition-colors bg-white rounded shadow-sm border border-slate-200" title="Reenviar invitación">
                        <Mail size={16} />
                      </button>
                      <button onClick={() => handleRevoke(d.id)} className="p-1.5 text-slate-400 hover:text-rose-600 transition-colors bg-white rounded shadow-sm border border-slate-200" title="Revocar acceso">
                        <Trash2 size={16} />
                      </button>
                    </>
                  ) : (
                    <button onClick={() => handleReactivate(d.id)} className="flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-semibold text-emerald-600 hover:text-emerald-700 transition-colors bg-white rounded shadow-sm border border-slate-200" title="Reactivar acceso">
                      <RotateCcw size={14} />
                      Reactivar
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {showModal && (
        <div className="fixed inset-0 bg-slate-900/50 backdrop-blur-sm z-50 flex items-center justify-center">
          <div className="bg-white rounded-2xl p-6 max-w-md w-full shadow-2xl">
            <h2 className="text-xl font-bold text-slate-800 mb-4">Invitar Developer</h2>
            <form onSubmit={handleSubmit} className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-slate-600 mb-1">Nombre</label>
                <input required type="text" className="w-full px-4 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500 outline-none" value={formData.name} onChange={e => setFormData({ ...formData, name: e.target.value })} />
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-600 mb-1">Email</label>
                <input required type="email" className="w-full px-4 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500 outline-none" value={formData.email} onChange={e => setFormData({ ...formData, email: e.target.value })} />
              </div>
              <p className="text-xs text-slate-400">Le enviaremos un enlace para que establezca su propia contraseña en el portal de developers.</p>
              <div className="flex gap-3 justify-end pt-4">
                <button type="button" onClick={() => setShowModal(false)} className="px-4 py-2 text-slate-500 hover:bg-slate-100 rounded-lg font-medium transition-colors">Cancelar</button>
                <button type="submit" disabled={saving} className="bg-blue-600 hover:bg-blue-700 text-white px-5 py-2 rounded-lg font-medium shadow-md transition-colors disabled:opacity-50">
                  {saving ? 'Enviando...' : 'Invitar'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
