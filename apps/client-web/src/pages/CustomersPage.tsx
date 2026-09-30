import React, { useState, useEffect } from 'react';
import { Plus, Edit2, Trash2, Loader2, Search } from 'lucide-react';
import { toast } from 'sonner';
import { api } from '../lib/api';
import ImportExcelButton from '../components/ImportExcelButton';
import CustomerFormModal from '../components/CustomerFormModal';
import { useConfirm } from '@shared/components/ConfirmDialog';

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

  return (
    <div className="p-8">
      <div className="flex justify-between items-center mb-8">
        <div>
          <h1 className="text-3xl font-extrabold text-slate-800">Terceros</h1>
          <p className="text-slate-500 mt-1">Administra clientes, proveedores y empleados.</p>
        </div>
        <div className="flex gap-3">
          <ImportExcelButton endpoint="/client/customers/import" templateEndpoint="/client/customers/template" label="Importar Excel" onDone={loadCustomers} />
          <button
            onClick={() => handleOpenModal()}
            className="bg-primary hover:bg-primary/90 text-white px-5 py-2.5 rounded-xl font-bold flex items-center gap-2 shadow-sm transition-all"
          >
            <Plus size={20} /> Nuevo {activeType}
          </button>
        </div>
      </div>

      <div className="flex gap-2 mb-6 items-center">
        {PARTY_TYPES.map(pt => (
          <button
            key={pt.value}
            onClick={() => setActiveType(pt.value)}
            className={`px-4 py-2 rounded-xl font-bold text-sm transition-colors ${
              activeType === pt.value ? 'bg-primary text-white shadow-sm' : 'bg-white text-slate-500 border border-slate-200 hover:bg-slate-50'
            }`}
          >
            {pt.label}
          </button>
        ))}
        <div className="relative ml-auto w-80">
          <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
          <input
            type="text"
            placeholder="Buscar por nombre, identificación, email o teléfono..."
            value={search}
            onChange={e => setSearch(e.target.value)}
            className="w-full pl-9 pr-3 py-2 bg-white border border-slate-200 rounded-xl text-sm outline-none focus:ring-2 focus:ring-primary"
          />
        </div>
      </div>

      <div className="bg-white rounded-2xl shadow-sm border border-slate-200 overflow-hidden">
        <table className="w-full text-left border-collapse">
          <thead>
            <tr className="bg-slate-50 border-b border-slate-200 text-sm font-bold text-slate-500 uppercase tracking-wider">
              <th className="p-4">Identificación</th>
              <th className="p-4">Razón Social / Nombre</th>
              <th className="p-4">Contacto</th>
              <th className="p-4">Régimen</th>
              <th className="p-4 text-right">Acciones</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {filteredCustomers.map(c => (
              <tr key={c.id} className="hover:bg-slate-50/50 transition-colors">
                <td className="p-4 font-medium text-slate-700">
                  {identificationTypeOptions.find(o => o.value === c.identificationType)?.label || c.identificationType} {c.identificationNumber}
                  {c.verificationDigit && `-${c.verificationDigit}`}
                </td>
                <td className="p-4 text-slate-900 font-bold">{c.name}</td>
                <td className="p-4 text-slate-500 text-sm">
                  {c.email}<br/>{c.phone}
                </td>
                <td className="p-4 text-slate-500 text-sm">
                  {c.taxRegime === '48' ? 'Resp. IVA' : 'No Resp. IVA'}
                </td>
                <td className="p-4 flex items-center justify-end gap-2">
                  <button onClick={() => handleOpenModal(c)} className="p-2 text-slate-400 hover:text-blue-600 hover:bg-blue-50 rounded-lg transition-colors">
                    <Edit2 size={18} />
                  </button>
                  <button onClick={() => handleDelete(c.id)} className="p-2 text-slate-400 hover:text-rose-600 hover:bg-rose-50 rounded-lg transition-colors">
                    <Trash2 size={18} />
                  </button>
                </td>
              </tr>
            ))}
            {filteredCustomers.length === 0 && (
              <tr>
                <td colSpan={5} className="p-8 text-center text-slate-500">
                  {q ? 'Ningún resultado para tu búsqueda.' : `No tienes ${activeType.toLowerCase()}s registrados.`}
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

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
