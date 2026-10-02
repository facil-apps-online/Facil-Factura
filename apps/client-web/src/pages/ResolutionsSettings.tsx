import React, { useState, useEffect } from 'react';
import { FileSignature, Plus, Trash2, X, Loader2, Pencil, Check, Star } from 'lucide-react';
import { api, getErrorMessage } from '../lib/api';
import { toast } from 'sonner';
import SearchableSelect from '@shared/components/SearchableSelect';
import { useConfirm } from '@shared/components/ConfirmDialog';
import { todayColombia } from '../lib/colombiaTime';

export default function ResolutionsSettings() {
  const confirm = useConfirm();
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

  const [editingId, setEditingId] = useState<string | null>(null);

  const documentTypeLabels: Record<string, string> = {
    FE: 'Factura Electrónica (FE)',
    NC: 'Nota Crédito (NC)',
    ND: 'Nota Débito (ND)',
    POS: 'Documento Soporte / POS',
    NE: 'Nómina Electrónica (NE)'
  };

  const [habilitationStatus, setHabilitationStatus] = useState<any>(null);
  const [magicLink, setMagicLink] = useState('');
  const [isHabilitating, setIsHabilitating] = useState(false);
  const [testDocPreview, setTestDocPreview] = useState<any>(null);
  const [loadingPreview, setLoadingPreview] = useState(false);
  const [sendingTestDoc, setSendingTestDoc] = useState(false);

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

  const [noteCounters, setNoteCounters] = useState<{ nextCreditNoteNumber: number; nextDebitNoteNumber: number } | null>(null);

  const loadNoteCounters = () => {
    api.get('/client/resolutions/note-counters')
      .then(res => setNoteCounters(res.data))
      .catch(() => toast.error("Error al cargar los consecutivos de notas"));
  };

  useEffect(() => {
    loadResolutions();
    loadHabilitationStatus();
    loadNoteCounters();
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

      const parsedList: any[] = res.data;

      if (parsedList.length > 1) {
        // El PDF trae varios rangos en la misma hoja (ej. Factura Electrónica + Documento
        // Soporte) — se cargan todas directamente en vez de pasar por el modal una por una;
        // se pueden eliminar después las que no se necesiten.
        let created = 0;
        for (const parsed of parsedList) {
          try {
            await api.post('/client/resolutions', {
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
      toast.error(getErrorMessage(err, "Error al procesar el PDF"));
    } finally {
      setUploadingPdf(false);
      e.target.value = '';
    }
  };

  const closeResModal = () => {
    setShowResModal(false);
    setEditingId(null);
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

  const openCreateModal = () => {
    setEditingId(null);
    setShowResModal(true);
  };

  const startEditResolution = (r: any) => {
    setEditingId(r.id);
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

  const handleSubmitResolution = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      // La nómina no tiene resolución DIAN real: se completan con valores neutros los campos que
      // el formulario ocultó para este tipo (no aplican, pero el modelo los sigue teniendo).
      const payload = newRes.documentType === 'NE'
        ? {
            ...newRes,
            resolutionNumber: newRes.resolutionNumber || 'N/A',
            numberEnd: newRes.numberEnd || 999999999999,
            validFrom: newRes.validFrom || todayColombia(),
            validTo: newRes.validTo || '2099-12-31',
          }
        : newRes;

      if (editingId) {
        await api.put(`/client/resolutions/${editingId}`, payload);
        toast.success("Resolución actualizada");
      } else {
        await api.post('/client/resolutions', payload);
        toast.success("Resolución agregada");
      }
      closeResModal();
      loadResolutions();
    } catch (err: any) {
      toast.error(getErrorMessage(err, editingId ? "Error al actualizar resolución" : "Error al crear resolución"));
    }
  };

  const [editingNextNumberId, setEditingNextNumberId] = useState<string | null>(null);
  const [nextNumberDraft, setNextNumberDraft] = useState('');
  const [savingNextNumber, setSavingNextNumber] = useState(false);

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
      await api.put(`/client/resolutions/${r.id}/next-number`, { nextNumber: value });
      toast.success('Próximo consecutivo actualizado');
      setEditingNextNumberId(null);
      loadResolutions();
    } catch (err: any) {
      toast.error(getErrorMessage(err, 'Error al actualizar el consecutivo'));
    } finally {
      setSavingNextNumber(false);
    }
  };

  const [editingNoteCounterType, setEditingNoteCounterType] = useState<'credit' | 'debit' | null>(null);
  const [noteCounterDraft, setNoteCounterDraft] = useState('');
  const [savingNoteCounter, setSavingNoteCounter] = useState(false);

  const startEditNoteCounter = (type: 'credit' | 'debit') => {
    setEditingNoteCounterType(type);
    setNoteCounterDraft(String(type === 'credit' ? noteCounters?.nextCreditNoteNumber ?? 1 : noteCounters?.nextDebitNoteNumber ?? 1));
  };

  const saveNoteCounter = async (type: 'credit' | 'debit') => {
    const value = parseInt(noteCounterDraft, 10);
    if (!Number.isFinite(value) || value < 1) {
      toast.error('Ingresa un número válido');
      return;
    }
    setSavingNoteCounter(true);
    try {
      const payload = type === 'credit' ? { nextCreditNoteNumber: value } : { nextDebitNoteNumber: value };
      const res = await api.put('/client/resolutions/note-counters', payload);
      setNoteCounters(res.data);
      toast.success('Consecutivo actualizado');
      setEditingNoteCounterType(null);
    } catch (err: any) {
      toast.error(getErrorMessage(err, 'Error al actualizar el consecutivo'));
    } finally {
      setSavingNoteCounter(false);
    }
  };

  const handleDeleteResolution = async (resId: string) => {
    if (!(await confirm("¿Seguro que deseas eliminar esta resolución?"))) return;
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
      await api.post('/client/dian/start-habilitation', { magicLink });
      toast.success("¡Habilitación configurada y en progreso!");
      setMagicLink('');
      loadHabilitationStatus();
    } catch (err: any) {
      toast.error(getErrorMessage(err, "Error al iniciar habilitación"));
    } finally {
      setIsHabilitating(false);
    }
  };

  const handlePreviewTestDocument = async () => {
    setLoadingPreview(true);
    setTestDocPreview(null);
    try {
      const res = await api.get('/client/dian/preview-test-document');
      setTestDocPreview(res.data);
    } catch (err: any) {
      toast.error(getErrorMessage(err, "Error armando la vista previa"));
    } finally {
      setLoadingPreview(false);
    }
  };

  const handleSendTestDocument = async () => {
    if (!(await confirm("Esto envía un documento real al set de pruebas de la DIAN y gasta uno de los intentos disponibles (no se puede deshacer). ¿Continuar?", { destructive: false, confirmText: 'Continuar' }))) return;
    setSendingTestDoc(true);
    try {
      const res = await api.post('/client/dian/send-test-document');
      toast.success(`Documento ${res.data.documentNumber} enviado a la DIAN.`);
      setTestDocPreview(null);
      loadHabilitationStatus();
    } catch (err: any) {
      toast.error(getErrorMessage(err, "Error enviando el documento de prueba"));
    } finally {
      setSendingTestDoc(false);
    }
  };

  const handleSetDefault = async (resId: string) => {
    try {
      await api.put(`/client/resolutions/${resId}/set-default`);
      toast.success('Resolución marcada como predeterminada');
      loadResolutions();
    } catch (err: any) {
      toast.error(getErrorMessage(err, 'Error al marcar como predeterminada'));
    }
  };

  const isNomina = newRes.documentType === 'NE';

  if (loading) {
    return (
      <div className="flex h-full items-center justify-center">
        <Loader2 className="w-10 h-10 animate-spin text-blue-500" />
      </div>
    );
  }

  return (
    <div className="p-8">
      <div className="flex justify-between items-center mb-8">
        <div>
          <h1 className="text-3xl font-extrabold text-slate-800 tracking-tight">Resoluciones DIAN</h1>
          <p className="text-slate-500 mt-2">Administra las autorizaciones de numeración para emitir facturas.</p>
        </div>
        <button onClick={openCreateModal} className="bg-primary hover:bg-primary-hover text-white px-5 py-2.5 rounded-xl font-semibold shadow-lg shadow-primary/30 transition-all hover:-translate-y-0.5 flex items-center gap-2">
          <Plus size={18} /> Nueva Resolución
        </button>
      </div>

      {/* Splash Screen Full-Screen */}
      {(isHabilitating || habilitationStatus?.status === 'Approved') && (
        <div className="fixed inset-0 bg-slate-900/80 backdrop-blur-md z-[100] flex items-center justify-center p-4">
          <div className="bg-white rounded-[2rem] p-10 max-w-lg w-full shadow-2xl flex flex-col items-center text-center animate-in zoom-in-95 duration-300">
            {habilitationStatus?.status === 'Approved' ? (
              <>
                <div className="w-24 h-24 bg-emerald-100 text-emerald-500 rounded-full flex items-center justify-center mb-6 shadow-inner ring-8 ring-emerald-50">
                  <svg className="w-12 h-12" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth="3" d="M5 13l4 4L19 7"></path></svg>
                </div>
                <h2 className="text-3xl font-black text-slate-800 mb-2">¡Habilitación Exitosa!</h2>
                <p className="text-slate-500 mb-8 text-lg">Tu empresa está lista para emitir facturas en producción.</p>
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
              </>
            )}
          </div>
        </div>
      )}

      {/* Panel de Set de Pruebas — visible una vez el software propio quedó registrado (status
          "Testing"), en vez del overlay de "en progreso" que antes se quedaba pegado ahí para
          siempre porque "Testing" es un estado real, no transitorio. */}
      {habilitationStatus?.status === 'Testing' && (
        <div className="bg-white rounded-3xl p-8 shadow-sm border border-slate-100 mb-8">
          <h2 className="text-xl font-bold text-slate-800 mb-2">Set de Pruebas DIAN</h2>
          <p className="text-slate-500 mb-6">{habilitationStatus?.message}</p>

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
              className="bg-primary hover:bg-primary-hover disabled:opacity-50 text-white px-6 py-3 rounded-xl font-bold shadow-md transition-all flex items-center gap-2"
            >
              {sendingTestDoc ? <Loader2 className="w-5 h-5 animate-spin" /> : null}
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
        </div>
      )}

      {/* Panel de Habilitación DIAN Automática */}
      <div className="bg-white rounded-3xl p-8 shadow-sm border border-slate-100 mb-8">
        <h2 className="text-xl font-bold text-slate-800 mb-2">Habilitación y Set de Pruebas</h2>
        <p className="text-slate-500 mb-6">Pega el enlace de habilitación enviado por la DIAN y completa el proceso.</p>

        {habilitationStatus?.status === 'Production' ? (
          <div className="bg-emerald-50 border border-emerald-100 rounded-2xl p-6 flex items-center gap-4">
            <div className="w-12 h-12 bg-emerald-500 text-white rounded-full flex items-center justify-center shadow-lg shadow-emerald-500/30">
              <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth="3" d="M5 13l4 4L19 7"></path></svg>
            </div>
            <div>
              <h3 className="font-bold text-emerald-900 text-lg">Empresa Habilitada en Producción</h3>
              <p className="text-emerald-700/80">Has completado los requisitos de la DIAN.</p>
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
              FacilFactura crea automáticamente tu Software Propio en la DIAN — el Software ID y el PIN los asigna la DIAN y se muestran arriba una vez completado el registro.
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
            <p className="text-slate-500 max-w-sm mt-2">Carga tu formulario 1876 para comenzar a facturar.</p>
            <button onClick={openCreateModal} className="mt-6 text-primary font-bold hover:underline">Configurar ahora</button>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="bg-slate-50 text-slate-500 text-sm border-y border-slate-200">
                  <th className="font-semibold py-3 px-4 rounded-tl-xl">Tipo / Prefijo</th>
                  <th className="font-semibold py-3 px-4">No. Resolución</th>
                  <th className="font-semibold py-3 px-4">Rango Autorizado</th>
                  <th className="font-semibold py-3 px-4">Próximo #</th>
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
                        <span className="font-bold text-slate-700 text-lg">{r.prefix || '-'}</span>
                      </div>
                    </td>
                    <td className="py-4 px-4 font-mono text-sm text-slate-600 font-medium">{r.resolutionNumber}</td>
                    <td className="py-4 px-4 text-sm text-slate-600">
                      <span className="font-bold">{r.numberStart}</span> a <span className="font-bold">{r.numberEnd}</span>
                    </td>
                    <td className="py-4 px-4 text-sm">
                      {editingNextNumberId === r.id ? (
                        <div className="flex items-center gap-1.5">
                          <input
                            type="number"
                            min={r.numberStart}
                            max={r.numberEnd}
                            autoFocus
                            className="w-24 px-2 py-1.5 bg-white border border-primary rounded-lg outline-none text-sm font-mono"
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
                        <button onClick={() => startEditNextNumber(r)} className="flex items-center gap-1.5 font-mono font-bold text-slate-700 hover:text-primary group">
                          {r.nextNumber ?? r.numberStart}
                          <Pencil size={13} className="text-slate-300 group-hover:text-primary" />
                        </button>
                      )}
                    </td>
                    <td className="py-4 px-4 text-sm text-slate-500">
                      {r.documentType === 'NE' ? 'Sin vencimiento' : `${new Date(r.validFrom).toLocaleDateString()} — ${new Date(r.validTo).toLocaleDateString()}`}
                    </td>
                    <td className="py-4 px-4 text-center">
                      {r.isDefault ? (
                        <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-xs font-bold bg-amber-100 text-amber-700">
                          <Star size={14} fill="currentColor" /> Predeterminada
                        </span>
                      ) : (
                        <button onClick={() => handleSetDefault(r.id)} className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-xs font-bold text-slate-400 hover:bg-amber-50 hover:text-amber-600 transition-colors" title="Marcar como predeterminada para este tipo de documento">
                          <Star size={14} /> Marcar
                        </button>
                      )}
                    </td>
                    <td className="py-4 px-4 text-center">
                      <div className="flex items-center justify-center gap-1">
                        <button onClick={() => startEditResolution(r)} className="p-2 text-slate-400 hover:text-primary hover:bg-primary/5 rounded-lg transition-colors" title="Editar">
                          <Pencil size={18} />
                        </button>
                        <button onClick={() => handleDeleteResolution(r.id)} className="p-2 text-rose-400 hover:text-rose-600 hover:bg-rose-50 rounded-lg transition-colors" title="Eliminar">
                          <Trash2 size={18} />
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
            Las Notas Crédito y Débito no tienen un rango autorizado por la DIAN (no aplica una resolución) — este consecutivo es interno, y sirve solo para numerarlas de forma ordenada.
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
              {(['credit', 'debit'] as const).map(type => (
                <tr key={type} className="border-b border-slate-100 hover:bg-slate-50/50 transition-colors">
                  <td className="py-4 px-4">
                    <span className={`px-2 py-1 rounded-md text-xs font-bold ${type === 'credit' ? 'bg-emerald-100 text-emerald-700' : 'bg-orange-100 text-orange-700'}`}>
                      {type === 'credit' ? 'Nota Crédito' : 'Nota Débito'}
                    </span>
                  </td>
                  <td className="py-4 px-4 text-sm">
                    {editingNoteCounterType === type ? (
                      <div className="flex items-center gap-1.5">
                        <input
                          type="number"
                          min={1}
                          autoFocus
                          className="w-24 px-2 py-1.5 bg-white border border-primary rounded-lg outline-none text-sm font-mono"
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
                      <button onClick={() => startEditNoteCounter(type)} className="flex items-center gap-1.5 font-mono font-bold text-slate-700 hover:text-primary group">
                        {type === 'credit' ? noteCounters?.nextCreditNoteNumber ?? 1 : noteCounters?.nextDebitNoteNumber ?? 1}
                        <Pencil size={13} className="text-slate-300 group-hover:text-primary" />
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {showResModal && (
        <div className="fixed inset-0 bg-slate-900/50 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-3xl p-8 max-w-2xl w-full shadow-2xl animate-in fade-in zoom-in duration-200">
            <div className="flex justify-between items-center mb-6">
              <h3 className="text-xl font-bold text-slate-800 flex items-center gap-2">
                <FileSignature className="text-primary" /> {editingId ? 'Editar Resolución DIAN' : 'Agregar Resolución DIAN'}
              </h3>
              <button onClick={closeResModal} className="text-slate-400 hover:text-slate-600 hover:bg-slate-100 p-2 rounded-full transition-colors">
                <X size={20} />
              </button>
            </div>

            <form onSubmit={handleSubmitResolution} className="space-y-5">
              {!editingId && (
                <div className="bg-primary/5 border border-primary/20 rounded-2xl p-5 flex items-center justify-between shadow-inner">
                  <div>
                    <h4 className="text-sm font-bold text-primary-hover">Cargar datos desde PDF</h4>
                    <p className="text-xs text-primary-hover/70 mt-1">Sube el formulario 1876 y completaremos los datos disponibles.</p>
                  </div>
                  <div>
                    <label className="cursor-pointer bg-white text-primary hover:text-primary-hover border border-primary/20 hover:border-primary/40 px-5 py-2.5 rounded-xl text-sm font-bold transition-all shadow-sm flex items-center gap-2">
                      {uploadingPdf ? <Loader2 className="w-5 h-5 animate-spin" /> : <FileSignature size={18} />}
                      {uploadingPdf ? 'Analizando...' : 'Cargar PDF'}
                      <input type="file" accept="application/pdf" className="hidden" onChange={handlePdfUpload} disabled={uploadingPdf} />
                    </label>
                  </div>
                </div>
              )}

              <div className="grid grid-cols-2 gap-5">
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-1.5">Tipo de Documento</label>
                  {editingId ? (
                    <div className="w-full px-4 py-3 bg-slate-100 border border-slate-200 rounded-xl font-medium text-slate-500 cursor-not-allowed" title="El tipo de documento no se puede cambiar al editar — crea una resolución nueva si necesitas otro tipo.">
                      {documentTypeLabels[newRes.documentType] || newRes.documentType}
                    </div>
                  ) : (
                    <SearchableSelect
                      required
                      inputClassName="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-primary outline-none transition-all font-medium text-slate-700"
                      value={newRes.documentType}
                      onChange={v => setNewRes({...newRes, documentType: v})}
                      placeholder="Buscar tipo de documento..."
                      options={[
                        { value: 'FE', label: 'Factura Electrónica (FE)' },
                        { value: 'NC', label: 'Nota Crédito (NC)' },
                        { value: 'ND', label: 'Nota Débito (ND)' },
                        { value: 'POS', label: 'Documento Soporte / POS' },
                        { value: 'NE', label: 'Nómina Electrónica (NE)' }
                      ]}
                    />
                  )}
                </div>
                {!isNomina && (
                  <div>
                    <label className="block text-sm font-bold text-slate-700 mb-1.5">No. de Resolución / Autorización</label>
                    <input required type="text" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-primary outline-none transition-all font-mono" placeholder="Ej. 1876..." value={newRes.resolutionNumber} onChange={e => setNewRes({...newRes, resolutionNumber: e.target.value})} />
                  </div>
                )}
              </div>

              {isNomina && (
                <p className="text-xs text-slate-500 bg-slate-50 border border-slate-200 rounded-xl p-3">
                  La nómina electrónica no tiene una resolución de numeración autorizada por la DIAN — el consecutivo lo administras tú libremente. Solo indica desde qué número quieres empezar.
                </p>
              )}

              <div className={`grid ${isNomina ? 'grid-cols-2' : 'grid-cols-3'} gap-5`}>
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-1.5">Prefijo</label>
                  <input type="text" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-primary outline-none transition-all uppercase font-bold" placeholder="Opcional" value={newRes.prefix} onChange={e => setNewRes({...newRes, prefix: e.target.value})} />
                </div>
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-1.5">{isNomina ? 'Número Inicial' : 'Desde'}</label>
                  <input required type="number" min="1" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-primary outline-none transition-all" value={newRes.numberStart || ''} onChange={e => setNewRes({...newRes, numberStart: parseInt(e.target.value) || 0})} />
                </div>
                {!isNomina && (
                  <div>
                    <label className="block text-sm font-bold text-slate-700 mb-1.5">Hasta</label>
                    <input required type="number" min="1" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-primary outline-none transition-all" value={newRes.numberEnd || ''} onChange={e => setNewRes({...newRes, numberEnd: parseInt(e.target.value) || 0})} />
                  </div>
                )}
              </div>

              {!isNomina && (
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
              )}

              {!isNomina && (
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-1.5">Clave Técnica (Solo FE)</label>
                  <input type="text" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-primary outline-none transition-all font-mono text-sm" placeholder="Pega aquí el hash técnico de la DIAN..." value={newRes.technicalKey} onChange={e => setNewRes({...newRes, technicalKey: e.target.value})} />
                </div>
              )}

              <div className="pt-6 border-t border-slate-100 flex justify-end gap-3">
                <button type="button" onClick={closeResModal} className="px-6 py-3 text-slate-500 hover:bg-slate-100 rounded-xl font-bold transition-colors">Cancelar</button>
                <button type="submit" className="bg-primary hover:bg-primary-hover text-white px-8 py-3 rounded-xl font-bold shadow-lg shadow-primary/30 transition-transform hover:-translate-y-0.5">
                  {editingId ? 'Guardar Cambios' : 'Guardar Resolución'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
