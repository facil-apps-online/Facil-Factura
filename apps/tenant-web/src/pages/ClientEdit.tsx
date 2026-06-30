import React, { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { GoogleMap, useJsApiLoader, Autocomplete, Marker } from '@react-google-maps/api';
import { ArrowLeft, Save, Building2, FileKey, FileSignature, ShieldAlert, Loader2, MapPin, Plus, Trash2, X, Copy } from 'lucide-react';
import { api } from '../lib/api';
import { toast } from 'sonner';

const libraries: "places"[] = ['places'];
// TODO: El usuario deberá reemplazar esto por su API Key real en el .env
const GOOGLE_MAPS_API_KEY = import.meta.env.VITE_GOOGLE_MAPS_API_KEY || "AIzaSy_TU_LLAVE_DE_PRUEBA_AQUI"; 

export default function ClientEdit() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState('info');
  const [resolutions, setResolutions] = useState<any[]>([]);
  const [showResModal, setShowResModal] = useState(false);
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
  const [certInfo, setCertInfo] = useState<any>(null);
  const [certFile, setCertFile] = useState<File | null>(null);
  const [certPassword, setCertPassword] = useState('');
  const [uploadingCert, setUploadingCert] = useState(false);
  const [client, setClient] = useState({
    companyName: '',
    commercialName: '',
    taxId: '',
    verificationDigit: '',
    email: '',
    phone: '',
    address: '',
    city: '',
    taxRegime: '',
    economicActivity: '',
    latitude: null as number | null,
    longitude: null as number | null,
    isActive: true,
    liveApiKey: '',
    liveApiSecret: '',
    testApiKey: '',
    testApiSecret: ''
  });

  useEffect(() => {
    if (id) {
      api.get(`/tenant/clients/${id}`)
        .then(res => setClient(prev => ({
          ...prev,
          ...res.data,
          latitude: res.data.latitude || 4.6097, // Default a Bogotá si no tiene
          longitude: res.data.longitude || -74.0817
        })))
        .catch(() => toast.error("No se pudo cargar el cliente"))
        .finally(() => setLoading(false));
    }
  }, [id]);

  const loadResolutions = () => {
    api.get(`/tenant/clients/${id}/resolutions`)
      .then(res => setResolutions(res.data))
      .catch(() => toast.error("Error al cargar resoluciones"));
  };

  const loadCertificate = () => {
    api.get(`/tenant/clients/${id}/certificate`)
      .then(res => setCertInfo(res.data))
      .catch(() => setCertInfo(null));
  };

  useEffect(() => {
    if (activeTab === 'resolutions') loadResolutions();
    if (activeTab === 'certificate') loadCertificate();
  }, [activeTab]);

  const autocompleteRef = React.useRef<google.maps.places.Autocomplete | null>(null);

  const { isLoaded } = useJsApiLoader({
    id: 'google-map-script',
    googleMapsApiKey: GOOGLE_MAPS_API_KEY,
    libraries
  });

  const onLoadAutocomplete = (autocomplete: google.maps.places.Autocomplete) => {
    autocompleteRef.current = autocomplete;
  };

  const onPlaceChanged = () => {
    if (autocompleteRef.current !== null) {
      const place = autocompleteRef.current.getPlace();
      if (place.geometry && place.geometry.location) {
        const lat = place.geometry.location.lat();
        const lng = place.geometry.location.lng();
        
        let newCity = '';
        place.address_components?.forEach(component => {
          if (component.types.includes('locality')) {
            newCity = component.long_name;
          }
        });

        setClient(prev => ({
          ...prev,
          address: place.formatted_address || '',
          city: newCity || prev.city,
          latitude: lat,
          longitude: lng
        }));
      }
    }
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await api.put(`/tenant/clients/${id}`, {
        companyName: client.companyName,
        commercialName: client.commercialName,
        taxId: client.taxId,
        verificationDigit: client.verificationDigit,
        email: client.email,
        phone: client.phone,
        address: client.address,
        city: client.city,
        taxRegime: client.taxRegime,
        economicActivity: client.economicActivity,
        latitude: client.latitude,
        longitude: client.longitude
      });
      toast.success("Información del cliente actualizada exitosamente.");
    } catch (err) {
      toast.error("Error al actualizar la información.");
    }
  };

  const generateKey = async (env: 'live' | 'test') => {
    try {
      const res = await api.post(`/tenant/clients/${id}/generate-key?env=${env}`);
      setClient(prev => ({
        ...prev,
        [env === 'live' ? 'liveApiKey' : 'testApiKey']: res.data.key,
        [env === 'live' ? 'liveApiSecret' : 'testApiSecret']: res.data.secret
      }));
      toast.success(`Llaves de ${env === 'live' ? 'producción' : 'pruebas'} generadas exitosamente.`);
    } catch (err) {
      toast.error("Error al generar la credencial.");
    }
  };

  const copyToClipboard = (text: string, label: string) => {
    if (!text || text === 'No generada') {
      toast.error(`No hay ${label} para copiar.`);
      return;
    }
    navigator.clipboard.writeText(text);
    toast.success(`${label} copiada al portapapeles.`);
  };

  const handleCreateResolution = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await api.post(`/tenant/clients/${id}/resolutions`, newRes);
      toast.success("Resolución agregada");
      setShowResModal(false);
      loadResolutions();
    } catch (err) {
      toast.error("Error al crear resolución");
    }
  };

  const handleDeleteResolution = async (resId: string) => {
    if (!confirm("¿Eliminar esta resolución?")) return;
    try {
      await api.delete(`/tenant/clients/${id}/resolutions/${resId}`);
      toast.success("Resolución eliminada");
      loadResolutions();
    } catch (err) {
      toast.error("Error al eliminar");
    }
  };

  const handleUploadCertificate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!certFile || !certPassword) {
      toast.error("El archivo y la contraseña son obligatorios");
      return;
    }
    
    setUploadingCert(true);
    const formData = new FormData();
    formData.append("file", certFile);
    formData.append("password", certPassword);

    try {
      await api.post(`/tenant/clients/${id}/certificate`, formData, {
        headers: { 'Content-Type': 'multipart/form-data' }
      });
      toast.success("Certificado cargado y validado con éxito");
      setCertFile(null);
      setCertPassword('');
      loadCertificate();
    } catch (err: any) {
      toast.error(err.response?.data || "Error al cargar el certificado");
    } finally {
      setUploadingCert(false);
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
    <div className="h-full flex flex-col overflow-hidden bg-slate-50/50">
      {/* Cabecera */}
      <header className="bg-white border-b border-slate-200 px-8 py-5 flex items-center justify-between shrink-0 shadow-sm z-10">
        <div className="flex items-center gap-4">
          <button 
            onClick={() => navigate('/clients')}
            className="p-2 hover:bg-slate-100 text-slate-500 hover:text-slate-800 rounded-lg transition-colors"
          >
            <ArrowLeft size={20} />
          </button>
          <div>
            <h1 className="text-2xl font-bold text-slate-800 tracking-tight">{client.companyName}</h1>
            <div className="flex items-center gap-2 text-sm text-slate-500 mt-0.5">
              <span>NIT: {client.taxId}</span>
              <span className="w-1 h-1 rounded-full bg-slate-300"></span>
              <span className={client.isActive ? 'text-emerald-600 font-medium' : 'text-rose-600 font-medium'}>
                {client.isActive ? 'Emisor Activo' : 'Emisor Inactivo'}
              </span>
            </div>
          </div>
        </div>
      </header>

      {/* Contenido / Tabs */}
      <div className="flex-1 flex overflow-hidden">
        {/* Menú lateral interno */}
        <div className="w-64 bg-slate-50/80 border-r border-slate-200 p-6 shrink-0 flex flex-col gap-2 overflow-y-auto">
          <button 
            onClick={() => setActiveTab('info')}
            className={`flex items-center gap-3 px-4 py-3 rounded-xl transition-all text-sm font-medium ${
              activeTab === 'info' ? 'bg-white shadow-sm border border-slate-200 text-blue-600' : 'text-slate-600 hover:bg-slate-100'
            }`}
          >
            <Building2 size={18} /> Info. Básica
          </button>
          <button 
            onClick={() => setActiveTab('resolutions')}
            className={`flex items-center gap-3 px-4 py-3 rounded-xl transition-all text-sm font-medium ${
              activeTab === 'resolutions' ? 'bg-white shadow-sm border border-slate-200 text-blue-600' : 'text-slate-600 hover:bg-slate-100'
            }`}
          >
            <FileSignature size={18} /> Resoluciones DIAN
          </button>
          <button 
            onClick={() => setActiveTab('certificate')}
            className={`flex items-center gap-3 px-4 py-3 rounded-xl transition-all text-sm font-medium ${
              activeTab === 'certificate' ? 'bg-white shadow-sm border border-slate-200 text-blue-600' : 'text-slate-600 hover:bg-slate-100'
            }`}
          >
            <FileKey size={18} /> Certificado Digital
          </button>
          <button 
            onClick={() => setActiveTab('credentials')}
            className={`flex items-center gap-3 px-4 py-3 rounded-xl transition-all text-sm font-medium ${
              activeTab === 'credentials' ? 'bg-white shadow-sm border border-slate-200 text-blue-600' : 'text-slate-600 hover:bg-slate-100'
            }`}
          >
            <ShieldAlert size={18} /> Credenciales API
          </button>
        </div>

        {/* Panel principal con scroll propio */}
        <div className="flex-1 overflow-y-auto p-10 animate-in fade-in duration-300">
          <div className="max-w-4xl">
            {activeTab === 'info' && (
              <div className="bg-white rounded-3xl p-8 shadow-sm border border-slate-100">
                <h2 className="text-xl font-bold text-slate-800 mb-6">Información del Emisor</h2>
                <form onSubmit={handleSave} className="space-y-6">
                  
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
                    {/* Identidad */}
                    <div className="space-y-6">
                      <h3 className="text-sm font-bold text-slate-400 uppercase tracking-wider border-b border-slate-100 pb-2">Identidad Tributaria</h3>
                      
                      <div>
                        <label className="block text-sm font-semibold text-slate-700 mb-2">Razón Social</label>
                        <input type="text" required className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none transition-all" value={client.companyName} onChange={e => setClient({...client, companyName: e.target.value})} />
                      </div>
                      
                      <div>
                        <label className="block text-sm font-semibold text-slate-700 mb-2">Nombre Comercial</label>
                        <input type="text" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none transition-all" value={client.commercialName} onChange={e => setClient({...client, commercialName: e.target.value})} />
                      </div>

                      <div className="grid grid-cols-4 gap-4">
                        <div className="col-span-3">
                          <label className="block text-sm font-semibold text-slate-700 mb-2">NIT</label>
                          <input type="text" required className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none transition-all" value={client.taxId} onChange={e => setClient({...client, taxId: e.target.value})} />
                        </div>
                        <div className="col-span-1">
                          <label className="block text-sm font-semibold text-slate-700 mb-2">DV</label>
                          <input type="text" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none transition-all text-center" value={client.verificationDigit} onChange={e => setClient({...client, verificationDigit: e.target.value})} />
                        </div>
                      </div>

                      <div className="grid grid-cols-2 gap-4">
                        <div>
                          <label className="block text-sm font-semibold text-slate-700 mb-2">Régimen Fiscal</label>
                          <select className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none transition-all appearance-none" value={client.taxRegime} onChange={e => setClient({...client, taxRegime: e.target.value})}>
                            <option value="">Seleccione...</option>
                            <option value="48">Resp. de IVA (48)</option>
                            <option value="49">No Resp. de IVA (49)</option>
                          </select>
                        </div>
                        <div>
                          <label className="block text-sm font-semibold text-slate-700 mb-2">CIIU</label>
                          <input type="text" placeholder="Ej. 6201" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none transition-all" value={client.economicActivity} onChange={e => setClient({...client, economicActivity: e.target.value})} />
                        </div>
                      </div>
                    </div>

                    {/* Contacto y Ubicación */}
                    <div className="space-y-6">
                      <h3 className="text-sm font-bold text-slate-400 uppercase tracking-wider border-b border-slate-100 pb-2">Contacto y Ubicación (Google Maps)</h3>
                      
                      <div>
                        <label className="block text-sm font-semibold text-slate-700 mb-2">Correo de Alertas / Facturación</label>
                        <input type="email" required className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none transition-all" value={client.email} onChange={e => setClient({...client, email: e.target.value})} />
                      </div>

                      <div>
                        <label className="block text-sm font-semibold text-slate-700 mb-2">Teléfono Celular o Fijo</label>
                        <input type="text" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none transition-all" value={client.phone} onChange={e => setClient({...client, phone: e.target.value})} />
                      </div>

                      <div>
                        <label className="block text-sm font-semibold text-slate-700 mb-2">Buscar Dirección Oficial</label>
                        {isLoaded ? (
                          <Autocomplete onLoad={onLoadAutocomplete} onPlaceChanged={onPlaceChanged}>
                            <input 
                              type="text" 
                              placeholder="Busca en Google Maps..."
                              className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none transition-all"
                              value={client.address}
                              onChange={e => setClient({...client, address: e.target.value})}
                            />
                          </Autocomplete>
                        ) : (
                          <input type="text" disabled placeholder="Cargando mapas..." className="w-full px-4 py-3 bg-slate-100 text-slate-500 border border-slate-200 rounded-xl" />
                        )}
                      </div>

                      <div className="h-48 rounded-2xl overflow-hidden border border-slate-200 shadow-inner relative">
                        {isLoaded && client.latitude && client.longitude ? (
                          <GoogleMap
                            mapContainerStyle={{ width: '100%', height: '100%' }}
                            center={{ lat: client.latitude, lng: client.longitude }}
                            zoom={15}
                            options={{ disableDefaultUI: true, zoomControl: true }}
                          >
                            <Marker position={{ lat: client.latitude, lng: client.longitude }} />
                          </GoogleMap>
                        ) : (
                          <div className="w-full h-full bg-slate-100 flex items-center justify-center text-slate-400 font-medium text-sm">
                            <MapPin className="w-8 h-8 opacity-50 mb-2" />
                            <span>Ubique el negocio en el mapa</span>
                          </div>
                        )}
                      </div>

                      <div>
                        <label className="block text-sm font-semibold text-slate-700 mb-2">Municipio / Ciudad (Auto-completado)</label>
                        <input type="text" placeholder="Ej. 11001 (Bogotá)" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none transition-all" value={client.city} onChange={e => setClient({...client, city: e.target.value})} />
                      </div>
                    </div>
                  </div>

                  <div className="pt-6 border-t border-slate-100 flex justify-end">
                    <button type="submit" className="bg-blue-600 hover:bg-blue-700 text-white px-8 py-3 rounded-xl font-semibold shadow-lg shadow-blue-500/30 flex items-center gap-2 transition-transform hover:-translate-y-0.5">
                      <Save size={18} />
                      Guardar Cambios
                    </button>
                  </div>
                </form>
              </div>
            )}

            {activeTab === 'resolutions' && (
              <div className="bg-white rounded-3xl p-8 shadow-sm border border-slate-100">
                <div className="flex justify-between items-center mb-6">
                  <h2 className="text-xl font-bold text-slate-800">Resoluciones de Facturación</h2>
                  <button onClick={() => setShowResModal(true)} className="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-xl font-medium shadow-md transition-colors flex items-center gap-2 text-sm">
                    <Plus size={16} /> Nueva Resolución
                  </button>
                </div>

                {resolutions.length === 0 ? (
                  <div className="flex flex-col items-center justify-center text-center h-64 border-2 border-dashed border-slate-200 rounded-2xl bg-slate-50/50">
                    <div className="w-16 h-16 bg-blue-50 text-blue-600 rounded-full flex items-center justify-center mb-4">
                      <FileSignature size={32} />
                    </div>
                    <h3 className="text-lg font-bold text-slate-700">Sin Resoluciones</h3>
                    <p className="text-slate-500 max-w-sm mt-2">No hay rangos de numeración activos para este emisor.</p>
                  </div>
                ) : (
                  <div className="overflow-x-auto">
                    <table className="w-full text-left border-collapse">
                      <thead>
                        <tr className="bg-slate-50 text-slate-500 text-sm border-y border-slate-200">
                          <th className="font-semibold py-3 px-4 rounded-tl-xl">Tipo / Prefijo</th>
                          <th className="font-semibold py-3 px-4">Resolución</th>
                          <th className="font-semibold py-3 px-4">Rango</th>
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
                                <span className="font-bold text-slate-700">{r.prefix || '-'}</span>
                              </div>
                            </td>
                            <td className="py-4 px-4 font-mono text-sm text-slate-600">{r.resolutionNumber}</td>
                            <td className="py-4 px-4 text-sm text-slate-600">{r.numberStart} a {r.numberEnd}</td>
                            <td className="py-4 px-4 text-sm text-slate-500">
                              {new Date(r.validFrom).toLocaleDateString()} - {new Date(r.validTo).toLocaleDateString()}
                            </td>
                            <td className="py-4 px-4 text-center">
                              <button onClick={() => handleDeleteResolution(r.id)} className="p-2 text-rose-500 hover:bg-rose-50 rounded-lg transition-colors">
                                <Trash2 size={16} />
                              </button>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            )}

            {activeTab === 'certificate' && (
              <div className="bg-white rounded-3xl p-8 shadow-sm border border-slate-100">
                <div className="flex items-center gap-4 mb-8">
                  <div className="w-12 h-12 bg-blue-50 text-blue-600 rounded-xl flex items-center justify-center">
                    <FileKey size={24} />
                  </div>
                  <div>
                    <h2 className="text-xl font-bold text-slate-800">Certificado Digital (Firma Electrónica)</h2>
                    <p className="text-sm text-slate-500">Carga el certificado .p12 o .pfx para firmar XML en nombre de este emisor.</p>
                  </div>
                </div>

                <div className="grid grid-cols-1 lg:grid-cols-2 gap-8">
                  {/* Estado Actual */}
                  <div className="bg-slate-50 border border-slate-200 rounded-2xl p-6">
                    <h3 className="text-sm font-bold text-slate-400 uppercase tracking-wider mb-4">Estado del Certificado</h3>
                    {certInfo ? (
                      <div className="space-y-4">
                        <div className="flex items-center gap-3">
                          <div className="w-3 h-3 rounded-full bg-emerald-500 shadow-[0_0_10px_rgba(16,185,129,0.5)]"></div>
                          <span className="font-semibold text-slate-700">Certificado Activo y Validado</span>
                        </div>
                        <div className="pt-2 border-t border-slate-200">
                          <p className="text-sm text-slate-500 mb-1">Archivo:</p>
                          <p className="font-mono text-sm text-slate-700 bg-white p-2 border border-slate-200 rounded-lg overflow-hidden text-ellipsis whitespace-nowrap" title={certInfo.fileName}>{certInfo.fileName}</p>
                        </div>
                        <div>
                          <p className="text-sm text-slate-500 mb-1">Fecha de Caducidad:</p>
                          <p className="font-bold text-slate-700">{new Date(certInfo.expirationDate).toLocaleDateString()} {new Date(certInfo.expirationDate).toLocaleTimeString()}</p>
                        </div>
                        {new Date(certInfo.expirationDate) < new Date() && (
                          <div className="p-3 bg-rose-50 text-rose-600 border border-rose-200 rounded-xl text-sm font-medium">
                            El certificado ha expirado. Por favor, sube uno nuevo.
                          </div>
                        )}
                      </div>
                    ) : (
                      <div className="flex flex-col items-center justify-center h-40 text-center">
                        <ShieldAlert className="text-slate-300 w-12 h-12 mb-2" />
                        <span className="font-medium text-slate-500">No hay certificado cargado.</span>
                        <span className="text-xs text-slate-400 mt-1">El emisor no podrá firmar facturas.</span>
                      </div>
                    )}
                  </div>

                  {/* Formulario Carga */}
                  <form onSubmit={handleUploadCertificate} className="bg-white border border-slate-200 rounded-2xl p-6 shadow-sm">
                    <h3 className="text-sm font-bold text-slate-400 uppercase tracking-wider mb-4">Reemplazar / Subir Certificado</h3>
                    
                    <div className="space-y-4">
                      <div>
                        <label className="block text-sm font-semibold text-slate-700 mb-2">Archivo .p12 / .pfx</label>
                        <input 
                          type="file" 
                          accept=".p12,.pfx"
                          required
                          onChange={e => setCertFile(e.target.files ? e.target.files[0] : null)}
                          className="w-full text-sm text-slate-500 file:mr-4 file:py-2.5 file:px-4 file:rounded-xl file:border-0 file:text-sm file:font-semibold file:bg-blue-50 file:text-blue-700 hover:file:bg-blue-100 outline-none cursor-pointer border border-slate-200 rounded-xl bg-slate-50"
                        />
                      </div>
                      <div>
                        <label className="block text-sm font-semibold text-slate-700 mb-2">Contraseña del Certificado</label>
                        <input 
                          type="password" 
                          required
                          value={certPassword}
                          onChange={e => setCertPassword(e.target.value)}
                          placeholder="Ingresa la contraseña..."
                          className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none"
                        />
                      </div>
                      <div className="pt-2">
                        <button 
                          type="submit" 
                          disabled={uploadingCert || !certFile || !certPassword}
                          className="w-full bg-slate-800 hover:bg-slate-900 disabled:opacity-50 text-white py-3 rounded-xl font-bold shadow-md transition-colors flex justify-center items-center gap-2"
                        >
                          {uploadingCert ? <Loader2 className="w-5 h-5 animate-spin" /> : <FileKey size={18} />}
                          {uploadingCert ? 'Validando...' : 'Cargar y Validar'}
                        </button>
                      </div>
                    </div>
                  </form>
                </div>
              </div>
            )}
            
            {activeTab === 'credentials' && (
              <div className="bg-white rounded-3xl p-8 shadow-sm border border-slate-100">
                <h2 className="text-xl font-bold text-slate-800 mb-6">Credenciales de Integración B2B</h2>
                <div className="space-y-6">
                  <div className="bg-slate-50 border border-slate-200 p-6 rounded-2xl">
                    <div className="flex justify-between items-start mb-4">
                      <div>
                        <h3 className="font-semibold text-slate-800 mb-1">Credenciales de Producción (Live)</h3>
                        <p className="text-slate-500 text-sm">Usa estas credenciales para emitir documentos con validez legal.</p>
                      </div>
                      <button type="button" onClick={() => generateKey('live')} className="px-4 py-2 bg-slate-800 text-white rounded-lg font-medium hover:bg-slate-700 transition-colors">Regenerar</button>
                    </div>
                    <div className="space-y-3">
                      <div>
                        <label className="block text-xs font-bold text-slate-500 uppercase mb-1">API Key</label>
                        <div className="flex gap-2">
                          <input type="text" readOnly value={client.liveApiKey || 'No generada'} className="flex-1 px-4 py-2 bg-white border border-slate-300 rounded-lg text-slate-600 font-mono text-sm outline-none" />
                          <button type="button" onClick={() => copyToClipboard(client.liveApiKey, 'API Key (Live)')} className="px-3 bg-white border border-slate-300 rounded-lg hover:bg-slate-50 text-slate-500 transition-colors" title="Copiar API Key">
                            <Copy size={16} />
                          </button>
                        </div>
                      </div>
                      <div>
                        <label className="block text-xs font-bold text-slate-500 uppercase mb-1">API Secret (HMAC)</label>
                        <div className="flex gap-2">
                          <input type="text" readOnly value={client.liveApiSecret || 'No generada'} className="flex-1 px-4 py-2 bg-white border border-slate-300 rounded-lg text-slate-600 font-mono text-sm outline-none" />
                          <button type="button" onClick={() => copyToClipboard(client.liveApiSecret, 'API Secret (Live)')} className="px-3 bg-white border border-slate-300 rounded-lg hover:bg-slate-50 text-slate-500 transition-colors" title="Copiar API Secret">
                            <Copy size={16} />
                          </button>
                        </div>
                      </div>
                    </div>
                  </div>
                  <div className="bg-amber-50/50 border border-amber-200/50 p-6 rounded-2xl">
                    <div className="flex justify-between items-start mb-4">
                      <div>
                        <h3 className="font-semibold text-slate-800 mb-1">Credenciales de Pruebas (Test / Sandbox)</h3>
                        <p className="text-slate-500 text-sm">Entorno de habilitación de la DIAN.</p>
                      </div>
                      <button type="button" onClick={() => generateKey('test')} className="px-4 py-2 bg-white border border-amber-300 text-amber-700 rounded-lg font-medium hover:bg-amber-50 transition-colors">Regenerar</button>
                    </div>
                    <div className="space-y-3">
                      <div>
                        <label className="block text-xs font-bold text-amber-600 uppercase mb-1">API Key</label>
                        <div className="flex gap-2">
                          <input type="text" readOnly value={client.testApiKey || 'No generada'} className="flex-1 px-4 py-2 bg-white border border-amber-200 rounded-lg text-slate-600 font-mono text-sm outline-none" />
                          <button type="button" onClick={() => copyToClipboard(client.testApiKey, 'API Key (Test)')} className="px-3 bg-white border border-amber-200 rounded-lg hover:bg-amber-100 text-amber-700 transition-colors" title="Copiar API Key">
                            <Copy size={16} />
                          </button>
                        </div>
                      </div>
                      <div>
                        <label className="block text-xs font-bold text-amber-600 uppercase mb-1">API Secret (HMAC)</label>
                        <div className="flex gap-2">
                          <input type="text" readOnly value={client.testApiSecret || 'No generada'} className="flex-1 px-4 py-2 bg-white border border-amber-200 rounded-lg text-slate-600 font-mono text-sm outline-none" />
                          <button type="button" onClick={() => copyToClipboard(client.testApiSecret, 'API Secret (Test)')} className="px-3 bg-white border border-amber-200 rounded-lg hover:bg-amber-100 text-amber-700 transition-colors" title="Copiar API Secret">
                            <Copy size={16} />
                          </button>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Modal Crear Resolución */}
      {showResModal && (
        <div className="fixed inset-0 bg-slate-900/40 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-3xl p-8 max-w-2xl w-full shadow-2xl">
            <div className="flex justify-between items-center mb-6">
              <h3 className="text-xl font-bold text-slate-800 flex items-center gap-2">
                <FileSignature className="text-blue-600" /> Nueva Resolución
              </h3>
              <button onClick={() => setShowResModal(false)} className="text-slate-400 hover:text-slate-600 transition-colors">
                <X size={24} />
              </button>
            </div>
            
            <form onSubmit={handleCreateResolution} className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-semibold text-slate-700 mb-1">Tipo de Documento</label>
                  <select required className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none" value={newRes.documentType} onChange={e => setNewRes({...newRes, documentType: e.target.value})}>
                    <option value="FE">Factura Electrónica (FE)</option>
                    <option value="NC">Nota Crédito (NC)</option>
                    <option value="ND">Nota Débito (ND)</option>
                    <option value="POS">Documento Soporte / POS</option>
                  </select>
                </div>
                <div>
                  <label className="block text-sm font-semibold text-slate-700 mb-1">Número de Resolución</label>
                  <input required type="text" className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none" value={newRes.resolutionNumber} onChange={e => setNewRes({...newRes, resolutionNumber: e.target.value})} />
                </div>
              </div>

              <div className="grid grid-cols-3 gap-4">
                <div>
                  <label className="block text-sm font-semibold text-slate-700 mb-1">Prefijo (Opcional)</label>
                  <input type="text" className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none uppercase" value={newRes.prefix} onChange={e => setNewRes({...newRes, prefix: e.target.value})} />
                </div>
                <div>
                  <label className="block text-sm font-semibold text-slate-700 mb-1">Rango Inicial</label>
                  <input required type="number" min="1" className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none" value={newRes.numberStart || ''} onChange={e => setNewRes({...newRes, numberStart: parseInt(e.target.value) || 0})} />
                </div>
                <div>
                  <label className="block text-sm font-semibold text-slate-700 mb-1">Rango Final</label>
                  <input required type="number" min="1" className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none" value={newRes.numberEnd || ''} onChange={e => setNewRes({...newRes, numberEnd: parseInt(e.target.value) || 0})} />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-semibold text-slate-700 mb-1">Válida Desde</label>
                  <input required type="date" className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none" value={newRes.validFrom} onChange={e => setNewRes({...newRes, validFrom: e.target.value})} />
                </div>
                <div>
                  <label className="block text-sm font-semibold text-slate-700 mb-1">Válida Hasta</label>
                  <input required type="date" className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none" value={newRes.validTo} onChange={e => setNewRes({...newRes, validTo: e.target.value})} />
                </div>
              </div>

              <div>
                <label className="block text-sm font-semibold text-slate-700 mb-1">Clave Técnica (Solo FE)</label>
                <input type="text" className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none font-mono text-sm" value={newRes.technicalKey} onChange={e => setNewRes({...newRes, technicalKey: e.target.value})} />
              </div>

              <div className="pt-4 flex justify-end gap-3">
                <button type="button" onClick={() => setShowResModal(false)} className="px-5 py-2.5 text-slate-500 hover:bg-slate-100 rounded-xl font-medium transition-colors">Cancelar</button>
                <button type="submit" className="bg-blue-600 hover:bg-blue-700 text-white px-6 py-2.5 rounded-xl font-semibold shadow-md transition-colors">
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
