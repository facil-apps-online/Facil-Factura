import React, { useEffect, useState } from 'react';
import { Plus, Save, Trash2, Pencil, ToggleLeft, ToggleRight, BadgeCheck } from 'lucide-react';
import { toast } from 'sonner';
import { api } from './api';
import DataTable, { type DataTableColumn } from '@shared/components/DataTable';

interface IdentificationTypeItem {
  id: string;
  code: string;
  name: string;
  isActive: boolean;
  dataicoCode: string | null;
  dataicoPayrollCode: string | null;
}

const equivalence = (code?: string | null) =>
  code ? (
    <span className="inline-flex items-center px-3 py-1 rounded-lg text-sm font-bold bg-fuchsia-500/20 text-fuchsia-400 border border-fuchsia-500/30 font-mono">{code}</span>
  ) : (
    <span className="text-slate-500 text-sm">—</span>
  );

export const IdentificationTypes = () => {
  const [items, setItems] = useState<IdentificationTypeItem[]>([]);
  const [formData, setFormData] = useState({ code: '', name: '', dataicoCode: '', dataicoPayrollCode: '' });
  const [showModal, setShowModal] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);

  const loadItems = () => {
    api.get<IdentificationTypeItem[]>('/identification-types')
      .then(res => setItems(res.data))
      .catch(() => toast.error('Error al cargar los tipos de identificación'));
  };

  useEffect(() => {
    loadItems();
  }, []);

  const closeModal = () => {
    setShowModal(false);
    setEditingId(null);
    setFormData({ code: '', name: '', dataicoCode: '', dataicoPayrollCode: '' });
  };

  const openCreateModal = () => {
    setEditingId(null);
    setFormData({ code: '', name: '', dataicoCode: '', dataicoPayrollCode: '' });
    setShowModal(true);
  };

  const startEdit = (item: IdentificationTypeItem) => {
    setEditingId(item.id);
    setFormData({ code: item.code, name: item.name, dataicoCode: item.dataicoCode || '', dataicoPayrollCode: item.dataicoPayrollCode || '' });
    setShowModal(true);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      if (editingId) {
        await api.put(`/identification-types/${editingId}`, { ...formData, isActive: items.find(i => i.id === editingId)?.isActive ?? true });
        toast.success('Tipo de identificación actualizado');
      } else {
        await api.post('/identification-types', formData);
        toast.success('Tipo de identificación creado');
      }
      closeModal();
      loadItems();
    } catch (err: any) {
      toast.error(err.response?.data || (editingId ? 'Error al actualizar' : 'Error al crear'));
    }
  };

  const handleToggleActive = async (item: IdentificationTypeItem) => {
    try {
      await api.put(`/identification-types/${item.id}`, { code: item.code, name: item.name, isActive: !item.isActive, dataicoCode: item.dataicoCode, dataicoPayrollCode: item.dataicoPayrollCode });
      loadItems();
    } catch {
      toast.error('Error al actualizar el tipo de identificación');
    }
  };

  const handleDelete = async (id: string) => {
    if (!window.confirm('¿Seguro que deseas eliminar este tipo de identificación?')) return;
    try {
      await api.delete(`/identification-types/${id}`);
      toast.success('Eliminado correctamente');
      loadItems();
    } catch (err: any) {
      toast.error(err.response?.data || 'No se pudo eliminar');
    }
  };

  const columns: DataTableColumn<IdentificationTypeItem>[] = [
    {
      key: 'code',
      header: 'Código DIAN',
      render: item => <span className="inline-flex items-center px-3 py-1 rounded-lg text-sm font-bold bg-slate-800 text-slate-300 font-mono">{item.code}</span>,
      sortValue: item => item.code,
      searchValue: item => item.code,
    },
    { key: 'name', header: 'Nombre', primary: true, cellClassName: 'font-bold text-white', render: item => item.name, sortValue: item => item.name, searchValue: item => item.name },
    { key: 'dataico', header: 'Equivalencia Dataico', render: item => equivalence(item.dataicoCode), sortValue: item => item.dataicoCode, searchValue: item => item.dataicoCode },
    { key: 'dataicoPayroll', header: 'Dataico nómina', render: item => equivalence(item.dataicoPayrollCode), sortValue: item => item.dataicoPayrollCode, searchValue: item => item.dataicoPayrollCode },
    {
      key: 'status',
      header: 'Estado',
      render: item => (
        <button onClick={() => handleToggleActive(item)} className="flex items-center gap-1.5 text-xs font-bold">
          {item.isActive ? (
            <span className="inline-flex items-center gap-1 px-3 py-1 rounded-full bg-emerald-100 text-emerald-700"><ToggleRight className="w-4 h-4" /> Activo</span>
          ) : (
            <span className="inline-flex items-center gap-1 px-3 py-1 rounded-full bg-slate-700 text-slate-400"><ToggleLeft className="w-4 h-4" /> Inactivo</span>
          )}
        </button>
      ),
      sortValue: item => (item.isActive ? 0 : 1),
    },
  ];

  return (
    <div className="p-10 animate-in fade-in slide-in-from-bottom-4 duration-500">
      <div className="flex justify-between items-center mb-10">
        <div>
          <h1 className="text-3xl font-extrabold text-white tracking-tight flex items-center gap-3">
            <BadgeCheck className="w-8 h-8 text-indigo-400" />
            Tipos de Identificación
          </h1>
          <p className="text-slate-400 mt-2 text-lg font-medium">
            Catálogo DIAN de documentos de identificación (Cédula, NIT, Cédula de Extranjería, etc.) que usan tenants y clientes al registrar terceros. Independiente de los tipos de documento que se emiten (facturas, notas, RIPS) y del catálogo de impuestos.
          </p>
        </div>
        <button
          onClick={openCreateModal}
          className="bg-indigo-600 hover:bg-indigo-700 text-white px-6 py-3 rounded-2xl font-bold flex items-center shadow-lg shadow-indigo-600/30 transition-all transform hover:-translate-y-1"
        >
          <Plus className="w-5 h-5 mr-2" />
          Nuevo Tipo
        </button>
      </div>

      <div className="mt-8">
        <DataTable
          theme="dark"
          rows={items}
          rowKey={item => item.id}
          tableFrom="md"
          search="Buscar por código, nombre o equivalencia..."
          defaultSort={{ key: 'code', dir: 'asc' }}
          emptyMessage="No hay tipos de identificación registrados."
          columns={columns}
          actions={item => (
            <>
              <button onClick={() => startEdit(item)} className="p-2 text-slate-300 hover:text-indigo-400 hover:bg-indigo-500/10 rounded-xl" title="Editar" aria-label={`Editar ${item.name}`}>
                <Pencil className="w-5 h-5" />
              </button>
              <button onClick={() => handleDelete(item.id)} className="p-2 text-red-500 hover:bg-red-500/10 rounded-xl" title="Eliminar" aria-label={`Eliminar ${item.name}`}>
                <Trash2 className="w-5 h-5" />
              </button>
            </>
          )}
        />
      </div>

      {showModal && (
        <div className="fixed inset-0 bg-slate-900/80 backdrop-blur-md flex items-center justify-center z-50 animate-in fade-in duration-200">
          <div className="glass-panel rounded-3xl shadow-2xl p-8 w-full max-w-md animate-in zoom-in-95 duration-200 border-slate-700">
            <h2 className="text-2xl font-bold text-white mb-6">{editingId ? 'Editar Tipo de Identificación' : 'Nuevo Tipo de Identificación'}</h2>
            <form onSubmit={handleSubmit} className="space-y-5">
              <div>
                <label className="block text-sm font-bold text-slate-300 mb-2">Código DIAN</label>
                <input
                  type="text"
                  required
                  className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500 font-mono placeholder:text-slate-500"
                  value={formData.code}
                  onChange={e => setFormData({ ...formData, code: e.target.value })}
                  placeholder="Ej. 13"
                />
              </div>
              <div>
                <label className="block text-sm font-bold text-slate-300 mb-2">Nombre Descriptivo</label>
                <input
                  type="text"
                  required
                  className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500 placeholder:text-slate-500"
                  value={formData.name}
                  onChange={e => setFormData({ ...formData, name: e.target.value })}
                  placeholder="Cédula de Ciudadanía"
                />
              </div>
              <div>
                <label className="block text-sm font-bold text-slate-300 mb-2">Código del proveedor (opcional)</label>
                <input
                  type="text"
                  className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500 font-mono placeholder:text-slate-500"
                  value={formData.dataicoCode}
                  onChange={e => setFormData({ ...formData, dataicoCode: e.target.value.toUpperCase() })}
                  placeholder="Ej. CC"
                />
                <p className="text-xs text-slate-500 mt-1">Código usado por el proveedor para facturación, si aplica.</p>
              </div>
              <div>
                <label className="block text-sm font-bold text-slate-300 mb-2">Código del proveedor para nómina (opcional)</label>
                <input
                  type="text"
                  className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500 font-mono placeholder:text-slate-500"
                  value={formData.dataicoPayrollCode}
                  onChange={e => setFormData({ ...formData, dataicoPayrollCode: e.target.value.toUpperCase() })}
                  placeholder="Ej. CEDULA_DE_CIUDADANIA"
                />
                <p className="text-xs text-slate-500 mt-1">La nómina electrónica usa otro vocabulario que la facturación. Sin este código, no se puede emitir nómina de empleados con este tipo de documento.</p>
              </div>
              <div className="flex gap-4 pt-4">
                <button type="button" onClick={closeModal} className="flex-1 py-3 px-4 bg-slate-100 hover:bg-slate-200 text-slate-700 font-bold rounded-xl transition-colors">
                  Cancelar
                </button>
                <button type="submit" className="flex-1 py-3 px-4 bg-indigo-600 hover:bg-indigo-700 text-white font-bold rounded-xl shadow-lg shadow-indigo-600/30 transition-all flex justify-center items-center">
                  <Save className="w-5 h-5 mr-2" />
                  {editingId ? 'Guardar Cambios' : 'Guardar'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
