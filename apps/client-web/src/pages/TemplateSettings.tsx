import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Settings, FileText, CheckCircle2, ChevronRight, LayoutTemplate, Image, Copy, Upload, GitBranch, Pencil, Eye, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { api, getErrorMessage } from '../lib/api';
import { buildNumberFormat, setDecimalSeparator, useNumberFormat } from '../lib/numberFormat';
import { useConfirm } from '@/components/ConfirmDialog';
import Modal from '../components/Modal';
import { Button } from '../components/ui/button';
import SmtpSettingsCard from '@shared/components/SmtpSettingsCard';

interface ClientSetting {
  settingId: string;
  documentTypeId: string;
  documentTypeName: string;
  selectedTemplateId: string | null;
  selectedTemplateName: string | null;
}

interface AvailableTemplate {
  id: string;
  name: string;
  isGlobal: boolean;
  isOwn?: boolean;
}

interface MyTemplate {
  id: string;
  name: string;
  repxTemplateKey: string;
  status: 'Draft' | 'Published' | 'Archived';
  versionNumber: number;
  documentTypeId: string;
  documentType: string;
  scope: 'Global' | 'Tenant' | 'Propio';
  mostrarRetenciones: boolean;
}

interface BrandingData {
  invoiceLogoUrl: string;
  unitOfMeasureDisplayOverride: string;
}

export default function TemplateSettings() {
  const confirm = useConfirm();
  const [settings, setSettings] = useState<ClientSetting[]>([]);
  const [selectedSetting, setSelectedSetting] = useState<ClientSetting | null>(null);
  const [availableTemplates, setAvailableTemplates] = useState<AvailableTemplate[]>([]);
  const [myTemplates, setMyTemplates] = useState<MyTemplate[]>([]);
  const [showCloneModal, setShowCloneModal] = useState<{ id: string, name: string } | null>(null);
  const [cloneData, setCloneData] = useState({ newName: '', newRepxTemplateKey: '' });
  const [showVersionModal, setShowVersionModal] = useState<string | null>(null);
  const [versionKey, setVersionKey] = useState('');
  const [previewingId, setPreviewingId] = useState<string | null>(null);
  const fmt = useNumberFormat();
  const [savingNumberFormat, setSavingNumberFormat] = useState(false);
  const [branding, setBranding] = useState<BrandingData>({ invoiceLogoUrl: '', unitOfMeasureDisplayOverride: '' });
  const [uploadingLogo, setUploadingLogo] = useState(false);
  const [savingUnitFormat, setSavingUnitFormat] = useState(false);

  const loadBranding = () => {
    api.get<{ invoiceLogoUrl?: string, unitOfMeasureDisplayOverride?: string }>('/v1/branding/my-branding')
      .then(res => setBranding({ invoiceLogoUrl: res.data.invoiceLogoUrl || '', unitOfMeasureDisplayOverride: res.data.unitOfMeasureDisplayOverride || '' }))
      .catch(() => toast.error("No se pudo cargar el branding"));
  };

  const handleSaveUnitFormat = async (value: string) => {
    setBranding(prev => ({ ...prev, unitOfMeasureDisplayOverride: value }));
    setSavingUnitFormat(true);
    try {
      await api.put('/v1/branding/my-branding', { unitOfMeasureDisplayOverride: value });
      toast.success('Formato de unidad de medida actualizado');
    } catch (err: any) {
      toast.error(getErrorMessage(err, 'Error al guardar el formato de unidad de medida'));
    } finally {
      setSavingUnitFormat(false);
    }
  };

  const handleSaveNumberFormat = async (value: '.' | ',') => {
    setSavingNumberFormat(true);
    try {
      await api.put('/v1/branding/my-branding', { decimalSeparator: value });
      setDecimalSeparator(value);
      toast.success('Formato de números actualizado');
    } catch (err: any) {
      toast.error(getErrorMessage(err, 'Error al guardar el formato de números'));
    } finally {
      setSavingNumberFormat(false);
    }
  };

  const loadSettings = () => {
    api.get<ClientSetting[]>('/client/templates/settings')
      .then(res => setSettings(res.data))
      .catch(() => toast.error("Error al cargar configuraciones"));
  };

  const loadMyTemplates = () => {
    api.get<MyTemplate[]>('/client/templates')
      .then(res => setMyTemplates(res.data))
      .catch(() => toast.error("Error al cargar tus plantillas"));
  };

  useEffect(() => {
    loadSettings();
    loadBranding();
    loadMyTemplates();
  }, []);

  const handleSelectType = async (setting: ClientSetting) => {
    setSelectedSetting(setting);
    try {
      const res = await api.get<AvailableTemplate[]>(`/client/templates/available/${setting.documentTypeId}`);
      setAvailableTemplates(res.data);
    } catch {
      toast.error("Error al cargar las plantillas disponibles");
    }
  };

  const handleClone = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!showCloneModal) return;
    try {
      await api.post(`/client/templates/${showCloneModal.id}/clone`, cloneData);
      toast.success("Plantilla clonada con éxito. Ahora tienes tu versión propia en borrador.");
      setCloneData({ newName: '', newRepxTemplateKey: '' });
      setShowCloneModal(null);
      loadMyTemplates();
    } catch (err: any) {
      toast.error(getErrorMessage(err, "Error al clonar la plantilla"));
    }
  };

  const handlePublishOwn = async (id: string) => {
    if (!(await confirm("¿Publicar esta plantilla? Quedará disponible para aplicarla a tus comprobantes.", { destructive: false, confirmText: 'Publicar' }))) return;
    try {
      await api.put(`/client/templates/${id}/publish`);
      toast.success("Plantilla publicada.");
      loadMyTemplates();
      if (selectedSetting) handleSelectType(selectedSetting);
    } catch (err: any) {
      toast.error(getErrorMessage(err, "Error al publicar"));
    }
  };

  const handleNewVersion = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!showVersionModal) return;
    try {
      await api.post(`/client/templates/${showVersionModal}/new-version`, { newRepxTemplateKey: versionKey });
      toast.success("Nueva versión creada en estado Borrador.");
      setVersionKey('');
      setShowVersionModal(null);
      loadMyTemplates();
    } catch (err: any) {
      toast.error(getErrorMessage(err, "Error al crear la versión"));
    }
  };

  const handleToggleMostrarRetenciones = async (template: MyTemplate) => {
    const next = !template.mostrarRetenciones;
    setMyTemplates(prev => prev.map(t => t.id === template.id ? { ...t, mostrarRetenciones: next } : t));
    try {
      await api.put(`/client/templates/${template.id}/mostrar-retenciones`, { mostrarRetenciones: next });
    } catch (err: any) {
      setMyTemplates(prev => prev.map(t => t.id === template.id ? { ...t, mostrarRetenciones: !next } : t));
      toast.error(getErrorMessage(err, "Error al actualizar la preferencia de retenciones"));
    }
  };

  const handlePreview = async (id: string) => {
    setPreviewingId(id);
    try {
      const res = await api.post(`/client/templates/${id}/preview`, {}, { responseType: 'blob' });
      const url = URL.createObjectURL(new Blob([res.data], { type: 'application/pdf' }));
      window.open(url, '_blank');
    } catch (err: any) {
      if (err.response?.data instanceof Blob) {
        const text = await err.response.data.text();
        toast.error(text || 'Error al generar la vista previa');
      } else {
        toast.error(getErrorMessage(err, 'Error al generar la vista previa'));
      }
    } finally {
      setPreviewingId(null);
    }
  };

  const handleChooseTemplate = async (templateId: string) => {
    if (!selectedSetting) return;
    try {
      await api.post('/client/templates/select', {
        documentTypeId: selectedSetting.documentTypeId,
        templateId
      });
      toast.success("Plantilla actualizada para tus futuras facturas");
      setSelectedSetting(null);
      loadSettings();
    } catch (err: any) {
      toast.error(getErrorMessage(err, "Error al actualizar la plantilla"));
    }
  };

  const handleLogoUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setUploadingLogo(true);
    try {
      const formData = new FormData();
      formData.append('file', file);
      const res = await api.post<{ logoLightUrl: string }>('/v1/branding/logo', formData, {
        headers: { 'Content-Type': 'multipart/form-data' }
      });
      setBranding(prev => ({ ...prev, invoiceLogoUrl: res.data.logoLightUrl }));
      toast.success('Logo actualizado correctamente');
    } catch (err: any) {
      toast.error(getErrorMessage(err, 'Error al subir el logo'));
    } finally {
      setUploadingLogo(false);
      e.target.value = '';
    }
  };

  return (
    <div className="p-4 sm:p-6 lg:p-8 animate-in fade-in slide-in-from-bottom-4 duration-500">
      <div className="mb-6 sm:mb-10">
        <h1 className="text-2xl sm:text-3xl font-extrabold text-slate-800 tracking-tight flex items-center gap-3">
          <LayoutTemplate className="w-8 h-8 text-primary" />
          Diseño de mis Facturas
        </h1>
        <p className="text-slate-500 mt-2 text-base font-medium">
          Personaliza el aspecto visual que verán tus clientes al recibir sus comprobantes electrónicos.
        </p>
      </div>

      {/* Logo del Cliente (solo para sus comprobantes — el portal usa la marca de tu proveedor) */}
      <div className="bg-white rounded-3xl border border-slate-200 p-4 sm:p-6 lg:p-8 mb-6 lg:mb-8">
        <h2 className="text-xl font-extrabold text-slate-800 flex items-center gap-2 mb-1">
          <Image className="w-5 h-5 text-primary" /> Logo de tus documentos
        </h2>
        <p className="text-sm text-slate-500 mb-6">
          Este logo aparece en las facturas y demás comprobantes electrónicos que emites. Se guarda automáticamente al seleccionarlo.
        </p>
        <div className="max-w-sm">
          <input
            type="file"
            accept="image/png,image/jpeg,image/webp,image/svg+xml"
            disabled={uploadingLogo}
            onChange={handleLogoUpload}
            className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-primary focus:border-transparent transition-all text-slate-800 file:mr-4 file:py-2 file:px-4 file:rounded-lg file:border-0 file:bg-primary file:text-white file:font-semibold disabled:opacity-50"
          />
          {uploadingLogo && <p className="text-xs text-slate-400 mt-2">Subiendo...</p>}
          {branding.invoiceLogoUrl && (
            <div className="mt-3 p-4 bg-slate-50 rounded-xl border border-slate-200 flex items-center justify-center">
              <img src={branding.invoiceLogoUrl} alt="Vista previa" className="max-h-16 max-w-full object-contain" onError={e => { (e.target as HTMLImageElement).style.display = 'none'; }} />
            </div>
          )}
        </div>
      </div>

      {/* Formato de la Unidad de Medida en el detalle de la factura */}
      <div className="bg-white rounded-3xl border border-slate-200 p-4 sm:p-6 lg:p-8 mb-6 lg:mb-8">
        <h2 className="text-xl font-extrabold text-slate-800 flex items-center gap-2 mb-1">
          <LayoutTemplate className="w-5 h-5 text-primary" /> Unidad de medida en tus facturas
        </h2>
        <p className="text-sm text-slate-500 mb-6">
          Cómo se imprime la unidad de cada producto en el detalle (ej. "94 - EA"). Por defecto usa lo que trae cada unidad en el catálogo; puedes forzar el mismo formato para todas.
        </p>
        <div className="max-w-sm">
          <select
            disabled={savingUnitFormat}
            value={branding.unitOfMeasureDisplayOverride}
            onChange={e => handleSaveUnitFormat(e.target.value)}
            className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-primary focus:border-transparent transition-all text-slate-800 appearance-none disabled:opacity-50"
          >
            <option value="">Automático (según el catálogo)</option>
            <option value="Combined">Código y sigla ("94 - EA")</option>
            <option value="CodeOnly">Solo código DIAN ("94")</option>
            <option value="AbbreviationOnly">Solo sigla ("EA")</option>
          </select>
        </div>
      </div>

      {/* Formato de números: portal y PDF */}
      <div className="bg-white rounded-3xl border border-slate-200 p-4 sm:p-6 lg:p-8 mb-6 lg:mb-8">
        <h2 className="text-xl font-extrabold text-slate-800 flex items-center gap-2 mb-1">
          <LayoutTemplate className="w-5 h-5 text-primary" /> Formato de números
        </h2>
        <p className="text-sm text-slate-500 mb-6">
          Cómo se escriben y se muestran los valores en el portal y en los PDF de tus documentos. No cambia los valores que se envían a la DIAN.
        </p>
        <div className="max-w-sm">
          <select
            disabled={savingNumberFormat}
            value={fmt.decimal}
            onChange={e => handleSaveNumberFormat(e.target.value as '.' | ',')}
            className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-primary focus:border-transparent transition-all text-slate-800 appearance-none disabled:opacity-50"
          >
            <option value=".">Punto decimal, coma de miles ({buildNumberFormat('.').money(1234567.89)})</option>
            <option value=",">Coma decimal, punto de miles ({buildNumberFormat(',').money(1234567.89)})</option>
          </select>
        </div>
      </div>

      <SmtpSettingsCard
        api={api}
        basePath="/client/smtp-settings"
        className="bg-white rounded-3xl border border-slate-200 p-4 sm:p-6 lg:p-8 mb-6 lg:mb-8"
        description={<>Si emites directo a la DIAN, este SMTP es el que se usa para reenviarle un documento ya aprobado a tu cliente. Si lo dejas vacío, se usa el SMTP de tu proveedor (si lo tiene configurado).</>}
      />

      <div className="grid grid-cols-1 md:grid-cols-3 gap-6 lg:gap-8">
        {/* Lista de Tipos de Documento */}
        <div className="min-w-0 md:col-span-1 space-y-4">
          <h2 className="text-sm font-bold text-slate-400 uppercase tracking-wider mb-4">Tipos de Comprobante</h2>
          {settings.map(setting => (
            <button
              key={setting.settingId}
              onClick={() => handleSelectType(setting)}
              className={`w-full text-left p-5 rounded-2xl border transition-all flex items-center justify-between group ${
                selectedSetting?.settingId === setting.settingId
                  ? 'bg-blue-50 border-primary shadow-sm shadow-blue-500/10'
                  : 'bg-white border-slate-200 hover:border-blue-300 hover:bg-slate-50'
              }`}
            >
              <div className="min-w-0 flex-1">
                <div className="flex items-center gap-2 font-bold text-slate-700">
                  <FileText className={`w-4 h-4 ${selectedSetting?.settingId === setting.settingId ? 'text-primary' : 'text-slate-400'}`} />
                  {setting.documentTypeName}
                </div>
                <div className="text-xs text-slate-500 mt-1 font-medium truncate pr-4">
                  Actual: {setting.selectedTemplateName || 'Por defecto'}
                </div>
              </div>
              <ChevronRight className={`w-5 h-5 ${selectedSetting?.settingId === setting.settingId ? 'text-primary' : 'text-slate-300 group-hover:text-primary transition-colors'}`} />
            </button>
          ))}
          {settings.length === 0 && (
            <div className="p-6 bg-slate-50 border border-slate-200 rounded-2xl text-center text-slate-500 text-sm">
              Tu proveedor aún no te ha habilitado comprobantes personalizables.
            </div>
          )}
        </div>

        {/* Catálogo de Plantillas */}
        <div className="min-w-0 md:col-span-2">
          {selectedSetting ? (
            <div className="bg-white rounded-3xl border border-slate-200 shadow-sm p-4 sm:p-6 lg:p-8 animate-in fade-in duration-300 h-full">
              <h2 className="text-xl font-bold text-slate-800 mb-6 flex items-center gap-2">
                Plantillas para {selectedSetting.documentTypeName}
              </h2>
              
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                {availableTemplates.map(tpl => {
                  const isActive = selectedSetting.selectedTemplateId === tpl.id;
                  return (
                    <div
                      key={tpl.id}
                      className={`relative p-4 sm:p-6 rounded-2xl border-2 transition-all ${
                        isActive
                          ? 'border-primary bg-blue-50/50 shadow-md shadow-blue-500/10'
                          : 'border-slate-100 bg-white'
                      }`}
                    >
                      {isActive && (
                        <div className="absolute -top-3 -right-3 bg-primary text-white rounded-full p-1 shadow-md">
                          <CheckCircle2 className="w-5 h-5" />
                        </div>
                      )}
                      
                      <div className="w-full h-32 bg-slate-100 rounded-xl mb-4 border border-slate-200/60 overflow-hidden flex items-center justify-center relative">
                        {/* Dummy preview thumbnail */}
                        <div className="w-3/4 h-3/4 bg-white shadow-sm border border-slate-200 p-2 opacity-80 flex flex-col gap-1">
                           <div className="h-2 w-1/3 bg-slate-200 rounded-full"></div>
                           <div className="h-10 w-full bg-slate-50 rounded mt-2"></div>
                           <div className="flex gap-1 mt-auto">
                              <div className="h-4 w-1/2 bg-slate-100 rounded"></div>
                              <div className="h-4 w-1/2 bg-slate-200 rounded"></div>
                           </div>
                        </div>
                      </div>
                      
                      <h3 className="font-bold text-slate-800 flex items-center justify-between">
                        {tpl.name}
                      </h3>
                      <p className="text-xs text-slate-500 mt-1">
                        {tpl.isOwn ? 'Tu Diseño Personalizado' : tpl.isGlobal ? 'Diseño Base del Sistema' : 'Diseño Personalizado de tu Proveedor'}
                      </p>

                      <div className="mt-4 flex gap-2">
                        <button
                          onClick={() => handlePreview(tpl.id)}
                          disabled={previewingId === tpl.id}
                          title="Vista previa"
                          aria-label="Vista previa"
                          className="min-h-11 min-w-11 px-3 py-2 bg-slate-100 hover:bg-slate-200 text-slate-600 text-sm font-bold rounded-xl transition-colors flex items-center justify-center disabled:opacity-50 md:min-h-0 md:min-w-0"
                        >
                          {previewingId === tpl.id ? <Loader2 size={14} className="animate-spin" /> : <Eye size={14} />}
                        </button>
                        {!isActive && (
                          <button onClick={() => handleChooseTemplate(tpl.id)} className="min-h-11 flex-1 py-2 bg-slate-800 hover:bg-slate-900 text-white text-sm font-bold rounded-xl transition-colors md:min-h-0">
                            Aplicar
                          </button>
                        )}
                        <button
                          onClick={() => { setCloneData({ newName: `${tpl.name} (Mi Versión)`, newRepxTemplateKey: '' }); setShowCloneModal({ id: tpl.id, name: tpl.name }); }}
                          title="Clonar y personalizar"
                          aria-label="Clonar y personalizar"
                          className={`min-h-11 min-w-11 py-2 md:min-h-0 md:min-w-0 ${isActive ? 'flex-1' : 'px-3'} bg-indigo-50 hover:bg-indigo-100 text-indigo-600 border border-indigo-200 text-sm font-bold rounded-xl transition-colors flex items-center justify-center gap-1`}
                        >
                          <Copy size={14} /> {isActive ? 'Copiar y personalizar' : ''}
                        </button>
                      </div>
                    </div>
                  );
                })}

                {availableTemplates.length === 0 && (
                  <div className="sm:col-span-2 p-8 sm:p-12 text-center text-slate-500 bg-slate-50 rounded-2xl border border-slate-100 border-dashed">
                    No hay plantillas disponibles para este tipo de documento.
                  </div>
                )}
              </div>

              {myTemplates.filter(t => t.documentTypeId === selectedSetting.documentTypeId && t.scope === 'Propio').length > 0 && (
                <div className="mt-8 pt-8 border-t border-slate-100">
                  <h3 className="text-sm font-bold text-slate-400 uppercase tracking-wider mb-4">Mis diseños</h3>
                  <div className="space-y-3">
                    {myTemplates.filter(t => t.documentTypeId === selectedSetting.documentTypeId && t.scope === 'Propio').map(t => (
                      <div key={t.id} className="flex flex-col gap-3 p-4 bg-slate-50 border border-slate-200 rounded-xl sm:flex-row sm:items-center sm:justify-between">
                        <div className="min-w-0">
                          <div className="flex flex-wrap items-center gap-2">
                            <span className="font-bold text-slate-700 text-sm">{t.name}</span>
                            <span className="font-mono text-xs px-2 py-0.5 rounded bg-slate-200 text-slate-600">v{t.versionNumber}</span>
                            <span className={`text-xs font-bold px-2 py-0.5 rounded-full ${
                              t.status === 'Published' ? 'bg-emerald-100 text-emerald-700' :
                              t.status === 'Draft' ? 'bg-amber-100 text-amber-700' :
                              'bg-slate-200 text-slate-600'
                            }`}>
                              {t.status === 'Published' ? 'Publicado' : t.status === 'Draft' ? 'Borrador' : 'Archivado'}
                            </span>
                          </div>
                          <p className="text-xs text-slate-400 font-mono mt-1 break-all">Key: {t.repxTemplateKey || 'N/A'}</p>
                          <label className="flex items-center gap-2 mt-2 py-1 text-xs font-medium text-slate-600 cursor-pointer w-fit">
                            <input
                              type="checkbox"
                              checked={t.mostrarRetenciones}
                              onChange={() => handleToggleMostrarRetenciones(t)}
                              className="rounded border-slate-300"
                            />
                            Mostrar retenciones en el pie del documento
                          </label>
                        </div>
                        <div className="flex flex-wrap gap-2">
                          <button onClick={() => handlePreview(t.id)} disabled={previewingId === t.id} className="min-h-11 px-3 py-1.5 text-xs font-bold text-slate-600 bg-slate-100 hover:bg-slate-200 rounded-lg flex items-center gap-1 disabled:opacity-50 md:min-h-0">
                            {previewingId === t.id ? <Loader2 size={14} className="animate-spin" /> : <Eye size={14} />} Vista Previa
                          </button>
                          {t.status === 'Draft' && (
                            <Link to={`/templates/editor?key=${encodeURIComponent(t.repxTemplateKey)}`} className="min-h-11 px-3 py-1.5 text-xs font-bold text-amber-600 bg-amber-50 hover:bg-amber-100 border border-amber-200 rounded-lg flex items-center gap-1 md:min-h-0">
                              <Pencil size={14} /> Editar
                            </Link>
                          )}
                          {t.status === 'Draft' && (
                            <button onClick={() => handlePublishOwn(t.id)} className="min-h-11 px-3 py-1.5 text-xs font-bold text-white bg-primary hover:bg-primary/90 rounded-lg flex items-center gap-1 md:min-h-0">
                              <Upload size={14} /> Publicar
                            </button>
                          )}
                          {t.status === 'Published' && (
                            <button onClick={() => { setVersionKey(''); setShowVersionModal(t.id); }} className="min-h-11 px-3 py-1.5 text-xs font-bold text-blue-600 bg-blue-50 hover:bg-blue-100 border border-blue-200 rounded-lg flex items-center gap-1 md:min-h-0">
                              <GitBranch size={14} /> Nueva Versión
                            </button>
                          )}
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>
          ) : (
            <div className="h-full min-h-[240px] md:min-h-[400px] rounded-3xl border-2 border-dashed border-slate-200 bg-slate-50 flex items-center justify-center p-8 text-center">
              <div className="max-w-xs">
                <Settings className="w-12 h-12 text-slate-300 mx-auto mb-4" />
                <h3 className="text-lg font-bold text-slate-600 mb-2">Selecciona un documento</h3>
                <p className="text-sm text-slate-400">Selecciona un documento para ver sus diseños.</p>
              </div>
            </div>
          )}
        </div>
      </div>

      {showCloneModal && (
        <Modal
          open
          onOpenChange={open => { if (!open) setShowCloneModal(null); }}
          title="Copiar y personalizar"
          description={`Crea tu propia copia de "${showCloneModal.name}" para personalizarla. Quedará en borrador hasta que la publiques.`}
          size="sm"
          preventDismiss
          footer={
            <>
              <Button type="button" variant="ghost" onClick={() => setShowCloneModal(null)}>Cancelar</Button>
              <Button type="submit" form="clone-form"><Copy className="w-5 h-5" /> Guardar Clon</Button>
            </>
          }
        >
          <form id="clone-form" onSubmit={handleClone} className="space-y-5">
            <div>
              <label className="block text-sm font-bold text-slate-700 mb-2">Nuevo Nombre</label>
              <input
                type="text"
                required
                className="w-full px-4 py-3 bg-slate-50 border border-slate-200 text-slate-800 rounded-xl focus:ring-2 focus:ring-primary focus:border-transparent outline-none transition-all"
                value={cloneData.newName}
                onChange={e => setCloneData({ ...cloneData, newName: e.target.value })}
              />
            </div>
            <div>
              <label className="block text-sm font-bold text-slate-700 mb-2">Identificador de plantilla</label>
              <input
                type="text"
                required
                className="w-full px-4 py-3 bg-slate-50 border border-slate-200 text-slate-800 rounded-xl focus:ring-2 focus:ring-primary focus:border-transparent outline-none transition-all font-mono text-sm"
                value={cloneData.newRepxTemplateKey}
                onChange={e => setCloneData({ ...cloneData, newRepxTemplateKey: e.target.value })}
                placeholder="Ej. mi-cliente/factura-v1"
              />
            </div>
          </form>
        </Modal>
      )}

      {showVersionModal && (
        <Modal
          open
          onOpenChange={open => { if (!open) setShowVersionModal(null); }}
          title="Nueva Versión"
          description="Se creará un borrador; la versión publicada seguirá activa hasta que publiques la nueva."
          size="sm"
          preventDismiss
          footer={
            <>
              <Button type="button" variant="ghost" onClick={() => setShowVersionModal(null)}>Cancelar</Button>
              <Button type="submit" form="version-form"><GitBranch className="w-5 h-5" /> Crear Versión</Button>
            </>
          }
        >
          <form id="version-form" onSubmit={handleNewVersion} className="space-y-5">
            <div>
              <label className="block text-sm font-bold text-slate-700 mb-2">Nuevo identificador de plantilla</label>
              <input
                type="text"
                required
                className="w-full px-4 py-3 bg-slate-50 border border-slate-200 text-slate-800 rounded-xl focus:ring-2 focus:ring-primary focus:border-transparent outline-none transition-all font-mono text-sm"
                value={versionKey}
                onChange={e => setVersionKey(e.target.value)}
                placeholder="Ej. mi-cliente/factura-v2"
              />
            </div>
          </form>
        </Modal>
      )}
    </div>
  );
}
