import React, { useState, useEffect } from 'react';
import { FileSignature, Plus, Trash2, X, Loader2 } from 'lucide-react';
import { api } from '../lib/api';
import { toast } from 'sonner';

export default function ResolutionsSettings() {
  const [loading, setLoading] = useState(true);
  const [resolutions, setResolutions] = useState<any[]>([]);
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

  const [habilitationStatus, setHabilitationStatus] = useState<any>(null);
  const [magicLink, setMagicLink] = useState('');
  const [softwareId, setSoftwareId] = useState('');
  const [softwarePin, setSoftwarePin] = useState('');
  const [isHabilitating, setIsHabilitating] = useState(false);

  const loadResolutions = () => {
    setLoading(true);
    api.get('/client/resolutions')
      .then(res => setResolutions(res.data))
      .catch(() => toast.error("Error al cargar resoluciones"))
      .finally(() => setLoading(false));
  };

  const loadHabilitationStatus = () => {
    api.get('/client/dian/habilitation-status')
      .then(res => setHabilitationStatus(res.data))
      .catch(() => setHabilitationStatus(null));
  };

  useEffect(() => {
    loadResolutions();
    loadHabilitationStatus();
  }, []);

  // Polling para revisar si el background worker terminó
  useEffect(() => {
    let interval: NodeJS.Timeout;
    if (habilitationStatus?.status === 'Testing') {
      interval = setInterval(() => {
        loadHabilitationStatus();
      }, 4000); // Revisar cada 4 segundos
    }
    return () => clearInterval(interval);
  }, [habilitationStatus?.status]);

  const handlePdfUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setUploadingPdf(true);
    const formData = new FormData();
    formData.append("file", file);

    try {
      const res = await api.post('/client/resolutions/parse', formData, {
        headers: { 'Content-Type': 'multipart/form-data' }
      });
      
      const parsed = res.data;
      setNewRes(prev => ({
        ...prev,
        resolutionNumber: parsed.resolutionNumber || prev.resolutionNumber,
        prefix: parsed.prefix || prev.prefix,
        numberStart: parsed.numberStart || prev.numberStart,
        numberEnd: parsed.numberEnd || prev.numberEnd,
        validFrom: parsed.validFrom ? parsed.validFrom.split('T')[0] : prev.validFrom,
        validTo: parsed.validTo ? parsed.validTo.split('T')[0] : prev.validTo
      }));
      toast.success("PDF procesado. Verifica los datos extraídos.");
    } catch (err: any) {
      toast.error(err.response?.data || "Error al procesar el PDF");
    } finally {
      setUploadingPdf(false);
      e.target.value = '';
    }
  };

  const handleCreateResolution = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await api.post('/client/resolutions', newRes);
      toast.success("Resolución agregada");
      setShowResModal(false);
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
      loadResolutions();
    } catch (err) {
      toast.error("Error al crear resolución");
    }
  };

  const handleDeleteResolution = async (resId: string) => {
    if (!confirm("¿Seguro que deseas eliminar esta resolución?")) return;
    try {
      await api.delete(`/client/resolutions/${resId}`);
      toast.success("Resolución eliminada");
      loadResolutions();
    } catch (err) {
      toast.error("Error al eliminar la resolución");
    }
  };

  const handleStartHabilitation = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!magicLink) return;

    setIsHabilitating(true);
    try {
      await api.post('/client/dian/start-habilitation', { magicLink, softwareId, softwarePin });
      toast.success("¡Habilitación configurada y en progreso!");
      setMagicLink('');
      setSoftwareId('');
      setSoftwarePin('');
      loadHabilitationStatus();
    } catch (err: any) {
      toast.error(err.response?.data || "Error al iniciar habilitación");
    } finally {
      setIsHabilitating(false);
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
    <div className="p-8 max-w-5xl mx-auto">
      <div className="flex justify-between items-center mb-8">
        <div>
          <h1 className="text-3xl font-extrabold text-slate-800 tracking-tight">Mis Resoluciones DIAN</h1>
          <p className="text-slate-500 mt-2">Gestiona tus autorizaciones de numeración para emitir facturas electrónicas válidas ante la DIAN.</p>
        </div>
        <button onClick={() => setShowResModal(true)} className="bg-primary hover:bg-primary-hover text-white px-5 py-2.5 rounded-xl font-semibold shadow-lg shadow-primary/30 transition-all hover:-translate-y-0.5 flex items-center gap-2">
          <Plus size={18} /> Nueva Resolución
        </button>
      </div>

      {/* Splash Screen Full-Screen */}
      {(isHabilitating || habilitationStatus?.status === 'Testing' || habilitationStatus?.status === 'Approved') && (
        <div className="fixed inset-0 bg-slate-900/80 backdrop-blur-md z-[100] flex items-center justify-center p-4">
          <div className="bg-white rounded-[2rem] p-10 max-w-lg w-full shadow-2xl flex flex-col items-center text-center animate-in zoom-in-95 duration-300">
            {habilitationStatus?.status === 'Approved' ? (
              <>
                <div className="w-24 h-24 bg-emerald-100 text-emerald-500 rounded-full flex items-center justify-center mb-6 shadow-inner ring-8 ring-emerald-50">
                  <svg className="w-12 h-12" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth="3" d="M5 13l4 4L19 7"></path></svg>
                </div>
                <h2 className="text-3xl font-black text-slate-800 mb-2">¡Habilitación Exitosa!</h2>
                <p className="text-slate-500 mb-8 text-lg">Tu empresa ya está sincronizada y lista para emitir facturación electrónica real ante la DIAN.</p>
                <button onClick={() => setHabilitationStatus({ ...habilitationStatus, status: 'Production' })} className="w-full bg-slate-900 hover:bg-slate-800 text-white py-4 rounded-2xl font-bold text-lg transition-all shadow-lg hover:shadow-xl hover:-translate-y-1">
                  Continuar a mi cuenta
                </button>
              </>
            ) : (
              <>
                <div className="relative w-28 h-28 mb-8">
                  <div className="absolute inset-0 bg-primary/20 rounded-full animate-ping"></div>
                  <div className="absolute inset-2 bg-primary/20 rounded-full animate-pulse"></div>
                  <div className="absolute inset-0 flex items-center justify-center">
                    <Loader2 className="w-12 h-12 text-primary animate-spin" />
                  </div>
                </div>
                <h2 className="text-2xl font-bold text-slate-800 mb-3">
                  {isHabilitating && !habilitationStatus?.progress ? 'Conectando con la DIAN...' : 'Configuración en Progreso'}
                </h2>
                
                {/* Progress Bar Container */}
                <div className="w-full mt-6 mb-4">
                  <div className="flex justify-between items-end mb-2">
                    <span className="text-sm font-bold text-primary">{habilitationStatus?.message || (isHabilitating ? 'Autenticando...' : '')}</span>
                    <span className="text-sm font-bold text-slate-500">{habilitationStatus?.progress || 0}%</span>
                  </div>
                  <div className="w-full h-3 bg-slate-100 rounded-full overflow-hidden">
                    <div 
                      className="h-full bg-primary transition-all duration-500 ease-out rounded-full"
                      style={{ width: `${habilitationStatus?.progress || 0}%` }}
                    ></div>
                  </div>
                </div>

                <p className="text-slate-500 text-sm">
                  {isHabilitating && !habilitationStatus?.progress 
                    ? 'Extrayendo tu identificador de software.' 
                    : 'Automatizando la configuración ante el ente fiscal. Esto puede tomar unos segundos, no cierres esta ventana.'}
                </p>
                
                {habilitationStatus?.status === 'Testing' && (
                  <div className="mt-8 bg-slate-50 border border-slate-100 rounded-xl p-4 w-full flex items-center justify-between">
                    <span className="text-sm font-bold text-slate-400">TestSetId</span>
                    <span className="font-mono text-sm text-slate-600 bg-white px-2 py-1 rounded shadow-sm">{habilitationStatus.testSetId}</span>
                  </div>
                )}
              </>
            )}
          </div>
        </div>
      )}

      {/* Panel de Habilitación DIAN Automática */}
      <div className="bg-white rounded-3xl p-8 shadow-sm border border-slate-100 mb-8">
        <h2 className="text-xl font-bold text-slate-800 mb-2">Habilitación y Set de Pruebas</h2>
        <p className="text-slate-500 mb-6">Automatiza tu proceso de habilitación pegando el enlace que te envió la DIAN. Nosotros hacemos el resto.</p>

        {habilitationStatus?.status === 'Production' ? (
          <div className="bg-emerald-50 border border-emerald-100 rounded-2xl p-6 flex items-center gap-4">
            <div className="w-12 h-12 bg-emerald-500 text-white rounded-full flex items-center justify-center shadow-lg shadow-emerald-500/30">
              <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth="3" d="M5 13l4 4L19 7"></path></svg>
            </div>
            <div>
              <h3 className="font-bold text-emerald-900 text-lg">Empresa Habilitada en Producción</h3>
              <p className="text-emerald-700/80">Has completado satisfactoriamente los requisitos técnicos de la DIAN.</p>
            </div>
          </div>
        ) : (
          <form onSubmit={handleStartHabilitation} className="bg-slate-50 border border-slate-200 rounded-2xl p-6">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-4">
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-2">Software ID (MUISCA)</label>
                <input 
                  type="text" 
                  placeholder="Ej: 7a12b4c9-8f3e-4b... (Opcional)" 
                  className="w-full px-4 py-3 bg-white border border-slate-300 rounded-xl focus:ring-2 focus:ring-primary outline-none transition-all font-mono text-sm"
                  value={softwareId}
                  onChange={e => setSoftwareId(e.target.value)}
                  disabled={isHabilitating}
                />
              </div>
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-2">PIN del Software</label>
                <input 
                  type="text" 
                  placeholder="Ej: 12345 (Opcional)" 
                  className="w-full px-4 py-3 bg-white border border-slate-300 rounded-xl focus:ring-2 focus:ring-primary outline-none transition-all font-mono text-sm"
                  value={softwarePin}
                  onChange={e => setSoftwarePin(e.target.value)}
                  disabled={isHabilitating}
                />
              </div>
            </div>
            
            <label className="block text-sm font-bold text-slate-700 mb-3">Enlace Mágico de Acceso (Token DIAN)</label>
            <div className="flex gap-4">
              <input 
                type="url" 
                required 
                placeholder="https://catalogo-vpfe.dian.gov.co/User/Login?token=..." 
                className="flex-1 px-4 py-3 bg-white border border-slate-300 rounded-xl focus:ring-2 focus:ring-primary outline-none transition-all"
                value={magicLink}
                onChange={e => setMagicLink(e.target.value)}
                disabled={isHabilitating}
              />
              <button 
                type="submit" 
                disabled={isHabilitating || !magicLink}
                className="bg-slate-800 hover:bg-slate-900 disabled:opacity-50 text-white px-8 py-3 rounded-xl font-bold shadow-md transition-all flex items-center gap-2 shrink-0"
              >
                {isHabilitating ? <Loader2 className="w-5 h-5 animate-spin" /> : null}
                {isHabilitating ? 'Conectando...' : 'Guardar e Iniciar Automatización'}
              </button>
            </div>
            <p className="text-xs text-slate-400 mt-3">
              Si dejas los campos de Software ID y PIN en blanco, FacilFactura creará automáticamente tu Software Propio en la DIAN. Al hacer clic, enviaremos automáticamente los documentos de prueba requeridos.
            </p>
          </form>
        )}
      </div>

      <div className="bg-white rounded-3xl p-8 shadow-sm border border-slate-100">
        {resolutions.length === 0 ? (
          <div className="flex flex-col items-center justify-center text-center h-64 border-2 border-dashed border-slate-200 rounded-2xl bg-slate-50/50">
            <div className="w-16 h-16 bg-blue-50 text-blue-600 rounded-full flex items-center justify-center mb-4 shadow-inner">
              <FileSignature size={32} />
            </div>
            <h3 className="text-lg font-bold text-slate-700">Aún no tienes resoluciones</h3>
            <p className="text-slate-500 max-w-sm mt-2">Carga tu primer formulario 1876 de la DIAN para empezar a facturar legalmente.</p>
            <button onClick={() => setShowResModal(true)} className="mt-6 text-primary font-bold hover:underline">Configurar ahora</button>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="bg-slate-50 text-slate-500 text-sm border-y border-slate-200">
                  <th className="font-semibold py-3 px-4 rounded-tl-xl">Tipo / Prefijo</th>
                  <th className="font-semibold py-3 px-4">No. Resolución</th>
                  <th className="font-semibold py-3 px-4">Rango Autorizado</th>
                  <th className="font-semibold py-3 px-4">Vigencia</th>
                  <th className="font-semibold py-3 px-4 text-center rounded-tr-xl">Acciones</th>
                </tr>
              </thead>
              <tbody>
                {resolutions.map(r => (
                  <tr key={r.id} className="border-b border-slate-100 hover:bg-slate-50/50 transition-colors">
                    <td className="py-4 px-4">
                      <div className="flex items-center gap-3">
                        <span className={`px-2 py-1 rounded-md text-xs font-bold ${r.documentType === 'FE' ? 'bg-blue-100 text-blue-700' : 'bg-purple-100 text-purple-700'}`}>{r.documentType}</span>
                        <span className="font-bold text-slate-700 text-lg">{r.prefix || '-'}</span>
                      </div>
                    </td>
                    <td className="py-4 px-4 font-mono text-sm text-slate-600 font-medium">{r.resolutionNumber}</td>
                    <td className="py-4 px-4 text-sm text-slate-600">
                      <span className="font-bold">{r.numberStart}</span> a <span className="font-bold">{r.numberEnd}</span>
                    </td>
                    <td className="py-4 px-4 text-sm text-slate-500">
                      {new Date(r.validFrom).toLocaleDateString()} &mdash; {new Date(r.validTo).toLocaleDateString()}
                    </td>
                    <td className="py-4 px-4 text-center">
                      <button onClick={() => handleDeleteResolution(r.id)} className="p-2 text-rose-400 hover:text-rose-600 hover:bg-rose-50 rounded-lg transition-colors" title="Eliminar">
                        <Trash2 size={18} />
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {showResModal && (
        <div className="fixed inset-0 bg-slate-900/50 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-3xl p-8 max-w-2xl w-full shadow-2xl animate-in fade-in zoom-in duration-200">
            <div className="flex justify-between items-center mb-6">
              <h3 className="text-xl font-bold text-slate-800 flex items-center gap-2">
                <FileSignature className="text-primary" /> Agregar Resolución DIAN
              </h3>
              <button onClick={() => setShowResModal(false)} className="text-slate-400 hover:text-slate-600 hover:bg-slate-100 p-2 rounded-full transition-colors">
                <X size={20} />
              </button>
            </div>
            
            <form onSubmit={handleCreateResolution} className="space-y-5">
              <div className="bg-primary/5 border border-primary/20 rounded-2xl p-5 flex items-center justify-between shadow-inner">
                <div>
                  <h4 className="text-sm font-bold text-primary-hover">Extracción Inteligente de PDF</h4>
                  <p className="text-xs text-primary-hover/70 mt-1">Sube el Formulario 1876 y llenaremos todo mágicamente.</p>
                </div>
                <div>
                  <label className="cursor-pointer bg-white text-primary hover:text-primary-hover border border-primary/20 hover:border-primary/40 px-5 py-2.5 rounded-xl text-sm font-bold transition-all shadow-sm flex items-center gap-2">
                    {uploadingPdf ? <Loader2 className="w-5 h-5 animate-spin" /> : <FileSignature size={18} />}
                    {uploadingPdf ? 'Analizando...' : 'Cargar PDF'}
                    <input type="file" accept="application/pdf" className="hidden" onChange={handlePdfUpload} disabled={uploadingPdf} />
                  </label>
                </div>
              </div>

              <div className="grid grid-cols-2 gap-5">
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-1.5">Tipo de Documento</label>
                  <select required className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-primary outline-none transition-all font-medium text-slate-700" value={newRes.documentType} onChange={e => setNewRes({...newRes, documentType: e.target.value})}>
                    <option value="FE">Factura Electrónica (FE)</option>
                    <option value="NC">Nota Crédito (NC)</option>
                    <option value="ND">Nota Débito (ND)</option>
                    <option value="POS">Documento Soporte / POS</option>
                  </select>
                </div>
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-1.5">No. de Resolución / Autorización</label>
                  <input required type="text" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-primary outline-none transition-all font-mono" placeholder="Ej. 1876..." value={newRes.resolutionNumber} onChange={e => setNewRes({...newRes, resolutionNumber: e.target.value})} />
                </div>
              </div>

              <div className="grid grid-cols-3 gap-5">
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-1.5">Prefijo</label>
                  <input type="text" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-primary outline-none transition-all uppercase font-bold" placeholder="Opcional" value={newRes.prefix} onChange={e => setNewRes({...newRes, prefix: e.target.value})} />
                </div>
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-1.5">Desde</label>
                  <input required type="number" min="1" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-primary outline-none transition-all" value={newRes.numberStart || ''} onChange={e => setNewRes({...newRes, numberStart: parseInt(e.target.value) || 0})} />
                </div>
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-1.5">Hasta</label>
                  <input required type="number" min="1" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-primary outline-none transition-all" value={newRes.numberEnd || ''} onChange={e => setNewRes({...newRes, numberEnd: parseInt(e.target.value) || 0})} />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-5">
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-1.5">Válida Desde</label>
                  <input required type="date" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-primary outline-none transition-all" value={newRes.validFrom} onChange={e => setNewRes({...newRes, validFrom: e.target.value})} />
                </div>
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-1.5">Válida Hasta</label>
                  <input required type="date" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-primary outline-none transition-all" value={newRes.validTo} onChange={e => setNewRes({...newRes, validTo: e.target.value})} />
                </div>
              </div>

              <div>
                <label className="block text-sm font-bold text-slate-700 mb-1.5">Clave Técnica (Solo FE)</label>
                <input type="text" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-primary outline-none transition-all font-mono text-sm" placeholder="Pega aquí el hash técnico de la DIAN..." value={newRes.technicalKey} onChange={e => setNewRes({...newRes, technicalKey: e.target.value})} />
              </div>

              <div className="pt-6 border-t border-slate-100 flex justify-end gap-3">
                <button type="button" onClick={() => setShowResModal(false)} className="px-6 py-3 text-slate-500 hover:bg-slate-100 rounded-xl font-bold transition-colors">Cancelar</button>
                <button type="submit" className="bg-primary hover:bg-primary-hover text-white px-8 py-3 rounded-xl font-bold shadow-lg shadow-primary/30 transition-transform hover:-translate-y-0.5">
                  Guardar Resolución
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
