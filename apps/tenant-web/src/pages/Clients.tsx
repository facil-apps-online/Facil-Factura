import React, { useState, useEffect } from 'react';
import { Plus, Edit2, Trash2, Search, RefreshCw, Send } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { api } from '../lib/api';
import { toast } from 'sonner';

interface Client {
  id: string;
  companyName: string;
  taxId: string;
  email: string;
  isActive: boolean;
  createdAt: string;
  nearestResolutionExpiry: string | null;
  associateId: string | null;
  associateName: string | null;
  portalUserStatus: 'NotInvited' | 'Active' | 'Revoked';
}

function PortalStatusBadge({ status }: { status: Client['portalUserStatus'] }) {
  if (status === 'Active') {
    return <span className="px-2.5 py-1 rounded-full text-xs font-semibold text-emerald-700 bg-emerald-100 border border-emerald-200">Invitado</span>;
  }
  if (status === 'Revoked') {
    return <span className="px-2.5 py-1 rounded-full text-xs font-semibold text-rose-700 bg-rose-100 border border-rose-200">Revocado</span>;
  }
  return <span className="px-2.5 py-1 rounded-full text-xs font-semibold text-slate-500 bg-slate-100 border border-slate-200">Sin invitar</span>;
}

function formatDate(value: string) {
  return new Date(value).toLocaleDateString('es-CO', { year: 'numeric', month: 'short', day: 'numeric' });
}

function ExpiryBadge({ value }: { value: string | null }) {
  if (!value) {
    return <span className="text-slate-400 text-xs">Sin resoluciones</span>;
  }
  const days = Math.ceil((new Date(value).getTime() - Date.now()) / (1000 * 60 * 60 * 24));
  const colorClass = days < 0
    ? 'text-rose-700 bg-rose-100 border-rose-200'
    : days < 7
      ? 'text-rose-700 bg-rose-100 border-rose-200'
      : days < 30
        ? 'text-amber-700 bg-amber-100 border-amber-200'
        : 'text-slate-600 bg-slate-100 border-slate-200';

  return (
    <span className={`px-2.5 py-1 rounded-full text-xs font-semibold border ${colorClass}`}>
      {formatDate(value)}
    </span>
  );
}

export default function Clients() {
  const [clients, setClients] = useState<Client[]>([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [newClient, setNewClient] = useState({ companyName: '', taxId: '', email: '' });
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [bulkInviting, setBulkInviting] = useState(false);
  const navigate = useNavigate();

  const loadClients = () => {
    setLoading(true);
    api.get('/tenant/clients')
      .then(res => setClients(res.data))
      .catch(err => toast.error("Error al cargar los clientes del servidor"))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    loadClients();
  }, []);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      const res = await api.post('/tenant/clients', newClient);
      toast.success("Cliente creado exitosamente. Completa su información.");
      setShowModal(false);
      navigate(`/clients/edit/${res.data.id}`);
    } catch (err: any) {
      toast.error(err.response?.data || "Error al crear cliente");
    }
  };

  const handleDelete = async (id: string) => {
    if (window.confirm('¿Estás seguro de eliminar este emisor?')) {
      try {
        await api.delete(`/tenant/clients/${id}`);
        toast.success("Emisor eliminado correctamente");
        loadClients();
      } catch (err) {
        toast.error("Error al eliminar emisor");
      }
    }
  };

  const notInvitedIds = clients.filter(c => c.portalUserStatus === 'NotInvited').map(c => c.id);

  const toggleSelected = (id: string) => {
    setSelectedIds(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
  };

  const selectAllNotInvited = () => setSelectedIds(new Set(notInvitedIds));
  const clearSelection = () => setSelectedIds(new Set());

  const handleBulkInvite = async () => {
    if (selectedIds.size === 0) return;
    setBulkInviting(true);
    try {
      const res = await api.post('/tenant/clients/bulk-invite', { clientIds: Array.from(selectedIds) });
      const { invitedCount, skippedAlreadyInvited, skippedNoEmail } = res.data;
      let message = `${invitedCount} invitación${invitedCount === 1 ? '' : 'es'} enviada${invitedCount === 1 ? '' : 's'}.`;
      if (skippedAlreadyInvited?.length) message += ` ${skippedAlreadyInvited.length} ya tenían acceso.`;
      if (skippedNoEmail?.length) message += ` ${skippedNoEmail.length} sin correo registrado.`;
      toast.success(message);
      clearSelection();
      loadClients();
    } catch (err: any) {
      toast.error(err.response?.data || 'Error al enviar las invitaciones masivas.');
    } finally {
      setBulkInviting(false);
    }
  };

  return (
    <div className="p-10 h-full overflow-y-auto animate-in fade-in slide-in-from-bottom-4 duration-500">
      <div className="flex justify-between items-center mb-8">
        <div>
          <h1 className="text-3xl font-bold text-slate-800">Gestión de Clientes (Emisores)</h1>
          <p className="text-slate-500 mt-2">Administra los negocios que emitirán facturas bajo tu cuenta.</p>
        </div>
        <button 
          onClick={() => setShowModal(true)}
          className="bg-blue-600 hover:bg-blue-700 text-white px-5 py-2.5 rounded-xl font-medium shadow-lg shadow-blue-500/30 flex items-center gap-2 transition-all"
        >
          <Plus size={20} />
          <span>Nuevo Cliente</span>
        </button>
      </div>

      <div className="bg-white rounded-2xl shadow-sm border border-slate-100 overflow-hidden">
        <div className="p-4 border-b border-slate-100 flex justify-between items-center bg-slate-50/50">
          <div className="relative w-72">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" size={18} />
            <input
              type="text"
              placeholder="Buscar por nombre o NIT..."
              className="w-full pl-10 pr-4 py-2 rounded-lg border border-slate-200 focus:outline-none focus:ring-2 focus:ring-blue-500/20 focus:border-blue-500 text-sm"
            />
          </div>
          <button
            onClick={loadClients}
            className="text-slate-400 hover:text-blue-600 transition-colors p-2 bg-white rounded-lg border border-slate-200 shadow-sm"
          >
            <RefreshCw size={18} className={loading ? 'animate-spin' : ''} />
          </button>
        </div>

        {(selectedIds.size > 0 || notInvitedIds.length > 0) && (
          <div className="px-4 py-3 border-b border-slate-100 bg-blue-50/60 flex items-center justify-between gap-4">
            <div className="flex items-center gap-3 text-sm">
              {selectedIds.size > 0 ? (
                <>
                  <span className="font-medium text-slate-700">{selectedIds.size} seleccionado{selectedIds.size === 1 ? '' : 's'}</span>
                  <button onClick={clearSelection} className="text-slate-500 hover:text-slate-700 underline">Limpiar</button>
                </>
              ) : (
                <span className="text-slate-500">{notInvitedIds.length} cliente{notInvitedIds.length === 1 ? '' : 's'} sin invitar al portal</span>
              )}
              {notInvitedIds.length > 0 && (
                <button onClick={selectAllNotInvited} className="text-blue-600 hover:text-blue-700 font-medium underline">
                  Seleccionar todos los sin invitar
                </button>
              )}
            </div>
            <button
              onClick={handleBulkInvite}
              disabled={selectedIds.size === 0 || bulkInviting}
              className="flex items-center gap-2 bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg font-medium text-sm shadow-sm transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
            >
              <Send size={16} />
              {bulkInviting ? 'Enviando...' : 'Invitar seleccionados al portal'}
            </button>
          </div>
        )}

        <table className="w-full text-left">
          <thead className="bg-slate-50 text-slate-500 text-xs uppercase tracking-wider font-medium border-b border-slate-100">
            <tr>
              <th className="px-4 py-4 w-10">
                <input
                  type="checkbox"
                  checked={notInvitedIds.length > 0 && notInvitedIds.every(cid => selectedIds.has(cid))}
                  onChange={e => e.target.checked ? selectAllNotInvited() : clearSelection()}
                  className="rounded border-slate-300"
                />
              </th>
              <th className="px-6 py-4">Razón Social</th>
              <th className="px-6 py-4">NIT</th>
              <th className="px-6 py-4">Correo Recepción</th>
              <th className="px-6 py-4">Asociado</th>
              <th className="px-6 py-4">Registrado</th>
              <th className="px-6 py-4">Próx. Vencimiento Resolución</th>
              <th className="px-6 py-4">Estado</th>
              <th className="px-6 py-4">Portal</th>
              <th className="px-6 py-4 text-right">Acciones</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100 text-sm">
            {clients.length === 0 && !loading && (
              <tr><td colSpan={10} className="text-center py-8 text-slate-500">No hay clientes registrados aún.</td></tr>
            )}
            {clients.map(client => (
              <tr key={client.id} className="hover:bg-slate-50/80 transition-colors">
                <td className="px-4 py-4">
                  <input
                    type="checkbox"
                    checked={selectedIds.has(client.id)}
                    onChange={() => toggleSelected(client.id)}
                    className="rounded border-slate-300"
                  />
                </td>
                <td className="px-6 py-4 font-medium text-slate-800">{client.companyName}</td>
                <td className="px-6 py-4 text-slate-600">{client.taxId}</td>
                <td className="px-6 py-4 text-slate-600">{client.email}</td>
                <td className="px-6 py-4 text-slate-600">{client.associateName || <span className="text-slate-400">Sin asociado</span>}</td>
                <td className="px-6 py-4 text-slate-600 text-sm">{formatDate(client.createdAt)}</td>
                <td className="px-6 py-4">
                  <button onClick={() => navigate(`/clients/edit/${client.id}?tab=resolutions`)} className="hover:opacity-75 transition-opacity">
                    <ExpiryBadge value={client.nearestResolutionExpiry} />
                  </button>
                </td>
                <td className="px-6 py-4">
                  {client.isActive ? (
                    <span className="px-3 py-1 rounded-full text-xs font-semibold text-emerald-700 bg-emerald-100 border border-emerald-200">
                      Activo
                    </span>
                  ) : (
                    <span className="px-3 py-1 rounded-full text-xs font-semibold text-rose-700 bg-rose-100 border border-rose-200">
                      Inactivo
                    </span>
                  )}
                </td>
                <td className="px-6 py-4">
                  <PortalStatusBadge status={client.portalUserStatus} />
                </td>
                <td className="px-6 py-4 flex items-center justify-end gap-3">
                  <button
                    onClick={() => navigate(`/clients/edit/${client.id}`)}
                    className="p-1.5 text-slate-400 hover:text-blue-600 transition-colors bg-white rounded shadow-sm border border-slate-200"
                    title="Editar"
                  >
                    <Edit2 size={16} />
                  </button>
                  <button
                    onClick={() => handleDelete(client.id)}
                    className="p-1.5 text-slate-400 hover:text-rose-600 transition-colors bg-white rounded shadow-sm border border-slate-200"
                    title="Eliminar"
                  >
                    <Trash2 size={16} />
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {/* Modal Nuevo Cliente */}
      {showModal && (
        <div className="fixed inset-0 bg-slate-900/50 backdrop-blur-sm z-50 flex items-center justify-center">
          <div className="bg-white rounded-2xl p-6 max-w-md w-full shadow-2xl">
            <h2 className="text-xl font-bold text-slate-800 mb-4">Crear Nuevo Emisor</h2>
            <form onSubmit={handleCreate} className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-slate-600 mb-1">NIT</label>
                <input required type="text" className="w-full px-4 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500 outline-none" value={newClient.taxId} onChange={e => setNewClient({...newClient, taxId: e.target.value})} />
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-600 mb-1">Razón Social</label>
                <input required type="text" className="w-full px-4 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500 outline-none" value={newClient.companyName} onChange={e => setNewClient({...newClient, companyName: e.target.value})} />
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-600 mb-1">Correo Electrónico</label>
                <input required type="email" className="w-full px-4 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500 outline-none" value={newClient.email} onChange={e => setNewClient({...newClient, email: e.target.value})} />
              </div>
              <div className="flex gap-3 justify-end pt-4">
                <button type="button" onClick={() => setShowModal(false)} className="px-4 py-2 text-slate-500 hover:bg-slate-100 rounded-lg font-medium transition-colors">Cancelar</button>
                <button type="submit" className="bg-blue-600 hover:bg-blue-700 text-white px-5 py-2 rounded-lg font-medium shadow-md transition-colors flex items-center gap-2">Crear y Continuar <Edit2 size={16}/></button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
