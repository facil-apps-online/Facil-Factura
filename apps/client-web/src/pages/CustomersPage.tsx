import React, { useState, useEffect } from 'react';
import { Plus, Edit2, Trash2, X, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { api } from '../lib/api';

export default function CustomersPage() {
  const [customers, setCustomers] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingCustomer, setEditingCustomer] = useState<any>(null);
  
  const initialForm = {
    name: '', identificationType: '13', identificationNumber: '', verificationDigit: '',
    email: '', phone: '', address: '', cityCode: '', taxRegime: '49', fiscalResponsibilities: 'R-99-PN'
  };
  const [formData, setFormData] = useState(initialForm);

  useEffect(() => {
    loadCustomers();
  }, []);

  const loadCustomers = () => {
    api.get('/client/customers')
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
    if (customer) {
      setEditingCustomer(customer);
      setFormData(customer);
    } else {
      setEditingCustomer(null);
      setFormData(initialForm);
    }
    setIsModalOpen(true);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      if (editingCustomer) {
        await api.put(`/client/customers/${editingCustomer.id}`, formData);
        toast.success('Cliente actualizado');
      } else {
        await api.post('/client/customers', formData);
        toast.success('Cliente creado');
      }
      setIsModalOpen(false);
      loadCustomers();
    } catch (err: any) {
      toast.error(err.response?.data || 'Error guardando cliente');
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm('¿Estás seguro de eliminar este cliente?')) return;
    try {
      await api.delete(`/client/customers/${id}`);
      toast.success('Cliente eliminado');
      loadCustomers();
    } catch (err: any) {
      toast.error('Error al eliminar');
    }
  };

  if (loading) return <div className="flex justify-center p-12"><Loader2 className="animate-spin w-8 h-8 text-primary" /></div>;

  return (
    <div className="p-8 max-w-7xl mx-auto">
      <div className="flex justify-between items-center mb-8">
        <div>
          <h1 className="text-3xl font-extrabold text-slate-800">Mis Clientes</h1>
          <p className="text-slate-500 mt-1">Catálogo de adquirentes para facturación</p>
        </div>
        <button 
          onClick={() => handleOpenModal()}
          className="bg-primary hover:bg-primary/90 text-white px-5 py-2.5 rounded-xl font-bold flex items-center gap-2 shadow-sm transition-all"
        >
          <Plus size={20} /> Nuevo Cliente
        </button>
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
            {customers.map(c => (
              <tr key={c.id} className="hover:bg-slate-50/50 transition-colors">
                <td className="p-4 font-medium text-slate-700">
                  {c.identificationType === '31' ? 'NIT' : 'CC'} {c.identificationNumber}
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
            {customers.length === 0 && (
              <tr>
                <td colSpan={5} className="p-8 text-center text-slate-500">No tienes clientes registrados.</td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {isModalOpen && (
        <div className="fixed inset-0 bg-slate-900/50 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-3xl w-full max-w-2xl shadow-2xl overflow-hidden animate-in zoom-in-95 duration-200">
            <div className="px-6 py-4 border-b border-slate-100 flex justify-between items-center bg-slate-50/50">
              <h3 className="text-xl font-bold text-slate-800">{editingCustomer ? 'Editar Cliente' : 'Nuevo Cliente'}</h3>
              <button onClick={() => setIsModalOpen(false)} className="text-slate-400 hover:text-slate-600 p-2"><X size={20} /></button>
            </div>
            <form onSubmit={handleSubmit} className="p-6">
              <div className="grid grid-cols-2 gap-4 mb-4">
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-1">Nombre o Razón Social</label>
                  <input type="text" required value={formData.name} onChange={e => setFormData({...formData, name: e.target.value})} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none" />
                </div>
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-1">Email</label>
                  <input type="email" required value={formData.email} onChange={e => setFormData({...formData, email: e.target.value})} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none" />
                </div>
                
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-1">Tipo de Identificación</label>
                  <select value={formData.identificationType} onChange={e => setFormData({...formData, identificationType: e.target.value})} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none bg-white">
                    <option value="13">Cédula de Ciudadanía</option>
                    <option value="31">NIT</option>
                    <option value="22">Cédula de Extranjería</option>
                  </select>
                </div>
                <div className="flex gap-2">
                  <div className="flex-1">
                    <label className="block text-sm font-bold text-slate-700 mb-1">Número</label>
                    <input type="text" required value={formData.identificationNumber} onChange={e => setFormData({...formData, identificationNumber: e.target.value})} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none" />
                  </div>
                  {formData.identificationType === '31' && (
                    <div className="w-20">
                      <label className="block text-sm font-bold text-slate-700 mb-1">DV</label>
                      <input type="text" value={formData.verificationDigit} onChange={e => setFormData({...formData, verificationDigit: e.target.value})} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none text-center" />
                    </div>
                  )}
                </div>

                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-1">Régimen</label>
                  <select value={formData.taxRegime} onChange={e => setFormData({...formData, taxRegime: e.target.value})} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none bg-white">
                    <option value="49">No Responsable de IVA</option>
                    <option value="48">Responsable de IVA</option>
                  </select>
                </div>
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-1">Teléfono</label>
                  <input type="text" value={formData.phone} onChange={e => setFormData({...formData, phone: e.target.value})} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none" />
                </div>
              </div>
              <div className="mt-6 flex justify-end gap-3 pt-4 border-t border-slate-100">
                <button type="button" onClick={() => setIsModalOpen(false)} className="px-5 py-2 text-slate-600 font-bold hover:bg-slate-100 rounded-xl transition-colors">Cancelar</button>
                <button type="submit" className="px-5 py-2 bg-primary text-white font-bold rounded-xl hover:bg-primary/90 transition-colors shadow-md">Guardar Cliente</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
