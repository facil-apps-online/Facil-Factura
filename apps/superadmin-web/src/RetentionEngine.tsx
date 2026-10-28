import React, { useEffect, useState } from 'react';
import { Plus, Save, Trash2, Pencil, Percent, ToggleLeft, ToggleRight, Calculator } from 'lucide-react';
import { toast } from 'sonner';
import { api } from './api';
import DataTable, { type DataTableColumn } from '@shared/components/DataTable';
import ConfirmAlert from '@shared/components/ConfirmAlert';
import SearchableSelect from '@shared/components/SearchableSelect';

interface RetentionConcept {
  id: string;
  groupKey: string;
  groupLabel: string;
  name: string;
  personType: 'Ambas' | 'Natural' | 'Juridica';
  taxCategory: string;
  baseType: 'Subtotal' | 'IvaGenerado';
  baseUvt: number;
  rate: number;
  effectiveFrom: string;
  isActive: boolean;
}

interface TaxParameter {
  id: string;
  code: string;
  value: number;
  effectiveFrom: string;
}

const PERSON_TYPE_LABELS: Record<string, string> = { Ambas: 'Ambas', Natural: 'Natural', Juridica: 'Jurídica' };
const BASE_TYPE_LABELS: Record<string, string> = { Subtotal: 'Subtotal de la línea', IvaGenerado: 'IVA generado de la línea' };

const initialConceptForm = {
  groupKey: '', groupLabel: '', name: '', personType: 'Ambas', taxCategory: 'RET_FUENTE',
  baseType: 'Subtotal', baseUvt: '0', rate: '', effectiveFrom: new Date().toISOString().slice(0, 10)
};

const PERSON_TYPE_OPTIONS = [
  { value: 'Ambas', label: 'Ambas' },
  { value: 'Natural', label: 'Natural' },
  { value: 'Juridica', label: 'Jurídica' },
];
const TAX_CATEGORY_OPTIONS = [
  { value: 'RET_FUENTE', label: 'RET_FUENTE' },
  { value: 'RET_IVA', label: 'RET_IVA' },
];
const BASE_TYPE_OPTIONS = [
  { value: 'Subtotal', label: BASE_TYPE_LABELS.Subtotal },
  { value: 'IvaGenerado', label: BASE_TYPE_LABELS.IvaGenerado },
];
const SELECT_INPUT_CLASS = 'w-full px-4 py-3 pr-8 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500 outline-none';

// Nombre corto de cada categoría de retención (RETE FUENTE, RETE ICA, RETE IVA). Con la tarifa arma el texto corto de la retención ("RETE FUENTE 2.5%") en los
// selectores de los formularios de los clientes (según el estilo que elija cada cliente o su tenant) y en el PDF de la factura y del documento soporte.
interface RetentionShortName { id: string; taxCategory: string; shortName: string }
const SHORT_NAME_MAX = 30;
const TAX_CATEGORY_LABELS: Record<string, string> = { RET_FUENTE: 'Retención en la fuente', RET_ICA: 'Retención de ICA', RET_IVA: 'Retención de IVA' };

function ShortNamesPanel() {
  const [items, setItems] = useState<RetentionShortName[]>([]);
  const [drafts, setDrafts] = useState<Record<string, string>>({});
  const [savingId, setSavingId] = useState<string | null>(null);

  useEffect(() => {
    api.get<RetentionShortName[]>('/retention-short-names')
      .then(res => { setItems(res.data); setDrafts(Object.fromEntries(res.data.map(i => [i.id, i.shortName]))); })
      .catch(() => toast.error('Error al cargar los nombres cortos'));
  }, []);

  const save = async (item: RetentionShortName) => {
    setSavingId(item.id);
    try {
      const res = await api.put<RetentionShortName>(`/retention-short-names/${item.id}`, { shortName: drafts[item.id] });
      setItems(prev => prev.map(i => (i.id === item.id ? res.data : i)));
      setDrafts(prev => ({ ...prev, [item.id]: res.data.shortName }));
      toast.success('Nombre corto guardado');
    } catch (err: any) {
      toast.error(err.response?.data?.message || 'No se pudo guardar el nombre corto');
    } finally {
      setSavingId(null);
    }
  };

  return (
    <div className="mt-8 max-w-3xl space-y-4">
      <p className="text-sm text-slate-400">Con la tarifa forman el texto corto de cada retención (por ejemplo <span className="font-mono text-slate-200">RETE FUENTE 2.5%</span>). Se ven en los selectores de los formularios de los clientes que usan el nombre corto y en el PDF de la factura y del documento soporte.</p>
      {items.map(item => {
        const changed = (drafts[item.id] ?? '').trim() !== item.shortName;
        return (
          <div key={item.id} className="flex flex-wrap items-end gap-3 rounded-2xl border border-slate-700/60 bg-slate-800/40 p-4">
            <div className="min-w-0 flex-1">
              <label htmlFor={`short-${item.taxCategory}`} className="mb-1 block text-xs font-bold uppercase tracking-wider text-slate-400">{TAX_CATEGORY_LABELS[item.taxCategory] || item.taxCategory} <span className="font-mono normal-case text-slate-500">({item.taxCategory})</span></label>
              <input
                id={`short-${item.taxCategory}`}
                type="text"
                maxLength={SHORT_NAME_MAX}
                value={drafts[item.id] ?? ''}
                onChange={e => setDrafts({ ...drafts, [item.id]: e.target.value })}
                className={SELECT_INPUT_CLASS}
              />
            </div>
            <span className="pb-3 font-mono text-sm text-slate-400">{(drafts[item.id] ?? '').trim() || '…'} 2.5%</span>
            <button
              onClick={() => save(item)}
              disabled={!changed || savingId === item.id || !(drafts[item.id] ?? '').trim()}
              className="flex items-center gap-2 rounded-xl bg-indigo-600 px-5 py-3 text-sm font-bold text-white transition-colors hover:bg-indigo-700 disabled:opacity-40"
            >
              <Save className="h-4 w-4" /> Guardar
            </button>
          </div>
        );
      })}
    </div>
  );
}

const initialParameterForm = { code: 'UVT', value: '', effectiveFrom: new Date().toISOString().slice(0, 10) };

const parameterColumns: DataTableColumn<TaxParameter>[] = [
  { key: 'code', header: 'Código', primary: true, cellClassName: 'font-bold text-white font-mono', render: p => p.code, sortValue: p => p.code },
  { key: 'value', header: 'Valor', align: 'right', cellClassName: 'font-mono text-slate-300', render: p => `$${p.value.toLocaleString('es-CO')}`, sortValue: p => p.value },
  { key: 'effectiveFrom', header: 'Vigente desde', cellClassName: 'text-slate-400 text-sm whitespace-nowrap', render: p => new Date(p.effectiveFrom).toLocaleDateString('es-CO'), sortValue: p => p.effectiveFrom },
];

export const RetentionEngine = () => {
  const [tab, setTab] = useState<'concepts' | 'parameters' | 'shortNames'>('concepts');
  const [concepts, setConcepts] = useState<RetentionConcept[]>([]);
  const [parameters, setParameters] = useState<TaxParameter[]>([]);
  const [conceptForm, setConceptForm] = useState(initialConceptForm);
  const [parameterForm, setParameterForm] = useState(initialParameterForm);
  const [showConceptModal, setShowConceptModal] = useState(false);
  const [showParameterModal, setShowParameterModal] = useState(false);
  // Registro que se está editando; null = el formulario crea uno nuevo.
  const [editingConceptId, setEditingConceptId] = useState<string | null>(null);
  const [editingParameterId, setEditingParameterId] = useState<string | null>(null);
  const [conceptToDelete, setConceptToDelete] = useState<RetentionConcept | null>(null);
  const [parameterToDelete, setParameterToDelete] = useState<TaxParameter | null>(null);
  const [deleting, setDeleting] = useState(false);

  const loadConcepts = () => {
    api.get<RetentionConcept[]>('/retention-concepts')
      .then(res => setConcepts(res.data))
      .catch(() => toast.error('Error al cargar los conceptos de retención'));
  };

  const loadParameters = () => {
    api.get<TaxParameter[]>('/tax-parameters')
      .then(res => setParameters(res.data))
      .catch(() => toast.error('Error al cargar los parámetros tributarios'));
  };

  useEffect(() => {
    loadConcepts();
    loadParameters();
  }, []);

  const openNewConcept = () => {
    setEditingConceptId(null);
    setConceptForm({ ...initialConceptForm, effectiveFrom: new Date().toISOString().slice(0, 10) });
    setShowConceptModal(true);
  };

  const openEditConcept = (c: RetentionConcept) => {
    setEditingConceptId(c.id);
    setConceptForm({
      groupKey: c.groupKey, groupLabel: c.groupLabel, name: c.name, personType: c.personType, taxCategory: c.taxCategory,
      baseType: c.baseType, baseUvt: String(c.baseUvt), rate: String(c.rate), effectiveFrom: c.effectiveFrom.slice(0, 10)
    });
    setShowConceptModal(true);
  };

  const handleSaveConcept = async (e: React.FormEvent) => {
    e.preventDefault();
    const payload = { ...conceptForm, baseUvt: parseFloat(conceptForm.baseUvt) || 0, rate: parseFloat(conceptForm.rate) || 0 };
    try {
      if (editingConceptId) {
        // Editar no cambia si el concepto está activo: eso se maneja con el interruptor de la lista.
        const current = concepts.find(c => c.id === editingConceptId);
        await api.put(`/retention-concepts/${editingConceptId}`, { ...payload, isActive: current?.isActive ?? true });
        toast.success('Concepto actualizado');
      } else {
        await api.post('/retention-concepts', payload);
        toast.success('Concepto creado');
      }
      setConceptForm(initialConceptForm);
      setEditingConceptId(null);
      setShowConceptModal(false);
      loadConcepts();
    } catch (err: any) {
      toast.error(err.response?.data || (editingConceptId ? 'Error al actualizar el concepto' : 'Error al crear el concepto'));
    }
  };

  const handleToggleConceptActive = async (c: RetentionConcept) => {
    try {
      await api.put(`/retention-concepts/${c.id}`, { ...c, isActive: !c.isActive });
      loadConcepts();
    } catch {
      toast.error('Error al actualizar el concepto');
    }
  };

  const handleDeleteConcept = async () => {
    if (!conceptToDelete) return;
    setDeleting(true);
    try {
      await api.delete(`/retention-concepts/${conceptToDelete.id}`);
      toast.success('Eliminado correctamente');
      setConceptToDelete(null);
      loadConcepts();
    } catch {
      toast.error('No se pudo eliminar el concepto');
    } finally {
      setDeleting(false);
    }
  };

  const openNewParameter = () => {
    setEditingParameterId(null);
    setParameterForm({ ...initialParameterForm, effectiveFrom: new Date().toISOString().slice(0, 10) });
    setShowParameterModal(true);
  };

  const openEditParameter = (p: TaxParameter) => {
    setEditingParameterId(p.id);
    setParameterForm({ code: p.code, value: String(p.value), effectiveFrom: p.effectiveFrom.slice(0, 10) });
    setShowParameterModal(true);
  };

  const handleSaveParameter = async (e: React.FormEvent) => {
    e.preventDefault();
    const payload = { ...parameterForm, value: parseFloat(parameterForm.value) || 0 };
    try {
      if (editingParameterId) {
        await api.put(`/tax-parameters/${editingParameterId}`, payload);
        toast.success('Parámetro actualizado');
      } else {
        await api.post('/tax-parameters', payload);
        toast.success('Parámetro creado');
      }
      setParameterForm(initialParameterForm);
      setEditingParameterId(null);
      setShowParameterModal(false);
      loadParameters();
    } catch (err: any) {
      toast.error(err.response?.data || (editingParameterId ? 'Error al actualizar el parámetro' : 'Error al crear el parámetro'));
    }
  };

  const handleDeleteParameter = async () => {
    if (!parameterToDelete) return;
    setDeleting(true);
    try {
      await api.delete(`/tax-parameters/${parameterToDelete.id}`);
      toast.success('Eliminada correctamente');
      setParameterToDelete(null);
      loadParameters();
    } catch {
      toast.error('No se pudo eliminar la vigencia');
    } finally {
      setDeleting(false);
    }
  };

  const conceptColumns: DataTableColumn<RetentionConcept>[] = [
    { key: 'group', header: 'Concepto', primary: true, cellClassName: 'font-bold text-white', render: c => c.groupLabel, sortValue: c => c.groupLabel, searchValue: c => c.groupLabel },
    { key: 'name', header: 'Variante', cellClassName: 'text-slate-300 text-sm', render: c => c.name, sortValue: c => c.name, searchValue: c => c.name },
    {
      key: 'personType',
      header: 'Persona',
      render: c => (
        <span className="inline-flex items-center px-3 py-1 rounded-full text-xs font-bold bg-indigo-500/20 text-indigo-400 border border-indigo-500/30">
          {PERSON_TYPE_LABELS[c.personType] || c.personType}
        </span>
      ),
      sortValue: c => PERSON_TYPE_LABELS[c.personType] || c.personType,
      searchValue: c => PERSON_TYPE_LABELS[c.personType] || c.personType,
    },
    { key: 'baseUvt', header: 'Base mín. (UVT)', align: 'right', cellClassName: 'font-mono text-slate-300', render: c => `${c.baseUvt} UVT`, sortValue: c => c.baseUvt },
    { key: 'rate', header: 'Tarifa', align: 'right', cellClassName: 'font-mono text-slate-300', render: c => `${c.rate}%`, sortValue: c => c.rate },
    { key: 'effectiveFrom', header: 'Vigente desde', cellClassName: 'text-slate-400 text-sm whitespace-nowrap', render: c => new Date(c.effectiveFrom).toLocaleDateString('es-CO'), sortValue: c => c.effectiveFrom },
    {
      key: 'status',
      header: 'Estado',
      render: c => (
        <button onClick={() => handleToggleConceptActive(c)} className="flex items-center gap-1.5 text-xs font-bold">
          {c.isActive ? (
            <span className="inline-flex items-center gap-1 px-3 py-1 rounded-full bg-emerald-100 text-emerald-700"><ToggleRight className="w-4 h-4" /> Activo</span>
          ) : (
            <span className="inline-flex items-center gap-1 px-3 py-1 rounded-full bg-slate-700 text-slate-400"><ToggleLeft className="w-4 h-4" /> Inactivo</span>
          )}
        </button>
      ),
      sortValue: c => (c.isActive ? 0 : 1),
    },
  ];

  return (
    <div className="p-10 animate-in fade-in slide-in-from-bottom-4 duration-500">
      <div className="flex justify-between items-center mb-8">
        <div>
          <h1 className="text-3xl font-extrabold text-white tracking-tight flex items-center gap-3">
            <Calculator className="w-8 h-8 text-indigo-400" />
            Motor de Retenciones
          </h1>
          <p className="text-slate-400 mt-2 text-lg font-medium">
            Conceptos de retención en la fuente/IVA (con vigencia) y parámetros tributarios (UVT) que usan los clientes para calcular automáticamente sus facturas.
          </p>
        </div>
        {tab !== 'shortNames' && <button
          onClick={() => tab === 'concepts' ? openNewConcept() : openNewParameter()}
          className="bg-indigo-600 hover:bg-indigo-700 text-white px-6 py-3 rounded-2xl font-bold flex items-center shadow-lg shadow-indigo-600/30 transition-all transform hover:-translate-y-1"
        >
          <Plus className="w-5 h-5 mr-2" />
          {tab === 'concepts' ? 'Nuevo Concepto' : 'Nueva Vigencia'}
        </button>}
      </div>

      <div className="flex gap-2 mb-6">
        <button onClick={() => setTab('concepts')} className={`px-4 py-2 rounded-xl text-sm font-bold transition-colors ${tab === 'concepts' ? 'bg-indigo-600 text-white' : 'bg-slate-800/50 text-slate-400 hover:text-white'}`}>
          Conceptos ({concepts.length})
        </button>
        <button onClick={() => setTab('parameters')} className={`px-4 py-2 rounded-xl text-sm font-bold transition-colors ${tab === 'parameters' ? 'bg-indigo-600 text-white' : 'bg-slate-800/50 text-slate-400 hover:text-white'}`}>
          Parámetros / UVT ({parameters.length})
        </button>
        <button onClick={() => setTab('shortNames')} className={`px-4 py-2 rounded-xl text-sm font-bold transition-colors ${tab === 'shortNames' ? 'bg-indigo-600 text-white' : 'bg-slate-800/50 text-slate-400 hover:text-white'}`}>
          Nombres cortos
        </button>
      </div>

      {tab === 'shortNames' ? (
        <ShortNamesPanel />
      ) : tab === 'concepts' ? (
        <div className="mt-8">
          <DataTable
            theme="dark"
            rows={concepts}
            rowKey={c => c.id}
            tableFrom="lg"
            search="Buscar por concepto, variante o persona..."
            defaultSort={{ key: 'group', dir: 'asc' }}
            emptyMessage="No hay conceptos de retención registrados."
            columns={conceptColumns}
            actions={c => (
              <div className="flex items-center gap-1">
                <button onClick={() => openEditConcept(c)} className="p-2 text-indigo-400 hover:bg-indigo-500/10 rounded-xl transition-all" title="Editar" aria-label={`Editar ${c.name}`}>
                  <Pencil className="w-5 h-5" />
                </button>
                <button onClick={() => setConceptToDelete(c)} className="p-2 text-red-500 hover:bg-red-500/10 rounded-xl transition-all" title="Eliminar" aria-label={`Eliminar ${c.groupLabel}`}>
                  <Trash2 className="w-5 h-5" />
                </button>
              </div>
            )}
          />
        </div>
      ) : (
        <div className="mt-8">
          <DataTable
            theme="dark"
            rows={parameters}
            rowKey={p => p.id}
            tableFrom="md"
            defaultSort={{ key: 'code', dir: 'asc' }}
            emptyMessage="No hay parámetros registrados."
            columns={parameterColumns}
            actions={p => (
              <div className="flex items-center gap-1">
                <button onClick={() => openEditParameter(p)} className="p-2 text-indigo-400 hover:bg-indigo-500/10 rounded-xl transition-all" title="Editar" aria-label={`Editar ${p.code}`}>
                  <Pencil className="w-5 h-5" />
                </button>
                <button onClick={() => setParameterToDelete(p)} className="p-2 text-red-500 hover:bg-red-500/10 rounded-xl transition-all" title="Eliminar" aria-label={`Eliminar ${p.code}`}>
                  <Trash2 className="w-5 h-5" />
                </button>
              </div>
            )}
          />
        </div>
      )}

      {showConceptModal && (
        <div className="fixed inset-0 bg-slate-900/80 backdrop-blur-md flex items-center justify-center z-50 animate-in fade-in duration-200">
          <div className="glass-panel rounded-3xl shadow-2xl p-8 w-full max-w-lg animate-in zoom-in-95 duration-200 border-slate-700 max-h-[90vh] overflow-y-auto">
            <h2 className="text-2xl font-bold text-white mb-6 flex items-center gap-2"><Percent className="w-6 h-6 text-indigo-400" /> {editingConceptId ? 'Editar Concepto de Retención' : 'Nuevo Concepto de Retención'}</h2>
            <form onSubmit={handleSaveConcept} className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-bold text-slate-300 mb-2">Código interno</label>
                  <input type="text" required className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500 font-mono placeholder:text-slate-500" value={conceptForm.groupKey} onChange={e => setConceptForm({ ...conceptForm, groupKey: e.target.value.toUpperCase() })} placeholder="COMPRAS_GENERALES" />
                </div>
                <div>
                  <label className="block text-sm font-bold text-slate-300 mb-2">Tipo de persona</label>
                  <SearchableSelect options={PERSON_TYPE_OPTIONS} value={conceptForm.personType} onChange={v => setConceptForm({ ...conceptForm, personType: v })} inputClassName={SELECT_INPUT_CLASS} required />
                </div>
              </div>
              <div>
                <label className="block text-sm font-bold text-slate-300 mb-2">Nombre visible</label>
                <input type="text" required className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500 placeholder:text-slate-500" value={conceptForm.groupLabel} onChange={e => setConceptForm({ ...conceptForm, groupLabel: e.target.value })} placeholder="Compras generales" />
              </div>
              <div>
                <label className="block text-sm font-bold text-slate-300 mb-2">Nombre de esta variante</label>
                <input type="text" required className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500 placeholder:text-slate-500" value={conceptForm.name} onChange={e => setConceptForm({ ...conceptForm, name: e.target.value })} placeholder="Compras generales (declarantes)" />
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-bold text-slate-300 mb-2">Categoría</label>
                  <SearchableSelect options={TAX_CATEGORY_OPTIONS} value={conceptForm.taxCategory} onChange={v => setConceptForm({ ...conceptForm, taxCategory: v })} inputClassName={SELECT_INPUT_CLASS} required />
                </div>
                <div>
                  <label className="block text-sm font-bold text-slate-300 mb-2">Base de cálculo</label>
                  <SearchableSelect options={BASE_TYPE_OPTIONS} value={conceptForm.baseType} onChange={v => setConceptForm({ ...conceptForm, baseType: v })} inputClassName={SELECT_INPUT_CLASS} required />
                </div>
              </div>
              <div className="grid grid-cols-3 gap-4">
                <div>
                  <label className="block text-sm font-bold text-slate-300 mb-2">Base mínima (UVT)</label>
                  <input type="number" step="0.01" min="0" required className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500 font-mono" value={conceptForm.baseUvt} onChange={e => setConceptForm({ ...conceptForm, baseUvt: e.target.value })} />
                </div>
                <div>
                  <label className="block text-sm font-bold text-slate-300 mb-2">Tarifa (%)</label>
                  <input type="number" step="0.01" min="0" required className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500 font-mono" value={conceptForm.rate} onChange={e => setConceptForm({ ...conceptForm, rate: e.target.value })} />
                </div>
                <div>
                  <label className="block text-sm font-bold text-slate-300 mb-2">Vigente desde</label>
                  <input type="date" required className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500" value={conceptForm.effectiveFrom} onChange={e => setConceptForm({ ...conceptForm, effectiveFrom: e.target.value })} />
                </div>
              </div>
              <div className="flex gap-4 pt-4">
                <button type="button" onClick={() => { setShowConceptModal(false); setEditingConceptId(null); }} className="flex-1 py-3 px-4 bg-slate-100 hover:bg-slate-200 text-slate-700 font-bold rounded-xl transition-colors">Cancelar</button>
                <button type="submit" className="flex-1 py-3 px-4 bg-indigo-600 hover:bg-indigo-700 text-white font-bold rounded-xl shadow-lg shadow-indigo-600/30 transition-all flex justify-center items-center">
                  <Save className="w-5 h-5 mr-2" /> Guardar
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {showParameterModal && (
        <div className="fixed inset-0 bg-slate-900/80 backdrop-blur-md flex items-center justify-center z-50 animate-in fade-in duration-200">
          <div className="glass-panel rounded-3xl shadow-2xl p-8 w-full max-w-md animate-in zoom-in-95 duration-200 border-slate-700">
            <h2 className="text-2xl font-bold text-white mb-6">{editingParameterId ? 'Editar Vigencia de Parámetro' : 'Nueva Vigencia de Parámetro'}</h2>
            <p className="text-sm text-slate-400 mb-4">
              {editingParameterId
                ? 'Editar corrige esta vigencia en su lugar. Si el valor cambió por una norma nueva, mejor crea una vigencia nueva con su fecha para conservar la anterior.'
                : 'Crea una nueva vigencia cuando cambie el valor. Así se conservan los cálculos de documentos anteriores.'}
            </p>
            <form onSubmit={handleSaveParameter} className="space-y-5">
              <div>
                <label className="block text-sm font-bold text-slate-300 mb-2">Código</label>
                <input type="text" required readOnly={!!editingParameterId} className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500 font-mono placeholder:text-slate-500 read-only:opacity-60" value={parameterForm.code} onChange={e => setParameterForm({ ...parameterForm, code: e.target.value.toUpperCase() })} placeholder="UVT" />
              </div>
              <div>
                <label className="block text-sm font-bold text-slate-300 mb-2">Valor ($)</label>
                <input type="number" step="0.01" min="0" required className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500 font-mono" value={parameterForm.value} onChange={e => setParameterForm({ ...parameterForm, value: e.target.value })} placeholder="52374" />
              </div>
              <div>
                <label className="block text-sm font-bold text-slate-300 mb-2">Vigente desde</label>
                <input type="date" required className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500" value={parameterForm.effectiveFrom} onChange={e => setParameterForm({ ...parameterForm, effectiveFrom: e.target.value })} />
              </div>
              <div className="flex gap-4 pt-4">
                <button type="button" onClick={() => { setShowParameterModal(false); setEditingParameterId(null); }} className="flex-1 py-3 px-4 bg-slate-100 hover:bg-slate-200 text-slate-700 font-bold rounded-xl transition-colors">Cancelar</button>
                <button type="submit" className="flex-1 py-3 px-4 bg-indigo-600 hover:bg-indigo-700 text-white font-bold rounded-xl shadow-lg shadow-indigo-600/30 transition-all flex justify-center items-center">
                  <Save className="w-5 h-5 mr-2" /> Guardar
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      <ConfirmAlert
        theme="dark"
        open={conceptToDelete !== null}
        onOpenChange={open => { if (!open) setConceptToDelete(null); }}
        title="Eliminar concepto"
        description={conceptToDelete ? `Se eliminará "${conceptToDelete.name}" del catálogo. Los clientes que lo tenían habilitado dejarán de verlo. Esta acción no se puede deshacer.` : ''}
        confirmText="Eliminar"
        busy={deleting}
        onConfirm={handleDeleteConcept}
      />
      <ConfirmAlert
        theme="dark"
        open={parameterToDelete !== null}
        onOpenChange={open => { if (!open) setParameterToDelete(null); }}
        title="Eliminar vigencia"
        description={parameterToDelete ? `Se eliminará la vigencia de ${parameterToDelete.code} desde el ${new Date(parameterToDelete.effectiveFrom).toLocaleDateString('es-CO')}. Esta acción no se puede deshacer.` : ''}
        confirmText="Eliminar"
        busy={deleting}
        onConfirm={handleDeleteParameter}
      />
    </div>
  );
};
