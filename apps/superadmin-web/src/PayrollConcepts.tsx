import React, { useEffect, useState } from 'react';
import { Plus, Save, Trash2, Pencil, ToggleLeft, ToggleRight, Banknote } from 'lucide-react';
import { toast } from 'sonner';
import { api } from './api';
import DataTable, { type DataTableColumn } from '@shared/components/DataTable';

// Los enums viajan como número (la API de superadmin no usa nombres): mismos valores que PayrollConceptType.cs.
const SECTIONS = [{ value: 0, label: 'Devengo' }, { value: 1, label: 'Deducción' }];
const SHAPES = [
  { value: 0, label: 'Atributos (un elemento con atributos)' },
  { value: 1, label: 'Lista (elementos repetibles con fechas u horas)' },
  { value: 2, label: 'Valor (un solo valor)' },
];
const QUANTITY_KINDS = [{ value: 0, label: 'Sin cantidad' }, { value: 1, label: 'Días' }, { value: 2, label: 'Horas' }, { value: 3, label: 'Porcentaje' }];
const SPECIALS = [
  { value: 0, label: 'Ninguno' },
  { value: 1, label: 'Fechas inicial y final' },
  { value: 2, label: 'Tipo de incapacidad' },
  { value: 3, label: 'Intereses de cesantías' },
  { value: 4, label: 'Compensación ordinaria y extraordinaria' },
  { value: 5, label: 'Sanción pública y privada' },
];

interface PayrollConceptItem {
  id: string;
  section: number;
  dataicoCode: string;
  name: string;
  dianElement: string;
  dianShape: number;
  dianAmountAttribute: string | null;
  dianNonSalaryAttribute: string | null;
  dianQuantityAttribute: string | null;
  dianPercentageAttribute: string | null;
  quantityKind: number;
  hasPercentage: boolean;
  allowsAmount: boolean;
  allowsNonSalary: boolean;
  requiresDescription: boolean;
  specialInput: number;
  sortOrder: number;
  isActive: boolean;
}

const emptyForm = (): Omit<PayrollConceptItem, 'id'> => ({
  section: 0, dataicoCode: '', name: '', dianElement: '', dianShape: 0,
  dianAmountAttribute: '', dianNonSalaryAttribute: '', dianQuantityAttribute: '', dianPercentageAttribute: '',
  quantityKind: 0, hasPercentage: false, allowsAmount: true, allowsNonSalary: false, requiresDescription: false,
  specialInput: 0, sortOrder: 0, isActive: true,
});

const chip = (text?: string | null) =>
  text ? (
    <span className="inline-flex items-center px-3 py-1 rounded-lg text-sm font-bold bg-fuchsia-500/20 text-fuchsia-400 border border-fuchsia-500/30 font-mono">{text}</span>
  ) : (
    <span className="text-slate-500 text-sm">—</span>
  );

const inputClass = 'w-full px-4 py-2.5 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500 placeholder:text-slate-500';
const labelClass = 'block text-sm font-bold text-slate-300 mb-1.5';

export const PayrollConcepts = () => {
  const [items, setItems] = useState<PayrollConceptItem[]>([]);
  const [formData, setFormData] = useState(emptyForm());
  const [showModal, setShowModal] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);

  const loadItems = () => {
    api.get<PayrollConceptItem[]>('/payroll-concepts')
      .then(res => setItems(res.data))
      .catch(() => toast.error('Error al cargar los conceptos de nómina'));
  };

  useEffect(() => {
    loadItems();
  }, []);

  const closeModal = () => {
    setShowModal(false);
    setEditingId(null);
    setFormData(emptyForm());
  };

  const openCreateModal = () => {
    setEditingId(null);
    setFormData({ ...emptyForm(), sortOrder: items.length + 1 });
    setShowModal(true);
  };

  const startEdit = (item: PayrollConceptItem) => {
    const { id, ...rest } = item;
    setEditingId(id);
    setFormData({
      ...rest,
      dianAmountAttribute: rest.dianAmountAttribute ?? '',
      dianNonSalaryAttribute: rest.dianNonSalaryAttribute ?? '',
      dianQuantityAttribute: rest.dianQuantityAttribute ?? '',
      dianPercentageAttribute: rest.dianPercentageAttribute ?? '',
    });
    setShowModal(true);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      if (editingId) {
        await api.put(`/payroll-concepts/${editingId}`, formData);
        toast.success('Concepto actualizado');
      } else {
        await api.post('/payroll-concepts', formData);
        toast.success('Concepto creado');
      }
      closeModal();
      loadItems();
    } catch (err: any) {
      toast.error(err.response?.data || (editingId ? 'Error al actualizar' : 'Error al crear'));
    }
  };

  const handleToggleActive = async (item: PayrollConceptItem) => {
    try {
      const { id, ...rest } = item;
      await api.put(`/payroll-concepts/${id}`, { ...rest, isActive: !item.isActive });
      loadItems();
    } catch (err: any) {
      toast.error(err.response?.data || 'Error al actualizar el concepto');
    }
  };

  const handleDelete = async (id: string) => {
    if (!window.confirm('¿Seguro que deseas eliminar este concepto? Los borradores que lo usan conservan el código, pero dejará de ofrecerse en el formulario.')) return;
    try {
      await api.delete(`/payroll-concepts/${id}`);
      toast.success('Eliminado correctamente');
      loadItems();
    } catch (err: any) {
      toast.error(err.response?.data || 'No se pudo eliminar');
    }
  };

  const attributes = (item: PayrollConceptItem) =>
    [item.dianAmountAttribute, item.dianNonSalaryAttribute, item.dianQuantityAttribute, item.dianPercentageAttribute].filter(Boolean).join(' · ');

  const columns: DataTableColumn<PayrollConceptItem>[] = [
    {
      key: 'section',
      header: 'Sección',
      render: item => <span className={`inline-flex px-3 py-1 rounded-lg text-xs font-bold ${item.section === 0 ? 'bg-emerald-500/20 text-emerald-400' : 'bg-rose-500/20 text-rose-400'}`}>{SECTIONS[item.section]?.label}</span>,
      sortValue: item => item.section,
    },
    { key: 'name', header: 'Nombre', primary: true, cellClassName: 'font-bold text-white', render: item => item.name, sortValue: item => item.name, searchValue: item => item.name },
    { key: 'dataico', header: 'Código Dataico', render: item => chip(item.dataicoCode), sortValue: item => item.dataicoCode, searchValue: item => item.dataicoCode },
    { key: 'dian', header: 'Elemento DIAN', render: item => chip(item.dianElement), sortValue: item => item.dianElement, searchValue: item => item.dianElement },
    { key: 'attrs', header: 'Atributos DIAN', cellClassName: 'text-slate-400 text-sm font-mono', render: item => attributes(item) || '—' },
    { key: 'qty', header: 'Cantidad', cellClassName: 'text-slate-300 text-sm', render: item => QUANTITY_KINDS[item.quantityKind]?.label + (item.hasPercentage ? ' + %' : ''), sortValue: item => item.quantityKind },
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

  const checkbox = (field: 'hasPercentage' | 'allowsAmount' | 'allowsNonSalary' | 'requiresDescription', label: string) => (
    <label className="flex items-center gap-2 text-sm text-slate-300">
      <input type="checkbox" checked={formData[field]} onChange={e => setFormData({ ...formData, [field]: e.target.checked })} className="h-4 w-4 rounded" />
      {label}
    </label>
  );

  const attributeInput = (field: 'dianAmountAttribute' | 'dianNonSalaryAttribute' | 'dianQuantityAttribute' | 'dianPercentageAttribute', label: string, placeholder: string) => (
    <div>
      <label className={labelClass}>{label}</label>
      <input type="text" className={`${inputClass} font-mono`} value={formData[field] ?? ''} onChange={e => setFormData({ ...formData, [field]: e.target.value })} placeholder={placeholder} />
    </div>
  );

  return (
    <div className="p-10 animate-in fade-in slide-in-from-bottom-4 duration-500">
      <div className="flex justify-between items-center mb-10 gap-4">
        <div>
          <h1 className="text-3xl font-extrabold text-white tracking-tight flex items-center gap-3">
            <Banknote className="w-8 h-8 text-indigo-400" />
            Conceptos de Nómina
          </h1>
          <p className="text-slate-400 mt-2 text-lg font-medium">
            Devengos y deducciones de la nómina electrónica, con su código en Dataico, el elemento y los atributos del XML de la DIAN, el significado de la cantidad (días, horas o porcentaje) y el nombre que ven los clientes. Es la única fuente: el formulario, el PDF, la plantilla de Excel y los dos caminos de emisión leen de aquí.
          </p>
        </div>
        <button
          onClick={openCreateModal}
          className="shrink-0 bg-indigo-600 hover:bg-indigo-700 text-white px-6 py-3 rounded-2xl font-bold flex items-center shadow-lg shadow-indigo-600/30 transition-all transform hover:-translate-y-1"
        >
          <Plus className="w-5 h-5 mr-2" />
          Nuevo Concepto
        </button>
      </div>

      <div className="mt-8">
        <DataTable
          theme="dark"
          rows={items}
          rowKey={item => item.id}
          tableFrom="lg"
          search="Buscar por nombre, código Dataico o elemento DIAN..."
          defaultSort={{ key: 'section', dir: 'asc' }}
          emptyMessage="No hay conceptos de nómina registrados."
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
        <div className="fixed inset-0 bg-slate-900/80 backdrop-blur-md flex items-center justify-center z-50 p-4 animate-in fade-in duration-200">
          <div className="glass-panel rounded-3xl shadow-2xl p-8 w-full max-w-3xl max-h-[92vh] overflow-y-auto animate-in zoom-in-95 duration-200 border-slate-700">
            <h2 className="text-2xl font-bold text-white mb-6">{editingId ? 'Editar Concepto' : 'Nuevo Concepto'}</h2>
            <form onSubmit={handleSubmit} className="space-y-6">
              <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                <div>
                  <label className={labelClass}>Sección</label>
                  <select className={inputClass} value={formData.section} onChange={e => setFormData({ ...formData, section: Number(e.target.value) })}>
                    {SECTIONS.map(s => <option key={s.value} value={s.value}>{s.label}</option>)}
                  </select>
                </div>
                <div>
                  <label className={labelClass}>Código Dataico</label>
                  <input type="text" required className={`${inputClass} font-mono`} value={formData.dataicoCode} onChange={e => setFormData({ ...formData, dataicoCode: e.target.value.toUpperCase() })} placeholder="Ej. HORA_EXTRA_DIURNA" />
                </div>
                <div>
                  <label className={labelClass}>Nombre que ve el cliente</label>
                  <input type="text" required className={inputClass} value={formData.name} onChange={e => setFormData({ ...formData, name: e.target.value })} placeholder="Hora extra diurna" />
                </div>
              </div>

              <div>
                <p className="text-xs font-bold text-slate-400 uppercase mb-3">Equivalencia en el XML de la DIAN</p>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <label className={labelClass}>Elemento</label>
                    <input type="text" required className={`${inputClass} font-mono`} value={formData.dianElement} onChange={e => setFormData({ ...formData, dianElement: e.target.value })} placeholder="Ej. HED" />
                  </div>
                  <div>
                    <label className={labelClass}>Forma</label>
                    <select className={inputClass} value={formData.dianShape} onChange={e => setFormData({ ...formData, dianShape: Number(e.target.value) })}>
                      {SHAPES.map(s => <option key={s.value} value={s.value}>{s.label}</option>)}
                    </select>
                  </div>
                  {attributeInput('dianAmountAttribute', 'Atributo del valor (salarial)', 'Ej. Pago')}
                  {attributeInput('dianNonSalaryAttribute', 'Atributo del valor no salarial', 'Ej. PagoNS')}
                  {attributeInput('dianQuantityAttribute', 'Atributo de la cantidad', 'Ej. Cantidad')}
                  {attributeInput('dianPercentageAttribute', 'Atributo del porcentaje', 'Ej. Porcentaje')}
                </div>
              </div>

              <div>
                <p className="text-xs font-bold text-slate-400 uppercase mb-3">Captura</p>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <label className={labelClass}>La cantidad es</label>
                    <select className={inputClass} value={formData.quantityKind} onChange={e => setFormData({ ...formData, quantityKind: Number(e.target.value) })}>
                      {QUANTITY_KINDS.map(s => <option key={s.value} value={s.value}>{s.label}</option>)}
                    </select>
                  </div>
                  <div>
                    <label className={labelClass}>Dato especial que exige el integrador</label>
                    <select className={inputClass} value={formData.specialInput} onChange={e => setFormData({ ...formData, specialInput: Number(e.target.value) })}>
                      {SPECIALS.map(s => <option key={s.value} value={s.value}>{s.label}</option>)}
                    </select>
                  </div>
                </div>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 mt-4">
                  {checkbox('allowsAmount', 'Lleva valor')}
                  {checkbox('allowsNonSalary', 'Admite valor no salarial')}
                  {checkbox('hasPercentage', 'Lleva además el porcentaje de recargo (horas)')}
                  {checkbox('requiresDescription', 'Pide una descripción')}
                </div>
              </div>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div>
                  <label className={labelClass}>Orden en la lista</label>
                  <input type="number" min={0} className={inputClass} value={formData.sortOrder} onChange={e => setFormData({ ...formData, sortOrder: Number(e.target.value) })} />
                </div>
              </div>

              <div className="flex gap-4 pt-2">
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
