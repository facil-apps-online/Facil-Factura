import React, { useState, useEffect } from 'react';
import { Plus, Trash2, Loader2, ArrowLeft, UserPlus, X, Edit2, Send, Percent, Eye, RotateCcw, Search, Download, ChevronLeft, ChevronRight } from 'lucide-react';
import { toast } from 'sonner';
import { api, getErrorMessage } from '../lib/api';
import { useConfirm } from '@shared/components/ConfirmDialog';
import SearchableSelect from '@shared/components/SearchableSelect';
import { DATE_RANGE_PRESET_OPTIONS, getDateRangeForPreset, type DateRangePreset } from '../lib/dateRangePresets';
import { exportToCsv } from '../lib/exportCsv';

const PAGE_SIZE = 25;

const IVA_TREATMENTS = [
  { value: 'Gravado', label: 'Gravado' },
  { value: 'Exento', label: 'Exento' },
  { value: 'Excluido', label: 'Excluido' }
];

const initialQuickProvider = {
  personType: 'Natural' as 'Natural' | 'Juridica',
  name: '', firstName: '', secondName: '', firstLastName: '', secondLastName: '',
  identificationType: '13', identificationNumber: '', verificationDigit: '',
  partyType: 'Proveedor', email: '', phone: '', address: '', cityCode: '',
  dataicoTaxLevelCode: 'COMUN', dataicoRegimen: 'ORDINARIO'
};

interface Retention {
  catalogId?: string;
  taxCategory: string;
  rate: number;
}

interface Item {
  code: string;
  name: string;
  quantity: number;
  unitPrice: number;
  ivaTreatment: string;
  taxRate: number;
  discountRate: number;
  retentions: Retention[];
}

const lineBaseOf = (item: Item) => item.quantity * item.unitPrice * (1 - (item.discountRate || 0) / 100);
const itemRetentionAmount = (item: Item) => (item.retentions || []).reduce((sum, r) => sum + lineBaseOf(item) * r.rate / 100, 0);

// Desglose del totalizador de retenciones por cada categoría+tarifa (las por ítem se agrupan
// entre todos los ítems que la usan, y se suman con la general si coincide categoría+tarifa).
const discriminatedRetentions = (
  items: Item[],
  generalRetentions: { taxCategory: string, rate: number }[],
  subtotal: number,
  taxTotal: number
) => {
  const map = new Map<string, { label: string, amount: number }>();
  items.forEach(item => {
    (item.retentions || []).forEach(r => {
      const key = `${r.taxCategory}|${r.rate}`;
      const prev = map.get(key)?.amount || 0;
      map.set(key, { label: `${r.taxCategory} ${r.rate}%`, amount: prev + lineBaseOf(item) * r.rate / 100 });
    });
  });
  generalRetentions.forEach(r => {
    const key = `${r.taxCategory}|${r.rate}`;
    const prev = map.get(key)?.amount || 0;
    const amount = r.taxCategory === 'RET_IVA' ? taxTotal * r.rate / 100 : subtotal * r.rate / 100;
    map.set(key, { label: `${r.taxCategory} ${r.rate}%`, amount: prev + amount });
  });
  return Array.from(map.values());
};

export default function SupportDocumentsPage() {
  const confirm = useConfirm();
  const [documents, setDocuments] = useState<any[]>([]);
  const [datePreset, setDatePreset] = useState<DateRangePreset>('this-month');
  const [customFrom, setCustomFrom] = useState(() => getDateRangeForPreset('this-month')!.from);
  const [customTo, setCustomTo] = useState(() => getDateRangeForPreset('this-month')!.to);
  const [providers, setProviders] = useState<any[]>([]);
  const [resolutions, setResolutions] = useState<any[]>([]);
  const [searchTerm, setSearchTerm] = useState('');
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [view, setView] = useState<'list' | 'create' | 'detail'>('list');
  const [submitting, setSubmitting] = useState(false);
  const [showProviderModal, setShowProviderModal] = useState(false);
  const [quickProvider, setQuickProvider] = useState(initialQuickProvider);
  const [savingProvider, setSavingProvider] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [publishingId, setPublishingId] = useState<string | null>(null);
  const [expandedRetentions, setExpandedRetentions] = useState<Record<number, boolean>>({});
  const [paymentTermCustom, setPaymentTermCustom] = useState(false);
  // Una retención (por ítem o general) queda "bloqueada" (solo texto) apenas se elige su valor,
  // para que el scroll del mouse u otra interacción accidental sobre el select no la cambie sin
  // querer; el lápiz la vuelve a abrir para editarla. Solo una fila editable a la vez.
  const [editingGeneralRetentionIdx, setEditingGeneralRetentionIdx] = useState<number | null>(null);
  const [editingItemRetention, setEditingItemRetention] = useState<{ itemIndex: number, retIndex: number } | null>(null);
  const [retentionCatalog, setRetentionCatalog] = useState<{ id: string, category: string, name: string, rate: number }[]>([]);
  const [ivaRateCatalog, setIvaRateCatalog] = useState<{ id: string, category: string, name: string }[]>([]);
  const [paymentTermCatalog, setPaymentTermCatalog] = useState<{ id: string, category: string, name: string }[]>([]);
  const [paymentMeansCatalog, setPaymentMeansCatalog] = useState<{ id: string, category: string, name: string }[]>([]);
  const [taxLevelCatalog, setTaxLevelCatalog] = useState<{ id: string, category: string, name: string }[]>([]);
  const [regimenCatalog, setRegimenCatalog] = useState<{ id: string, category: string, name: string }[]>([]);
  const [identificationTypeOptions, setIdentificationTypeOptions] = useState<{ value: string, label: string }[]>([]);

  const initialForm = {
    customerId: '',
    resolutionId: '',
    paymentMeans: '',
    paymentMeansType: '',
    paymentTermDays: '' as number | '',
    purchaseOrderReference: '',
    generalDiscountReason: '',
    generalDiscountAmount: 0,
    generalRetentions: [] as { catalogId: string, taxCategory: string, rate: number }[],
    items: [{ code: '', name: '', quantity: 1, unitPrice: 0, ivaTreatment: 'Gravado', taxRate: 0, discountRate: 0, retentions: [] }] as Item[],
    referenceDocumentId: null as string | null,
    referenceConcept: ''
  };
  const [formData, setFormData] = useState(initialForm);
  const [viewingDoc, setViewingDoc] = useState<any>(null);
  const [viewingRelated, setViewingRelated] = useState<any[]>([]);
  const [viewingOriginal, setViewingOriginal] = useState<any>(null);

  useEffect(() => {
    loadData();
  }, [datePreset, customFrom, customTo]);

  useEffect(() => {
    setPage(1);
  }, [searchTerm, datePreset, customFrom, customTo]);

  useEffect(() => {
    api.get('/client/resolutions')
      .then(res => setResolutions(res.data.filter((r: any) => r.documentType === 'POS')))
      .catch(() => {});
    api.get('/client/tax-catalog?kind=Retention')
      .then(res => setRetentionCatalog(res.data))
      .catch(() => {});
    api.get('/client/tax-catalog?kind=IvaRate')
      .then(res => setIvaRateCatalog(res.data))
      .catch(() => {});
    api.get('/client/tax-catalog?kind=PaymentTerm')
      .then(res => setPaymentTermCatalog(res.data))
      .catch(() => {});
    api.get('/client/tax-catalog?kind=PaymentMeans')
      .then(res => setPaymentMeansCatalog(res.data))
      .catch(() => {});
    api.get('/client/tax-catalog?kind=TaxLevelCode')
      .then(res => setTaxLevelCatalog(res.data))
      .catch(() => {});
    api.get('/client/tax-catalog?kind=Regimen')
      .then(res => setRegimenCatalog(res.data))
      .catch(() => {});
    api.get('/client/identification-types')
      .then(res => setIdentificationTypeOptions(res.data.map((t: any) => ({ value: t.code, label: t.name }))))
      .catch(() => {});
  }, []);

  const loadData = async () => {
    setLoading(true);
    try {
      const range = datePreset === 'custom' ? { from: customFrom, to: customTo } : getDateRangeForPreset(datePreset)!;
      const [docsRes, provRes] = await Promise.all([
        api.get(`/client/support-documents?from=${range.from}&to=${range.to}`),
        api.get('/client/customers?partyType=Proveedor')
      ]);
      setDocuments(docsRes.data);
      setProviders(provRes.data);
    } catch {
      toast.error('Error al cargar documentos soporte');
    } finally {
      setLoading(false);
    }
  };

  const addItem = () => setFormData({ ...formData, items: [...formData.items, { code: '', name: '', quantity: 1, unitPrice: 0, ivaTreatment: 'Gravado', taxRate: 0, discountRate: 0, retentions: [] }] });
  const removeItem = (idx: number) => setFormData({ ...formData, items: formData.items.filter((_, i) => i !== idx) });
  const updateItem = (idx: number, field: keyof Item, value: any) => {
    setFormData({
      ...formData,
      items: formData.items.map((it, i) => {
        if (i !== idx) return it;
        const updated = { ...it, [field]: value };
        if (field === 'ivaTreatment' && value !== 'Gravado') updated.taxRate = 0;
        return updated;
      })
    });
  };

  const toggleItemRetentions = (idx: number) => setExpandedRetentions(prev => ({ ...prev, [idx]: !prev[idx] }));
  // No se puede repetir la misma retención (mismo catálogo) dos veces en el mismo detalle
  // (ítem o retenciones generales) — no tendría sentido aplicar dos veces la misma tarifa.
  const availableRetentionOptions = (usedIds: (string | undefined)[], currentId?: string) =>
    retentionCatalog.filter(c => c.id === currentId || !usedIds.includes(c.id));

  const addItemRetention = (itemIndex: number) => {
    const used = formData.items[itemIndex].retentions.map(r => r.catalogId);
    const first = availableRetentionOptions(used)[0];
    if (!first) return;
    const items = [...formData.items];
    const retentions = [...items[itemIndex].retentions, { catalogId: first.id, taxCategory: first.category, rate: first.rate }];
    items[itemIndex] = { ...items[itemIndex], retentions };
    setFormData({ ...formData, items });
    setEditingItemRetention({ itemIndex, retIndex: retentions.length - 1 });
  };
  const removeItemRetention = (itemIndex: number, retIndex: number) => {
    const items = [...formData.items];
    items[itemIndex] = { ...items[itemIndex], retentions: items[itemIndex].retentions.filter((_, i) => i !== retIndex) };
    setFormData({ ...formData, items });
    setEditingItemRetention(null);
  };
  // Selecciona una combinación categoría+tarifa completa del catálogo (nunca se escribe el % a mano).
  // Al elegir, la fila se bloquea (vuelve a mostrarse como texto) para que no se pueda cambiar sin
  // querer con el scroll del mouse u otra interacción accidental; el lápiz la reabre para editar.
  const updateItemRetention = (itemIndex: number, retIndex: number, catalogId: string) => {
    const entry = retentionCatalog.find(c => c.id === catalogId);
    if (!entry) return;
    const items = [...formData.items];
    items[itemIndex] = {
      ...items[itemIndex],
      retentions: items[itemIndex].retentions.map((r, i) => i === retIndex ? { ...r, catalogId: entry.id, taxCategory: entry.category, rate: entry.rate } : r)
    };
    setFormData({ ...formData, items });
    setEditingItemRetention(null);
  };

  const addGeneralRetention = () => {
    const used = formData.generalRetentions.map(r => r.catalogId);
    const first = availableRetentionOptions(used)[0];
    if (!first) return;
    setFormData({ ...formData, generalRetentions: [...formData.generalRetentions, { catalogId: first.id, taxCategory: first.category, rate: first.rate }] });
    setEditingGeneralRetentionIdx(formData.generalRetentions.length);
  };
  const removeGeneralRetention = (idx: number) => {
    setFormData({ ...formData, generalRetentions: formData.generalRetentions.filter((_, i) => i !== idx) });
    setEditingGeneralRetentionIdx(null);
  };
  // Mismo bloqueo que las retenciones por ítem: elegir un valor cierra la edición de la fila.
  const updateGeneralRetention = (idx: number, catalogId: string) => {
    const entry = retentionCatalog.find(c => c.id === catalogId);
    if (!entry) return;
    setFormData({
      ...formData,
      generalRetentions: formData.generalRetentions.map((r, i) => i === idx ? { catalogId: entry.id, taxCategory: entry.category, rate: entry.rate } : r)
    });
    setEditingGeneralRetentionIdx(null);
  };
  // RET_IVA se calcula sobre el IVA generado; cualquier otra categoría sobre el subtotal.
  const generalRetentionAmount = (r: { taxCategory: string, rate: number }, subtotal: number, taxTotal: number) =>
    r.taxCategory === 'RET_IVA' ? taxTotal * r.rate / 100 : subtotal * r.rate / 100;

  const handleSaveQuickProvider = async (e: React.FormEvent) => {
    e.preventDefault();
    setSavingProvider(true);
    try {
      const res = await api.post('/client/customers', quickProvider);
      toast.success('Proveedor creado');
      setProviders(prev => [...prev, res.data]);
      setFormData(f => ({ ...f, customerId: res.data.id }));
      setShowProviderModal(false);
      setQuickProvider(initialQuickProvider);
    } catch (err: any) {
      toast.error(getErrorMessage(err, 'Error creando el proveedor'));
    } finally {
      setSavingProvider(false);
    }
  };

  const handleSaveDraft = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!formData.customerId) { toast.error('Selecciona el proveedor.'); return; }
    if (!formData.resolutionId) { toast.error('Debes seleccionar la resolución de documento soporte a usar.'); return; }
    setSubmitting(true);
    try {
      const payload = {
        ...formData,
        paymentTermDays: formData.paymentTermDays === '' ? null : formData.paymentTermDays,
        generalRetentions: formData.generalRetentions.map(r => ({ taxCategory: r.taxCategory, rate: r.rate })),
        items: formData.items.map(it => ({
          ...it,
          retentions: it.retentions.map(r => ({
            taxCategory: r.taxCategory,
            rate: r.rate,
            baseAmount: lineBaseOf(it),
            amount: lineBaseOf(it) * r.rate / 100
          }))
        }))
      };
      if (editingId) {
        await api.put(`/client/support-documents/${editingId}/draft`, payload);
        toast.success('Borrador actualizado');
      } else {
        await api.post('/client/support-documents/draft', payload);
        toast.success('Borrador guardado');
      }
      setEditingId(null);
      setFormData(initialForm);
      setView('list');
      loadData();
    } catch (err: any) {
      toast.error(getErrorMessage(err, 'Error al guardar el documento soporte'));
    } finally {
      setSubmitting(false);
    }
  };

  const handleEditDraft = async (doc: any) => {
    try {
      const res = await api.get(`/client/support-documents/${doc.id}`);
      const full = res.data;
      setEditingId(full.id);
      setPaymentTermCustom(full.paymentTermDays != null && !paymentTermCatalog.some(c => c.category === String(full.paymentTermDays)));
      setFormData({
        customerId: full.customerId || '',
        resolutionId: full.resolutionId || '',
        paymentMeans: full.paymentMeans || '',
        paymentMeansType: full.paymentMeansType || '',
        paymentTermDays: full.paymentTermDays ?? '',
        purchaseOrderReference: full.purchaseOrderReference || '',
        generalDiscountReason: full.generalDiscountReason || '',
        generalDiscountAmount: full.generalDiscountAmount || 0,
        generalRetentions: (full.generalRetentions || []).map((r: any) => ({
          catalogId: retentionCatalog.find(c => c.category === r.taxCategory && c.rate === r.rate)?.id || '',
          taxCategory: r.taxCategory,
          rate: r.rate
        })),
        items: full.items.map((i: any) => ({
          code: i.code,
          name: i.name,
          quantity: i.quantity,
          unitPrice: i.unitPrice,
          ivaTreatment: i.ivaTreatment || 'Gravado',
          taxRate: i.taxRate,
          discountRate: i.discountRate || 0,
          retentions: (i.retentions || []).map((r: any) => ({
            catalogId: retentionCatalog.find(c => c.category === r.taxCategory && c.rate === r.rate)?.id || '',
            taxCategory: r.taxCategory,
            rate: r.rate
          }))
        })),
        referenceDocumentId: full.referenceDocumentId || null,
        referenceConcept: full.referenceConcept || ''
      });
      setView('create');
    } catch {
      toast.error('Error al cargar el documento soporte');
    }
  };

  const handleViewDetail = async (doc: any) => {
    try {
      const [fullRes, relatedRes] = await Promise.all([
        api.get(`/client/support-documents/${doc.id}`),
        api.get(`/client/support-documents/${doc.id}/related`)
      ]);
      setViewingDoc(fullRes.data);
      setViewingRelated(relatedRes.data);
      setViewingOriginal(null);
      if (fullRes.data.referenceDocumentId) {
        api.get(`/client/support-documents/${fullRes.data.referenceDocumentId}`)
          .then(res => setViewingOriginal(res.data))
          .catch(() => {});
      }
      setView('detail');
    } catch {
      toast.error('Error al cargar el detalle del documento');
    }
  };

  const handleCreateAdjustment = async (original: any) => {
    try {
      const res = await api.get(`/client/support-documents/${original.id}`);
      const full = res.data;
      setEditingId(null);
      setPaymentTermCustom(false);
      setFormData({
        ...initialForm,
        customerId: full.customerId,
        referenceDocumentId: full.id,
        referenceConcept: 'Ajuste de precio',
        items: full.items.map((i: any) => ({
          code: i.code,
          name: i.name,
          quantity: i.quantity,
          unitPrice: i.unitPrice,
          ivaTreatment: i.ivaTreatment || 'Gravado',
          taxRate: i.taxRate,
          discountRate: i.discountRate || 0,
          retentions: []
        }))
      });
      setView('create');
    } catch {
      toast.error('Error al cargar el documento soporte original');
    }
  };

  const handlePublish = async (id: string) => {
    setPublishingId(id);
    try {
      await api.post(`/client/support-documents/${id}/publish`);
      toast.success('Documento soporte emitido correctamente');
    } catch (err: any) {
      // El backend guarda el estado "RECHAZADA" y el motivo aunque la respuesta sea un error
      // (el rechazo del integrador no es una falla nuestra) — hay que refrescar igual.
      toast.error(getErrorMessage(err, 'Error al emitir el documento soporte'));
    } finally {
      loadData();
      if (viewingDoc?.id === id) handleViewDetail({ id });
      setPublishingId(null);
    }
  };

  const handleDeleteDraft = async (id: string) => {
    if (!(await confirm('¿Eliminar este documento?'))) return;
    try {
      await api.delete(`/client/support-documents/${id}`);
      toast.success('Documento eliminado');
      if (viewingDoc?.id === id) { setViewingDoc(null); setView('list'); }
      loadData();
    } catch (err: any) {
      toast.error(getErrorMessage(err, 'Error al eliminar'));
    }
  };

  if (loading) return <div className="flex justify-center p-12"><Loader2 className="animate-spin w-8 h-8 text-primary" /></div>;

  if (view === 'create') {
    const subtotal = formData.items.reduce((sum, it) => sum + lineBaseOf(it), 0);
    const taxTotal = formData.items.reduce((sum, it) => sum + (it.ivaTreatment === 'Gravado' ? lineBaseOf(it) * it.taxRate / 100 : 0), 0);
    const retentionsTotal = formData.items.reduce((sum, it) => sum + itemRetentionAmount(it), 0);
    const generalRetentionsTotal = formData.generalRetentions.reduce((sum, r) => sum + generalRetentionAmount(r, subtotal, taxTotal), 0);
    const totalRetentions = retentionsTotal + generalRetentionsTotal;
    const total = subtotal + taxTotal - totalRetentions - (formData.generalDiscountAmount || 0);
    return (
      <div className="p-8">
        <button onClick={() => { setEditingId(null); setView('list'); }} className="flex items-center gap-2 text-slate-500 hover:text-slate-800 mb-6 font-medium">
          <ArrowLeft size={18} /> Volver
        </button>
        <h1 className="text-3xl font-extrabold text-slate-800 mb-8">{editingId ? 'Editar Documento Soporte' : 'Nuevo Documento Soporte'}</h1>
        <form onSubmit={handleSaveDraft} className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6 space-y-6">
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="md:col-span-1">
              <label className="block text-sm font-bold text-slate-700 mb-1">Proveedor</label>
              <div className="flex gap-2">
                <SearchableSelect
                  required
                  className="flex-1"
                  value={formData.customerId}
                  onChange={v => setFormData({ ...formData, customerId: v })}
                  placeholder="Buscar proveedor..."
                  options={providers.map(p => ({ value: p.id, label: `${p.name} (${p.identificationNumber})` }))}
                />
                <button type="button" onClick={() => setShowProviderModal(true)} title="Crear proveedor rápido" className="p-2 bg-slate-100 hover:bg-slate-200 text-slate-600 rounded-xl transition-colors">
                  <UserPlus size={20} />
                </button>
              </div>
            </div>
            <div>
              <label className="block text-sm font-bold text-slate-700 mb-1">Resolución</label>
              <SearchableSelect
                required
                value={formData.resolutionId}
                onChange={v => setFormData({ ...formData, resolutionId: v })}
                placeholder="Buscar resolución por prefijo o número..."
                options={resolutions.map(r => ({
                  value: r.id,
                  label: `Prefijo ${r.prefix} · Res. ${r.resolutionNumber} (vence ${new Date(r.validTo).toLocaleDateString('es-CO')})`
                }))}
              />
              {resolutions.length === 0 && (
                <p className="text-xs text-amber-600 mt-1">No hay resoluciones de documento soporte activas.</p>
              )}
            </div>
            <div>
              <label className="block text-sm font-bold text-slate-700 mb-1">Medio de Pago</label>
              <SearchableSelect
                required
                value={formData.paymentMeans}
                onChange={v => setFormData({ ...formData, paymentMeans: v })}
                placeholder="Buscar medio de pago..."
                options={[...paymentMeansCatalog].sort((a, b) => a.name.localeCompare(b.name, 'es')).map(c => ({ value: c.category, label: c.name }))}
              />
            </div>
            <div>
              <label className="block text-sm font-bold text-slate-700 mb-1">Forma de Pago</label>
              <SearchableSelect
                required
                value={formData.paymentMeansType}
                onChange={v => {
                  setFormData({
                    ...formData,
                    paymentMeansType: v,
                    paymentTermDays: v === 'CREDITO' ? formData.paymentTermDays : '',
                    paymentMeans: v === 'CREDITO' ? '' : formData.paymentMeans,
                  });
                  if (v !== 'CREDITO') setPaymentTermCustom(false);
                }}
                placeholder="Contado o crédito..."
                options={[{ value: 'DEBITO', label: 'Contado' }, { value: 'CREDITO', label: 'Crédito' }]}
              />
            </div>
            {formData.paymentMeansType === 'CREDITO' && (
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-1">Plazo de Pago</label>
                <SearchableSelect
                  value={paymentTermCustom ? 'OTHER' : (formData.paymentTermDays === '' ? '' : String(formData.paymentTermDays))}
                  onChange={v => {
                    if (v === 'OTHER') {
                      setPaymentTermCustom(true);
                    } else {
                      setPaymentTermCustom(false);
                      setFormData({ ...formData, paymentTermDays: v === '' ? '' : parseInt(v) });
                    }
                  }}
                  placeholder="Buscar plazo..."
                  options={[...paymentTermCatalog.map(c => ({ value: c.category, label: c.name })), { value: 'OTHER', label: 'Otro (personalizado)' }]}
                />
                {paymentTermCustom && (
                  <input
                    type="number" min="0" placeholder="Días"
                    value={formData.paymentTermDays === '' ? '' : formData.paymentTermDays}
                    onChange={e => setFormData({ ...formData, paymentTermDays: e.target.value === '' ? '' : parseInt(e.target.value) })}
                    className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none mt-2"
                  />
                )}
              </div>
            )}
            <div>
              <label className="block text-sm font-bold text-slate-700 mb-1">Orden de Compra</label>
              <input type="text" placeholder="Opcional" value={formData.purchaseOrderReference} onChange={e => setFormData({ ...formData, purchaseOrderReference: e.target.value })} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none" />
            </div>
          </div>

          {formData.referenceDocumentId && (
            <div className="p-6 bg-amber-50 border border-amber-200 rounded-xl">
              <h3 className="text-amber-800 font-bold mb-2">Generando nota de ajuste</h3>
              <p className="text-sm text-amber-700 mb-4">La nota ajustará el documento seleccionado.</p>
              <div>
                <label className="block text-sm font-bold text-amber-800 mb-2">Concepto del Ajuste</label>
                <input type="text" value={formData.referenceConcept} onChange={e => setFormData({ ...formData, referenceConcept: e.target.value })} className="w-full px-4 py-2 bg-white border border-amber-200 rounded-xl focus:ring-2 focus:ring-amber-500 outline-none" />
              </div>
            </div>
          )}

          <div>
            <div className="flex justify-between items-center mb-2">
              <label className="block text-sm font-bold text-slate-700">Ítems</label>
              <button type="button" onClick={addItem} className="text-xs font-bold text-primary hover:underline flex items-center gap-1">
                <Plus size={14} /> Agregar ítem
              </button>
            </div>
            <div className="space-y-2">
              {formData.items.map((item, idx) => (
                <div key={idx}>
                  <div className="grid grid-cols-12 gap-2 items-center">
                    <input placeholder="Código" value={item.code} onChange={e => updateItem(idx, 'code', e.target.value)} className="col-span-2 px-3 py-2 border rounded-lg text-sm outline-none" />
                    <input placeholder="Descripción" required value={item.name} onChange={e => updateItem(idx, 'name', e.target.value)} className="col-span-2 px-3 py-2 border rounded-lg text-sm outline-none" />
                    <input type="number" step="0.01" placeholder="Cant." value={item.quantity} onChange={e => updateItem(idx, 'quantity', parseFloat(e.target.value) || 0)} className="col-span-1 px-3 py-2 border rounded-lg text-sm outline-none font-mono" />
                    <input type="number" step="0.01" placeholder="Valor" value={item.unitPrice} onChange={e => updateItem(idx, 'unitPrice', parseFloat(e.target.value) || 0)} className="col-span-2 px-3 py-2 border rounded-lg text-sm outline-none font-mono" />
                    <input type="number" min="0" max="100" step="0.01" placeholder="Desc. %" value={item.discountRate || 0} onChange={e => updateItem(idx, 'discountRate', parseFloat(e.target.value) || 0)} className="col-span-1 px-3 py-2 border rounded-lg text-sm outline-none font-mono" />
                    <SearchableSelect
                      className="col-span-2"
                      inputClassName="w-full px-2 py-2 border rounded-lg text-sm outline-none bg-white"
                      value={item.ivaTreatment}
                      onChange={v => updateItem(idx, 'ivaTreatment', v)}
                      placeholder="IVA..."
                      options={IVA_TREATMENTS}
                    />
                    {item.ivaTreatment === 'Gravado' ? (
                      <SearchableSelect
                        className="col-span-1"
                        inputClassName="w-full px-2 py-2 border rounded-lg text-sm outline-none bg-white"
                        value={String(item.taxRate)}
                        onChange={v => updateItem(idx, 'taxRate', parseFloat(v) || 0)}
                        placeholder="%..."
                        options={ivaRateCatalog.length === 0
                          ? [{ value: String(item.taxRate), label: `${item.taxRate}%` }]
                          : ivaRateCatalog.map(c => ({ value: c.category, label: `${c.category}%` }))}
                      />
                    ) : <div className="col-span-1" />}
                    <button type="button" onClick={() => toggleItemRetentions(idx)} title="Retenciones de esta línea" className={`col-span-1 p-2 rounded-lg transition-colors justify-self-center ${item.retentions.length > 0 ? 'text-amber-600 bg-amber-50' : 'text-slate-400 hover:bg-slate-100'}`}>
                      <Percent size={16} />
                    </button>
                    <button type="button" onClick={() => removeItem(idx)} className="col-span-12 sm:col-span-12 md:col-span-12 p-2 text-slate-400 hover:text-rose-600 justify-self-end"><Trash2 size={16} /></button>
                  </div>
                  {expandedRetentions[idx] && (
                    <div className="mt-2 p-3 bg-amber-50/50 rounded-lg">
                      <div className="flex justify-between items-center mb-2">
                        <span className="text-xs font-bold text-amber-800 uppercase">Retenciones de esta línea</span>
                        <button type="button" disabled={availableRetentionOptions(item.retentions.map(r => r.catalogId)).length === 0} onClick={() => addItemRetention(idx)} className="text-xs font-bold text-primary hover:underline flex items-center gap-1 disabled:opacity-50 disabled:no-underline">
                          <Plus size={14} /> Agregar retención
                        </button>
                      </div>
                      <div className="space-y-2">
                        {item.retentions.map((r, rIdx) => {
                          const catalogEntry = retentionCatalog.find(c => c.id === r.catalogId);
                          const isEditing = editingItemRetention?.itemIndex === idx && editingItemRetention?.retIndex === rIdx;
                          return (
                            <div key={rIdx} className="flex items-center gap-2">
                              {isEditing ? (
                                <SearchableSelect
                                  className="flex-1"
                                  inputClassName="w-full px-3 py-2 border rounded-lg text-sm outline-none bg-white"
                                  value={r.catalogId || ''}
                                  onChange={v => updateItemRetention(idx, rIdx, v)}
                                  placeholder="Buscar retención..."
                                  options={availableRetentionOptions(item.retentions.map(rr => rr.catalogId), r.catalogId).map(c => ({ value: c.id, label: `${c.name} (${c.category} ${c.rate}%)` }))}
                                />
                              ) : (
                                <div className="flex-1 flex items-center gap-2">
                                  <span className="flex-1 px-3 py-2 text-sm text-slate-700">{catalogEntry ? `${catalogEntry.name} (${catalogEntry.category} ${catalogEntry.rate}%)` : `${r.taxCategory} ${r.rate}%`}</span>
                                  <button type="button" onClick={() => setEditingItemRetention({ itemIndex: idx, retIndex: rIdx })} title="Editar" className="p-2 text-slate-400 hover:text-primary hover:bg-white rounded-lg transition-colors">
                                    <Edit2 size={14} />
                                  </button>
                                </div>
                              )}
                              <span className="w-28 text-right font-mono text-sm text-slate-500">${(lineBaseOf(item) * r.rate / 100).toLocaleString('es-CO', { maximumFractionDigits: 0 })}</span>
                              <button type="button" onClick={() => removeItemRetention(idx, rIdx)} className="p-2 text-slate-400 hover:text-rose-600"><Trash2 size={16} /></button>
                            </div>
                          );
                        })}
                        {item.retentions.length === 0 && <p className="text-xs text-slate-400">Sin retenciones en esta línea.</p>}
                        {retentionCatalog.length === 0 && <p className="text-xs text-amber-600">No hay retenciones configuradas.</p>}
                      </div>
                    </div>
                  )}
                </div>
              ))}
            </div>
          </div>

          <div>
            <div className="flex justify-between items-center mb-2">
              <label className="block text-sm font-bold text-slate-700">Retenciones Generales (opcional)</label>
              <button type="button" disabled={availableRetentionOptions(formData.generalRetentions.map(r => r.catalogId)).length === 0} onClick={addGeneralRetention} className="text-xs font-bold text-primary hover:underline flex items-center gap-1 disabled:opacity-50 disabled:no-underline">
                <Plus size={14} /> Agregar retención
              </button>
            </div>
            <div className="space-y-2 max-w-2xl">
              {formData.generalRetentions.map((r, idx) => {
                const catalogEntry = retentionCatalog.find(c => c.id === r.catalogId);
                const isEditing = editingGeneralRetentionIdx === idx;
                return (
                  <div key={idx} className="flex items-center gap-2">
                    {isEditing ? (
                      <SearchableSelect
                        className="flex-1"
                        value={r.catalogId}
                        onChange={v => updateGeneralRetention(idx, v)}
                        placeholder="Buscar retención..."
                        options={availableRetentionOptions(formData.generalRetentions.map(rr => rr.catalogId), r.catalogId).map(c => ({ value: c.id, label: `${c.name} (${c.category} ${c.rate}%)` }))}
                      />
                    ) : (
                      <div className="flex-1 flex items-center gap-2">
                        <span className="flex-1 px-3 py-2 text-sm text-slate-700">{catalogEntry ? `${catalogEntry.name} (${catalogEntry.category} ${catalogEntry.rate}%)` : `${r.taxCategory} ${r.rate}%`}</span>
                        <button type="button" onClick={() => setEditingGeneralRetentionIdx(idx)} title="Editar" className="p-2 text-slate-400 hover:text-primary hover:bg-slate-100 rounded-lg transition-colors">
                          <Edit2 size={14} />
                        </button>
                      </div>
                    )}
                    <span className="w-32 text-right font-mono text-sm text-slate-500">${generalRetentionAmount(r, subtotal, taxTotal).toLocaleString('es-CO', { maximumFractionDigits: 0 })}</span>
                    <button type="button" onClick={() => removeGeneralRetention(idx)} className="p-2 text-slate-400 hover:text-rose-600"><Trash2 size={16} /></button>
                  </div>
                );
              })}
              {formData.generalRetentions.length === 0 && <p className="text-xs text-slate-400">No hay retenciones agregadas.</p>}
              {retentionCatalog.length === 0 && <p className="text-xs text-amber-600">No hay retenciones configuradas.</p>}
            </div>
          </div>

          <div>
            <label className="block text-sm font-bold text-slate-700 mb-2">Descuento General (opcional)</label>
            <div className="grid grid-cols-2 gap-4 max-w-lg">
              <div>
                <label className="block text-xs font-bold text-slate-500 mb-1">Motivo</label>
                <input type="text" placeholder="Ej. Pronto pago" value={formData.generalDiscountReason} onChange={e => setFormData({ ...formData, generalDiscountReason: e.target.value })} className="w-full px-3 py-2 border rounded-lg text-sm outline-none" />
              </div>
              <div>
                <label className="block text-xs font-bold text-slate-500 mb-1">Valor ($)</label>
                <input type="number" min="0" step="0.01" value={formData.generalDiscountAmount} onChange={e => setFormData({ ...formData, generalDiscountAmount: parseFloat(e.target.value) || 0 })} className="w-full px-3 py-2 border rounded-lg text-sm outline-none font-mono" />
              </div>
            </div>
          </div>

          {discriminatedRetentions(formData.items, formData.generalRetentions, subtotal, taxTotal).map(r => (
            <div key={r.label} className="flex justify-between text-sm text-rose-600">
              <span>{r.label}:</span>
              <span className="font-mono">-${r.amount.toLocaleString('es-CO', { maximumFractionDigits: 0 })}</span>
            </div>
          ))}

          <div className="flex justify-between items-center pt-4 border-t border-slate-100">
            <p className="text-lg font-bold text-slate-800">Total: ${total.toLocaleString('es-CO', { maximumFractionDigits: 0 })}</p>
            <button type="submit" disabled={submitting} className="px-6 py-3 bg-slate-900 hover:bg-black text-white font-bold rounded-xl hover:bg-primary/90 transition-colors shadow-md disabled:opacity-50">
              {submitting ? 'Guardando...' : editingId ? 'Guardar Cambios' : 'Guardar Borrador'}
            </button>
          </div>
        </form>

        {showProviderModal && (
          <div className="fixed inset-0 bg-slate-900/50 backdrop-blur-sm z-50 flex items-center justify-center p-4">
            <div className="bg-white rounded-3xl w-full max-w-lg shadow-2xl overflow-hidden">
              <div className="px-6 py-4 border-b border-slate-100 flex justify-between items-center bg-slate-50/50">
                <h3 className="text-xl font-bold text-slate-800">Nuevo Proveedor</h3>
                <button onClick={() => setShowProviderModal(false)} className="text-slate-400 hover:text-slate-600 p-2"><X size={20} /></button>
              </div>
              <form onSubmit={handleSaveQuickProvider} className="p-6 space-y-4">
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-1">Tipo de Persona</label>
                  <div className="grid grid-cols-2 gap-2">
                    {(['Natural', 'Juridica'] as const).map(pt => (
                      <button
                        key={pt}
                        type="button"
                        onClick={() => setQuickProvider({ ...quickProvider, personType: pt, identificationType: pt === 'Juridica' ? '31' : '13' })}
                        className={`px-3 py-2.5 rounded-xl font-bold text-sm transition-colors ${
                          quickProvider.personType === pt ? 'bg-primary text-white shadow-sm' : 'bg-slate-100 text-slate-500 hover:bg-slate-200'
                        }`}
                      >
                        {pt === 'Natural' ? 'Persona Natural' : 'Persona Jurídica'}
                      </button>
                    ))}
                  </div>
                </div>
                {quickProvider.personType === 'Juridica' ? (
                  <div>
                    <label className="block text-sm font-bold text-slate-700 mb-1">Razón Social</label>
                    <input type="text" required value={quickProvider.name} onChange={e => setQuickProvider({ ...quickProvider, name: e.target.value })} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none" />
                  </div>
                ) : (
                  <div className="grid grid-cols-2 gap-4">
                    {(['firstName', 'secondName', 'firstLastName', 'secondLastName'] as const).map((field, i) => (
                      <div key={field}>
                        <label className="block text-sm font-bold text-slate-700 mb-1">{['Nombre 1', 'Nombre 2', 'Apellido 1', 'Apellido 2'][i]}</label>
                        <input
                          type="text" required={i === 0 || i === 2} value={quickProvider[field]}
                          onChange={e => {
                            const next = { ...quickProvider, [field]: e.target.value };
                            next.name = [next.firstName, next.secondName, next.firstLastName, next.secondLastName].filter(Boolean).join(' ');
                            setQuickProvider(next);
                          }}
                          className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none"
                        />
                      </div>
                    ))}
                  </div>
                )}
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className="block text-sm font-bold text-slate-700 mb-1">Tipo de Identificación</label>
                    <SearchableSelect
                      value={quickProvider.identificationType}
                      onChange={v => setQuickProvider({ ...quickProvider, identificationType: v })}
                      placeholder="Buscar tipo de identificación..."
                      options={identificationTypeOptions}
                    />
                  </div>
                  <div>
                    <label className="block text-sm font-bold text-slate-700 mb-1">Número</label>
                    <input type="text" required value={quickProvider.identificationNumber} onChange={e => setQuickProvider({ ...quickProvider, identificationNumber: e.target.value })} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none" />
                  </div>
                </div>
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className="block text-sm font-bold text-slate-700 mb-1">Régimen Fiscal</label>
                    <SearchableSelect
                      required
                      value={quickProvider.dataicoRegimen}
                      onChange={v => setQuickProvider({ ...quickProvider, dataicoRegimen: v })}
                      placeholder="Buscar régimen..."
                      options={regimenCatalog.map(c => ({ value: c.category, label: c.name }))}
                    />
                  </div>
                  <div>
                    <label className="block text-sm font-bold text-slate-700 mb-1">Nivel Tributario</label>
                    <SearchableSelect
                      required
                      value={quickProvider.dataicoTaxLevelCode}
                      onChange={v => setQuickProvider({ ...quickProvider, dataicoTaxLevelCode: v })}
                      placeholder="Buscar nivel tributario..."
                      options={taxLevelCatalog.map(c => ({ value: c.category, label: c.name }))}
                    />
                  </div>
                </div>
                <div className="flex justify-end gap-3 pt-2">
                  <button type="button" onClick={() => setShowProviderModal(false)} className="px-5 py-2 text-slate-600 font-bold hover:bg-slate-100 rounded-xl transition-colors">Cancelar</button>
                  <button type="submit" disabled={savingProvider} className="px-5 py-2 bg-primary text-white font-bold rounded-xl hover:bg-primary/90 transition-colors shadow-md disabled:opacity-50">
                    {savingProvider ? 'Creando...' : 'Crear Proveedor'}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}
      </div>
    );
  }

  if (view === 'detail' && viewingDoc) {
    const doc = viewingDoc;
    const isAdjustment = doc.typeCode === 'DS-AJUSTE';
    return (
      <div className="p-8">
        <button onClick={() => { setViewingDoc(null); setView('list'); }} className="flex items-center gap-2 text-slate-500 hover:text-slate-800 mb-6 font-medium transition-colors">
          <ArrowLeft size={20} /> Volver a documentos soporte
        </button>

        <div className="bg-white rounded-3xl shadow-xl overflow-hidden border border-slate-100">
          <div className="p-8 border-b border-slate-100 bg-slate-50/50 flex justify-between items-start flex-wrap gap-4">
            <div>
              <div className="flex items-center gap-3 mb-1">
                {isAdjustment && <span className="px-2 py-0.5 rounded text-xs font-bold bg-amber-100 text-amber-700">Ajuste</span>}
                <h2 className="text-2xl font-extrabold text-slate-800">{doc.number || 'Borrador'}</h2>
                <span className={`px-3 py-1 rounded-full text-xs font-bold ${
                  doc.status === 'DRAFT' ? 'bg-slate-100 text-slate-600' :
                  doc.status === 'APPROVED' ? 'bg-emerald-100 text-emerald-700' :
                  'bg-rose-100 text-rose-600'
                }`}>
                  {doc.status}
                </span>
              </div>
              <p className="text-slate-500">{new Date(doc.issueDate).toLocaleDateString('es-CO')} · {doc.customer?.name || '-'}</p>
              {doc.status === 'REJECTED' && doc.dianResponseMessage && (
                <p className="text-sm text-rose-500 mt-2">{doc.dianResponseMessage}</p>
              )}
            </div>
            <div className="flex gap-2 flex-wrap">
              {(doc.status === 'DRAFT' || doc.status === 'REJECTED') && (
                <>
                  <button onClick={() => handleEditDraft(doc)} className="px-4 py-2 text-slate-600 bg-slate-100 hover:bg-slate-200 rounded-xl font-bold text-sm transition-colors flex items-center gap-2">
                    <Edit2 size={16} /> Editar
                  </button>
                  <button onClick={() => handlePublish(doc.id)} disabled={publishingId === doc.id} className="px-4 py-2 text-white bg-primary hover:bg-primary/90 rounded-xl font-bold text-sm shadow-sm transition-all flex items-center gap-2 disabled:opacity-50">
                    <Send size={16} /> Emitir
                  </button>
                  <button onClick={() => handleDeleteDraft(doc.id)} className="px-4 py-2 text-rose-600 bg-rose-50 hover:bg-rose-100 rounded-xl font-bold text-sm transition-colors flex items-center gap-2">
                    <Trash2 size={16} /> Eliminar
                  </button>
                </>
              )}
              {doc.status === 'APPROVED' && !isAdjustment && (
                <button onClick={() => handleCreateAdjustment(doc)} className="px-4 py-2 text-amber-700 bg-amber-100 hover:bg-amber-200 rounded-xl font-bold text-sm shadow-sm transition-all flex items-center gap-2">
                  <RotateCcw size={16} /> Generar Nota de Ajuste
                </button>
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
                      ← Ajuste del documento N° {viewingOriginal.number}
                    </button>
                  )}
                  {viewingRelated.map(r => (
                    <button key={r.id} onClick={() => handleViewDetail(r)} className="text-sm text-primary hover:underline flex items-center gap-2">
                      <span className="px-1.5 py-0.5 rounded text-[10px] font-bold bg-amber-100 text-amber-700">Ajuste</span>
                      {r.number} · ${r.totalAmount?.toLocaleString('es-CO')} · {r.status}
                    </button>
                  ))}
                </div>
              </div>
            )}

            <div className="border border-slate-200 rounded-2xl overflow-hidden">
              <table className="w-full text-left border-collapse">
                <thead>
                  <tr className="bg-slate-50 text-xs uppercase tracking-wider text-slate-500 font-bold border-b border-slate-200">
                    <th className="p-3">Producto</th>
                    <th className="p-3">Cant.</th>
                    <th className="p-3">Precio Und.</th>
                    <th className="p-3">IVA</th>
                    <th className="p-3 text-right">Total</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-200">
                  {(doc.items || []).map((item: any, idx: number) => (
                    <tr key={idx}>
                      <td className="p-3 text-sm text-slate-700">{item.name}</td>
                      <td className="p-3 text-sm">{item.quantity}</td>
                      <td className="p-3 text-sm font-mono">${item.unitPrice.toLocaleString('es-CO')}</td>
                      <td className="p-3 text-sm">{item.ivaTreatment === 'Gravado' ? `${item.taxRate}%` : item.ivaTreatment}</td>
                      <td className="p-3 text-sm font-mono font-bold text-right">${item.totalAmount.toLocaleString('es-CO')}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div className="flex justify-end">
              <p className="text-lg font-bold text-slate-800">Total: ${doc.totalAmount?.toLocaleString('es-CO')}</p>
            </div>
          </div>
        </div>
      </div>
    );
  }

  const filteredDocuments = documents.filter(d => {
    if (!searchTerm.trim()) return true;
    const q = searchTerm.trim().toLowerCase();
    return (d.number || '').toLowerCase().includes(q) ||
      (d.customer?.name || '').toLowerCase().includes(q) ||
      (d.customer?.identificationNumber || '').toLowerCase().includes(q);
  });
  const totalPages = Math.max(1, Math.ceil(filteredDocuments.length / PAGE_SIZE));
  const currentPage = Math.min(page, totalPages);
  const paginatedDocuments = filteredDocuments.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE);
  const filteredTotal = filteredDocuments.reduce((sum, d) => sum + (d.totalAmount || 0), 0);

  const handleExport = () => exportToCsv(
    `documentos_soporte_${customFrom}_a_${customTo}.csv`,
    filteredDocuments.map(d => ({
      numero: d.number || '',
      proveedor: d.customer?.name || '',
      identificacion: d.customer?.identificationNumber || '',
      total: d.totalAmount || 0,
      estado: d.status,
      fecha: new Date(d.issueDate).toLocaleDateString('es-CO')
    })),
    [
      { key: 'numero', label: 'Número' },
      { key: 'proveedor', label: 'Proveedor' },
      { key: 'identificacion', label: 'Identificación' },
      { key: 'total', label: 'Total' },
      { key: 'estado', label: 'Estado' },
      { key: 'fecha', label: 'Fecha' }
    ]
  );

  return (
    <div className="p-8">
      <div className="flex justify-between items-center mb-8">
        <div>
          <h1 className="text-3xl font-extrabold text-slate-800">Documentos Soporte</h1>
          <p className="text-slate-500 mt-1">Compras a proveedores no obligados a facturar electrónicamente</p>
        </div>
        <button onClick={() => { setEditingId(null); setPaymentTermCustom(false); setFormData(initialForm); setView('create'); }} className="bg-primary hover:bg-primary/90 text-white px-5 py-2.5 rounded-xl font-bold flex items-center gap-2 shadow-sm transition-all">
          <Plus size={20} /> Nuevo Documento Soporte
        </button>
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
            <input type="text" value={searchTerm} onChange={e => setSearchTerm(e.target.value)} placeholder="Número, proveedor o identificación..." className="w-full pl-9 pr-3 py-2.5 bg-white border border-slate-200 rounded-xl text-sm outline-none focus:ring-2 focus:ring-primary" />
          </div>
        </div>
        <button onClick={handleExport} disabled={filteredDocuments.length === 0} className="flex items-center gap-2 px-4 py-2.5 bg-slate-100 hover:bg-slate-200 text-slate-600 rounded-xl font-bold text-sm transition-colors disabled:opacity-50">
          <Download size={16} /> Exportar CSV
        </button>
        <p className="text-xs text-slate-400 pb-2.5">{filteredDocuments.length} documento{filteredDocuments.length === 1 ? '' : 's'} en el periodo</p>
      </div>

      <div className="bg-white rounded-2xl shadow-sm border border-slate-200 overflow-hidden">
        <table className="w-full text-left border-collapse">
          <thead>
            <tr className="bg-slate-50 border-b border-slate-200 text-sm font-bold text-slate-500 uppercase tracking-wider">
              <th className="p-4">Número</th>
              <th className="p-4">Proveedor</th>
              <th className="p-4 text-right">Total</th>
              <th className="p-4">Estado</th>
              <th className="p-4">Fecha</th>
              <th className="p-4 text-right">Acciones</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {paginatedDocuments.map(d => (
              <tr key={d.id} className="hover:bg-slate-50/50 transition-colors">
                <td className="p-4 font-mono text-sm">
                  <div className="flex items-center gap-2">
                    {d.typeCode === 'DS-AJUSTE' && <span className="px-2 py-0.5 rounded text-xs font-bold bg-amber-100 text-amber-700">Ajuste</span>}
                    {d.number}
                  </div>
                </td>
                <td className="p-4 font-bold text-slate-800">{d.customer?.name || '-'}</td>
                <td className="p-4 text-right font-medium">${d.totalAmount?.toLocaleString('es-CO')}</td>
                <td className="p-4">
                  <span className={`px-3 py-1 rounded-full text-xs font-bold ${
                    d.status === 'DRAFT' ? 'bg-slate-100 text-slate-600' :
                    d.status === 'APPROVED' ? 'bg-emerald-100 text-emerald-700' :
                    'bg-rose-100 text-rose-600'
                  }`}>
                    {d.status}
                  </span>
                  {d.status === 'REJECTED' && d.dianResponseMessage && (
                    <p className="text-xs text-rose-500 mt-1 max-w-[220px]">{d.dianResponseMessage}</p>
                  )}
                </td>
                <td className="p-4 text-slate-500 text-sm">{new Date(d.issueDate).toLocaleDateString('es-CO')}</td>
                <td className="p-4 flex items-center justify-end gap-2">
                  {(d.status === 'DRAFT' || d.status === 'REJECTED') && (
                    <>
                      <button onClick={() => handleEditDraft(d)} title="Editar" className="p-2 text-slate-400 hover:text-blue-600 hover:bg-blue-50 rounded-lg transition-colors">
                        <Edit2 size={16} />
                      </button>
                      <button onClick={() => handlePublish(d.id)} disabled={publishingId === d.id} title="Emitir a la DIAN" className="p-2 text-white bg-primary hover:bg-primary/90 rounded-lg shadow-sm transition-all flex items-center gap-1 text-sm font-bold disabled:opacity-50">
                        <Send size={16} /> Emitir
                      </button>
                      <button onClick={() => handleDeleteDraft(d.id)} title="Eliminar" className="p-2 text-slate-400 hover:text-rose-600 hover:bg-rose-50 rounded-lg transition-colors">
                        <Trash2 size={16} />
                      </button>
                    </>
                  )}
                  <button onClick={() => handleViewDetail(d)} title="Ver detalle" className="p-2 text-slate-400 hover:text-primary hover:bg-slate-100 rounded-lg transition-colors">
                    <Eye size={16} />
                  </button>
                </td>
              </tr>
            ))}
            {filteredDocuments.length === 0 && (
              <tr><td colSpan={6} className="p-8 text-center text-slate-500">No hay documentos que coincidan con el filtro.</td></tr>
            )}
          </tbody>
          {filteredDocuments.length > 0 && (
            <tfoot>
              <tr className="bg-slate-50 border-t-2 border-slate-200 font-bold text-slate-700">
                <td colSpan={2} className="p-4 text-right">Total del periodo:</td>
                <td className="p-4 text-right font-mono">${filteredTotal.toLocaleString('es-CO')}</td>
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
