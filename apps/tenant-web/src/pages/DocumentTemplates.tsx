import React, { useEffect, useState } from 'react';
import { Copy, Save, FileText, Upload, Globe, User } from 'lucide-react';
import { toast } from 'sonner';
import { api } from '../lib/api';

interface DocumentTemplate {
  id: string;
  name: string;
  versionNumber: number;
  status: 'Draft' | 'Published' | 'Archived';
  documentType: string;
  isGlobal: boolean;
  clonedFromId?: string;
  repxTemplateKey: string;
}

export default function DocumentTemplates() {
  const [templates, setTemplates] = useState<DocumentTemplate[]>([]);
  const [showCloneModal, setShowCloneModal] = useState<string | null>(null);
  const [cloneData, setCloneData] = useState({ newName: '', newRepxTemplateKey: '' });

  const loadTemplates = () => {
    api.get<DocumentTemplate[]>('/tenant/templates')
      .then(res => setTemplates(res.data))
      .catch(() => toast.error("Error al cargar las plantillas"));
  };

  useEffect(() => {
    loadTemplates();
  }, []);

  const handleClone = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!showCloneModal) return;
    try {
      await api.post(`/tenant/templates/${showCloneModal}/clone`, cloneData);
      toast.success("Plantilla clonada con éxito. Ahora tienes tu versión propia.");
      setCloneData({ newName: '', newRepxTemplateKey: '' });
      setShowCloneModal(null);
      loadTemplates();
    } catch (err: any) {
      toast.error(err.response?.data || "Error al clonar la plantilla");
    }
  };

  const handlePublish = async (id: string) => {
    if (!window.confirm("¿Seguro que deseas publicar esta plantilla? Se archivará la anterior.")) return;
    try {
      await api.put(`/tenant/templates/${id}/publish`);
      toast.success("Plantilla publicada.");
      loadTemplates();
    } catch (err: any) {
      toast.error(err.response?.data || "Error al publicar");
    }
  };

  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'Published': return <span className="bg-emerald-100 text-emerald-700 px-3 py-1 rounded-full text-xs font-bold border border-emerald-200">Publicado</span>;
      case 'Draft': return <span className="bg-amber-100 text-amber-700 px-3 py-1 rounded-full text-xs font-bold border border-amber-200">Borrador</span>;
      case 'Archived': return <span className="bg-slate-200 text-slate-700 px-3 py-1 rounded-full text-xs font-bold border border-slate-300">Archivado</span>;
      default: return null;
    }
  };

  return (
    <div className="p-8 animate-in fade-in duration-500 max-w-7xl mx-auto">
      <div className="flex justify-between items-end mb-8">
        <div>
          <h1 className="text-3xl font-extrabold text-slate-800 tracking-tight flex items-center gap-3">
            <FileText className="w-8 h-8 text-primary" />
            Diseños y Plantillas
          </h1>
          <p className="text-slate-500 mt-2 text-base font-medium">Visualiza los diseños globales o clónalos para personalizarlos para tus clientes.</p>
        </div>
      </div>

      <div className="bg-white rounded-3xl shadow-sm border border-slate-200 overflow-hidden">
        <table className="w-full text-left">
          <thead className="bg-slate-50 border-b border-slate-200">
            <tr>
              <th className="px-6 py-4 text-xs font-bold text-slate-500 uppercase tracking-wider">Tipo (Origen)</th>
              <th className="px-6 py-4 text-xs font-bold text-slate-500 uppercase tracking-wider">Nombre del Diseño</th>
              <th className="px-6 py-4 text-xs font-bold text-slate-500 uppercase tracking-wider">Versión</th>
              <th className="px-6 py-4 text-xs font-bold text-slate-500 uppercase tracking-wider">Estado</th>
              <th className="px-6 py-4 text-right">Acciones</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {templates.map(tpl => (
              <tr key={tpl.id} className="hover:bg-slate-50/50 transition-colors group">
                <td className="px-6 py-4">
                  <div className="flex items-center gap-2">
                    {tpl.isGlobal ? (
                      <span className="flex items-center gap-1 text-xs font-bold text-indigo-600 bg-indigo-50 px-2 py-1 rounded-md border border-indigo-100">
                        <Globe size={14} /> Global
                      </span>
                    ) : (
                      <span className="flex items-center gap-1 text-xs font-bold text-primary bg-blue-50 px-2 py-1 rounded-md border border-blue-100">
                        <User size={14} /> Propio
                      </span>
                    )}
                    <span className="text-sm font-semibold text-slate-600">{tpl.documentType}</span>
                  </div>
                </td>
                <td className="px-6 py-4">
                  <div className="font-bold text-slate-800">
                    {tpl.name}
                  </div>
                  <div className="text-xs text-slate-400 font-mono mt-1">Key: {tpl.repxTemplateKey || 'N/A'}</div>
                </td>
                <td className="px-6 py-4">
                  <span className="inline-flex items-center px-2 py-1 rounded-lg text-sm font-bold bg-slate-100 text-slate-600 font-mono">
                    v{tpl.versionNumber}
                  </span>
                </td>
                <td className="px-6 py-4">
                  {getStatusBadge(tpl.status)}
                </td>
                <td className="px-6 py-4 text-right space-x-2">
                  {tpl.isGlobal && tpl.status === 'Published' && (
                    <button 
                      onClick={() => {
                        setCloneData({ newName: `${tpl.name} (Mi Versión)`, newRepxTemplateKey: '' });
                        setShowCloneModal(tpl.id);
                      }} 
                      className="px-4 py-2 text-sm font-bold text-indigo-600 bg-indigo-50 hover:bg-indigo-100 border border-indigo-200 rounded-xl transition-all shadow-sm flex items-center inline-flex"
                    >
                      <Copy className="w-4 h-4 mr-2" />
                      Clonar (Fork)
                    </button>
                  )}
                  
                  {!tpl.isGlobal && tpl.status === 'Draft' && (
                    <button 
                      onClick={() => handlePublish(tpl.id)} 
                      className="px-4 py-2 text-sm font-bold text-white bg-primary hover:bg-blue-600 rounded-xl transition-all shadow-md shadow-blue-500/20 flex items-center inline-flex"
                    >
                      <Upload className="w-4 h-4 mr-2" />
                      Publicar
                    </button>
                  )}
                </td>
              </tr>
            ))}
            {templates.length === 0 && (
              <tr>
                <td colSpan={5} className="px-6 py-12 text-center text-slate-500 font-medium bg-slate-50/50">
                  No hay plantillas registradas. El Superadmin debe publicar diseños base.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {/* Modal Clonado */}
      {showCloneModal && (
        <div className="fixed inset-0 bg-slate-900/40 backdrop-blur-sm flex items-center justify-center z-50 animate-in fade-in duration-200">
          <div className="bg-white rounded-3xl shadow-2xl p-8 w-full max-w-lg animate-in zoom-in-95 duration-200">
            <h2 className="text-2xl font-bold text-slate-800 mb-2">Clonar Diseño Base</h2>
            <p className="text-slate-500 mb-6 text-sm">Crea una copia privada de este diseño global para personalizarla para tus clientes.</p>
            <form onSubmit={handleClone} className="space-y-5">
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-2">Nuevo Nombre</label>
                <input 
                  type="text" 
                  required 
                  className="w-full px-4 py-3 bg-slate-50 border border-slate-200 text-slate-800 rounded-xl focus:ring-2 focus:ring-primary focus:border-transparent outline-none transition-all" 
                  value={cloneData.newName}
                  onChange={e => setCloneData({...cloneData, newName: e.target.value})}
                />
              </div>
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-2">Llave REPX (Motor DevExpress)</label>
                <input 
                  type="text" 
                  required 
                  className="w-full px-4 py-3 bg-slate-50 border border-slate-200 text-slate-800 rounded-xl focus:ring-2 focus:ring-primary focus:border-transparent outline-none transition-all font-mono text-sm" 
                  value={cloneData.newRepxTemplateKey}
                  onChange={e => setCloneData({...cloneData, newRepxTemplateKey: e.target.value})}
                  placeholder="Ej. mi-tenant/factura-v1"
                />
              </div>
              
              <div className="flex justify-end gap-3 pt-6 border-t border-slate-100 mt-6">
                <button type="button" onClick={() => setShowCloneModal(null)} className="px-5 py-2.5 bg-slate-100 hover:bg-slate-200 text-slate-700 font-bold rounded-xl transition-colors">
                  Cancelar
                </button>
                <button type="submit" className="px-5 py-2.5 bg-primary hover:bg-blue-600 text-white font-bold rounded-xl shadow-md shadow-blue-500/20 transition-all flex items-center">
                  <Save className="w-5 h-5 mr-2" />
                  Guardar Clon
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
