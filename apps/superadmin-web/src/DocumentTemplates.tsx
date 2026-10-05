import React, { useEffect, useRef, useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { Plus, Trash2, FileText, ArrowLeft, Upload, Loader2, Eye, Pencil, Undo2 } from 'lucide-react';
import { toast } from 'sonner';
import { api } from './api';

interface DocumentTemplate {
  id: string;
  name: string;
  repxTemplateKey: string;
  version: number;
  status: 'Draft' | 'Published' | 'Archived';
  createdAt: string;
  updatedAt: string;
}

export const DocumentTemplates = () => {
  const { typeId } = useParams();
  const [templates, setTemplates] = useState<DocumentTemplate[]>([]);

  const [showModal, setShowModal] = useState(false);
  const [newName, setNewName] = useState('');
  const [newFile, setNewFile] = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);
  const newFileInputRef = useRef<HTMLInputElement>(null);

  // Para nueva versión
  const [showVersionModal, setShowVersionModal] = useState<string | null>(null);
  const [versionFile, setVersionFile] = useState<File | null>(null);
  const [uploadingVersion, setUploadingVersion] = useState(false);

  const [previewingId, setPreviewingId] = useState<string | null>(null);

  const handlePreview = async (id: string) => {
    setPreviewingId(id);
    try {
      const res = await api.post(`/templates/${id}/preview`, {}, { responseType: 'blob' });
      const url = URL.createObjectURL(new Blob([res.data], { type: 'application/pdf' }));
      window.open(url, '_blank');
    } catch (err: any) {
      if (err.response?.data instanceof Blob) {
        const text = await err.response.data.text();
        toast.error(text || 'Error al generar la vista previa');
      } else {
        toast.error(err.response?.data || 'Error al generar la vista previa');
      }
    } finally {
      setPreviewingId(null);
    }
  };

  const loadTemplates = () => {
    api.get<DocumentTemplate[]>(`/templates/by-type/${typeId}`)
      .then(res => setTemplates([...res.data].sort((a, b) => a.name.localeCompare(b.name))))
      .catch(() => toast.error("Error al cargar las plantillas"));
  };

  useEffect(() => {
    loadTemplates();
  }, [typeId]);

  const resetNewForm = () => {
    setNewName('');
    setNewFile(null);
    if (newFileInputRef.current) newFileInputRef.current.value = '';
  };

  const handleUpload = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newFile) {
      toast.error("Selecciona un archivo .repx.");
      return;
    }
    setUploading(true);
    try {
      const fd = new FormData();
      fd.append('file', newFile);
      fd.append('name', newName);
      fd.append('documentTypeId', typeId || '');
      await api.post('/templates/upload', fd, {
        headers: { 'Content-Type': 'multipart/form-data' }
      });
      toast.success("Plantilla subida exitosamente");
      resetNewForm();
      setShowModal(false);
      loadTemplates();
    } catch (err: any) {
      toast.error(err.response?.data || "Error al subir la plantilla");
    } finally {
      setUploading(false);
    }
  };

  const handleUploadNewVersion = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!showVersionModal || !versionFile) {
      toast.error("Selecciona un archivo .repx.");
      return;
    }
    setUploadingVersion(true);
    try {
      const fd = new FormData();
      fd.append('file', versionFile);
      await api.post(`/templates/${showVersionModal}/upload-new-version`, fd, {
        headers: { 'Content-Type': 'multipart/form-data' }
      });
      toast.success("Nueva versión subida exitosamente");
      setVersionFile(null);
      setShowVersionModal(null);
      loadTemplates();
    } catch (err: any) {
      toast.error(err.response?.data || "Error al subir la versión");
    } finally {
      setUploadingVersion(false);
    }
  };

  const handlePublish = async (id: string) => {
    if (!window.confirm("¿Publicar esta plantilla? Esto archivará las versiones publicadas anteriores.")) return;
    try {
      await api.put(`/templates/${id}/publish`, {});
      toast.success("Plantilla publicada");
      loadTemplates();
    } catch (err: any) {
      toast.error(err.response?.data || "Error al publicar");
    }
  };

  const handleReturnToDraft = async (id: string) => {
    if (!window.confirm("¿Devolver esta plantilla a Borrador? Dejará de estar disponible como plantilla publicada mientras haces las pruebas.")) return;
    try {
      await api.put(`/templates/${id}/status`, { status: 'Draft' });
      toast.success("Plantilla devuelta a Borrador");
      loadTemplates();
    } catch (err: any) {
      toast.error(err.response?.data || "Error al cambiar el estado");
    }
  };

  const handleRename = async (template: DocumentTemplate) => {
    const name = window.prompt('Nuevo nombre de la plantilla:', template.name);
    if (name === null || name.trim() === '' || name.trim() === template.name) return;
    try {
      await api.put(`/templates/${template.id}/name`, { name: name.trim() });
      toast.success('Nombre actualizado');
      loadTemplates();
    } catch (err: any) {
      toast.error(err.response?.data || 'Error al cambiar el nombre');
    }
  };

  const handleDelete = async (id: string) => {
    if (!window.confirm("¿Seguro que deseas eliminar esta plantilla?")) return;
    try {
      await api.delete(`/templates/${id}`);
      toast.success("Eliminada correctamente");
      loadTemplates();
    } catch (err: any) {
      toast.error(err.response?.data || "No se puede eliminar la plantilla");
    }
  };

  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'Published': return <span className="bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 px-3 py-1 rounded-full text-xs font-bold">Publicada</span>;
      case 'Draft': return <span className="bg-amber-500/20 text-amber-400 border border-amber-500/30 px-3 py-1 rounded-full text-xs font-bold">Borrador</span>;
      case 'Archived': return <span className="bg-slate-500/20 text-slate-400 border border-slate-500/30 px-3 py-1 rounded-full text-xs font-bold">Archivada</span>;
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
          <p className="text-slate-400 mt-2 text-lg font-medium">Administra las plantillas y sus versiones para este tipo de documento.</p>
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
              <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Nombre de la plantilla</th>
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
                    <button onClick={() => handleRename(tpl)} className="ml-3 p-1 text-slate-400 hover:text-indigo-300 rounded transition-colors" title="Cambiar nombre">
                      <Pencil className="w-4 h-4" />
                    </button>
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
                <td className="px-6 py-4">
                  <div className="flex items-center justify-end gap-2">
                    <button onClick={() => handlePreview(tpl.id)} disabled={previewingId === tpl.id} className="w-32 px-3 py-1.5 text-sm font-bold text-indigo-400 hover:bg-indigo-400/10 rounded-xl transition-all disabled:opacity-50 inline-flex items-center justify-center gap-1.5">
                      {previewingId === tpl.id ? <Loader2 className="w-4 h-4 animate-spin" /> : <Eye className="w-4 h-4" />}
                      Vista Previa
                    </button>
                    {tpl.status === 'Draft' ? (
                      <Link to={`/document-types/${typeId}/templates/${tpl.repxTemplateKey}/edit`} className="w-24 px-3 py-1.5 text-sm font-bold text-amber-400 hover:bg-amber-400/10 rounded-xl transition-all inline-flex items-center justify-center gap-1.5">
                        <Pencil className="w-4 h-4" /> Editar
                      </Link>
                    ) : (
                      <span className="w-24" />
                    )}
                    {tpl.status === 'Draft' && (
                      <button onClick={() => handlePublish(tpl.id)} className="w-28 px-3 py-1.5 text-sm font-bold text-emerald-400 hover:bg-emerald-400/10 rounded-xl transition-all">
                        Publicar
                      </button>
                    )}
                    {tpl.status === 'Published' && (
                      <button onClick={() => handleReturnToDraft(tpl.id)} className="w-32 px-3 py-1.5 text-sm font-bold text-amber-400 hover:bg-amber-400/10 rounded-xl transition-all inline-flex items-center justify-center gap-1.5">
                        <Undo2 className="w-4 h-4" /> Borrador
                      </button>
                    )}
                    {tpl.status === 'Published' && (
                      <button onClick={() => setShowVersionModal(tpl.id)} className="w-28 px-3 py-1.5 text-sm font-bold text-blue-400 hover:bg-blue-400/10 rounded-xl transition-all">
                        Nueva Versión
                      </button>
                    )}
                    {tpl.status === 'Archived' && <span className="w-28" />}
                    <button onClick={() => handleDelete(tpl.id)} className="p-2 text-red-500 hover:bg-red-500/10 rounded-xl transition-all">
                      <Trash2 className="w-5 h-5" />
                    </button>
                  </div>
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

      {/* Modal Nueva Plantilla */}
      {showModal && (
        <div className="fixed inset-0 bg-slate-900/80 backdrop-blur-md flex items-center justify-center z-50 animate-in fade-in duration-200">
          <div className="glass-panel rounded-3xl shadow-2xl p-8 w-full max-w-lg animate-in zoom-in-95 duration-200 border-slate-700">
            <h2 className="text-2xl font-bold text-white mb-6">Nueva plantilla</h2>
            <form onSubmit={handleUpload} className="space-y-5">
              <div>
                <label className="block text-sm font-bold text-slate-300 mb-2">Nombre de la plantilla</label>
                <input
                  type="text"
                  required
                  className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500 placeholder:text-slate-500"
                  value={newName}
                  onChange={e => setNewName(e.target.value)}
                  placeholder="Ej. Diseño Minimalista 2024"
                />
              </div>
              <div>
                <label className="block text-sm font-bold text-slate-300 mb-2">Archivo de plantilla</label>
                <input
                  ref={newFileInputRef}
                  required
                  type="file"
                  accept=".repx"
                  onChange={e => setNewFile(e.target.files?.[0] ?? null)}
                  className="w-full text-sm text-slate-300 file:mr-4 file:py-2.5 file:px-4 file:rounded-xl file:border-0 file:font-bold file:bg-indigo-600 file:text-white hover:file:bg-indigo-700 file:cursor-pointer cursor-pointer bg-slate-800/50 border border-slate-700 rounded-xl"
                />
                <p className="text-xs text-slate-500 mt-2">La plantilla quedará como borrador hasta que la publiques.</p>
              </div>

              <div className="flex gap-4 pt-4">
                <button type="button" onClick={() => { resetNewForm(); setShowModal(false); }} className="flex-1 py-3 px-4 bg-slate-800 hover:bg-slate-700 text-white font-bold rounded-xl transition-colors">
                  Cancelar
                </button>
                <button type="submit" disabled={uploading} className="flex-1 py-3 px-4 bg-indigo-600 hover:bg-indigo-700 text-white font-bold rounded-xl shadow-lg shadow-indigo-600/30 transition-all flex justify-center items-center disabled:opacity-50">
                  {uploading ? <Loader2 className="w-5 h-5 mr-2 animate-spin" /> : <Upload className="w-5 h-5 mr-2" />}
                  {uploading ? 'Subiendo...' : 'Subir Plantilla'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal Nueva Versión */}
      {showVersionModal && (
        <div className="fixed inset-0 bg-slate-900/80 backdrop-blur-md flex items-center justify-center z-50 animate-in fade-in duration-200">
          <div className="glass-panel rounded-3xl shadow-2xl p-8 w-full max-w-lg animate-in zoom-in-95 duration-200 border-slate-700">
            <h2 className="text-2xl font-bold text-white mb-6">Subir nueva versión</h2>
            <p className="text-slate-400 mb-4 text-sm">Se creará un borrador. La versión publicada seguirá activa hasta que publiques la nueva.</p>
            <form onSubmit={handleUploadNewVersion} className="space-y-5">
              <div>
                <label className="block text-sm font-bold text-slate-300 mb-2">Archivo de plantilla</label>
                <input
                  required
                  type="file"
                  accept=".repx"
                  onChange={e => setVersionFile(e.target.files?.[0] ?? null)}
                  className="w-full text-sm text-slate-300 file:mr-4 file:py-2.5 file:px-4 file:rounded-xl file:border-0 file:font-bold file:bg-blue-600 file:text-white hover:file:bg-blue-700 file:cursor-pointer cursor-pointer bg-slate-800/50 border border-slate-700 rounded-xl"
                />
              </div>

              <div className="flex gap-4 pt-4">
                <button type="button" onClick={() => { setVersionFile(null); setShowVersionModal(null); }} className="flex-1 py-3 px-4 bg-slate-800 hover:bg-slate-700 text-white font-bold rounded-xl transition-colors">
                  Cancelar
                </button>
                <button type="submit" disabled={uploadingVersion} className="flex-1 py-3 px-4 bg-blue-600 hover:bg-blue-700 text-white font-bold rounded-xl shadow-lg shadow-blue-600/30 transition-all flex justify-center items-center disabled:opacity-50">
                  {uploadingVersion ? <Loader2 className="w-5 h-5 mr-2 animate-spin" /> : <Upload className="w-5 h-5 mr-2" />}
                  {uploadingVersion ? 'Subiendo...' : 'Crear Versión'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
