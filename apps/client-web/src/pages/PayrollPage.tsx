import React, { useState, useEffect } from 'react';
import { Plus, Trash2, Loader2, ArrowLeft, Edit2, Send, AlertCircle, Eye, RotateCcw, XCircle, Search, Download, ChevronLeft, ChevronRight } from 'lucide-react';
import { toast } from 'sonner';
import { api, getErrorMessage } from '../lib/api';
import { useConfirm } from '@shared/components/ConfirmDialog';
import ImportExcelButton from '../components/ImportExcelButton';
import SearchableSelect from '@shared/components/SearchableSelect';
import { DATE_RANGE_PRESET_OPTIONS, getDateRangeForPreset, type DateRangePreset } from '../lib/dateRangePresets';
import { exportToCsv } from '../lib/exportCsv';
import { getNumberFormat, useNumberFormat } from '../lib/numberFormat';
import DecimalInput from '../components/DecimalInput';

const PAGE_SIZE = 25;

interface Concept {
  code?: string;
  description: string;
  amount: number;
  amountNs?: number;
  days?: number;
  percentage?: number;
  hours?: number;
  initialDate?: string;
  finalDate?: string;
  medicalLeaveType?: string;
  cesantiasInterest?: number;
  ordinaryCompensation?: number;
  extraordinaryCompensation?: number;
}

// Los conceptos cargados por Excel traen un código real de Dataico (ej. "BASICO",
// "AUXILIO_DE_TRANSPORTE") en vez de una descripción libre. Esta función lo vuelve legible sin
// tener que exponer todo el catálogo de códigos en la captura manual simplificada.
const humanizeConceptCode = (code: string) =>
  code.toLowerCase().split('_').map(w => w.charAt(0).toUpperCase() + w.slice(1)).join(' ');

const GENERIC_CODES = ['OTRO_CONCEPTO', 'OTRA_DEDUCCION'];

const conceptDetails = (c: Concept): string => {
  const parts: string[] = [];
  if (c.days) parts.push(`${c.days} día(s)`);
  if (c.percentage) parts.push(`${c.percentage}%`);
  if (c.hours) parts.push(`${c.hours}h`);
  if (c.amountNs) parts.push(`+$${getNumberFormat().number(c.amountNs, 3)} no salarial`);
  return parts.join(' · ');
};

export default function PayrollPage() {
  const fmt = useNumberFormat();
  const confirm = useConfirm();
  const [entries, setEntries] = useState<any[]>([]);
  const [employees, setEmployees] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [view, setView] = useState<'list' | 'create' | 'detail'>('list');
  const [submitting, setSubmitting] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [publishingId, setPublishingId] = useState<string | null>(null);
  const [datePreset, setDatePreset] = useState<DateRangePreset>('last-2-months');
  const [customFrom, setCustomFrom] = useState(() => getDateRangeForPreset('last-2-months')!.from);
  const [customTo, setCustomTo] = useState(() => getDateRangeForPreset('last-2-months')!.to);
  const [viewingEntry, setViewingEntry] = useState<any>(null);
  const [viewingRelated, setViewingRelated] = useState<any[]>([]);
  const [viewingOriginal, setViewingOriginal] = useState<any>(null);
  const [searchTerm, setSearchTerm] = useState('');
  const [page, setPage] = useState(1);

  const today = new Date().toISOString().substring(0, 10);
  const initialForm = {
    employeeId: '',
    prefix: 'NE',
    initialSettlementDate: today,
    finalSettlementDate: today,
    paymentDate: today,
    accruals: [{ description: '', amount: 0 }] as Concept[],
    deductions: [] as Concept[],
    referenceDocumentId: null as string | null,
    referenceConcept: '',
    noteType: '' as '' | 'ELIMINACION' | 'REEMPLAZO'
  };
  const [formData, setFormData] = useState(initialForm);

  useEffect(() => {
    loadData();
  }, [datePreset, customFrom, customTo]);

  useEffect(() => {
    setPage(1);
  }, [searchTerm, datePreset, customFrom, customTo]);

  const loadData = async () => {
    setLoading(true);
    try {
      const range = datePreset === 'custom' ? { from: customFrom, to: customTo } : getDateRangeForPreset(datePreset)!;
      const [entriesRes, empRes] = await Promise.all([
        api.get(`/client/payroll?from=${range.from}&to=${range.to}`),
        api.get('/client/customers?partyType=Empleado')
      ]);
      setEntries(entriesRes.data);
      setEmployees(empRes.data);
    } catch {
      toast.error('Error al cargar la nómina');
    } finally {
      setLoading(false);
    }
  };

  const addConcept = (list: 'accruals' | 'deductions') => {
    setFormData({ ...formData, [list]: [...formData[list], { description: '', amount: 0 }] });
  };
  const removeConcept = (list: 'accruals' | 'deductions', idx: number) => {
    setFormData({ ...formData, [list]: formData[list].filter((_, i) => i !== idx) });
  };
  const updateConcept = (list: 'accruals' | 'deductions', idx: number, field: keyof Concept, value: any) => {
    setFormData({ ...formData, [list]: formData[list].map((c, i) => i === idx ? { ...c, [field]: value } : c) });
  };

  const handleSaveDraft = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!formData.employeeId) { toast.error('Selecciona el empleado.'); return; }
    setSubmitting(true);
    try {
      if (editingId) {
        await api.put(`/client/payroll/${editingId}/draft`, formData);
        toast.success('Borrador actualizado');
      } else {
        await api.post('/client/payroll/draft', formData);
        toast.success('Borrador guardado');
      }
      setEditingId(null);
      setFormData(initialForm);
      setView('list');
      loadData();
    } catch (err: any) {
      toast.error(getErrorMessage(err, 'Error al guardar la nómina'));
    } finally {
      setSubmitting(false);
    }
  };

  const noteTypeOf = (typeCode: string): '' | 'ELIMINACION' | 'REEMPLAZO' =>
    typeCode === 'NE-ELIMINACION' ? 'ELIMINACION' : typeCode === 'NE-REEMPLAZO' ? 'REEMPLAZO' : '';

  const handleEditDraft = async (entry: any) => {
    try {
      const res = await api.get(`/client/payroll/${entry.id}`);
      const full = res.data;
      setEditingId(full.id);
      setFormData({
        employeeId: full.employeeId || '',
        prefix: full.prefix || 'NE',
        initialSettlementDate: (full.initialSettlementDate || today).substring(0, 10),
        finalSettlementDate: (full.finalSettlementDate || today).substring(0, 10),
        paymentDate: (full.paymentDate || today).substring(0, 10),
        accruals: (full.accruals || []).length ? full.accruals.map((a: any) => ({ ...a, description: a.description || '', amount: a.amount || 0 })) : [{ description: '', amount: 0 }],
        deductions: (full.deductions || []).map((d: any) => ({ ...d, description: d.description || '', amount: d.amount || 0 })),
        referenceDocumentId: full.referenceDocumentId || null,
        referenceConcept: full.referenceConcept || '',
        noteType: noteTypeOf(full.typeCode)
      });
      setView('create');
    } catch {
      toast.error('Error al cargar la nómina');
    }
  };

  const handleViewDetail = async (entry: any) => {
    try {
      const [fullRes, relatedRes] = await Promise.all([
        api.get(`/client/payroll/${entry.id}`),
        api.get(`/client/payroll/${entry.id}/related`)
      ]);
      setViewingEntry(fullRes.data);
      setViewingRelated(relatedRes.data);
      setViewingOriginal(null);
      if (fullRes.data.referenceDocumentId) {
        api.get(`/client/payroll/${fullRes.data.referenceDocumentId}`)
          .then(res => setViewingOriginal(res.data))
          .catch(() => {});
      }
      setView('detail');
    } catch {
      toast.error('Error al cargar el detalle de la nómina');
    }
  };

  const handleCreateDeletion = async (original: any) => {
    try {
      const res = await api.get(`/client/payroll/${original.id}`);
      const full = res.data;
      setEditingId(null);
      setFormData({
        ...initialForm,
        employeeId: full.employeeId || '',
        prefix: full.prefix || 'NE',
        initialSettlementDate: (full.initialSettlementDate || today).substring(0, 10),
        finalSettlementDate: (full.finalSettlementDate || today).substring(0, 10),
        paymentDate: (full.paymentDate || today).substring(0, 10),
        accruals: full.accruals || [],
        deductions: full.deductions || [],
        referenceDocumentId: full.id,
        referenceConcept: '',
        noteType: 'ELIMINACION'
      });
      setView('create');
    } catch {
      toast.error('Error al cargar la nómina original');
    }
  };

  const handleCreateReplacement = async (original: any) => {
    try {
      const res = await api.get(`/client/payroll/${original.id}`);
      const full = res.data;
      setEditingId(null);
      setFormData({
        employeeId: full.employeeId || '',
        prefix: full.prefix || 'NE',
        initialSettlementDate: (full.initialSettlementDate || today).substring(0, 10),
        finalSettlementDate: (full.finalSettlementDate || today).substring(0, 10),
        paymentDate: (full.paymentDate || today).substring(0, 10),
        accruals: (full.accruals || []).length ? full.accruals.map((a: any) => ({ ...a, description: a.description || '', amount: a.amount || 0 })) : [{ description: '', amount: 0 }],
        deductions: (full.deductions || []).map((d: any) => ({ ...d, description: d.description || '', amount: d.amount || 0 })),
        referenceDocumentId: full.id,
        referenceConcept: '',
        noteType: 'REEMPLAZO'
      });
      setView('create');
    } catch {
      toast.error('Error al cargar la nómina original');
    }
  };

  const handlePublish = async (id: string) => {
    setPublishingId(id);
    try {
      await api.post(`/client/payroll/${id}/publish`);
      toast.success('Nómina electrónica emitida correctamente');
      loadData();
      if (viewingEntry?.id === id) handleViewDetail({ id });
    } catch (err: any) {
      toast.error(getErrorMessage(err, 'Error al emitir la nómina'));
    } finally {
      setPublishingId(null);
    }
  };

  const handleDeleteDraft = async (id: string) => {
    if (!(await confirm('¿Eliminar esta nómina?'))) return;
    try {
      await api.delete(`/client/payroll/${id}`);
      toast.success('Nómina eliminada');
      if (viewingEntry?.id === id) { setViewingEntry(null); setView('list'); }
      loadData();
    } catch (err: any) {
      toast.error(getErrorMessage(err, 'Error al eliminar'));
    }
  };

  if (loading) return <div className="flex justify-center p-12"><Loader2 className="animate-spin w-8 h-8 text-primary" /></div>;

  if (view === 'create') {
    const totalAccruals = formData.accruals.reduce((s, c) => s + c.amount + (c.amountNs || 0), 0);
    const totalDeductions = formData.deductions.reduce((s, c) => s + c.amount, 0);

    const ConceptTable = ({ list, label }: { list: 'accruals' | 'deductions', label: string }) => (
      <div>
        <div className="flex justify-between items-center mb-2">
          <label className="block text-sm font-bold text-slate-700">{label}</label>
          <button type="button" onClick={() => addConcept(list)} className="text-xs font-bold text-primary hover:underline flex items-center gap-1">
            <Plus size={14} /> Agregar concepto
          </button>
        </div>
        <div className="space-y-2">
          {formData[list].map((c, idx) => {
            const isCoded = !!c.code && !GENERIC_CODES.includes(c.code);
            return (
              <div key={idx} className="flex items-center gap-2">
                {isCoded ? (
                  <div className="flex-1 px-3 py-2 bg-slate-50 border border-slate-200 rounded-lg text-sm">
                    <span className="font-bold text-slate-700">{humanizeConceptCode(c.code!)}</span>
                    {c.description && <span className="text-slate-500"> — {c.description}</span>}
                    {conceptDetails(c) && <span className="block text-xs text-slate-400">{conceptDetails(c)}</span>}
                  </div>
                ) : (
                  <input placeholder="Descripción del concepto" required value={c.description} onChange={e => updateConcept(list, idx, 'description', e.target.value)} className="flex-1 px-3 py-2 border rounded-lg text-sm outline-none" />
                )}
                <DecimalInput placeholder="Valor" value={c.amount} onValueChange={v => updateConcept(list, idx, 'amount', v)} className="w-32 px-3 py-2 border rounded-lg text-sm outline-none font-mono" />
                <button type="button" onClick={() => removeConcept(list, idx)} className="p-2 text-slate-400 hover:text-rose-600"><Trash2 size={16} /></button>
              </div>
            );
          })}
          {formData[list].length === 0 && <p className="text-xs text-slate-400">Sin conceptos.</p>}
        </div>
      </div>
    );

    return (
      <div className="p-8">
        <button onClick={() => { setEditingId(null); setView('list'); }} className="flex items-center gap-2 text-slate-500 hover:text-slate-800 mb-6 font-medium">
          <ArrowLeft size={18} /> Volver
        </button>
        <h1 className="text-3xl font-extrabold text-slate-800 mb-2">
          {formData.noteType === 'ELIMINACION' ? 'Nota de Eliminación de Nómina' : formData.noteType === 'REEMPLAZO' ? 'Nota de Reemplazo de Nómina' : editingId ? 'Editar Nómina Electrónica' : 'Nueva Nómina Electrónica'}
        </h1>
        <p className="text-slate-500 mb-8">
          {formData.noteType ? 'Revisa el motivo antes de guardar el borrador.' : 'Digita los conceptos y sus valores; el total se calcula automáticamente.'}
        </p>
        <form onSubmit={handleSaveDraft} className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6 space-y-6">
          {formData.referenceDocumentId && (
            <div className="p-6 bg-amber-50 border border-amber-200 rounded-xl">
              <h3 className="text-amber-800 font-bold mb-2">
                {formData.noteType === 'ELIMINACION' ? 'Generando Nota de Eliminación' : 'Generando Nota de Reemplazo'}
              </h3>
              <p className="text-sm text-amber-700 mb-4">
                {formData.noteType === 'ELIMINACION'
                  ? 'Esta nota anula ante la DIAN el comprobante de nómina original. No reenvía datos de nómina.'
                  : 'Esta nota anula el comprobante original y genera uno nuevo completo con los cambios que edites abajo.'}
              </p>
              <div>
                <label className="block text-sm font-bold text-amber-800 mb-2">Motivo</label>
                <input type="text" required value={formData.referenceConcept} onChange={e => setFormData({ ...formData, referenceConcept: e.target.value })} className="w-full px-4 py-2 bg-white border border-amber-200 rounded-xl focus:ring-2 focus:ring-amber-500 outline-none" />
              </div>
            </div>
          )}

          {formData.noteType !== 'ELIMINACION' && (
            <>
              <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                <div className="md:col-span-1">
                  <label className="block text-sm font-bold text-slate-700 mb-1">Empleado</label>
                  <SearchableSelect
                    required
                    value={formData.employeeId}
                    onChange={v => setFormData({ ...formData, employeeId: v })}
                    placeholder="Buscar empleado..."
                    options={employees.map(emp => ({ value: emp.id, label: `${emp.name} (${emp.identificationNumber})` }))}
                  />
                  {employees.length === 0 && <p className="text-xs text-amber-600 mt-1">No tienes empleados registrados. Créales un registro en Terceros.</p>}
                </div>
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-1">Inicio de Período</label>
                  <input type="date" required value={formData.initialSettlementDate} onChange={e => setFormData({ ...formData, initialSettlementDate: e.target.value })} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none" />
                </div>
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-1">Fin de Período</label>
                  <input type="date" required value={formData.finalSettlementDate} onChange={e => setFormData({ ...formData, finalSettlementDate: e.target.value })} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none" />
                </div>
              </div>

              <ConceptTable list="accruals" label="Devengos" />
              <ConceptTable list="deductions" label="Deducciones" />
            </>
          )}

          <div className="flex justify-between items-center pt-4 border-t border-slate-100">
            {formData.noteType !== 'ELIMINACION' ? (
              <div className="text-sm text-slate-600">
                <p>Devengado: <span className="font-bold">${fmt.number(totalAccruals, 3)}</span></p>
                <p>Deducciones: <span className="font-bold">${fmt.number(totalDeductions, 3)}</span></p>
                <p className="text-lg font-bold text-slate-800 mt-1">Neto a Pagar: ${fmt.number((totalAccruals - totalDeductions), 3)}</p>
              </div>
            ) : <div />}
            <button type="submit" disabled={submitting} className="px-6 py-3 bg-slate-900 hover:bg-black text-white font-bold rounded-xl transition-colors shadow-md disabled:opacity-50">
              {submitting ? 'Guardando...' : editingId ? 'Guardar Cambios' : 'Guardar Borrador'}
            </button>
          </div>
        </form>
      </div>
    );
  }

  if (view === 'detail' && viewingEntry) {
    const entry = viewingEntry;
    const noteType = noteTypeOf(entry.typeCode);
    const noteBadgeLabel = noteType === 'ELIMINACION' ? 'Eliminación' : noteType === 'REEMPLAZO' ? 'Reemplazo' : null;
    return (
      <div className="p-8">
        <button onClick={() => { setViewingEntry(null); setView('list'); }} className="flex items-center gap-2 text-slate-500 hover:text-slate-800 mb-6 font-medium transition-colors">
          <ArrowLeft size={20} /> Volver a nómina electrónica
        </button>

        <div className="bg-white rounded-3xl shadow-xl overflow-hidden border border-slate-100">
          <div className="p-8 border-b border-slate-100 bg-slate-50/50 flex justify-between items-start flex-wrap gap-4">
            <div>
              <div className="flex items-center gap-3 mb-1">
                {noteBadgeLabel && <span className="px-2 py-0.5 rounded text-xs font-bold bg-amber-100 text-amber-700">{noteBadgeLabel}</span>}
                <h2 className="text-2xl font-extrabold text-slate-800">{entry.number || 'Borrador'}</h2>
                <span className={`px-3 py-1 rounded-full text-xs font-bold ${
                  entry.status === 'DRAFT' ? 'bg-slate-100 text-slate-600' :
                  entry.status === 'APPROVED' ? 'bg-emerald-100 text-emerald-700' :
                  'bg-rose-100 text-rose-600'
                }`}>
                  {entry.status}
                </span>
              </div>
              <p className="text-slate-500">{new Date(entry.issueDate).toLocaleDateString('es-CO')} · {entry.customer?.name || '-'}</p>
              {entry.status === 'REJECTED' && entry.dianResponseMessage && (
                <p className="text-sm text-rose-500 mt-2">{entry.dianResponseMessage}</p>
              )}
            </div>
            <div className="flex gap-2 flex-wrap">
              {(entry.status === 'DRAFT' || entry.status === 'REJECTED') && (
                <>
                  <button onClick={() => handleEditDraft(entry)} className="px-4 py-2 text-slate-600 bg-slate-100 hover:bg-slate-200 rounded-xl font-bold text-sm transition-colors flex items-center gap-2">
                    <Edit2 size={16} /> Editar
                  </button>
                  <button onClick={() => handlePublish(entry.id)} disabled={publishingId === entry.id} className="px-4 py-2 text-white bg-primary hover:bg-primary/90 rounded-xl font-bold text-sm shadow-sm transition-all flex items-center gap-2 disabled:opacity-50">
                    <Send size={16} /> Emitir
                  </button>
                  <button onClick={() => handleDeleteDraft(entry.id)} className="px-4 py-2 text-rose-600 bg-rose-50 hover:bg-rose-100 rounded-xl font-bold text-sm transition-colors flex items-center gap-2">
                    <Trash2 size={16} /> Eliminar
                  </button>
                </>
              )}
              {entry.status === 'APPROVED' && !noteType && (
                <>
                  <button onClick={() => handleCreateReplacement(entry)} className="px-4 py-2 text-amber-700 bg-amber-100 hover:bg-amber-200 rounded-xl font-bold text-sm shadow-sm transition-all flex items-center gap-2">
                    <RotateCcw size={16} /> Generar Nota de Reemplazo
                  </button>
                  <button onClick={() => handleCreateDeletion(entry)} className="px-4 py-2 text-rose-700 bg-rose-100 hover:bg-rose-200 rounded-xl font-bold text-sm shadow-sm transition-all flex items-center gap-2">
                    <XCircle size={16} /> Generar Nota de Eliminación
                  </button>
                </>
              )}
            </div>
          </div>

          <div className="p-8 space-y-8">
            {(viewingOriginal || viewingRelated.length > 0) && (
              <div className="p-4 bg-slate-50 border border-slate-200 rounded-xl">
                <p className="text-xs font-bold text-slate-500 uppercase mb-2">Documentos Relacionados</p>
                <div className="space-y-1">
                  {viewingOriginal && (
                    <button onClick={() => handleViewDetail(viewingOriginal)} className="text-sm text-primary hover:underline block">
                      ← Comprobante original N° {viewingOriginal.number}
                    </button>
                  )}
                  {viewingRelated.map(r => (
                    <button key={r.id} onClick={() => handleViewDetail(r)} className="text-sm text-primary hover:underline flex items-center gap-2">
                      <span className="px-1.5 py-0.5 rounded text-[10px] font-bold bg-amber-100 text-amber-700">
                        {noteTypeOf(r.typeCode) === 'ELIMINACION' ? 'Eliminación' : 'Reemplazo'}
                      </span>
                      {r.number} · ${fmt.number(r.totalAmount, 3)} · {r.status}
                    </button>
                  ))}
                </div>
              </div>
            )}

            {entry.referenceConcept && (
              <p className="text-sm text-slate-600"><span className="font-bold">Motivo:</span> {entry.referenceConcept}</p>
            )}

            {noteType !== 'ELIMINACION' && (
              <>
                <div>
                  <p className="text-xs font-bold text-slate-500 uppercase mb-2">Devengos</p>
                  <div className="border border-slate-200 rounded-2xl overflow-hidden">
                    <table className="w-full text-left border-collapse">
                      <tbody className="divide-y divide-slate-200">
                        {(entry.accruals || []).map((c: any, idx: number) => (
                          <tr key={idx}>
                            <td className="p-3 text-sm text-slate-700">{c.code ? humanizeConceptCode(c.code) : c.description}</td>
                            <td className="p-3 text-sm font-mono font-bold text-right">${fmt.number((c.amount || 0), 3)}</td>
                          </tr>
                        ))}
                        {(!entry.accruals || entry.accruals.length === 0) && <tr><td className="p-3 text-sm text-slate-400">No hay devengos.</td></tr>}
                      </tbody>
                    </table>
                  </div>
                </div>
                <div>
                  <p className="text-xs font-bold text-slate-500 uppercase mb-2">Deducciones</p>
                  <div className="border border-slate-200 rounded-2xl overflow-hidden">
                    <table className="w-full text-left border-collapse">
                      <tbody className="divide-y divide-slate-200">
                        {(entry.deductions || []).map((c: any, idx: number) => (
                          <tr key={idx}>
                            <td className="p-3 text-sm text-slate-700">{c.code ? humanizeConceptCode(c.code) : c.description}</td>
                            <td className="p-3 text-sm font-mono font-bold text-right">${fmt.number((c.amount || 0), 3)}</td>
                          </tr>
                        ))}
                        {(!entry.deductions || entry.deductions.length === 0) && <tr><td className="p-3 text-sm text-slate-400">No hay deducciones.</td></tr>}
                      </tbody>
                    </table>
                  </div>
                </div>
              </>
            )}

            <div className="flex justify-end">
              <p className="text-lg font-bold text-slate-800">Neto: ${fmt.number(entry.totalAmount, 3)}</p>
            </div>
          </div>
        </div>
      </div>
    );
  }

  const filteredEntries = entries.filter(e => {
    if (!searchTerm.trim()) return true;
    const q = searchTerm.trim().toLowerCase();
    return (e.consecutiveNumber || '').toLowerCase().includes(q) ||
      (e.customer?.name || '').toLowerCase().includes(q) ||
      (e.customer?.identificationNumber || '').toLowerCase().includes(q);
  });
  const totalPages = Math.max(1, Math.ceil(filteredEntries.length / PAGE_SIZE));
  const currentPage = Math.min(page, totalPages);
  const paginatedEntries = filteredEntries.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE);
  const filteredTotal = filteredEntries.reduce((sum, e) => sum + (e.totalAmount || 0), 0);
  const periodOf = (e: any) => e.initialSettlementDate && e.finalSettlementDate
    ? `${new Date(e.initialSettlementDate).toLocaleDateString('es-CO')} - ${new Date(e.finalSettlementDate).toLocaleDateString('es-CO')}`
    : '-';

  const handleExport = () => exportToCsv(
    `nomina_${customFrom}_a_${customTo}.csv`,
    filteredEntries.map(e => ({
      consecutivo: e.consecutiveNumber || '',
      empleado: e.customer?.name || '',
      identificacion: e.customer?.identificationNumber || '',
      periodo: periodOf(e),
      neto: e.totalAmount || 0,
      estado: e.status,
      fecha: new Date(e.issueDate).toLocaleDateString('es-CO')
    })),
    [
      { key: 'consecutivo', label: 'Consecutivo' },
      { key: 'empleado', label: 'Empleado' },
      { key: 'identificacion', label: 'Identificación' },
      { key: 'periodo', label: 'Período' },
      { key: 'neto', label: 'Neto' },
      { key: 'estado', label: 'Estado' },
      { key: 'fecha', label: 'Fecha' }
    ]
  );

  return (
    <div className="p-8">
      <div className="flex justify-between items-center mb-8">
        <div>
          <h1 className="text-3xl font-extrabold text-slate-800">Nómina electrónica</h1>
          <p className="text-slate-500 mt-1">Historial de nómina emitida</p>
        </div>
        <div className="flex gap-3">
          <ImportExcelButton endpoint="/client/payroll/import" templateEndpoint="/client/payroll/template" label="Importar Excel" onDone={loadData} />
          <button onClick={() => { setEditingId(null); setFormData(initialForm); setView('create'); }} className="bg-primary hover:bg-primary/90 text-white px-5 py-2.5 rounded-xl font-bold flex items-center gap-2 shadow-sm transition-all">
            <Plus size={20} /> Nueva Nómina
          </button>
        </div>
      </div>

      <div className="flex flex-wrap items-end gap-3 mb-4">
        <div className="w-52">
          <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Periodo</label>
          <SearchableSelect
            value={datePreset}
            onChange={v => setDatePreset(v as DateRangePreset)}
            placeholder="Periodo..."
            options={DATE_RANGE_PRESET_OPTIONS}
          />
        </div>
        {datePreset === 'custom' && (
          <>
            <div>
              <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Desde</label>
              <input type="date" value={customFrom} onChange={e => setCustomFrom(e.target.value)} className="p-2.5 bg-white border border-slate-200 rounded-xl text-sm outline-none focus:ring-2 focus:ring-primary" />
            </div>
            <div>
              <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Hasta</label>
              <input type="date" value={customTo} onChange={e => setCustomTo(e.target.value)} className="p-2.5 bg-white border border-slate-200 rounded-xl text-sm outline-none focus:ring-2 focus:ring-primary" />
            </div>
          </>
        )}
        <div className="flex-1 min-w-[220px]">
          <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Buscar</label>
          <div className="relative">
            <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
            <input type="text" value={searchTerm} onChange={e => setSearchTerm(e.target.value)} placeholder="Consecutivo, empleado o identificación..." className="w-full pl-9 pr-3 py-2.5 bg-white border border-slate-200 rounded-xl text-sm outline-none focus:ring-2 focus:ring-primary" />
          </div>
        </div>
        <button onClick={handleExport} disabled={filteredEntries.length === 0} className="flex items-center gap-2 px-4 py-2.5 bg-slate-100 hover:bg-slate-200 text-slate-600 rounded-xl font-bold text-sm transition-colors disabled:opacity-50">
          <Download size={16} /> Exportar CSV
        </button>
        <p className="text-xs text-slate-400 pb-2.5">{filteredEntries.length} comprobante{filteredEntries.length === 1 ? '' : 's'} en el periodo</p>
      </div>

      <div className="bg-white rounded-2xl shadow-sm border border-slate-200 overflow-hidden">
        <table className="w-full text-left border-collapse">
          <thead>
            <tr className="bg-slate-50 border-b border-slate-200 text-sm font-bold text-slate-500 uppercase tracking-wider">
              <th className="p-4">Consecutivo</th>
              <th className="p-4">Empleado</th>
              <th className="p-4">Período</th>
              <th className="p-4 text-right">Neto</th>
              <th className="p-4">Estado</th>
              <th className="p-4">Fecha</th>
              <th className="p-4 text-right">Acciones</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {paginatedEntries.map(e => (
              <tr key={e.id} className="hover:bg-slate-50/50 transition-colors">
                <td className="p-4 font-mono text-sm text-slate-600">{e.consecutiveNumber || '-'}</td>
                <td className="p-4 font-bold text-slate-800">
                  <div className="flex items-center gap-2">
                    {noteTypeOf(e.typeCode) === 'ELIMINACION' && <span className="px-2 py-0.5 rounded text-xs font-bold bg-rose-100 text-rose-700">Eliminación</span>}
                    {noteTypeOf(e.typeCode) === 'REEMPLAZO' && <span className="px-2 py-0.5 rounded text-xs font-bold bg-amber-100 text-amber-700">Reemplazo</span>}
                    {e.customer?.name || '-'}
                  </div>
                </td>
                <td className="p-4 text-slate-500 text-sm">{periodOf(e)}</td>
                <td className="p-4 text-right font-medium">${fmt.number(e.totalAmount, 3)}</td>
                <td className="p-4">
                  <span className={`px-3 py-1 rounded-full text-xs font-bold ${
                    e.status === 'DRAFT' ? 'bg-slate-100 text-slate-600' :
                    e.status === 'APPROVED' ? 'bg-emerald-100 text-emerald-700' :
                    'bg-rose-100 text-rose-600'
                  }`}>
                    {e.status}
                  </span>
                </td>
                <td className="p-4 text-slate-500 text-sm">{new Date(e.issueDate).toLocaleDateString('es-CO')}</td>
                <td className="p-4 flex items-center justify-end gap-2">
                  {(e.status === 'DRAFT' || e.status === 'REJECTED') && (
                    <>
                      {e.status === 'REJECTED' && e.dianResponseMessage && (
                        <span title={e.dianResponseMessage} className="p-2 text-rose-500"><AlertCircle size={16} /></span>
                      )}
                      <button onClick={() => handleEditDraft(e)} title="Editar" className="p-2 text-slate-400 hover:text-blue-600 hover:bg-blue-50 rounded-lg transition-colors">
                        <Edit2 size={16} />
                      </button>
                      <button onClick={() => handlePublish(e.id)} disabled={publishingId === e.id} title="Emitir a la DIAN" className="p-2 text-white bg-primary hover:bg-primary/90 rounded-lg shadow-sm transition-all flex items-center gap-1 text-sm font-bold disabled:opacity-50">
                        <Send size={16} /> Emitir
                      </button>
                      <button onClick={() => handleDeleteDraft(e.id)} title="Eliminar" className="p-2 text-slate-400 hover:text-rose-600 hover:bg-rose-50 rounded-lg transition-colors">
                        <Trash2 size={16} />
                      </button>
                    </>
                  )}
                  <button onClick={() => handleViewDetail(e)} title="Ver detalle" className="p-2 text-slate-400 hover:text-primary hover:bg-slate-100 rounded-lg transition-colors">
                    <Eye size={16} />
                  </button>
                </td>
              </tr>
            ))}
            {filteredEntries.length === 0 && (
              <tr><td colSpan={7} className="p-8 text-center text-slate-500">No hay nóminas que coincidan con el filtro.</td></tr>
            )}
          </tbody>
          {filteredEntries.length > 0 && (
            <tfoot>
              <tr className="bg-slate-50 border-t-2 border-slate-200 font-bold text-slate-700">
                <td colSpan={3} className="p-4 text-right">Total del periodo:</td>
                <td className="p-4 text-right font-mono">${fmt.number(filteredTotal, 3)}</td>
                <td colSpan={3}></td>
              </tr>
            </tfoot>
          )}
        </table>
      </div>

      {totalPages > 1 && (
        <div className="flex justify-between items-center mt-4">
          <p className="text-sm text-slate-500">Página {currentPage} de {totalPages}</p>
          <div className="flex gap-2">
            <button onClick={() => setPage(p => Math.max(1, p - 1))} disabled={currentPage === 1} className="p-2 bg-white border border-slate-200 rounded-lg text-slate-500 hover:bg-slate-50 disabled:opacity-40 transition-colors">
              <ChevronLeft size={18} />
            </button>
            <button onClick={() => setPage(p => Math.min(totalPages, p + 1))} disabled={currentPage === totalPages} className="p-2 bg-white border border-slate-200 rounded-lg text-slate-500 hover:bg-slate-50 disabled:opacity-40 transition-colors">
              <ChevronRight size={18} />
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
