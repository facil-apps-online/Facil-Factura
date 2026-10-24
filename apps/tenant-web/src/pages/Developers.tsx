import React, { useState, useEffect } from 'react';
import { Plus, Trash2, Code2, Mail, Link2, RotateCcw } from 'lucide-react';
import { api } from '../lib/api';
import { toast } from 'sonner';
import DataTable, { type DataTableColumn } from '@shared/components/DataTable';
import { copyText } from '@shared/lib/clipboard';

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

  // El enlace de invitación para mandarlo por otro medio (WhatsApp); si ya venció, el servidor genera uno nuevo.
  const handleCopyInvitationLink = async (d: Developer) => {
    try {
      const { link } = (await api.post(`/tenant/developers/${d.id}/invitation-link`)).data as { link: string };
      if (await copyText(link)) toast.success(`Enlace de invitación de ${d.name} copiado`);
      else toast.message('Copie el enlace de invitación:', { description: link, duration: 30000 });
    } catch (err: any) {
      toast.error(err.response?.data || 'Error al obtener el enlace de invitación');
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

  const columns: DataTableColumn<Developer>[] = [
    {
      key: 'name',
      header: 'Nombre',
      primary: true,
      cellClassName: 'font-medium text-slate-800',
      render: d => <span className="inline-flex items-center gap-2"><Code2 size={16} className="text-slate-400" /> {d.name}</span>,
      sortValue: d => d.name,
      searchValue: d => d.name,
    },
    { key: 'email', header: 'Email', cellClassName: 'text-slate-600', render: d => d.email, sortValue: d => d.email, searchValue: d => d.email },
    {
      key: 'status',
      header: 'Estado',
      render: d => d.isActive ? (
        <span className="px-3 py-1 rounded-full text-xs font-semibold text-emerald-700 bg-emerald-100 border border-emerald-200">Activo</span>
      ) : (
        <span className="px-3 py-1 rounded-full text-xs font-semibold text-rose-700 bg-rose-100 border border-rose-200">Revocado</span>
      ),
      sortValue: d => (d.isActive ? 0 : 1),
    },
  ];

  return (
    <div className="p-10 h-full overflow-y-auto animate-in fade-in slide-in-from-bottom-4 duration-500">
      <div className="flex justify-between items-center mb-8">
        <div>
          <h1 className="text-3xl font-bold text-slate-800">Desarrolladores</h1>
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
          <span>Invitar desarrollador</span>
        </button>
      </div>

      <DataTable
        rows={developers}
        rowKey={d => d.id}
        loading={loading}
        tableFrom="md"
        search="Buscar por nombre o correo..."
        defaultSort={{ key: 'name', dir: 'asc' }}
        emptyMessage="Aún no has invitado a ningún desarrollador."
        columns={columns}
        actions={d => (d.isActive ? (
          <>
            <button onClick={() => handleResendInvitation(d.id)} className="p-1.5 text-slate-400 hover:text-blue-600 transition-colors bg-white rounded shadow-sm border border-slate-200" title="Reenviar invitación" aria-label={`Reenviar invitación a ${d.name}`}>
              <Mail size={16} />
            </button>
            <button onClick={() => handleCopyInvitationLink(d)} className="p-1.5 text-slate-400 hover:text-blue-600 transition-colors bg-white rounded shadow-sm border border-slate-200" title="Copiar enlace de invitación" aria-label={`Copiar enlace de invitación de ${d.name}`}>
              <Link2 size={16} />
            </button>
            <button onClick={() => handleRevoke(d.id)} className="p-1.5 text-slate-400 hover:text-rose-600 transition-colors bg-white rounded shadow-sm border border-slate-200" title="Revocar acceso" aria-label={`Revocar acceso de ${d.name}`}>
              <Trash2 size={16} />
            </button>
          </>
        ) : (
          <button onClick={() => handleReactivate(d.id)} className="flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-semibold text-emerald-600 hover:text-emerald-700 transition-colors bg-white rounded shadow-sm border border-slate-200" title="Reactivar acceso">
            <RotateCcw size={14} />
            Reactivar
          </button>
        ))}
      />

      {showModal && (
        <div className="fixed inset-0 bg-slate-900/50 backdrop-blur-sm z-50 flex items-center justify-center">
          <div className="bg-white rounded-2xl p-6 max-w-md w-full shadow-2xl">
            <h2 className="text-xl font-bold text-slate-800 mb-4">Invitar desarrollador</h2>
            <form onSubmit={handleSubmit} className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-slate-600 mb-1">Nombre</label>
                <input required type="text" className="w-full px-4 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500 outline-none" value={formData.name} onChange={e => setFormData({ ...formData, name: e.target.value })} />
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-600 mb-1">Email</label>
                <input required type="email" className="w-full px-4 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500 outline-none" value={formData.email} onChange={e => setFormData({ ...formData, email: e.target.value })} />
              </div>
              <p className="text-xs text-slate-400">Enviaremos un enlace para crear la contraseña y entrar al portal de desarrolladores.</p>
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
