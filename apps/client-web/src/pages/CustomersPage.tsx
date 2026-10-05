import React, { useState, useEffect } from 'react';
import { Plus, Loader2, Search } from 'lucide-react';
import { toast } from 'sonner';
import { api } from '../lib/api';
import ImportExcelButton from '../components/ImportExcelButton';
import CustomerFormModal from '../components/CustomerFormModal';
import ResponsiveList, { type ResponsiveListColumn } from '../components/ResponsiveList';
import RowIconButton from '../components/RowIconButton';
import { useConfirm } from '@/components/ConfirmDialog';

const PARTY_TYPES = [
  { value: 'Cliente', label: 'Clientes' },
  { value: 'Proveedor', label: 'Proveedores' },
  { value: 'Empleado', label: 'Empleados' }
];

export default function CustomersPage() {
  const confirm = useConfirm();
  const [customers, setCustomers] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingCustomer, setEditingCustomer] = useState<any>(null);
  const [activeType, setActiveType] = useState('Cliente');
  const [search, setSearch] = useState('');
  const [identificationTypeOptions, setIdentificationTypeOptions] = useState<{ value: string, label: string }[]>([]);

  useEffect(() => {
    loadCustomers();
  }, [activeType]);

  useEffect(() => {
    api.get('/client/identification-types')
      .then(res => setIdentificationTypeOptions(res.data.map((t: any) => ({ value: t.code, label: t.name }))))
      .catch(() => {});
  }, []);

  const loadCustomers = () => {
    setLoading(true);
    api.get(`/client/customers?partyType=${activeType}`)
      .then(res => {
        setCustomers(res.data);
        setLoading(false);
      })
      .catch(() => {
        toast.error('Error al cargar clientes');
        setLoading(false);
      });
  };

  const handleOpenModal = (customer?: any) => {
    setEditingCustomer(customer || null);
    setIsModalOpen(true);
  };

  const handleSaved = () => {
    setIsModalOpen(false);
    loadCustomers();
  };

  const handleDelete = async (id: string) => {
    if (!(await confirm('¿Estás seguro de eliminar este cliente?'))) return;
    try {
      await api.delete(`/client/customers/${id}`);
      toast.success('Cliente eliminado');
      loadCustomers();
    } catch (err: any) {
      toast.error('Error al eliminar');
    }
  };

  if (loading) return <div className="flex justify-center p-12"><Loader2 className="animate-spin w-8 h-8 text-primary" /></div>;

  const q = search.trim().toLowerCase();
  const filteredCustomers = q
    ? customers.filter(c => [c.name, c.identificationNumber, c.email, c.phone].some((f: string) => f?.toLowerCase().includes(q)))
    : customers;

  const columns: ResponsiveListColumn<any>[] = [
    {
      key: 'identification',
      header: 'Identificación',
      cellClassName: 'whitespace-nowrap font-medium text-slate-700',
      render: c => (
        <>
          {identificationTypeOptions.find(o => o.value === c.identificationType)?.label || c.identificationType} {c.identificationNumber}
          {c.verificationDigit && `-${c.verificationDigit}`}
        </>
      )
    },
    {
      key: 'name',
      header: 'Razón Social / Nombre',
      primary: true,
      cellClassName: 'font-bold text-slate-900',
      render: c => c.name
    },
    {
      key: 'contact',
      header: 'Contacto',
      cellClassName: 'text-sm text-slate-500',
      render: c => (
        <>
          {c.email}<br />{c.phone}
        </>
      )
    },
    {
      key: 'regime',
      header: 'Régimen',
      cellClassName: 'whitespace-nowrap text-sm text-slate-500',
      render: c => (c.taxRegime === '48' ? 'Resp. IVA' : 'No Resp. IVA')
    }
  ];

  return (
    <div className="p-4 sm:p-6 lg:p-8">
      <div className="mb-6 flex flex-col gap-4 sm:mb-8 sm:flex-row sm:items-center sm:justify-between">
        <div className="min-w-0">
          <h1 className="text-2xl font-extrabold text-slate-800 sm:text-3xl">Terceros</h1>
          <p className="mt-1 text-slate-500">Administra clientes, proveedores y empleados.</p>
        </div>
        <div className="flex flex-wrap gap-3">
          <ImportExcelButton endpoint="/client/customers/import" templateEndpoint="/client/customers/template" label="Importar Excel" onDone={loadCustomers} />
          <button
            onClick={() => handleOpenModal()}
            className="bg-primary hover:bg-primary/90 text-white px-5 py-2.5 rounded-xl font-bold flex items-center gap-2 shadow-sm transition-all"
          >
            <Plus size={20} /> Nuevo {activeType}
          </button>
        </div>
      </div>

      <div className="mb-6 flex flex-wrap items-center gap-2">
        {PARTY_TYPES.map(pt => (
          <button
            key={pt.value}
            onClick={() => setActiveType(pt.value)}
            className={`px-4 py-2.5 rounded-xl font-bold text-sm transition-colors ${
              activeType === pt.value ? 'bg-primary text-white shadow-sm' : 'bg-white text-slate-500 border border-slate-200 hover:bg-slate-50'
            }`}
          >
            {pt.label}
          </button>
        ))}
        <div className="relative w-full sm:ml-auto sm:w-80">
          <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
          <input
            type="text"
            placeholder="Buscar por nombre, identificación, email o teléfono..."
            aria-label="Buscar terceros"
            value={search}
            onChange={e => setSearch(e.target.value)}
            className="w-full pl-9 pr-3 py-2.5 bg-white border border-slate-200 rounded-xl text-sm outline-none focus:ring-2 focus:ring-primary"
          />
        </div>
      </div>

      <ResponsiveList
        rows={filteredCustomers}
        columns={columns}
        rowKey={c => c.id}
        tableFrom="wide"
        emptyMessage={q ? 'Ningún resultado para tu búsqueda.' : `No tienes ${activeType.toLowerCase()}s registrados.`}
        actions={c => (
          <>
            <RowIconButton action="edit" label={`Editar ${c.name}`} onClick={() => handleOpenModal(c)} />
            <RowIconButton action="delete" label={`Eliminar ${c.name}`} onClick={() => handleDelete(c.id)} />
          </>
        )}
      />

      <CustomerFormModal
        open={isModalOpen}
        editingCustomer={editingCustomer}
        defaultPartyType={activeType}
        onClose={() => setIsModalOpen(false)}
        onSaved={handleSaved}
      />
    </div>
  );
}
