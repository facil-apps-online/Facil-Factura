import React, { useState, useEffect } from 'react';
import { Plus, Edit2, Trash2, Send, FileText, Loader2, ArrowLeft, PlusCircle, RotateCcw, X, UserPlus, Percent, Eye, Search, Download, ChevronLeft, ChevronRight, Printer } from 'lucide-react';
import { toast } from 'sonner';
import { api, getErrorMessage } from '../lib/api';
import ImportExcelButton from '../components/ImportExcelButton';
import SearchableSelect from '@shared/components/SearchableSelect';
import { DATE_RANGE_PRESET_OPTIONS, getDateRangeForPreset, type DateRangePreset } from '../lib/dateRangePresets';
import { exportToCsv } from '../lib/exportCsv';

const PAGE_SIZE = 25;

const IVA_TREATMENTS = [
  { value: 'Gravado', label: 'Gravado' },
  { value: 'Exento', label: 'Exento' },
  { value: 'Excluido', label: 'Excluido' }
];

const IDENTIFICATION_TYPES = [
  { value: '13', label: 'Cédula de Ciudadanía' },
  { value: '22', label: 'Cédula de Extranjería' },
  { value: '42', label: 'Documento de Identificación Extranjero' },
  { value: '31', label: 'NIT' },
  { value: '50', label: 'NIT de Otro País' },
  { value: '91', label: 'NUIP' },
  { value: '41', label: 'Pasaporte' },
  { value: '11', label: 'Registro Civil' },
  { value: '21', label: 'Tarjeta de Extranjería' },
  { value: '12', label: 'Tarjeta de Identidad' }
];

const initialQuickCustomer = {
  personType: 'Natural' as 'Natural' | 'Juridica',
  name: '', firstName: '', secondName: '', firstLastName: '', secondLastName: '',
  identificationType: '13', identificationNumber: '', verificationDigit: '',
  partyType: 'Cliente', email: '', phone: '', address: '', cityCode: '',
  dataicoTaxLevelCode: 'COMUN', dataicoRegimen: 'ORDINARIO'
};

// Solo la etiqueta visible cambia a español; el valor (DRAFT/PROCESSING/APPROVED/REJECTED) sigue
// igual porque el resto del código lo usa en comparaciones.
const STATUS_LABELS: Record<string, string> = {
  DRAFT: 'Borrador',
  PROCESSING: 'Procesando',
  APPROVED: 'Emitida',
  REJECTED: 'Rechazada'
};

const initialQuickProduct = {
  code: '', name: '', unitPrice: 0, unitOfMeasure: '94', ivaTreatment: 'Gravado', ivaRate: 19, taxes: [] as any[]
};

export default function InvoicesPage() {
  const [invoices, setInvoices] = useState<any[]>([]);
  const [datePreset, setDatePreset] = useState<DateRangePreset>('this-month');
  const [customFrom, setCustomFrom] = useState(() => getDateRangeForPreset('this-month')!.from);
  const [customTo, setCustomTo] = useState(() => getDateRangeForPreset('this-month')!.to);
  const [customers, setCustomers] = useState<any[]>([]);
  const [products, setProducts] = useState<any[]>([]);
  const [documentTypes, setDocumentTypes] = useState<any[]>([]);
  const [searchTerm, setSearchTerm] = useState('');
  const [page, setPage] = useState(1);
  const [resolutions, setResolutions] = useState<any[]>([]);
  const [retentionCatalog, setRetentionCatalog] = useState<{ id: string, category: string, name: string, rate: number }[]>([]);
  const [ivaRateCatalog, setIvaRateCatalog] = useState<{ id: string, category: string, name: string }[]>([]);
  const [paymentTermCatalog, setPaymentTermCatalog] = useState<{ id: string, category: string, name: string }[]>([]);
  const [paymentMeansCatalog, setPaymentMeansCatalog] = useState<{ id: string, category: string, name: string }[]>([]);
  const [taxLevelCatalog, setTaxLevelCatalog] = useState<{ id: string, category: string, name: string }[]>([]);
  const [regimenCatalog, setRegimenCatalog] = useState<{ id: string, category: string, name: string }[]>([]);
  // Catálogo de conceptos de retención (RetefUENTE/ReteIVA) administrado en Superadmin — se ofrece
  // como opción para elegir manualmente por línea (máximo una por línea), sin ninguna resolución
  // automática.
  const [autoRetentionCatalog, setAutoRetentionCatalog] = useState<any[]>([]);

  const [loading, setLoading] = useState(true);
  const [savingDraft, setSavingDraft] = useState(false);
  const [view, setView] = useState<'list' | 'create' | 'detail'>('list');
  const [viewingInvoice, setViewingInvoice] = useState<any>(null);
  const [viewingRelated, setViewingRelated] = useState<any[]>([]);
  const [viewingOriginal, setViewingOriginal] = useState<any>(null);
  const [publishingId, setPublishingId] = useState<string | null>(null);
  const [publishPayment, setPublishPayment] = useState({ paymentMeans: '', paymentMeansType: '' });
  const [editingId, setEditingId] = useState<string | null>(null);
  const [expandedRetentions, setExpandedRetentions] = useState<Record<number, boolean>>({});
  const [paymentTermCustom, setPaymentTermCustom] = useState(false);
  // Una retención (por ítem o general) queda "bloqueada" (solo texto) apenas se elige su valor,
  // para que el scroll del mouse u otra interacción accidental sobre el select no la cambie sin
  // querer; el lápiz la vuelve a abrir para editarla. Solo una fila editable a la vez.
  const [editingGeneralRetentionIdx, setEditingGeneralRetentionIdx] = useState<number | null>(null);
  // A un producto de factura solo se le puede aplicar una retención — item.retentions sigue
  // siendo un array (mismo modelo que usa el backend) pero la UI garantiza máximo 1 elemento.
  // expandedRetentions[index] alterna entre el botón (retención ya elegida o "Ninguna") y el
  // select para elegir/quitar — nunca se muestran los dos a la vez.

  const todayIso = () => new Date().toISOString().slice(0, 10);

  const initialForm = {
    documentTypeId: '',
    customerId: '',
    resolutionId: '',
    issueDate: todayIso(),
    notes: '',
    items: [] as any[],
    generalDiscountReason: '',
    generalDiscountAmount: 0,
    generalChargeReason: '',
    generalChargeAmount: 0,
    generalRetentions: [] as { catalogId: string, taxCategory: string, rate: number }[],
    paymentMeans: '',
    paymentMeansType: '',
    paymentTermDays: '' as number | '',
    purchaseOrderReference: '',
    subtotal: 0,
    taxAmount: 0,
    totalAmount: 0,
    referenceDocumentId: null as string | null,
    referenceConcept: ''
  };
  const [formData, setFormData] = useState(initialForm);

  const [showCustomerModal, setShowCustomerModal] = useState(false);
  const [quickCustomer, setQuickCustomer] = useState(initialQuickCustomer);
  const [savingCustomer, setSavingCustomer] = useState(false);

  const [productModalForItemIndex, setProductModalForItemIndex] = useState<number | null>(null);
  const [quickProduct, setQuickProduct] = useState(initialQuickProduct);
  const [savingProduct, setSavingProduct] = useState(false);

  useEffect(() => {
    loadData();
  }, [datePreset, customFrom, customTo]);

  useEffect(() => {
    setPage(1);
  }, [searchTerm, datePreset, customFrom, customTo]);

  useEffect(() => {
    api.get('/client/resolutions')
      .then(res => setResolutions(res.data.filter((r: any) => r.documentType !== 'POS')))
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
    api.get('/client/retention-concepts/catalog')
      .then(res => setAutoRetentionCatalog(res.data.concepts))
      .catch(() => {});
  }, []);

  const loadData = async () => {
    setLoading(true);
    try {
      const range = datePreset === 'custom' ? { from: customFrom, to: customTo } : getDateRangeForPreset(datePreset)!;
      const [invRes, custRes, prodRes, docTypesRes] = await Promise.all([
        api.get(`/client/invoices?from=${range.from}&to=${range.to}`),
        api.get('/client/customers?partyType=Cliente'),
        api.get('/client/products'),
        api.get('/client/invoices/document-types')
      ]);
      setInvoices(invRes.data);
      setCustomers(custRes.data);
      setProducts(prodRes.data);
      setDocumentTypes(docTypesRes.data);
    } catch (err) {
      toast.error('Error cargando datos');
    }
    setLoading(false);
  };

  const handleCreateNew = () => {
    setEditingId(null);
    setPaymentTermCustom(false);
    setFormData({
      ...initialForm,
      documentTypeId: documentTypes.find(d => d.code === '01')?.id || ''
    });
    setView('create');
  };

  const [previewingId, setPreviewingId] = useState<string | null>(null);

  const handlePreviewPdf = async (invoice: any) => {
    setPreviewingId(invoice.id);
    try {
      const res = await api.get(`/client/invoices/${invoice.id}/preview`, { responseType: 'blob' });
      const url = URL.createObjectURL(new Blob([res.data], { type: 'application/pdf' }));
      window.open(url, '_blank');
    } catch (err: any) {
      if (err.response?.data instanceof Blob) {
        const text = await err.response.data.text();
        try {
          toast.error(getErrorMessage({ response: { data: JSON.parse(text) } }, 'Error al generar la vista previa'));
        } catch {
          toast.error(text || 'Error al generar la vista previa');
        }
      } else {
        toast.error(getErrorMessage(err, 'Error al generar la vista previa'));
      }
    } finally {
      setPreviewingId(null);
    }
  };

  const handleViewDetail = async (invoice: any) => {
    try {
      const [fullRes, relatedRes] = await Promise.all([
        api.get(`/client/invoices/${invoice.id}`),
        api.get(`/client/invoices/${invoice.id}/related`)
      ]);
      setViewingInvoice(fullRes.data);
      setViewingRelated(relatedRes.data);
      setViewingOriginal(null);
      if (fullRes.data.referenceDocumentId) {
        api.get(`/client/invoices/${fullRes.data.referenceDocumentId}`)
          .then(res => setViewingOriginal(res.data))
          .catch(() => {});
      }
      setView('detail');
    } catch {
      toast.error('Error al cargar el detalle del documento');
    }
  };

  const handleEditDraft = async (invoice: any) => {
    try {
      const res = await api.get(`/client/invoices/${invoice.id}`);
      const full = res.data;
      setEditingId(full.id);
      setPaymentTermCustom(full.paymentTermDays != null && !paymentTermCatalog.some(c => c.category === String(full.paymentTermDays)));
      setFormData({
        documentTypeId: full.documentTypeId || documentTypes.find(d => d.code === '01')?.id || '',
        customerId: full.customerId || '',
        resolutionId: full.resolutionId || '',
        issueDate: full.issueDate ? String(full.issueDate).slice(0, 10) : todayIso(),
        notes: full.notes || '',
        items: full.items.map((i: any) => ({
          productId: i.productId || '',
          code: i.code,
          name: i.name,
          quantity: i.quantity,
          unitPrice: i.unitPrice,
          ivaTreatment: i.ivaTreatment || 'Gravado',
          taxRate: i.taxRate,
          discountRate: i.discountRate || 0,
          taxAmount: i.taxAmount,
          totalAmount: i.totalAmount,
          retentions: (i.retentions || []).map((r: any) => {
            const concept = autoRetentionCatalog.find((c: any) => c.taxCategory === r.taxCategory && c.rate === r.rate);
            return {
              conceptId: concept?.id || '',
              taxCategory: r.taxCategory,
              rate: r.rate,
              baseType: concept?.baseType || 'Subtotal',
              name: concept?.name || `${r.taxCategory} ${r.rate}%`
            };
          })
        })),
        generalDiscountReason: full.generalDiscountReason || '',
        generalDiscountAmount: full.generalDiscountAmount || 0,
        generalChargeReason: full.generalChargeReason || '',
        generalChargeAmount: full.generalChargeAmount || 0,
        generalRetentions: (full.generalRetentions || []).map((r: any) => ({
          catalogId: retentionCatalog.find(c => c.category === r.taxCategory && c.rate === r.rate)?.id || '',
          taxCategory: r.taxCategory,
          rate: r.rate
        })),
        paymentMeans: full.paymentMeans || '',
        paymentMeansType: full.paymentMeansType || '',
        paymentTermDays: full.paymentTermDays ?? '',
        purchaseOrderReference: full.purchaseOrderReference || '',
        subtotal: full.subtotal,
        taxAmount: full.taxAmount,
        totalAmount: full.totalAmount,
        referenceDocumentId: full.referenceDocumentId,
        referenceConcept: full.referenceConcept || ''
      });
      setView('create');
    } catch (err) {
      toast.error('Error al cargar la factura');
    }
  };

  const handleDeleteDraft = async (id: string) => {
    if (!confirm('¿Eliminar este documento?')) return;
    try {
      await api.delete(`/client/invoices/${id}`);
      toast.success('Documento eliminado');
      if (viewingInvoice?.id === id) { setViewingInvoice(null); setView('list'); }
      loadData();
    } catch (err: any) {
      toast.error(getErrorMessage(err, 'Error al eliminar'));
    }
  };

  const handleCreateCreditNote = async (originalInvoice: any) => {
    try {
      // Necesitamos cargar los ítems originales si no vienen en la lista
      const res = await api.get(`/client/invoices/${originalInvoice.id}`);
      const fullInvoice = res.data;

      setEditingId(null);
      setPaymentTermCustom(false);
      setFormData({
        ...initialForm,
        documentTypeId: documentTypes.find(d => d.code === '91')?.id || '',
        customerId: fullInvoice.customerId,
        referenceDocumentId: fullInvoice.id,
        referenceConcept: 'Devolución de mercancía', // Valor por defecto
        items: fullInvoice.items.map((i: any) => ({
          productId: i.productId || '',
          code: i.code,
          name: i.name,
          quantity: i.quantity,
          unitPrice: i.unitPrice,
          ivaTreatment: i.ivaTreatment || 'Gravado',
          taxRate: i.taxRate,
          discountRate: i.discountRate || 0,
          taxAmount: i.taxAmount,
          totalAmount: i.totalAmount,
          retentions: (i.retentions || []).map((r: any) => {
            const concept = autoRetentionCatalog.find((c: any) => c.taxCategory === r.taxCategory && c.rate === r.rate);
            return {
              conceptId: concept?.id || '',
              taxCategory: r.taxCategory,
              rate: r.rate,
              baseType: concept?.baseType || 'Subtotal',
              name: concept?.name || `${r.taxCategory} ${r.rate}%`
            };
          })
        })),
        subtotal: fullInvoice.subtotal,
        taxAmount: fullInvoice.taxAmount,
        totalAmount: fullInvoice.totalAmount
      });
      setView('create');
    } catch (err) {
      toast.error('Error al cargar la factura original');
    }
  };

  const handleCreateDebitNote = async (originalInvoice: any) => {
    try {
      // Necesitamos cargar los ítems originales si no vienen en la lista
      const res = await api.get(`/client/invoices/${originalInvoice.id}`);
      const fullInvoice = res.data;

      setEditingId(null);
      setPaymentTermCustom(false);
      setFormData({
        ...initialForm,
        documentTypeId: documentTypes.find(d => d.code === '92')?.id || '',
        customerId: fullInvoice.customerId,
        referenceDocumentId: fullInvoice.id,
        referenceConcept: 'Intereses u otros cargos', // Valor por defecto
        items: fullInvoice.items.map((i: any) => ({
          productId: i.productId || '',
          code: i.code,
          name: i.name,
          quantity: i.quantity,
          unitPrice: i.unitPrice,
          ivaTreatment: i.ivaTreatment || 'Gravado',
          taxRate: i.taxRate,
          discountRate: i.discountRate || 0,
          taxAmount: i.taxAmount,
          totalAmount: i.totalAmount,
          retentions: (i.retentions || []).map((r: any) => {
            const concept = autoRetentionCatalog.find((c: any) => c.taxCategory === r.taxCategory && c.rate === r.rate);
            return {
              conceptId: concept?.id || '',
              taxCategory: r.taxCategory,
              rate: r.rate,
              baseType: concept?.baseType || 'Subtotal',
              name: concept?.name || `${r.taxCategory} ${r.rate}%`
            };
          })
        })),
        subtotal: fullInvoice.subtotal,
        taxAmount: fullInvoice.taxAmount,
        totalAmount: fullInvoice.totalAmount
      });
      setView('create');
    } catch (err) {
      toast.error('Error al cargar la factura original');
    }
  };

  const addItem = () => {
    setFormData({
      ...formData,
      items: [...formData.items, { productId: '', code: '', name: '', quantity: 1, unitPrice: 0, ivaTreatment: 'Gravado', taxRate: 19, discountRate: 0, taxAmount: 0, totalAmount: 0, retentions: [] }]
    });
  };

  const removeItem = (index: number) => {
    const newItems = [...formData.items];
    newItems.splice(index, 1);
    recalculateTotals(newItems);
  };

  const updateItem = (index: number, field: string, value: any) => {
    const newItems = [...formData.items];
    const item = { ...newItems[index], [field]: value };

    // Si seleccionan un producto del catálogo, autocompletar
    if (field === 'productId' && value !== '') {
      const p = products.find(prod => prod.id === value);
      if (p) {
        item.code = p.code;
        item.name = p.name;
        item.unitPrice = p.unitPrice;
        item.ivaTreatment = p.ivaTreatment || 'Gravado';
        item.taxRate = p.ivaTreatment === 'Gravado' ? (p.ivaRate ?? 0) : 0;
      }
    }

    if (field === 'ivaTreatment' && value !== 'Gravado') {
      item.taxRate = 0;
    }

    recalcItem(item);
    newItems[index] = item;
    recalculateTotals(newItems);
  };

  // Base gravable de la línea = cantidad*valorUnitario menos el descuento de esa línea; el
  // impuesto y el total de la línea se calculan sobre esa base, no sobre el valor bruto.
  const recalcItem = (item: any) => {
    const effectiveRate = item.ivaTreatment === 'Gravado' ? item.taxRate : 0;
    const lineBase = item.quantity * item.unitPrice * (1 - (item.discountRate || 0) / 100);
    item.taxAmount = lineBase * (effectiveRate / 100);
    item.totalAmount = lineBase + item.taxAmount;
  };

  // Vigencia del UVT: el valor más reciente cuyo EffectiveFrom no supere la fecha de factura.
  // Base de la línea sobre la que se calcula cada retención: la mayoría se calculan sobre el
  // subtotal de la línea, pero ReteIVA se calcula sobre el IVA generado de esa misma línea.
  const itemRetentionEntryAmount = (item: any, r: { rate: number, baseType?: string }) => {
    const lineBase = item.quantity * item.unitPrice * (1 - (item.discountRate || 0) / 100);
    const base = r.baseType === 'IvaGenerado' ? item.taxAmount : lineBase;
    return base * r.rate / 100;
  };

  const itemRetentionAmount = (item: any) =>
    (item.retentions || []).reduce((sum: number, r: any) => sum + itemRetentionEntryAmount(item, r), 0);

  const recalculateTotals = (newItems: any[]) => {
    let sub = 0;
    let tax = 0;
    newItems.forEach(i => {
      sub += i.quantity * i.unitPrice * (1 - (i.discountRate || 0) / 100);
      tax += i.taxAmount;
    });
    setFormData({
      ...formData,
      items: newItems,
      subtotal: sub,
      taxAmount: tax,
      totalAmount: sub + tax
    });
  };

  // No se puede repetir la misma retención (mismo catálogo) dos veces en las retenciones generales
  // del documento — no tendría sentido aplicar dos veces la misma tarifa.
  const availableRetentionOptions = (usedIds: string[], currentId?: string) =>
    retentionCatalog.filter(c => c.id === currentId || !usedIds.includes(c.id));

  const toggleItemRetentions = (idx: number) => {
    setExpandedRetentions(prev => ({ ...prev, [idx]: !prev[idx] }));
  };
  // Solo una retención por línea: elegir una reemplaza cualquiera que hubiera. Elegir o quitar
  // siempre cierra el select y vuelve a mostrar el botón (ver expandedRetentions más arriba).
  const setItemRetention = (itemIndex: number, conceptId: string) => {
    const entry = autoRetentionCatalog.find((c: any) => c.id === conceptId);
    if (!entry) return;
    const newItems = [...formData.items];
    newItems[itemIndex] = { ...newItems[itemIndex], retentions: [{ conceptId: entry.id, taxCategory: entry.taxCategory, rate: entry.rate, baseType: entry.baseType, name: entry.name }] };
    setFormData({ ...formData, items: newItems });
    setExpandedRetentions(prev => ({ ...prev, [itemIndex]: false }));
  };
  const clearItemRetention = (itemIndex: number) => {
    const newItems = [...formData.items];
    newItems[itemIndex] = { ...newItems[itemIndex], retentions: [] };
    setFormData({ ...formData, items: newItems });
    setExpandedRetentions(prev => ({ ...prev, [itemIndex]: false }));
  };

  const retentionsTotal = formData.items.reduce((sum, i) => sum + itemRetentionAmount(i), 0);

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
  // RET_IVA se calcula sobre el IVA generado (es una retención sobre el impuesto); cualquier otra
  // categoría (ReteICA, etc.) se calcula sobre el subtotal. El backend prorratea el monto entre
  // los ítems al armar el envío a Dataico; acá solo se muestra el total para que cuadre con lo
  // que se emitirá.
  const generalRetentionAmount = (r: { taxCategory: string, rate: number }) =>
    r.taxCategory === 'RET_IVA' ? formData.taxAmount * r.rate / 100 : formData.subtotal * r.rate / 100;
  const generalRetentionsTotal = formData.generalRetentions.reduce((sum, r) => sum + generalRetentionAmount(r), 0);
  const totalRetentions = retentionsTotal + generalRetentionsTotal;

  // Desglose del totalizador de retenciones por cada categoría+tarifa (las por ítem se agrupan
  // entre todos los ítems que la usan, y se suman con la general si coincide categoría+tarifa).
  const discriminatedRetentions = () => {
    const map = new Map<string, { label: string, amount: number }>();
    formData.items.forEach(item => {
      (item.retentions || []).forEach((r: any) => {
        const key = `${r.taxCategory}|${r.rate}`;
        const prev = map.get(key)?.amount || 0;
        const label = `${r.name || r.taxCategory} ${r.rate}%`;
        map.set(key, { label, amount: prev + itemRetentionEntryAmount(item, r) });
      });
    });
    formData.generalRetentions.forEach(r => {
      const key = `${r.taxCategory}|${r.rate}`;
      const prev = map.get(key)?.amount || 0;
      const label = `${retentionCatalog.find(c => c.id === r.catalogId)?.name || r.taxCategory} ${r.rate}%`;
      map.set(key, { label, amount: prev + generalRetentionAmount(r) });
    });
    return Array.from(map.values());
  };

  // Desglose del IVA por cada tarifa distinta presente en las líneas (ej. 5% y 19% aparte), más
  // Exento/Excluido agrupados aparte. El orden va de menor a mayor tarifa, con Exento/Excluido
  // primero.
  const ivaBreakdown = () => {
    const map = new Map<string, { label: string, base: number, tax: number, isGravado: boolean, order: number }>();
    formData.items.forEach(item => {
      const lineBase = item.quantity * item.unitPrice * (1 - (item.discountRate || 0) / 100);
      const key = item.ivaTreatment === 'Gravado' ? `Gravado-${item.taxRate}` : item.ivaTreatment;
      const existing = map.get(key);
      if (existing) {
        existing.base += lineBase;
        existing.tax += item.taxAmount;
      } else {
        map.set(key, {
          label: item.ivaTreatment === 'Gravado' ? `IVA ${item.taxRate}%` : item.ivaTreatment,
          base: lineBase,
          tax: item.taxAmount,
          isGravado: item.ivaTreatment === 'Gravado',
          order: item.ivaTreatment === 'Gravado' ? 100 + item.taxRate : item.ivaTreatment === 'Exento' ? 1 : 2
        });
      }
    });
    return Array.from(map.values()).sort((a, b) => a.order - b.order);
  };

  const handleSaveQuickCustomer = async (e: React.FormEvent) => {
    e.preventDefault();
    setSavingCustomer(true);
    try {
      const res = await api.post('/client/customers', quickCustomer);
      toast.success('Tercero creado');
      setCustomers(prev => [...prev, res.data]);
      setFormData(f => ({ ...f, customerId: res.data.id }));
      setShowCustomerModal(false);
      setQuickCustomer(initialQuickCustomer);
    } catch (err: any) {
      toast.error(getErrorMessage(err, 'Error creando el tercero'));
    } finally {
      setSavingCustomer(false);
    }
  };

  const handleSaveQuickProduct = async (e: React.FormEvent) => {
    e.preventDefault();
    if (productModalForItemIndex === null) return;
    setSavingProduct(true);
    try {
      const res = await api.post('/client/products', quickProduct);
      const p = res.data;
      toast.success('Producto creado');
      setProducts(prev => [...prev, p]);

      const rate = p.ivaTreatment === 'Gravado' ? (p.ivaRate ?? 0) : 0;
      const newItems = [...formData.items];
      const item = { ...newItems[productModalForItemIndex], productId: p.id, code: p.code, name: p.name, unitPrice: p.unitPrice, ivaTreatment: p.ivaTreatment || 'Gravado', taxRate: rate };
      recalcItem(item);
      newItems[productModalForItemIndex] = item;
      recalculateTotals(newItems);

      setProductModalForItemIndex(null);
      setQuickProduct(initialQuickProduct);
    } catch (err: any) {
      toast.error(getErrorMessage(err, 'Error creando el producto'));
    } finally {
      setSavingProduct(false);
    }
  };

  const handleSaveDraft = async () => {
    if (savingDraft) return; // evita doble envío por doble clic mientras la petición está en curso
    if (!formData.customerId) {
      toast.error('Debes seleccionar un cliente');
      return;
    }
    if (formData.items.length === 0) {
      toast.error('Agrega al menos un ítem');
      return;
    }
    if (!formData.resolutionId) {
      toast.error('Debes seleccionar la resolución de facturación a usar');
      return;
    }

    setSavingDraft(true);
    try {
      const payload = {
        ...formData,
        paymentTermDays: formData.paymentTermDays === '' ? null : formData.paymentTermDays,
        generalRetentions: formData.generalRetentions.map(r => ({ taxCategory: r.taxCategory, rate: r.rate })),
        items: formData.items.map(i => {
          const lineBase = i.quantity * i.unitPrice * (1 - (i.discountRate || 0) / 100);
          return {
            ...i,
            productId: i.productId || null,
            retentions: (i.retentions || []).map((r: any) => {
              const baseAmount = r.baseType === 'IvaGenerado' ? i.taxAmount : lineBase;
              return { taxCategory: r.taxCategory, rate: r.rate, baseAmount, amount: baseAmount * r.rate / 100 };
            })
          };
        })
      };
      if (editingId) {
        await api.put(`/client/invoices/${editingId}/draft`, payload);
        toast.success('Borrador actualizado');
      } else {
        await api.post('/client/invoices/draft', payload);
        toast.success('Borrador guardado');
      }
      setEditingId(null);
      setView('list');
      loadData();
    } catch (err) {
      toast.error('Error guardando factura');
    } finally {
      setSavingDraft(false);
    }
  };

  const confirmPublish = async () => {
    if (!publishingId) return;
    if (!publishPayment.paymentMeans || !publishPayment.paymentMeansType) {
      toast.error('Indica el medio de pago.');
      return;
    }
    try {
      const wasViewingId = viewingInvoice?.id;
      await api.post(`/client/invoices/${publishingId}/publish`, publishPayment);
      toast.success('Factura emitida correctamente');
      setPublishingId(null);
      loadData();
      // Si se publicó desde la vista de detalle, refrescarla en vez de dejarla con el estado viejo.
      if (wasViewingId === publishingId) handleViewDetail({ id: publishingId });
    } catch (err: any) {
      toast.error(getErrorMessage(err, 'Error al publicar'));
    }
  };

  if (loading) return <div className="flex justify-center p-12"><Loader2 className="animate-spin w-8 h-8 text-primary" /></div>;

  if (view === 'create') {
    return (
      <div className="p-8 animate-in fade-in slide-in-from-bottom-4 duration-300">
        <button onClick={() => { setEditingId(null); setView('list'); }} className="flex items-center gap-2 text-slate-500 hover:text-slate-800 mb-6 font-medium transition-colors">
          <ArrowLeft size={20} /> Volver a mis facturas
        </button>

        <div className="bg-white rounded-3xl shadow-xl overflow-hidden border border-slate-100">
          <div className="p-8 border-b border-slate-100 bg-slate-50/50">
            <h2 className="text-2xl font-extrabold text-slate-800">{editingId ? 'Editar Factura' : 'Nueva Factura'}</h2>
            <p className="text-slate-500 mt-1">Ingresa los datos para emitir un nuevo documento</p>
          </div>
          
          <div className="p-8">
            <div className="grid grid-cols-2 gap-6 mb-8">
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-2">Tipo de Documento</label>
                <SearchableSelect
                  value={formData.documentTypeId}
                  onChange={v => setFormData({...formData, documentTypeId: v})}
                  placeholder="Buscar tipo de documento..."
                  options={documentTypes.map(d => ({ value: d.id, label: d.name }))}
                />
              </div>
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-2">Resolución de Facturación</label>
                <SearchableSelect
                  value={formData.resolutionId}
                  onChange={v => setFormData({ ...formData, resolutionId: v })}
                  placeholder="Buscar resolución por prefijo o número..."
                  options={resolutions.map(r => ({
                    value: r.id,
                    label: `${r.documentType} · Prefijo ${r.prefix} · Res. ${r.resolutionNumber} (vence ${new Date(r.validTo).toLocaleDateString('es-CO')})`
                  }))}
                />
                {resolutions.length === 0 && (
                  <p className="text-xs text-amber-600 mt-1">No hay resoluciones activas registradas. Configúralas en Ajustes → Resoluciones.</p>
                )}
              </div>
              <div className="col-span-2">
                <label className="block text-sm font-bold text-slate-700 mb-2">Cliente / Adquirente</label>
                <div className="flex gap-2">
                  <SearchableSelect
                    className="flex-1"
                    value={formData.customerId}
                    onChange={v => setFormData({...formData, customerId: v})}
                    placeholder="Buscar cliente por nombre o identificación..."
                    options={customers.map(c => ({ value: c.id, label: `${c.name} (${c.identificationNumber})` }))}
                  />
                  <button type="button" onClick={() => setShowCustomerModal(true)} title="Crear tercero rápido" className="p-3 bg-slate-100 hover:bg-slate-200 text-slate-600 rounded-xl transition-colors">
                    <UserPlus size={20} />
                  </button>
                </div>
              </div>
            </div>

            <div className="grid grid-cols-2 md:grid-cols-4 gap-6 mb-8">
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-2">Fecha de Factura</label>
                <input
                  type="date"
                  value={formData.issueDate}
                  onChange={e => setFormData({ ...formData, issueDate: e.target.value })}
                  className="w-full p-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-primary outline-none"
                />
              </div>
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-2">Tipo de Pago</label>
                <SearchableSelect
                  value={formData.paymentMeansType}
                  onChange={v => {
                    setFormData({ ...formData, paymentMeansType: v, paymentTermDays: v === 'CREDITO' ? formData.paymentTermDays : '' });
                    if (v !== 'CREDITO') setPaymentTermCustom(false);
                  }}
                  placeholder="Contado o crédito..."
                  options={[{ value: 'DEBITO', label: 'Contado' }, { value: 'CREDITO', label: 'Crédito' }]}
                />
              </div>
              {formData.paymentMeansType === 'CREDITO' && (
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-2">Plazo de Pago</label>
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
                      className="w-full p-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-primary outline-none mt-2"
                    />
                  )}
                  {formData.paymentTermDays !== '' && formData.issueDate && (
                    <p className="text-xs text-slate-500 mt-2">
                      Vence: <span className="font-bold text-slate-700">
                        {new Date(new Date(formData.issueDate + 'T00:00:00').getTime() + Number(formData.paymentTermDays) * 86400000).toLocaleDateString('es-CO')}
                      </span>
                    </p>
                  )}
                </div>
              )}
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-2">Orden de Compra</label>
                <input type="text" placeholder="Opcional" value={formData.purchaseOrderReference} onChange={e => setFormData({ ...formData, purchaseOrderReference: e.target.value })} className="w-full p-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-primary outline-none" />
              </div>
            </div>

            {formData.referenceDocumentId && (() => {
              const isDebitNote = formData.documentTypeId === documentTypes.find(d => d.code === '92')?.id;
              return (
                <div className="mb-8 p-6 bg-amber-50 border border-amber-200 rounded-xl">
                  <h3 className="text-amber-800 font-bold mb-2 flex items-center gap-2">
                    <RotateCcw size={18} />
                    Generando {isDebitNote ? 'Nota Débito' : 'Nota Crédito'}
                  </h3>
                  <p className="text-sm text-amber-700 mb-4">
                    {isDebitNote
                      ? 'Esta nota afectará a la factura seleccionada agregando un cargo adicional.'
                      : 'Esta nota afectará a la factura seleccionada. Puedes emitirla por el valor total (sin tocar nada) o ajustar las líneas/cantidades para una devolución parcial.'}
                  </p>
                  <div>
                    <label className="block text-sm font-bold text-amber-800 mb-2">Concepto de la Nota</label>
                    <SearchableSelect
                      value={formData.referenceConcept}
                      onChange={v => setFormData({...formData, referenceConcept: v})}
                      placeholder="Buscar concepto..."
                      inputClassName="w-full px-4 py-2 pr-8 bg-white border border-amber-200 rounded-xl focus:ring-2 focus:ring-amber-500 outline-none"
                      options={isDebitNote ? [
                        { value: 'Intereses u otros cargos', label: 'Intereses u otros cargos' },
                        { value: 'Cambio en el valor', label: 'Cambio en el valor' },
                        { value: 'Otros', label: 'Otros' }
                      ] : [
                        { value: 'Devolución de mercancía', label: 'Devolución de mercancía' },
                        { value: 'Anulación de factura', label: 'Anulación de factura' },
                        { value: 'Rebaja o descuento parcial o total', label: 'Rebaja o descuento parcial o total' },
                        { value: 'Otros', label: 'Otros' }
                      ]}
                    />
                  </div>
                </div>
              );
            })()}

            <div className="mb-8">
              <div className="flex justify-between items-end mb-4">
                <h3 className="text-lg font-bold text-slate-800">Líneas de Factura</h3>
                <button onClick={addItem} className="text-primary font-bold flex items-center gap-2 hover:text-blue-700 transition-colors">
                  <PlusCircle size={18} /> Agregar Ítem
                </button>
              </div>
              
              <div className="border border-slate-200 rounded-2xl overflow-hidden">
                <table className="w-full text-left border-collapse">
                  <thead>
                    <tr className="bg-slate-50 text-xs uppercase tracking-wider text-slate-500 font-bold border-b border-slate-200">
                      <th className="p-2 pl-3 w-1/3">Producto</th>
                      <th className="p-2">Cant.</th>
                      <th className="p-2">Precio Und.</th>
                      <th className="p-2">Desc. %</th>
                      <th className="p-2">IVA</th>
                      <th className="p-2">Retención</th>
                      <th className="p-2 text-right">Total</th>
                      <th className="p-2"></th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-200">
                    {formData.items.map((item, index) => (
                      <React.Fragment key={index}>
                        <tr className="bg-white">
                          <td className="p-2 pl-3">
                            <div className="flex gap-1">
                              <SearchableSelect
                                className="flex-1"
                                inputClassName="w-full p-2 border border-slate-200 rounded-lg text-sm outline-none"
                                value={item.productId}
                                onChange={v => updateItem(index, 'productId', v)}
                                placeholder="Buscar por código o nombre..."
                                options={products.map(p => ({ value: p.id, label: `${p.code} - ${p.name}`, displayLabel: p.code }))}
                                createOptions={q => [
                                  { label: `Crear "${q}" como código`, onSelect: () => { setProductModalForItemIndex(index); setQuickProduct({ ...initialQuickProduct, code: q }); } },
                                  { label: `Crear "${q}" como nombre`, onSelect: () => { setProductModalForItemIndex(index); setQuickProduct({ ...initialQuickProduct, name: q }); } },
                                ]}
                              />
                              <input
                                type="text"
                                placeholder={item.productId ? 'Descripción de esta línea' : 'Nombre o descripción...'}
                                value={item.name}
                                onChange={e => updateItem(index, 'name', e.target.value)}
                                title={item.productId ? 'Solo afecta esta línea, no modifica el producto del catálogo' : undefined}
                                className="flex-1 p-2 border border-slate-200 rounded-lg text-sm outline-none"
                              />
                            </div>
                          </td>
                          <td className="p-2">
                            <input type="number" min="1" value={item.quantity} onChange={e => updateItem(index, 'quantity', parseFloat(e.target.value) || 0)} className="w-20 p-2 border border-slate-200 rounded-lg text-sm outline-none text-center" />
                          </td>
                          <td className="p-2">
                            <input type="number" value={item.unitPrice} onChange={e => updateItem(index, 'unitPrice', parseFloat(e.target.value) || 0)} className="w-32 p-2 border border-slate-200 rounded-lg text-sm outline-none font-mono" />
                          </td>
                          <td className="p-2">
                            <input type="number" min="0" max="100" step="0.01" value={item.discountRate || 0} onChange={e => updateItem(index, 'discountRate', parseFloat(e.target.value) || 0)} className="w-20 p-2 border border-slate-200 rounded-lg text-sm outline-none font-mono" />
                          </td>
                          <td className="p-2">
                            {item.productId ? (
                              <div className="w-full p-2 text-sm text-slate-600 bg-slate-50 border border-slate-200 rounded-lg" title="El IVA lo define el producto del catálogo">
                                {item.ivaTreatment === 'Gravado' ? `Gravado ${item.taxRate}%` : IVA_TREATMENTS.find(t => t.value === item.ivaTreatment)?.label || item.ivaTreatment}
                              </div>
                            ) : (
                              <div className="flex gap-1">
                                <SearchableSelect
                                  className="w-32"
                                  inputClassName="w-full p-2 border border-slate-200 rounded-lg text-sm outline-none"
                                  value={item.ivaTreatment}
                                  onChange={v => updateItem(index, 'ivaTreatment', v)}
                                  placeholder="IVA..."
                                  options={IVA_TREATMENTS}
                                />
                                {item.ivaTreatment === 'Gravado' && (
                                  <SearchableSelect
                                    className="w-24"
                                    inputClassName="w-full p-2 border border-slate-200 rounded-lg text-sm outline-none bg-white"
                                    value={String(item.taxRate)}
                                    onChange={v => updateItem(index, 'taxRate', parseFloat(v) || 0)}
                                    placeholder="%..."
                                    options={ivaRateCatalog.length === 0
                                      ? [{ value: String(item.taxRate), label: `${item.taxRate}%` }]
                                      : ivaRateCatalog.map(c => ({ value: c.category, label: `${c.category}%` }))}
                                  />
                                )}
                              </div>
                            )}
                          </td>
                          <td className="p-2">
                            {expandedRetentions[index] ? (
                              <div className="flex items-center gap-1">
                                <SearchableSelect
                                  className="flex-1"
                                  inputClassName="w-full p-2 border border-slate-200 rounded-lg text-sm outline-none bg-white"
                                  value={item.retentions?.[0]?.conceptId || ''}
                                  onChange={v => setItemRetention(index, v)}
                                  placeholder="Buscar retención..."
                                  options={autoRetentionCatalog.map((c: any) => ({ value: c.id, label: `${c.name} (${c.taxCategory} ${c.rate}%)`, shortLabel: `${c.rate}%` }))}
                                />
                                <button type="button" onClick={() => clearItemRetention(index)} title="Quitar retención" className="p-2 text-rose-400 hover:text-rose-600 hover:bg-rose-50 rounded-lg transition-colors">
                                  <X size={16} />
                                </button>
                              </div>
                            ) : (
                              <button type="button" onClick={() => toggleItemRetentions(index)} title="Retención de esta línea" className={`w-full flex items-center gap-1 p-2 rounded-lg text-sm transition-colors border ${item.retentions?.[0] ? 'text-amber-700 bg-amber-50 border-amber-200 hover:bg-amber-100' : 'text-slate-400 border-slate-200 hover:bg-slate-100'}`}>
                                <Percent size={14} />
                                <span className="truncate">{item.retentions?.[0] ? `${item.retentions[0].rate}%` : 'Ninguna'}</span>
                              </button>
                            )}
                          </td>
                          <td className="p-2 pr-3 text-right font-mono font-bold text-slate-700">
                            ${item.totalAmount.toLocaleString('es-CO')}
                          </td>
                          <td className="p-1 text-right whitespace-nowrap">
                            <button onClick={() => removeItem(index)} className="p-2 text-rose-400 hover:text-rose-600 hover:bg-rose-50 rounded-lg transition-colors">
                              <Trash2 size={18} />
                            </button>
                          </td>
                        </tr>
                      </React.Fragment>
                    ))}
                    {formData.items.length === 0 && (
                      <tr>
                        <td colSpan={8} className="p-8 text-center text-slate-400">Sin ítems agregados.</td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>
            </div>

            <div className="mb-8">
              <div className="flex justify-between items-end mb-4">
                <h3 className="text-lg font-bold text-slate-800">Retenciones Generales (opcional)</h3>
                <button type="button" disabled={availableRetentionOptions(formData.generalRetentions.map(r => r.catalogId)).length === 0} onClick={addGeneralRetention} className="text-primary font-bold flex items-center gap-2 hover:text-blue-700 transition-colors disabled:opacity-50 disabled:hover:text-primary">
                  <PlusCircle size={18} /> Agregar Retención
                </button>
              </div>

              <div className="border border-slate-200 rounded-2xl overflow-hidden">
                <table className="w-full text-left border-collapse">
                  <thead>
                    <tr className="bg-slate-50 text-xs uppercase tracking-wider text-slate-500 font-bold border-b border-slate-200">
                      <th className="p-4 w-2/3">Retención</th>
                      <th className="p-4 text-right">Monto</th>
                      <th className="p-4"></th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {formData.generalRetentions.map((r, idx) => {
                      const catalogEntry = retentionCatalog.find(c => c.id === r.catalogId);
                      const isEditing = editingGeneralRetentionIdx === idx;
                      return (
                        <tr key={idx} className="bg-white">
                          <td className="p-2">
                            {isEditing ? (
                              <SearchableSelect
                                value={r.catalogId}
                                onChange={v => updateGeneralRetention(idx, v)}
                                placeholder="Buscar retención..."
                                options={availableRetentionOptions(formData.generalRetentions.map(rr => rr.catalogId), r.catalogId).map(c => ({ value: c.id, label: `${c.name} (${c.category} ${c.rate}%)`, shortLabel: `${c.rate}%` }))}
                              />
                            ) : (
                              <div className="flex items-center gap-2">
                                <span className="flex-1 p-2 text-sm text-slate-700">{catalogEntry ? `${catalogEntry.rate}%` : `${r.rate}%`}</span>
                                <button type="button" onClick={() => setEditingGeneralRetentionIdx(idx)} title="Editar" className="p-2 text-slate-400 hover:text-primary hover:bg-slate-100 rounded-lg transition-colors">
                                  <Edit2 size={16} />
                                </button>
                              </div>
                            )}
                          </td>
                          <td className="p-4 text-right font-mono font-bold text-slate-700">
                            ${generalRetentionAmount(r).toLocaleString('es-CO', { maximumFractionDigits: 0 })}
                          </td>
                          <td className="p-2 text-right">
                            <button type="button" onClick={() => removeGeneralRetention(idx)} className="p-2 text-rose-400 hover:text-rose-600 hover:bg-rose-50 rounded-lg transition-colors">
                              <Trash2 size={18} />
                            </button>
                          </td>
                        </tr>
                      );
                    })}
                    {formData.generalRetentions.length === 0 && (
                      <tr>
                        <td colSpan={3} className="p-8 text-center text-slate-400">Sin retenciones generales (ej. ReteICA, ReteIVA) en este documento.</td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>
              {retentionCatalog.length === 0 && <p className="text-xs text-amber-600 mt-2">Superadmin no ha configurado retenciones en el catálogo todavía.</p>}
            </div>

            <div className="flex flex-col lg:flex-row gap-8 border-t border-slate-100 pt-8">
              <div className="flex flex-col gap-8 w-full lg:max-w-lg">
                <div>
                  <div className="flex justify-between items-end mb-4">
                    <h3 className="text-lg font-bold text-slate-800">Descuento General (opcional)</h3>
                  </div>
                  <div className="grid grid-cols-2 gap-4">
                    <div>
                      <label className="block text-xs font-bold text-slate-500 mb-1">Motivo</label>
                      <input type="text" placeholder="Ej. Pronto pago" value={formData.generalDiscountReason} onChange={e => setFormData({ ...formData, generalDiscountReason: e.target.value })} className="w-full p-2 border border-slate-200 rounded-lg text-sm outline-none" />
                    </div>
                    <div>
                      <label className="block text-xs font-bold text-slate-500 mb-1">Valor ($)</label>
                      <input type="number" min="0" step="0.01" value={formData.generalDiscountAmount} onChange={e => setFormData({ ...formData, generalDiscountAmount: parseFloat(e.target.value) || 0 })} className="w-full p-2 border border-slate-200 rounded-lg text-sm outline-none font-mono" />
                    </div>
                  </div>
                </div>

                <div>
                  <div className="flex justify-between items-end mb-4">
                    <h3 className="text-lg font-bold text-slate-800">Cargos Generales (opcional)</h3>
                  </div>
                  <div className="grid grid-cols-2 gap-4">
                    <div>
                      <label className="block text-xs font-bold text-slate-500 mb-1">Motivo</label>
                      <input type="text" placeholder="Ej. Flete" value={formData.generalChargeReason} onChange={e => setFormData({ ...formData, generalChargeReason: e.target.value })} className="w-full p-2 border border-slate-200 rounded-lg text-sm outline-none" />
                    </div>
                    <div>
                      <label className="block text-xs font-bold text-slate-500 mb-1">Valor ($)</label>
                      <input type="number" min="0" step="0.01" value={formData.generalChargeAmount} onChange={e => setFormData({ ...formData, generalChargeAmount: parseFloat(e.target.value) || 0 })} className="w-full p-2 border border-slate-200 rounded-lg text-sm outline-none font-mono" />
                    </div>
                  </div>
                </div>
              </div>

              <div className="flex-1 bg-slate-50 p-6 rounded-2xl border border-slate-200 h-fit">
                <div className="flex justify-between text-slate-500 mb-2">
                  <span>Subtotal:</span>
                  <span className="font-mono">${formData.subtotal.toLocaleString('es-CO')}</span>
                </div>
                {ivaBreakdown().map(g => (
                  g.isGravado ? (
                    <React.Fragment key={g.label}>
                      <div className="flex justify-between text-slate-500 mb-2">
                        <span>Base {g.label}:</span>
                        <span className="font-mono">${g.base.toLocaleString('es-CO')}</span>
                      </div>
                      <div className="flex justify-between text-slate-500 mb-2">
                        <span>{g.label}:</span>
                        <span className="font-mono">${g.tax.toLocaleString('es-CO')}</span>
                      </div>
                    </React.Fragment>
                  ) : (
                    <div key={g.label} className="flex justify-between text-slate-500 mb-2">
                      <span>{g.label}:</span>
                      <span className="font-mono">${g.base.toLocaleString('es-CO')}</span>
                    </div>
                  )
                ))}
                {discriminatedRetentions().map(r => (
                  <div key={r.label} className="flex justify-between text-rose-600 mb-2">
                    <span>{r.label}:</span>
                    <span className="font-mono">-${r.amount.toLocaleString('es-CO', { maximumFractionDigits: 0 })}</span>
                  </div>
                ))}
                {formData.generalDiscountAmount > 0 && (
                  <div className="flex justify-between text-rose-600 mb-2">
                    <span>Desc. general:</span>
                    <span className="font-mono">-${formData.generalDiscountAmount.toLocaleString('es-CO', { maximumFractionDigits: 0 })}</span>
                  </div>
                )}
                {formData.generalChargeAmount > 0 && (
                  <div className="flex justify-between text-emerald-600 mb-2">
                    <span>Cargo general:</span>
                    <span className="font-mono">+${formData.generalChargeAmount.toLocaleString('es-CO', { maximumFractionDigits: 0 })}</span>
                  </div>
                )}
                <div className="mb-4 pb-4 border-b border-slate-200" />
                <div className="flex justify-between font-extrabold text-xl text-slate-800">
                  <span>Total:</span>
                  <span className="font-mono">${(formData.totalAmount - totalRetentions - formData.generalDiscountAmount + formData.generalChargeAmount).toLocaleString('es-CO')}</span>
                </div>
              </div>
            </div>
            
            <div className="mt-8 flex justify-end gap-4">
              <button onClick={() => { setEditingId(null); setView('list'); }} className="px-6 py-3 font-bold text-slate-600 hover:bg-slate-100 rounded-xl transition-colors">Cancelar</button>
              <button onClick={handleSaveDraft} disabled={savingDraft} className="px-8 py-3 bg-slate-900 hover:bg-black text-white font-bold rounded-xl shadow-lg transition-all flex items-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed">
                <FileText size={18} /> {savingDraft ? 'Guardando...' : editingId ? 'Guardar Cambios' : 'Guardar Borrador'}
              </button>
            </div>

          </div>
        </div>

        {showCustomerModal && (
          <div className="fixed inset-0 bg-slate-900/50 backdrop-blur-sm z-50 flex items-center justify-center p-4">
            <div className="bg-white rounded-3xl w-full max-w-lg shadow-2xl overflow-hidden">
              <div className="px-6 py-4 border-b border-slate-100 flex justify-between items-center bg-slate-50/50">
                <h3 className="text-xl font-bold text-slate-800">Nuevo Tercero</h3>
                <button onClick={() => setShowCustomerModal(false)} className="text-slate-400 hover:text-slate-600 p-2"><X size={20} /></button>
              </div>
              <form onSubmit={handleSaveQuickCustomer} className="p-6 space-y-4">
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-1">Tipo de Persona</label>
                  <div className="grid grid-cols-2 gap-2">
                    {(['Natural', 'Juridica'] as const).map(pt => (
                      <button
                        key={pt}
                        type="button"
                        onClick={() => setQuickCustomer({ ...quickCustomer, personType: pt, identificationType: pt === 'Juridica' ? '31' : '13' })}
                        className={`px-3 py-2.5 rounded-xl font-bold text-sm transition-colors ${
                          quickCustomer.personType === pt ? 'bg-primary text-white shadow-sm' : 'bg-slate-100 text-slate-500 hover:bg-slate-200'
                        }`}
                      >
                        {pt === 'Natural' ? 'Persona Natural' : 'Persona Jurídica'}
                      </button>
                    ))}
                  </div>
                </div>
                {quickCustomer.personType === 'Juridica' ? (
                  <div>
                    <label className="block text-sm font-bold text-slate-700 mb-1">Razón Social</label>
                    <input type="text" required value={quickCustomer.name} onChange={e => setQuickCustomer({ ...quickCustomer, name: e.target.value })} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none" />
                  </div>
                ) : (
                  <div className="grid grid-cols-2 gap-4">
                    {(['firstName', 'secondName', 'firstLastName', 'secondLastName'] as const).map((field, i) => (
                      <div key={field}>
                        <label className="block text-sm font-bold text-slate-700 mb-1">{['Nombre 1', 'Nombre 2', 'Apellido 1', 'Apellido 2'][i]}</label>
                        <input
                          type="text" required={i === 0 || i === 2} value={quickCustomer[field]}
                          onChange={e => {
                            const next = { ...quickCustomer, [field]: e.target.value };
                            next.name = [next.firstName, next.secondName, next.firstLastName, next.secondLastName].filter(Boolean).join(' ');
                            setQuickCustomer(next);
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
                      value={quickCustomer.identificationType}
                      onChange={v => setQuickCustomer({ ...quickCustomer, identificationType: v })}
                      placeholder="Buscar tipo de identificación..."
                      options={IDENTIFICATION_TYPES}
                    />
                  </div>
                  <div>
                    <label className="block text-sm font-bold text-slate-700 mb-1">Número</label>
                    <input type="text" required value={quickCustomer.identificationNumber} onChange={e => setQuickCustomer({ ...quickCustomer, identificationNumber: e.target.value })} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none" />
                  </div>
                </div>
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className="block text-sm font-bold text-slate-700 mb-1">Email</label>
                    <input type="email" required value={quickCustomer.email} onChange={e => setQuickCustomer({ ...quickCustomer, email: e.target.value })} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none" />
                  </div>
                  <div>
                    <label className="block text-sm font-bold text-slate-700 mb-1">Teléfono</label>
                    <input type="text" value={quickCustomer.phone} onChange={e => setQuickCustomer({ ...quickCustomer, phone: e.target.value })} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none" />
                  </div>
                </div>
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className="block text-sm font-bold text-slate-700 mb-1">Régimen Fiscal</label>
                    <SearchableSelect
                      required
                      value={quickCustomer.dataicoRegimen}
                      onChange={v => setQuickCustomer({ ...quickCustomer, dataicoRegimen: v })}
                      placeholder="Buscar régimen..."
                      options={regimenCatalog.map(c => ({ value: c.category, label: c.name }))}
                    />
                  </div>
                  <div>
                    <label className="block text-sm font-bold text-slate-700 mb-1">Nivel Tributario</label>
                    <SearchableSelect
                      required
                      value={quickCustomer.dataicoTaxLevelCode}
                      onChange={v => setQuickCustomer({ ...quickCustomer, dataicoTaxLevelCode: v })}
                      placeholder="Buscar nivel tributario..."
                      options={taxLevelCatalog.map(c => ({ value: c.category, label: c.name }))}
                    />
                  </div>
                </div>
                <p className="text-xs text-slate-400">Podrás completar dirección y demás datos después, desde "Mis Terceros".</p>
                <div className="flex justify-end gap-3 pt-2">
                  <button type="button" onClick={() => setShowCustomerModal(false)} className="px-5 py-2 text-slate-600 font-bold hover:bg-slate-100 rounded-xl transition-colors">Cancelar</button>
                  <button type="submit" disabled={savingCustomer} className="px-5 py-2 bg-primary text-white font-bold rounded-xl hover:bg-primary/90 transition-colors shadow-md disabled:opacity-50">
                    {savingCustomer ? 'Creando...' : 'Crear Tercero'}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {productModalForItemIndex !== null && (
          <div className="fixed inset-0 bg-slate-900/50 backdrop-blur-sm z-50 flex items-center justify-center p-4">
            <div className="bg-white rounded-3xl w-full max-w-lg shadow-2xl overflow-hidden">
              <div className="px-6 py-4 border-b border-slate-100 flex justify-between items-center bg-slate-50/50">
                <h3 className="text-xl font-bold text-slate-800">Nuevo Producto</h3>
                <button onClick={() => setProductModalForItemIndex(null)} className="text-slate-400 hover:text-slate-600 p-2"><X size={20} /></button>
              </div>
              <form onSubmit={handleSaveQuickProduct} className="p-6 space-y-4">
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className="block text-sm font-bold text-slate-700 mb-1">Código (SKU)</label>
                    <input type="text" required value={quickProduct.code} onChange={e => setQuickProduct({ ...quickProduct, code: e.target.value })} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none font-mono" />
                  </div>
                  <div>
                    <label className="block text-sm font-bold text-slate-700 mb-1">Precio Base</label>
                    <input type="number" step="0.01" required value={quickProduct.unitPrice} onChange={e => setQuickProduct({ ...quickProduct, unitPrice: parseFloat(e.target.value) || 0 })} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none font-mono" />
                  </div>
                </div>
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-1">Nombre o Descripción</label>
                  <input type="text" required value={quickProduct.name} onChange={e => setQuickProduct({ ...quickProduct, name: e.target.value })} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none" />
                </div>
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className="block text-sm font-bold text-slate-700 mb-1">Tratamiento de IVA</label>
                    <SearchableSelect
                      value={quickProduct.ivaTreatment}
                      onChange={v => setQuickProduct({ ...quickProduct, ivaTreatment: v, ivaRate: v === 'Gravado' ? 19 : 0 })}
                      placeholder="Buscar tratamiento IVA..."
                      options={IVA_TREATMENTS}
                    />
                  </div>
                  {quickProduct.ivaTreatment === 'Gravado' && (
                    <div>
                      <label className="block text-sm font-bold text-slate-700 mb-1">Tarifa IVA</label>
                      <SearchableSelect
                        value={String(quickProduct.ivaRate)}
                        onChange={v => setQuickProduct({ ...quickProduct, ivaRate: parseFloat(v) || 0 })}
                        placeholder="Buscar tarifa..."
                        options={ivaRateCatalog.length === 0
                          ? [{ value: String(quickProduct.ivaRate), label: `${quickProduct.ivaRate}%` }]
                          : ivaRateCatalog.map(c => ({ value: c.category, label: c.name }))}
                      />
                    </div>
                  )}
                </div>
                <div className="flex justify-end gap-3 pt-2">
                  <button type="button" onClick={() => setProductModalForItemIndex(null)} className="px-5 py-2 text-slate-600 font-bold hover:bg-slate-100 rounded-xl transition-colors">Cancelar</button>
                  <button type="submit" disabled={savingProduct} className="px-5 py-2 bg-primary text-white font-bold rounded-xl hover:bg-primary/90 transition-colors shadow-md disabled:opacity-50">
                    {savingProduct ? 'Creando...' : 'Crear Producto'}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}
      </div>
    );
  }

  if (view === 'detail' && viewingInvoice) {
    const inv = viewingInvoice;
    const isNote = inv.typeCode === 'NC' || inv.typeCode === 'ND';
    const lineBaseOf = (item: any) => item.quantity * item.unitPrice * (1 - (item.discountRate || 0) / 100);

    const taxGroups = new Map<string, { label: string, base: number, tax: number }>();
    (inv.items || []).forEach((item: any) => {
      const key = item.ivaTreatment === 'Gravado' ? `Gravado-${item.taxRate}` : item.ivaTreatment;
      const existing = taxGroups.get(key);
      const base = lineBaseOf(item);
      if (existing) { existing.base += base; existing.tax += item.taxAmount; }
      else taxGroups.set(key, { label: item.ivaTreatment === 'Gravado' ? `IVA ${item.taxRate}%` : item.ivaTreatment, base, tax: item.taxAmount });
    });

    const retentionsTotal =
      (inv.items || []).reduce((sum: number, item: any) => sum + (item.retentions || []).reduce((s: number, r: any) => {
        const base = r.baseType === 'IvaGenerado' ? item.taxAmount : lineBaseOf(item);
        return s + base * r.rate / 100;
      }, 0), 0) +
      (inv.generalRetentions || []).reduce((sum: number, r: any) => sum + (r.taxCategory === 'RET_IVA' ? inv.taxAmount * r.rate / 100 : inv.subtotal * r.rate / 100), 0);

    return (
      <div className="p-8">
        <button onClick={() => { setViewingInvoice(null); setView('list'); }} className="flex items-center gap-2 text-slate-500 hover:text-slate-800 mb-6 font-medium transition-colors">
          <ArrowLeft size={20} /> Volver a mis facturas
        </button>

        <div className="bg-white rounded-3xl shadow-xl overflow-hidden border border-slate-100">
          <div className="p-8 border-b border-slate-100 bg-slate-50/50 flex justify-between items-start flex-wrap gap-4">
            <div>
              <div className="flex items-center gap-3 mb-1">
                {isNote && (
                  <span className={`px-2 py-0.5 rounded text-xs font-bold ${inv.typeCode === 'NC' ? 'bg-amber-100 text-amber-700' : 'bg-orange-100 text-orange-700'}`}>{inv.typeCode}</span>
                )}
                <h2 className="text-2xl font-extrabold text-slate-800">{inv.number || 'Borrador'}</h2>
                <span className={`px-3 py-1 rounded-full text-xs font-bold ${
                  inv.status === 'DRAFT' ? 'bg-slate-100 text-slate-600' :
                  inv.status === 'PROCESSING' ? 'bg-blue-100 text-blue-600' :
                  inv.status === 'APPROVED' ? 'bg-emerald-100 text-emerald-700' :
                  'bg-rose-100 text-rose-600'
                }`}>
                  {STATUS_LABELS[inv.status] || inv.status}
                </span>
              </div>
              <p className="text-slate-500">{new Date(inv.issueDate).toLocaleDateString('es-CO')} · {inv.customer?.name || 'Consumidor Final'}</p>
              {inv.status === 'REJECTED' && inv.dianResponseMessage && (
                <p className="text-sm text-rose-500 mt-2">{inv.dianResponseMessage}</p>
              )}
            </div>
            <div className="flex gap-2 flex-wrap">
              <button onClick={() => handlePreviewPdf(inv)} disabled={previewingId === inv.id} className="px-4 py-2 text-slate-600 bg-slate-100 hover:bg-slate-200 rounded-xl font-bold text-sm transition-colors flex items-center gap-2 disabled:opacity-50">
                {previewingId === inv.id ? <Loader2 size={16} className="animate-spin" /> : <Printer size={16} />} Vista Previa
              </button>
              {(inv.status === 'DRAFT' || inv.status === 'REJECTED') && (
                <>
                  <button onClick={() => handleEditDraft(inv)} className="px-4 py-2 text-slate-600 bg-slate-100 hover:bg-slate-200 rounded-xl font-bold text-sm transition-colors flex items-center gap-2">
                    <Edit2 size={16} /> Editar
                  </button>
                  <button onClick={() => { setPublishingId(inv.id); setPublishPayment({ paymentMeans: inv.paymentMeans || '', paymentMeansType: inv.paymentMeansType || '' }); }} className="px-4 py-2 text-white bg-primary hover:bg-primary/90 rounded-xl font-bold text-sm shadow-sm transition-all flex items-center gap-2">
                    <Send size={16} /> Emitir
                  </button>
                  <button onClick={() => handleDeleteDraft(inv.id)} className="px-4 py-2 text-rose-600 bg-rose-50 hover:bg-rose-100 rounded-xl font-bold text-sm transition-colors flex items-center gap-2">
                    <Trash2 size={16} /> Eliminar
                  </button>
                </>
              )}
              {inv.status === 'APPROVED' && !isNote && (
                <>
                  <button onClick={() => handleCreateDebitNote(inv)} className="px-4 py-2 text-orange-700 bg-orange-100 hover:bg-orange-200 rounded-xl font-bold text-sm shadow-sm transition-all flex items-center gap-2">
                    <RotateCcw size={16} /> Generar Nota Débito
                  </button>
                  <button onClick={() => handleCreateCreditNote(inv)} className="px-4 py-2 text-amber-700 bg-amber-100 hover:bg-amber-200 rounded-xl font-bold text-sm shadow-sm transition-all flex items-center gap-2">
                    <RotateCcw size={16} /> Generar Nota Crédito
                  </button>
                </>
              )}
            </div>
          </div>

          <div className="p-8 space-y-8">
            <div className="grid grid-cols-2 md:grid-cols-4 gap-6 text-sm">
              <div><p className="text-slate-400 font-bold uppercase text-xs mb-1">Resolución</p><p className="text-slate-700 font-medium">{inv.resolution?.prefix || '-'}</p></div>
              <div><p className="text-slate-400 font-bold uppercase text-xs mb-1">Identificación</p><p className="text-slate-700 font-medium">{inv.customer?.identificationNumber || '-'}</p></div>
              <div><p className="text-slate-400 font-bold uppercase text-xs mb-1">Forma de Pago</p><p className="text-slate-700 font-medium">{inv.paymentMeansType === 'CREDITO' ? 'Crédito' : inv.paymentMeansType === 'DEBITO' ? 'Contado' : '-'}</p></div>
              <div><p className="text-slate-400 font-bold uppercase text-xs mb-1">Orden de Compra</p><p className="text-slate-700 font-medium">{inv.purchaseOrderReference || '-'}</p></div>
            </div>

            {(viewingOriginal || viewingRelated.length > 0) && (
              <div className="p-4 bg-slate-50 border border-slate-200 rounded-xl">
                <p className="text-xs font-bold text-slate-500 uppercase mb-2">Documentos Relacionados</p>
                <div className="space-y-1">
                  {viewingOriginal && (
                    <button onClick={() => handleViewDetail(viewingOriginal)} className="text-sm text-primary hover:underline block">
                      ← Nota de la factura N° {viewingOriginal.number}
                    </button>
                  )}
                  {viewingRelated.map(r => (
                    <button key={r.id} onClick={() => handleViewDetail(r)} className="text-sm text-primary hover:underline flex items-center gap-2">
                      <span className={`px-1.5 py-0.5 rounded text-[10px] font-bold ${r.typeCode === 'NC' ? 'bg-amber-100 text-amber-700' : 'bg-orange-100 text-orange-700'}`}>{r.typeCode}</span>
                      {r.number} · ${r.totalAmount?.toLocaleString('es-CO')} · {STATUS_LABELS[r.status] || r.status}
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
                    <th className="p-3">Desc. %</th>
                    <th className="p-3">IVA</th>
                    <th className="p-3 text-right">Total</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-200">
                  {(inv.items || []).map((item: any, idx: number) => (
                    <tr key={idx}>
                      <td className="p-3 text-sm text-slate-700">{item.name}</td>
                      <td className="p-3 text-sm">{item.quantity}</td>
                      <td className="p-3 text-sm font-mono">${item.unitPrice.toLocaleString('es-CO')}</td>
                      <td className="p-3 text-sm font-mono">{item.discountRate || 0}%</td>
                      <td className="p-3 text-sm">{item.ivaTreatment === 'Gravado' ? `${item.taxRate}%` : item.ivaTreatment}</td>
                      <td className="p-3 text-sm font-mono font-bold text-right">${item.totalAmount.toLocaleString('es-CO')}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div className="flex justify-end">
              <div className="w-full max-w-xs space-y-2 text-sm">
                {Array.from(taxGroups.values()).map(g => (
                  <React.Fragment key={g.label}>
                    <div className="flex justify-between text-slate-500"><span>Base {g.label}</span><span className="font-mono">${g.base.toLocaleString('es-CO', { maximumFractionDigits: 0 })}</span></div>
                    {g.tax > 0 && <div className="flex justify-between text-slate-700"><span>{g.label}</span><span className="font-mono">${g.tax.toLocaleString('es-CO', { maximumFractionDigits: 0 })}</span></div>}
                  </React.Fragment>
                ))}
                {retentionsTotal > 0 && (
                  <div className="flex justify-between text-rose-600"><span>Retenciones</span><span className="font-mono">-${retentionsTotal.toLocaleString('es-CO', { maximumFractionDigits: 0 })}</span></div>
                )}
                <div className="flex justify-between text-lg font-bold text-slate-800 pt-2 border-t border-slate-200">
                  <span>Total</span><span className="font-mono">${inv.totalAmount?.toLocaleString('es-CO')}</span>
                </div>
              </div>
            </div>
          </div>
        </div>

        {publishingId && (
          <div className="fixed inset-0 bg-slate-900/50 backdrop-blur-sm z-50 flex items-center justify-center p-4">
            <div className="bg-white rounded-3xl w-full max-w-md shadow-2xl p-6">
              <h3 className="text-xl font-bold text-slate-800 mb-1">Emitir Factura</h3>
              <p className="text-slate-500 text-sm mb-4">Indica el medio de pago para emitirla.</p>
              <div className="space-y-3">
                <div>
                  <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Medio de Pago</label>
                  <SearchableSelect
                    value={publishPayment.paymentMeans}
                    onChange={v => setPublishPayment({ ...publishPayment, paymentMeans: v })}
                    placeholder="Buscar medio de pago..."
                    options={paymentMeansCatalog.map(c => ({ value: c.category, label: c.name }))}
                  />
                </div>
                <div>
                  <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Forma de Pago</label>
                  <SearchableSelect
                    value={publishPayment.paymentMeansType}
                    onChange={v => setPublishPayment({ ...publishPayment, paymentMeansType: v })}
                    placeholder="Contado o crédito..."
                    options={[{ value: 'DEBITO', label: 'Contado' }, { value: 'CREDITO', label: 'Crédito' }]}
                  />
                </div>
              </div>
              <div className="mt-6 flex justify-end gap-3">
                <button onClick={() => setPublishingId(null)} className="px-5 py-2 text-slate-600 font-bold hover:bg-slate-100 rounded-xl transition-colors">Cancelar</button>
                <button onClick={confirmPublish} className="px-5 py-2 bg-primary text-white font-bold rounded-xl hover:bg-primary/90 transition-colors shadow-md">Emitir</button>
              </div>
            </div>
          </div>
        )}
      </div>
    );
  }

  const filteredInvoices = invoices.filter(inv => {
    if (!searchTerm.trim()) return true;
    const q = searchTerm.trim().toLowerCase();
    return (inv.number || '').toLowerCase().includes(q) ||
      (inv.customer?.name || '').toLowerCase().includes(q) ||
      (inv.customer?.identificationNumber || '').toLowerCase().includes(q);
  });
  const totalPages = Math.max(1, Math.ceil(filteredInvoices.length / PAGE_SIZE));
  const currentPage = Math.min(page, totalPages);
  const paginatedInvoices = filteredInvoices.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE);
  const filteredTotal = filteredInvoices.reduce((sum, inv) => sum + (inv.totalAmount || 0), 0);

  const handleExport = () => exportToCsv(
    `facturas_${customFrom}_a_${customTo}.csv`,
    filteredInvoices.map(inv => ({
      fecha: new Date(inv.issueDate).toLocaleDateString('es-CO'),
      numero: inv.number || '',
      resolucion: inv.resolution?.prefix || '',
      cliente: inv.customer?.name || 'Consumidor Final',
      identificacion: inv.customer?.identificationNumber || '',
      total: inv.totalAmount || 0,
      estado: STATUS_LABELS[inv.status] || inv.status
    })),
    [
      { key: 'fecha', label: 'Fecha' },
      { key: 'numero', label: 'Número' },
      { key: 'resolucion', label: 'Resolución' },
      { key: 'cliente', label: 'Cliente' },
      { key: 'identificacion', label: 'Identificación' },
      { key: 'total', label: 'Total' },
      { key: 'estado', label: 'Estado' }
    ]
  );

  return (
    <div className="p-8">
      <div className="flex justify-between items-center mb-8">
        <div>
          <h1 className="text-3xl font-extrabold text-slate-800">Mis Facturas</h1>
          <p className="text-slate-500 mt-1">Gestión de documentos electrónicos</p>
        </div>
        <div className="flex gap-3">
          <ImportExcelButton endpoint="/client/invoices/import" templateEndpoint="/client/invoices/template" label="Importar Excel" onDone={loadData} />
          <button
            onClick={handleCreateNew}
            className="bg-primary hover:bg-primary/90 text-white px-5 py-2.5 rounded-xl font-bold flex items-center gap-2 shadow-sm transition-all"
          >
            <Plus size={20} /> Nueva Factura
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
            <input type="text" value={searchTerm} onChange={e => setSearchTerm(e.target.value)} placeholder="Número, cliente o identificación..." className="w-full pl-9 pr-3 py-2.5 bg-white border border-slate-200 rounded-xl text-sm outline-none focus:ring-2 focus:ring-primary" />
          </div>
        </div>
        <button onClick={handleExport} disabled={filteredInvoices.length === 0} className="flex items-center gap-2 px-4 py-2.5 bg-slate-100 hover:bg-slate-200 text-slate-600 rounded-xl font-bold text-sm transition-colors disabled:opacity-50">
          <Download size={16} /> Exportar CSV
        </button>
        <p className="text-xs text-slate-400 pb-2.5">{filteredInvoices.length} documento{filteredInvoices.length === 1 ? '' : 's'} en el periodo</p>
      </div>

      <div className="bg-white rounded-2xl shadow-sm border border-slate-200 overflow-hidden">
        <table className="w-full text-left border-collapse">
          <thead>
            <tr className="bg-slate-50 border-b border-slate-200 text-sm font-bold text-slate-500 uppercase tracking-wider">
              <th className="p-4">Fecha</th>
              <th className="p-4">Número</th>
              <th className="p-4">Resolución</th>
              <th className="p-4">Cliente</th>
              <th className="p-4 text-right">Total</th>
              <th className="p-4 text-center">Estado</th>
              <th className="p-4 text-right">Acciones</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {paginatedInvoices.map(inv => (
              <tr key={inv.id} className="hover:bg-slate-50/50 transition-colors">
                <td className="p-4 text-slate-500 text-sm">{new Date(inv.issueDate).toLocaleDateString('es-CO')}</td>
                <td className="p-4 font-bold text-slate-700">
                  <div className="flex items-center gap-2">
                    {(inv.typeCode === 'NC' || inv.typeCode === 'ND') && (
                      <span className={`px-2 py-0.5 rounded text-xs font-bold ${inv.typeCode === 'NC' ? 'bg-amber-100 text-amber-700' : 'bg-orange-100 text-orange-700'}`}>
                        {inv.typeCode}
                      </span>
                    )}
                    {inv.number || '---'}
                  </div>
                </td>
                <td className="p-4 text-slate-500 text-sm font-mono">{inv.resolution?.prefix || '-'}</td>
                <td className="p-4 text-slate-900">{inv.customer?.name || 'Consumidor Final'}</td>
                <td className="p-4 text-right font-mono font-medium">${inv.totalAmount?.toLocaleString('es-CO') || '0'}</td>
                <td className="p-4 text-center">
                  <span className={`px-3 py-1 rounded-full text-xs font-bold ${
                    inv.status === 'DRAFT' ? 'bg-slate-100 text-slate-600' :
                    inv.status === 'PROCESSING' ? 'bg-blue-100 text-blue-600' :
                    inv.status === 'APPROVED' ? 'bg-emerald-100 text-emerald-700' :
                    'bg-rose-100 text-rose-600'
                  }`}>
                    {STATUS_LABELS[inv.status] || inv.status}
                  </span>
                  {inv.status === 'REJECTED' && inv.dianResponseMessage && (
                    <p className="text-xs text-rose-500 mt-1 max-w-[220px] mx-auto">{inv.dianResponseMessage}</p>
                  )}
                </td>
                <td className="p-4 flex items-center justify-end gap-2">
                  {(inv.status === 'DRAFT' || inv.status === 'REJECTED') && (
                    <>
                      <button onClick={() => handleEditDraft(inv)} title="Editar" className="p-2 text-slate-400 hover:text-blue-600 hover:bg-blue-50 rounded-lg transition-colors">
                        <Edit2 size={16} />
                      </button>
                      <button onClick={() => { setPublishingId(inv.id); setPublishPayment({ paymentMeans: inv.paymentMeans || '', paymentMeansType: inv.paymentMeansType || '' }); }} title="Emitir a la DIAN" className="p-2 text-white bg-primary hover:bg-primary/90 rounded-lg shadow-sm transition-all flex items-center gap-1 text-sm font-bold">
                        <Send size={16} /> Emitir
                      </button>
                      <button onClick={() => handleDeleteDraft(inv.id)} title="Eliminar" className="p-2 text-slate-400 hover:text-rose-600 hover:bg-rose-50 rounded-lg transition-colors">
                        <Trash2 size={16} />
                      </button>
                    </>
                  )}
                  <button onClick={() => handleViewDetail(inv)} title="Ver detalle" className="p-2 text-slate-400 hover:text-primary hover:bg-slate-100 rounded-lg transition-colors">
                    <Eye size={16} />
                  </button>
                  <button onClick={() => handlePreviewPdf(inv)} disabled={previewingId === inv.id} title="Vista previa de impresión" className="p-2 text-slate-400 hover:text-primary hover:bg-slate-100 rounded-lg transition-colors disabled:opacity-50">
                    {previewingId === inv.id ? <Loader2 size={16} className="animate-spin" /> : <Printer size={16} />}
                  </button>
                </td>
              </tr>
            ))}
            {filteredInvoices.length === 0 && (
              <tr>
                <td colSpan={7} className="p-8 text-center text-slate-500">No hay facturas que coincidan con el filtro.</td>
              </tr>
            )}
          </tbody>
          {filteredInvoices.length > 0 && (
            <tfoot>
              <tr className="bg-slate-50 border-t-2 border-slate-200 font-bold text-slate-700">
                <td colSpan={4} className="p-4 text-right">Total del periodo:</td>
                <td className="p-4 text-right font-mono">${filteredTotal.toLocaleString('es-CO')}</td>
                <td colSpan={2}></td>
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

      {publishingId && (
        <div className="fixed inset-0 bg-slate-900/50 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-3xl w-full max-w-md shadow-2xl p-6">
            <h3 className="text-xl font-bold text-slate-800 mb-1">Emitir Factura</h3>
            <p className="text-slate-500 text-sm mb-4">Indica el medio de pago para emitirla.</p>
            <div className="space-y-3">
              <div>
                <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Medio de Pago</label>
                <SearchableSelect
                  value={publishPayment.paymentMeans}
                  onChange={v => setPublishPayment({ ...publishPayment, paymentMeans: v })}
                  placeholder="Buscar medio de pago..."
                  options={paymentMeansCatalog.map(c => ({ value: c.category, label: c.name }))}
                />
              </div>
              <div>
                <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Forma de Pago</label>
                <SearchableSelect
                  value={publishPayment.paymentMeansType}
                  onChange={v => setPublishPayment({ ...publishPayment, paymentMeansType: v })}
                  placeholder="Contado o crédito..."
                  options={[{ value: 'DEBITO', label: 'Contado' }, { value: 'CREDITO', label: 'Crédito' }]}
                />
              </div>
            </div>
            <div className="mt-6 flex justify-end gap-3">
              <button onClick={() => setPublishingId(null)} className="px-5 py-2 text-slate-600 font-bold hover:bg-slate-100 rounded-xl transition-colors">Cancelar</button>
              <button onClick={confirmPublish} className="px-5 py-2 bg-primary text-white font-bold rounded-xl hover:bg-primary/90 transition-colors shadow-md">Emitir</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
