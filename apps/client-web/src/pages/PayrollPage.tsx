import React, { useState, useEffect } from 'react';
import { Plus, Trash2, Loader2, ArrowLeft, Edit2, Send, AlertCircle, Info, Printer, RotateCcw, XCircle, Search, Download, ChevronLeft, ChevronRight } from 'lucide-react';
import { toast } from 'sonner';
import { api, getErrorMessage } from '../lib/api';
import { useConfirm } from '@/components/ConfirmDialog';
import ImportExcelButton from '../components/ImportExcelButton';
import ResponsiveList, { type ResponsiveListColumn } from '../components/ResponsiveList';
import RowIconButton from '../components/RowIconButton';
import SearchableSelect from '@shared/components/SearchableSelect';
import { DATE_RANGE_PRESET_OPTIONS, getDateRangeForPreset, type DateRangePreset } from '../lib/dateRangePresets';
import { exportToCsv } from '../lib/exportCsv';
import { STATUS_FILTER_OPTIONS, statusLabel } from '../lib/documentStatus';
import DianResponseDialog, { hasDianResponse } from '../components/DianResponseDialog';
import { usePdfPreview } from '../lib/pdfPreview';
import { getNumberFormat, useNumberFormat } from '../lib/numberFormat';
import { BatchActionBar, BatchProgressDialog } from '../components/BatchActions';
import { isBatchEditable, useBatchRunner, useBatchSelection } from '../lib/batch';
import DecimalInput from '../components/DecimalInput';
import { todayColombia } from '../lib/colombiaTime';

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
  // Las fechas Desde/Hasta solo se aplican al pulsar Filtrar, como en facturas.
  const [appliedCustomFrom, setAppliedCustomFrom] = useState(() => getDateRangeForPreset('last-2-months')!.from);
  const [appliedCustomTo, setAppliedCustomTo] = useState(() => getDateRangeForPreset('last-2-months')!.to);
  const [statusFilter, setStatusFilter] = useState('');
  const [responseEntry, setResponseEntry] = useState<any>(null);
  const { previewingId, preview: handlePreviewPdf } = usePdfPreview('/client/payroll');
  const [viewingEntry, setViewingEntry] = useState<any>(null);
  const [viewingRelated, setViewingRelated] = useState<any[]>([]);
  const [viewingOriginal, setViewingOriginal] = useState<any>(null);
  const [searchTerm, setSearchTerm] = useState('');
  const [page, setPage] = useState(1);

  const today = todayColombia();
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
  }, [datePreset, appliedCustomFrom, appliedCustomTo]);

  useEffect(() => {
    setPage(1);
  }, [searchTerm, statusFilter, datePreset, appliedCustomFrom, appliedCustomTo]);

  const loadData = async ({ quiet = false } = {}) => {
    // quiet: refresca la lista sin el spinner de página completa (no desmonta el avance de un lote en curso).
    if (!quiet) setLoading(true);
    try {
      const range = datePreset === 'custom' ? { from: appliedCustomFrom, to: appliedCustomTo } : getDateRangeForPreset(datePreset)!;
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

  // Acciones masivas: emitir o eliminar varios a la vez; el servidor los procesa de a uno.
  const batchSelection = useBatchSelection();
  const batch = useBatchRunner('/client/payroll', failedIds => {
    // Los que fallaron quedan seleccionados para corregirlos y reintentar.
    batchSelection.replace(failedIds);
    loadData({ quiet: true });
  });

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

  // Anular (eliminación) y modificar (reemplazo) piden lo mismo: el motivo. Al modificar, cuando Dataico aprueba la eliminación se crea
  // un borrador nuevo con los datos del original.
  const handleCreateNote = async (original: any, noteType: 'ELIMINACION' | 'REEMPLAZO') => {
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
        noteType
      });
      setView('create');
    } catch {
      toast.error('Error al cargar la nómina original');
    }
  };

  const handlePublish = async (id: string) => {
    setPublishingId(id);
    try {
      const res = await api.post(`/client/payroll/${id}/publish`);
      toast.success('Nómina electrónica emitida correctamente');
      loadData();
      if (res.data?.newDraftId) {
        // Nota de reemplazo aprobada: se abre el borrador nuevo para que el usuario lo corrija y lo emita.
        toast.success('Se creó un borrador nuevo con los datos de la nómina anulada. Corrígelo y emítelo.');
        await handleEditDraft({ id: res.data.newDraftId });
      } else if (viewingEntry?.id === id) {
        handleViewDetail({ id });
      }
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

  const statusBadge = (status: string) => (
    <span className={`px-3 py-1 rounded-full text-xs font-bold ${
      status === 'DRAFT' ? 'bg-slate-100 text-slate-600' :
      status === 'PROCESSING' ? 'bg-blue-100 text-blue-600' :
      status === 'APPROVED' ? 'bg-emerald-100 text-emerald-700' :
      'bg-rose-100 text-rose-600'
    }`}>
      {statusLabel(status)}
    </span>
  );

  // Un comprobante con una nota de eliminación o de reemplazo aprobada está anulado: se muestra así en vez de "Emitida".
  const entryStatusBadge = (e: any) => e.annulledBy ? (
    <span className="px-3 py-1 rounded-full text-xs font-bold bg-slate-200 text-slate-600">{e.annulledBy === 'NE-REEMPLAZO' ? 'Reemplazada' : 'Anulada'}</span>
  ) : statusBadge(e.status);

  const responseDialog = responseEntry && (
    <DianResponseDialog doc={responseEntry} label={`Nómina ${responseEntry.consecutiveNumber || responseEntry.number || 'Borrador'}`} onClose={() => setResponseEntry(null)} />
  );

  if (view === 'create') {
    const totalAccruals = formData.accruals.reduce((s, c) => s + c.amount + (c.amountNs || 0), 0);
    const totalDeductions = formData.deductions.reduce((s, c) => s + c.amount, 0);

    // Función de render, no componente: definirla como componente dentro del render la remonta en cada
    // tecla y el campo que se está escribiendo pierde el foco.
    const renderConceptTable = (list: 'accruals' | 'deductions', label: string) => (
      <div>
        <div className="flex justify-between items-center mb-2 gap-2">
          <label className="block text-sm font-bold text-slate-700">{label}</label>
          <button type="button" onClick={() => addConcept(list)} className="shrink-0 px-2 py-2.5 text-xs font-bold text-primary hover:underline flex items-center gap-1">
            <Plus size={14} /> Agregar concepto
          </button>
        </div>
        <div className="space-y-3 sm:space-y-2">
          {formData[list].map((c, idx) => {
            const isCoded = !!c.code && !GENERIC_CODES.includes(c.code);
            return (
              // En móvil la descripción ocupa toda la fila y debajo van el valor y el botón de quitar.
              <div key={idx} className="flex flex-wrap items-center gap-2">
                {isCoded ? (
                  <div className="w-full min-w-0 break-words px-3 py-2 bg-slate-50 border border-slate-200 rounded-lg text-sm sm:w-auto sm:flex-1">
                    <span className="font-bold text-slate-700">{humanizeConceptCode(c.code!)}</span>
                    {c.description && <span className="text-slate-500"> — {c.description}</span>}
                    {conceptDetails(c) && <span className="block text-xs text-slate-400">{conceptDetails(c)}</span>}
                  </div>
                ) : (
                  <input placeholder="Descripción del concepto" required value={c.description} onChange={e => updateConcept(list, idx, 'description', e.target.value)} className="w-full min-w-0 px-3 py-2 border rounded-lg text-sm outline-none sm:w-auto sm:flex-1" />
                )}
                <DecimalInput placeholder="Valor" value={c.amount} onValueChange={v => updateConcept(list, idx, 'amount', v)} className="min-w-0 flex-1 px-3 py-2 border rounded-lg text-sm outline-none font-mono sm:w-32 sm:flex-none" />
                <button type="button" aria-label="Quitar concepto" onClick={() => removeConcept(list, idx)} className="flex h-11 w-11 shrink-0 items-center justify-center rounded-lg text-slate-400 hover:text-rose-600 md:h-9 md:w-9"><Trash2 size={16} /></button>
              </div>
            );
          })}
          {formData[list].length === 0 && <p className="text-xs text-slate-400">Sin conceptos.</p>}
        </div>
      </div>
    );

    return (
      <div className="p-4 sm:p-6 lg:p-8">
        <button onClick={() => { setEditingId(null); setView('list'); }} className="flex items-center gap-2 py-2 text-slate-500 hover:text-slate-800 mb-4 sm:mb-6 font-medium">
          <ArrowLeft size={18} /> Volver
        </button>
        <h1 className="text-2xl sm:text-3xl font-extrabold text-slate-800 mb-2">
          {formData.noteType === 'ELIMINACION' ? 'Nota de Eliminación de Nómina' : formData.noteType === 'REEMPLAZO' ? 'Nota de Reemplazo de Nómina' : editingId ? 'Editar Nómina Electrónica' : 'Nueva Nómina Electrónica'}
        </h1>
        <p className="text-slate-500 mb-6 sm:mb-8">
          {formData.noteType ? 'Revisa el motivo antes de guardar el borrador.' : 'Digita los conceptos y sus valores; el total se calcula automáticamente.'}
        </p>
        <form onSubmit={handleSaveDraft} className="bg-white rounded-2xl border border-slate-200 shadow-sm p-4 sm:p-6 space-y-6">
          {formData.referenceDocumentId && (
            <div className="p-4 sm:p-6 bg-amber-50 border border-amber-200 rounded-xl">
              <h3 className="text-amber-800 font-bold mb-2">
                {formData.noteType === 'ELIMINACION' ? 'Generando Nota de Eliminación' : 'Generando Nota de Reemplazo'}
              </h3>
              <p className="text-sm text-amber-700 mb-4">
                {formData.noteType === 'ELIMINACION'
                  ? 'Esta nota anula ante la DIAN el comprobante de nómina original. No reenvía datos de nómina.'
                  : 'Esta nota anula ante la DIAN el comprobante original. Cuando Dataico la apruebe se crea un borrador nuevo con los mismos datos para que lo corrijas y lo emitas.'}
              </p>
              <div>
                <label className="block text-sm font-bold text-amber-800 mb-2">Motivo</label>
                <input type="text" required value={formData.referenceConcept} onChange={e => setFormData({ ...formData, referenceConcept: e.target.value })} className="w-full px-4 py-2 bg-white border border-amber-200 rounded-xl focus:ring-2 focus:ring-amber-500 outline-none" />
              </div>
            </div>
          )}

          {!formData.noteType && (
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

              {renderConceptTable('accruals', 'Devengos')}
              {renderConceptTable('deductions', 'Deducciones')}
            </>
          )}

          <div className="flex flex-col gap-4 pt-4 border-t border-slate-100 sm:flex-row sm:items-center sm:justify-between">
            {!formData.noteType ? (
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
    // Botones de acción del encabezado: 44 px de alto en móvil/tableta.
    const actionButton = 'px-4 py-3 md:py-2 rounded-xl font-bold text-sm transition-colors flex items-center gap-2';

    const conceptList = (items: any[] | undefined, emptyText: string) => (
      <div className="border border-slate-200 rounded-2xl overflow-hidden">
        <table className="w-full text-left border-collapse">
          <tbody className="divide-y divide-slate-200">
            {(items || []).map((c: any, idx: number) => (
              <tr key={idx}>
                <td className="p-3 text-sm text-slate-700 break-words">{c.code ? humanizeConceptCode(c.code) : c.description}</td>
                <td className="p-3 text-sm font-mono font-bold text-right whitespace-nowrap">${fmt.number((c.amount || 0), 3)}</td>
              </tr>
            ))}
            {(!items || items.length === 0) && <tr><td className="p-3 text-sm text-slate-400">{emptyText}</td></tr>}
          </tbody>
        </table>
      </div>
    );

    return (
      <div className="p-4 sm:p-6 lg:p-8">
        <button onClick={() => { setViewingEntry(null); setView('list'); }} className="flex items-center gap-2 py-2 text-slate-500 hover:text-slate-800 mb-4 sm:mb-6 font-medium transition-colors">
          <ArrowLeft size={20} /> Volver a nómina electrónica
        </button>

        <div className="bg-white rounded-3xl shadow-xl overflow-hidden border border-slate-100">
          <div className="p-4 sm:p-6 lg:p-8 border-b border-slate-100 bg-slate-50/50 flex justify-between items-start flex-wrap gap-4">
            <div className="min-w-0">
              <div className="flex items-center gap-3 mb-1 flex-wrap">
                {noteBadgeLabel && <span className="px-2 py-0.5 rounded text-xs font-bold bg-amber-100 text-amber-700">{noteBadgeLabel}</span>}
                <h2 className="text-2xl font-extrabold text-slate-800 break-words">{entry.number || 'Borrador'}</h2>
                {entryStatusBadge(entry)}
              </div>
              <p className="text-slate-500 break-words">{new Date(entry.issueDate).toLocaleDateString('es-CO')} · {entry.customer?.name || '-'}</p>
            </div>
            <div className="flex gap-2 flex-wrap">
              <button onClick={() => handlePreviewPdf(entry.id)} disabled={previewingId === entry.id} className={`${actionButton} text-slate-600 bg-slate-100 hover:bg-slate-200 disabled:opacity-50`}>
                {previewingId === entry.id ? <Loader2 size={16} className="animate-spin" /> : <Printer size={16} />} Vista Previa
              </button>
              {hasDianResponse(entry) && (
                <button
                  onClick={() => setResponseEntry(entry)}
                  className={`${actionButton} ${entry.status === 'REJECTED' ? 'text-rose-700 bg-rose-50 hover:bg-rose-100' : 'text-slate-600 bg-slate-100 hover:bg-slate-200'}`}
                >
                  {entry.status === 'REJECTED' ? <AlertCircle size={16} /> : <Info size={16} />}
                  {entry.status === 'REJECTED' ? 'Motivo del rechazo' : 'Respuesta de la DIAN'}
                </button>
              )}
              {(entry.status === 'DRAFT' || entry.status === 'REJECTED') && (
                <>
                  <button onClick={() => handleEditDraft(entry)} className={`${actionButton} text-slate-600 bg-slate-100 hover:bg-slate-200`}>
                    <Edit2 size={16} /> Editar
                  </button>
                  <button onClick={() => handlePublish(entry.id)} disabled={publishingId === entry.id} className={`${actionButton} text-white bg-primary hover:bg-primary/90 shadow-sm disabled:opacity-50`}>
                    <Send size={16} /> Emitir
                  </button>
                  <button onClick={() => handleDeleteDraft(entry.id)} className={`${actionButton} text-rose-600 bg-rose-50 hover:bg-rose-100`}>
                    <Trash2 size={16} /> Eliminar
                  </button>
                </>
              )}
              {entry.status === 'APPROVED' && !noteType && !entry.annulledBy && (
                <>
                  <button onClick={() => handleCreateNote(entry, 'REEMPLAZO')} title="Anula la nómina y crea un borrador nuevo para corregirla" className={`${actionButton} text-amber-700 bg-amber-100 hover:bg-amber-200 shadow-sm`}>
                    <RotateCcw size={16} /> Generar Nota de Reemplazo
                  </button>
                  <button onClick={() => handleCreateNote(entry, 'ELIMINACION')} title="Anula la nómina" className={`${actionButton} text-rose-700 bg-rose-100 hover:bg-rose-200 shadow-sm`}>
                    <XCircle size={16} /> Generar Nota de Eliminación
                  </button>
                </>
              )}
            </div>
          </div>

          <div className="p-4 sm:p-6 lg:p-8 space-y-6 lg:space-y-8">
            {(viewingOriginal || viewingRelated.length > 0) && (
              <div className="p-4 bg-slate-50 border border-slate-200 rounded-xl">
                <p className="text-xs font-bold text-slate-500 uppercase mb-2">Documentos Relacionados</p>
                <div className="space-y-1">
                  {viewingOriginal && (
                    <button onClick={() => handleViewDetail(viewingOriginal)} className="py-1.5 text-sm text-primary hover:underline block text-left">
                      ← {noteType ? 'Comprobante original' : 'Nómina anulada que corrige'} N° {viewingOriginal.number}
                    </button>
                  )}
                  {viewingRelated.map(r => (
                    <button key={r.id} onClick={() => handleViewDetail(r)} className="py-1.5 text-sm text-primary hover:underline flex flex-wrap items-center gap-2 text-left">
                      <span className="px-1.5 py-0.5 rounded text-[10px] font-bold bg-amber-100 text-amber-700">
                        {noteTypeOf(r.typeCode) === 'ELIMINACION' ? 'Eliminación' : noteTypeOf(r.typeCode) === 'REEMPLAZO' ? 'Reemplazo' : 'Nómina nueva'}
                      </span>
                      {r.number || 'Borrador'} · ${fmt.number(r.totalAmount, 3)} · {statusLabel(r.status)}
                    </button>
                  ))}
                </div>
              </div>
            )}

            {entry.referenceConcept && (
              <p className="text-sm text-slate-600 break-words"><span className="font-bold">Motivo:</span> {entry.referenceConcept}</p>
            )}

            {!noteType && (
              <>
                <div>
                  <p className="text-xs font-bold text-slate-500 uppercase mb-2">Devengos</p>
                  {conceptList(entry.accruals, 'No hay devengos.')}
                </div>
                <div>
                  <p className="text-xs font-bold text-slate-500 uppercase mb-2">Deducciones</p>
                  {conceptList(entry.deductions, 'No hay deducciones.')}
                </div>
              </>
            )}

            <div className="flex justify-end">
              <p className="text-lg font-bold text-slate-800">Neto: ${fmt.number(entry.totalAmount, 3)}</p>
            </div>
          </div>
        </div>
        {responseDialog}
      </div>
    );
  }

  const filteredEntries = entries.filter(e => {
    if (statusFilter && e.status !== statusFilter) return false;
    if (!searchTerm.trim()) return true;
    const q = searchTerm.trim().toLowerCase();
    return (e.consecutiveNumber || '').toLowerCase().includes(q) ||
      (e.customer?.name || '').toLowerCase().includes(q) ||
      (e.customer?.identificationNumber || '').toLowerCase().includes(q);
  });
  const totalPages = Math.max(1, Math.ceil(filteredEntries.length / PAGE_SIZE));
  const currentPage = Math.min(page, totalPages);
  const paginatedEntries = filteredEntries.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE);
  const selectedRows = filteredEntries.filter(r => batchSelection.selected.has(r.id));
  const notEditableCount = selectedRows.filter(r => !isBatchEditable(r.status)).length;
  const batchRunning = batch.progress !== null && !batch.progress.finished;
  const batchLabelOf = (id: string) => {
    const r = entries.find(x => x.id === id);
    return r ? `${r.consecutiveNumber || 'Borrador'} · ${r.customer?.name || '-'}` : id;
  };
  const runBatch = async (kind: 'publish' | 'delete') => {
    const n = selectedRows.length;
    const what = n === 1 ? 'nómina' : 'nóminas';
    const confirmed = kind === 'publish'
      ? await confirm(`Se emitirán ${n} ${what} a la DIAN, de a uno y en orden. ¿Continuar?`, { title: 'Emitir en lote', confirmText: 'Emitir', destructive: false })
      : await confirm(`Se eliminarán ${n} ${what}. Esta acción no se puede deshacer. ¿Continuar?`, { title: 'Eliminar en lote', confirmText: 'Eliminar' });
    if (!confirmed) return;
    batch.run(kind, selectedRows.map(r => r.id));
  };
  const batchSelectionProps = {
    isSelected: (r: any) => batchSelection.selected.has(r.id),
    onToggle: (r: any) => batchSelection.toggle(r.id),
    onToggleAll: () => batchSelection.toggleMany(paginatedEntries.map(r => r.id)),
    allSelected: paginatedEntries.length > 0 && paginatedEntries.every(r => batchSelection.selected.has(r.id)),
    someSelected: paginatedEntries.some(r => batchSelection.selected.has(r.id)),
    label: (r: any) => `Seleccionar nómina ${r.consecutiveNumber || 'borrador'}`,
  };
  // Las notas de eliminación/reemplazo y los comprobantes anulados no suman al total del periodo.
  const countedEntries = filteredEntries.filter(e => !noteTypeOf(e.typeCode) && !e.annulledBy);
  const filteredAccruals = countedEntries.reduce((sum, e) => sum + (e.totalAccruals || 0), 0);
  const filteredDeductions = countedEntries.reduce((sum, e) => sum + (e.totalDeductions || 0), 0);
  const filteredTotal = countedEntries.reduce((sum, e) => sum + (e.totalAmount || 0), 0);
  const periodOf = (e: any) => e.initialSettlementDate && e.finalSettlementDate
    ? `${new Date(e.initialSettlementDate).toLocaleDateString('es-CO')} - ${new Date(e.finalSettlementDate).toLocaleDateString('es-CO')}`
    : '-';

  const handleExport = () => exportToCsv(
    `nomina_${appliedCustomFrom}_a_${appliedCustomTo}.csv`,
    filteredEntries.map(e => ({
      consecutivo: e.consecutiveNumber || '',
      empleado: e.customer?.name || '',
      identificacion: e.customer?.identificationNumber || '',
      periodo: periodOf(e),
      neto: e.totalAmount || 0,
      estado: statusLabel(e.status),
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

  const columns: ResponsiveListColumn<any>[] = [
    { key: 'consecutive', header: 'Consecutivo', cellClassName: 'font-mono text-sm text-slate-600', render: e => e.consecutiveNumber || '-' },
    {
      key: 'employee',
      header: 'Empleado',
      primary: true,
      cellClassName: 'font-bold text-slate-800',
      render: e => (
        <div className="flex flex-wrap items-center gap-2">
          {noteTypeOf(e.typeCode) === 'ELIMINACION' && <span className="px-2 py-0.5 rounded text-xs font-bold bg-rose-100 text-rose-700">Eliminación</span>}
          {noteTypeOf(e.typeCode) === 'REEMPLAZO' && <span className="px-2 py-0.5 rounded text-xs font-bold bg-amber-100 text-amber-700">Reemplazo</span>}
          {e.customer?.name || '-'}
        </div>
      )
    },
    { key: 'period', header: 'Período', cellClassName: 'text-slate-500 text-sm', render: periodOf },
    { key: 'net', header: 'Neto', align: 'right', cellClassName: 'font-medium', render: e => `$${fmt.number(e.totalAmount, 3)}` },
    { key: 'status', header: 'Estado', render: e => entryStatusBadge(e) },
    { key: 'date', header: 'Fecha', hideBelow: 'ultra', cellClassName: 'text-slate-500 text-sm', render: e => new Date(e.issueDate).toLocaleDateString('es-CO') }
  ];

  return (
    <div className="p-4 sm:p-6 lg:p-8">
      <div className="mb-6 flex flex-col gap-4 sm:mb-8 sm:flex-row sm:items-center sm:justify-between">
        <div className="min-w-0">
          <h1 className="text-2xl sm:text-3xl font-extrabold text-slate-800">Nómina electrónica</h1>
          <p className="text-slate-500 mt-1">Historial de nómina emitida</p>
        </div>
        <div className="flex flex-wrap gap-3">
          <ImportExcelButton endpoint="/client/payroll/import" templateEndpoint="/client/payroll/template" label="Importar Excel" onDone={loadData} />
          <button onClick={() => { setEditingId(null); setFormData(initialForm); setView('create'); }} className="bg-primary hover:bg-primary/90 text-white px-5 py-2.5 rounded-xl font-bold flex items-center gap-2 shadow-sm transition-all">
            <Plus size={20} /> Nueva Nómina
          </button>
        </div>
      </div>

      {/* En móvil los filtros van de a dos por fila (menos alto antes del listado); desde sm vuelven a una fila con salto. Igual que en facturas. */}
      <div className="grid grid-cols-2 gap-3 mb-4 sm:flex sm:flex-wrap sm:items-end">
        <div className="w-full sm:w-52">
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
            <div className="min-w-0 flex-1 sm:flex-none">
              <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Desde</label>
              <input type="date" value={customFrom} onChange={e => setCustomFrom(e.target.value)} className="w-full p-2.5 bg-white border border-slate-200 rounded-xl text-sm outline-none focus:ring-2 focus:ring-primary" />
            </div>
            <div className="min-w-0 flex-1 sm:flex-none">
              <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Hasta</label>
              <input type="date" value={customTo} onChange={e => setCustomTo(e.target.value)} className="w-full p-2.5 bg-white border border-slate-200 rounded-xl text-sm outline-none focus:ring-2 focus:ring-primary" />
            </div>
            <button
              type="button"
              onClick={() => {
                if (!customFrom || !customTo) {
                  toast.error('Selecciona las fechas Desde y Hasta');
                  return;
                }
                if (customFrom > customTo) {
                  toast.error('La fecha Desde no puede ser posterior a Hasta');
                  return;
                }
                setAppliedCustomFrom(customFrom);
                setAppliedCustomTo(customTo);
              }}
              className="col-span-2 w-full self-end px-4 py-3 sm:col-auto sm:w-auto sm:py-2.5 bg-primary hover:bg-primary/90 text-white rounded-xl text-sm font-bold transition-colors"
            >
              Filtrar
            </button>
          </>
        )}
        <div className="w-full sm:w-48">
          <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Estado</label>
          <SearchableSelect
            value={statusFilter}
            onChange={setStatusFilter}
            placeholder="Todos..."
            options={STATUS_FILTER_OPTIONS}
          />
        </div>
        <div className="order-4 col-span-2 min-w-0 flex-1 sm:order-none sm:col-auto sm:basis-auto sm:min-w-[220px]">
          <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Buscar</label>
          <div className="relative">
            <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
            <input type="text" value={searchTerm} onChange={e => setSearchTerm(e.target.value)} placeholder="Consecutivo, empleado o identificación..." className="w-full pl-9 pr-3 py-2.5 bg-white border border-slate-200 rounded-xl text-sm outline-none focus:ring-2 focus:ring-primary" />
          </div>
        </div>
        <button onClick={handleExport} disabled={filteredEntries.length === 0} title="Exportar CSV" aria-label="Exportar CSV" className="order-3 flex h-11 w-11 items-center justify-center self-end bg-slate-100 sm:order-none hover:bg-slate-200 text-slate-600 rounded-xl transition-colors disabled:opacity-50">
          <Download size={16} />
        </button>
      </div>

      <BatchActionBar
        count={selectedRows.length}
        canPublish={notEditableCount === 0}
        canDelete={notEditableCount === 0}
        blockedReason={notEditableCount > 0 ? `${notEditableCount} de los seleccionados ya fueron emitidos o están en proceso: solo se pueden emitir o eliminar borradores y rechazados.` : undefined}
        disabled={batchRunning}
        onPublish={() => runBatch('publish')}
        onDelete={() => runBatch('delete')}
        onClear={batchSelection.clear}
      />
      <BatchProgressDialog progress={batch.progress} labelOf={batchLabelOf} noun={{ singular: 'nómina', plural: 'nóminas' }} onClose={batch.close} />

      <ResponsiveList
        rows={paginatedEntries}
        selection={batchSelectionProps}
        columns={columns}
        rowKey={e => e.id}
        tableFrom="2xl"
        emptyMessage="No hay nóminas que coincidan con el filtro."
        actions={e => (
          <>
            {(e.status === 'DRAFT' || e.status === 'REJECTED') && (
              <>
                <RowIconButton action="edit" label={`Editar nómina ${e.consecutiveNumber || ''}`.trim()} onClick={() => handleEditDraft(e)} />
                <button onClick={() => handlePublish(e.id)} disabled={publishingId === e.id} title="Emitir a la DIAN" className="min-h-11 px-3 text-white bg-primary hover:bg-primary/90 rounded-lg shadow-sm transition-all flex items-center gap-1 text-sm font-bold disabled:opacity-50 md:min-h-9">
                  <Send size={16} /> Emitir
                </button>
                <RowIconButton action="delete" label={`Eliminar nómina ${e.consecutiveNumber || ''}`.trim()} onClick={() => handleDeleteDraft(e.id)} />
              </>
            )}
            {hasDianResponse(e) && (
              <button
                onClick={() => setResponseEntry(e)}
                title={e.status === 'REJECTED' ? 'Ver motivo del rechazo' : 'Ver respuesta de la DIAN'}
                aria-label={e.status === 'REJECTED' ? 'Ver motivo del rechazo' : 'Ver respuesta de la DIAN'}
                className={`flex h-11 w-11 items-center justify-center rounded-lg transition-colors md:h-9 md:w-9 ${e.status === 'REJECTED' ? 'text-rose-600 hover:bg-rose-50' : 'text-slate-400 hover:text-primary hover:bg-slate-100'}`}
              >
                {e.status === 'REJECTED' ? <AlertCircle size={16} /> : <Info size={16} />}
              </button>
            )}
            <RowIconButton action="view" label={`Ver detalle de nómina ${e.consecutiveNumber || ''}`.trim()} onClick={() => handleViewDetail(e)} />
            <button onClick={() => handlePreviewPdf(e.id)} disabled={previewingId === e.id} title="Vista previa de impresión" aria-label="Vista previa de impresión" className="flex h-11 w-11 items-center justify-center rounded-lg text-slate-400 transition-colors hover:bg-slate-100 hover:text-primary disabled:opacity-50 md:h-9 md:w-9">
              {previewingId === e.id ? <Loader2 size={16} className="animate-spin" /> : <Printer size={16} />}
            </button>
          </>
        )}
      />

      {filteredEntries.length > 0 && (
        <div className="mt-3 rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-slate-700">
          <p className="mb-2 font-bold">Total del período</p>
          <dl className="grid grid-cols-2 gap-x-4 gap-y-2 text-sm sm:grid-cols-3">
            <div><dt className="text-xs uppercase text-slate-400">Devengado</dt><dd className="font-mono font-bold text-slate-500">${fmt.number(filteredAccruals, 3)}</dd></div>
            <div><dt className="text-xs uppercase text-slate-400">Deducciones</dt><dd className="font-mono font-bold text-rose-600">-${fmt.number(filteredDeductions, 3)}</dd></div>
            <div><dt className="text-xs uppercase text-slate-400">Neto</dt><dd className="font-mono font-bold">${fmt.number(filteredTotal, 3)}</dd></div>
          </dl>
        </div>
      )}

      <div className="mt-4 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <p className="text-sm text-slate-400">{filteredEntries.length} comprobante{filteredEntries.length === 1 ? '' : 's'} en el periodo</p>
        {totalPages > 1 && (
          <div className="flex items-center justify-between gap-3 sm:justify-end">
            <p className="text-sm text-slate-500">Página {currentPage} de {totalPages}</p>
            <div className="flex gap-2">
              <button onClick={() => setPage(p => Math.max(1, p - 1))} disabled={currentPage === 1} aria-label="Página anterior" className="flex h-11 w-11 items-center justify-center bg-white border border-slate-200 rounded-lg text-slate-500 hover:bg-slate-50 disabled:opacity-40 transition-colors lg:h-9 lg:w-9">
                <ChevronLeft size={18} />
              </button>
              <button onClick={() => setPage(p => Math.min(totalPages, p + 1))} disabled={currentPage === totalPages} aria-label="Página siguiente" className="flex h-11 w-11 items-center justify-center bg-white border border-slate-200 rounded-lg text-slate-500 hover:bg-slate-50 disabled:opacity-40 transition-colors lg:h-9 lg:w-9">
                <ChevronRight size={18} />
              </button>
            </div>
          </div>
        )}
      </div>

      {responseDialog}
    </div>
  );
}
