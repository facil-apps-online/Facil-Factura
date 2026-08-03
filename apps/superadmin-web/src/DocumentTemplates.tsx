import React, { useEffect, useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { Plus, Save, Trash2, FileText, ArrowLeft, Upload } from 'lucide-react';
import { toast } from 'sonner';
import { api } from './api';

interface DocumentTemplate {
  id: string;
  name: string;
  version: number;
  status: 'Draft' | 'Published' | 'Archived';
  createdAt: string;
  updatedAt: string;
}

export const DocumentTemplates = () => {
  const { typeId } = useParams();
  const [templates, setTemplates] = useState<DocumentTemplate[]>([]);
  const [showModal, setShowModal] = useState(false);
  const [formData, setFormData] = useState({ name: '', content: '' });
  
  // Para nueva versión
  const [showVersionModal, setShowVersionModal] = useState<string | null>(null);
  const [versionContent, setVersionContent] = useState('');

  const loadTemplates = () => {
    api.get<DocumentTemplate[]>(`/superadmin/templates/${typeId}`)
      .then(res => setTemplates(res.data))
      .catch(() => toast.error("Error al cargar las plantillas"));
  };

  useEffect(() => {
    loadTemplates();
  }, [typeId]);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await api.post('/superadmin/templates', { ...formData, documentTypeId: typeId });
      toast.success("Plantilla creada exitosamente");
      setFormData({ name: '', content: '' });
      setShowModal(false);
      loadTemplates();
    } catch (err: any) {
      toast.error(err.response?.data || "Error al crear la plantilla");
    }
  };

  const handleNewVersion = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!showVersionModal) return;
    try {
      await api.post(`/superadmin/templates/${showVersionModal}/new-version`, { content: versionContent });
      toast.success("Nueva versión creada exitosamente");
      setVersionContent('');
      setShowVersionModal(null);
      loadTemplates();
    } catch (err: any) {
      toast.error(err.response?.data || "Error al crear la versión");
    }
  };

  const handlePublish = async (id: string) => {
    if (!window.confirm("¿Publicar esta plantilla? Esto archivará las versiones publicadas anteriores.")) return;
    try {
      await api.post(`/superadmin/templates/${id}/publish`);
      toast.success("Plantilla publicada");
      loadTemplates();
    } catch (err: any) {
      toast.error(err.response?.data || "Error al publicar");
    }
  };

  const handleDelete = async (id: string) => {
    if (!window.confirm("¿Seguro que deseas eliminar esta plantilla?")) return;
    try {
      await api.delete(`/superadmin/templates/${id}`);
      toast.success("Eliminada correctamente");
      loadTemplates();
    } catch (err: any) {
      toast.error(err.response?.data || "No se puede eliminar la plantilla");
    }
  };

  // Status Badge Helper
  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'Published': return <span className="bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 px-3 py-1 rounded-full text-xs font-bold">Publicado</span>;
      case 'Draft': return <span className="bg-amber-500/20 text-amber-400 border border-amber-500/30 px-3 py-1 rounded-full text-xs font-bold">Borrador</span>;
      case 'Archived': return <span className="bg-slate-500/20 text-slate-400 border border-slate-500/30 px-3 py-1 rounded-full text-xs font-bold">Archivado</span>;
      default: return null;
    }
  };

  return (
    <div className="p-10 animate-in fade-in slide-in-from-bottom-4 duration-500">
      <div className="flex justify-between items-center mb-10">
        <div>
          <Link to="/document-types" className="text-indigo-400 hover:text-indigo-300 font-semibold flex items-center mb-4 transition-colors">
            <ArrowLeft className="w-4 h-4 mr-1" /> Volver a Tipos
          </Link>
          <h1 className="text-3xl font-extrabold text-white tracking-tight flex items-center gap-3">
            <FileText className="w-8 h-8 text-indigo-400" />
            Modelos de Diseño
          </h1>
          <p className="text-slate-400 mt-2 text-lg font-medium">Gestiona las plantillas base y sus versiones para este tipo de documento.</p>
        </div>
        <button 
          onClick={() => setShowModal(true)}
          className="bg-indigo-600 hover:bg-indigo-700 text-white px-6 py-3 rounded-2xl font-bold flex items-center shadow-lg shadow-indigo-600/30 transition-all transform hover:-translate-y-1"
        >
          <Plus className="w-5 h-5 mr-2" />
          Nueva Plantilla
        </button>
      </div>

      <div className="glass-panel rounded-3xl overflow-hidden mt-8">
        <table className="w-full text-left">
          <thead className="bg-slate-900/50 border-b border-slate-700/50">
            <tr>
              <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Nombre del Modelo</th>
              <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Versión</th>
              <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Estado</th>
              <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Fecha Creación</th>
              <th className="px-6 py-4 text-right">Acciones</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-800/50">
            {templates.map(tpl => (
              <tr key={tpl.id} className="hover:bg-slate-800/30 transition-colors group">
                <td className="px-6 py-4">
                  <div className="flex items-center font-bold text-white">
                    <FileText className="w-4 h-4 mr-3 text-indigo-400" />
                    {tpl.name}
                  </div>
                </td>
                <td className="px-6 py-4">
                  <span className="inline-flex items-center px-3 py-1 rounded-lg text-sm font-bold bg-slate-800 text-slate-300 font-mono">
                    v{tpl.version}
                  </span>
                </td>
                <td className="px-6 py-4">
                  {getStatusBadge(tpl.status)}
                </td>
                <td className="px-6 py-4 text-slate-400 font-medium">
                  {new Date(tpl.createdAt).toLocaleDateString()}
                </td>
                <td className="px-6 py-4 text-right space-x-2">
                  {tpl.status === 'Draft' && (
                    <button onClick={() => handlePublish(tpl.id)} className="px-3 py-1.5 text-sm font-bold text-emerald-400 hover:bg-emerald-400/10 rounded-xl transition-all">
                      Publicar
                    </button>
                  )}
                  {tpl.status === 'Published' && (
                    <button onClick={() => setShowVersionModal(tpl.id)} className="px-3 py-1.5 text-sm font-bold text-blue-400 hover:bg-blue-400/10 rounded-xl transition-all">
                      Nueva Versión
                    </button>
                  )}
                  <button onClick={() => handleDelete(tpl.id)} className="p-2 text-red-500 hover:bg-red-500/10 rounded-xl transition-all">
                    <Trash2 className="w-5 h-5" />
                  </button>
                </td>
              </tr>
            ))}
            {templates.length === 0 && (
              <tr>
                <td colSpan={5} className="px-6 py-12 text-center text-slate-400 font-medium">
                  No hay plantillas registradas para este documento.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {/* Modal Creación */}
      {showModal && (
        <div className="fixed inset-0 bg-slate-900/80 backdrop-blur-md flex items-center justify-center z-50 animate-in fade-in duration-200">
          <div className="glass-panel rounded-3xl shadow-2xl p-8 w-full max-w-xl animate-in zoom-in-95 duration-200 border-slate-700">
            <h2 className="text-2xl font-bold text-white mb-6">Nueva Plantilla Base</h2>
            <form onSubmit={handleCreate} className="space-y-5">
              <div>
                <label className="block text-sm font-bold text-slate-300 mb-2">Nombre del Modelo</label>
                <input 
                  type="text" 
                  required 
                  className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500 placeholder:text-slate-500" 
                  value={formData.name}
                  onChange={e => setFormData({...formData, name: e.target.value})}
                  placeholder="Ej. Diseño Minimalista 2024"
                />
              </div>
              <div>
                <label className="block text-sm font-bold text-slate-300 mb-2">Contenido (REPX / XML DevExpress)</label>
                <textarea 
                  required 
                  rows={6}
                  className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500 font-mono text-sm placeholder:text-slate-500 resize-none" 
                  value={formData.content}
                  onChange={e => setFormData({...formData, content: e.target.value})}
                  placeholder="<XtraReportsLayoutSerializer>..."
                />
              </div>
              
              <div className="flex gap-4 pt-4">
                <button type="button" onClick={() => setShowModal(false)} className="flex-1 py-3 px-4 bg-slate-800 hover:bg-slate-700 text-white font-bold rounded-xl transition-colors">
                  Cancelar
                </button>
                <button type="submit" className="flex-1 py-3 px-4 bg-indigo-600 hover:bg-indigo-700 text-white font-bold rounded-xl shadow-lg shadow-indigo-600/30 transition-all flex justify-center items-center">
                  <Save className="w-5 h-5 mr-2" />
                  Guardar
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal Nueva Versión */}
      {showVersionModal && (
        <div className="fixed inset-0 bg-slate-900/80 backdrop-blur-md flex items-center justify-center z-50 animate-in fade-in duration-200">
          <div className="glass-panel rounded-3xl shadow-2xl p-8 w-full max-w-xl animate-in zoom-in-95 duration-200 border-slate-700">
            <h2 className="text-2xl font-bold text-white mb-6">Subir Nueva Versión</h2>
            <p className="text-slate-400 mb-4 text-sm">Al guardar, se creará un borrador de la versión siguiente. Tu plantilla actualmente publicada no será afectada hasta que publiques la nueva versión.</p>
            <form onSubmit={handleNewVersion} className="space-y-5">
              <div>
                <label className="block text-sm font-bold text-slate-300 mb-2">Nuevo Contenido (REPX / XML DevExpress)</label>
                <textarea 
                  required 
                  rows={8}
                  className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-blue-500 font-mono text-sm placeholder:text-slate-500 resize-none" 
                  value={versionContent}
                  onChange={e => setVersionContent(e.target.value)}
                  placeholder="<XtraReportsLayoutSerializer>..."
                />
              </div>
              
              <div className="flex gap-4 pt-4">
                <button type="button" onClick={() => setShowVersionModal(null)} className="flex-1 py-3 px-4 bg-slate-800 hover:bg-slate-700 text-white font-bold rounded-xl transition-colors">
                  Cancelar
                </button>
                <button type="submit" className="flex-1 py-3 px-4 bg-blue-600 hover:bg-blue-700 text-white font-bold rounded-xl shadow-lg shadow-blue-600/30 transition-all flex justify-center items-center">
                  <Upload className="w-5 h-5 mr-2" />
                  Crear Versión
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
