import React, { useEffect, useState } from 'react';
import { Settings, FileText, CheckCircle2, ChevronRight, LayoutTemplate } from 'lucide-react';
import { toast } from 'sonner';
import { api } from '../lib/api';

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
}

export default function TemplateSettings() {
  const [settings, setSettings] = useState<ClientSetting[]>([]);
  const [selectedSetting, setSelectedSetting] = useState<ClientSetting | null>(null);
  const [availableTemplates, setAvailableTemplates] = useState<AvailableTemplate[]>([]);

  const loadSettings = () => {
    api.get<ClientSetting[]>('/client/templates/settings')
      .then(res => setSettings(res.data))
      .catch(() => toast.error("Error al cargar configuraciones"));
  };

  useEffect(() => {
    loadSettings();
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
      toast.error(err.response?.data || "Error al actualizar la plantilla");
    }
  };

  return (
    <div className="p-8 max-w-6xl mx-auto animate-in fade-in slide-in-from-bottom-4 duration-500">
      <div className="mb-10">
        <h1 className="text-3xl font-extrabold text-slate-800 tracking-tight flex items-center gap-3">
          <LayoutTemplate className="w-8 h-8 text-primary" />
          Diseño de mis Facturas
        </h1>
        <p className="text-slate-500 mt-2 text-base font-medium">
          Personaliza el aspecto visual que verán tus clientes al recibir sus comprobantes electrónicos.
        </p>
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
                      className={`relative p-6 rounded-2xl border-2 transition-all cursor-pointer ${
                        isActive 
                          ? 'border-primary bg-blue-50/50 shadow-md shadow-blue-500/10' 
                          : 'border-slate-100 hover:border-blue-200 bg-white hover:bg-slate-50'
                      }`}
                      onClick={() => !isActive && handleChooseTemplate(tpl.id)}
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
                        {tpl.isGlobal ? 'Diseño Base del Sistema' : 'Diseño Personalizado de tu Proveedor'}
                      </p>
                      
                      {!isActive && (
                        <button className="mt-4 w-full py-2 bg-slate-800 hover:bg-slate-900 text-white text-sm font-bold rounded-xl transition-colors">
                          Aplicar este Diseño
                        </button>
                      )}
                    </div>
                  );
                })}
                
                {availableTemplates.length === 0 && (
                  <div className="col-span-2 p-12 text-center text-slate-500 bg-slate-50 rounded-2xl border border-slate-100 border-dashed">
                    No hay plantillas disponibles para este tipo de documento.
                  </div>
                )}
              </div>
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
    </div>
  );
}
