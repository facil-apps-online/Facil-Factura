import React, { useEffect, useState } from 'react';
import { Settings, FileText, CheckCircle2, ChevronRight, LayoutTemplate, Image, Palette, Copy, Upload, GitBranch } from 'lucide-react';
import { toast } from 'sonner';
import { api, getErrorMessage } from '../lib/api';

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
}

interface BrandingData {
  logoLightUrl: string;
  primaryColorLight: string;
}

export default function TemplateSettings() {
  const [settings, setSettings] = useState<ClientSetting[]>([]);
  const [selectedSetting, setSelectedSetting] = useState<ClientSetting | null>(null);
  const [availableTemplates, setAvailableTemplates] = useState<AvailableTemplate[]>([]);
  const [myTemplates, setMyTemplates] = useState<MyTemplate[]>([]);
  const [showCloneModal, setShowCloneModal] = useState<{ id: string, name: string } | null>(null);
  const [cloneData, setCloneData] = useState({ newName: '', newRepxTemplateKey: '' });
  const [showVersionModal, setShowVersionModal] = useState<string | null>(null);
  const [versionKey, setVersionKey] = useState('');
  const [branding, setBranding] = useState<BrandingData>({ logoLightUrl: '', primaryColorLight: '#2563eb' });
  const [savingBranding, setSavingBranding] = useState(false);
  const [uploadingLogo, setUploadingLogo] = useState(false);

  const loadBranding = () => {
    api.get<BrandingData & { hasCustomLogo?: boolean }>('/v1/branding/my-branding')
      .then(res => setBranding({
        logoLightUrl: res.data.logoLightUrl || '',
        primaryColorLight: res.data.primaryColorLight || '#2563eb'
      }))
      .catch(() => toast.error("No se pudo cargar el branding"));
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
    if (!window.confirm("¿Publicar esta plantilla? Quedará disponible para aplicarla a tus comprobantes.")) return;
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
      setBranding(prev => ({ ...prev, logoLightUrl: res.data.logoLightUrl }));
      toast.success('Logo actualizado correctamente');
    } catch (err: any) {
      toast.error(getErrorMessage(err, 'Error al subir el logo'));
    } finally {
      setUploadingLogo(false);
      e.target.value = '';
    }
  };

  const handleSaveBranding = async () => {
    setSavingBranding(true);
    try {
      await api.put('/v1/branding/my-branding', branding);
      toast.success("Tu identidad visual fue actualizada correctamente");
    } catch (err: any) {
      toast.error(getErrorMessage(err, "Error al guardar tu identidad"));
    } finally {
      setSavingBranding(false);
    }
  };

  return (
    <div className="p-8 animate-in fade-in slide-in-from-bottom-4 duration-500">
      <div className="mb-10">
        <h1 className="text-3xl font-extrabold text-slate-800 tracking-tight flex items-center gap-3">
          <LayoutTemplate className="w-8 h-8 text-primary" />
          Diseño de mis Facturas
        </h1>
        <p className="text-slate-500 mt-2 text-base font-medium">
          Personaliza el aspecto visual que verán tus clientes al recibir sus comprobantes electrónicos.
        </p>
      </div>

      {/* Identidad Visual del Cliente */}
      <div className="bg-white rounded-3xl border border-slate-200 p-8 mb-8">
        <h2 className="text-xl font-extrabold text-slate-800 flex items-center gap-2 mb-1">
          <Palette className="w-5 h-5 text-primary" /> Tu Identidad Visual
        </h2>
        <p className="text-sm text-slate-500 mb-6">
          Este logo y color identifican tu empresa en tu portal y en los comprobantes que emites.
        </p>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          <div>
            <label className="block text-sm font-semibold text-slate-700 mb-2 flex items-center gap-1">
              <Image className="w-4 h-4" /> Logo
            </label>
            <input
              type="file"
              accept="image/png,image/jpeg,image/webp,image/svg+xml"
              disabled={uploadingLogo}
              onChange={handleLogoUpload}
              className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-primary focus:border-transparent transition-all text-slate-800 file:mr-4 file:py-2 file:px-4 file:rounded-lg file:border-0 file:bg-primary file:text-white file:font-semibold disabled:opacity-50"
            />
            {uploadingLogo && <p className="text-xs text-slate-400 mt-2">Subiendo...</p>}
            {branding.logoLightUrl && (
              <div className="mt-3 p-4 bg-slate-50 rounded-xl border border-slate-200 flex items-center justify-center">
                <img src={branding.logoLightUrl} alt="Vista previa" className="max-h-16 max-w-full object-contain" onError={e => { (e.target as HTMLImageElement).style.display = 'none'; }} />
              </div>
            )}
          </div>
          <div>
            <label className="block text-sm font-semibold text-slate-700 mb-2 flex items-center gap-1">
              <Palette className="w-4 h-4" /> Color Principal
            </label>
            <div className="flex items-center gap-3">
              <input
                type="color"
                value={branding.primaryColorLight}
                onChange={e => setBranding({ ...branding, primaryColorLight: e.target.value })}
                className="w-14 h-14 rounded-xl border border-slate-200 cursor-pointer bg-slate-50 p-1"
              />
              <input
                type="text"
                value={branding.primaryColorLight}
                onChange={e => setBranding({ ...branding, primaryColorLight: e.target.value })}
                className="flex-1 px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-primary focus:border-transparent transition-all text-slate-800 font-mono"
              />
            </div>
            <p className="text-xs text-slate-400 mt-2">Este color se usa en los botones y enlaces de tu portal.</p>
          </div>
        </div>
        <div className="mt-6">
          <button
            onClick={handleSaveBranding}
            disabled={savingBranding}
            className="px-6 py-3 bg-primary text-white font-bold rounded-xl hover:opacity-90 transition-all shadow-lg shadow-blue-500/20 disabled:opacity-50"
          >
            {savingBranding ? 'Guardando...' : 'Guardar Identidad Visual'}
          </button>
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
        {/* Lista de Tipos de Documento */}
        <div className="md:col-span-1 space-y-4">
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
              <div>
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
        <div className="md:col-span-2">
          {selectedSetting ? (
            <div className="bg-white rounded-3xl border border-slate-200 shadow-sm p-8 animate-in fade-in duration-300 h-full">
              <h2 className="text-xl font-bold text-slate-800 mb-6 flex items-center gap-2">
                Plantillas para {selectedSetting.documentTypeName}
              </h2>
              
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                {availableTemplates.map(tpl => {
                  const isActive = selectedSetting.selectedTemplateId === tpl.id;
                  return (
                    <div
                      key={tpl.id}
                      className={`relative p-6 rounded-2xl border-2 transition-all ${
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
                        {!isActive && (
                          <button onClick={() => handleChooseTemplate(tpl.id)} className="flex-1 py-2 bg-slate-800 hover:bg-slate-900 text-white text-sm font-bold rounded-xl transition-colors">
                            Aplicar
                          </button>
                        )}
                        <button
                          onClick={() => { setCloneData({ newName: `${tpl.name} (Mi Versión)`, newRepxTemplateKey: '' }); setShowCloneModal({ id: tpl.id, name: tpl.name }); }}
                          title="Clonar y personalizar"
                          className={`py-2 ${isActive ? 'flex-1' : 'px-3'} bg-indigo-50 hover:bg-indigo-100 text-indigo-600 border border-indigo-200 text-sm font-bold rounded-xl transition-colors flex items-center justify-center gap-1`}
                        >
                          <Copy size={14} /> {isActive ? 'Clonar y Personalizar' : ''}
                        </button>
                      </div>
                    </div>
                  );
                })}

                {availableTemplates.length === 0 && (
                  <div className="col-span-2 p-12 text-center text-slate-500 bg-slate-50 rounded-2xl border border-slate-100 border-dashed">
                    No hay plantillas disponibles para este tipo de documento.
                  </div>
                )}
              </div>

              {myTemplates.filter(t => t.documentTypeId === selectedSetting.documentTypeId && t.scope === 'Propio').length > 0 && (
                <div className="mt-8 pt-8 border-t border-slate-100">
                  <h3 className="text-sm font-bold text-slate-400 uppercase tracking-wider mb-4">Mis Diseños Propios (todos los estados)</h3>
                  <div className="space-y-3">
                    {myTemplates.filter(t => t.documentTypeId === selectedSetting.documentTypeId && t.scope === 'Propio').map(t => (
                      <div key={t.id} className="flex items-center justify-between p-4 bg-slate-50 border border-slate-200 rounded-xl">
                        <div>
                          <div className="flex items-center gap-2">
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
                          <p className="text-xs text-slate-400 font-mono mt-1">Key: {t.repxTemplateKey || 'N/A'}</p>
                        </div>
                        <div className="flex gap-2">
                          {t.status === 'Draft' && (
                            <button onClick={() => handlePublishOwn(t.id)} className="px-3 py-1.5 text-xs font-bold text-white bg-primary hover:bg-primary/90 rounded-lg flex items-center gap-1">
                              <Upload size={14} /> Publicar
                            </button>
                          )}
                          {t.status === 'Published' && (
                            <button onClick={() => { setVersionKey(''); setShowVersionModal(t.id); }} className="px-3 py-1.5 text-xs font-bold text-blue-600 bg-blue-50 hover:bg-blue-100 border border-blue-200 rounded-lg flex items-center gap-1">
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
            <div className="h-full min-h-[400px] rounded-3xl border-2 border-dashed border-slate-200 bg-slate-50 flex items-center justify-center p-8 text-center">
              <div className="max-w-xs">
                <Settings className="w-12 h-12 text-slate-300 mx-auto mb-4" />
                <h3 className="text-lg font-bold text-slate-600 mb-2">Selecciona un Comprobante</h3>
                <p className="text-sm text-slate-400">Selecciona un tipo de comprobante en la lista de la izquierda para ver los diseños disponibles.</p>
              </div>
            </div>
          )}
        </div>
      </div>

      {showCloneModal && (
        <div className="fixed inset-0 bg-slate-900/40 backdrop-blur-sm flex items-center justify-center z-50 animate-in fade-in duration-200">
          <div className="bg-white rounded-3xl shadow-2xl p-8 w-full max-w-lg animate-in zoom-in-95 duration-200">
            <h2 className="text-2xl font-bold text-slate-800 mb-2">Clonar y Personalizar</h2>
            <p className="text-slate-500 mb-6 text-sm">Crea tu propia copia de "{showCloneModal.name}" para personalizarla. Quedará en borrador hasta que la publiques.</p>
            <form onSubmit={handleClone} className="space-y-5">
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
                <label className="block text-sm font-bold text-slate-700 mb-2">Llave REPX (Motor DevExpress)</label>
                <input
                  type="text"
                  required
                  className="w-full px-4 py-3 bg-slate-50 border border-slate-200 text-slate-800 rounded-xl focus:ring-2 focus:ring-primary focus:border-transparent outline-none transition-all font-mono text-sm"
                  value={cloneData.newRepxTemplateKey}
                  onChange={e => setCloneData({ ...cloneData, newRepxTemplateKey: e.target.value })}
                  placeholder="Ej. mi-cliente/factura-v1"
                />
              </div>
              <div className="flex justify-end gap-3 pt-6 border-t border-slate-100 mt-6">
                <button type="button" onClick={() => setShowCloneModal(null)} className="px-5 py-2.5 bg-slate-100 hover:bg-slate-200 text-slate-700 font-bold rounded-xl transition-colors">
                  Cancelar
                </button>
                <button type="submit" className="px-5 py-2.5 bg-primary hover:bg-primary/90 text-white font-bold rounded-xl shadow-md transition-all flex items-center">
                  <Copy className="w-5 h-5 mr-2" />
                  Guardar Clon
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {showVersionModal && (
        <div className="fixed inset-0 bg-slate-900/40 backdrop-blur-sm flex items-center justify-center z-50 animate-in fade-in duration-200">
          <div className="bg-white rounded-3xl shadow-2xl p-8 w-full max-w-lg animate-in zoom-in-95 duration-200">
            <h2 className="text-2xl font-bold text-slate-800 mb-2">Nueva Versión</h2>
            <p className="text-slate-500 mb-6 text-sm">Se creará un borrador de la siguiente versión. Tu plantilla publicada actual no se ve afectada hasta que publiques esta nueva versión.</p>
            <form onSubmit={handleNewVersion} className="space-y-5">
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-2">Nueva Llave REPX (Motor DevExpress)</label>
                <input
                  type="text"
                  required
                  className="w-full px-4 py-3 bg-slate-50 border border-slate-200 text-slate-800 rounded-xl focus:ring-2 focus:ring-primary focus:border-transparent outline-none transition-all font-mono text-sm"
                  value={versionKey}
                  onChange={e => setVersionKey(e.target.value)}
                  placeholder="Ej. mi-cliente/factura-v2"
                />
              </div>
              <div className="flex justify-end gap-3 pt-6 border-t border-slate-100 mt-6">
                <button type="button" onClick={() => setShowVersionModal(null)} className="px-5 py-2.5 bg-slate-100 hover:bg-slate-200 text-slate-700 font-bold rounded-xl transition-colors">
                  Cancelar
                </button>
                <button type="submit" className="px-5 py-2.5 bg-primary hover:bg-primary/90 text-white font-bold rounded-xl shadow-md transition-all flex items-center">
                  <GitBranch className="w-5 h-5 mr-2" />
                  Crear Versión
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
