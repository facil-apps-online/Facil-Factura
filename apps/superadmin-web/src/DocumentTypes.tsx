import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Plus, Save, Trash2, FileText, Server } from 'lucide-react';
import { toast } from 'sonner';
import { api } from './api';

interface DocumentType {
  id: string;
  code: string;
  name: string;
  governingEntity: string;
  isActive: boolean;
}

export const DocumentTypes = () => {
  const [docTypes, setDocTypes] = useState<DocumentType[]>([]);
  const [formData, setFormData] = useState({ code: '', name: '', governingEntity: 'DIAN' });
  const [showModal, setShowModal] = useState(false);

  const loadDocTypes = () => {
    api.get<DocumentType[]>('/document-types')
      .then(res => setDocTypes(res.data))
      .catch(() => toast.error("Error al cargar los Tipos de Documentos"));
  };

  useEffect(() => {
    loadDocTypes();
  }, []);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await api.post('/document-types', formData);
      toast.success("Tipo de Documento creado exitosamente");
      setFormData({ code: '', name: '', governingEntity: 'DIAN' });
      setShowModal(false);
      loadDocTypes();
    } catch (err: any) {
      toast.error(err.response?.data || "Error al crear el Tipo de Documento");
    }
  };

  const handleDelete = async (id: string) => {
    if (!window.confirm("¿Seguro que deseas eliminar este Tipo de Documento? Esto fallará si hay Tenants usándolo.")) return;
    try {
      await api.delete(`/document-types/${id}`);
      toast.success("Eliminado correctamente");
      loadDocTypes();
    } catch (err: any) {
      toast.error(err.response?.data || "No se puede eliminar el documento");
    }
  };

  return (
    <div className="p-10 animate-in fade-in slide-in-from-bottom-4 duration-500">
      <div className="flex justify-between items-center mb-10">
        <div>
          <h1 className="text-3xl font-extrabold text-white tracking-tight flex items-center gap-3">
            <Server className="w-8 h-8 text-indigo-400" />
            Configuración del Core
          </h1>
          <p className="text-slate-400 mt-2 text-lg font-medium">Gestiona los tipos de documentos DIAN permitidos en el sistema.</p>
        </div>
        <button 
          onClick={() => setShowModal(true)}
          className="bg-indigo-600 hover:bg-indigo-700 text-white px-6 py-3 rounded-2xl font-bold flex items-center shadow-lg shadow-indigo-600/30 transition-all transform hover:-translate-y-1"
        >
          <Plus className="w-5 h-5 mr-2" />
          Nuevo Documento
        </button>
      </div>

      <div className="glass-panel rounded-3xl overflow-hidden mt-8">
        <table className="w-full text-left">
          <thead className="bg-slate-900/50 border-b border-slate-700/50">
            <tr>
              <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Entidad</th>
              <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Código</th>
              <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Nombre del Documento</th>
              <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Estado</th>
              <th className="px-6 py-4 text-right"></th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-800/50">
            {docTypes.map(doc => (
              <tr key={doc.id} className="hover:bg-slate-800/30 transition-colors group">
                <td className="px-6 py-4">
                  <span className={`inline-flex items-center px-3 py-1 rounded-full text-xs font-bold ${doc.governingEntity === 'DIAN' ? 'bg-indigo-500/20 text-indigo-400 border border-indigo-500/30' : 'bg-fuchsia-500/20 text-fuchsia-400 border border-fuchsia-500/30'}`}>
                    {doc.governingEntity || 'DIAN'}
                  </span>
                </td>
                <td className="px-6 py-4">
                  <span className="inline-flex items-center px-3 py-1 rounded-lg text-sm font-bold bg-slate-800 text-slate-300 font-mono">
                    {doc.code}
                  </span>
                </td>
                <td className="px-6 py-4">
                  <div className="flex items-center font-bold text-white">
                    <FileText className="w-4 h-4 mr-2 text-slate-400" />
                    {doc.name}
                  </div>
                </td>
                <td className="px-6 py-4">
                  <span className="inline-flex items-center px-3 py-1 rounded-full text-xs font-bold bg-emerald-100 text-emerald-700">
                    Activo
                  </span>
                </td>
                <td className="px-6 py-4 text-right space-x-2">
                  <Link to={`/document-types/${doc.id}/templates`} className="inline-block p-2 text-indigo-400 hover:bg-indigo-500/10 rounded-xl opacity-0 group-hover:opacity-100 transition-all" title="Ver Modelos de Diseño">
                    <FileText className="w-5 h-5" />
                  </Link>
                  <button onClick={() => handleDelete(doc.id)} className="p-2 text-red-500 hover:bg-red-500/10 rounded-xl opacity-0 group-hover:opacity-100 transition-all" title="Eliminar Tipo">
                    <Trash2 className="w-5 h-5" />
                  </button>
                </td>
              </tr>
            ))}
            {docTypes.length === 0 && (
              <tr>
                <td colSpan={4} className="px-6 py-12 text-center text-slate-400 font-medium">
                  No hay tipos de documentos registrados. Comienza agregando FE (Factura Electrónica).
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {/* Modal Creación */}
      {showModal && (
        <div className="fixed inset-0 bg-slate-900/80 backdrop-blur-md flex items-center justify-center z-50 animate-in fade-in duration-200">
          <div className="glass-panel rounded-3xl shadow-2xl p-8 w-full max-w-md animate-in zoom-in-95 duration-200 border-slate-700">
            <h2 className="text-2xl font-bold text-white mb-6">Nuevo Tipo de Documento</h2>
            <form onSubmit={handleCreate} className="space-y-5">
              <div>
                <label className="block text-sm font-bold text-slate-300 mb-2">Código (Ej. FE, NC, DS)</label>
                <input 
                  type="text" 
                  required 
                  className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500 font-mono uppercase placeholder:text-slate-500" 
                  value={formData.code}
                  onChange={e => setFormData({...formData, code: e.target.value.toUpperCase()})}
                  placeholder="DIAN-FE"
                />
              </div>
              <div>
                <label className="block text-sm font-bold text-slate-300 mb-2">Nombre Descriptivo</label>
                <input 
                  type="text" 
                  required 
                  className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500 placeholder:text-slate-500" 
                  value={formData.name}
                  onChange={e => setFormData({...formData, name: e.target.value})}
                  placeholder="Factura Electrónica de Venta"
                />
              </div>
              <div>
                <label className="block text-sm font-bold text-slate-300 mb-2">Entidad Reguladora</label>
                <select 
                  required 
                  className="w-full px-4 py-3 bg-slate-800/50 border border-slate-700 text-white rounded-xl focus:ring-2 focus:ring-indigo-500" 
                  value={formData.governingEntity}
                  onChange={e => setFormData({...formData, governingEntity: e.target.value})}
                >
                  <option value="DIAN">DIAN (Facturación Electrónica, Nómina, etc)</option>
                  <option value="MINSALUD">Ministerio de Salud (RIPS, FEV en Salud)</option>
                  <option value="UGPP">UGPP (Nómina Parafiscales)</option>
                  <option value="OTRA">Otra Entidad</option>
                </select>
              </div>
              <div className="flex gap-4 pt-4">
                <button type="button" onClick={() => setShowModal(false)} className="flex-1 py-3 px-4 bg-slate-100 hover:bg-slate-200 text-slate-700 font-bold rounded-xl transition-colors">
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
    </div>
  );
};
