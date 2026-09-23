import React, { useEffect, useState } from 'react';
import { Plus, Save, Trash2, Percent, ToggleLeft, ToggleRight, Calculator } from 'lucide-react';
import { toast } from 'sonner';
import { api } from './api';

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

const initialParameterForm = { code: 'UVT', value: '', effectiveFrom: new Date().toISOString().slice(0, 10) };

export const RetentionEngine = () => {
  const [tab, setTab] = useState<'concepts' | 'parameters'>('concepts');
  const [concepts, setConcepts] = useState<RetentionConcept[]>([]);
  const [parameters, setParameters] = useState<TaxParameter[]>([]);
  const [conceptForm, setConceptForm] = useState(initialConceptForm);
  const [parameterForm, setParameterForm] = useState(initialParameterForm);
  const [showConceptModal, setShowConceptModal] = useState(false);
  const [showParameterModal, setShowParameterModal] = useState(false);

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

  const handleCreateConcept = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await api.post('/retention-concepts', { ...conceptForm, baseUvt: parseFloat(conceptForm.baseUvt) || 0, rate: parseFloat(conceptForm.rate) || 0 });
      toast.success('Concepto creado');
      setConceptForm(initialConceptForm);
      setShowConceptModal(false);
      loadConcepts();
    } catch (err: any) {
      toast.error(err.response?.data || 'Error al crear el concepto');
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

  const handleDeleteConcept = async (id: string) => {
    if (!window.confirm('¿Seguro que deseas eliminar este concepto?')) return;
    try {
      await api.delete(`/retention-concepts/${id}`);
      toast.success('Eliminado correctamente');
      loadConcepts();
    } catch {
      toast.error('No se pudo eliminar el concepto');
    }
  };

  const handleCreateParameter = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await api.post('/tax-parameters', { ...parameterForm, value: parseFloat(parameterForm.value) || 0 });
      toast.success('Parámetro creado');
      setParameterForm(initialParameterForm);
      setShowParameterModal(false);
      loadParameters();
    } catch (err: any) {
      toast.error(err.response?.data || 'Error al crear el parámetro');
    }
  };

  const handleDeleteParameter = async (id: string) => {
    if (!window.confirm('¿Seguro que deseas eliminar esta vigencia?')) return;
    try {
      await api.delete(`/tax-parameters/${id}`);
      toast.success('Eliminada correctamente');
      loadParameters();
    } catch {
      toast.error('No se pudo eliminar la vigencia');
    }
  };

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
        <button
          onClick={() => tab === 'concepts' ? setShowConceptModal(true) : setShowParameterModal(true)}
          className="bg-indigo-600 hover:bg-indigo-700 text-white px-6 py-3 rounded-2xl font-bold flex items-center shadow-lg shadow-indigo-600/30 transition-all transform hover:-translate-y-1"
        >
          <Plus className="w-5 h-5 mr-2" />
          {tab === 'concepts' ? 'Nuevo Concepto' : 'Nueva Vigencia'}
        </button>
      </div>

      <div className="flex gap-2 mb-6">
        <button onClick={() => setTab('concepts')} className={`px-4 py-2 rounded-xl text-sm font-bold transition-colors ${tab === 'concepts' ? 'bg-indigo-600 text-white' : 'bg-slate-800/50 text-slate-400 hover:text-white'}`}>
          Conceptos ({concepts.length})
        </button>
        <button onClick={() => setTab('parameters')} className={`px-4 py-2 rounded-xl text-sm font-bold transition-colors ${tab === 'parameters' ? 'bg-indigo-600 text-white' : 'bg-slate-800/50 text-slate-400 hover:text-white'}`}>
          Parámetros / UVT ({parameters.length})
        </button>
      </div>

      {tab === 'concepts' ? (
        <div className="glass-panel rounded-3xl overflow-hidden">
          <table className="w-full text-left">
            <thead className="bg-slate-900/50 border-b border-slate-700/50">
              <tr>
                <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Concepto</th>
                <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Variante</th>
                <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Persona</th>
                <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Base mín. (UVT)</th>
                <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Tarifa</th>
                <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Vigente desde</th>
                <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Estado</th>
                <th className="px-6 py-4 text-right"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800/50">
              {concepts.map(c => (
                <tr key={c.id} className="hover:bg-slate-800/30 transition-colors group">
                  <td className="px-6 py-4 font-bold text-white">{c.groupLabel}</td>
                  <td className="px-6 py-4 text-slate-300 text-sm">{c.name}</td>
                  <td className="px-6 py-4">
                    <span className="inline-flex items-center px-3 py-1 rounded-full text-xs font-bold bg-indigo-500/20 text-indigo-400 border border-indigo-500/30">
                      {PERSON_TYPE_LABELS[c.personType] || c.personType}
                    </span>
                  </td>
                  <td className="px-6 py-4 font-mono text-slate-300">{c.baseUvt} UVT</td>
                  <td className="px-6 py-4 font-mono text-slate-300">{c.rate}%</td>
                  <td className="px-6 py-4 text-slate-400 text-sm">{new Date(c.effectiveFrom).toLocaleDateString('es-CO')}</td>
                  <td className="px-6 py-4">
                    <button onClick={() => handleToggleConceptActive(c)} className="flex items-center gap-1.5 text-xs font-bold">
                      {c.isActive ? (
                        <span className="inline-flex items-center gap-1 px-3 py-1 rounded-full bg-emerald-100 text-emerald-700"><ToggleRight className="w-4 h-4" /> Activo</span>
                      ) : (
                        <span className="inline-flex items-center gap-1 px-3 py-1 rounded-full bg-slate-700 text-slate-400"><ToggleLeft className="w-4 h-4" /> Inactivo</span>
                      )}
                    </button>
                  </td>
                  <td className="px-6 py-4 text-right">
                    <button onClick={() => handleDeleteConcept(c.id)} className="p-2 text-red-500 hover:bg-red-500/10 rounded-xl opacity-0 group-hover:opacity-100 transition-all" title="Eliminar">
                      <Trash2 className="w-5 h-5" />
                    </button>
                  </td>
                </tr>
              ))}
              {concepts.length === 0 && (
                <tr>
                  <td colSpan={8} className="px-6 py-12 text-center text-slate-400 font-medium">No hay conceptos de retención registrados.</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      ) : (
        <div className="glass-panel rounded-3xl overflow-hidden">
          <table className="w-full text-left">
            <thead className="bg-slate-900/50 border-b border-slate-700/50">
              <tr>
                <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Código</th>
                <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Valor</th>
                <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Vigente desde</th>
                <th className="px-6 py-4 text-right"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800/50">
              {parameters.map(p => (
                <tr key={p.id} className="hover:bg-slate-800/30 transition-colors group">
                  <td className="px-6 py-4 font-bold text-white font-mono">{p.code}</td>
                  <td className="px-6 py-4 font-mono text-slate-300">${p.value.toLocaleString('es-CO')}</td>
                  <td className="px-6 py-4 text-slate-400 text-sm">{new Date(p.effectiveFrom).toLocaleDateString('es-CO')}</td>
                  <td className="px-6 py-4 text-right">
                    <button onClick={() => handleDeleteParameter(p.id)} className="p-2 text-red-500 hover:bg-red-500/10 rounded-xl opacity-0 group-hover:opacity-100 transition-all" title="Eliminar">
                      <Trash2 className="w-5 h-5" />
                    </button>
                  </td>
                </tr>
              ))}
              {parameters.length === 0 && (
                <tr>
                  <td colSpan={4} className="px-6 py-12 text-center text-slate-400 font-medium">No hay parámetros registrados.</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}

      {showConceptModal && (
        <div className="fixed inset-0 bg-slate-900/80 backdrop-blur-md flex items-center justify-center z-50 animate-in fade-in duration-200">
          <div className="glass-panel rounded-3xl shadow-2xl p-8 w-full max-w-lg animate-in zoom-in-95 duration-200 border-slate-700 max-h-[90vh] overflow-y-auto">
            <h2 className="text-2xl font-bold text-white mb-6 flex items-center gap-2"><Percent className="w-6 h-6 text-indigo-400" /> Nuevo Concepto de Retención</h2>
            <form onSubmit={handleCreateConcept} className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-bold text-slate-300 mb-2">GroupKey (código interno)</label>
                  <input type="text" required className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500 font-mono placeholder:text-slate-500" value={conceptForm.groupKey} onChange={e => setConceptForm({ ...conceptForm, groupKey: e.target.value.toUpperCase() })} placeholder="COMPRAS_GENERALES" />
                </div>
                <div>
                  <label className="block text-sm font-bold text-slate-300 mb-2">Tipo de persona</label>
                  <select className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500" value={conceptForm.personType} onChange={e => setConceptForm({ ...conceptForm, personType: e.target.value })}>
                    <option value="Ambas">Ambas</option>
                    <option value="Natural">Natural</option>
                    <option value="Juridica">Jurídica</option>
                  </select>
                </div>
              </div>
              <div>
                <label className="block text-sm font-bold text-slate-300 mb-2">Rótulo genérico (selector del producto)</label>
                <input type="text" required className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500 placeholder:text-slate-500" value={conceptForm.groupLabel} onChange={e => setConceptForm({ ...conceptForm, groupLabel: e.target.value })} placeholder="Compras generales" />
              </div>
              <div>
                <label className="block text-sm font-bold text-slate-300 mb-2">Nombre de esta variante</label>
                <input type="text" required className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500 placeholder:text-slate-500" value={conceptForm.name} onChange={e => setConceptForm({ ...conceptForm, name: e.target.value })} placeholder="Compras generales (declarantes)" />
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-bold text-slate-300 mb-2">Categoría (Dataico)</label>
                  <select className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500" value={conceptForm.taxCategory} onChange={e => setConceptForm({ ...conceptForm, taxCategory: e.target.value })}>
                    <option value="RET_FUENTE">RET_FUENTE</option>
                    <option value="RET_IVA">RET_IVA</option>
                  </select>
                </div>
                <div>
                  <label className="block text-sm font-bold text-slate-300 mb-2">Se calcula sobre</label>
                  <select className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500" value={conceptForm.baseType} onChange={e => setConceptForm({ ...conceptForm, baseType: e.target.value })}>
                    <option value="Subtotal">{BASE_TYPE_LABELS.Subtotal}</option>
                    <option value="IvaGenerado">{BASE_TYPE_LABELS.IvaGenerado}</option>
                  </select>
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
                <button type="button" onClick={() => setShowConceptModal(false)} className="flex-1 py-3 px-4 bg-slate-100 hover:bg-slate-200 text-slate-700 font-bold rounded-xl transition-colors">Cancelar</button>
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
            <h2 className="text-2xl font-bold text-white mb-6">Nueva Vigencia de Parámetro</h2>
            <p className="text-sm text-slate-400 mb-4">Ej. cuando el gobierno actualiza el UVT: agrega una fila nueva con el valor y la fecha desde la que aplica, en vez de editar la anterior — así las facturas ya emitidas siguen calculando con el valor que tenían vigente.</p>
            <form onSubmit={handleCreateParameter} className="space-y-5">
              <div>
                <label className="block text-sm font-bold text-slate-300 mb-2">Código</label>
                <input type="text" required className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500 font-mono placeholder:text-slate-500" value={parameterForm.code} onChange={e => setParameterForm({ ...parameterForm, code: e.target.value.toUpperCase() })} placeholder="UVT" />
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
                <button type="button" onClick={() => setShowParameterModal(false)} className="flex-1 py-3 px-4 bg-slate-100 hover:bg-slate-200 text-slate-700 font-bold rounded-xl transition-colors">Cancelar</button>
                <button type="submit" className="flex-1 py-3 px-4 bg-indigo-600 hover:bg-indigo-700 text-white font-bold rounded-xl shadow-lg shadow-indigo-600/30 transition-all flex justify-center items-center">
                  <Save className="w-5 h-5 mr-2" /> Guardar
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
