import React, { useState, useEffect } from 'react';
import { Plus, Edit2, Trash2, Send, FileText, Loader2, ArrowLeft, PlusCircle, RotateCcw, X, UserPlus, Percent, Search, Download, ChevronLeft, ChevronRight, Printer, Link2, Info, AlertCircle, Mail } from 'lucide-react';
import { toast } from 'sonner';
import { api, getErrorMessage } from '../lib/api';
import { useConfirm } from '@/components/ConfirmDialog';
import ImportExcelButton from '../components/ImportExcelButton';
import CustomerFormModal from '../components/CustomerFormModal';
import SearchableSelect from '@shared/components/SearchableSelect';
import { DATE_RANGE_PRESET_OPTIONS, getDateRangeForPreset, type DateRangePreset } from '../lib/dateRangePresets';
import { exportToCsv } from '../lib/exportCsv';
import { useNumberFormat } from '../lib/numberFormat';
import DecimalInput from '../components/DecimalInput';
import { todayColombia } from '../lib/colombiaTime';
import Modal from '../components/Modal';
import ResponsiveList, { type ResponsiveListColumn } from '../components/ResponsiveList';
import RowIconButton from '../components/RowIconButton';
import { Button } from '../components/ui/button';

const PAGE_SIZE = 25;

const IVA_TREATMENTS = [
  { value: 'Gravado', label: 'Gravado' },
  { value: 'Exento', label: 'Exento' },
  { value: 'Excluido', label: 'Excluido' }
];

// Rótulo del resumen de retenciones: el tipo de impuesto, no el nombre del concepto específico
// del catálogo (ej. "Compras generales (declarantes)") — para que RET_FUENTE al 2.5% y al 3.5%
// aparezcan como "RETE FUENTE 2.5%" / "RETE FUENTE 3.5%" sin importar qué concepto se usó.
const RETENTION_CATEGORY_LABELS: Record<string, string> = {
  RET_FUENTE: 'RETE FUENTE',
  RET_ICA: 'RETE ICA',
  RET_IVA: 'RETE IVA'
};
const retentionCategoryLabel = (taxCategory: string) => RETENTION_CATEGORY_LABELS[taxCategory] || taxCategory;

// Total de retenciones de un documento ya guardado: las de ítem usan el monto que quedó
// calculado y persistido al guardar (item.retentions[].amount), no se recalculan a partir de la
// tarifa — las generales sí, porque DocumentGeneralRetentions solo guarda categoría+tarifa, no un
// monto (se prorratean entre ítems recién al enviar al integrador).
const invoiceRetentionsTotal = (inv: any) =>
  (inv.items || []).reduce((sum: number, item: any) => sum + (item.retentions || []).reduce((s: number, r: any) => s + (r.amount || 0), 0), 0) +
  (inv.generalRetentions || []).reduce((sum: number, r: any) => sum + (r.taxCategory === 'RET_IVA' ? inv.taxAmount * r.rate / 100 : inv.subtotal * r.rate / 100), 0);

// Total neto de un documento: el bruto (subtotal + IVA) menos retenciones y descuento general,
// más cargo general — misma fórmula que el "Total:" del formulario de creación/edición.
const invoiceNetTotal = (inv: any) =>
  (inv.totalAmount || 0) - invoiceRetentionsTotal(inv) - (inv.generalDiscountAmount || 0) + (inv.generalChargeAmount || 0);

// dianResponseMessage guarda la respuesta cruda del integrador (Dataico o DIAN nativo) como texto: en rechazo, un JSON
// {"errors":[{"path":[...], "error":"..."}]}; en aprobación, un JSON con los datos del documento
// ya emitido (incluye "dian_status", el estado propio de la DIAN dentro de esa misma respuesta —
// no hay un campo separado nuestro para eso). Se parsea acá solo para mostrarlo legible en un
// modal en vez de como texto crudo; si no es JSON válido, se muestra tal cual.
//
// La respuesta del integrador trae mucho más de lo que vale la pena mostrarle al cliente (customer e
// items como objetos/arreglos completos, el SOAP de la DIAN en base64 dentro de "xml", uuid,
// numbering, etc.) — por eso esto es una lista blanca explícita, no un volcado de todas las
// claves; lo que no está acá simplemente no se muestra.
const DIAN_RESPONSE_DISPLAY_FIELDS: { key: string; label: string }[] = [
  { key: 'number', label: 'Número' },
  { key: 'issue_date', label: 'Fecha de emisión' },
  { key: 'dian_status', label: 'Estado DIAN' },
  { key: 'payment_means_type', label: 'Forma de pago' },
];
const parseDianResponse = (raw: string): { errors?: { path?: string[]; error: string }[] } | { fields: Record<string, any> } | null => {
  try {
    const parsed = JSON.parse(raw);
    if (Array.isArray(parsed?.errors)) return { errors: parsed.errors };
    if (parsed && typeof parsed === 'object') return { fields: parsed };
    return null;
  } catch {
    return null;
  }
};

// Solo la etiqueta visible cambia a español; el valor (DRAFT/PROCESSING/APPROVED/REJECTED) sigue
// igual porque el resto del código lo usa en comparaciones.
const STATUS_LABELS: Record<string, string> = {
  DRAFT: 'Borrador',
  PROCESSING: 'Procesando',
  APPROVED: 'Emitida',
  REJECTED: 'Rechazada'
};

// Contrato AIU (Administración, Imprevistos, Utilidad) — factura con tipo de operación "09". El
// formulario pide la base del contrato y los porcentajes y genera las tres líneas; solo la
// Utilidad causa IVA. El objeto del contrato va en la nota obligatoria de la línea de Administración.
// Se guarda como JSON en sectorExtensionData ({ aiu: {...} }), el backend lo lee del documento.
interface AiuParams {
  baseAmount: number;
  adminPercent: number;
  unforeseenPercent: number;
  profitPercent: number;
  ivaRate: number;
  contractObject: string;
}
const AIU_DEFAULT: AiuParams = { baseAmount: 0, adminPercent: 10, unforeseenPercent: 5, profitPercent: 5, ivaRate: 19, contractObject: '' };
const AIU_OPERATION_TYPE = '09';
const round2 = (n: number) => Math.round((n + Number.EPSILON) * 100) / 100;

const buildAiuItems = (aiu: AiuParams) => {
  const base = (code: string, name: string, percent: number, taxed: boolean) => {
    const unitPrice = round2(aiu.baseAmount * percent / 100);
    const taxAmount = taxed ? round2(unitPrice * aiu.ivaRate / 100) : 0;
    return {
      productId: '', code, name, quantity: 1, unitPrice,
      unitOfMeasureCode: '94', unitOfMeasureAbbreviation: 'EA', unitOfMeasureDisplayFormat: 'Combined',
      ivaTreatment: taxed ? 'Gravado' : 'Excluido', taxRate: taxed ? aiu.ivaRate : 0, discountRate: 0,
      taxAmount, totalAmount: unitPrice + taxAmount, retentions: [] as any[]
    };
  };
  return [
    base('AIU-A', 'Administración (A.I.U.)', aiu.adminPercent, false),
    base('AIU-I', 'Imprevistos (A.I.U.)', aiu.unforeseenPercent, false),
    base('AIU-U', 'Utilidad (A.I.U.)', aiu.profitPercent, true),
  ];
};

const parseAiu = (sectorExtensionData?: string): AiuParams | null => {
  try {
    const a = JSON.parse(sectorExtensionData || '{}')?.aiu;
    return a ? { ...AIU_DEFAULT, ...a } : null;
  } catch { return null; }
};

// Notas sobre otro documento: crédito/débito de facturas y ajuste de documento soporte.
const NOTE_BADGES: Record<string, { label: string, className: string }> = {
  NC: { label: 'NC', className: 'bg-amber-100 text-amber-700' },
  ND: { label: 'ND', className: 'bg-orange-100 text-orange-700' },
  'DS-AJUSTE': { label: 'Ajuste', className: 'bg-amber-100 text-amber-700' },
};
const isNoteType = (typeCode?: string) => !!typeCode && typeCode in NOTE_BADGES;
const isInvoiceDocument = (typeCode?: string) =>
  !!typeCode && ((typeCode.startsWith('FE') && typeCode !== 'FE-TEST') || typeCode === 'NC' || typeCode === 'ND');
const isSupportDocument = (typeCode?: string) => typeCode === 'DS' || typeCode === 'DS-AJUSTE';

// Orden de la lista: primero lo que falta por enviar (Borrador), luego lo rechazado (necesita
// atención), y de último lo ya tramitado (Procesando/Emitida) — dentro de cada grupo, de mayor a
// menor número.
const STATUS_SORT_RANK: Record<string, number> = { DRAFT: 0, REJECTED: 1, PROCESSING: 2, APPROVED: 2 };

// Mismo formulario y listado para Factura y Documento Soporte: lo que cambia entre ambos es el
// tercero (cliente / proveedor), la resolución, el endpoint y qué funciones aplican. Facturas lleva
// además Notas Crédito/Débito con tipos de documento; Documento Soporte lleva Nota de Ajuste.
export type DocumentMode = 'invoice' | 'support';

const MODES = {
  invoice: {
    api: '/client/invoices',
    partyType: 'Cliente',
    party: 'Cliente',
    partyPlaceholder: 'Buscar cliente por nombre o identificación...',
    resolutionType: 'FE',
    title: 'Mis Facturas',
    singular: 'Factura',
    backToList: 'Volver a mis facturas',
    csvPrefix: 'facturas',
    searchPlaceholder: 'Número, cliente o identificación...',
    missingParty: 'Debes seleccionar un cliente',
    missingResolution: 'Debes seleccionar la resolución de facturación a usar',
    documentTypes: true,
    creditDebitNotes: true,
    resend: true,
    importExcel: true,
    generalCharge: true,
    originalLabel: 'Factura original',
  },
  support: {
    api: '/client/support-documents',
    partyType: 'Proveedor',
    party: 'Proveedor',
    partyPlaceholder: 'Buscar proveedor por nombre o identificación...',
    resolutionType: 'DS',
    title: 'Documentos Soporte',
    singular: 'Documento Soporte',
    backToList: 'Volver a documentos soporte',
    csvPrefix: 'documentos_soporte',
    searchPlaceholder: 'Número, proveedor o identificación...',
    missingParty: 'Debes seleccionar un proveedor',
    missingResolution: 'Debes seleccionar la resolución de documento soporte a usar',
    documentTypes: false,
    creditDebitNotes: false,
    resend: false,
    importExcel: false,
    // Los ejemplos de Dataico para support_docs no traen cargos generales: apagado hasta confirmarlo en
    // el sandbox (el backend ya los guarda y los envía como "charges" si llegan).
    generalCharge: false,
    originalLabel: 'Documento soporte original',
  },
} as const;

const initialQuickProduct = {
  code: '', name: '', unitPrice: 0, unitOfMeasureId: '30000000-0000-0000-0000-000000000001', standardCode: '', ivaTreatment: 'Gravado', ivaRate: 19, taxes: [] as any[]
};

export default function DocumentsPage({ mode }: { mode: DocumentMode }) {
  const cfg = MODES[mode];
  const isSupport = mode === 'support';
  const fmt = useNumberFormat();
  const confirm = useConfirm();
  const [invoices, setInvoices] = useState<any[]>([]);
  const [datePreset, setDatePreset] = useState<DateRangePreset>('this-month');
  const [customFrom, setCustomFrom] = useState(() => getDateRangeForPreset('this-month')!.from);
  const [customTo, setCustomTo] = useState(() => getDateRangeForPreset('this-month')!.to);
  const [appliedCustomFrom, setAppliedCustomFrom] = useState(() => getDateRangeForPreset('this-month')!.from);
  const [appliedCustomTo, setAppliedCustomTo] = useState(() => getDateRangeForPreset('this-month')!.to);
  const [customers, setCustomers] = useState<any[]>([]);
  const [products, setProducts] = useState<any[]>([]);
  const [documentTypes, setDocumentTypes] = useState<any[]>([]);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [relatedOpenId, setRelatedOpenId] = useState<string | null>(null);
  const [responseModalInv, setResponseModalInv] = useState<any>(null);
  const [resendModalInv, setResendModalInv] = useState<any>(null);
  const [resendEmailDraft, setResendEmailDraft] = useState('');
  const [resendingId, setResendingId] = useState<string | null>(null);
  const [resolutionFilter, setResolutionFilter] = useState('');
  const [page, setPage] = useState(1);
  const [resolutions, setResolutions] = useState<any[]>([]);
  const [retentionCatalog, setRetentionCatalog] = useState<{ id: string, category: string, name: string, rate: number }[]>([]);
  const [ivaRateCatalog, setIvaRateCatalog] = useState<{ id: string, category: string, name: string }[]>([]);
  const [paymentTermCatalog, setPaymentTermCatalog] = useState<{ id: string, category: string, name: string }[]>([]);
  const [paymentMeansCatalog, setPaymentMeansCatalog] = useState<{ id: string, category: string, name: string }[]>([]);
  const [formaPagoCatalog, setFormaPagoCatalog] = useState<{ id: string, category: string, name: string }[]>([]);
  const [creditNoteReasonCatalog, setCreditNoteReasonCatalog] = useState<{ id: string, category: string, name: string }[]>([]);
  const [debitNoteReasonCatalog, setDebitNoteReasonCatalog] = useState<{ id: string, category: string, name: string }[]>([]);
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

  const todayIso = () => todayColombia();

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
    referenceConcept: '',
    discrepancyResponseCode: '',
    aiu: null as AiuParams | null,
    sectorExtensionData: '{}'
  };
  const [formData, setFormData] = useState(initialForm);

  const [showCustomerModal, setShowCustomerModal] = useState(false);

  const [productModalForItemIndex, setProductModalForItemIndex] = useState<number | null>(null);
  const [quickProduct, setQuickProduct] = useState(initialQuickProduct);
  const [savingProduct, setSavingProduct] = useState(false);

  useEffect(() => {
    loadData();
  }, [datePreset, appliedCustomFrom, appliedCustomTo]);

  useEffect(() => {
    setPage(1);
  }, [searchTerm, statusFilter, resolutionFilter, datePreset, appliedCustomFrom, appliedCustomTo]);

  useEffect(() => {
    if (!relatedOpenId) return;
    const handler = (e: MouseEvent) => {
      if (!(e.target as HTMLElement).closest('[data-related-popover]')) setRelatedOpenId(null);
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [relatedOpenId]);

  useEffect(() => {
    api.get('/client/resolutions')
      .then(res => setResolutions(res.data.filter((r: any) => isSupport ? (r.documentType === 'DS' || r.documentType === 'POS') : r.documentType !== 'POS' && r.documentType !== 'FE-TEST')))
      .catch(() => {});
    api.get('/client/tax-catalog?kind=Retention')
      .then(res => setRetentionCatalog(res.data.filter((c: any) => !isSupport || c.category !== 'RET_IVA')))
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
    api.get('/client/tax-catalog?kind=FormaPago')
      .then(res => setFormaPagoCatalog(res.data))
      .catch(() => {});
    api.get('/client/tax-catalog?kind=CreditNoteReason')
      .then(res => setCreditNoteReasonCatalog(res.data))
      .catch(() => {});
    api.get('/client/tax-catalog?kind=DebitNoteReason')
      .then(res => setDebitNoteReasonCatalog(res.data))
      .catch(() => {});
    api.get(`/client/retention-concepts/catalog?scope=${isSupport ? 'Support' : 'Invoice'}`)
      .then(res => setAutoRetentionCatalog((res.data.concepts || []).filter((c: any) => !isSupport || c.taxCategory !== 'RET_IVA')))
      .catch(() => {});
  }, []);

  const loadData = async () => {
    setLoading(true);
    try {
      const range = datePreset === 'custom'
        ? { from: appliedCustomFrom, to: appliedCustomTo }
        : getDateRangeForPreset(datePreset)!;
      const [invRes, custRes, prodRes, docTypesRes] = await Promise.all([
        api.get(`${cfg.api}?from=${range.from}&to=${range.to}`),
        api.get(`/client/customers?partyType=${cfg.partyType}`),
        api.get(`/client/products?scope=${isSupport ? 'Support' : 'Invoice'}`),
        cfg.documentTypes ? api.get('/client/invoices/document-types') : Promise.resolve({ data: [] })
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
      documentTypeId: (documentTypes.find(d => d.typeCode === 'FE-STD') || documentTypes.find(d => d.code === '01'))?.id || '',
      // Preselecciona la resolución marcada como default para Factura (FE); antes siempre
      // arrancaba vacía aunque el cliente ya tuviera una definida, y quedaba editable por si
      // hay que emitir con otra.
      resolutionId: resolutions.find(r => (r.documentType === cfg.resolutionType || (isSupport && r.documentType === 'POS')) && r.isDefault)?.id || ''
    });
    setView('create');
  };

  const [previewingId, setPreviewingId] = useState<string | null>(null);

  const handlePreviewPdf = async (invoice: any) => {
    setPreviewingId(invoice.id);
    try {
      const res = await api.get(`${cfg.api}/${invoice.id}/preview`, { responseType: 'blob' });
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

  const openResendModal = (invoice: any) => {
    setResendModalInv(invoice);
    setResendEmailDraft(invoice.customer?.email || '');
  };

  const handleResend = async () => {
    if (!resendModalInv) return;
    if (!resendEmailDraft.trim()) {
      toast.error('Indica un correo de destino.');
      return;
    }
    setResendingId(resendModalInv.id);
    try {
      await api.post(`/client/invoices/${resendModalInv.id}/resend`, { email: resendEmailDraft.trim() });
      toast.success('Documento reenviado correctamente.');
      setResendModalInv(null);
    } catch (err: any) {
      toast.error(getErrorMessage(err, 'Error al reenviar el documento'));
    } finally {
      setResendingId(null);
    }
  };

  const handleViewDetail = async (invoice: any) => {
    try {
      const [fullRes, relatedRes] = await Promise.all([
        api.get(`${cfg.api}/${invoice.id}`),
        api.get(`${cfg.api}/${invoice.id}/related`)
      ]);
      setViewingInvoice(fullRes.data);
      setViewingRelated(relatedRes.data);
      setViewingOriginal(null);
      if (fullRes.data.referenceDocumentId) {
        api.get(`${cfg.api}/${fullRes.data.referenceDocumentId}`)
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
      const res = await api.get(`${cfg.api}/${invoice.id}`);
      const full = res.data;
      setEditingId(full.id);
      setPaymentTermCustom(full.paymentTermDays != null && !paymentTermCatalog.some(c => c.category === String(full.paymentTermDays)));
      setFormData({
        documentTypeId: full.documentTypeId || (documentTypes.find(d => d.typeCode === 'FE-STD') || documentTypes.find(d => d.code === '01'))?.id || '',
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
          unitOfMeasureCode: i.unitOfMeasureCode || '94',
          unitOfMeasureAbbreviation: i.unitOfMeasureAbbreviation || 'EA',
          unitOfMeasureDisplayFormat: i.unitOfMeasureDisplayFormat || 'Combined',
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
        referenceConcept: full.referenceConcept || '',
        discrepancyResponseCode: full.discrepancyResponseCode || '',
        aiu: parseAiu(full.sectorExtensionData),
        sectorExtensionData: full.sectorExtensionData || '{}'
      });
      setView('create');
    } catch (err) {
      toast.error('Error al cargar el documento');
    }
  };

  const handleDeleteDraft = async (id: string) => {
    if (!(await confirm('¿Eliminar este documento?'))) return;
    try {
      await api.delete(`${cfg.api}/${id}`);
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
        referenceConcept: 'Devolución parcial de los bienes y/o no aceptación parcial del servicio', // Valor por defecto — código 1
        discrepancyResponseCode: '1',
        // La nota es un espejo de la factura que afecta: mismas condiciones comerciales, no las
        // que traiga initialForm por defecto.
        paymentMeans: fullInvoice.paymentMeans || '',
        paymentMeansType: fullInvoice.paymentMeansType || '',
        paymentTermDays: fullInvoice.paymentTermDays ?? '',
        purchaseOrderReference: fullInvoice.purchaseOrderReference || '',
        items: fullInvoice.items.map((i: any) => ({
          productId: i.productId || '',
          code: i.code,
          name: i.name,
          quantity: i.quantity,
          unitPrice: i.unitPrice,
          unitOfMeasureCode: i.unitOfMeasureCode || '94',
          unitOfMeasureAbbreviation: i.unitOfMeasureAbbreviation || 'EA',
          unitOfMeasureDisplayFormat: i.unitOfMeasureDisplayFormat || 'Combined',
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
        generalRetentions: (fullInvoice.generalRetentions || []).map((r: any) => ({
          catalogId: retentionCatalog.find(c => c.category === r.taxCategory && c.rate === r.rate)?.id || '',
          taxCategory: r.taxCategory,
          rate: r.rate
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
        referenceConcept: 'Intereses', // Valor por defecto — código 1
        discrepancyResponseCode: '1',
        // La nota es un espejo de la factura que afecta: mismas condiciones comerciales, no las
        // que traiga initialForm por defecto.
        paymentMeans: fullInvoice.paymentMeans || '',
        paymentMeansType: fullInvoice.paymentMeansType || '',
        paymentTermDays: fullInvoice.paymentTermDays ?? '',
        purchaseOrderReference: fullInvoice.purchaseOrderReference || '',
        items: fullInvoice.items.map((i: any) => ({
          productId: i.productId || '',
          code: i.code,
          name: i.name,
          quantity: i.quantity,
          unitPrice: i.unitPrice,
          unitOfMeasureCode: i.unitOfMeasureCode || '94',
          unitOfMeasureAbbreviation: i.unitOfMeasureAbbreviation || 'EA',
          unitOfMeasureDisplayFormat: i.unitOfMeasureDisplayFormat || 'Combined',
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
        generalRetentions: (fullInvoice.generalRetentions || []).map((r: any) => ({
          catalogId: retentionCatalog.find(c => c.category === r.taxCategory && c.rate === r.rate)?.id || '',
          taxCategory: r.taxCategory,
          rate: r.rate
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

  // Documento Soporte: la Nota de Ajuste es un espejo del documento que ajusta (mismos ítems y
  // condiciones comerciales); el usuario edita lo que cambia y el concepto del ajuste.
  const handleCreateAdjustment = async (original: any) => {
    try {
      const res = await api.get(`${cfg.api}/${original.id}`);
      const full = res.data;
      setEditingId(null);
      setPaymentTermCustom(false);
      setFormData({
        ...initialForm,
        customerId: full.customerId,
        // La nota abre como el documento que ajusta: misma resolución y mismas observaciones.
        resolutionId: full.resolutionId || '',
        notes: full.notes || '',
        referenceDocumentId: full.id,
        referenceConcept: 'Ajuste de precio',
        paymentMeans: full.paymentMeans || '',
        paymentMeansType: full.paymentMeansType || '',
        paymentTermDays: full.paymentTermDays ?? '',
        purchaseOrderReference: full.purchaseOrderReference || '',
        items: full.items.map((i: any) => ({
          productId: i.productId || '',
          code: i.code,
          name: i.name,
          quantity: i.quantity,
          unitPrice: i.unitPrice,
          unitOfMeasureCode: i.unitOfMeasureCode || '94',
          unitOfMeasureAbbreviation: i.unitOfMeasureAbbreviation || 'EA',
          unitOfMeasureDisplayFormat: i.unitOfMeasureDisplayFormat || 'Combined',
          ivaTreatment: i.ivaTreatment || 'Gravado',
          taxRate: i.taxRate,
          discountRate: i.discountRate || 0,
          taxAmount: i.taxAmount,
          totalAmount: i.totalAmount,
          // La nota es el reflejo del documento que ajusta: se copian sus retenciones (por ítem y generales)
          // y el descuento/cargo generales, igual que hace la nota crédito con la factura.
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
        generalRetentions: (full.generalRetentions || []).map((r: any) => ({
          catalogId: retentionCatalog.find(c => c.category === r.taxCategory && c.rate === r.rate)?.id || '',
          taxCategory: r.taxCategory,
          rate: r.rate
        })),
        generalDiscountReason: full.generalDiscountReason || '',
        generalDiscountAmount: Number(full.generalDiscountAmount || 0),
        generalChargeReason: full.generalChargeReason || '',
        generalChargeAmount: Number(full.generalChargeAmount || 0),
        subtotal: full.subtotal,
        taxAmount: full.taxAmount,
        totalAmount: full.totalAmount
      });
      setView('create');
    } catch {
      toast.error('Error al cargar el documento soporte original');
    }
  };

  const selectedDocType = documentTypes.find(d => d.id === formData.documentTypeId);
  const isAiu = !isSupport && selectedDocType?.operationType === AIU_OPERATION_TYPE && !formData.referenceDocumentId;

  // Cambia algún dato del contrato AIU: se regeneran las tres líneas y los totales.
  const applyAiu = (next: AiuParams) => {
    const items = buildAiuItems(next);
    const sub = items.reduce((sum, i) => sum + i.unitPrice, 0);
    const tax = items.reduce((sum, i) => sum + i.taxAmount, 0);
    setFormData({ ...formData, aiu: next, items, subtotal: sub, taxAmount: tax, totalAmount: sub + tax });
  };

  const handleDocumentTypeChange = (typeId: string) => {
    const next = documentTypes.find(d => d.id === typeId);
    if (!isSupport && next?.operationType === AIU_OPERATION_TYPE) {
      const aiu = formData.aiu || AIU_DEFAULT;
      const items = buildAiuItems(aiu);
      const sub = items.reduce((sum, i) => sum + i.unitPrice, 0);
      const tax = items.reduce((sum, i) => sum + i.taxAmount, 0);
      setFormData({ ...formData, documentTypeId: typeId, aiu, items, subtotal: sub, taxAmount: tax, totalAmount: sub + tax });
    } else if (formData.aiu) {
      // Se sale de AIU: las líneas generadas no sirven para otro tipo de factura.
      setFormData({ ...formData, documentTypeId: typeId, aiu: null, items: [], subtotal: 0, taxAmount: 0, totalAmount: 0 });
    } else {
      setFormData({ ...formData, documentTypeId: typeId });
    }
  };

  const addItem = () => {
    setFormData({
      ...formData,
      items: [...formData.items, { productId: '', code: '', name: '', quantity: 1, unitPrice: 0, unitOfMeasureCode: '94', unitOfMeasureAbbreviation: 'EA', unitOfMeasureDisplayFormat: 'Combined', ivaTreatment: isSupport ? 'Exento' : 'Gravado', taxRate: isSupport ? 0 : 19, discountRate: 0, taxAmount: 0, totalAmount: 0, retentions: [] }]
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
        item.unitOfMeasureCode = p.unitOfMeasure?.dianCode || '94';
        item.unitOfMeasureAbbreviation = p.unitOfMeasure?.abbreviation || 'EA';
        item.unitOfMeasureDisplayFormat = p.unitOfMeasure?.displayFormat || 'Combined';
        item.ivaTreatment = isSupport ? 'Exento' : (p.ivaTreatment || 'Gravado');
        item.taxRate = isSupport ? 0 : (p.ivaTreatment === 'Gravado' ? (p.ivaRate ?? 0) : 0);
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
    const effectiveRate = !isSupport && item.ivaTreatment === 'Gravado' ? item.taxRate : 0;
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
  // los ítems al armar el envío al integrador; acá solo se muestra el total para que cuadre con lo
  // que se emitirá.
  const generalRetentionAmount = (r: { taxCategory: string, rate: number }) =>
    r.taxCategory === 'RET_IVA' ? formData.taxAmount * r.rate / 100 : formData.subtotal * r.rate / 100;
  const generalRetentionsTotal = formData.generalRetentions.reduce((sum, r) => sum + generalRetentionAmount(r), 0);
  const totalRetentions = retentionsTotal + generalRetentionsTotal;

  // Desglose del totalizador de retenciones por cada categoría+tarifa (las por ítem se agrupan
  // entre todos los ítems que la usan, y se suman con la general si coincide categoría+tarifa).
  // El rótulo es el tipo de impuesto (Rete Fuente/ICA/IVA), no el nombre específico del concepto
  // del catálogo (ej. "Compras generales (declarantes)") — así varios conceptos con la misma
  // categoría+tarifa quedan agrupados bajo un solo rótulo reconocible.
  const discriminatedRetentions = () => {
    const map = new Map<string, { label: string, amount: number }>();
    formData.items.forEach(item => {
      (item.retentions || []).forEach((r: any) => {
        const key = `${r.taxCategory}|${r.rate}`;
        const prev = map.get(key)?.amount || 0;
        const label = `${retentionCategoryLabel(r.taxCategory)} ${r.rate}%`;
        map.set(key, { label, amount: prev + itemRetentionEntryAmount(item, r) });
      });
    });
    formData.generalRetentions.forEach(r => {
      const key = `${r.taxCategory}|${r.rate}`;
      const prev = map.get(key)?.amount || 0;
      const label = `${retentionCategoryLabel(r.taxCategory)} ${r.rate}%`;
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

  const handleCustomerSaved = (customer: any) => {
    setCustomers(prev => [...prev, customer]);
    setFormData(f => ({ ...f, customerId: customer.id }));
    setShowCustomerModal(false);
  };

  const handleSaveQuickProduct = async (e: React.FormEvent) => {
    e.preventDefault();
    if (productModalForItemIndex === null) return;
    setSavingProduct(true);
    try {
      const quickProductPayload = isSupport
        ? { ...quickProduct, scope: 'Support', ivaTreatment: 'Exento', ivaRate: 0, taxes: [] }
        : { ...quickProduct, scope: 'Invoice' };
      const res = await api.post('/client/products', quickProductPayload);
      const p = res.data;
      toast.success('Producto creado');
      setProducts(prev => [...prev, p]);

      const rate = isSupport ? 0 : (p.ivaTreatment === 'Gravado' ? (p.ivaRate ?? 0) : 0);
      const newItems = [...formData.items];
      const item = { ...newItems[productModalForItemIndex], productId: p.id, code: p.code, name: p.name, unitPrice: p.unitPrice, ivaTreatment: isSupport ? 'Exento' : (p.ivaTreatment || 'Gravado'), taxRate: rate };
      recalcItem(item);
      newItems[productModalForItemIndex] = item;
      recalculateTotals(newItems);

      setProductModalForItemIndex(null);
      setQuickProduct({ ...initialQuickProduct, ivaTreatment: isSupport ? 'Exento' : 'Gravado', ivaRate: isSupport ? 0 : 19 });
    } catch (err: any) {
      toast.error(getErrorMessage(err, 'Error creando el producto'));
    } finally {
      setSavingProduct(false);
    }
  };

  const handleSaveDraft = async () => {
    if (savingDraft) return; // evita doble envío por doble clic mientras la petición está en curso
    if (!isNote && !formData.resolutionId) {
      toast.error(cfg.missingResolution);
      return;
    }
    if (cfg.documentTypes && !formData.documentTypeId) {
      toast.error('Debes seleccionar el tipo de documento');
      return;
    }
    if (!formData.issueDate) {
      toast.error('Debes indicar la fecha de emisión');
      return;
    }
    if (!formData.customerId) {
      toast.error(cfg.missingParty);
      return;
    }
    if (!formData.paymentMeansType) {
      toast.error('Debes seleccionar las condiciones de pago: contado o crédito');
      return;
    }
    if (!formData.paymentMeans) {
      toast.error('Debes seleccionar el medio de pago');
      return;
    }
    if (formData.paymentMeansType === 'CREDITO' && (formData.paymentTermDays === '' || !Number.isFinite(Number(formData.paymentTermDays)) || Number(formData.paymentTermDays) < 0)) {
      toast.error('Debes indicar el plazo de pago cuando la operación es a crédito');
      return;
    }
    if (formData.items.length === 0) {
      toast.error('Agrega al menos un ítem');
      return;
    }
    // Las notas no tienen resolución propia — el backend siempre usa la de Factura del emisor,
    // así que no hay nada que el usuario deba elegir acá.
    if (isAiu) {
      const a = formData.aiu || AIU_DEFAULT;
      if (a.baseAmount <= 0) { toast.error('Indica el valor base del contrato AIU.'); return; }
      if (a.contractObject.trim().length < 10) { toast.error('Describe el objeto del contrato (es obligatorio en la factura AIU).'); return; }
    }
    // Hora de Colombia: la fecha de emisión no puede ser anterior a hoy (de hoy en adelante).
    if (formData.issueDate && formData.issueDate < todayColombia()) {
      toast.error('La fecha de emisión no puede ser anterior a hoy.');
      return;
    }

    setSavingDraft(true);
    try {
      const { aiu: aiuParams, ...formRest } = formData;
      const payload = {
        ...formRest,
        sectorExtensionData: isAiu && aiuParams ? JSON.stringify({ aiu: aiuParams }) : '{}',
        paymentTermDays: formData.paymentTermDays === '' ? null : formData.paymentTermDays,
        // Las notas no traen resolutionId (el selector queda oculto) — "" no es un Guid? válido
        // para el backend, hay que mandar null.
        resolutionId: formData.resolutionId || null,
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
        await api.put(`${cfg.api}/${editingId}/draft`, payload);
        toast.success('Borrador actualizado');
      } else {
        await api.post(`${cfg.api}/draft`, payload);
        toast.success('Borrador guardado');
      }
      setEditingId(null);
      setView('list');
      loadData();
    } catch (err) {
      toast.error(getErrorMessage(err, `Error guardando ${cfg.singular.toLowerCase()}`));
    } finally {
      setSavingDraft(false);
    }
  };

  // Forma y medio de pago ya se piden en el formulario de la factura, así que emitir es una sola
  // acción sin pasos intermedios. Si al borrador le falta alguno (uno guardado antes de este
  // cambio, por ejemplo), se manda de vuelta a completarlo en el formulario en vez de fallar en
  // la DIAN sin explicación.
  const handlePublishInvoice = async (inv: any) => {
    if (!inv.paymentMeansType) {
      toast.error('Este documento no tiene definida la forma de pago (Contado/Crédito). Edítalo para completarla.');
      return;
    }
    if (!inv.paymentMeans) {
      toast.error('Este documento no tiene definido el medio de pago. Edítalo para completarla.');
      return;
    }
    setPublishingId(inv.id);
    const wasViewingId = viewingInvoice?.id;
    try {
      await api.post(`${cfg.api}/${inv.id}/publish`, {});
      toast.success(`${cfg.singular} emitid${isSupport ? 'o' : 'a'} correctamente`);
    } catch (err: any) {
      // El backend guarda el estado "RECHAZADA" y el motivo aunque la respuesta sea un error
      // (el rechazo del integrador no es una falla nuestra) — hay que refrescar igual, si no la
      // lista se queda mostrando el estado anterior hasta que el usuario recargue la página.
      toast.error(getErrorMessage(err, 'Error al publicar'));
    } finally {
      loadData();
      // Si se publicó desde la vista de detalle, refrescarla en vez de dejarla con el estado viejo.
      if (wasViewingId === inv.id) handleViewDetail({ id: inv.id });
      setPublishingId(null);
    }
  };

  const isNote = !!formData.referenceDocumentId;

  if (loading) return <div className="flex justify-center p-12"><Loader2 className="animate-spin w-8 h-8 text-primary" /></div>;

  if (view === 'create') {
    // Campos de cada ítem. Son funciones de render, no componentes: definir un componente dentro del
    // render lo remonta en cada tecla y el campo que se está escribiendo pierde el foco. `table` fija
    // anchos compactos para la tabla (desde lg); en la tarjeta de móvil los campos ocupan todo el ancho.
    const itemFields = (item: any, index: number, table: boolean) => ({
      product: (
        <div className="flex flex-col gap-2 lg:flex-row lg:gap-1">
          <SearchableSelect
            className="lg:flex-1"
            inputClassName="w-full p-2 border border-slate-200 rounded-lg text-sm outline-none"
            value={item.productId}
            onChange={v => updateItem(index, 'productId', v)}
            placeholder="Buscar por código o nombre..."
            options={products.map(p => ({ value: p.id, label: `${p.code} - ${p.name}`, shortLabel: p.code }))}
            createOptions={q => [
              { label: `Crear "${q}" como código`, onSelect: () => { setProductModalForItemIndex(index); setQuickProduct({ ...initialQuickProduct, code: q, ivaTreatment: isSupport ? 'Exento' : 'Gravado', ivaRate: isSupport ? 0 : 19 }); } },
              { label: `Crear "${q}" como nombre`, onSelect: () => { setProductModalForItemIndex(index); setQuickProduct({ ...initialQuickProduct, name: q, ivaTreatment: isSupport ? 'Exento' : 'Gravado', ivaRate: isSupport ? 0 : 19 }); } },
            ]}
          />
          <input
            type="text"
            placeholder={item.productId ? 'Descripción de esta línea' : 'Nombre o descripción...'}
            value={item.name}
            onChange={e => updateItem(index, 'name', e.target.value)}
            title={item.productId ? 'Solo afecta esta línea, no modifica el producto del catálogo' : undefined}
            className="w-full p-2 border border-slate-200 rounded-lg text-sm outline-none lg:w-auto lg:flex-1"
          />
        </div>
      ),
      quantity: (
        <DecimalInput
          value={item.quantity}
          onValueChange={v => updateItem(index, 'quantity', v)}
          className={`${table ? 'w-20' : 'w-full'} p-2 border border-slate-200 rounded-lg text-sm outline-none text-center`}
        />
      ),
      unitPrice: (
        <DecimalInput
          value={item.unitPrice}
          onValueChange={v => updateItem(index, 'unitPrice', v)}
          className={`${table ? 'w-32' : 'w-full'} p-2 border border-slate-200 rounded-lg text-sm outline-none font-mono text-right`}
        />
      ),
      discount: (
        <DecimalInput maxDecimals={2} value={item.discountRate || 0} onValueChange={v => updateItem(index, 'discountRate', Math.min(v, 100))} className={`${table ? 'w-20' : 'w-full'} p-2 border border-slate-200 rounded-lg text-sm outline-none font-mono`} />
      ),
      iva: isSupport ? (
        <div className="w-full p-2 text-sm text-slate-500 bg-slate-50 border border-slate-200 rounded-lg">
          No aplica IVA
        </div>
      ) : item.productId ? (
        <div className="w-full p-2 text-sm text-slate-600 bg-slate-50 border border-slate-200 rounded-lg" title="El IVA lo define el producto del catálogo">
          {item.ivaTreatment === 'Gravado' ? `Gravado ${item.taxRate}%` : IVA_TREATMENTS.find(t => t.value === item.ivaTreatment)?.label || item.ivaTreatment}
        </div>
      ) : (
        <div className="flex gap-1">
          <SearchableSelect
            className={table ? 'w-32' : 'flex-1'}
            inputClassName="w-full p-2 border border-slate-200 rounded-lg text-sm outline-none"
            value={item.ivaTreatment}
            onChange={v => updateItem(index, 'ivaTreatment', v)}
            placeholder="IVA..."
            options={IVA_TREATMENTS}
          />
          {item.ivaTreatment === 'Gravado' && (
            <SearchableSelect
              className={table ? 'w-24' : 'w-28'}
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
      ),
      retention: expandedRetentions[index] ? (
        <div className="flex items-center gap-1">
          <SearchableSelect
            className="flex-1"
            inputClassName="w-full p-2 border border-slate-200 rounded-lg text-sm outline-none bg-white"
            value={item.retentions?.[0]?.conceptId || ''}
            onChange={v => setItemRetention(index, v)}
            placeholder="Buscar retención..."
            options={autoRetentionCatalog.map((c: any) => ({ value: c.id, label: `${c.name} (${c.taxCategory} ${c.rate}%)`, shortLabel: `${c.rate}%` }))}
          />
          <button type="button" onClick={() => clearItemRetention(index)} title="Quitar retención" aria-label="Quitar retención" className="flex h-11 w-11 shrink-0 items-center justify-center text-rose-400 hover:text-rose-600 hover:bg-rose-50 rounded-lg transition-colors lg:h-9 lg:w-9">
            <X size={16} />
          </button>
        </div>
      ) : (
        <button type="button" onClick={() => toggleItemRetentions(index)} title="Retención de esta línea" className={`w-full flex min-h-11 items-center gap-1 p-2 rounded-lg text-sm transition-colors border lg:min-h-0 ${item.retentions?.[0] ? 'text-amber-700 bg-amber-50 border-amber-200 hover:bg-amber-100' : 'text-slate-400 border-slate-200 hover:bg-slate-100'}`}>
          <Percent size={14} />
          <span className="truncate">{item.retentions?.[0] ? `${item.retentions[0].rate}%` : 'Ninguna'}</span>
        </button>
      ),
      remove: (
        <button type="button" onClick={() => removeItem(index)} title="Quitar ítem" aria-label={`Quitar ítem ${index + 1}`} className="flex h-11 w-11 items-center justify-center text-rose-400 hover:text-rose-600 hover:bg-rose-50 rounded-lg transition-colors lg:h-9 lg:w-9">
          <Trash2 size={18} />
        </button>
      ),
    });

    return (
      <div className="p-4 sm:p-6 lg:p-8 animate-in fade-in slide-in-from-bottom-4 duration-300">
        <button onClick={() => { setEditingId(null); setView('list'); }} className="flex items-center gap-2 py-2 text-slate-500 hover:text-slate-800 mb-4 sm:mb-6 font-medium transition-colors">
          <ArrowLeft size={20} /> {cfg.backToList}
        </button>

        <div className="bg-white rounded-3xl shadow-xl overflow-hidden border border-slate-100">
          <div className="p-4 sm:p-6 lg:p-8 border-b border-slate-100 bg-slate-50/50">
            <h2 className="text-2xl font-extrabold text-slate-800">{editingId ? `Editar ${cfg.singular}` : `${isSupport ? 'Nuevo' : 'Nueva'} ${cfg.singular}`}</h2>
            <p className="text-slate-500 mt-1">Completa los datos del documento</p>
          </div>
          
          <div className="p-4 sm:p-6 lg:p-8">
            <div className={`grid grid-cols-1 ${isNote ? '' : 'md:grid-cols-2'} gap-6 mb-8`}>
              {cfg.documentTypes && (
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-2">Tipo de Documento</label>
                <SearchableSelect
                  value={formData.documentTypeId}
                  onChange={handleDocumentTypeChange}
                  placeholder="Buscar tipo de documento..."
                  options={documentTypes.map(d => ({ value: d.id, label: d.name }))}
                />
              </div>
              )}
              {/* Las Notas Crédito/Débito no tienen resolución autorizada propia ante la DIAN —
                  siempre reutilizan automáticamente la de Factura del emisor (ver
                  InvoiceController.Publish), así que este selector no aplica ni hace falta. */}
              {!isNote && (
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-2">Resolución</label>
                <SearchableSelect
                  value={formData.resolutionId}
                  onChange={v => setFormData({ ...formData, resolutionId: v })}
                  placeholder="Buscar resolución por prefijo o número..."
                  options={resolutions.map(r => ({
                    value: r.id,
                    label: `${r.documentType === 'POS' ? 'DS' : r.documentType} · Prefijo ${r.prefix} · Res. ${r.resolutionNumber} (vence ${new Date(r.validTo).toLocaleDateString('es-CO')})`
                  }))}
                />
                {resolutions.length === 0 && (
                  <p className="text-xs text-amber-600 mt-1">No hay resoluciones activas. Configúralas en Ajustes &gt; Resoluciones.</p>
                )}
              </div>
              )}
              <div className={isNote ? '' : 'md:col-span-2'}>
                <label className="block text-sm font-bold text-slate-700 mb-2">{cfg.party}</label>
                <div className="flex gap-2">
                  <SearchableSelect
                    className="flex-1"
                    value={formData.customerId}
                    onChange={v => setFormData({...formData, customerId: v})}
                    placeholder={cfg.partyPlaceholder}
                    options={customers.map(c => ({ value: c.id, label: `${c.name} (${c.identificationNumber})` }))}
                  />
                  <button type="button" onClick={() => setShowCustomerModal(true)} title={`Crear ${cfg.party.toLowerCase()} rápido`} className="p-3 bg-slate-100 hover:bg-slate-200 text-slate-600 rounded-xl transition-colors">
                    <UserPlus size={20} />
                  </button>
                </div>
              </div>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-6 mb-8">
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-2">Fecha de emisión</label>
                <input
                  type="date"
                  min={todayColombia()}
                  value={formData.issueDate}
                  onChange={e => setFormData({ ...formData, issueDate: e.target.value })}
                  className="w-full px-4 py-2 text-sm bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-primary outline-none"
                />
              </div>
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-2">Condiciones de pago</label>
                <SearchableSelect
                  value={formData.paymentMeansType}
                  onChange={v => {
                    setFormData({
                      ...formData,
                      paymentMeansType: v,
                      paymentTermDays: v === 'CREDITO' ? formData.paymentTermDays : '',
                    });
                    if (v !== 'CREDITO') setPaymentTermCustom(false);
                  }}
                  placeholder="Contado o crédito..."
                  options={formaPagoCatalog.map(c => ({ value: c.category, label: c.name }))}
                />
              </div>
              {formData.paymentMeansType === 'CREDITO' && (
                <div>
                  <div className="flex items-center justify-between mb-2">
                    <label className="block text-sm font-bold text-slate-700">Plazo de Pago</label>
                    {formData.paymentTermDays !== '' && formData.issueDate && (
                      <span className="text-xs text-slate-500">
                        Vence: <span className="font-bold text-slate-700">
                          {new Date(new Date(formData.issueDate + 'T00:00:00').getTime() + Number(formData.paymentTermDays) * 86400000).toLocaleDateString('es-CO')}
                        </span>
                      </span>
                    )}
                  </div>
                  <div className="flex gap-2">
                    <div className={paymentTermCustom ? 'flex-1' : 'w-full'}>
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
                    </div>
                    {paymentTermCustom && (
                      <input
                        type="number" min="0" placeholder="Días"
                        value={formData.paymentTermDays === '' ? '' : formData.paymentTermDays}
                        onChange={e => setFormData({ ...formData, paymentTermDays: e.target.value === '' ? '' : parseInt(e.target.value) })}
                        className="w-32 px-4 py-2 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-primary outline-none"
                      />
                    )}
                  </div>
                </div>
              )}
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-2">Medio de Pago</label>
                <SearchableSelect
                  value={formData.paymentMeans}
                  onChange={v => setFormData({ ...formData, paymentMeans: v })}
                  placeholder="Buscar medio de pago..."
                  options={[...paymentMeansCatalog].sort((a, b) => a.name.localeCompare(b.name, 'es')).map(c => ({ value: c.category, label: c.name }))}
                />
              </div>
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-2">Orden de Compra</label>
                <input type="text" placeholder="Opcional" value={formData.purchaseOrderReference} onChange={e => setFormData({ ...formData, purchaseOrderReference: e.target.value })} className="w-full px-4 py-2 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-primary outline-none" />
              </div>
            </div>

            {formData.referenceDocumentId && isSupport && (
              <div className="mb-8 p-4 sm:p-6 bg-amber-50 border border-amber-200 rounded-xl">
                <h3 className="text-amber-800 font-bold mb-2 flex items-center gap-2">
                  <RotateCcw size={18} />
                  Generando Nota de Ajuste
                </h3>
                <p className="text-sm text-amber-700 mb-4">
                  Esta nota afectará al documento soporte seleccionado. Ajusta las líneas o los valores que cambian e indica el concepto del ajuste.
                </p>
                <div>
                  <label className="block text-sm font-bold text-amber-800 mb-2">Concepto del Ajuste</label>
                  <input
                    type="text"
                    value={formData.referenceConcept}
                    onChange={e => setFormData({ ...formData, referenceConcept: e.target.value })}
                    className="w-full px-4 py-2 bg-white border border-amber-200 rounded-xl focus:ring-2 focus:ring-amber-500 outline-none"
                  />
                </div>
              </div>
            )}

            {formData.referenceDocumentId && !isSupport && (() => {
              const isDebitNote = formData.documentTypeId === documentTypes.find(d => d.code === '92')?.id;
              return (
                <div className="mb-8 p-4 sm:p-6 bg-amber-50 border border-amber-200 rounded-xl">
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
                    <label className="block text-sm font-bold text-amber-800 mb-2">Motivo de la Nota</label>
                    <SearchableSelect
                      value={formData.discrepancyResponseCode}
                      onChange={v => {
                        const catalog = isDebitNote ? debitNoteReasonCatalog : creditNoteReasonCatalog;
                        const selected = catalog.find(c => c.category === v);
                        setFormData({...formData, discrepancyResponseCode: v, referenceConcept: selected?.name || formData.referenceConcept});
                      }}
                      placeholder="Buscar motivo..."
                      inputClassName="w-full px-4 py-2 pr-8 bg-white border border-amber-200 rounded-xl focus:ring-2 focus:ring-amber-500 outline-none"
                      options={(isDebitNote ? debitNoteReasonCatalog : creditNoteReasonCatalog).map(c => ({ value: c.category, label: c.name }))}
                    />
                  </div>
                </div>
              );
            })()}

            {isAiu && formData.aiu && (
              <div className="mb-8">
                <h3 className="text-lg font-bold text-slate-800 mb-1">Contrato AIU</h3>
                <p className="text-sm text-slate-500 mb-4">Administración, Imprevistos y Utilidad. El IVA se calcula solo sobre la Utilidad.</p>
                <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-5 gap-4 mb-4">
                  <div className="sm:col-span-2">
                    <label className="block text-xs font-bold text-slate-500 mb-1">Valor base del contrato ($)</label>
                    <DecimalInput value={formData.aiu.baseAmount} onValueChange={v => applyAiu({ ...formData.aiu!, baseAmount: v })} placeholder="0" blankWhenZero className="w-full p-2 border border-slate-200 rounded-lg text-sm outline-none font-mono text-right" />
                  </div>
                  <div>
                    <label className="block text-xs font-bold text-slate-500 mb-1">Administración %</label>
                    <DecimalInput value={formData.aiu.adminPercent} onValueChange={v => applyAiu({ ...formData.aiu!, adminPercent: Math.min(v, 100) })} className="w-full p-2 border border-slate-200 rounded-lg text-sm outline-none font-mono text-right" />
                  </div>
                  <div>
                    <label className="block text-xs font-bold text-slate-500 mb-1">Imprevistos %</label>
                    <DecimalInput value={formData.aiu.unforeseenPercent} onValueChange={v => applyAiu({ ...formData.aiu!, unforeseenPercent: Math.min(v, 100) })} className="w-full p-2 border border-slate-200 rounded-lg text-sm outline-none font-mono text-right" />
                  </div>
                  <div>
                    <label className="block text-xs font-bold text-slate-500 mb-1">Utilidad %</label>
                    <DecimalInput value={formData.aiu.profitPercent} onValueChange={v => applyAiu({ ...formData.aiu!, profitPercent: Math.min(v, 100) })} className="w-full p-2 border border-slate-200 rounded-lg text-sm outline-none font-mono text-right" />
                  </div>
                </div>
                <div className="grid grid-cols-1 md:grid-cols-4 gap-4 mb-4">
                  <div>
                    <label className="block text-xs font-bold text-slate-500 mb-1">IVA sobre la Utilidad %</label>
                    <DecimalInput value={formData.aiu.ivaRate} onValueChange={v => applyAiu({ ...formData.aiu!, ivaRate: Math.min(v, 100) })} className="w-full p-2 border border-slate-200 rounded-lg text-sm outline-none font-mono text-right" />
                  </div>
                  <div className="md:col-span-3">
                    <label className="block text-xs font-bold text-slate-500 mb-1">Objeto del contrato</label>
                    <input type="text" placeholder="Ej. Obra civil de adecuación de bodega" value={formData.aiu.contractObject} onChange={e => setFormData({ ...formData, aiu: { ...formData.aiu!, contractObject: e.target.value } })} className="w-full p-2 border border-slate-200 rounded-lg text-sm outline-none" />
                    <p className="text-xs text-slate-400 mt-1">Va en la línea de Administración como: "{'Contrato de servicios AIU por concepto de:'} …"</p>
                  </div>
                </div>
                <ResponsiveList
                  rows={formData.items.map((item, index) => ({ item, index }))}
                  rowKey={row => String(row.index)}
                  tableFrom="md"
                  columns={[
                    { key: 'name', header: 'Concepto', primary: true, render: (row: any) => row.item.name },
                    { key: 'value', header: 'Valor', align: 'right', cellClassName: 'font-mono', render: (row: any) => `$${fmt.money(row.item.unitPrice)}` },
                    { key: 'iva', header: 'IVA', cellClassName: 'text-slate-600', render: (row: any) => (row.item.ivaTreatment === 'Gravado' ? `Gravado ${row.item.taxRate}%` : 'No gravado') },
                    { key: 'total', header: 'Total', align: 'right', cellClassName: 'font-mono font-bold', render: (row: any) => `$${fmt.money(row.item.totalAmount)}` },
                  ]}
                />
              </div>
            )}

            {!isAiu && (
            <div className="mb-8">
              <div className="flex justify-between items-end mb-4">
                <h3 className="text-lg font-bold text-slate-800">Productos o servicios</h3>
              </div>

              {/* Desde lg: tabla. Por debajo: una tarjeta editable por ítem. Las dos presentaciones usan los
                  mismos campos (ver itemFields). */}
              <div className="hidden border border-slate-200 rounded-2xl overflow-hidden lg:block">
                <table className="w-full text-left border-collapse">
                  <thead>
                    <tr className="bg-slate-50 text-xs uppercase tracking-wider text-slate-500 font-bold border-b border-slate-200">
                      <th className="p-2 pl-3 w-1/3">Producto</th>
                      <th className="p-2">Cant.</th>
                      <th className="p-2">Precio unitario</th>
                      <th className="p-2">Descuento %</th>
                      <th className="p-2">IVA</th>
                      <th className="p-2">Retención</th>
                      <th className="p-2 text-right">Total</th>
                      <th className="p-2"></th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-200">
                    {formData.items.map((item, index) => {
                      const f = itemFields(item, index, true);
                      return (
                        <tr key={index} className="bg-white">
                          <td className="p-2 pl-3">{f.product}</td>
                          <td className="p-2">{f.quantity}</td>
                          <td className="p-2">{f.unitPrice}</td>
                          <td className="p-2">{f.discount}</td>
                          <td className="p-2">{f.iva}</td>
                          <td className="p-2">{f.retention}</td>
                          <td className="p-2 pr-3 text-right font-mono font-bold text-slate-700">
                            ${fmt.money(item.totalAmount)}
                          </td>
                          <td className="p-1 text-right whitespace-nowrap">{f.remove}</td>
                        </tr>
                      );
                    })}
                    {formData.items.length === 0 && (
                      <tr>
                        <td colSpan={8} className="p-8 text-center text-slate-400">Sin ítems agregados.</td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>

              <div className="space-y-3 lg:hidden">
                {formData.items.map((item, index) => {
                  const f = itemFields(item, index, false);
                  const label = 'block text-xs font-bold text-slate-500 mb-1';
                  return (
                    <div key={index} className="space-y-3 rounded-2xl border border-slate-200 bg-white p-4">
                      <div className="flex items-center justify-between">
                        <span className="text-xs font-bold uppercase tracking-wider text-slate-400">Ítem {index + 1}</span>
                        {f.remove}
                      </div>
                      <div>
                        <span className={label}>Producto</span>
                        {f.product}
                      </div>
                      <div className="grid grid-cols-2 gap-3">
                        <div>
                          <span className={label}>Cantidad</span>
                          {f.quantity}
                        </div>
                        <div>
                          <span className={label}>Descuento %</span>
                          {f.discount}
                        </div>
                      </div>
                      <div>
                        <span className={label}>Precio unitario</span>
                        {f.unitPrice}
                      </div>
                      <div>
                        <span className={label}>IVA</span>
                        {f.iva}
                      </div>
                      <div>
                        <span className={label}>Retención</span>
                        {f.retention}
                      </div>
                      <div className="flex items-center justify-between border-t border-slate-100 pt-3">
                        <span className="text-sm font-bold text-slate-500">Total</span>
                        <span className="font-mono font-bold text-slate-700">${fmt.money(item.totalAmount)}</span>
                      </div>
                    </div>
                  );
                })}
                {formData.items.length === 0 && (
                  <p className="rounded-2xl border border-slate-200 bg-white p-8 text-center text-slate-400">Sin ítems agregados.</p>
                )}
              </div>

              <button onClick={addItem} className="mt-3 w-full justify-center px-4 py-3 text-primary font-bold flex items-center gap-2 rounded-xl border-2 border-primary/30 hover:bg-primary/5 hover:border-primary transition-all sm:w-auto sm:py-2">
                <PlusCircle size={18} /> Agregar Ítem
              </button>
            </div>
            )}

            <div className="mb-8">
              <div className="flex flex-col gap-3 mb-4 sm:flex-row sm:items-end sm:justify-between">
                <h3 className="text-lg font-bold text-slate-800">Retenciones (opcional)</h3>
                <button type="button" disabled={availableRetentionOptions(formData.generalRetentions.map(r => r.catalogId)).length === 0} onClick={addGeneralRetention} className="px-4 py-3 sm:py-2 text-primary font-bold flex items-center justify-center gap-2 rounded-xl border-2 border-primary/30 hover:bg-primary/5 hover:border-primary transition-all disabled:opacity-50 disabled:hover:bg-transparent disabled:hover:border-primary/30">
                  <PlusCircle size={18} /> Agregar Retención
                </button>
              </div>

              {/* Lista de filas (no tabla): en cada fila la retención a la izquierda y, a la derecha, el monto y quitar. */}
              <ul className="border border-slate-200 rounded-2xl overflow-hidden divide-y divide-slate-100 bg-white">
                {formData.generalRetentions.map((r, idx) => {
                  const catalogEntry = retentionCatalog.find(c => c.id === r.catalogId);
                  const isEditing = editingGeneralRetentionIdx === idx;
                  return (
                    <li key={idx} className="flex flex-wrap items-center gap-x-3 gap-y-2 p-3">
                      <div className="min-w-0 basis-full sm:basis-auto sm:flex-1">
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
                            <button type="button" onClick={() => setEditingGeneralRetentionIdx(idx)} title="Editar" aria-label="Editar retención" className="flex h-11 w-11 items-center justify-center text-slate-400 hover:text-primary hover:bg-slate-100 rounded-lg transition-colors md:h-9 md:w-9">
                              <Edit2 size={16} />
                            </button>
                          </div>
                        )}
                      </div>
                      <span className="ml-auto font-mono font-bold text-slate-700">${fmt.moneyInt(generalRetentionAmount(r))}</span>
                      <button type="button" onClick={() => removeGeneralRetention(idx)} title="Quitar retención" aria-label="Quitar retención" className="flex h-11 w-11 items-center justify-center text-rose-400 hover:text-rose-600 hover:bg-rose-50 rounded-lg transition-colors md:h-9 md:w-9">
                        <Trash2 size={18} />
                      </button>
                    </li>
                  );
                })}
                {formData.generalRetentions.length === 0 && (
                  <li className="p-8 text-center text-slate-400">No hay retenciones agregadas.</li>
                )}
              </ul>
              {retentionCatalog.length === 0 && <p className="text-xs text-amber-600 mt-2">No hay retenciones configuradas.</p>}
            </div>

            <div className="flex flex-col lg:flex-row gap-8 border-t border-slate-100 pt-8">
              <div className="flex flex-col gap-8 w-full lg:flex-1">
                <div>
                  <div className="flex justify-between items-end mb-4">
                    <h3 className="text-lg font-bold text-slate-800">Descuento general (opcional)</h3>
                  </div>
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                    <div>
                      <label className="block text-xs font-bold text-slate-500 mb-1">Motivo</label>
                      <input type="text" placeholder="Ej. Pronto pago" value={formData.generalDiscountReason} onChange={e => setFormData({ ...formData, generalDiscountReason: e.target.value })} className="w-full p-2 border border-slate-200 rounded-lg text-sm outline-none" />
                    </div>
                    <div>
                      <label className="block text-xs font-bold text-slate-500 mb-1">Valor ($)</label>
                      <DecimalInput blankWhenZero placeholder="0" value={formData.generalDiscountAmount} onValueChange={v => setFormData({ ...formData, generalDiscountAmount: v })} className="w-full p-2 border border-slate-200 rounded-lg text-sm outline-none font-mono text-right" />
                    </div>
                  </div>
                </div>

                {cfg.generalCharge && (
                <div>
                  <div className="flex justify-between items-end mb-4">
                    <h3 className="text-lg font-bold text-slate-800">Cargo general (opcional)</h3>
                  </div>
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                    <div>
                      <label className="block text-xs font-bold text-slate-500 mb-1">Motivo</label>
                      <input type="text" placeholder="Ej. Flete" value={formData.generalChargeReason} onChange={e => setFormData({ ...formData, generalChargeReason: e.target.value })} className="w-full p-2 border border-slate-200 rounded-lg text-sm outline-none" />
                    </div>
                    <div>
                      <label className="block text-xs font-bold text-slate-500 mb-1">Valor ($)</label>
                      <DecimalInput blankWhenZero placeholder="0" value={formData.generalChargeAmount} onValueChange={v => setFormData({ ...formData, generalChargeAmount: v })} className="w-full p-2 border border-slate-200 rounded-lg text-sm outline-none font-mono text-right" />
                    </div>
                  </div>
                </div>
                )}
              </div>

              <div className="w-full lg:flex-1">
                <h3 className="text-lg font-bold text-slate-800 mb-4">Observaciones (opcional)</h3>
                <input
                  type="text"
                  placeholder="Notas adicionales para este documento..."
                  value={formData.notes}
                  onChange={e => setFormData({ ...formData, notes: e.target.value })}
                  maxLength={500}
                  className="w-full p-3 border border-slate-200 rounded-lg text-sm outline-none focus:ring-2 focus:ring-primary"
                />
              </div>

              <div className="flex-1 bg-slate-50 p-4 sm:p-6 rounded-2xl border border-slate-200 h-fit">
                <div className="flex justify-between text-slate-500 mb-2">
                  <span>Subtotal:</span>
                  <span className="font-mono">${fmt.money(formData.subtotal)}</span>
                </div>
                {ivaBreakdown().map(g => (
                  g.isGravado ? (
                    <React.Fragment key={g.label}>
                      <div className="flex justify-between text-slate-500 mb-2">
                        <span>Base {g.label}:</span>
                        <span className="font-mono">${fmt.money(g.base)}</span>
                      </div>
                      <div className="flex justify-between text-slate-500 mb-2">
                        <span>{g.label}:</span>
                        <span className="font-mono">${fmt.money(g.tax)}</span>
                      </div>
                    </React.Fragment>
                  ) : (
                    <div key={g.label} className="flex justify-between text-slate-500 mb-2">
                      <span>{g.label}:</span>
                      <span className="font-mono">${fmt.money(g.base)}</span>
                    </div>
                  )
                ))}
                {discriminatedRetentions().map(r => (
                  <div key={r.label} className="flex justify-between text-rose-600 mb-2">
                    <span>{r.label}:</span>
                    <span className="font-mono">-${fmt.money(r.amount)}</span>
                  </div>
                ))}
                {formData.generalDiscountAmount > 0 && (
                  <div className="flex justify-between text-rose-600 mb-2">
                    <span>{formData.generalDiscountReason || 'Descuento general'}:</span>
                    <span className="font-mono">-${fmt.money(formData.generalDiscountAmount)}</span>
                  </div>
                )}
                {formData.generalChargeAmount > 0 && (
                  <div className="flex justify-between text-emerald-600 mb-2">
                    <span>{formData.generalChargeReason || 'Cargo general'}:</span>
                    <span className="font-mono">+${fmt.money(formData.generalChargeAmount)}</span>
                  </div>
                )}
                <div className="mb-4 pb-4 border-b border-slate-200" />
                <div className="flex justify-between font-extrabold text-xl text-slate-800">
                  <span>Total:</span>
                  <span className="font-mono">${fmt.money((formData.totalAmount - totalRetentions - formData.generalDiscountAmount + formData.generalChargeAmount))}</span>
                </div>
              </div>
            </div>
            
            <div className="mt-8 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end sm:gap-4">
              <button onClick={() => { setEditingId(null); setView('list'); }} className="px-6 py-3 font-bold text-slate-600 hover:bg-slate-100 rounded-xl transition-colors">Cancelar</button>
              <button onClick={handleSaveDraft} disabled={savingDraft} className="justify-center px-8 py-3 bg-slate-900 hover:bg-black text-white font-bold rounded-xl shadow-lg transition-all flex items-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed">
                <FileText size={18} /> {savingDraft ? 'Guardando...' : editingId ? 'Guardar Cambios' : 'Guardar Borrador'}
              </button>
            </div>

          </div>
        </div>

        <CustomerFormModal
          open={showCustomerModal}
          editingCustomer={null}
          defaultPartyType={cfg.partyType}
          onClose={() => setShowCustomerModal(false)}
          onSaved={handleCustomerSaved}
        />

        {productModalForItemIndex !== null && (
          <Modal
            open
            onOpenChange={open => { if (!open) setProductModalForItemIndex(null); }}
            title="Nuevo producto"
            size="sm"
            // Los SearchableSelect de IVA se pintan en un portal sobre <body>.
            withFloatingPickers
            footer={
              <>
                <Button type="button" variant="ghost" onClick={() => setProductModalForItemIndex(null)}>Cancelar</Button>
                <Button type="submit" form="quick-product-form" disabled={savingProduct}>{savingProduct ? 'Creando...' : 'Crear Producto'}</Button>
              </>
            }
          >
            <form id="quick-product-form" onSubmit={handleSaveQuickProduct} className="space-y-4">
              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-1">Código (SKU)</label>
                  <input type="text" required value={quickProduct.code} onChange={e => setQuickProduct({ ...quickProduct, code: e.target.value })} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none font-mono" />
                </div>
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-1">Precio Base</label>
                  <DecimalInput value={quickProduct.unitPrice} onValueChange={v => setQuickProduct({ ...quickProduct, unitPrice: v })} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none font-mono" />
                </div>
              </div>
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-1">Nombre o Descripción</label>
                <input type="text" required value={quickProduct.name} onChange={e => setQuickProduct({ ...quickProduct, name: e.target.value })} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none" />
              </div>
              {!isSupport && <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-1">Tratamiento del IVA</label>
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
              </div>}
            </form>
          </Modal>
        )}
      </div>
    );
  }

  if (view === 'detail' && viewingInvoice) {
    const inv = viewingInvoice;
    const isNote = isNoteType(inv.typeCode);
    const lineBaseOf = (item: any) => item.quantity * item.unitPrice * (1 - (item.discountRate || 0) / 100);

    const taxGroups = new Map<string, { label: string, base: number, tax: number }>();
    (inv.items || []).forEach((item: any) => {
      const key = item.ivaTreatment === 'Gravado' ? `Gravado-${item.taxRate}` : item.ivaTreatment;
      const existing = taxGroups.get(key);
      const base = lineBaseOf(item);
      if (existing) { existing.base += base; existing.tax += item.taxAmount; }
      else taxGroups.set(key, { label: item.ivaTreatment === 'Gravado' ? `IVA ${item.taxRate}%` : item.ivaTreatment, base, tax: item.taxAmount });
    });

    const retentionsTotal = invoiceRetentionsTotal(inv);

    return (
      <div className="p-4 sm:p-6 lg:p-8">
        <button onClick={() => { setViewingInvoice(null); setView('list'); }} className="flex items-center gap-2 py-2 text-slate-500 hover:text-slate-800 mb-4 sm:mb-6 font-medium transition-colors">
          <ArrowLeft size={20} /> {cfg.backToList}
        </button>

        <div className="bg-white rounded-3xl shadow-xl overflow-hidden border border-slate-100">
          <div className="p-4 sm:p-6 lg:p-8 border-b border-slate-100 bg-slate-50/50 flex justify-between items-start flex-wrap gap-4">
            <div className="min-w-0">
              <div className="flex flex-wrap items-center gap-3 mb-1">
                {isNote && (
                  <span className={`px-2 py-0.5 rounded text-xs font-bold ${NOTE_BADGES[inv.typeCode].className}`}>{NOTE_BADGES[inv.typeCode].label}</span>
                )}
                <h2 className="text-2xl font-extrabold text-slate-800 break-words">{(inv.prefix || inv.resolution?.prefix) ? `${inv.prefix || inv.resolution?.prefix} ` : ''}{inv.number || 'Borrador'}</h2>
                <span className={`px-3 py-1 rounded-full text-xs font-bold ${
                  inv.status === 'DRAFT' ? 'bg-slate-100 text-slate-600' :
                  inv.status === 'PROCESSING' ? 'bg-blue-100 text-blue-600' :
                  inv.status === 'APPROVED' ? 'bg-emerald-100 text-emerald-700' :
                  'bg-rose-100 text-rose-600'
                }`}>
                  {STATUS_LABELS[inv.status] || inv.status}
                </span>
              </div>
              <p className="text-slate-500 break-words">{new Date(inv.issueDate).toLocaleDateString('es-CO')} · {inv.customer?.name || 'Consumidor Final'}</p>
            </div>
            <div className="flex gap-2 flex-wrap">
              <button onClick={() => handlePreviewPdf(inv)} disabled={previewingId === inv.id} className="px-4 py-3 md:py-2 text-slate-600 bg-slate-100 hover:bg-slate-200 rounded-xl font-bold text-sm transition-colors flex items-center gap-2 disabled:opacity-50">
                {previewingId === inv.id ? <Loader2 size={16} className="animate-spin" /> : <Printer size={16} />} Vista Previa
              </button>
              {(inv.status === 'REJECTED' || inv.status === 'APPROVED') && inv.dianResponseMessage && (
                <button
                  onClick={() => setResponseModalInv(inv)}
                  className={`px-4 py-3 md:py-2 rounded-xl font-bold text-sm transition-colors flex items-center gap-2 ${inv.status === 'REJECTED' ? 'text-rose-700 bg-rose-50 hover:bg-rose-100' : 'text-slate-600 bg-slate-100 hover:bg-slate-200'}`}
                >
                  {inv.status === 'REJECTED' ? <AlertCircle size={16} /> : <Info size={16} />}
                  {inv.status === 'REJECTED' ? 'Motivo del rechazo' : 'Respuesta de la DIAN'}
                </button>
              )}
              {cfg.resend && inv.status === 'APPROVED' && (
                <button onClick={() => openResendModal(inv)} className="px-4 py-3 md:py-2 text-slate-600 bg-slate-100 hover:bg-slate-200 rounded-xl font-bold text-sm transition-colors flex items-center gap-2">
                  <Mail size={16} /> Reenviar
                </button>
              )}
              {(inv.status === 'DRAFT' || inv.status === 'REJECTED') && (
                <>
                  <button onClick={() => handleEditDraft(inv)} className="px-4 py-3 md:py-2 text-slate-600 bg-slate-100 hover:bg-slate-200 rounded-xl font-bold text-sm transition-colors flex items-center gap-2">
                    <Edit2 size={16} /> Editar
                  </button>
                  <button onClick={() => handlePublishInvoice(inv)} disabled={publishingId === inv.id} className="px-4 py-3 md:py-2 text-white bg-primary hover:bg-primary/90 rounded-xl font-bold text-sm shadow-sm transition-all flex items-center gap-2 disabled:opacity-50">
                    <Send size={16} /> Emitir
                  </button>
                  <button onClick={() => handleDeleteDraft(inv.id)} className="px-4 py-3 md:py-2 text-rose-600 bg-rose-50 hover:bg-rose-100 rounded-xl font-bold text-sm transition-colors flex items-center gap-2">
                    <Trash2 size={16} /> Eliminar
                  </button>
                </>
              )}
              {isSupport && inv.status === 'APPROVED' && !isNote && (
                <button onClick={() => handleCreateAdjustment(inv)} className="px-4 py-3 md:py-2 text-amber-700 bg-amber-100 hover:bg-amber-200 rounded-xl font-bold text-sm shadow-sm transition-all flex items-center gap-2">
                  <RotateCcw size={16} /> Generar Nota de Ajuste
                </button>
              )}
              {cfg.creditDebitNotes && inv.status === 'APPROVED' && !isNote && (
                <>
                  <button onClick={() => handleCreateDebitNote(inv)} className="px-4 py-3 md:py-2 text-orange-700 bg-orange-100 hover:bg-orange-200 rounded-xl font-bold text-sm shadow-sm transition-all flex items-center gap-2">
                    <RotateCcw size={16} /> Generar Nota Débito
                  </button>
                  <button onClick={() => handleCreateCreditNote(inv)} className="px-4 py-3 md:py-2 text-amber-700 bg-amber-100 hover:bg-amber-200 rounded-xl font-bold text-sm shadow-sm transition-all flex items-center gap-2">
                    <RotateCcw size={16} /> Generar Nota Crédito
                  </button>
                </>
              )}
            </div>
          </div>

          <div className="p-4 sm:p-6 lg:p-8 space-y-6 lg:space-y-8">
            <div className="grid grid-cols-2 md:grid-cols-3 gap-6 text-sm [&_p]:break-words">
              <div><p className="text-slate-400 font-bold uppercase text-xs mb-1">Identificación</p><p className="text-slate-700 font-medium">{inv.customer?.identificationNumber || '-'}</p></div>
              <div><p className="text-slate-400 font-bold uppercase text-xs mb-1">Forma de Pago</p><p className="text-slate-700 font-medium">{formaPagoCatalog.find(c => c.category === inv.paymentMeansType)?.name || '-'}</p></div>
              <div><p className="text-slate-400 font-bold uppercase text-xs mb-1">Orden de Compra</p><p className="text-slate-700 font-medium">{inv.purchaseOrderReference || '-'}</p></div>
            </div>

            <div className="grid grid-cols-2 md:grid-cols-4 gap-6 text-sm [&_p]:break-words">
              <div><p className="text-slate-400 font-bold uppercase text-xs mb-1">Subtotal</p><p className="text-slate-700 font-medium font-mono">${fmt.money((inv.subtotal || 0))}</p></div>
              <div><p className="text-slate-400 font-bold uppercase text-xs mb-1">Impuestos</p><p className="text-slate-700 font-medium font-mono">${fmt.money((inv.taxAmount || 0))}</p></div>
              <div><p className="text-slate-400 font-bold uppercase text-xs mb-1">Retenciones</p><p className="text-rose-600 font-medium font-mono">-${fmt.money(retentionsTotal)}</p></div>
              <div><p className="text-slate-400 font-bold uppercase text-xs mb-1">Total</p><p className="text-slate-800 font-bold font-mono">${fmt.money(invoiceNetTotal(inv))}</p></div>
            </div>

            {inv.notes && (
              <div className="p-4 bg-slate-50 border border-slate-200 rounded-xl">
                <p className="text-xs font-bold text-slate-500 uppercase mb-1">Observaciones</p>
                <p className="text-sm text-slate-700 whitespace-pre-wrap break-words">{inv.notes}</p>
              </div>
            )}

            {(viewingOriginal || viewingRelated.length > 0) && (
              <div className="p-4 bg-slate-50 border border-slate-200 rounded-xl">
                <p className="text-xs font-bold text-slate-500 uppercase mb-2">Documentos Relacionados</p>
                <div className="space-y-1">
                  {viewingOriginal && (
                    <button onClick={() => handleViewDetail(viewingOriginal)} className="py-1.5 text-sm text-primary hover:underline block text-left">
                      ← {isSupport ? 'Ajuste del documento' : 'Nota de la factura'} N° {viewingOriginal.number}
                    </button>
                  )}
                  {viewingRelated.map(r => (
                    <button key={r.id} onClick={() => handleViewDetail(r)} className="py-1.5 text-sm text-primary hover:underline flex flex-wrap items-center gap-2 text-left">
                      <span className={`px-1.5 py-0.5 rounded text-[10px] font-bold ${(NOTE_BADGES[r.typeCode] || NOTE_BADGES.ND).className}`}>{(NOTE_BADGES[r.typeCode] || NOTE_BADGES.ND).label}</span>
                      {r.number} · ${fmt.number(r.totalAmount, 3)} · {STATUS_LABELS[r.status] || r.status}
                    </button>
                  ))}
                </div>
              </div>
            )}

            <ResponsiveList
              rows={(inv.items || []).map((item: any, idx: number) => ({ item, idx }))}
              rowKey={row => String(row.idx)}
              tableFrom="xl"
              columns={[
                { key: 'name', header: 'Producto', primary: true, cellClassName: 'text-sm text-slate-700', render: (row: any) => row.item.name },
                { key: 'quantity', header: 'Cant.', cellClassName: 'text-sm', render: (row: any) => row.item.quantity },
                { key: 'unitPrice', header: 'Precio unitario', cellClassName: 'text-sm font-mono', render: (row: any) => `$${fmt.number(row.item.unitPrice, 3)}` },
                { key: 'discount', header: 'Descuento %', cellClassName: 'text-sm font-mono', render: (row: any) => `${row.item.discountRate || 0}%` },
                { key: 'iva', header: 'IVA', cellClassName: 'text-sm', render: (row: any) => (row.item.ivaTreatment === 'Gravado' ? `${row.item.taxRate}%` : row.item.ivaTreatment) },
                { key: 'total', header: 'Total', align: 'right', cellClassName: 'text-sm font-mono font-bold', render: (row: any) => `$${fmt.number(row.item.totalAmount, 3)}` },
              ]}
            />

            <div className="flex justify-end">
              <div className="w-full max-w-xs space-y-2 text-sm">
                {Array.from(taxGroups.values()).map(g => (
                  <React.Fragment key={g.label}>
                    <div className="flex justify-between text-slate-500"><span>Base {g.label}</span><span className="font-mono">${fmt.moneyInt(g.base)}</span></div>
                    {g.tax > 0 && <div className="flex justify-between text-slate-700"><span>{g.label}</span><span className="font-mono">${fmt.moneyInt(g.tax)}</span></div>}
                  </React.Fragment>
                ))}
                {inv.generalDiscountAmount > 0 && (
                  <div className="flex justify-between text-rose-600"><span>{inv.generalDiscountReason || 'Descuento'}</span><span className="font-mono">-${fmt.moneyInt(inv.generalDiscountAmount)}</span></div>
                )}
                {inv.generalChargeAmount > 0 && (
                  <div className="flex justify-between text-slate-700"><span>{inv.generalChargeReason || 'Cargo'}</span><span className="font-mono">${fmt.moneyInt(inv.generalChargeAmount)}</span></div>
                )}
                {retentionsTotal > 0 && (
                  <div className="flex justify-between text-rose-600"><span>Retenciones</span><span className="font-mono">-${fmt.moneyInt(retentionsTotal)}</span></div>
                )}
                <div className="flex justify-between text-lg font-bold text-slate-800 pt-2 border-t border-slate-200">
                  <span>Total</span><span className="font-mono">${fmt.money(invoiceNetTotal(inv))}</span>
                </div>
              </div>
            </div>
          </div>
        </div>

      </div>
    );
  }

  const filteredInvoices = invoices
    .filter(inv => {
      if (isSupport ? !isSupportDocument(inv.typeCode) : !isInvoiceDocument(inv.typeCode)) return false;
      if (statusFilter && inv.status !== statusFilter) return false;
      if (resolutionFilter && inv.resolution?.id !== resolutionFilter) return false;
      if (!searchTerm.trim()) return true;
      const q = searchTerm.trim().toLowerCase();
      return (inv.number || '').toLowerCase().includes(q) ||
        (inv.customer?.name || '').toLowerCase().includes(q) ||
        (inv.customer?.identificationNumber || '').toLowerCase().includes(q);
    })
    .sort((a, b) => {
      const rankDiff = (STATUS_SORT_RANK[a.status] ?? 3) - (STATUS_SORT_RANK[b.status] ?? 3);
      if (rankDiff !== 0) return rankDiff;
      return (parseInt(b.number) || 0) - (parseInt(a.number) || 0);
    });
  const totalPages = Math.max(1, Math.ceil(filteredInvoices.length / PAGE_SIZE));
  const currentPage = Math.min(page, totalPages);
  const paginatedInvoices = filteredInvoices.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE);
  // Nota Crédito resta del saldo (es una devolución/anulación parcial), Nota Débito suma igual
  // que una Factura (aumenta lo adeudado) — antes las dos sumaban en el mismo sentido que las
  // facturas, así que el total del pie de página quedaba doblado en vez de neteado.
  const invoiceSign = (inv: any) => inv.typeCode === 'NC' ? -1 : 1;
  const filteredTotal = filteredInvoices.reduce((sum, inv) => sum + invoiceSign(inv) * (inv.subtotal || 0), 0);
  const filteredTaxTotal = filteredInvoices.reduce((sum, inv) => sum + invoiceSign(inv) * (inv.taxAmount || 0), 0);
  const filteredRetentionsTotal = filteredInvoices.reduce((sum, inv) => sum + invoiceRetentionsTotal(inv), 0);
  const filteredNetTotal = filteredInvoices.reduce((sum, inv) => sum + invoiceSign(inv) * invoiceNetTotal(inv), 0);

  // Factura original (si esta fila es una nota) + notas que referencian esta fila — para el ícono
  // de documentos relacionados en la lista.
  const getRelatedDocs = (inv: any) => {
    const docs: any[] = [];
    if (inv.referenceDocument) docs.push({ ...inv.referenceDocument, relation: cfg.originalLabel });
    (inv.relatedNotes || []).forEach((r: any) => docs.push({ ...r, relation: r.typeCode === 'NC' ? 'Nota Crédito' : r.typeCode === 'ND' ? 'Nota Débito' : r.typeCode === 'DS-AJUSTE' ? 'Nota de Ajuste' : r.typeCode }));
    return docs;
  };

  const handleExport = () => exportToCsv(
    `${cfg.csvPrefix}_${customFrom}_a_${customTo}.csv`,
    filteredInvoices.map(inv => ({
      fecha: new Date(inv.issueDate).toLocaleDateString('es-CO'),
      numero: inv.number || '',
      resolucion: inv.prefix || inv.resolution?.prefix || '',
      cliente: inv.customer?.name || 'Consumidor Final',
      identificacion: inv.customer?.identificationNumber || '',
      total: inv.totalAmount || 0,
      estado: STATUS_LABELS[inv.status] || inv.status
    })),
    [
      { key: 'fecha', label: 'Fecha' },
      { key: 'numero', label: 'Número' },
      { key: 'resolucion', label: 'Resolución' },
      { key: 'cliente', label: cfg.party },
      { key: 'identificacion', label: 'Identificación' },
      { key: 'total', label: 'Total' },
      { key: 'estado', label: 'Estado' }
    ]
  );

  // Botones de icono de las acciones de fila: 44 px en móvil/tableta, compactos desde lg (donde se ve la tabla).
  const iconBtn = 'flex h-11 w-11 items-center justify-center rounded-lg transition-colors lg:h-9 lg:w-9';

  const statusBadge = (status: string) => (
    <span className={`px-3 py-1 rounded-full text-xs font-bold ${
      status === 'DRAFT' ? 'bg-slate-100 text-slate-600' :
      status === 'PROCESSING' ? 'bg-blue-100 text-blue-600' :
      status === 'APPROVED' ? 'bg-emerald-100 text-emerald-700' :
      'bg-rose-100 text-rose-600'
    }`}>
      {STATUS_LABELS[status] || status}
    </span>
  );

  const columns: ResponsiveListColumn<any>[] = [
    { key: 'date', header: 'Fecha', cellClassName: 'text-slate-500 text-sm', render: inv => new Date(inv.issueDate).toLocaleDateString('es-CO') },
    {
      key: 'number',
      header: 'Número',
      primary: true,
      cellClassName: 'font-bold text-slate-700',
      render: inv => (
        <div className="flex flex-wrap items-center gap-2">
          {isNoteType(inv.typeCode) && (
            <span className={`px-2 py-0.5 rounded text-xs font-bold ${NOTE_BADGES[inv.typeCode].className}`}>
              {NOTE_BADGES[inv.typeCode].label}
            </span>
          )}
          <span>{(inv.prefix || inv.resolution?.prefix) ? `${inv.prefix || inv.resolution?.prefix} ` : ''}{inv.number || '---'}</span>
          {getRelatedDocs(inv).length > 0 && (
            <div className="relative" data-related-popover>
              <button
                onClick={() => setRelatedOpenId(relatedOpenId === inv.id ? null : inv.id)}
                title="Documentos relacionados"
                aria-label="Documentos relacionados"
                className="flex h-11 w-11 items-center justify-center text-slate-400 hover:text-primary hover:bg-slate-100 rounded-md transition-colors lg:h-7 lg:w-7"
              >
                <Link2 size={14} />
              </button>
              {relatedOpenId === inv.id && (
                <div className="absolute z-20 top-full left-0 mt-1 w-64 max-w-[calc(100vw-3rem)] bg-white border border-slate-200 rounded-xl shadow-lg overflow-hidden">
                  <p className="px-3 py-2 text-[10px] font-bold text-slate-400 uppercase border-b border-slate-100">Documentos relacionados</p>
                  {getRelatedDocs(inv).map(doc => (
                    <button
                      key={doc.id}
                      onClick={() => { setRelatedOpenId(null); handleViewDetail(doc); }}
                      className="w-full flex min-h-11 items-center justify-between gap-2 px-3 py-2 text-left text-sm hover:bg-slate-50 transition-colors"
                    >
                      <span className="text-slate-600 font-normal">{doc.relation} · {doc.number || '---'}</span>
                      <span className="text-xs font-bold text-primary">Ver</span>
                    </button>
                  ))}
                </div>
              )}
            </div>
          )}
        </div>
      )
    },
    { key: 'party', header: cfg.party, cardFullWidth: true, cellClassName: 'text-slate-900 min-w-[180px]', render: inv => inv.customer?.name || 'Consumidor Final' },
    { key: 'subtotal', header: 'Subtotal', hideBelow: 'ultra', align: 'right', cellClassName: 'font-mono text-slate-500', render: inv => `$${fmt.money((inv.subtotal || 0))}` },
    { key: 'tax', header: 'Impuestos', hideBelow: 'ultra', align: 'right', cellClassName: 'font-mono text-slate-500', render: inv => `$${fmt.money((inv.taxAmount || 0))}` },
    { key: 'retentions', header: 'Retenciones', hideBelow: 'ultra', align: 'right', cellClassName: 'font-mono text-rose-600', render: inv => `-$${fmt.money(invoiceRetentionsTotal(inv))}` },
    { key: 'total', header: 'Total', align: 'right', cellClassName: 'font-mono font-medium', render: inv => `$${fmt.money(invoiceNetTotal(inv))}` },
    { key: 'status', header: 'Estado', render: inv => statusBadge(inv.status) }
  ];

  const parsedResponse = responseModalInv ? parseDianResponse(responseModalInv.dianResponseMessage) : null;
  const responseRejected = responseModalInv?.status === 'REJECTED';

  return (
    <div className="p-4 sm:p-6 lg:p-8">
      <div className="mb-6 flex flex-col gap-4 sm:mb-8 sm:flex-row sm:items-center sm:justify-between">
        <div className="min-w-0">
          <h1 className="text-2xl sm:text-3xl font-extrabold text-slate-800">{cfg.title}</h1>
          <p className="text-slate-500 mt-1">Gestión de documentos electrónicos</p>
        </div>
        <div className="flex flex-wrap gap-3">
          {cfg.importExcel && <ImportExcelButton endpoint="/client/invoices/import" templateEndpoint="/client/invoices/template" label="Importar Excel" onDone={loadData} />}
          <button
            onClick={handleCreateNew}
            className="bg-primary hover:bg-primary/90 text-white px-5 py-2.5 rounded-xl font-bold flex items-center gap-2 shadow-sm transition-all"
          >
            <Plus size={20} /> {isSupport ? 'Nuevo' : 'Nueva'} {cfg.singular}
          </button>
        </div>
      </div>

      {/* En móvil los filtros van de a dos por fila (menos alto antes del listado); desde sm vuelven a una fila con salto. */}
      <div className="grid grid-cols-2 gap-3 mb-4 sm:flex sm:flex-wrap sm:items-end">
        <div className="w-full sm:w-52">
          <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Resolución</label>
          <SearchableSelect
            value={resolutionFilter}
            onChange={setResolutionFilter}
            placeholder="Todas..."
            options={[
              { value: '', label: 'Todas' },
              ...resolutions.map(r => ({ value: r.id, label: `${r.prefix} - ${r.documentType}` }))
            ]}
          />
        </div>
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
            options={[
              { value: '', label: 'Todos' },
              { value: 'DRAFT', label: 'Borrador' },
              { value: 'REJECTED', label: 'Rechazada' },
              { value: 'PROCESSING', label: 'Procesando' },
              { value: 'APPROVED', label: 'Emitida' }
            ]}
          />
        </div>
        <div className="order-4 col-span-2 min-w-0 flex-1 sm:order-none sm:col-auto sm:basis-auto sm:min-w-[220px]">
          <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Buscar</label>
          <div className="relative">
            <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
            <input type="text" value={searchTerm} onChange={e => setSearchTerm(e.target.value)} placeholder={cfg.searchPlaceholder} className="w-full pl-9 pr-3 py-2.5 bg-white border border-slate-200 rounded-xl text-sm outline-none focus:ring-2 focus:ring-primary" />
          </div>
        </div>
        <button onClick={handleExport} disabled={filteredInvoices.length === 0} title="Exportar CSV" aria-label="Exportar CSV" className="order-3 flex h-11 w-11 items-center justify-center self-end bg-slate-100 sm:order-none hover:bg-slate-200 text-slate-600 rounded-xl transition-colors disabled:opacity-50">
          <Download size={16} />
        </button>
      </div>

      <ResponsiveList
        rows={paginatedInvoices}
        columns={columns}
        rowKey={inv => inv.id}
        tableFrom="wide"
        emptyMessage="No hay documentos que coincidan con el filtro."
        actions={inv => (
          <>
            {(inv.status === 'DRAFT' || inv.status === 'REJECTED') && (
              <>
                <RowIconButton action="edit" label={`Editar ${cfg.singular} ${inv.number || 'borrador'}`} onClick={() => handleEditDraft(inv)} />
                <button onClick={() => handlePublishInvoice(inv)} disabled={publishingId === inv.id} title="Emitir a la DIAN" className="min-h-11 px-3 text-white bg-primary hover:bg-primary/90 rounded-lg shadow-sm transition-all flex items-center gap-1 text-sm font-bold disabled:opacity-50 lg:min-h-9">
                  <Send size={16} /> Emitir
                </button>
                <RowIconButton action="delete" label={`Eliminar ${cfg.singular} ${inv.number || 'borrador'}`} onClick={() => handleDeleteDraft(inv.id)} />
              </>
            )}
            {(inv.status === 'REJECTED' || inv.status === 'APPROVED') && inv.dianResponseMessage && (
              <button
                onClick={() => setResponseModalInv(inv)}
                title={inv.status === 'REJECTED' ? 'Ver motivo del rechazo' : 'Ver respuesta de la DIAN'}
                aria-label={inv.status === 'REJECTED' ? 'Ver motivo del rechazo' : 'Ver respuesta de la DIAN'}
                className={`${iconBtn} ${inv.status === 'REJECTED' ? 'text-rose-600 hover:bg-rose-50' : 'text-slate-400 hover:text-primary hover:bg-slate-100'}`}
              >
                {inv.status === 'REJECTED' ? <AlertCircle size={16} /> : <Info size={16} />}
              </button>
            )}
            {cfg.resend && inv.status === 'APPROVED' && (
              <button onClick={() => openResendModal(inv)} title="Reenviar documento" aria-label="Reenviar documento" className={`${iconBtn} text-slate-400 hover:text-primary hover:bg-slate-100`}>
                <Mail size={16} />
              </button>
            )}
            <RowIconButton action="view" label={`Ver detalle de ${cfg.singular} ${inv.number || 'borrador'}`} onClick={() => handleViewDetail(inv)} />
            <button onClick={() => handlePreviewPdf(inv)} disabled={previewingId === inv.id} title="Vista previa de impresión" aria-label="Vista previa de impresión" className={`${iconBtn} text-slate-400 hover:text-primary hover:bg-slate-100 disabled:opacity-50`}>
              {previewingId === inv.id ? <Loader2 size={16} className="animate-spin" /> : <Printer size={16} />}
            </button>
          </>
        )}
      />

      {filteredInvoices.length > 0 && (
        <div className="mt-3 rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-slate-700">
          <p className="mb-2 font-bold">Total del período</p>
          <dl className="grid grid-cols-2 gap-x-4 gap-y-2 text-sm sm:grid-cols-4">
            <div><dt className="text-xs uppercase text-slate-400">Subtotal</dt><dd className="font-mono font-bold text-slate-500">${fmt.money(filteredTotal)}</dd></div>
            <div><dt className="text-xs uppercase text-slate-400">Impuestos</dt><dd className="font-mono font-bold text-slate-500">${fmt.money(filteredTaxTotal)}</dd></div>
            <div><dt className="text-xs uppercase text-slate-400">Retenciones</dt><dd className="font-mono font-bold text-rose-600">-${fmt.money(filteredRetentionsTotal)}</dd></div>
            <div><dt className="text-xs uppercase text-slate-400">Total</dt><dd className="font-mono font-bold">${fmt.money(filteredNetTotal)}</dd></div>
          </dl>
        </div>
      )}

      <div className="mt-4 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <p className="text-sm text-slate-400">{filteredInvoices.length} documento{filteredInvoices.length === 1 ? '' : 's'} en el periodo</p>
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

      {responseModalInv && (
        <Modal
          open
          onOpenChange={open => { if (!open) setResponseModalInv(null); }}
          title={
            <span className="flex items-center gap-2">
              {responseRejected ? <AlertCircle size={18} className="shrink-0 text-rose-600" /> : <Info size={18} className="shrink-0 text-primary" />}
              {responseRejected ? 'Motivo del rechazo' : 'Respuesta de la DIAN'}
            </span>
          }
          description={`${cfg.singular} ${responseModalInv.number || 'Borrador'}`}
          size="md"
          footer={<Button type="button" onClick={() => setResponseModalInv(null)}>Cerrar</Button>}
        >
          <div className="space-y-3">
            {parsedResponse && 'errors' in parsedResponse && parsedResponse.errors ? (
              parsedResponse.errors.map((e, idx) => (
                <div key={idx} className="p-3 bg-rose-50 border border-rose-100 rounded-xl">
                  {e.path && e.path.length > 0 && (
                    <p className="text-[10px] font-bold text-rose-400 uppercase mb-1 break-words">{e.path.join(' → ')}</p>
                  )}
                  <p className="text-sm text-rose-700 break-words">{e.error}</p>
                </div>
              ))
            ) : parsedResponse && 'fields' in parsedResponse ? (
              <>
                <div className="divide-y divide-slate-100">
                  {DIAN_RESPONSE_DISPLAY_FIELDS.filter(f => parsedResponse.fields[f.key] != null && parsedResponse.fields[f.key] !== '').map(f => (
                    <div key={f.key} className="flex justify-between items-start gap-4 py-2 text-sm">
                      <span className="text-slate-400 font-medium">{f.label}</span>
                      <span className="text-slate-700 font-medium text-right break-all">
                        {f.key === 'payment_means_type'
                          ? (parsedResponse.fields[f.key] === 'CREDITO' ? 'Crédito' : 'Contado')
                          : String(parsedResponse.fields[f.key])}
                      </span>
                    </div>
                  ))}
                </div>
                {parsedResponse.fields.cufe && (
                  <div className="py-2 border-t border-slate-100">
                    <p className="text-slate-400 font-medium text-sm mb-1">CUFE</p>
                    <p className="text-xs font-mono text-slate-600 break-all">{parsedResponse.fields.cufe}</p>
                  </div>
                )}
                {(parsedResponse.fields.pdf_url || parsedResponse.fields.xml_url) && (
                  <div className="flex flex-col gap-2 pt-3 border-t border-slate-100 sm:flex-row">
                    {parsedResponse.fields.pdf_url && (
                      <a href={parsedResponse.fields.pdf_url} target="_blank" rel="noreferrer" className="flex-1 text-center px-4 py-3 sm:py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded-xl text-sm font-bold transition-colors">
                        Ver PDF
                      </a>
                    )}
                    {parsedResponse.fields.xml_url && (
                      <a href={parsedResponse.fields.xml_url} target="_blank" rel="noreferrer" className="flex-1 text-center px-4 py-3 sm:py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded-xl text-sm font-bold transition-colors">
                        Descargar XML
                      </a>
                    )}
                  </div>
                )}
                {parsedResponse.fields.qrcode && (
                  <div className="pt-3 border-t border-slate-100">
                    <p className="text-xs font-bold text-slate-400 uppercase mb-1">Código QR (contenido)</p>
                    <pre className="text-[11px] bg-slate-50 border border-slate-200 rounded-xl p-3 whitespace-pre-wrap break-all font-mono text-slate-600 max-h-32 overflow-y-auto">{parsedResponse.fields.qrcode}</pre>
                  </div>
                )}
              </>
            ) : (
              <p className="text-sm text-slate-600 whitespace-pre-wrap break-words">{responseModalInv.dianResponseMessage}</p>
            )}
          </div>
        </Modal>
      )}

      {resendModalInv && (
        <Modal
          open
          onOpenChange={open => { if (!open) setResendModalInv(null); }}
          title={<span className="flex items-center gap-2"><Mail size={18} className="shrink-0 text-primary" /> Reenviar documento</span>}
          description={`${(resendModalInv.prefix || resendModalInv.resolution?.prefix) ? `${resendModalInv.prefix || resendModalInv.resolution?.prefix} ` : ''}${resendModalInv.number || 'Borrador'}`}
          size="sm"
          footer={
            <>
              <Button type="button" variant="ghost" onClick={() => setResendModalInv(null)}>Cancelar</Button>
              <Button type="button" onClick={handleResend} disabled={resendingId === resendModalInv.id}>
                {resendingId === resendModalInv.id ? <Loader2 size={16} className="animate-spin" /> : <Send size={16} />}
                {resendingId === resendModalInv.id ? 'Enviando...' : 'Reenviar'}
              </Button>
            </>
          }
        >
          <div>
            <label className="block text-sm font-bold text-slate-700 mb-1.5">Correo de destino</label>
            <input
              type="email"
              autoFocus
              value={resendEmailDraft}
              onChange={e => setResendEmailDraft(e.target.value)}
              className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl outline-none focus:ring-2 focus:ring-primary"
              placeholder="correo@ejemplo.com"
            />
          </div>
        </Modal>
      )}

    </div>
  );
}
