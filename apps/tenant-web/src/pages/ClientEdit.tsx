import React, { useState, useEffect } from 'react';
import { useParams, useNavigate, useSearchParams } from 'react-router-dom';
import { ArrowLeft, Save, Building2, FileKey, FileSignature, ShieldAlert, Loader2, Plus, Trash2, X, Copy, Zap, Package, Pencil, PowerOff, Power, ListChecks, Check, Mail, Inbox, LayoutTemplate, ChevronRight, CheckCircle2, Star, UserRound } from 'lucide-react';
import { api, getErrorMessage } from '../lib/api';
import { toast } from 'sonner';
import SearchableSelect from '@shared/components/SearchableSelect';
import ClientFormFields from '../components/ClientFormFields';
import BranchesTab from '../components/BranchesTab';
import ClientUsersTab from '../components/ClientUsersTab';
import { MinSaludCard, IhceCard, ReceptionMailboxCard } from '../components/BranchCredentials';

// Consecutivos internos de notas: crédito y débito (facturas) y ajuste (documento soporte).
type NoteCounterType = 'credit' | 'debit' | 'adjustment';
const NOTE_COUNTER_FIELD = { credit: 'nextCreditNoteNumber', debit: 'nextDebitNoteNumber', adjustment: 'nextSupportAdjustmentNumber' } as const;
const NOTE_COUNTER_LABEL: Record<NoteCounterType, string> = { credit: 'Nota Crédito', debit: 'Nota Débito', adjustment: 'Nota de Ajuste (Doc. Soporte)' };
const NOTE_COUNTER_BADGE: Record<NoteCounterType, string> = {
  credit: 'bg-emerald-100 text-emerald-700',
  debit: 'bg-orange-100 text-orange-700',
  adjustment: 'bg-sky-100 text-sky-700'
};

const CLIENT_CERTIFICATE_FIELDS = new Set([
  'district', 'state', 'departament', 'addressCorp', 'address', 'legalNameCorp',
  'name', 'lastName', 'dnAlternativo1', 'identity', 'email', 'countryCode',
  'identityType', 'dnAlternativo2'
]);

export default function ClientEdit() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState(searchParams.get('tab') || 'info');
  const [resolutions, setResolutions] = useState<any[]>([]);
  const [branches, setBranches] = useState<{ id: string; name: string; isActive: boolean; isMain: boolean }[]>([]);
  // Sucursales elegidas en el modal; vacío al crear = las de la resolución que reemplaza o la principal.
  const [resBranchIds, setResBranchIds] = useState<string[]>([]);
  const [associates, setAssociates] = useState<{ id: string, name: string, isActive: boolean }[]>([]);
  const [showResModal, setShowResModal] = useState(false);
  const [uploadingPdf, setUploadingPdf] = useState(false);
  const [newRes, setNewRes] = useState({
    resolutionNumber: '',
    prefix: '',
    numberStart: 0,
    numberEnd: 0,
    validFrom: '',
    validTo: '',
    technicalKey: '',
    documentType: 'FE'
  });
  const [editingResolutionId, setEditingResolutionId] = useState<string | null>(null);
  const documentTypeLabels: Record<string, string> = {
    FE: 'Factura Electrónica (FE)',
    NC: 'Nota Crédito (NC)',
    ND: 'Nota Débito (ND)',
    DS: 'Documento Soporte - Adquisiciones a No Obligados (DS)',
    NE: 'Nómina Electrónica (NE)'
  };
  const [certInfo, setCertInfo] = useState<any>(null);
  const [certFile, setCertFile] = useState<File | null>(null);
  const [certPassword, setCertPassword] = useState('');
  const [uploadingCert, setUploadingCert] = useState(false);
  const [certificateEnvironment, setCertificateEnvironment] = useState<'Sandbox' | 'Production'>('Sandbox');
  const [certificateOptions, setCertificateOptions] = useState<any>(null);
  const [selectedCertificateProfile, setSelectedCertificateProfile] = useState('');
  const [certificateFormValues, setCertificateFormValues] = useState<Record<string, string>>({});
  const [acceptCertificateTerms, setAcceptCertificateTerms] = useState(false);
  const [requestingCertificate, setRequestingCertificate] = useState(false);
  const [habilitationStatus, setHabilitationStatus] = useState<any>(null);
  const [magicLink, setMagicLink] = useState('');
  const [isHabilitating, setIsHabilitating] = useState(false);
  const [testDocPreview, setTestDocPreview] = useState<any>(null);
  const [loadingPreview, setLoadingPreview] = useState(false);
  const [sendingTestDoc, setSendingTestDoc] = useState(false);
  // Documentos de prueba ya enviados en esta sesión — el trackId es lo que permite preguntarle
  // después a la DIAN el veredicto real (GetStatusZip), ver handleCheckTestDocStatus.
  const [sentTestDocs, setSentTestDocs] = useState<{ documentNumber: string, cufe: string, trackId: string, checking: boolean, outcome: any }[]>([]);
  // Para consultar documentos enviados en una sesión anterior (el trackId sale de los logs del
  // servidor si se perdió) sin tener que reenviarlos — reenviar gasta un intento real del set.
  const [manualTrackId, setManualTrackId] = useState('');
  const [checkingManualTrackId, setCheckingManualTrackId] = useState(false);
  const [manualTrackIdOutcome, setManualTrackIdOutcome] = useState<any>(null);
  const [docProvider, setDocProvider] = useState({
    documentProvider: 'Native',
    dataicoApiUser: '',
    dataicoApiPassword: '',
    dataicoAuthToken: '',
    dataicoAccountId: '',
    dataicoEnvironment: 'PRUEBAS',
    hasApiPassword: false,
    hasAuthToken: false
  });
  const [savingDocProvider, setSavingDocProvider] = useState(false);
  const [integrators, setIntegrators] = useState<{ id: string, code: string, name: string }[]>([]);
  const [prepaidPackages, setPrepaidPackages] = useState<any[]>([]);
  const [prepaidBags, setPrepaidBags] = useState<any[]>([]);
  const [showPackageModal, setShowPackageModal] = useState(false);
  const [editingPackage, setEditingPackage] = useState<any>(null);
  const [packageForm, setPackageForm] = useState({ name: '', totalPrice: 0, discountedPricePerDocument: 0, integratorId: '', isActive: true });
  const [savingPackage, setSavingPackage] = useState(false);
  const [activatingBagPackageId, setActivatingBagPackageId] = useState<string | null>(null);
  const [enabledDocTypes, setEnabledDocTypes] = useState<any[]>([]);
  const [enabledRetentions, setEnabledRetentions] = useState<any[]>([]);
  const [retentionScope, setRetentionScope] = useState<'Invoice' | 'Support'>('Invoice');
  const [savingDocTypes, setSavingDocTypes] = useState(false);
  const [savingRetentions, setSavingRetentions] = useState(false);
  const [client, setClient] = useState({
    companyName: '',
    personType: 'PJ',
    commercialName: '',
    taxId: '',
    verificationDigit: '',
    email: '',
    phone: '',
    address: '',
    city: '',
    cityCode: '' as string | null,
    organizationDepartment: '',
    organizationType: 'RM',
    legalRepresentativeFirstName: '',
    legalRepresentativeOtherNames: '',
    legalRepresentativeFirstLastName: '',
    legalRepresentativeSecondLastName: '',
    legalRepresentativeDocumentType: '',
    legalRepresentativeDocumentNumber: '',
    legalRepresentativeDocumentCountryCode: 'CO',
    legalRepresentativeEmail: '',
    legalRepresentativeRepresentationCode: '',
    legalRepresentativeOrganizationalArea: '',
    legalRepresentativeStartDate: null as string | null,
    taxRegime: '',
    economicActivity: '',
    isGranContribuyente: false,
    isAgenteRetenedorIva: false,
    isAutorretenedorRenta: false,
    decimalSeparator: '.',
    appliesRetentions: true,
    associateId: '' as string | null,
    latitude: null as number | null,
    longitude: null as number | null,
    isActive: true,
    billingFrequency: 'Monthly',
     electronicInvoiceLegend: '',
     supportDocumentLegend: ''
  });

  useEffect(() => {
    if (id) {
      api.get(`/tenant/clients/${id}`)
        .then(res => setClient(prev => ({
          ...prev,
          ...res.data,
          associateId: res.data.associateId || '',
          latitude: res.data.latitude || 4.6097, // Default a Bogotá si no tiene
          longitude: res.data.longitude || -74.0817,
          legalRepresentativeDocumentCountryCode: res.data.legalRepresentativeDocumentCountryCode || 'CO'
        })))
        .catch(() => toast.error("No se pudo cargar el cliente"))
        .finally(() => setLoading(false));
    }
    api.get('/tenant/associates')
      .then(res => setAssociates(res.data))
      .catch(() => {});
  }, [id]);

  const loadResolutions = () => {
    api.get(`/tenant/clients/${id}/branches`).then(res => setBranches(res.data)).catch(() => {});
    api.get(`/tenant/clients/${id}/resolutions`)
      .then(res => setResolutions(res.data))
      .catch(() => toast.error("Error al cargar resoluciones"));
  };

  const [noteCounters, setNoteCounters] = useState<{ nextCreditNoteNumber: number; nextDebitNoteNumber: number; nextSupportAdjustmentNumber: number } | null>(null);

  const loadNoteCounters = () => {
    api.get(`/tenant/clients/${id}/note-counters`)
      .then(res => setNoteCounters(res.data))
      .catch(() => toast.error("Error al cargar los consecutivos de notas"));
  };

  const [editingNextNumberId, setEditingNextNumberId] = useState<string | null>(null);
  const [nextNumberDraft, setNextNumberDraft] = useState('');
  const [savingNextNumber, setSavingNextNumber] = useState(false);
  const [fetchingTechnicalKeyId, setFetchingTechnicalKeyId] = useState<string | null>(null);

  const startEditNextNumber = (r: any) => {
    setEditingNextNumberId(r.id);
    setNextNumberDraft(String(r.nextNumber ?? r.numberStart));
  };

  const saveNextNumber = async (r: any) => {
    const value = parseInt(nextNumberDraft, 10);
    if (!Number.isFinite(value)) {
      toast.error('Ingresa un número válido');
      return;
    }
    setSavingNextNumber(true);
    try {
      await api.put(`/tenant/clients/${id}/resolutions/${r.id}/next-number`, { nextNumber: value });
      toast.success('Próximo consecutivo actualizado');
      setEditingNextNumberId(null);
      loadResolutions();
    } catch (err: any) {
      toast.error(err?.response?.data?.message || err?.response?.data || 'Error al actualizar el consecutivo');
    } finally {
      setSavingNextNumber(false);
    }
  };

  const [editingNoteCounterType, setEditingNoteCounterType] = useState<NoteCounterType | null>(null);
  const [noteCounterDraft, setNoteCounterDraft] = useState('');
  const [savingNoteCounter, setSavingNoteCounter] = useState(false);

  const startEditNoteCounter = (type: NoteCounterType) => {
    setEditingNoteCounterType(type);
    setNoteCounterDraft(String(noteCounters?.[NOTE_COUNTER_FIELD[type]] ?? 1));
  };

  const saveNoteCounter = async (type: NoteCounterType) => {
    const value = parseInt(noteCounterDraft, 10);
    if (!Number.isFinite(value) || value < 1) {
      toast.error('Ingresa un número válido');
      return;
    }
    setSavingNoteCounter(true);
    try {
      const payload = { [NOTE_COUNTER_FIELD[type]]: value };
      const res = await api.put(`/tenant/clients/${id}/note-counters`, payload);
      setNoteCounters(res.data);
      toast.success('Consecutivo actualizado');
      setEditingNoteCounterType(null);
    } catch (err: any) {
      toast.error(err?.response?.data?.message || err?.response?.data || 'Error al actualizar el consecutivo');
    } finally {
      setSavingNoteCounter(false);
    }
  };

  const [receptionSettings, setReceptionSettings] = useState<any>({
    receptionEmailEnabled: false, receptionEmailHost: '', receptionEmailPort: 993, receptionEmailUseSsl: true,
    receptionEmailUser: '', hasPassword: false,
    autoSendAcuseRecibo: false, autoSendReciboBien: false, autoSendAceptacion: false, autoSendReclamo: false
  });
  const [receptionPasswordDraft, setReceptionPasswordDraft] = useState('');
  const [savingReception, setSavingReception] = useState(false);
  const [testingReceptionConn, setTestingReceptionConn] = useState(false);

  const loadReceptionSettings = () => {
    api.get(`/tenant/clients/${id}/reception-settings`)
      .then(res => setReceptionSettings(res.data))
      .catch(() => toast.error('Error al cargar la configuración de recepción'));
  };

  const saveReceptionSettings = async () => {
    setSavingReception(true);
    try {
      await api.put(`/tenant/clients/${id}/reception-settings`, {
        ...receptionSettings,
        receptionEmailPassword: receptionPasswordDraft || undefined
      });
      toast.success('Configuración guardada');
      setReceptionPasswordDraft('');
      loadReceptionSettings();
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Error al guardar');
    } finally {
      setSavingReception(false);
    }
  };

  const testReceptionConnection = async () => {
    setTestingReceptionConn(true);
    try {
      const res = await api.post(`/tenant/clients/${id}/reception-settings/test-connection`);
      if (res.data.success) toast.success(res.data.message);
      else toast.error(res.data.message);
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Error al probar la conexión');
    } finally {
      setTestingReceptionConn(false);
    }
  };

  const [smtpSettings, setSmtpSettings] = useState<any>({
    smtpHost: '', smtpPort: 587, smtpUseSsl: true, smtpUser: '',
    smtpFromEmail: '', smtpFromName: '', hasPassword: false
  });
  const [smtpPasswordDraft, setSmtpPasswordDraft] = useState('');
  const [savingSmtp, setSavingSmtp] = useState(false);
  const [testingSmtpConn, setTestingSmtpConn] = useState(false);

  const loadSmtpSettings = () => {
    api.get(`/tenant/clients/${id}/smtp-settings`)
      .then(res => setSmtpSettings(res.data))
      .catch(() => toast.error('Error al cargar la configuración SMTP'));
  };

  const saveSmtpSettings = async () => {
    setSavingSmtp(true);
    try {
      await api.put(`/tenant/clients/${id}/smtp-settings`, {
        ...smtpSettings,
        smtpPassword: smtpPasswordDraft || undefined
      });
      toast.success('Configuración guardada');
      setSmtpPasswordDraft('');
      loadSmtpSettings();
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Error al guardar');
    } finally {
      setSavingSmtp(false);
    }
  };

  const testSmtpConnection = async () => {
    setTestingSmtpConn(true);
    try {
      const res = await api.post(`/tenant/clients/${id}/smtp-settings/test-connection`);
      if (res.data.success) toast.success(res.data.message);
      else toast.error(res.data.message);
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Error al probar la conexión');
    } finally {
      setTestingSmtpConn(false);
    }
  };

  const [templateSettings, setTemplateSettings] = useState<any[]>([]);
  const [selectedTemplateSetting, setSelectedTemplateSetting] = useState<any | null>(null);
  const [availableTemplates, setAvailableTemplates] = useState<any[]>([]);
  const [applyingTemplateId, setApplyingTemplateId] = useState<string | null>(null);

  const loadTemplateSettings = () => {
    api.get(`/tenant/clients/${id}/templates/settings`)
      .then(res => setTemplateSettings(res.data))
      .catch(() => toast.error('Error al cargar las plantillas del cliente'));
  };

  const handleSelectTemplateType = async (setting: any) => {
    setSelectedTemplateSetting(setting);
    try {
      const res = await api.get(`/tenant/clients/${id}/templates/available/${setting.documentTypeId}`);
      setAvailableTemplates(res.data);
    } catch {
      toast.error('Error al cargar las plantillas disponibles');
    }
  };

  const handleApplyTemplate = async (templateId: string) => {
    if (!selectedTemplateSetting) return;
    setApplyingTemplateId(templateId);
    try {
      await api.post('/tenant/templates/assign', {
        clientId: id,
        documentTypeId: selectedTemplateSetting.documentTypeId,
        selectedTemplateId: templateId
      });
      toast.success('Plantilla asignada al cliente');
      setSelectedTemplateSetting(null);
      loadTemplateSettings();
    } catch (err: any) {
      toast.error(err?.response?.data?.message || err?.response?.data || 'Error al asignar la plantilla');
    } finally {
      setApplyingTemplateId(null);
    }
  };

  const loadCertificate = () => {
    api.get(`/tenant/clients/${id}/certificate`)
      .then(res => setCertInfo(res.data))
      .catch(() => setCertInfo(null));
    api.get(`/tenant/clients/${id}/certificate/options?environment=${certificateEnvironment}`)
      .then(res => {
        setCertificateOptions(res.data);
        if (!selectedCertificateProfile && res.data.profiles?.length) {
          const firstProfile = res.data.profiles[0];
          setSelectedCertificateProfile(firstProfile.id);
          initializeCertificateForm(firstProfile);
        }
      })
      .catch(() => setCertificateOptions(null));
  };

  const initializeCertificateForm = (profile: any) => {
    const initial: Record<string, string> = {};
    for (const field of profile.fields || []) {
      const fallback = field.defaultValue?.split(';')[0]?.split('|').at(-1) || '';
      const clientValues: Record<string, string> = {
        district: client.city,
        state: client.organizationDepartment || client.city,
        departament: client.legalRepresentativeOrganizationalArea || 'FACTURACION ELECTRONICA',
        addressCorp: client.address,
        address: client.address,
        dnAlternativo1: client.taxId,
        legalNameCorp: client.companyName,
        email: client.legalRepresentativeEmail || client.email,
        countryCode: client.legalRepresentativeDocumentCountryCode || 'CO',
        identityType: client.legalRepresentativeDocumentType === 'CC' || client.legalRepresentativeDocumentType === 'IDC'
          ? 'IDC'
          : (client.legalRepresentativeDocumentType || 'IDC'),
        dnAlternativo2: client.organizationType || 'RM'
      };
      clientValues.name = [client.legalRepresentativeFirstName, client.legalRepresentativeOtherNames]
        .filter(Boolean)
        .join(' ');
      clientValues.lastName = [client.legalRepresentativeFirstLastName, client.legalRepresentativeSecondLastName].filter(Boolean).join(' ');
      clientValues.identity = client.legalRepresentativeDocumentNumber;
      clientValues.dnAlternativo2 = client.organizationType || 'RM';
      initial[field.externalName] = clientValues[field.externalName] || fallback;
    }
    // Viafirma llama "departament" al área/departamento del representante legal.
    // No confundirlo con "state", que es el departamento geográfico de la dirección.
    if (!initial.departament) {
      initial.departament = client.legalRepresentativeOrganizationalArea || 'FACTURACION ELECTRONICA';
    }
    setCertificateFormValues(initial);
  };

  const handleCreateCertificateRequest = async () => {
    if (!selectedCertificateProfile || !acceptCertificateTerms) {
      toast.error('Selecciona un perfil y acepta los términos del certificado');
      return;
    }
    const profile = certificateOptions?.profiles?.find((item: any) => item.id === selectedCertificateProfile);
    const missingFields = (profile?.fields || [])
      .filter((field: any) => field.isRequired && !String(certificateFormValues[field.externalName] || '').trim())
      .map((field: any) => field.label);
    if (profile?.externalType === 'INDIVIDUAL' && !String(certificateFormValues.departament || '').trim()) {
      missingFields.push('Departamento');
    }
    if (missingFields.length > 0) {
      toast.error(`Completa: ${missingFields.join(', ')}`);
      return;
    }
    setRequestingCertificate(true);
    try {
      const response = await api.post(`/tenant/clients/${id}/certificate/requests`, {
        profileId: selectedCertificateProfile,
        environment: certificateEnvironment === 'Sandbox' ? 1 : 2,
        acceptTerms: true,
        formValues: certificateFormValues
      });
      toast.success(response.data.kycUrl ? 'Solicitud creada. Completa la validación de identidad.' : 'Solicitud creada correctamente');
      loadCertificate();
    } catch (err: any) {
      const providerDetails = typeof err.response?.data?.details === 'string' ? err.response.data.details : '';
      const message = getErrorMessage(err, 'No fue posible crear la solicitud');
      toast.error(providerDetails ? `${message}: ${providerDetails}` : message);
    } finally {
      setRequestingCertificate(false);
    }
  };

  const loadHabilitationStatus = () => {
    api.get(`/tenant/clients/${id}/dian/habilitation-status`)
      .then(res => setHabilitationStatus(res.data))
      .catch(() => setHabilitationStatus(null));
  };

  const loadDocProvider = () => {
    api.get(`/tenant/clients/${id}/document-provider`)
      .then(res => setDocProvider(prev => ({ ...prev, ...res.data, dataicoApiPassword: '', dataicoAuthToken: '' })))
      .catch(() => {});
  };

  const loadPrepaid = () => {
    api.get(`/tenant/clients/${id}/prepaid/packages`).then(res => setPrepaidPackages(res.data)).catch(() => {});
    api.get(`/tenant/clients/${id}/prepaid/bags`).then(res => setPrepaidBags(res.data)).catch(() => {});
  };

  const loadIntegrators = () => {
    api.get('/tenant/integrators').then(res => setIntegrators(res.data)).catch(() => {});
  };

  useEffect(() => {
    if (activeTab === 'resolutions') { loadResolutions(); loadNoteCounters(); }
    if (activeTab === 'certificate') loadCertificate();
    if (activeTab === 'dian') loadHabilitationStatus();
    if (activeTab === 'credentials') { loadDocProvider(); loadIntegrators(); }
    if (activeTab === 'prepaid') { loadPrepaid(); loadIntegrators(); }
    if (activeTab === 'enablements') { loadEnabledDocTypes(); loadEnabledRetentions(); }
    if (activeTab === 'reception') loadReceptionSettings();
    if (activeTab === 'smtp') loadSmtpSettings();
    if (activeTab === 'templates') loadTemplateSettings();
  }, [activeTab, certificateEnvironment]);

  useEffect(() => {
    if (activeTab === 'enablements') loadEnabledRetentions();
  }, [retentionScope]);

  const loadEnabledDocTypes = () => {
    api.get(`/tenant/clients/${id}/enabled-document-types`).then(res => setEnabledDocTypes(res.data)).catch(() => {});
  };

  const loadEnabledRetentions = () => {
    api.get(`/tenant/clients/${id}/enabled-retention-concepts?scope=${retentionScope}`).then(res => setEnabledRetentions(res.data)).catch(() => {});
  };

  const toggleDocType = (docTypeId: string) => {
    setEnabledDocTypes(prev => prev.map(d => d.id === docTypeId ? { ...d, enabled: !d.enabled } : d));
  };

  const toggleRetention = (retentionId: string) => {
    setEnabledRetentions(prev => prev.map(r => r.id === retentionId ? { ...r, enabled: !r.enabled } : r));
  };

  const handleSaveDocTypes = async () => {
    setSavingDocTypes(true);
    try {
      const ids = enabledDocTypes.filter(d => d.enabled).map(d => d.id);
      await api.put(`/tenant/clients/${id}/enabled-document-types`, { ids });
      toast.success('Tipos de documento actualizados.');
    } catch {
      toast.error('Error al guardar los tipos de documento.');
    } finally {
      setSavingDocTypes(false);
    }
  };

  const handleSaveRetentions = async () => {
    setSavingRetentions(true);
    try {
      const ids = enabledRetentions.filter(r => r.enabled).map(r => r.id);
      await api.put(`/tenant/clients/${id}/enabled-retention-concepts`, { ids, scope: retentionScope });
      toast.success('Retenciones actualizadas.');
    } catch {
      toast.error('Error al guardar las retenciones.');
    } finally {
      setSavingRetentions(false);
    }
  };

  useEffect(() => {
    let interval: NodeJS.Timeout;
    if (habilitationStatus?.status === 'Testing') {
      interval = setInterval(() => {
        loadHabilitationStatus();
      }, 4000);
    }
    return () => clearInterval(interval);
  }, [habilitationStatus?.status]);

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await api.put(`/tenant/clients/${id}`, {
         companyName: client.companyName,
         personType: client.personType,
        commercialName: client.commercialName,
        taxId: client.taxId,
        verificationDigit: client.verificationDigit,
        email: client.email,
        phone: client.phone,
        address: client.address,
        city: client.city,
        cityCode: client.cityCode,
        organizationDepartment: client.organizationDepartment,
        organizationType: client.organizationType,
        legalRepresentativeFirstName: client.legalRepresentativeFirstName,
        legalRepresentativeOtherNames: client.legalRepresentativeOtherNames,
        legalRepresentativeFirstLastName: client.legalRepresentativeFirstLastName,
        legalRepresentativeSecondLastName: client.legalRepresentativeSecondLastName,
        legalRepresentativeDocumentType: client.legalRepresentativeDocumentType,
        legalRepresentativeDocumentNumber: client.legalRepresentativeDocumentNumber,
         legalRepresentativeDocumentCountryCode: client.legalRepresentativeDocumentCountryCode,
         legalRepresentativeEmail: client.legalRepresentativeEmail,
         legalRepresentativeRepresentationCode: client.legalRepresentativeRepresentationCode,
         legalRepresentativeOrganizationalArea: client.legalRepresentativeOrganizationalArea,
         legalRepresentativeStartDate: client.legalRepresentativeStartDate,
        taxRegime: client.taxRegime,
        economicActivity: client.economicActivity,
        isGranContribuyente: client.isGranContribuyente,
        isAgenteRetenedorIva: client.isAgenteRetenedorIva,
        isAutorretenedorRenta: client.isAutorretenedorRenta,
        decimalSeparator: client.decimalSeparator || '.',
        appliesRetentions: client.appliesRetentions,
        associateId: client.associateId || null,
        latitude: client.latitude,
        longitude: client.longitude,
         billingFrequency: client.billingFrequency
         ,electronicInvoiceLegend: client.electronicInvoiceLegend || '',
         supportDocumentLegend: client.supportDocumentLegend || ''
      });
      toast.success("Información del cliente actualizada exitosamente.");
    } catch (err) {
      toast.error("Error al actualizar la información.");
    }
  };

  const handleSaveDocProvider = async (e: React.FormEvent) => {
    e.preventDefault();
    setSavingDocProvider(true);
    try {
      await api.put(`/tenant/clients/${id}/document-provider`, docProvider);
      toast.success('Configuración de proveedor de documentos actualizada.');
      loadDocProvider();
    } catch (err: any) {
      toast.error(err.response?.data || 'Error al guardar la configuración.');
    } finally {
      setSavingDocProvider(false);
    }
  };

  const openNewPackageModal = () => {
    setEditingPackage(null);
    setPackageForm({ name: '', totalPrice: 0, discountedPricePerDocument: 0, integratorId: integrators[0]?.id || '', isActive: true });
    setShowPackageModal(true);
  };

  const openEditPackageModal = (pkg: any) => {
    setEditingPackage(pkg);
    setPackageForm({ name: pkg.name, totalPrice: pkg.totalPrice, discountedPricePerDocument: pkg.discountedPricePerDocument, integratorId: pkg.integratorId, isActive: pkg.isActive });
    setShowPackageModal(true);
  };

  const handleSavePackage = async (e: React.FormEvent) => {
    e.preventDefault();
    setSavingPackage(true);
    try {
      if (editingPackage) {
        await api.put(`/tenant/clients/${id}/prepaid/packages/${editingPackage.id}`, packageForm);
      } else {
        await api.post(`/tenant/clients/${id}/prepaid/packages`, packageForm);
      }
      toast.success('Paquete guardado.');
      setShowPackageModal(false);
      loadPrepaid();
    } catch (err: any) {
      toast.error(err.response?.data || 'Error al guardar el paquete.');
    } finally {
      setSavingPackage(false);
    }
  };

  const handleTogglePackageActive = async (pkg: any) => {
    try {
      await api.put(`/tenant/clients/${id}/prepaid/packages/${pkg.id}`, {
        name: pkg.name, totalPrice: pkg.totalPrice, discountedPricePerDocument: pkg.discountedPricePerDocument,
        integratorId: pkg.integratorId, isActive: !pkg.isActive
      });
      loadPrepaid();
    } catch {
      toast.error('Error al actualizar el paquete.');
    }
  };

  const handleActivateBag = async (packageId: string) => {
    setActivatingBagPackageId(packageId);
    try {
      await api.post(`/tenant/clients/${id}/prepaid/bags`, { packageId });
      toast.success('Bolsa activada para el Client.');
      loadPrepaid();
    } catch (err: any) {
      toast.error(err.response?.data || 'Error al activar la bolsa.');
    } finally {
      setActivatingBagPackageId(null);
    }
  };

  const closeResModal = () => {
    setShowResModal(false);
    setEditingResolutionId(null);
    setResBranchIds([]);
    setNewRes({
      resolutionNumber: '',
      prefix: '',
      numberStart: 0,
      numberEnd: 0,
      validFrom: '',
      validTo: '',
      technicalKey: '',
      documentType: 'FE'
    });
  };

  const openCreateResolutionModal = () => {
    setEditingResolutionId(null);
    setResBranchIds([]);
    setShowResModal(true);
  };

  const startEditResolution = (r: any) => {
    setEditingResolutionId(r.id);
    setResBranchIds(r.branchIds || []);
    setNewRes({
      resolutionNumber: r.resolutionNumber || '',
      prefix: r.prefix || '',
      numberStart: r.numberStart || 0,
      numberEnd: r.numberEnd || 0,
      validFrom: r.validFrom ? r.validFrom.split('T')[0] : '',
      validTo: r.validTo ? r.validTo.split('T')[0] : '',
      technicalKey: r.technicalKey || '',
      documentType: r.documentType
    });
    setShowResModal(true);
  };

  const handleCreateResolution = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      // La nómina no tiene resolución DIAN real: se completan con valores neutros los campos que
      // el formulario ocultó para este tipo (no aplican, pero el modelo los sigue teniendo).
      const payload = newRes.documentType === 'NE'
        ? {
            ...newRes,
            resolutionNumber: newRes.resolutionNumber || 'N/A',
            numberEnd: newRes.numberEnd || 999999999999,
            validFrom: newRes.validFrom || new Date().toISOString().split('T')[0],
            validTo: newRes.validTo || '2099-12-31',
          }
        : newRes;

      if (editingResolutionId) {
        await api.put(`/tenant/clients/${id}/resolutions/${editingResolutionId}`, payload);
        if (branches.length > 1) await api.put(`/tenant/clients/${id}/resolutions/${editingResolutionId}/branches`, { branchIds: resBranchIds });
        toast.success("Resolución actualizada");
      } else {
        await api.post(`/tenant/clients/${id}/resolutions`, { ...payload, branchIds: resBranchIds });
        toast.success("Resolución agregada");
      }
      closeResModal();
      loadResolutions();
    } catch (err: any) {
      toast.error(err?.response?.data?.message || err?.response?.data || (editingResolutionId ? "Error al actualizar resolución" : "Error al crear resolución"));
    }
  };

  const handleSetDefaultResolution = async (resId: string) => {
    try {
      await api.put(`/tenant/clients/${id}/resolutions/${resId}/set-default`);
      toast.success("Resolución marcada como predeterminada");
      loadResolutions();
    } catch (err: any) {
      toast.error(err?.response?.data?.message || "Error al marcar como predeterminada");
    }
  };

  const handlePdfUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setUploadingPdf(true);
    const formData = new FormData();
    formData.append("file", file);

    try {
      const res = await api.post(`/tenant/clients/${id}/resolutions/parse`, formData, {
        headers: { 'Content-Type': 'multipart/form-data' }
      });

      const parsedList: any[] = res.data;

      if (parsedList.length > 1) {
        // El PDF trae varios rangos en la misma hoja (ej. Factura Electrónica + Documento
        // Soporte) — se cargan todas directamente en vez de pasar por el modal una por una;
        // el cliente elimina después la que no necesite.
        let created = 0;
        for (const parsed of parsedList) {
          try {
            await api.post(`/tenant/clients/${id}/resolutions`, {
              resolutionNumber: parsed.resolutionNumber || '',
              prefix: parsed.prefix || '',
              numberStart: parsed.numberStart || 0,
              numberEnd: parsed.numberEnd || 0,
              validFrom: parsed.validFrom ? parsed.validFrom.split('T')[0] : '',
              validTo: parsed.validTo ? parsed.validTo.split('T')[0] : '',
              technicalKey: '',
              documentType: parsed.documentType || 'FE'
            });
            created++;
          } catch {
            // Sigue con las demás aunque una falle (ej. duplicada) — se reporta el conteo real al final.
          }
        }
        toast.success(`${created} de ${parsedList.length} resoluciones cargadas desde el PDF.`);
        closeResModal();
        loadResolutions();
      } else {
        const parsed = parsedList[0] || {};
        setNewRes(prev => ({
          ...prev,
          resolutionNumber: parsed.resolutionNumber || prev.resolutionNumber,
          prefix: parsed.prefix || prev.prefix,
          numberStart: parsed.numberStart || prev.numberStart,
          numberEnd: parsed.numberEnd || prev.numberEnd,
          validFrom: parsed.validFrom ? parsed.validFrom.split('T')[0] : prev.validFrom,
          validTo: parsed.validTo ? parsed.validTo.split('T')[0] : prev.validTo,
          documentType: parsed.documentType || prev.documentType
        }));
        toast.success("PDF procesado. Verifica los datos extraídos.");
      }
    } catch (err: any) {
      toast.error(err.response?.data || "Error al procesar el PDF");
    } finally {
      setUploadingPdf(false);
      e.target.value = '';
    }
  };

  const handleFetchTechnicalKey = async (resId: string) => {
    setFetchingTechnicalKeyId(resId);
    try {
      await api.post(`/tenant/clients/${id}/resolutions/${resId}/fetch-technical-key`);
      toast.success("Clave Técnica obtenida de la DIAN y guardada");
      loadResolutions();
    } catch (err: any) {
      toast.error(err?.response?.data || "Error consultando la Clave Técnica");
    } finally {
      setFetchingTechnicalKeyId(null);
    }
  };

  const handleDeleteResolution = async (resId: string) => {
    if (!confirm("¿Eliminar esta resolución?")) return;
    try {
      await api.delete(`/tenant/clients/${id}/resolutions/${resId}`);
      toast.success("Resolución eliminada");
      loadResolutions();
    } catch (err) {
      toast.error("Error al eliminar");
    }
  };

  const handleUploadCertificate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!certFile || !certPassword) {
      toast.error("El archivo y la contraseña son obligatorios");
      return;
    }
    
    setUploadingCert(true);
    const formData = new FormData();
    formData.append("file", certFile);
    formData.append("password", certPassword);

    try {
      await api.post(`/tenant/clients/${id}/certificate`, formData, {
        headers: { 'Content-Type': 'multipart/form-data' }
      });
      toast.success("Certificado cargado y validado con éxito");
      setCertFile(null);
      setCertPassword('');
      loadCertificate();
    } catch (err: any) {
      toast.error(err.response?.data || "Error al cargar el certificado");
    } finally {
      setUploadingCert(false);
    }
  };

  const handleStartHabilitation = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!magicLink) return;

    setIsHabilitating(true);
    try {
      await api.post(`/tenant/clients/${id}/dian/start-habilitation`, { magicLink });
      toast.success("¡Habilitación configurada y en progreso!");
      setMagicLink('');
      loadHabilitationStatus();
    } catch (err: any) {
      toast.error(err.response?.data || "Error al iniciar habilitación");
    } finally {
      setIsHabilitating(false);
    }
  };

  const handlePreviewTestDocument = async () => {
    setLoadingPreview(true);
    setTestDocPreview(null);
    try {
      const res = await api.get(`/tenant/clients/${id}/dian/preview-test-document`);
      setTestDocPreview(res.data);
    } catch (err: any) {
      toast.error(err.response?.data?.message || "Error armando la vista previa");
    } finally {
      setLoadingPreview(false);
    }
  };

  const handleSendTestDocument = async () => {
    if (!window.confirm("Esto envía un documento real al set de pruebas de la DIAN y gasta uno de los intentos disponibles (no se puede deshacer). ¿Continuar?")) return;
    setSendingTestDoc(true);
    try {
      const res = await api.post(`/tenant/clients/${id}/dian/send-test-document`);
      toast.success(`Documento ${res.data.documentNumber} enviado a la DIAN.`);
      setSentTestDocs(prev => [...prev, { documentNumber: res.data.documentNumber, cufe: res.data.cufe, trackId: res.data.trackId, checking: false, outcome: null }]);
      setTestDocPreview(null);
      loadHabilitationStatus();
    } catch (err: any) {
      toast.error(err.response?.data?.message || "Error enviando el documento de prueba");
    } finally {
      setSendingTestDoc(false);
    }
  };

  // "Resuelto" (aceptado/rechazado) según la DIAN vía GetStatusZip — sin esto, "enviado a la DIAN"
  // solo confirma que llegó, no si fue validado (ver el comentario en TestSetSubmissionResult).
  const handleCheckTestDocStatus = async (trackId: string) => {
    setSentTestDocs(prev => prev.map(d => d.trackId === trackId ? { ...d, checking: true } : d));
    try {
      const res = await api.get(`/tenant/clients/${id}/dian/test-document-status`, { params: { trackId } });
      setSentTestDocs(prev => prev.map(d => d.trackId === trackId ? { ...d, checking: false, outcome: res.data } : d));
    } catch (err: any) {
      toast.error(err.response?.data?.message || "Error consultando el estado del documento");
      setSentTestDocs(prev => prev.map(d => d.trackId === trackId ? { ...d, checking: false } : d));
    }
  };

  const handleCheckManualTrackId = async () => {
    if (!manualTrackId.trim()) return;
    setCheckingManualTrackId(true);
    setManualTrackIdOutcome(null);
    try {
      const res = await api.get(`/tenant/clients/${id}/dian/test-document-status`, { params: { trackId: manualTrackId.trim() } });
      setManualTrackIdOutcome(res.data);
    } catch (err: any) {
      toast.error(err.response?.data?.message || "Error consultando el estado del documento");
    } finally {
      setCheckingManualTrackId(false);
    }
  };

  if (loading) {
    return (
      <div className="flex h-full items-center justify-center">
        <Loader2 className="w-10 h-10 animate-spin text-blue-500" />
      </div>
    );
  }

  return (
    <div className="h-full flex flex-col overflow-hidden bg-slate-50/50">
      {/* Cabecera */}
      <header className="bg-white border-b border-slate-200 px-8 py-5 flex items-center justify-between shrink-0 shadow-sm z-10">
        <div className="flex items-center gap-4">
          <button 
            onClick={() => navigate('/clients')}
            className="p-2 hover:bg-slate-100 text-slate-500 hover:text-slate-800 rounded-lg transition-colors"
          >
            <ArrowLeft size={20} />
          </button>
          <div>
            <h1 className="text-2xl font-bold text-slate-800 tracking-tight">{client.companyName}</h1>
            <div className="flex items-center gap-2 text-sm text-slate-500 mt-0.5">
              <span>NIT: {client.taxId}</span>
              <span className="w-1 h-1 rounded-full bg-slate-300"></span>
              <span className={client.isActive ? 'text-emerald-600 font-medium' : 'text-rose-600 font-medium'}>
                {client.isActive ? 'Emisor Activo' : 'Emisor Inactivo'}
              </span>
            </div>
          </div>
        </div>
      </header>

      {/* Contenido / Tabs */}
      <div className="flex-1 flex overflow-hidden">
        {/* Menú lateral interno */}
        <div className="w-64 bg-slate-50/80 border-r border-slate-200 p-6 shrink-0 flex flex-col gap-2 overflow-y-auto">
          <button 
            onClick={() => setActiveTab('info')}
            className={`flex items-center gap-3 px-4 py-3 rounded-xl transition-all text-sm font-medium ${
              activeTab === 'info' ? 'bg-white shadow-sm border border-slate-200 text-blue-600' : 'text-slate-600 hover:bg-slate-100'
            }`}
          >
            <Building2 size={18} /> Info. Básica
          </button>
          <button 
            onClick={() => setActiveTab('resolutions')}
            className={`flex items-center gap-3 px-4 py-3 rounded-xl transition-all text-sm font-medium ${
              activeTab === 'resolutions' ? 'bg-white shadow-sm border border-slate-200 text-blue-600' : 'text-slate-600 hover:bg-slate-100'
            }`}
          >
            <FileSignature size={18} /> Resoluciones y Consecutivos
          </button>
          <button 
            onClick={() => setActiveTab('dian')}
            className={`flex items-center gap-3 px-4 py-3 rounded-xl transition-all text-sm font-medium ${
              activeTab === 'dian' ? 'bg-white shadow-sm border border-slate-200 text-blue-600' : 'text-slate-600 hover:bg-slate-100'
            }`}
          >
            <Zap size={18} /> Habilitación DIAN
          </button>
          <button 
            onClick={() => setActiveTab('certificate')}
            className={`flex items-center gap-3 px-4 py-3 rounded-xl transition-all text-sm font-medium ${
              activeTab === 'certificate' ? 'bg-white shadow-sm border border-slate-200 text-blue-600' : 'text-slate-600 hover:bg-slate-100'
            }`}
          >
            <FileKey size={18} /> Certificado Digital
          </button>
          <button
            onClick={() => setActiveTab('credentials')}
            className={`flex items-center gap-3 px-4 py-3 rounded-xl transition-all text-sm font-medium ${
              activeTab === 'credentials' ? 'bg-white shadow-sm border border-slate-200 text-blue-600' : 'text-slate-600 hover:bg-slate-100'
            }`}
          >
            <ShieldAlert size={18} /> Proveedor y MinSalud
          </button>
          <button
            onClick={() => setActiveTab('branches')}
            className={`flex items-center gap-3 px-4 py-3 rounded-xl transition-all text-sm font-medium ${
              activeTab === 'branches' ? 'bg-white shadow-sm border border-slate-200 text-blue-600' : 'text-slate-600 hover:bg-slate-100'
            }`}
          >
            <Building2 size={18} /> Sucursales
          </button>
          <button
            onClick={() => setActiveTab('users')}
            className={`flex items-center gap-3 px-4 py-3 rounded-xl transition-all text-sm font-medium ${
              activeTab === 'users' ? 'bg-white shadow-sm border border-slate-200 text-blue-600' : 'text-slate-600 hover:bg-slate-100'
            }`}
          >
            <UserRound size={18} /> Usuarios del portal
          </button>
          <button
            onClick={() => setActiveTab('prepaid')}
            className={`flex items-center gap-3 px-4 py-3 rounded-xl transition-all text-sm font-medium ${
              activeTab === 'prepaid' ? 'bg-white shadow-sm border border-slate-200 text-blue-600' : 'text-slate-600 hover:bg-slate-100'
            }`}
          >
            <Package size={18} /> Paquetes Prepago
          </button>
          <button
            onClick={() => setActiveTab('enablements')}
            className={`flex items-center gap-3 px-4 py-3 rounded-xl transition-all text-sm font-medium ${
              activeTab === 'enablements' ? 'bg-white shadow-sm border border-slate-200 text-blue-600' : 'text-slate-600 hover:bg-slate-100'
            }`}
          >
            <ListChecks size={18} /> Documentos y Retenciones
          </button>
          <button
            onClick={() => setActiveTab('reception')}
            className={`flex items-center gap-3 px-4 py-3 rounded-xl transition-all text-sm font-medium ${
              activeTab === 'reception' ? 'bg-white shadow-sm border border-slate-200 text-blue-600' : 'text-slate-600 hover:bg-slate-100'
            }`}
          >
            <Inbox size={18} /> Eventos de Recepción
          </button>
          <button
            onClick={() => setActiveTab('smtp')}
            className={`flex items-center gap-3 px-4 py-3 rounded-xl transition-all text-sm font-medium ${
              activeTab === 'smtp' ? 'bg-white shadow-sm border border-slate-200 text-blue-600' : 'text-slate-600 hover:bg-slate-100'
            }`}
          >
            <Mail size={18} /> SMTP de Reenvío
          </button>
          <button
            onClick={() => setActiveTab('templates')}
            className={`flex items-center gap-3 px-4 py-3 rounded-xl transition-all text-sm font-medium ${
              activeTab === 'templates' ? 'bg-white shadow-sm border border-slate-200 text-blue-600' : 'text-slate-600 hover:bg-slate-100'
            }`}
          >
            <LayoutTemplate size={18} /> Plantillas
          </button>
        </div>

        {/* Panel principal con scroll propio */}
        <div className="flex-1 overflow-y-auto p-10 animate-in fade-in duration-300">
          <div className="max-w-4xl">
            {activeTab === 'info' && (
              <div className="bg-white rounded-3xl p-8 shadow-sm border border-slate-100">
                <h2 className="text-xl font-bold text-slate-800 mb-6">Información del Emisor</h2>
                <form onSubmit={handleSave} className="space-y-6">
                  
                   <ClientFormFields client={client} setClient={setClient} associates={associates} showBillingSection />

                  <div className="rounded-2xl border border-slate-200 bg-slate-50 p-5 space-y-4">
                    <div>
                      <h3 className="font-semibold text-slate-800">Leyendas generales de documentos</h3>
                      <p className="text-sm text-slate-500 mt-1">Se usan cuando la resolución no tiene una leyenda específica por tipo y prefijo.</p>
                    </div>
                    <label className="block text-sm font-medium text-slate-700">
                      Facturación electrónica
                      <textarea rows={3} value={client.electronicInvoiceLegend} onChange={e => setClient({ ...client, electronicInvoiceLegend: e.target.value })} className="mt-1 w-full rounded-xl border border-slate-300 bg-white p-3 font-normal" placeholder="Ej. Consignar en la cuenta..." />
                    </label>
                    <label className="block text-sm font-medium text-slate-700">
                      Documento soporte
                      <textarea rows={3} value={client.supportDocumentLegend} onChange={e => setClient({ ...client, supportDocumentLegend: e.target.value })} className="mt-1 w-full rounded-xl border border-slate-300 bg-white p-3 font-normal" placeholder="Ej. Consignar en la cuenta..." />
                    </label>
                  </div>

                  <div className="pt-6 border-t border-slate-100 flex justify-end">
                    <button type="submit" className="bg-blue-600 hover:bg-blue-700 text-white px-8 py-3 rounded-xl font-semibold shadow-lg shadow-blue-500/30 flex items-center gap-2 transition-transform hover:-translate-y-0.5">
                      <Save size={18} />
                      Guardar Cambios
                    </button>
                  </div>
                </form>
              </div>
            )}

            {activeTab === 'resolutions' && (
              <>
              <div className="bg-white rounded-3xl p-8 shadow-sm border border-slate-100">
                <div className="flex justify-between items-center mb-6">
                  <h2 className="text-xl font-bold text-slate-800">Resoluciones de Facturación</h2>
                  <button onClick={openCreateResolutionModal} className="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-xl font-medium shadow-md transition-colors flex items-center gap-2 text-sm">
                    <Plus size={16} /> Nueva Resolución
                  </button>
                </div>

                {resolutions.length === 0 ? (
                  <div className="flex flex-col items-center justify-center text-center h-64 border-2 border-dashed border-slate-200 rounded-2xl bg-slate-50/50">
                    <div className="w-16 h-16 bg-blue-50 text-blue-600 rounded-full flex items-center justify-center mb-4">
                      <FileSignature size={32} />
                    </div>
                    <h3 className="text-lg font-bold text-slate-700">Sin Resoluciones</h3>
                    <p className="text-slate-500 max-w-sm mt-2">No hay rangos de numeración activos para este emisor.</p>
                  </div>
                ) : (
                  <div className="overflow-x-auto">
                    <table className="w-full text-left border-collapse">
                      <thead>
                        <tr className="bg-slate-50 text-slate-500 text-sm border-y border-slate-200">
                          <th className="font-semibold py-3 px-4 rounded-tl-xl">Tipo / Prefijo</th>
                          <th className="font-semibold py-3 px-4">Resolución</th>
                          <th className="font-semibold py-3 px-4">Rango</th>
                          <th className="font-semibold py-3 px-4">Próximo #</th>
                          {branches.length > 1 && <th className="font-semibold py-3 px-4">Sucursales</th>}
                          <th className="font-semibold py-3 px-4">Vigencia</th>
                          <th className="font-semibold py-3 px-4 text-center">Predeterminada</th>
                          <th className="font-semibold py-3 px-4 text-center rounded-tr-xl">Acciones</th>
                        </tr>
                      </thead>
                      <tbody>
                        {resolutions.map(r => (
                          <tr key={r.id} className="border-b border-slate-100 hover:bg-slate-50/50 transition-colors">
                            <td className="py-4 px-4">
                              <div className="flex items-center gap-3">
                                <span className={`px-2 py-1 rounded-md text-xs font-bold ${r.documentType === 'FE' ? 'bg-blue-100 text-blue-700' : 'bg-purple-100 text-purple-700'}`}>{r.documentType}</span>
                                <span className="font-bold text-slate-700">{r.prefix || '-'}</span>
                              </div>
                            </td>
                            <td className="py-4 px-4 font-mono text-sm text-slate-600">{r.resolutionNumber}</td>
                            <td className="py-4 px-4 text-sm text-slate-600">{r.numberStart} a {r.numberEnd}</td>
                            <td className="py-4 px-4 text-sm">
                              {editingNextNumberId === r.id ? (
                                <div className="flex items-center gap-1.5">
                                  <input
                                    type="number"
                                    min={r.numberStart}
                                    max={r.numberEnd}
                                    autoFocus
                                    className="w-24 px-2 py-1.5 bg-white border border-blue-500 rounded-lg outline-none text-sm font-mono"
                                    value={nextNumberDraft}
                                    onChange={e => setNextNumberDraft(e.target.value)}
                                    disabled={savingNextNumber}
                                  />
                                  <button onClick={() => saveNextNumber(r)} disabled={savingNextNumber} className="p-1.5 text-emerald-600 hover:bg-emerald-50 rounded-lg" title="Guardar">
                                    {savingNextNumber ? <Loader2 size={16} className="animate-spin" /> : <Check size={16} />}
                                  </button>
                                  <button onClick={() => setEditingNextNumberId(null)} disabled={savingNextNumber} className="p-1.5 text-slate-400 hover:bg-slate-100 rounded-lg" title="Cancelar">
                                    <X size={16} />
                                  </button>
                                </div>
                              ) : (
                                <button onClick={() => startEditNextNumber(r)} className="flex items-center gap-1.5 font-mono font-bold text-slate-700 hover:text-blue-600 group">
                                  {r.nextNumber ?? r.numberStart}
                                  <Pencil size={13} className="text-slate-300 group-hover:text-blue-600" />
                                </button>
                              )}
                            </td>
                            {branches.length > 1 && (
                              <td className="py-4 px-4 text-xs text-slate-600">
                                {(r.branchIds || []).map((bid: string) => branches.find(b => b.id === bid)?.name).filter(Boolean).join(', ') || '—'}
                              </td>
                            )}
                            <td className="py-4 px-4 text-sm text-slate-500">
                              {r.documentType === 'NE' ? 'Sin vencimiento' : `${new Date(r.validFrom).toLocaleDateString()} - ${new Date(r.validTo).toLocaleDateString()}`}
                            </td>
                            <td className="py-4 px-4 text-center">
                              {r.isDefault ? (
                                <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-xs font-bold bg-amber-100 text-amber-700">
                                  <Star size={14} fill="currentColor" /> Predeterminada
                                </span>
                              ) : (
                                <button onClick={() => handleSetDefaultResolution(r.id)} className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-xs font-bold text-slate-400 hover:bg-amber-50 hover:text-amber-600 transition-colors" title="Marcar como predeterminada para este tipo de documento">
                                  <Star size={14} /> Marcar
                                </button>
                              )}
                            </td>
                            <td className="py-4 px-4 text-center">
                              <div className="flex items-center justify-center gap-1">
                                {r.documentType === 'FE' && (
                                  <button
                                    onClick={() => handleFetchTechnicalKey(r.id)}
                                    disabled={fetchingTechnicalKeyId === r.id}
                                    className="p-2 text-slate-400 hover:text-emerald-600 hover:bg-emerald-50 rounded-lg transition-colors disabled:opacity-50"
                                    title="Consultar Clave Técnica real en la DIAN (GetNumberingRange)"
                                  >
                                    {fetchingTechnicalKeyId === r.id ? <Loader2 size={16} className="animate-spin" /> : <FileKey size={16} />}
                                  </button>
                                )}
                                <button onClick={() => startEditResolution(r)} className="p-2 text-slate-400 hover:text-blue-600 hover:bg-blue-50 rounded-lg transition-colors" title="Editar">
                                  <Pencil size={16} />
                                </button>
                                <button onClick={() => handleDeleteResolution(r.id)} className="p-2 text-rose-500 hover:bg-rose-50 rounded-lg transition-colors" title="Eliminar">
                                  <Trash2 size={16} />
                                </button>
                              </div>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>

              <div className="bg-white rounded-3xl p-8 shadow-sm border border-slate-100 mt-8">
                <div className="mb-6">
                  <h2 className="text-xl font-bold text-slate-800">Consecutivos de Notas</h2>
                  <p className="text-slate-500 mt-1 text-sm max-w-2xl">
                    Las Notas Crédito, Débito y de Ajuste no tienen un rango autorizado por la DIAN (no aplica una resolución) — este consecutivo es interno, y sirve solo para numerarlas de forma ordenada.
                  </p>
                </div>

                <div className="overflow-x-auto">
                  <table className="w-full text-left border-collapse">
                    <thead>
                      <tr className="bg-slate-50 text-slate-500 text-sm border-y border-slate-200">
                        <th className="font-semibold py-3 px-4 rounded-tl-xl">Tipo de Nota</th>
                        <th className="font-semibold py-3 px-4 rounded-tr-xl">Próximo #</th>
                      </tr>
                    </thead>
                    <tbody>
                      {(['credit', 'debit', 'adjustment'] as const).map(type => (
                        <tr key={type} className="border-b border-slate-100 hover:bg-slate-50/50 transition-colors">
                          <td className="py-4 px-4">
                            <span className={`px-2 py-1 rounded-md text-xs font-bold ${NOTE_COUNTER_BADGE[type]}`}>
                              {NOTE_COUNTER_LABEL[type]}
                            </span>
                          </td>
                          <td className="py-4 px-4 text-sm">
                            {editingNoteCounterType === type ? (
                              <div className="flex items-center gap-1.5">
                                <input
                                  type="number"
                                  min={1}
                                  autoFocus
                                  className="w-24 px-2 py-1.5 bg-white border border-blue-500 rounded-lg outline-none text-sm font-mono"
                                  value={noteCounterDraft}
                                  onChange={e => setNoteCounterDraft(e.target.value)}
                                  disabled={savingNoteCounter}
                                />
                                <button onClick={() => saveNoteCounter(type)} disabled={savingNoteCounter} className="p-1.5 text-emerald-600 hover:bg-emerald-50 rounded-lg" title="Guardar">
                                  {savingNoteCounter ? <Loader2 size={16} className="animate-spin" /> : <Check size={16} />}
                                </button>
                                <button onClick={() => setEditingNoteCounterType(null)} disabled={savingNoteCounter} className="p-1.5 text-slate-400 hover:bg-slate-100 rounded-lg" title="Cancelar">
                                  <X size={16} />
                                </button>
                              </div>
                            ) : (
                              <button onClick={() => startEditNoteCounter(type)} className="flex items-center gap-1.5 font-mono font-bold text-slate-700 hover:text-blue-600 group">
                                {noteCounters?.[NOTE_COUNTER_FIELD[type]] ?? 1}
                                <Pencil size={13} className="text-slate-300 group-hover:text-blue-600" />
                              </button>
                            )}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
              </>
            )}

            {activeTab === 'certificate' && (
              <div className="bg-white rounded-3xl p-8 shadow-sm border border-slate-100">
                <div className="flex items-center gap-4 mb-8">
                  <div className="w-12 h-12 bg-blue-50 text-blue-600 rounded-xl flex items-center justify-center">
                    <FileKey size={24} />
                  </div>
                  <div>
                    <h2 className="text-xl font-bold text-slate-800">Certificado digital</h2>
                    <p className="text-sm text-slate-500">Solicita y administra certificados por ambiente.</p>
                  </div>
                </div>

                <div className="mb-8 rounded-2xl border border-blue-100 bg-blue-50 p-6 space-y-4">
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <div>
                      <h3 className="font-bold text-slate-800">Solicitar certificado automáticamente</h3>
                      <p className="text-sm text-slate-600">La solicitud se procesa con Viafirma. En Sandbox no se reemplaza el certificado existente.</p>
                    </div>
                    <select value={certificateEnvironment} onChange={e => { setCertificateEnvironment(e.target.value as 'Sandbox' | 'Production'); setSelectedCertificateProfile(''); }} className="rounded-xl border border-blue-200 bg-white px-3 py-2 text-sm">
                      <option value="Sandbox">Sandbox</option>
                      <option value="Production">Producción</option>
                    </select>
                  </div>
                  {certificateOptions?.profiles?.length ? (
                    <>
                      <div className="rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900">
                        Completa los datos del representante legal en los campos marcados con *. Viafirma los exige para emitir el certificado y no los reemplazaremos con datos de la organización.
                      </div>
                       <div className="rounded-xl border border-blue-200 bg-white px-3 py-3 text-sm font-semibold text-slate-700">
                         Perfil seleccionado: {certificateOptions.profiles.find((profile: any) => profile.id === selectedCertificateProfile)?.title}
                       </div>
                      <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
                         {(certificateOptions.profiles.find((profile: any) => profile.id === selectedCertificateProfile)?.fields || []).filter((field: any) => !CLIENT_CERTIFICATE_FIELDS.has(field.externalName)).map((field: any) => {
                          const options = (field.defaultValue || '').split(';').filter(Boolean).map((option: string) => {
                            const separator = option.lastIndexOf('|');
                            return separator >= 0 ? { label: option.slice(0, separator), value: option.slice(separator + 1) } : { label: option, value: option };
                          });
                          if (field.externalName === 'dnAlternativo2' && !options.some((option: any) => option.value === 'RM')) {
                            options.unshift({ label: 'Registro Mercantil', value: 'RM' });
                          }
                          const value = certificateFormValues[field.externalName] || '';
                          const updateValue = (next: string) => setCertificateFormValues(current => ({ ...current, [field.externalName]: next }));
                          return (
                            <label key={field.externalName} className="space-y-1 text-sm font-medium text-slate-700">
                              <span>{field.label}{field.isRequired ? ' *' : ''}</span>
                              {field.type === 'SELECT' ? (
                                <select required={field.isRequired} value={value} onChange={event => updateValue(event.target.value)} className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5">
                                  <option value="">Selecciona una opción</option>
                                  {options.map((option: any) => <option key={option.value} value={option.value}>{option.label}</option>)}
                                </select>
                              ) : (
                                <input required={field.isRequired} type={field.type === 'EMAIL' ? 'email' : 'text'} value={value} onChange={event => updateValue(event.target.value)} className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5" />
                              )}
                            </label>
                          );
                        })}
                      </div>
                      <label className="flex items-start gap-2 text-sm text-slate-700"><input type="checkbox" checked={acceptCertificateTerms} onChange={e => setAcceptCertificateTerms(e.target.checked)} className="mt-1" /> Acepto los términos y condiciones del certificado seleccionado.</label>
                      <button type="button" onClick={handleCreateCertificateRequest} disabled={requestingCertificate || !acceptCertificateTerms} className="rounded-xl bg-blue-600 px-5 py-3 font-bold text-white disabled:opacity-50">
                        {requestingCertificate ? 'Creando solicitud...' : 'Solicitar certificado'}
                      </button>
                    </>
                  ) : (
                    <p className="text-sm text-slate-600">No hay perfiles disponibles para este ambiente. Solicita a Administración sincronizar los perfiles.</p>
                  )}
                  {certificateOptions?.certificates?.length > 0 && <div className="border-t border-blue-100 pt-3 text-sm text-slate-600">Este ambiente tiene {certificateOptions.certificates.length} certificado(s). Las solicitudes nuevas se conservan como historial y no eliminan los existentes.</div>}
                  {certificateOptions?.requests?.length > 0 && <div className="border-t border-blue-100 pt-3 space-y-2"><p className="text-sm font-semibold text-slate-700">Solicitudes recientes</p>{certificateOptions.requests.slice(0, 3).map((request: any) => <div key={request.id} className="flex flex-wrap items-center justify-between gap-2 text-sm text-slate-600"><span>{request.status} · {new Date(request.createdAt).toLocaleString()}</span>{request.kycUrl && <a href={request.kycUrl} target="_blank" rel="noreferrer" className="font-semibold text-blue-700 underline">Completar validación</a>}</div>)}</div>}
                </div>

                <div className="grid grid-cols-1 lg:grid-cols-2 gap-8">
                  {/* Estado Actual */}
                  <div className="bg-slate-50 border border-slate-200 rounded-2xl p-6">
                    <h3 className="text-sm font-bold text-slate-400 uppercase tracking-wider mb-4">Estado del Certificado</h3>
                    {certInfo ? (
                      <div className="space-y-4">
                        <div className="flex items-center gap-3">
                          <div className="w-3 h-3 rounded-full bg-emerald-500 shadow-[0_0_10px_rgba(16,185,129,0.5)]"></div>
                          <span className="font-semibold text-slate-700">Certificado Activo y Validado</span>
                        </div>
                        <div className="pt-2 border-t border-slate-200">
                          <p className="text-sm text-slate-500 mb-1">Archivo:</p>
                          <p className="font-mono text-sm text-slate-700 bg-white p-2 border border-slate-200 rounded-lg overflow-hidden text-ellipsis whitespace-nowrap" title={certInfo.fileName}>{certInfo.fileName}</p>
                        </div>
                        <div>
                          <p className="text-sm text-slate-500 mb-1">Fecha de Caducidad:</p>
                          <p className="font-bold text-slate-700">{new Date(certInfo.expirationDate).toLocaleDateString()} {new Date(certInfo.expirationDate).toLocaleTimeString()}</p>
                        </div>
                        {new Date(certInfo.expirationDate) < new Date() && (
                          <div className="p-3 bg-rose-50 text-rose-600 border border-rose-200 rounded-xl text-sm font-medium">
                            El certificado ha expirado. Por favor, sube uno nuevo.
                          </div>
                        )}
                      </div>
                    ) : (
                      <div className="flex flex-col items-center justify-center h-40 text-center">
                        <ShieldAlert className="text-slate-300 w-12 h-12 mb-2" />
                        <span className="font-medium text-slate-500">No hay certificado cargado.</span>
                        <span className="text-xs text-slate-400 mt-1">El emisor no podrá firmar facturas.</span>
                      </div>
                    )}
                  </div>

                  {/* Formulario Carga */}
                  <form onSubmit={handleUploadCertificate} className="bg-white border border-slate-200 rounded-2xl p-6 shadow-sm">
                    <h3 className="text-sm font-bold text-slate-400 uppercase tracking-wider mb-4">Reemplazar / Subir Certificado</h3>
                    
                    <div className="space-y-4">
                      <div>
                        <label className="block text-sm font-semibold text-slate-700 mb-2">Archivo .p12 / .pfx</label>
                        <input 
                          type="file" 
                          accept=".p12,.pfx"
                          required
                          onChange={e => setCertFile(e.target.files ? e.target.files[0] : null)}
                          className="w-full text-sm text-slate-500 file:mr-4 file:py-2.5 file:px-4 file:rounded-xl file:border-0 file:text-sm file:font-semibold file:bg-blue-50 file:text-blue-700 hover:file:bg-blue-100 outline-none cursor-pointer border border-slate-200 rounded-xl bg-slate-50"
                        />
                      </div>
                      <div>
                        <label className="block text-sm font-semibold text-slate-700 mb-2">Contraseña del Certificado</label>
                        <input 
                          type="password" 
                          required
                          value={certPassword}
                          onChange={e => setCertPassword(e.target.value)}
                          placeholder="Ingresa la contraseña..."
                          className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none"
                        />
                      </div>
                      <div className="pt-2">
                        <button 
                          type="submit" 
                          disabled={uploadingCert || !certFile || !certPassword}
                          className="w-full bg-slate-800 hover:bg-slate-900 disabled:opacity-50 text-white py-3 rounded-xl font-bold shadow-md transition-colors flex justify-center items-center gap-2"
                        >
                          {uploadingCert ? <Loader2 className="w-5 h-5 animate-spin" /> : <FileKey size={18} />}
                          {uploadingCert ? 'Validando...' : 'Cargar y Validar'}
                        </button>
                      </div>
                    </div>
                  </form>
                </div>
              </div>
            )}
            
            {activeTab === 'credentials' && (
              <div className="space-y-8">
                <div className="bg-white rounded-3xl p-8 shadow-sm border border-slate-100">
                  <h2 className="text-xl font-bold text-slate-800 mb-1">Proveedor de Documentos Electrónicos</h2>
                  <p className="text-slate-500 text-sm mb-6">
                    {integrators.length > 1
                      ? 'Por defecto los documentos se emiten con el motor propio. Si este emisor factura con otro proveedor, actívalo aquí y captura sus credenciales.'
                      : 'Los documentos de este emisor se emiten con el motor propio (emisión directa ante la DIAN).'}
                  </p>
                  <form onSubmit={handleSaveDocProvider} className="space-y-4">
                    <div className="flex gap-3">
                      {integrators.map(i => {
                        const value = i.code === 'DATAICO' ? 'Dataico' : 'Native';
                        return (
                          <button
                            key={i.id}
                            type="button"
                            onClick={() => setDocProvider({ ...docProvider, documentProvider: value })}
                            className={`px-5 py-2.5 rounded-xl font-bold text-sm transition-colors ${
                              docProvider.documentProvider === value ? 'bg-blue-600 text-white shadow-md' : 'bg-slate-100 text-slate-500 hover:bg-slate-200'
                            }`}
                          >
                            {i.code === 'NATIVE' ? 'Motor Propio' : i.name}
                          </button>
                        );
                      })}
                    </div>

                    {docProvider.documentProvider === 'Dataico' && (
                      <div className="grid grid-cols-1 md:grid-cols-2 gap-4 pt-2">
                        <div>
                          <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Ambiente</label>
                          <select value={docProvider.dataicoEnvironment} onChange={e => setDocProvider({ ...docProvider, dataicoEnvironment: e.target.value })} className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-lg focus:ring-2 focus:ring-blue-500 outline-none bg-white">
                            <option value="PRUEBAS">Pruebas</option>
                            <option value="PRODUCCION">Producción</option>
                          </select>
                        </div>
                        <div>
                          <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Dataico Account ID</label>
                          <input type="text" value={docProvider.dataicoAccountId} onChange={e => setDocProvider({ ...docProvider, dataicoAccountId: e.target.value })} className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-lg focus:ring-2 focus:ring-blue-500 outline-none font-mono text-sm" />
                        </div>
                        <div>
                          <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Usuario API</label>
                          <input type="text" value={docProvider.dataicoApiUser} onChange={e => setDocProvider({ ...docProvider, dataicoApiUser: e.target.value })} className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-lg focus:ring-2 focus:ring-blue-500 outline-none" />
                        </div>
                        <div>
                          <label className="block text-xs font-bold text-slate-500 uppercase mb-1">
                            Contraseña API {docProvider.hasApiPassword && <span className="text-emerald-600 normal-case font-normal">(configurada)</span>}
                          </label>
                          <input type="password" placeholder={docProvider.hasApiPassword ? 'Dejar vacío para no cambiar' : ''} value={docProvider.dataicoApiPassword} onChange={e => setDocProvider({ ...docProvider, dataicoApiPassword: e.target.value })} className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-lg focus:ring-2 focus:ring-blue-500 outline-none" />
                        </div>
                        <div className="md:col-span-2">
                          <label className="block text-xs font-bold text-slate-500 uppercase mb-1">
                            Auth-token {docProvider.hasAuthToken && <span className="text-emerald-600 normal-case font-normal">(configurado)</span>}
                          </label>
                          <input type="password" placeholder={docProvider.hasAuthToken ? 'Dejar vacío para no cambiar' : ''} value={docProvider.dataicoAuthToken} onChange={e => setDocProvider({ ...docProvider, dataicoAuthToken: e.target.value })} className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-lg focus:ring-2 focus:ring-blue-500 outline-none font-mono text-sm" />
                        </div>
                      </div>
                    )}

                    <div className="flex justify-end pt-2">
                      <button type="submit" disabled={savingDocProvider} className="px-5 py-2.5 bg-blue-600 text-white rounded-lg font-medium hover:bg-blue-500 transition-colors disabled:opacity-50">
                        {savingDocProvider ? 'Guardando...' : 'Guardar Proveedor'}
                      </button>
                    </div>
                  </form>
                </div>

                <MinSaludCard clientId={id!} />
                <IhceCard clientId={id!} />
              </div>
            )}

            {activeTab === 'branches' && <BranchesTab clientId={id!} />}

            {activeTab === 'users' && <ClientUsersTab clientId={id!} />}

            {activeTab === 'prepaid' && (
              <div className="space-y-8">
                <div className="bg-white rounded-3xl p-8 shadow-sm border border-slate-100">
                  <div className="flex justify-between items-center mb-1">
                    <h2 className="text-xl font-bold text-slate-800">Paquetes Prepago</h2>
                    <button onClick={openNewPackageModal} className="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-xl font-medium shadow-md transition-colors flex items-center gap-2 text-sm">
                      <Plus size={16} /> Nuevo Paquete
                    </button>
                  </div>
                  <p className="text-slate-500 text-sm mb-6">
                    Ofertas de documentos prepago para este Client: mientras tenga saldo, cada documento del integrador elegido se cobra a la tarifa preferencial en vez de la tarifa estándar.
                  </p>

                  {prepaidPackages.length === 0 ? (
                    <div className="flex flex-col items-center justify-center text-center h-56 border-2 border-dashed border-slate-200 rounded-2xl bg-slate-50/50">
                      <div className="w-16 h-16 bg-blue-50 text-blue-600 rounded-full flex items-center justify-center mb-4">
                        <Package size={32} />
                      </div>
                      <h3 className="text-lg font-bold text-slate-700">Sin paquetes definidos</h3>
                      <p className="text-slate-500 max-w-sm mt-2">Crea un paquete para poder ofrecerle documentos prepago a este Client.</p>
                    </div>
                  ) : (
                    <div className="overflow-x-auto">
                      <table className="w-full text-left border-collapse">
                        <thead>
                          <tr className="bg-slate-50 text-slate-500 text-sm border-y border-slate-200">
                            <th className="font-semibold py-3 px-4 rounded-tl-xl">Paquete</th>
                            <th className="font-semibold py-3 px-4">Integrador</th>
                            <th className="font-semibold py-3 px-4">Precio total</th>
                            <th className="font-semibold py-3 px-4">Tarifa / documento</th>
                            <th className="font-semibold py-3 px-4">Estado</th>
                            <th className="font-semibold py-3 px-4 text-center rounded-tr-xl">Acciones</th>
                          </tr>
                        </thead>
                        <tbody>
                          {prepaidPackages.map((p: any) => (
                            <tr key={p.id} className="border-b border-slate-100 hover:bg-slate-50/50 transition-colors">
                              <td className="py-4 px-4 font-semibold text-slate-700">{p.name}</td>
                              <td className="py-4 px-4 text-sm text-slate-600">{p.integratorName}</td>
                              <td className="py-4 px-4 text-sm text-slate-600 font-mono">${p.totalPrice.toLocaleString('es-CO')}</td>
                              <td className="py-4 px-4 text-sm text-slate-600 font-mono">${p.discountedPricePerDocument.toLocaleString('es-CO')}</td>
                              <td className="py-4 px-4">
                                <span className={`px-2 py-1 rounded-md text-xs font-bold ${p.isActive ? 'bg-emerald-100 text-emerald-700' : 'bg-slate-100 text-slate-500'}`}>
                                  {p.isActive ? 'Activo' : 'Inactivo'}
                                </span>
                              </td>
                              <td className="py-4 px-4">
                                <div className="flex items-center justify-center gap-1">
                                  <button
                                    onClick={() => handleActivateBag(p.id)}
                                    disabled={!p.isActive || activatingBagPackageId === p.id}
                                    title="Activar bolsa para este Client"
                                    className="px-3 py-1.5 bg-blue-50 text-blue-700 rounded-lg text-xs font-bold hover:bg-blue-100 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
                                  >
                                    {activatingBagPackageId === p.id ? 'Activando...' : 'Activar bolsa'}
                                  </button>
                                  <button onClick={() => openEditPackageModal(p)} className="p-2 text-slate-500 hover:bg-slate-100 rounded-lg transition-colors" title="Editar">
                                    <Pencil size={16} />
                                  </button>
                                  <button onClick={() => handleTogglePackageActive(p)} className="p-2 text-slate-500 hover:bg-slate-100 rounded-lg transition-colors" title={p.isActive ? 'Desactivar' : 'Reactivar'}>
                                    {p.isActive ? <PowerOff size={16} /> : <Power size={16} />}
                                  </button>
                                </div>
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  )}
                </div>

                <div className="bg-white rounded-3xl p-8 shadow-sm border border-slate-100">
                  <h2 className="text-xl font-bold text-slate-800 mb-1">Bolsas del Client</h2>
                  <p className="text-slate-500 text-sm mb-6">Bolsas activadas a partir de un paquete, con su saldo restante.</p>

                  {prepaidBags.length === 0 ? (
                    <div className="flex flex-col items-center justify-center text-center h-40 border-2 border-dashed border-slate-200 rounded-2xl bg-slate-50/50">
                      <p className="text-slate-500">Este Client todavía no tiene ninguna bolsa activada.</p>
                    </div>
                  ) : (
                    <div className="overflow-x-auto">
                      <table className="w-full text-left border-collapse">
                        <thead>
                          <tr className="bg-slate-50 text-slate-500 text-sm border-y border-slate-200">
                            <th className="font-semibold py-3 px-4 rounded-tl-xl">Paquete</th>
                            <th className="font-semibold py-3 px-4">Integrador</th>
                            <th className="font-semibold py-3 px-4">Saldo restante</th>
                            <th className="font-semibold py-3 px-4">Tarifa preferencial</th>
                            <th className="font-semibold py-3 px-4">Pagado</th>
                            <th className="font-semibold py-3 px-4">Estado</th>
                            <th className="font-semibold py-3 px-4 rounded-tr-xl">Activada</th>
                          </tr>
                        </thead>
                        <tbody>
                          {prepaidBags.map((b: any) => (
                            <tr key={b.id} className="border-b border-slate-100 hover:bg-slate-50/50 transition-colors">
                              <td className="py-4 px-4 font-semibold text-slate-700">{b.packageName}</td>
                              <td className="py-4 px-4 text-sm text-slate-600">{b.integratorName}</td>
                              <td className="py-4 px-4 text-sm text-slate-600 font-mono">${b.remainingBalance.toLocaleString('es-CO')}</td>
                              <td className="py-4 px-4 text-sm text-slate-600 font-mono">${b.discountedPricePerDocument.toLocaleString('es-CO')}</td>
                              <td className="py-4 px-4 text-sm text-slate-600 font-mono">${b.amountPaid.toLocaleString('es-CO')}</td>
                              <td className="py-4 px-4">
                                <span className={`px-2 py-1 rounded-md text-xs font-bold ${
                                  b.status === 'Active' ? 'bg-emerald-100 text-emerald-700' : b.status === 'Depleted' ? 'bg-amber-100 text-amber-700' : 'bg-slate-100 text-slate-500'
                                }`}>
                                  {b.status === 'Active' ? 'Activa' : b.status === 'Depleted' ? 'Agotada' : 'Cancelada'}
                                </span>
                              </td>
                              <td className="py-4 px-4 text-sm text-slate-500">{new Date(b.purchasedAt).toLocaleDateString()}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  )}
                </div>
              </div>
            )}

            {activeTab === 'enablements' && (
              <div className="space-y-8">
                <div className="bg-white rounded-3xl p-8 shadow-sm border border-slate-100">
                  <div className="flex justify-between items-center mb-1">
                    <h2 className="text-xl font-bold text-slate-800">Tipos de Documento</h2>
                    <button onClick={handleSaveDocTypes} disabled={savingDocTypes} className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg font-medium text-sm transition-colors disabled:opacity-50">
                      {savingDocTypes ? 'Guardando...' : 'Guardar'}
                    </button>
                  </div>
                  <p className="text-slate-500 text-sm mb-6">Documentos que puede emitir este cliente.</p>
                  <div className="space-y-2">
                    {enabledDocTypes.map((d: any) => (
                      <label key={d.id} className="flex items-center gap-3 p-3 bg-slate-50 hover:bg-slate-100 rounded-xl cursor-pointer transition-colors">
                        <input type="checkbox" checked={d.enabled} onChange={() => toggleDocType(d.id)} className="w-4 h-4 accent-blue-600" />
                        <span className="text-sm font-mono text-slate-400 w-14">{d.code}</span>
                        <span className="text-sm font-medium text-slate-700">{d.name}</span>
                      </label>
                    ))}
                    {enabledDocTypes.length === 0 && <p className="text-sm text-slate-400">Cargando...</p>}
                  </div>
                </div>

                <div className="bg-white rounded-3xl p-8 shadow-sm border border-slate-100">
                  <div className="flex justify-between items-center mb-1">
                    <h2 className="text-xl font-bold text-slate-800">Retenciones</h2>
                    <button onClick={handleSaveRetentions} disabled={savingRetentions} className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg font-medium text-sm transition-colors disabled:opacity-50">
                      {savingRetentions ? 'Guardando...' : 'Guardar'}
                    </button>
                  </div>
                   <p className="text-slate-500 text-sm mb-6">Retenciones disponibles para este cliente.</p>
                   <div className="flex gap-2 mb-5">
                     {(['Invoice', 'Support'] as const).map(scope => (
                       <button key={scope} type="button" onClick={() => setRetentionScope(scope)} className={`px-4 py-2 rounded-lg text-sm font-semibold ${retentionScope === scope ? 'bg-blue-600 text-white' : 'bg-slate-100 text-slate-600'}`}>
                         {scope === 'Invoice' ? 'Facturas' : 'Documentos soporte'}
                       </button>
                     ))}
                   </div>
                  <div className="space-y-4">
                    {Object.entries(
                      enabledRetentions.reduce((groups: Record<string, any[]>, r: any) => {
                        (groups[r.groupLabel] ||= []).push(r);
                        return groups;
                      }, {})
                    ).map(([groupLabel, items]) => (
                      <div key={groupLabel}>
                        <h3 className="text-xs font-bold text-slate-500 uppercase mb-1.5">{groupLabel}</h3>
                        <div className="space-y-1">
                          {items.map((r: any) => (
                            <label key={r.id} className="flex items-center gap-3 p-2.5 bg-slate-50 hover:bg-slate-100 rounded-xl cursor-pointer transition-colors">
                              <input type="checkbox" checked={r.enabled} onChange={() => toggleRetention(r.id)} className="w-4 h-4 accent-blue-600" />
                              <span className="text-sm text-slate-700 flex-1">{r.name}</span>
                              <span className="text-xs font-mono text-slate-400">{r.taxCategory} {r.rate}%</span>
                            </label>
                          ))}
                        </div>
                      </div>
                    ))}
                    {enabledRetentions.length === 0 && <p className="text-sm text-slate-400">Cargando...</p>}
                  </div>
                </div>
              </div>
            )}

            {activeTab === 'reception' && (
              <div className="space-y-6">
                <ReceptionMailboxCard clientId={id!} />
                <div className="bg-white rounded-3xl p-8 shadow-sm border border-slate-100">
                  <h2 className="text-xl font-bold text-slate-800 mb-2">Eventos automáticos</h2>
                  <p className="text-slate-500 mb-6 text-sm">Selecciona los eventos que se crearán automáticamente al recibir un documento.</p>

                  <div className="space-y-3">
                    {([
                      ['autoSendAcuseRecibo', 'Acuse de Recibo'],
                      ['autoSendReciboBien', 'Recibo del Bien o Servicio'],
                      ['autoSendAceptacion', 'Aceptación Expresa'],
                      ['autoSendReclamo', 'Reclamo'],
                    ] as const).map(([field, label]) => (
                      <label key={field} className="flex items-center gap-3 p-3 bg-slate-50 rounded-xl border border-slate-100 cursor-pointer">
                        <input type="checkbox" checked={receptionSettings[field]}
                          onChange={e => setReceptionSettings({ ...receptionSettings, [field]: e.target.checked })}
                          className="w-5 h-5 rounded accent-blue-600" />
                        <span className="font-semibold text-slate-700 text-sm">{label}</span>
                      </label>
                    ))}
                  </div>

                  <button onClick={saveReceptionSettings} disabled={savingReception} className="mt-6 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white px-6 py-2.5 rounded-xl font-bold shadow-md transition-all">
                    {savingReception ? 'Guardando...' : 'Guardar eventos automáticos'}
                  </button>
                </div>
              </div>
            )}

            {activeTab === 'smtp' && (
              <div className="bg-white rounded-3xl p-8 shadow-sm border border-slate-100">
                <div className="flex items-center gap-2 mb-2">
                  <Mail className="text-blue-600" size={20} />
                  <h2 className="text-xl font-bold text-slate-800">Correo para Reenvío de Documentos</h2>
                </div>
                <p className="text-slate-500 mb-6 text-sm">
                  Configura, en nombre de este cliente, el SMTP propio para reenviarle a sus clientes un documento ya aprobado cuando emite directo a la DIAN (sin Dataico).
                  Si lo dejas vacío, se usa el SMTP del tenant (si lo tiene configurado).
                </p>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
                  <div>
                    <label className="block text-sm font-bold text-slate-700 mb-1.5">Servidor SMTP</label>
                    <input type="text" placeholder="smtp.gmail.com" className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl outline-none focus:ring-2 focus:ring-blue-500"
                      value={smtpSettings.smtpHost || ''} onChange={e => setSmtpSettings({ ...smtpSettings, smtpHost: e.target.value })} />
                  </div>
                  <div>
                    <label className="block text-sm font-bold text-slate-700 mb-1.5">Puerto</label>
                    <input type="number" className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl outline-none focus:ring-2 focus:ring-blue-500"
                      value={smtpSettings.smtpPort || 587} onChange={e => setSmtpSettings({ ...smtpSettings, smtpPort: parseInt(e.target.value) || 587 })} />
                  </div>
                  <div>
                    <label className="block text-sm font-bold text-slate-700 mb-1.5">Usuario</label>
                    <input type="text" className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl outline-none focus:ring-2 focus:ring-blue-500"
                      value={smtpSettings.smtpUser || ''} onChange={e => setSmtpSettings({ ...smtpSettings, smtpUser: e.target.value })} />
                  </div>
                  <div>
                    <label className="block text-sm font-bold text-slate-700 mb-1.5">
                      Contraseña {smtpSettings.hasPassword && <span className="text-emerald-600 font-normal">(ya guardada)</span>}
                    </label>
                    <input type="password" placeholder={smtpSettings.hasPassword ? '••••••••' : ''} className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl outline-none focus:ring-2 focus:ring-blue-500"
                      value={smtpPasswordDraft} onChange={e => setSmtpPasswordDraft(e.target.value)} />
                  </div>
                  <div>
                    <label className="block text-sm font-bold text-slate-700 mb-1.5">Correo remitente</label>
                    <input type="email" className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl outline-none focus:ring-2 focus:ring-blue-500"
                      value={smtpSettings.smtpFromEmail || ''} onChange={e => setSmtpSettings({ ...smtpSettings, smtpFromEmail: e.target.value })} />
                  </div>
                  <div>
                    <label className="block text-sm font-bold text-slate-700 mb-1.5">Nombre remitente</label>
                    <input type="text" className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl outline-none focus:ring-2 focus:ring-blue-500"
                      value={smtpSettings.smtpFromName || ''} onChange={e => setSmtpSettings({ ...smtpSettings, smtpFromName: e.target.value })} />
                  </div>
                </div>

                <label className="flex items-center gap-2 mt-4 cursor-pointer">
                  <input type="checkbox" checked={!!smtpSettings.smtpUseSsl}
                    onChange={e => setSmtpSettings({ ...smtpSettings, smtpUseSsl: e.target.checked })}
                    className="w-4 h-4 rounded accent-blue-600" />
                  <span className="text-sm text-slate-600">Usar SSL/TLS (recomendado)</span>
                </label>

                <div className="flex gap-3 mt-6">
                  <button onClick={saveSmtpSettings} disabled={savingSmtp} className="bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white px-6 py-2.5 rounded-xl font-bold shadow-md transition-all">
                    {savingSmtp ? 'Guardando...' : 'Guardar configuración'}
                  </button>
                  <button onClick={testSmtpConnection} disabled={testingSmtpConn} className="bg-slate-100 hover:bg-slate-200 disabled:opacity-50 text-slate-700 px-6 py-2.5 rounded-xl font-bold transition-all">
                    {testingSmtpConn ? 'Probando...' : 'Probar conexión'}
                  </button>
                </div>
              </div>
            )}

            {activeTab === 'templates' && (
              <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
                <div className="md:col-span-1 space-y-4">
                  <h2 className="text-sm font-bold text-slate-400 uppercase tracking-wider mb-4">Tipos de Comprobante</h2>
                  {templateSettings.map(setting => (
                    <button
                      key={setting.settingId}
                      onClick={() => handleSelectTemplateType(setting)}
                      className={`w-full text-left p-5 rounded-2xl border transition-all flex items-center justify-between group ${
                        selectedTemplateSetting?.settingId === setting.settingId
                          ? 'bg-blue-50 border-blue-500 shadow-sm shadow-blue-500/10'
                          : 'bg-white border-slate-200 hover:border-blue-300 hover:bg-slate-50'
                      }`}
                    >
                      <div>
                        <div className="flex items-center gap-2 font-bold text-slate-700">
                          <LayoutTemplate className={`w-4 h-4 ${selectedTemplateSetting?.settingId === setting.settingId ? 'text-blue-600' : 'text-slate-400'}`} />
                          {setting.documentTypeName}
                        </div>
                        <div className="text-xs text-slate-500 mt-1 font-medium truncate pr-4">
                          Actual: {setting.selectedTemplateName || 'Por defecto'}
                        </div>
                      </div>
                      <ChevronRight className={`w-5 h-5 ${selectedTemplateSetting?.settingId === setting.settingId ? 'text-blue-600' : 'text-slate-300 group-hover:text-blue-600 transition-colors'}`} />
                    </button>
                  ))}
                  {templateSettings.length === 0 && (
                    <div className="p-6 bg-slate-50 border border-slate-200 rounded-2xl text-center text-slate-500 text-sm">
                      Este cliente aún no tiene comprobantes personalizables habilitados.
                    </div>
                  )}
                </div>

                <div className="md:col-span-2">
                  {selectedTemplateSetting ? (
                    <div className="bg-white rounded-3xl border border-slate-200 shadow-sm p-8 animate-in fade-in duration-300 h-full">
                      <h2 className="text-xl font-bold text-slate-800 mb-6">
                        Plantillas para {selectedTemplateSetting.documentTypeName}
                      </h2>

                      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                        {availableTemplates.map(tpl => {
                          const isActive = selectedTemplateSetting.selectedTemplateId === tpl.id;
                          return (
                            <div
                              key={tpl.id}
                              className={`relative p-6 rounded-2xl border-2 transition-all ${
                                isActive ? 'border-blue-500 bg-blue-50/50 shadow-md shadow-blue-500/10' : 'border-slate-100 bg-white'
                              }`}
                            >
                              {isActive && (
                                <div className="absolute -top-3 -right-3 bg-blue-600 text-white rounded-full p-1 shadow-md">
                                  <CheckCircle2 className="w-5 h-5" />
                                </div>
                              )}
                              <h3 className="font-bold text-slate-800">{tpl.name}</h3>
                              <p className="text-xs text-slate-500 mt-1">
                                {tpl.isOwn ? 'Diseño propio del cliente' : tpl.isGlobal ? 'Diseño base del sistema' : 'Diseño personalizado del tenant'}
                              </p>
                              {!isActive && (
                                <button
                                  onClick={() => handleApplyTemplate(tpl.id)}
                                  disabled={applyingTemplateId === tpl.id}
                                  className="mt-4 w-full py-2 bg-slate-800 hover:bg-slate-900 disabled:opacity-50 text-white text-sm font-bold rounded-xl transition-colors"
                                >
                                  {applyingTemplateId === tpl.id ? 'Aplicando...' : 'Aplicar'}
                                </button>
                              )}
                            </div>
                          );
                        })}
                        {availableTemplates.length === 0 && (
                          <div className="col-span-2 p-12 text-center text-slate-500 bg-slate-50 rounded-2xl border border-slate-100 border-dashed">
                            No hay plantillas disponibles para este tipo de documento. Clónalas o publícalas desde "Modelos de Documentos".
                          </div>
                        )}
                      </div>
                    </div>
                  ) : (
                    <div className="h-full min-h-[400px] rounded-3xl border-2 border-dashed border-slate-200 bg-slate-50 flex items-center justify-center p-8 text-center">
                      <div className="max-w-xs">
                        <LayoutTemplate className="w-12 h-12 text-slate-300 mx-auto mb-4" />
                        <h3 className="text-lg font-bold text-slate-600 mb-2">Selecciona un documento</h3>
                        <p className="text-sm text-slate-400">Selecciona un documento para ver y aplicar sus diseños.</p>
                      </div>
                    </div>
                  )}
                </div>
              </div>
            )}

            {activeTab === 'dian' && (
              <div className="relative">
                {/* Splash Screen Overlay for DIAN Tab */}
                {(isHabilitating || habilitationStatus?.status === 'Approved') && (
                  <div className="absolute inset-0 bg-white/90 backdrop-blur-md z-20 flex items-center justify-center p-4 rounded-3xl min-h-[500px]">
                    <div className="bg-white rounded-3xl p-10 max-w-lg w-full shadow-2xl flex flex-col items-center text-center animate-in zoom-in-95 duration-300 border border-slate-100">
                      {habilitationStatus?.status === 'Approved' ? (
                        <>
                          <div className="w-24 h-24 bg-emerald-100 text-emerald-500 rounded-full flex items-center justify-center mb-6 shadow-inner ring-8 ring-emerald-50">
                            <svg className="w-12 h-12" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth="3" d="M5 13l4 4L19 7"></path></svg>
                          </div>
                          <h2 className="text-3xl font-black text-slate-800 mb-2">¡Habilitación Exitosa!</h2>
                          <p className="text-slate-500 mb-8 text-lg">El cliente ya está sincronizado y listo para emitir facturación electrónica en Producción.</p>
                          <button onClick={() => {
                            setHabilitationStatus({ ...habilitationStatus, status: 'Production' });
                            setClient({...client, isActive: true});
                          }} className="w-full bg-blue-600 hover:bg-blue-700 text-white py-4 rounded-2xl font-bold text-lg transition-all shadow-lg hover:shadow-xl hover:-translate-y-1">
                            Continuar gestionando cliente
                          </button>
                        </>
                      ) : (
                        <>
                          <div className="relative w-28 h-28 mb-8">
                            <div className="absolute inset-0 bg-blue-500/20 rounded-full animate-ping"></div>
                            <div className="absolute inset-2 bg-blue-500/20 rounded-full animate-pulse"></div>
                            <div className="absolute inset-0 flex items-center justify-center">
                              <Loader2 className="w-12 h-12 text-blue-600 animate-spin" />
                            </div>
                          </div>
                          <h2 className="text-2xl font-bold text-slate-800 mb-3">
                            {isHabilitating && !habilitationStatus?.progress ? 'Conectando con la DIAN...' : 'Configuración en Progreso'}
                          </h2>
                          
                          {/* Progress Bar Container */}
                          <div className="w-full mt-6 mb-4">
                            <div className="flex justify-between items-end mb-2">
                              <span className="text-sm font-bold text-blue-600">{habilitationStatus?.message || (isHabilitating ? 'Autenticando...' : '')}</span>
                              <span className="text-sm font-bold text-slate-500">{habilitationStatus?.progress || 0}%</span>
                            </div>
                            <div className="w-full h-3 bg-slate-100 rounded-full overflow-hidden">
                              <div 
                                className="h-full bg-blue-600 transition-all duration-500 ease-out rounded-full"
                                style={{ width: `${habilitationStatus?.progress || 0}%` }}
                              ></div>
                            </div>
                          </div>

                          <p className="text-slate-500 text-sm">
                            {isHabilitating && !habilitationStatus?.progress 
                              ? 'Extrayendo el identificador de software del cliente.' 
                              : 'Automatizando la configuración ante el ente fiscal. Esto puede tomar unos segundos.'}
                          </p>
                        </>
                      )}
                    </div>
                  </div>
                )}

                {/* Panel de Set de Pruebas — visible una vez el software propio quedó registrado
                    (status "Testing"), en vez del overlay de "en progreso" que antes se quedaba
                    pegado ahí para siempre porque "Testing" es un estado real, no transitorio. */}
                {habilitationStatus?.status === 'Testing' && (
                  <div className="bg-white rounded-3xl p-8 shadow-sm border border-slate-100 mb-8">
                    <div className="flex items-center gap-4 mb-6">
                      <div className="w-12 h-12 bg-amber-50 text-amber-600 rounded-xl flex items-center justify-center">
                        <Loader2 size={24} />
                      </div>
                      <div>
                        <h2 className="text-xl font-bold text-slate-800">Set de Pruebas DIAN</h2>
                        <p className="text-sm text-slate-500">{habilitationStatus?.message}</p>
                      </div>
                    </div>

                    {habilitationStatus?.testSet && (
                      <div className="grid grid-cols-3 gap-4 mb-6 text-center">
                        <div className="bg-slate-50 rounded-xl p-4">
                          <p className="text-2xl font-bold text-slate-800">{habilitationStatus.testSet.sentInvoices}/{habilitationStatus.testSet.requiredInvoices}</p>
                          <p className="text-xs text-slate-500 mt-1">Facturas enviadas</p>
                        </div>
                        <div className="bg-slate-50 rounded-xl p-4">
                          <p className="text-2xl font-bold text-slate-800">{habilitationStatus.testSet.sentDebitNotes}/{habilitationStatus.testSet.requiredDebitNotes}</p>
                          <p className="text-xs text-slate-500 mt-1">Notas débito enviadas</p>
                        </div>
                        <div className="bg-slate-50 rounded-xl p-4">
                          <p className="text-2xl font-bold text-slate-800">{habilitationStatus.testSet.sentCreditNotes}/{habilitationStatus.testSet.requiredCreditNotes}</p>
                          <p className="text-xs text-slate-500 mt-1">Notas crédito enviadas</p>
                        </div>
                      </div>
                    )}

                    <div className="flex flex-wrap gap-3">
                      <button
                        onClick={handlePreviewTestDocument}
                        disabled={loadingPreview}
                        className="bg-slate-800 hover:bg-slate-900 disabled:opacity-50 text-white px-6 py-3 rounded-xl font-bold shadow-md transition-all flex items-center gap-2"
                      >
                        {loadingPreview ? <Loader2 className="w-5 h-5 animate-spin" /> : null}
                        Vista previa del próximo documento
                      </button>
                      <button
                        onClick={handleSendTestDocument}
                        disabled={sendingTestDoc}
                        className="bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white px-6 py-3 rounded-xl font-bold shadow-md transition-all flex items-center gap-2"
                      >
                        {sendingTestDoc ? <Loader2 className="w-5 h-5 animate-spin" /> : <Zap size={18} />}
                        Enviar a la DIAN
                      </button>
                    </div>
                    <p className="text-xs text-slate-400 mt-2">
                      La vista previa arma y firma el XML sin enviarlo — no gasta cupo. "Enviar a la DIAN" sí gasta un intento real y no se puede deshacer.
                    </p>

                    {testDocPreview && (
                      <div className="mt-6 bg-slate-50 border border-slate-200 rounded-2xl p-4">
                        <div className="flex flex-wrap gap-4 mb-3 text-sm">
                          <span><strong>Tipo:</strong> {testDocPreview.documentKind}</span>
                          <span><strong>Número:</strong> {testDocPreview.documentNumber}</span>
                          <span><strong>CUFE:</strong> <span className="font-mono">{testDocPreview.cufe}</span></span>
                        </div>
                        <pre className="text-xs bg-slate-900 text-slate-100 rounded-xl p-4 overflow-auto max-h-96 whitespace-pre-wrap break-all">{testDocPreview.signedXml}</pre>
                      </div>
                    )}

                    <div className="mt-6 bg-slate-50 border border-slate-200 rounded-2xl p-4">
                      <p className="text-sm font-bold text-slate-700 mb-2">Consultar un envío anterior por trackId (ZipKey)</p>
                      <div className="flex gap-2">
                        <input
                          type="text"
                          value={manualTrackId}
                          onChange={e => setManualTrackId(e.target.value)}
                          placeholder="ZipKey devuelto por la DIAN al enviar"
                          className="flex-1 px-4 py-2 border border-slate-200 rounded-xl text-sm outline-none font-mono"
                        />
                        <button
                          onClick={handleCheckManualTrackId}
                          disabled={checkingManualTrackId}
                          className="bg-slate-800 hover:bg-slate-900 disabled:opacity-50 text-white text-sm px-4 py-2 rounded-lg font-bold transition-all flex items-center gap-2"
                        >
                          {checkingManualTrackId ? <Loader2 className="w-4 h-4 animate-spin" /> : null}
                          Ver estado
                        </button>
                      </div>
                      {manualTrackIdOutcome && (
                        manualTrackIdOutcome.resolved ? (
                          <span className={`inline-block mt-3 text-sm font-bold px-3 py-1 rounded-full ${manualTrackIdOutcome.accepted ? 'bg-emerald-100 text-emerald-700' : 'bg-rose-100 text-rose-700'}`}>
                            {manualTrackIdOutcome.accepted ? 'Aceptado por la DIAN' : `Rechazado: ${manualTrackIdOutcome.rules?.join(' | ') || manualTrackIdOutcome.statusDescription}`}
                          </span>
                        ) : (
                          <p className="mt-3 text-sm text-slate-500">La DIAN todavía la está validando — intenta de nuevo en un momento.</p>
                        )
                      )}
                    </div>

                    {sentTestDocs.length > 0 && (
                      <div className="mt-6 space-y-2">
                        <p className="text-sm font-bold text-slate-700">Documentos enviados en esta sesión</p>
                        {sentTestDocs.map(doc => (
                          <div key={doc.trackId} className="bg-slate-50 border border-slate-200 rounded-xl p-4 flex items-center justify-between gap-4 flex-wrap">
                            <div className="text-sm">
                              <span className="font-bold">{doc.documentNumber}</span>
                              <span className="text-slate-400 mx-2">·</span>
                              <span className="text-xs text-slate-400">ZipKey:</span>{' '}
                              <span className="font-mono text-xs text-slate-500">{doc.trackId}</span>
                            </div>
                            {doc.outcome ? (
                              doc.outcome.resolved ? (
                                <span className={`text-sm font-bold px-3 py-1 rounded-full ${doc.outcome.accepted ? 'bg-emerald-100 text-emerald-700' : 'bg-rose-100 text-rose-700'}`}>
                                  {doc.outcome.accepted ? 'Aceptado por la DIAN' : `Rechazado: ${doc.outcome.rules?.join(' | ') || doc.outcome.statusDescription}`}
                                </span>
                              ) : (
                                <span className="text-sm text-slate-500">La DIAN todavía la está validando — intenta de nuevo en un momento.</span>
                              )
                            ) : (
                              <button
                                onClick={() => handleCheckTestDocStatus(doc.trackId)}
                                disabled={doc.checking}
                                className="bg-slate-800 hover:bg-slate-900 disabled:opacity-50 text-white text-sm px-4 py-2 rounded-lg font-bold transition-all flex items-center gap-2"
                              >
                                {doc.checking ? <Loader2 className="w-4 h-4 animate-spin" /> : null}
                                Ver estado
                              </button>
                            )}
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                )}

                {/* Panel de Habilitación DIAN Automática */}
                <div className="bg-white rounded-3xl p-8 shadow-sm border border-slate-100 min-h-[400px]">
                  <div className="flex items-center gap-4 mb-8">
                    <div className="w-12 h-12 bg-blue-50 text-blue-600 rounded-xl flex items-center justify-center">
                      <Zap size={24} />
                    </div>
                    <div>
                      <h2 className="text-xl font-bold text-slate-800">Habilitación y Set de Pruebas</h2>
                      <p className="text-sm text-slate-500">Completa la habilitación con el enlace enviado por la DIAN.</p>
                    </div>
                  </div>

                  {habilitationStatus?.status === 'Production' ? (
                    <div className="bg-emerald-50 border border-emerald-100 rounded-2xl p-6 flex items-center gap-4">
                      <div className="w-12 h-12 bg-emerald-500 text-white rounded-full flex items-center justify-center shadow-lg shadow-emerald-500/30 shrink-0">
                        <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth="3" d="M5 13l4 4L19 7"></path></svg>
                      </div>
                      <div>
                        <h3 className="font-bold text-emerald-900 text-lg">Cliente Habilitado en Producción</h3>
                        <p className="text-emerald-700/80">Este cliente está habilitado para emitir documentos en producción.</p>
                      </div>
                    </div>
                  ) : (
                    <form onSubmit={handleStartHabilitation} className="bg-slate-50 border border-slate-200 rounded-2xl p-6">
                      <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-4">
                        <div>
                          <label className="block text-sm font-bold text-slate-700 mb-2">Software ID (MUISCA)</label>
                          <input
                            type="text"
                            readOnly
                            placeholder="Se completa automáticamente al registrar"
                            className="w-full px-4 py-3 bg-slate-100 border border-slate-300 rounded-xl outline-none font-mono text-sm text-slate-600 cursor-not-allowed"
                            value={habilitationStatus?.softwareId || ''}
                          />
                        </div>
                        <div>
                          <label className="block text-sm font-bold text-slate-700 mb-2">PIN del Software</label>
                          <input
                            type="text"
                            readOnly
                            placeholder="Se completa automáticamente al registrar"
                            className="w-full px-4 py-3 bg-slate-100 border border-slate-300 rounded-xl outline-none font-mono text-sm text-slate-600 cursor-not-allowed"
                            value={habilitationStatus?.softwarePin || ''}
                          />
                        </div>
                      </div>

                      <label className="block text-sm font-bold text-slate-700 mb-3">Enlace Mágico de Acceso (Token DIAN)</label>
                      <div className="flex flex-col gap-4">
                        <input 
                          type="url" 
                          required 
                          placeholder="https://catalogo-vpfe.dian.gov.co/User/Login?token=..." 
                          className="w-full px-4 py-3 bg-white border border-slate-300 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none transition-all"
                          value={magicLink}
                          onChange={e => setMagicLink(e.target.value)}
                          disabled={isHabilitating}
                        />
                        <button 
                          type="submit" 
                          disabled={isHabilitating || !magicLink}
                          className="bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white px-8 py-3 rounded-xl font-bold shadow-md transition-all flex items-center justify-center gap-2"
                        >
                          {isHabilitating ? <Loader2 className="w-5 h-5 animate-spin" /> : <Zap size={18} />}
                          {isHabilitating ? 'Conectando...' : 'Guardar e Iniciar Automatización'}
                        </button>
                      </div>
                      <p className="text-sm text-slate-500 mt-4">
                        El sistema registra el Software Propio en la DIAN por ti — el Software ID y el PIN los asigna la DIAN y se muestran arriba una vez completado el registro.
                      </p>
                    </form>
                  )}
                </div>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Modal Crear Resolución */}
      {showResModal && (
        <div className="fixed inset-0 bg-slate-900/40 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-3xl p-8 max-w-2xl w-full shadow-2xl">
            <div className="flex justify-between items-center mb-6">
              <h3 className="text-xl font-bold text-slate-800 flex items-center gap-2">
                <FileSignature className="text-blue-600" /> {editingResolutionId ? 'Editar Resolución' : 'Nueva Resolución'}
              </h3>
              <button onClick={closeResModal} className="text-slate-400 hover:text-slate-600 transition-colors">
                <X size={24} />
              </button>
            </div>

            <form onSubmit={handleCreateResolution} className="space-y-4">
              {!editingResolutionId && (
                <div className="bg-blue-50/50 border border-blue-100 rounded-xl p-4 flex items-center justify-between">
                  <div>
                    <h4 className="text-sm font-semibold text-blue-800">Autocompletar con PDF</h4>
                    <p className="text-xs text-blue-600/70 mt-0.5">Sube el Formulario 1876 de la DIAN para extraer los datos.</p>
                  </div>
                  <div>
                    <label className="cursor-pointer bg-white text-blue-600 border border-blue-200 hover:border-blue-400 px-4 py-2 rounded-lg text-sm font-medium transition-colors shadow-sm flex items-center gap-2">
                      {uploadingPdf ? <Loader2 className="w-4 h-4 animate-spin" /> : <FileSignature size={16} />}
                      {uploadingPdf ? 'Leyendo...' : 'Cargar PDF'}
                      <input type="file" accept="application/pdf" className="hidden" onChange={handlePdfUpload} disabled={uploadingPdf} />
                    </label>
                  </div>
                </div>
              )}

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-semibold text-slate-700 mb-1">Tipo de Documento</label>
                  {editingResolutionId ? (
                    <div className="w-full px-4 py-2.5 bg-slate-100 border border-slate-200 rounded-xl text-slate-500 cursor-not-allowed" title="El tipo de documento no se puede cambiar al editar — crea una resolución nueva si necesitas otro tipo.">
                      {documentTypeLabels[newRes.documentType] || newRes.documentType}
                    </div>
                  ) : (
                    <select required className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none" value={newRes.documentType} onChange={e => setNewRes({...newRes, documentType: e.target.value})}>
                      <option value="FE">Factura Electrónica (FE)</option>
                      <option value="NC">Nota Crédito (NC)</option>
                      <option value="ND">Nota Débito (ND)</option>
                      <option value="DS">Documento Soporte - Adquisiciones a No Obligados (DS)</option>
                      <option value="NE">Nómina Electrónica (NE)</option>
                    </select>
                  )}
                </div>
                {newRes.documentType !== 'NE' && (
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1">Número de Resolución</label>
                    <input required type="text" className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none" value={newRes.resolutionNumber} onChange={e => setNewRes({...newRes, resolutionNumber: e.target.value})} />
                  </div>
                )}
              </div>

              {newRes.documentType === 'NE' && (
                <p className="text-xs text-slate-500 bg-slate-50 border border-slate-200 rounded-xl p-3">
                  La nómina electrónica no tiene una resolución de numeración autorizada por la DIAN — el consecutivo lo administra libremente el cliente. Solo indica desde qué número quiere empezar.
                </p>
              )}

              <div className={`grid ${newRes.documentType === 'NE' ? 'grid-cols-2' : 'grid-cols-3'} gap-4`}>
                <div>
                  <label className="block text-sm font-semibold text-slate-700 mb-1">Prefijo (Opcional)</label>
                  <input type="text" className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none uppercase" value={newRes.prefix} onChange={e => setNewRes({...newRes, prefix: e.target.value})} />
                </div>
                <div>
                  <label className="block text-sm font-semibold text-slate-700 mb-1">{newRes.documentType === 'NE' ? 'Número Inicial' : 'Rango Inicial'}</label>
                  <input required type="number" min="1" className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none" value={newRes.numberStart || ''} onChange={e => setNewRes({...newRes, numberStart: parseInt(e.target.value) || 0})} />
                </div>
                {newRes.documentType !== 'NE' && (
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1">Rango Final</label>
                    <input required type="number" min="1" className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none" value={newRes.numberEnd || ''} onChange={e => setNewRes({...newRes, numberEnd: parseInt(e.target.value) || 0})} />
                  </div>
                )}
              </div>

              {newRes.documentType !== 'NE' && (
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1">Válida Desde</label>
                    <input required type="date" className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none" value={newRes.validFrom} onChange={e => setNewRes({...newRes, validFrom: e.target.value})} />
                  </div>
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1">Válida Hasta</label>
                    <input required type="date" className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none" value={newRes.validTo} onChange={e => setNewRes({...newRes, validTo: e.target.value})} />
                  </div>
                </div>
              )}

              {newRes.documentType !== 'NE' && (
                <div>
                  <label className="block text-sm font-semibold text-slate-700 mb-1">Clave Técnica (Solo FE)</label>
                  <input type="text" className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none font-mono text-sm" value={newRes.technicalKey} onChange={e => setNewRes({...newRes, technicalKey: e.target.value})} />
                </div>
              )}

              {branches.filter(b => b.isActive).length > 1 && (
                <div>
                  <label className="block text-sm font-semibold text-slate-700 mb-2">Sucursales donde se usa</label>
                  <div className="space-y-1.5 border border-slate-200 rounded-xl p-3">
                    {branches.filter(b => b.isActive).map(b => (
                      <label key={b.id} className="flex items-center gap-2 text-sm text-slate-700 cursor-pointer">
                        <input type="checkbox" checked={resBranchIds.includes(b.id)} onChange={() => setResBranchIds(prev => prev.includes(b.id) ? prev.filter(x => x !== b.id) : [...prev, b.id])} />
                        {b.name}
                      </label>
                    ))}
                  </div>
                  {!editingResolutionId && <p className="text-xs text-slate-400 mt-1">Sin elegir, usa las sucursales de la resolución que reemplaza o, si no hay, la principal.</p>}
                </div>
              )}

              <div className="pt-4 flex justify-end gap-3">
                <button type="button" onClick={closeResModal} className="px-5 py-2.5 text-slate-500 hover:bg-slate-100 rounded-xl font-medium transition-colors">Cancelar</button>
                <button type="submit" className="bg-blue-600 hover:bg-blue-700 text-white px-6 py-2.5 rounded-xl font-semibold shadow-md transition-colors">
                  {editingResolutionId ? 'Guardar Cambios' : 'Guardar Resolución'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {showPackageModal && (
        <div className="fixed inset-0 bg-slate-900/40 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-3xl p-8 max-w-lg w-full shadow-2xl">
            <div className="flex justify-between items-center mb-6">
              <h3 className="text-xl font-bold text-slate-800 flex items-center gap-2">
                <Package className="text-blue-600" /> {editingPackage ? 'Editar Paquete' : 'Nuevo Paquete Prepago'}
              </h3>
              <button onClick={() => setShowPackageModal(false)} className="text-slate-400 hover:text-slate-600 transition-colors">
                <X size={24} />
              </button>
            </div>

            <form onSubmit={handleSavePackage} className="space-y-4">
              <div>
                <label className="block text-sm font-semibold text-slate-700 mb-1">Nombre del paquete</label>
                <input required type="text" placeholder="Ej: 1.000.000 documentos" className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none" value={packageForm.name} onChange={e => setPackageForm({ ...packageForm, name: e.target.value })} />
              </div>

              <div>
                <label className="block text-sm font-semibold text-slate-700 mb-1">Integrador</label>
                <select
                  required
                  disabled={!!editingPackage}
                  className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none disabled:opacity-60"
                  value={packageForm.integratorId}
                  onChange={e => setPackageForm({ ...packageForm, integratorId: e.target.value })}
                >
                  <option value="" disabled>Selecciona un integrador</option>
                  {integrators.map(i => <option key={i.id} value={i.id}>{i.name}</option>)}
                </select>
                {editingPackage && <p className="text-xs text-slate-400 mt-1">El integrador no se puede cambiar una vez creado el paquete.</p>}
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-semibold text-slate-700 mb-1">Precio total</label>
                  <input required type="number" min="1" step="1" className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none" value={packageForm.totalPrice || ''} onChange={e => setPackageForm({ ...packageForm, totalPrice: parseFloat(e.target.value) || 0 })} />
                  <p className="text-xs text-slate-400 mt-1">Lo que el Client paga de una vez.</p>
                </div>
                <div>
                  <label className="block text-sm font-semibold text-slate-700 mb-1">Tarifa por documento</label>
                  <input required type="number" min="1" step="1" className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none" value={packageForm.discountedPricePerDocument || ''} onChange={e => setPackageForm({ ...packageForm, discountedPricePerDocument: parseFloat(e.target.value) || 0 })} />
                  <p className="text-xs text-slate-400 mt-1">Tarifa preferencial mientras dure el saldo.</p>
                </div>
              </div>

              {packageForm.totalPrice > 0 && packageForm.discountedPricePerDocument > 0 && (
                <p className="text-sm text-slate-500 bg-slate-50 border border-slate-200 rounded-xl px-4 py-2.5">
                  Equivale a aproximadamente <span className="font-bold text-slate-700">{Math.floor(packageForm.totalPrice / packageForm.discountedPricePerDocument).toLocaleString('es-CO')}</span> documentos.
                </p>
              )}

              <div className="pt-4 flex justify-end gap-3">
                <button type="button" onClick={() => setShowPackageModal(false)} className="px-5 py-2.5 text-slate-500 hover:bg-slate-100 rounded-xl font-medium transition-colors">Cancelar</button>
                <button type="submit" disabled={savingPackage} className="bg-blue-600 hover:bg-blue-700 text-white px-6 py-2.5 rounded-xl font-semibold shadow-md transition-colors disabled:opacity-50">
                  {savingPackage ? 'Guardando...' : 'Guardar Paquete'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
