import React, { useState, useEffect } from 'react';
import { Inbox, Mail, Upload, Loader2, CheckCircle2, XCircle, Send } from 'lucide-react';
import { api, getErrorMessage } from '../lib/api';
import { toast } from 'sonner';
import { useNumberFormat } from '../lib/numberFormat';

const EVENT_INFO: Record<string, { label: string; description: string }> = {
  acuseRecibo: {
    label: 'Acuse de Recibo',
    description: 'Confirma que la factura fue recibida. Es el primer paso del proceso — normalmente se puede automatizar sin riesgo.'
  },
  reciboBien: {
    label: 'Recibo del Bien o Servicio',
    description: 'Confirma que el bien o servicio facturado efectivamente se recibió. Solo actívalo automático si tu proceso interno ya valida esto antes de que llegue la factura.'
  },
  aceptacion: {
    label: 'Aceptación Expresa',
    description: 'Declara que aceptas la factura como está — tiene efecto legal (título valor). Actívalo automático solo si confías en que tus proveedores no facturan con errores.'
  },
  reclamo: {
    label: 'Reclamo',
    description: 'Declara formalmente que hay un problema con la factura (no corresponde a lo pactado, error en los datos, etc.). Es una decisión de negocio — automatizarlo significa que se enviará sin que nadie lo revise primero. Solo actívalo si tienes una validación automática confiable antes.'
  }
};

export default function ReceivedDocumentsPage() {
  const fmt = useNumberFormat();
  const [loading, setLoading] = useState(true);
  const [documents, setDocuments] = useState<any[]>([]);
  const [settings, setSettings] = useState<any>({
    receptionEmailEnabled: false,
    receptionEmailHost: '',
    receptionEmailPort: 993,
    receptionEmailUseSsl: true,
    receptionEmailUser: '',
    hasPassword: false,
    autoSendAcuseRecibo: false,
    autoSendReciboBien: false,
    autoSendAceptacion: false,
    autoSendReclamo: false,
  });
  const [emailPasswordDraft, setEmailPasswordDraft] = useState('');
  const [savingSettings, setSavingSettings] = useState(false);
  const [testingConnection, setTestingConnection] = useState(false);
  const [uploading, setUploading] = useState(false);

  const loadDocuments = () => {
    api.get('/client/received-documents')
      .then(res => setDocuments(res.data))
      .catch(() => toast.error('Error al cargar los documentos recibidos'));
  };

  const loadSettings = () => {
    api.get('/client/reception-settings')
      .then(res => setSettings(res.data))
      .catch(() => toast.error('Error al cargar la configuración'))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    loadDocuments();
    loadSettings();
  }, []);

  const saveSettings = async () => {
    setSavingSettings(true);
    try {
      await api.put('/client/reception-settings', {
        ...settings,
        receptionEmailPassword: emailPasswordDraft || undefined
      });
      toast.success('Configuración guardada');
      setEmailPasswordDraft('');
      loadSettings();
    } catch (err: any) {
      toast.error(getErrorMessage(err, 'Error al guardar la configuración'));
    } finally {
      setSavingSettings(false);
    }
  };

  const testConnection = async () => {
    setTestingConnection(true);
    try {
      const res = await api.post('/client/reception-settings/test-connection');
      if (res.data.success) {
        toast.success(res.data.message);
      } else {
        toast.error(res.data.message);
      }
    } catch (err: any) {
      toast.error(getErrorMessage(err, 'Error al probar la conexión'));
    } finally {
      setTestingConnection(false);
    }
  };

  const handleUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setUploading(true);
    const formData = new FormData();
    formData.append('file', file);
    try {
      await api.post('/client/received-documents/upload', formData, {
        headers: { 'Content-Type': 'multipart/form-data' }
      });
      toast.success('Documento cargado');
      loadDocuments();
    } catch (err: any) {
      toast.error(getErrorMessage(err, 'Error al cargar el documento'));
    } finally {
      setUploading(false);
      e.target.value = '';
    }
  };

  const triggerEvent = async (docId: string, eventCode: string) => {
    try {
      const res = await api.post(`/client/received-documents/${docId}/events/${eventCode}`);
      if (res.data.status === 'SENT') {
        toast.success('Evento enviado a la DIAN');
      } else {
        toast.error(res.data.dianResponseMessage || 'La DIAN rechazó el evento');
      }
      loadDocuments();
    } catch (err: any) {
      toast.error(getErrorMessage(err, 'Error al disparar el evento'));
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
    <div className="p-8 space-y-8">
      <div>
        <h1 className="text-3xl font-extrabold text-slate-800 tracking-tight">Documentos recibidos</h1>
        <p className="text-slate-500 mt-2">Consulta documentos de tus proveedores y gestiona sus eventos.</p>
      </div>

      {/* Conexión de correo */}
      <div className="bg-white rounded-3xl p-8 shadow-sm border border-slate-100">
        <div className="flex items-center gap-2 mb-2">
          <Mail className="text-primary" size={20} />
          <h2 className="text-xl font-bold text-slate-800">Correo de Facturación Electrónica</h2>
        </div>
        <p className="text-slate-500 mb-6 text-sm">
          Conecta el buzón donde te llegan las facturas de tus proveedores para procesarlas automáticamente.
          Para Gmail u Outlook, usa una <strong>contraseña de aplicación</strong> (no tu clave normal) — esos proveedores ya no aceptan la clave normal por este medio.
        </p>

        <label className="flex items-center gap-3 mb-5 cursor-pointer">
          <input
            type="checkbox"
            checked={settings.receptionEmailEnabled}
            onChange={e => setSettings({ ...settings, receptionEmailEnabled: e.target.checked })}
            className="w-5 h-5 rounded accent-primary"
          />
          <span className="font-semibold text-slate-700">Activar conexión de correo</span>
        </label>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
          <div>
            <label className="block text-sm font-bold text-slate-700 mb-1.5">Servidor IMAP</label>
            <input type="text" placeholder="imap.gmail.com" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl outline-none focus:ring-2 focus:ring-primary"
              value={settings.receptionEmailHost} onChange={e => setSettings({ ...settings, receptionEmailHost: e.target.value })} />
          </div>
          <div>
            <label className="block text-sm font-bold text-slate-700 mb-1.5">Puerto</label>
            <input type="number" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl outline-none focus:ring-2 focus:ring-primary"
              value={settings.receptionEmailPort} onChange={e => setSettings({ ...settings, receptionEmailPort: parseInt(e.target.value) || 993 })} />
          </div>
          <div>
            <label className="block text-sm font-bold text-slate-700 mb-1.5">Usuario / Correo</label>
            <input type="email" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl outline-none focus:ring-2 focus:ring-primary"
              value={settings.receptionEmailUser} onChange={e => setSettings({ ...settings, receptionEmailUser: e.target.value })} />
          </div>
          <div>
            <label className="block text-sm font-bold text-slate-700 mb-1.5">
              Contraseña {settings.hasPassword && <span className="text-emerald-600 font-normal">(ya guardada — deja en blanco para no cambiarla)</span>}
            </label>
            <input type="password" placeholder={settings.hasPassword ? '••••••••' : ''} className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl outline-none focus:ring-2 focus:ring-primary"
              value={emailPasswordDraft} onChange={e => setEmailPasswordDraft(e.target.value)} />
          </div>
        </div>

        <label className="flex items-center gap-2 mt-4 cursor-pointer">
          <input type="checkbox" checked={settings.receptionEmailUseSsl} onChange={e => setSettings({ ...settings, receptionEmailUseSsl: e.target.checked })} className="w-4 h-4 rounded accent-primary" />
          <span className="text-sm text-slate-600">Usar SSL/TLS (recomendado)</span>
        </label>

        <div className="flex gap-3 mt-6">
          <button onClick={saveSettings} disabled={savingSettings} className="bg-primary hover:bg-primary-hover disabled:opacity-50 text-white px-6 py-2.5 rounded-xl font-bold shadow-md transition-all">
            {savingSettings ? 'Guardando...' : 'Guardar configuración'}
          </button>
          <button onClick={testConnection} disabled={testingConnection} className="bg-slate-100 hover:bg-slate-200 disabled:opacity-50 text-slate-700 px-6 py-2.5 rounded-xl font-bold transition-all">
            {testingConnection ? 'Probando...' : 'Probar conexión'}
          </button>
        </div>
      </div>

      {/* Eventos automáticos */}
      <div className="bg-white rounded-3xl p-8 shadow-sm border border-slate-100">
        <h2 className="text-xl font-bold text-slate-800 mb-2">Eventos automáticos</h2>
        <p className="text-slate-500 mb-6 text-sm">Elige qué eventos se crearán automáticamente al recibir documentos.</p>

        <div className="space-y-4">
          {([
            ['autoSendAcuseRecibo', 'acuseRecibo'],
            ['autoSendReciboBien', 'reciboBien'],
            ['autoSendAceptacion', 'aceptacion'],
            ['autoSendReclamo', 'reclamo'],
          ] as const).map(([field, infoKey]) => (
            <label key={field} className="flex items-start gap-3 p-4 bg-slate-50 rounded-2xl border border-slate-100 cursor-pointer">
              <input type="checkbox" checked={settings[field]} onChange={e => setSettings({ ...settings, [field]: e.target.checked })} className="w-5 h-5 mt-0.5 rounded accent-primary shrink-0" />
              <div>
                <span className="font-bold text-slate-800">{EVENT_INFO[infoKey].label}</span>
                <p className="text-xs text-slate-500 mt-0.5">{EVENT_INFO[infoKey].description}</p>
              </div>
            </label>
          ))}
        </div>

        <button onClick={saveSettings} disabled={savingSettings} className="mt-6 bg-primary hover:bg-primary-hover disabled:opacity-50 text-white px-6 py-2.5 rounded-xl font-bold shadow-md transition-all">
          {savingSettings ? 'Guardando...' : 'Guardar eventos automáticos'}
        </button>
      </div>

      {/* Documentos recibidos */}
      <div className="bg-white rounded-3xl p-8 shadow-sm border border-slate-100">
        <div className="flex justify-between items-center mb-6">
          <div className="flex items-center gap-2">
            <Inbox className="text-primary" size={20} />
            <h2 className="text-xl font-bold text-slate-800">Documentos Recibidos</h2>
          </div>
          <label className="cursor-pointer bg-slate-900 hover:bg-black text-white px-5 py-2.5 rounded-xl text-sm font-bold transition-all shadow-sm flex items-center gap-2">
            {uploading ? <Loader2 className="w-4 h-4 animate-spin" /> : <Upload size={16} />}
            {uploading ? 'Cargando...' : 'Cargar ZIP/XML'}
            <input type="file" accept=".zip,.xml" className="hidden" onChange={handleUpload} disabled={uploading} />
          </label>
        </div>

        {documents.length === 0 ? (
          <div className="flex flex-col items-center justify-center text-center h-48 border-2 border-dashed border-slate-200 rounded-2xl bg-slate-50/50">
            <Inbox className="text-slate-300 mb-3" size={32} />
            <p className="text-slate-500">Aún no tienes documentos recibidos.</p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="bg-slate-50 text-slate-500 text-sm border-y border-slate-200">
                  <th className="font-semibold py-3 px-4 rounded-tl-xl">Proveedor</th>
                  <th className="font-semibold py-3 px-4">Documento</th>
                  <th className="font-semibold py-3 px-4">Fecha</th>
                  <th className="font-semibold py-3 px-4">Total</th>
                  <th className="font-semibold py-3 px-4">Origen</th>
                  <th className="font-semibold py-3 px-4 rounded-tr-xl">Eventos</th>
                </tr>
              </thead>
              <tbody>
                {documents.map(doc => {
                  const sentCodes = new Set(doc.events.filter((e: any) => e.status === 'SENT').map((e: any) => e.eventCode));
                  return (
                    <tr key={doc.id} className="border-b border-slate-100 hover:bg-slate-50/50 transition-colors align-top">
                      <td className="py-4 px-4 font-semibold text-slate-700">{doc.issuerName || doc.issuerTaxId}</td>
                      <td className="py-4 px-4 font-mono text-sm text-slate-600">{doc.documentId}</td>
                      <td className="py-4 px-4 text-sm text-slate-500">{new Date(doc.issueDate).toLocaleDateString()}</td>
                      <td className="py-4 px-4 font-mono text-sm">${fmt.number(Number(doc.totalAmount), 3)}</td>
                      <td className="py-4 px-4 text-xs text-slate-400">{doc.sourceType === 'Email' ? 'Correo' : 'Manual'}</td>
                      <td className="py-4 px-4">
                        <div className="flex flex-wrap gap-1.5">
                          {(['030', '032', '033', '031'] as const).map(code => {
                            const sent = sentCodes.has(code);
                            const label = { '030': 'Acuse', '032': 'Recibo', '033': 'Aceptación', '031': 'Reclamo' }[code];
                            return sent ? (
                              <span key={code} className="inline-flex items-center gap-1 px-2 py-1 rounded-lg bg-emerald-50 text-emerald-700 text-xs font-bold">
                                <CheckCircle2 size={12} /> {label}
                              </span>
                            ) : (
                              <button key={code} onClick={() => triggerEvent(doc.id, code)} className="inline-flex items-center gap-1 px-2 py-1 rounded-lg bg-slate-100 hover:bg-slate-200 text-slate-600 text-xs font-bold transition-colors">
                                <Send size={12} /> {label}
                              </button>
                            );
                          })}
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
