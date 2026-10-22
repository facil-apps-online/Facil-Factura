import React, { useState, useEffect } from 'react';
import { Plus, Edit2, Trash2, Send, FileText, Loader2, ArrowLeft, PlusCircle, RotateCcw, X, UserPlus, Percent, Search, Download, Printer, Link2, Info, AlertCircle, Mail } from 'lucide-react';
import { toast } from 'sonner';
import { api, getErrorMessage } from '../lib/api';
import { useConfirm } from '@/components/ConfirmDialog';
import BulkImportModal from '../components/BulkImportModal';
import CustomerFormModal from '../components/CustomerFormModal';
import SearchableSelect from '@shared/components/SearchableSelect';
import { DATE_RANGE_PRESET_OPTIONS, getDateRangeForPreset, type DateRangePreset } from '../lib/dateRangePresets';
import { exportToCsv } from '../lib/exportCsv';
import { useNumberFormat } from '../lib/numberFormat';
import DecimalInput from '../components/DecimalInput';
import { useSession } from '../context/SessionContext';
import { todayColombia } from '../lib/colombiaTime';
import Modal from '../components/Modal';
import DataTable, { type DataTableColumn } from '@shared/components/DataTable';
import RowIconButton from '../components/RowIconButton';
import { Button } from '../components/ui/button';
import { STATUS_FILTER_OPTIONS, STATUS_LABELS } from '../lib/documentStatus';
import { BatchActionBar, BatchProgressDialog } from '../components/BatchActions';
import DianResponseDialog from '../components/DianResponseDialog';
import DataicoLookupModal from '../components/DataicoLookupModal';
import { usePdfPreview } from '../lib/pdfPreview';
import { isBatchEditable, useBatchRunner, useBatchSelection } from '../lib/batch';


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

// Contrato AIU (Administración, Imprevistos, Utilidad) — factura con tipo de operación "09". El
// formulario pide el valor del servicio y los porcentajes y genera cuatro líneas: Administración, Imprevistos, Utilidad y el propio
// servicio (que se cobra). A y I no causan IVA; la Utilidad y, si se indica, el servicio sí. El objeto del contrato va en la nota
// obligatoria de la línea de Administración (y, con Dataico, en la del servicio).
// Se guarda como JSON en sectorExtensionData ({ aiu: {...} }), el backend lo lee del documento.
interface AiuParams {
  baseAmount: number;
  adminPercent: number;
  unforeseenPercent: number;
  profitPercent: number;
  ivaRate: number;
  // IVA de la línea del servicio (0 = sin IVA).
  serviceIvaRate: number;
  contractObject: string;
}
const AIU_DEFAULT: AiuParams = { baseAmount: 0, adminPercent: 10, unforeseenPercent: 5, profitPercent: 5, ivaRate: 19, serviceIvaRate: 0, contractObject: '' };
const AIU_OPERATION_TYPE = '09';
const MANDATE_OPERATION_TYPE = '11';
const OBSERVATION_MAX_LENGTH = 500;
const round2 = (n: number) => Math.round((n + Number.EPSILON) * 100) / 100;

const buildAiuItems = (aiu: AiuParams) => {
  const line = (code: string, name: string, unitPrice: number, ivaRate: number) => {
    const taxAmount = round2(unitPrice * ivaRate / 100);
    return {
      productId: '', code, name, quantity: 1, unitPrice,
      unitOfMeasureCode: '94', unitOfMeasureAbbreviation: 'EA', unitOfMeasureDisplayFormat: 'Combined',
      ivaTreatment: ivaRate > 0 ? 'Gravado' : 'Excluido', taxRate: ivaRate, discountRate: 0,
      taxAmount, totalAmount: unitPrice + taxAmount, retentions: [] as any[]
    };
  };
  const percentOf = (percent: number) => round2(aiu.baseAmount * percent / 100);
  return [
    line('AIU-A', 'Administración (A.I.U.)', percentOf(aiu.adminPercent), 0),
    line('AIU-I', 'Imprevistos (A.I.U.)', percentOf(aiu.unforeseenPercent), 0),
    line('AIU-U', 'Utilidad (A.I.U.)', percentOf(aiu.profitPercent), aiu.ivaRate),
    line('SERVICIO', aiu.contractObject.trim() || 'Servicio contratado', aiu.baseAmount, aiu.serviceIvaRate),
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

// Precio base de la línea al cargar un producto: si el producto se digitó con IVA incluido se extrae la base
// (precio / (1 + tarifa)) con hasta 6 decimales, que es lo que admite la DIAN; el resto del cálculo no cambia.
const baseUnitPrice = (p: any, isSupport: boolean) =>
  !isSupport && p.priceIncludesIva && p.ivaTreatment === 'Gravado' && (p.ivaRate ?? 0) > 0
    ? Math.round((p.unitPrice / (1 + p.ivaRate / 100)) * 1e6) / 1e6
    : p.unitPrice;

const initialQuickProduct = {
  code: '', name: '', unitPrice: 0, unitOfMeasureId: '30000000-0000-0000-0000-000000000001', standardCode: '', ivaTreatment: 'Gravado', ivaRate: 19, priceIncludesIva: false, taxes: [] as any[]
};

export default function DocumentsPage({ mode }: { mode: DocumentMode }) {
  const cfg = MODES[mode];
  const isSupport = mode === 'support';
  const fmt = useNumberFormat();
  // Prefijo de los valores de un documento: "$" en pesos, o el código de la moneda ("USD ") si el documento está en otra.
  const symOf = (doc: { currency?: string } | null | undefined) => (doc?.currency && doc.currency !== 'COP' ? `${doc.currency} ` : '$');
  const confirm = useConfirm();
  const { features } = useSession();
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
  const [showDataicoLookup, setShowDataicoLookup] = useState(false);
  const [showBulkImport, setShowBulkImport] = useState(false);
  const [resendEmailDraft, setResendEmailDraft] = useState('');
  const [resendingId, setResendingId] = useState<string | null>(null);
  const [resolutionFilter, setResolutionFilter] = useState('');
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
  const [mandantes, setMandantes] = useState<any[]>([]);
  // Monedas extranjeras disponibles (catálogo del superadmin); solo se cargan si el tenant habilitó al cliente.
  const [currencyCatalog, setCurrencyCatalog] = useState<{ id: string, category: string, name: string }[]>([]);
  // Condiciones de entrega (Incoterms, tabla 13.3.7 de la DIAN) para la factura de exportación.
  const [incotermCatalog, setIncotermCatalog] = useState<{ id: string, category: string, name: string }[]>([]);
  // Valores por defecto de "Nueva factura" que definió el administrador (vacío = sin valor).
  const [invoiceDefaults, setInvoiceDefaults] = useState<{ documentTypeId: string | null, paymentMeans: string | null, paymentMeansType: string | null, paymentTermDays: number | null, currency: string | null, notes: string | null } | null>(null);
  // Línea para la que se está creando un mandante nuevo (mismo formulario de terceros, con el tipo Mandante).
  const [mandanteModalForItemIndex, setMandanteModalForItemIndex] = useState<number | null>(null);
  // Carga de varias observaciones de mandato de una vez (una por línea).
  const [showBulkObservations, setShowBulkObservations] = useState(false);
  const [bulkObservations, setBulkObservations] = useState('');
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
    // Moneda del documento: COP por defecto; otra moneda solo si el tenant habilitó al cliente (features.foreignCurrency).
    currency: 'COP',
    exchangeRate: '' as number | '',
    exchangeRateDate: todayIso(),
    // Factura de exportación: condición de entrega (código Incoterm, opcional).
    deliveryTermsCode: '',
    notes: '',
    // Observaciones de facturas y notas: una por fila, en orden (el documento soporte sigue usando "notes").
    observations: [] as string[],
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
    if (!relatedOpenId) return;
    const handler = (e: MouseEvent) => {
      if (!(e.target as HTMLElement).closest('[data-related-popover]')) setRelatedOpenId(null);
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [relatedOpenId]);

  useEffect(() => {
    api.get('/client/resolutions')
      .then(res => setResolutions(res.data.filter((r: any) => isSupport ? (r.documentType === 'DS' || r.documentType === 'POS') : r.documentType === cfg.resolutionType)))
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

  const loadData = async ({ quiet = false } = {}) => {
    // quiet: refresca la lista sin el spinner de página completa (no desmonta el avance de un lote en curso).
    if (!quiet) setLoading(true);
    try {
      const range = datePreset === 'custom'
        ? { from: appliedCustomFrom, to: appliedCustomTo }
        : getDateRangeForPreset(datePreset)!;
      const [invRes, custRes, prodRes, docTypesRes, mandantesRes, currencyRes, incotermRes, defaultsRes] = await Promise.all([
        api.get(`${cfg.api}?from=${range.from}&to=${range.to}`),
        // Lista liviana: el formulario solo muestra nombre e identificación; al emitir, el servidor carga el tercero completo por su id.
        api.get(`/client/customers?partyType=${cfg.partyType}&lite=true`),
        api.get(`/client/products?scope=${isSupport ? 'Support' : 'Invoice'}`),
        cfg.documentTypes ? api.get('/client/invoices/document-types') : Promise.resolve({ data: [] }),
        // Terceros marcados como mandante: los elige cada línea de una factura de mandato.
        isSupport ? Promise.resolve({ data: [] }) : api.get('/client/customers?partyType=Mandante&lite=true'),
        !isSupport && features.foreignCurrency ? api.get('/client/tax-catalog?kind=Currency') : Promise.resolve({ data: [] }),
        isSupport ? Promise.resolve({ data: [] }) : api.get('/client/tax-catalog?kind=Incoterm'),
        isSupport ? Promise.resolve({ data: null }) : api.get('/client/invoice-defaults')
      ]);
      setCurrencyCatalog(currencyRes.data);
      setIncotermCatalog(incotermRes.data);
      setInvoiceDefaults(defaultsRes.data);
      setInvoices(invRes.data);
      setCustomers(custRes.data);
      setProducts(prodRes.data);
      setDocumentTypes(docTypesRes.data);
      setMandantes(mandantesRes.data);
    } catch (err) {
      toast.error('Error cargando datos');
    }
    setLoading(false);
  };

  // Acciones masivas: emitir o eliminar varios a la vez; el servidor los procesa de a uno.
  const batchSelection = useBatchSelection();
  const batch = useBatchRunner(cfg.api, failedIds => {
    // Los que fallaron quedan seleccionados para corregirlos y reintentar.
    batchSelection.replace(failedIds);
    loadData({ quiet: true });
  });

  const handleCreateNew = () => {
    setEditingId(null);
    // Valores por defecto del cliente: solo los que siguen siendo válidos (un tipo deshabilitado o un código que salió del catálogo se ignora).
    const d = invoiceDefaults;
    const defaultType = d?.documentTypeId ? documentTypes.find(t => t.id === d.documentTypeId) : undefined;
    const typeId = (defaultType || documentTypes.find(t => t.typeCode === 'FE-STD') || documentTypes.find(t => t.code === '01'))?.id || '';
    const typeCode = documentTypes.find(t => t.id === typeId)?.typeCode;
    const paymentTypeOk = !!d?.paymentMeansType && formaPagoCatalog.some(c => c.category === d.paymentMeansType);
    const termDays = paymentTypeOk && d!.paymentMeansType === 'CREDITO' && d!.paymentTermDays != null ? d!.paymentTermDays : '';
    const currencyOk = !!d?.currency && features.foreignCurrency && (typeCode === 'FE-STD' || typeCode === 'FE-EXP')
      && currencyCatalog.some(c => c.category === d.currency);
    setPaymentTermCustom(termDays !== '' && !paymentTermCatalog.some(c => c.category === String(termDays)));
    setFormData({
      ...initialForm,
      documentTypeId: typeId,
      paymentMeansType: paymentTypeOk ? d!.paymentMeansType! : '',
      paymentTermDays: termDays,
      paymentMeans: d?.paymentMeans && paymentMeansCatalog.some(c => c.category === d.paymentMeans) ? d.paymentMeans : '',
      currency: currencyOk ? d!.currency! : 'COP',
      notes: d?.notes ?? '',
      // Preselecciona la resolución marcada como default para Factura (FE); antes siempre
      // arrancaba vacía aunque el cliente ya tuviera una definida, y quedaba editable por si
      // hay que emitir con otra.
      resolutionId: resolutions.find(r => (r.documentType === cfg.resolutionType || (isSupport && r.documentType === 'POS')) && r.isDefault)?.id || ''
    });
    setView('create');
  };

  // Nota crédito sin factura de referencia: se emite con su propio tercero e ítems (útil cuando una factura quedó aceptada por error y hay
  // que ajustarla de otra forma). No afecta a ninguna factura; el período que informa a la DIAN es el mes de la emisión.
  const creditNoteType = documentTypes.find(d => d.typeCode === 'NC');
  const handleCreateUnreferencedNote = () => {
    if (!creditNoteType) { toast.error('La nota crédito no está habilitada para tu empresa.'); return; }
    const reason = creditNoteReasonCatalog.find(c => c.category === '1');
    setEditingId(null);
    setPaymentTermCustom(false);
    setFormData({
      ...initialForm,
      documentTypeId: creditNoteType.id,
      referenceDocumentId: null,
      discrepancyResponseCode: '1',
      referenceConcept: reason?.name || 'Devolución parcial de los bienes y/o no aceptación parcial del servicio'
    });
    setView('create');
  };

  const { previewingId, preview: handlePreviewPdf } = usePdfPreview(cfg.api);

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
        currency: full.currency || 'COP',
        exchangeRate: full.exchangeRate ?? '',
        exchangeRateDate: full.exchangeRateDate ? String(full.exchangeRateDate).slice(0, 10) : todayIso(),
        deliveryTermsCode: full.deliveryTermsCode || '',
        notes: full.notes || '',
        observations: (full.observations || []).map((o: any) => o.text),
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
          brandName: i.brandName || '',
          modelName: i.modelName || '',
          tariffCode: i.tariffCode || '',
          mandanteCustomerId: i.mandanteCustomerId || null,
          mandanteIdentificationType: i.mandanteIdentificationType || null,
          mandanteIdentification: i.mandanteIdentification || null,
          mandanteName: i.mandanteName || null,
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
        // La nota hereda la moneda y la TRM de la factura que ajusta (el servidor también lo impone).
        currency: fullInvoice.currency || 'COP',
        exchangeRate: fullInvoice.exchangeRate ?? '',
        exchangeRateDate: fullInvoice.exchangeRateDate ? String(fullInvoice.exchangeRateDate).slice(0, 10) : todayIso(),
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
        // La nota hereda la moneda y la TRM de la factura que ajusta (el servidor también lo impone).
        currency: fullInvoice.currency || 'COP',
        exchangeRate: fullInvoice.exchangeRate ?? '',
        exchangeRateDate: fullInvoice.exchangeRateDate ? String(fullInvoice.exchangeRateDate).slice(0, 10) : todayIso(),
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
  // Factura de mandato (operación 11): cada línea declara su mandante.
  const isMandate = !isSupport && selectedDocType?.operationType === MANDATE_OPERATION_TYPE && !formData.referenceDocumentId;
  // Factura de exportación (tipo FE-EXP): condición de entrega y, por ítem, marca, modelo y subpartida arancelaria.
  const isExport = !isSupport && selectedDocType?.typeCode === 'FE-EXP' && !formData.referenceDocumentId;

  const setItemMandante = (index: number, customerId: string) => {
    const mandante = mandantes.find(m => m.id === customerId);
    const newItems = [...formData.items];
    newItems[index] = {
      ...newItems[index],
      mandanteCustomerId: customerId || null,
      mandanteIdentificationType: mandante?.identificationType ?? null,
      mandanteIdentification: mandante?.identificationNumber ?? null,
      mandanteName: mandante?.name ?? null
    };
    setFormData({ ...formData, items: newItems });
  };

  // Observaciones: una por fila, en el orden en que se agregan (ese orden es el que se envía y se imprime).
  const setObservation = (index: number, text: string) =>
    setFormData({ ...formData, observations: formData.observations.map((o, i) => (i === index ? text : o)) });
  const addObservation = () => setFormData({ ...formData, observations: [...formData.observations, ''] });

  // Líneas de un texto pegado o cargado de una vez: una observación por línea (Enter), sin renglones vacíos.
  const splitObservationLines = (text: string) => text.split(/\r?\n/).map(l => l.trim()).filter(Boolean);

  // Carga varias de una vez: cada línea del cuadro pasa a ser una observación, al final de la lista y en el mismo orden.
  const addBulkObservations = () => {
    const lines = splitObservationLines(bulkObservations);
    const tooLong = lines.findIndex(l => l.length > OBSERVATION_MAX_LENGTH);
    if (tooLong >= 0) {
      toast.error(`La línea ${tooLong + 1} supera los ${OBSERVATION_MAX_LENGTH} caracteres. Divídela en dos observaciones.`);
      return;
    }
    if (lines.length === 0) return;
    setFormData({ ...formData, observations: [...formData.observations.filter(o => o.trim()), ...lines] });
    setBulkObservations('');
    setShowBulkObservations(false);
    toast.success(`${lines.length} observación${lines.length === 1 ? '' : 'es'} agregada${lines.length === 1 ? '' : 's'}`);
  };

  // Si se pega en una fila un texto con varias líneas, cada línea pasa a ser su propia observación (desde esa fila).
  const handleObservationPaste = (index: number, e: React.ClipboardEvent<HTMLInputElement>) => {
    const text = e.clipboardData.getData('text');
    if (!/\r?\n/.test(text.trim())) return;
    e.preventDefault();
    const lines = splitObservationLines(text).map(l => l.slice(0, OBSERVATION_MAX_LENGTH));
    const observations = [...formData.observations];
    observations.splice(index, 1, ...(observations[index].trim() ? [observations[index], ...lines] : lines));
    setFormData({ ...formData, observations });
  };
  const removeObservation = (index: number) =>
    setFormData({ ...formData, observations: formData.observations.filter((_, i) => i !== index) });

  // Cambia algún dato del contrato AIU: se regeneran las cuatro líneas y los totales.
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
    } else if (!isSupport && next?.typeCode === 'NC' && !formData.referenceDocumentId) {
      const reason = creditNoteReasonCatalog.find(c => c.category === '1');
      setFormData({ ...formData, documentTypeId: typeId, discrepancyResponseCode: '1', referenceConcept: reason?.name || formData.referenceConcept, aiu: null });
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
        item.unitPrice = baseUnitPrice(p, isSupport);
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

  // Mandante creado desde la línea: se agrega a la lista y queda elegido en esa misma línea.
  const handleMandanteSaved = (mandante: any) => {
    const index = mandanteModalForItemIndex;
    setMandantes(prev => [...prev.filter(m => m.id !== mandante.id), mandante]);
    setFormData(f => {
      if (index === null || !f.items[index]) return f;
      const items = [...f.items];
      items[index] = {
        ...items[index],
        mandanteCustomerId: mandante.id,
        mandanteIdentificationType: mandante.identificationType ?? null,
        mandanteIdentification: mandante.identificationNumber ?? null,
        mandanteName: mandante.name ?? null
      };
      return { ...f, items };
    });
    setMandanteModalForItemIndex(null);
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
      const item = { ...newItems[productModalForItemIndex], productId: p.id, code: p.code, name: p.name, unitPrice: baseUnitPrice(p, isSupport), ivaTreatment: isSupport ? 'Exento' : (p.ivaTreatment || 'Gravado'), taxRate: rate };
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
    if (!isSupport && formData.currency !== 'COP' && !formData.referenceDocumentId) {
      if (formData.exchangeRate === '' || Number(formData.exchangeRate) <= 0) { toast.error('Indica la TRM (tasa de cambio en pesos).'); return; }
      if (!formData.exchangeRateDate) { toast.error('Indica la fecha de la TRM.'); return; }
    }
    if (isMandate && formData.items.some(i => !i.mandanteCustomerId)) {
      toast.error('En una factura de mandato cada línea debe tener su mandante.');
      return;
    }
    // Hora de Colombia: la fecha de emisión no puede ser anterior a hoy (de hoy en adelante).
    if (formData.issueDate && formData.issueDate < todayColombia()) {
      toast.error('La fecha de emisión no puede ser anterior a hoy.');
      return;
    }

    setSavingDraft(true);
    try {
      const { aiu: aiuParams, observations, ...formRest } = formData;
      const payload = {
        ...formRest,
        // Facturas y notas mandan la lista de observaciones; el documento soporte sigue con el texto "notes".
        // Moneda: en COP no se envía TRM; "" no es un decimal válido para el servidor.
        currency: isSupport ? 'COP' : (formData.currency || 'COP'),
        exchangeRate: !isSupport && formData.currency !== 'COP' && formData.exchangeRate !== '' ? formData.exchangeRate : null,
        exchangeRateDate: !isSupport && formData.currency !== 'COP' ? formData.exchangeRateDate : null,
        // Mandato: lista de observaciones. Cualquier otro documento: solo la observación general ("notes").
        ...(isMandate
          ? { notes: '', observations: observations.filter(o => o.trim()).map(text => ({ text: text.trim() })) }
          : { observations: [] as { text: string }[] }),
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

  // Nota crédito sin factura de referencia: el tipo es "Nota crédito" y no hay documento original.
  const isUnreferencedNote = !isSupport && selectedDocType?.typeCode === 'NC' && !formData.referenceDocumentId;
  const isNote = !!formData.referenceDocumentId || isUnreferencedNote;

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
      // Factura de exportación: marca, modelo y subpartida arancelaria del ítem (todos opcionales).
      exportFields: (
        <>
          <input type="text" placeholder="Marca (opcional)" maxLength={100} value={item.brandName || ''} onChange={e => updateItem(index, 'brandName', e.target.value)} className="w-full p-2 border border-slate-200 rounded-lg text-sm outline-none" />
          <input type="text" placeholder="Modelo (opcional)" maxLength={100} value={item.modelName || ''} onChange={e => updateItem(index, 'modelName', e.target.value)} className="w-full p-2 border border-slate-200 rounded-lg text-sm outline-none" />
          <input type="text" inputMode="numeric" placeholder="Subpartida arancelaria (4-10 dígitos)" maxLength={10} value={item.tariffCode || ''} onChange={e => updateItem(index, 'tariffCode', e.target.value.replace(/\D/g, ''))} className="w-full p-2 border border-slate-200 rounded-lg text-sm outline-none font-mono" />
        </>
      ),
      mandante: (
        <div className="flex gap-1">
          <SearchableSelect
            className={table ? 'w-56' : 'flex-1'}
            inputClassName={`w-full p-2 border rounded-lg text-sm outline-none ${item.mandanteCustomerId ? 'border-slate-200' : 'border-amber-300'}`}
            value={item.mandanteCustomerId || ''}
            onChange={v => setItemMandante(index, v)}
            placeholder="Buscar mandante..."
            options={mandantes.map(m => ({ value: m.id, label: `${m.name} - ${m.identificationNumber}`, shortLabel: m.name }))}
          />
          <button type="button" onClick={() => setMandanteModalForItemIndex(index)} title="Crear mandante" aria-label="Crear mandante" className="flex h-11 w-11 shrink-0 items-center justify-center bg-slate-100 hover:bg-slate-200 text-slate-600 rounded-lg transition-colors lg:h-9 lg:w-9">
            <UserPlus size={16} />
          </button>
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

            {isNote && !isSupport && (() => {
              const isDebitNote = formData.documentTypeId === documentTypes.find(d => d.code === '92')?.id;
              return (
                <div className="mb-8 p-4 sm:p-6 bg-amber-50 border border-amber-200 rounded-xl">
                  <h3 className="text-amber-800 font-bold mb-2 flex items-center gap-2">
                    <RotateCcw size={18} />
                    Generando {isDebitNote ? 'Nota Débito' : 'Nota Crédito'}
                  </h3>
                  <p className="text-sm text-amber-700 mb-4">
                    {isUnreferencedNote
                      ? 'Esta nota no afecta a ninguna factura: indica el cliente, los ítems y el motivo. El período que se informa a la DIAN es el mes de la fecha de emisión.'
                      : isDebitNote
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

            {!isSupport && ((features.foreignCurrency && (selectedDocType?.typeCode === 'FE-STD' || selectedDocType?.typeCode === 'FE-EXP')) || formData.currency !== 'COP') && (
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-6 mb-8">
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-2">Moneda</label>
                  {isNote ? (
                    <div className="w-full px-4 py-2 text-sm bg-slate-100 border border-slate-200 rounded-xl text-slate-600" title="La nota usa la moneda de la factura que ajusta">{formData.currency}</div>
                  ) : (
                    <SearchableSelect
                      value={formData.currency}
                      onChange={v => setFormData({ ...formData, currency: v || 'COP', exchangeRate: v && v !== 'COP' ? formData.exchangeRate : '' })}
                      placeholder="Moneda..."
                      options={currencyCatalog.map(c => ({ value: c.category, label: `${c.category} - ${c.name}`, shortLabel: c.category }))}
                    />
                  )}
                  {formData.currency !== 'COP' && !isNote && <p className="text-xs text-slate-400 mt-1">Los precios y totales de este documento van en {formData.currency}.</p>}
                </div>
                {formData.currency !== 'COP' && (
                  <>
                    <div>
                      <label className="block text-sm font-bold text-slate-700 mb-2">TRM (pesos por 1 {formData.currency})</label>
                      {isNote ? (
                        <div className="w-full px-4 py-2 text-sm bg-slate-100 border border-slate-200 rounded-xl text-slate-600 font-mono">{formData.exchangeRate === '' ? '' : fmt.money(Number(formData.exchangeRate))}</div>
                      ) : (
                        <DecimalInput
                          blankWhenZero
                          maxDecimals={4}
                          placeholder="Ej. 3.912,45"
                          value={formData.exchangeRate === '' ? 0 : formData.exchangeRate}
                          onValueChange={v => setFormData({ ...formData, exchangeRate: v > 0 ? v : '' })}
                          className="w-full px-4 py-2 text-sm bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-primary outline-none font-mono text-right"
                        />
                      )}
                    </div>
                    <div>
                      <label className="block text-sm font-bold text-slate-700 mb-2">Fecha de la TRM</label>
                      <input
                        type="date"
                        value={formData.exchangeRateDate}
                        disabled={isNote}
                        onChange={e => setFormData({ ...formData, exchangeRateDate: e.target.value })}
                        className="w-full px-4 py-2 text-sm bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-primary outline-none disabled:bg-slate-100"
                      />
                    </div>
                  </>
                )}
              </div>
            )}

            {isExport && (
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-6 mb-8">
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-2">Condición de entrega (Incoterm)</label>
                  <SearchableSelect
                    value={formData.deliveryTermsCode}
                    onChange={v => setFormData({ ...formData, deliveryTermsCode: v })}
                    placeholder="Opcional..."
                    options={[{ value: '', label: 'Sin condición de entrega', shortLabel: '—' }, ...incotermCatalog.map(c => ({ value: c.category, label: `${c.category} - ${c.name}`, shortLabel: c.category }))]}
                  />
                </div>
              </div>
            )}

            {isAiu && formData.aiu && (
              <div className="mb-8">
                <h3 className="text-lg font-bold text-slate-800 mb-1">Contrato AIU</h3>
                <p className="text-sm text-slate-500 mb-4">Se factura el servicio más Administración, Imprevistos y Utilidad. Administración e Imprevistos no causan IVA; la Utilidad y el servicio sí, si indicas su tarifa.</p>
                <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-5 gap-4 mb-4">
                  <div className="sm:col-span-2">
                    <label className="block text-xs font-bold text-slate-500 mb-1">Valor del servicio ($)</label>
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
                  <div>
                    <label className="block text-xs font-bold text-slate-500 mb-1">IVA sobre el servicio % (0 = sin IVA)</label>
                    <DecimalInput value={formData.aiu.serviceIvaRate} onValueChange={v => applyAiu({ ...formData.aiu!, serviceIvaRate: Math.min(v, 100) })} className="w-full p-2 border border-slate-200 rounded-lg text-sm outline-none font-mono text-right" />
                  </div>
                  <div className="md:col-span-2">
                    <label className="block text-xs font-bold text-slate-500 mb-1">Objeto del contrato</label>
                    <input type="text" placeholder="Ej. Obra civil de adecuación de bodega" value={formData.aiu.contractObject} onChange={e => applyAiu({ ...formData.aiu!, contractObject: e.target.value })} className="w-full p-2 border border-slate-200 rounded-lg text-sm outline-none" />
                    <p className="text-xs text-slate-400 mt-1">Va en la línea de Administración como: "{'Contrato de servicios AIU por concepto de:'} …"</p>
                  </div>
                </div>
                <DataTable
                  rows={formData.items.map((item, index) => ({ item, index }))}
                  rowKey={row => String(row.index)}
                  paginate={false}
                  tableFrom="md"
                  columns={[
                    { key: 'name', header: 'Concepto', primary: true, render: (row: any) => row.item.name },
                    { key: 'value', header: 'Valor', align: 'right', cellClassName: 'font-mono', render: (row: any) => `${symOf(formData)}${fmt.money(row.item.unitPrice)}` },
                    { key: 'iva', header: 'IVA', cellClassName: 'text-slate-600', render: (row: any) => (row.item.ivaTreatment === 'Gravado' ? `Gravado ${row.item.taxRate}%` : 'No gravado') },
                    { key: 'total', header: 'Total', align: 'right', cellClassName: 'font-mono font-bold', render: (row: any) => `${symOf(formData)}${fmt.money(row.item.totalAmount)}` },
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
                      {isMandate && <th className="p-2">Mandante</th>}
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
                        <React.Fragment key={index}>
                        <tr className="bg-white">
                          <td className="p-2 pl-3">{f.product}</td>
                          {isMandate && <td className="p-2">{f.mandante}</td>}
                          <td className="p-2">{f.quantity}</td>
                          <td className="p-2">{f.unitPrice}</td>
                          <td className="p-2">{f.discount}</td>
                          <td className="p-2">{f.iva}</td>
                          <td className="p-2">{f.retention}</td>
                          <td className="p-2 pr-3 text-right font-mono font-bold text-slate-700">
                            {symOf(formData)}{fmt.money(item.totalAmount)}
                          </td>
                          <td className="p-1 text-right whitespace-nowrap">{f.remove}</td>
                        </tr>
                        {isExport && (
                          <tr className="bg-white">
                            <td colSpan={8} className="px-3 pb-3 pt-0">
                              <div className="grid grid-cols-3 gap-2">{f.exportFields}</div>
                            </td>
                          </tr>
                        )}
                        </React.Fragment>
                      );
                    })}
                    {formData.items.length === 0 && (
                      <tr>
                        <td colSpan={isMandate ? 9 : 8} className="p-8 text-center text-slate-400">Sin ítems agregados.</td>
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
                      {isMandate && (
                        <div>
                          <span className={label}>Mandante</span>
                          {f.mandante}
                        </div>
                      )}
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
                      {isExport && (
                        <div>
                          <span className={label}>Marca, modelo y subpartida arancelaria</span>
                          <div className="grid grid-cols-1 gap-2">{f.exportFields}</div>
                        </div>
                      )}
                      <div className="flex items-center justify-between border-t border-slate-100 pt-3">
                        <span className="text-sm font-bold text-slate-500">Total</span>
                        <span className="font-mono font-bold text-slate-700">{symOf(formData)}{fmt.money(item.totalAmount)}</span>
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
                <div className="flex flex-wrap items-end justify-between gap-3 mb-4">
                  <h3 className="text-lg font-bold text-slate-800">Observaciones (opcional)</h3>
                  {isMandate && (
                    <div className="flex flex-wrap gap-2">
                      <button type="button" onClick={() => setShowBulkObservations(s => !s)} className="px-3 py-2 text-slate-600 font-bold flex items-center gap-2 rounded-xl border-2 border-slate-200 hover:bg-slate-50 transition-all text-sm">
                        Cargar varias
                      </button>
                      <button type="button" onClick={addObservation} className="px-3 py-2 text-primary font-bold flex items-center gap-2 rounded-xl border-2 border-primary/30 hover:bg-primary/5 hover:border-primary transition-all text-sm">
                        <PlusCircle size={16} /> Agregar observación
                      </button>
                    </div>
                  )}
                </div>
                {!isMandate ? (
                  <input
                    type="text"
                    placeholder="Notas adicionales para este documento..."
                    value={formData.notes}
                    onChange={e => setFormData({ ...formData, notes: e.target.value })}
                    maxLength={500}
                    className="w-full p-3 border border-slate-200 rounded-lg text-sm outline-none focus:ring-2 focus:ring-primary"
                  />
                ) : (
                  <>
                    {showBulkObservations && (
                      <div className="mb-3 rounded-xl border border-slate-200 bg-slate-50 p-3">
                        <label className="block text-xs font-bold text-slate-500 mb-1">Una observación por línea (separadas con Enter)</label>
                        <textarea
                          value={bulkObservations}
                          onChange={e => setBulkObservations(e.target.value)}
                          rows={5}
                          placeholder={'Canon de arrendamiento de octubre\nPago por consignación bancaria\n...'}
                          className="w-full p-3 border border-slate-200 rounded-lg text-sm outline-none focus:ring-2 focus:ring-primary bg-white"
                        />
                        <div className="mt-2 flex justify-end gap-2">
                          <button type="button" onClick={() => { setBulkObservations(''); setShowBulkObservations(false); }} className="px-3 py-2 text-sm font-bold text-slate-500 hover:bg-slate-100 rounded-lg">Cancelar</button>
                          <button type="button" onClick={addBulkObservations} disabled={!bulkObservations.trim()} className="px-3 py-2 text-sm font-bold text-white bg-primary rounded-lg disabled:opacity-50">Agregar al listado</button>
                        </div>
                      </div>
                    )}
                    {formData.observations.length === 0 ? (
                      <p className="rounded-lg border border-dashed border-slate-200 p-3 text-sm text-slate-400">Sin observaciones. Se envían en el orden en que las agregues; también puedes cargar varias de una vez.</p>
                    ) : (
                      <ol className="space-y-2">
                        {formData.observations.map((text, index) => (
                          <li key={index} className="flex items-center gap-2">
                            <span className="w-6 shrink-0 text-right text-sm font-bold text-slate-400">{index + 1}.</span>
                            <input
                              type="text"
                              value={text}
                              onChange={e => setObservation(index, e.target.value)}
                              onPaste={e => handleObservationPaste(index, e)}
                              placeholder="Observación..."
                              maxLength={OBSERVATION_MAX_LENGTH}
                              className="min-w-0 flex-1 p-3 border border-slate-200 rounded-lg text-sm outline-none focus:ring-2 focus:ring-primary"
                            />
                            <button type="button" onClick={() => removeObservation(index)} title="Quitar observación" aria-label={`Quitar observación ${index + 1}`} className="flex h-11 w-11 shrink-0 items-center justify-center text-rose-400 hover:text-rose-600 hover:bg-rose-50 rounded-lg transition-colors">
                              <X size={16} />
                            </button>
                          </li>
                        ))}
                      </ol>
                    )}
                  </>
                )}
              </div>

              <div className="flex-1 bg-slate-50 p-4 sm:p-6 rounded-2xl border border-slate-200 h-fit">
                <div className="flex justify-between text-slate-500 mb-2">
                  <span>Subtotal:</span>
                  <span className="font-mono">{symOf(formData)}{fmt.money(formData.subtotal)}</span>
                </div>
                {ivaBreakdown().map(g => (
                  g.isGravado ? (
                    <React.Fragment key={g.label}>
                      <div className="flex justify-between text-slate-500 mb-2">
                        <span>Base {g.label}:</span>
                        <span className="font-mono">{symOf(formData)}{fmt.money(g.base)}</span>
                      </div>
                      <div className="flex justify-between text-slate-500 mb-2">
                        <span>{g.label}:</span>
                        <span className="font-mono">{symOf(formData)}{fmt.money(g.tax)}</span>
                      </div>
                    </React.Fragment>
                  ) : (
                    <div key={g.label} className="flex justify-between text-slate-500 mb-2">
                      <span>{g.label}:</span>
                      <span className="font-mono">{symOf(formData)}{fmt.money(g.base)}</span>
                    </div>
                  )
                ))}
                {discriminatedRetentions().map(r => (
                  <div key={r.label} className="flex justify-between text-rose-600 mb-2">
                    <span>{r.label}:</span>
                    <span className="font-mono">-{symOf(formData)}{fmt.money(r.amount)}</span>
                  </div>
                ))}
                {formData.generalDiscountAmount > 0 && (
                  <div className="flex justify-between text-rose-600 mb-2">
                    <span>{formData.generalDiscountReason || 'Descuento general'}:</span>
                    <span className="font-mono">-{symOf(formData)}{fmt.money(formData.generalDiscountAmount)}</span>
                  </div>
                )}
                {formData.generalChargeAmount > 0 && (
                  <div className="flex justify-between text-emerald-600 mb-2">
                    <span>{formData.generalChargeReason || 'Cargo general'}:</span>
                    <span className="font-mono">+{symOf(formData)}{fmt.money(formData.generalChargeAmount)}</span>
                  </div>
                )}
                <div className="mb-4 pb-4 border-b border-slate-200" />
                <div className="flex justify-between font-extrabold text-xl text-slate-800">
                  <span>Total:</span>
                  <span className="font-mono">{symOf(formData)}{fmt.money((formData.totalAmount - totalRetentions - formData.generalDiscountAmount + formData.generalChargeAmount))}</span>
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

        <CustomerFormModal
          open={mandanteModalForItemIndex !== null}
          editingCustomer={null}
          defaultPartyType="Mandante"
          onClose={() => setMandanteModalForItemIndex(null)}
          onSaved={handleMandanteSaved}
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
                    onChange={v => setQuickProduct({ ...quickProduct, ivaTreatment: v, ivaRate: v === 'Gravado' ? 19 : 0, priceIncludesIva: v === 'Gravado' && quickProduct.priceIncludesIva })}
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
              {!isSupport && quickProduct.ivaTreatment === 'Gravado' && (
                <label className="flex items-center gap-2 text-sm font-bold text-slate-700">
                  <input type="checkbox" checked={quickProduct.priceIncludesIva} onChange={e => setQuickProduct({ ...quickProduct, priceIncludesIva: e.target.checked })} className="w-4 h-4 accent-primary" />
                  El precio incluye IVA
                </label>
              )}
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
                {inv.isAnnulled && (
                  <span className="px-3 py-1 rounded-full text-xs font-bold bg-red-600 text-white" title="Las notas crédito aceptadas cubren el total de esta factura">Anulada</span>
                )}
              </div>
              <p className="text-slate-500 break-words">{new Date(inv.issueDate).toLocaleDateString('es-CO')} · {inv.customer?.name || 'Consumidor Final'}</p>
            </div>
            <div className="flex gap-2 flex-wrap">
              <button onClick={() => handlePreviewPdf(inv.id)} disabled={previewingId === inv.id} className="px-4 py-3 md:py-2 text-slate-600 bg-slate-100 hover:bg-slate-200 rounded-xl font-bold text-sm transition-colors flex items-center gap-2 disabled:opacity-50">
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
              <div><p className="text-slate-400 font-bold uppercase text-xs mb-1">Subtotal</p><p className="text-slate-700 font-medium font-mono">{symOf(viewingInvoice)}{fmt.money((inv.subtotal || 0))}</p></div>
              <div><p className="text-slate-400 font-bold uppercase text-xs mb-1">Impuestos</p><p className="text-slate-700 font-medium font-mono">{symOf(viewingInvoice)}{fmt.money((inv.taxAmount || 0))}</p></div>
              <div><p className="text-slate-400 font-bold uppercase text-xs mb-1">Retenciones</p><p className="text-rose-600 font-medium font-mono">-{symOf(viewingInvoice)}{fmt.money(retentionsTotal)}</p></div>
              <div><p className="text-slate-400 font-bold uppercase text-xs mb-1">Total</p><p className="text-slate-800 font-bold font-mono">{symOf(viewingInvoice)}{fmt.money(invoiceNetTotal(inv))}</p></div>
            </div>

            {(inv.notes || (inv.observations || []).length > 0) && (
              <div className="p-4 bg-slate-50 border border-slate-200 rounded-xl">
                <p className="text-xs font-bold text-slate-500 uppercase mb-1">Observaciones</p>
                {(inv.observations || []).length > 0 ? (
                  <ol className="space-y-1 text-sm text-slate-700">
                    {inv.observations.map((o: any) => (
                      <li key={o.position} className="break-words"><span className="font-bold text-slate-400">{o.position}.</span> {o.text}</li>
                    ))}
                  </ol>
                ) : (
                  <p className="text-sm text-slate-700 whitespace-pre-wrap break-words">{inv.notes}</p>
                )}
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

            <DataTable
              rows={(inv.items || []).map((item: any, idx: number) => ({ item, idx }))}
              rowKey={row => String(row.idx)}
              paginate={false}
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
                  <span>Total</span><span className="font-mono">{symOf(viewingInvoice)}{fmt.money(invoiceNetTotal(inv))}</span>
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
  const selectedRows = filteredInvoices.filter(r => batchSelection.selected.has(r.id));
  const notEditableCount = selectedRows.filter(r => !isBatchEditable(r.status)).length;
  const batchRunning = batch.progress !== null && !batch.progress.finished;
  const batchLabelOf = (id: string) => {
    const r = invoices.find(x => x.id === id);
    return r ? `${r.number || 'Borrador'} · ${r.customer?.name || '-'}` : id;
  };
  const runBatch = async (kind: 'publish' | 'delete') => {
    const n = selectedRows.length;
    const what = n === 1 ? 'documento' : 'documentos';
    const confirmed = kind === 'publish'
      ? await confirm(`Se emitirán ${n} ${what} a la DIAN, de a uno y en orden. ¿Continuar?`, { title: 'Emitir en lote', confirmText: 'Emitir', destructive: false })
      : await confirm(`Se eliminarán ${n} ${what}. Esta acción no se puede deshacer. ¿Continuar?`, { title: 'Eliminar en lote', confirmText: 'Eliminar' });
    if (!confirmed) return;
    batch.run(kind, selectedRows.map(r => r.id));
  };
  const batchSelectionProps = {
    isSelected: (r: any) => batchSelection.selected.has(r.id),
    onToggle: (r: any) => batchSelection.toggle(r.id),
    onToggleMany: (rows: any[]) => batchSelection.toggleMany(rows.map(r => r.id)),
    label: (r: any) => `Seleccionar ${cfg.singular} ${r.number || 'borrador'}`,
  };
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

  // Las notas crédito aceptadas suman el total de la factura: se marca como anulada junto a su estado.
  const annulledBadge = (inv: any) => inv.isAnnulled ? (
    <span className="px-3 py-1 rounded-full text-xs font-bold bg-red-600 text-white" title="Las notas crédito aceptadas cubren el total de esta factura">Anulada</span>
  ) : null;

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

  const columns: DataTableColumn<any>[] = [
    { key: 'date', header: 'Fecha', cellClassName: 'text-slate-500 text-sm', render: inv => new Date(inv.issueDate).toLocaleDateString('es-CO'), sortValue: inv => inv.issueDate },
    {
      key: 'number',
      header: 'Número',
      primary: true,
      sortValue: inv => `${inv.prefix || inv.resolution?.prefix || ''} ${inv.number || ''}`,
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
    { key: 'party', header: cfg.party, cardFullWidth: true, cellClassName: 'text-slate-900 min-w-[180px]', render: inv => inv.customer?.name || 'Consumidor Final', sortValue: inv => inv.customer?.name || 'Consumidor Final' },
    { key: 'subtotal', header: 'Subtotal', hideBelow: 'ultra', align: 'right', cellClassName: 'font-mono text-slate-500', render: inv => `${symOf(inv)}${fmt.money((inv.subtotal || 0))}`, sortValue: inv => inv.subtotal || 0 },
    { key: 'tax', header: 'Impuestos', hideBelow: 'ultra', align: 'right', cellClassName: 'font-mono text-slate-500', render: inv => `${symOf(inv)}${fmt.money((inv.taxAmount || 0))}`, sortValue: inv => inv.taxAmount || 0 },
    { key: 'retentions', header: 'Retenciones', hideBelow: 'ultra', align: 'right', cellClassName: 'font-mono text-rose-600', render: inv => `-${symOf(inv)}${fmt.money(invoiceRetentionsTotal(inv))}`, sortValue: inv => invoiceRetentionsTotal(inv) },
    { key: 'total', header: 'Total', align: 'right', cellClassName: 'font-mono font-medium', render: inv => `${symOf(inv)}${fmt.money(invoiceNetTotal(inv))}`, sortValue: inv => invoiceNetTotal(inv) },
    { key: 'status', header: 'Estado', render: inv => <div className="flex flex-wrap items-center gap-1">{statusBadge(inv.status)}{annulledBadge(inv)}</div>, sortValue: inv => STATUS_LABELS[inv.status] || inv.status }
  ];


  return (
    <div className="p-3 sm:p-4">
      <div className="mb-6 flex flex-col gap-4 sm:mb-8 sm:flex-row sm:items-center sm:justify-between">
        <div className="min-w-0">
          <h1 className="text-2xl sm:text-3xl font-extrabold text-slate-800">{cfg.title}</h1>
          <p className="text-slate-500 mt-1">Gestión de documentos electrónicos</p>
        </div>
        <div className="flex flex-wrap gap-3">
          {features.dataico && (
            <button onClick={() => setShowDataicoLookup(true)} className="bg-white hover:bg-slate-50 text-slate-700 border border-slate-200 px-5 py-2.5 rounded-xl font-bold flex items-center gap-2 shadow-sm transition-all">
              <Search size={18} /> {isSupport ? 'Consultar documento' : 'Consultar factura'}
            </button>
          )}
          {mode === 'invoice' && creditNoteType && (
            <button onClick={handleCreateUnreferencedNote} className="bg-white hover:bg-slate-50 text-slate-700 border border-slate-200 px-5 py-2.5 rounded-xl font-bold flex items-center gap-2 shadow-sm transition-all">
              <RotateCcw size={18} /> Nota crédito sin factura
            </button>
          )}
          {cfg.importExcel && (
            <button onClick={() => setShowBulkImport(true)} className="bg-white hover:bg-slate-50 text-slate-700 border border-slate-200 px-5 py-2.5 rounded-xl font-bold flex items-center gap-2 shadow-sm transition-all">
              <FileText size={18} /> Carga masiva
            </button>
          )}
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
            options={STATUS_FILTER_OPTIONS}
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
      <BatchProgressDialog progress={batch.progress} labelOf={batchLabelOf} noun={{ singular: 'documento', plural: 'documentos' }} onClose={batch.close} />

      <DataTable
        rows={filteredInvoices}
        resetKey={`${searchTerm}|${statusFilter}|${resolutionFilter}|${datePreset}|${appliedCustomFrom}|${appliedCustomTo}`}
        selection={batchSelectionProps}
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
            <button onClick={() => handlePreviewPdf(inv.id)} disabled={previewingId === inv.id} title="Vista previa de impresión" aria-label="Vista previa de impresión" className={`${iconBtn} text-slate-400 hover:text-primary hover:bg-slate-100 disabled:opacity-50`}>
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

      <p className="mt-4 text-sm text-slate-400">{filteredInvoices.length} documento{filteredInvoices.length === 1 ? '' : 's'} en el periodo</p>

      {responseModalInv && (
        <DianResponseDialog doc={responseModalInv} label={`${cfg.singular} ${responseModalInv.number || 'Borrador'}`} onClose={() => setResponseModalInv(null)} />
      )}

      {showDataicoLookup && (isSupport
        ? <DataicoLookupModal
            endpoint="/client/support-documents"
            title="Consultar documento soporte en Dataico"
            placeholder="Ej. DS201"
            kinds={[{ value: 'document', label: 'Documento soporte' }, { value: 'adjustment', label: 'Nota de ajuste' }]}
            onClose={() => setShowDataicoLookup(false)} />
        : <DataicoLookupModal endpoint="/client/invoices" title="Consultar factura en Dataico" placeholder="Ej. FEG92010" onClose={() => setShowDataicoLookup(false)} />)}
      {showBulkImport && (
        <BulkImportModal title="Carga masiva de facturas" entityLabel="facturas" endpoint="/client/invoices/bulk" addressOptions resolutionOptions onDone={loadData} onClose={() => setShowBulkImport(false)} />
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
