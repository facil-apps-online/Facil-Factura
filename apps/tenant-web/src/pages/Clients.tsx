import React, { useState, useEffect } from 'react';
import { Plus, Edit2, Trash2, RefreshCw, Send, X, LogIn } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { api, getErrorMessage } from '../lib/api';
import { enterClientPortal } from '../lib/support';
import { toast } from 'sonner';
import ClientFormFields from '../components/ClientFormFields';
import DataTable, { type DataTableColumn } from '@shared/components/DataTable';

const initialNewClient = {
  companyName: '', personType: 'PJ', commercialName: '', taxId: '', verificationDigit: '',
  email: '', phone: '', address: '', city: '', cityCode: '' as string | null,
  organizationDepartment: '', organizationType: 'RM',
  legalRepresentativeFirstName: '', legalRepresentativeOtherNames: '', legalRepresentativeFirstLastName: '', legalRepresentativeSecondLastName: '',
  legalRepresentativeDocumentType: '', legalRepresentativeDocumentNumber: '', legalRepresentativeDocumentCountryCode: 'CO', legalRepresentativeEmail: '',
  legalRepresentativeRepresentationCode: '', legalRepresentativeStartDate: null as string | null,
  taxRegime: '', economicActivity: '', associateId: '' as string | null,
  isGranContribuyente: false, isAgenteRetenedorIva: false, isAutorretenedorRenta: false, incomeTaxRegime: '', foreignCurrencyEnabled: false, decimalSeparator: '.',
  latitude: null as number | null, longitude: null as number | null
};

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
  const [newClient, setNewClient] = useState(initialNewClient);
  const [associates, setAssociates] = useState<{ id: string, name: string, isActive: boolean }[]>([]);
  const [creating, setCreating] = useState(false);
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
    api.get('/tenant/associates').then(res => setAssociates(res.data)).catch(() => {});
  }, []);

  const openCreateModal = () => {
    setNewClient(initialNewClient);
    setShowModal(true);
  };

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    setCreating(true);
    try {
      const res = await api.post('/tenant/clients', { ...newClient, associateId: newClient.associateId || null });
      toast.success("Cliente creado exitosamente. Completa su información.");
      setShowModal(false);
      navigate(`/clients/edit/${res.data.id}`);
    } catch (err: any) {
      toast.error(getErrorMessage(err, "Error al crear cliente"));
    } finally {
      setCreating(false);
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

  const portalOrder: Record<Client['portalUserStatus'], number> = { Active: 0, Revoked: 1, NotInvited: 2 };

  const columns: DataTableColumn<Client>[] = [
    { key: 'companyName', header: 'Razón social', primary: true, cellClassName: 'font-medium text-slate-800', render: c => c.companyName, sortValue: c => c.companyName, searchValue: c => c.companyName },
    { key: 'taxId', header: 'NIT', cellClassName: 'text-slate-600 whitespace-nowrap', render: c => c.taxId, sortValue: c => c.taxId, searchValue: c => c.taxId },
    { key: 'email', header: 'Correo de recepción', cellClassName: 'text-slate-600', render: c => c.email, sortValue: c => c.email, searchValue: c => c.email },
    { key: 'associate', header: 'Asociado', cellClassName: 'text-slate-600', render: c => c.associateName || <span className="text-slate-400">Sin asociado</span>, sortValue: c => c.associateName, searchValue: c => c.associateName },
    { key: 'createdAt', header: 'Registrado', cellClassName: 'text-slate-600 whitespace-nowrap', render: c => formatDate(c.createdAt), sortValue: c => c.createdAt },
    {
      key: 'expiry',
      header: 'Vencimiento de resolución',
      render: c => (
        <button onClick={e => { e.stopPropagation(); navigate(`/clients/edit/${c.id}?tab=resolutions`); }} className="hover:opacity-75 transition-opacity">
          <ExpiryBadge value={c.nearestResolutionExpiry} />
        </button>
      ),
      sortValue: c => c.nearestResolutionExpiry,
    },
    {
      key: 'status',
      header: 'Estado',
      render: c => c.isActive ? (
        <span className="px-3 py-1 rounded-full text-xs font-semibold text-emerald-700 bg-emerald-100 border border-emerald-200">Activo</span>
      ) : (
        <span className="px-3 py-1 rounded-full text-xs font-semibold text-rose-700 bg-rose-100 border border-rose-200">Inactivo</span>
      ),
      sortValue: c => (c.isActive ? 0 : 1),
    },
    { key: 'portal', header: 'Portal', render: c => <PortalStatusBadge status={c.portalUserStatus} />, sortValue: c => portalOrder[c.portalUserStatus] },
  ];

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
          <h1 className="text-3xl font-bold text-slate-800">Clientes emisores</h1>
          <p className="text-slate-500 mt-2">Administra los negocios que emiten documentos desde tu cuenta.</p>
        </div>
        <button
          onClick={openCreateModal}
          className="bg-blue-600 hover:bg-blue-700 text-white px-5 py-2.5 rounded-xl font-medium shadow-lg shadow-blue-500/30 flex items-center gap-2 transition-all"
        >
          <Plus size={20} />
          <span>Nuevo cliente</span>
        </button>
      </div>

      {(selectedIds.size > 0 || notInvitedIds.length > 0) && (
        <div className="mb-4 px-4 py-3 rounded-2xl border border-blue-100 bg-blue-50/60 flex flex-wrap items-center justify-between gap-4">
          <div className="flex flex-wrap items-center gap-3 text-sm">
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

      <DataTable
        rows={clients}
        rowKey={c => c.id}
        loading={loading}
        tableFrom="xl"
        search="Buscar por nombre, NIT, correo..."
        defaultSort={{ key: 'companyName', dir: 'asc' }}
        emptyMessage="Aún no hay clientes registrados."
        toolbar={(
          <button
            onClick={loadClients}
            title="Actualizar"
            aria-label="Actualizar"
            className="text-slate-400 hover:text-blue-600 transition-colors p-2.5 bg-white rounded-lg border border-slate-200 shadow-sm"
          >
            <RefreshCw size={18} className={loading ? 'animate-spin' : ''} />
          </button>
        )}
        selection={{
          isSelected: c => selectedIds.has(c.id),
          onToggle: c => toggleSelected(c.id),
          onToggleMany: (rows, select) => setSelectedIds(prev => {
            const next = new Set(prev);
            rows.forEach(c => (select ? next.add(c.id) : next.delete(c.id)));
            return next;
          }),
          label: c => `Seleccionar a ${c.companyName}`,
        }}
        columns={columns}
        actions={client => (
          <>
            {client.isActive && (
              <button
                onClick={() => enterClientPortal(client.id)}
                className="p-1.5 text-slate-400 hover:text-emerald-600 transition-colors bg-white rounded shadow-sm border border-slate-200"
                title="Entrar al portal del cliente"
                aria-label={`Entrar al portal de ${client.companyName}`}
              >
                <LogIn size={16} />
              </button>
            )}
            <button
              onClick={() => navigate(`/clients/edit/${client.id}`)}
              className="p-1.5 text-slate-400 hover:text-blue-600 transition-colors bg-white rounded shadow-sm border border-slate-200"
              title="Editar"
              aria-label={`Editar ${client.companyName}`}
            >
              <Edit2 size={16} />
            </button>
            <button
              onClick={() => handleDelete(client.id)}
              className="p-1.5 text-slate-400 hover:text-rose-600 transition-colors bg-white rounded shadow-sm border border-slate-200"
              title="Eliminar"
              aria-label={`Eliminar ${client.companyName}`}
            >
              <Trash2 size={16} />
            </button>
          </>
        )}
      />
      {/* Modal Nuevo Cliente */}
      {showModal && (
        <div className="fixed inset-0 bg-slate-900/50 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-3xl w-full max-w-3xl shadow-2xl overflow-hidden animate-in zoom-in-95 duration-200 max-h-[90vh] flex flex-col">
            <div className="px-6 py-4 border-b border-slate-100 flex justify-between items-center bg-slate-50/50 shrink-0">
              <h2 className="text-xl font-bold text-slate-800">Crear Nuevo Emisor</h2>
              <button onClick={() => setShowModal(false)} className="text-slate-400 hover:text-slate-600 p-2"><X size={20} /></button>
            </div>
            <form onSubmit={handleCreate} className="p-6 overflow-y-auto">
              <ClientFormFields client={newClient} setClient={setNewClient} associates={associates} />
              <div className="flex gap-3 justify-end pt-6 border-t border-slate-100 mt-6">
                <button type="button" onClick={() => setShowModal(false)} className="px-4 py-2 text-slate-500 hover:bg-slate-100 rounded-lg font-medium transition-colors">Cancelar</button>
                <button type="submit" disabled={creating} className="bg-blue-600 hover:bg-blue-700 text-white px-5 py-2 rounded-lg font-medium shadow-md transition-colors flex items-center gap-2 disabled:opacity-50">
                  {creating ? 'Creando...' : 'Crear y Continuar'} <Edit2 size={16}/>
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
