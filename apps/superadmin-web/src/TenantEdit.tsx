import React, { useEffect, useState, useRef } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { GoogleMap, useJsApiLoader, Autocomplete, Marker } from '@react-google-maps/api';
import { ArrowLeft, Save, MapPin, Building2, Phone, Hash, FileText, Coins, ChevronDown, Check, Users, UserPlus, X } from 'lucide-react';
import { toast } from 'sonner';
import { api } from './api';

const libraries: "places"[] = ['places'];
// TODO: El usuario deberá reemplazar esto por su API Key real en el .env
const GOOGLE_MAPS_API_KEY = import.meta.env.VITE_GOOGLE_MAPS_API_KEY || "AIzaSy_TU_LLAVE_DE_PRUEBA_AQUI"; 

interface TenantEditForm {
  name: string;
  commercialName: string;
  email: string;
  taxId: string;
  verificationDigit: string;
  address: string;
  city: string;
  phone: string;
  taxRegime: string;
  economicActivity: string;
  latitude: number | null;
  longitude: number | null;
}

interface DocType {
  code: string;
  name: string;
  governingEntity: string;
}

export const TenantEdit = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [formData, setFormData] = useState<TenantEditForm | null>(null);
  const [docTypes, setDocTypes] = useState<DocType[]>([]);
  const [pricings, setPricings] = useState<Record<string, number>>({});
  const [users, setUsers] = useState<any[]>([]);
  const [showUserModal, setShowUserModal] = useState(false);
  const [newUser, setNewUser] = useState({ name: '', email: '', password: '' });
  const [saving, setSaving] = useState(false);
  const autocompleteRef = useRef<google.maps.places.Autocomplete | null>(null);

  const { isLoaded } = useJsApiLoader({
    id: 'google-map-script',
    googleMapsApiKey: GOOGLE_MAPS_API_KEY,
    libraries
  });

  useEffect(() => {
    api.get(`/tenants/${id}`)
      .then(res => setFormData({
        name: res.data.name || '',
        commercialName: res.data.commercialName || '',
        email: res.data.email || '',
        taxId: res.data.taxId || '',
        verificationDigit: res.data.verificationDigit || '',
        address: res.data.address || '',
        city: res.data.city || '',
        phone: res.data.phone || '',
        taxRegime: res.data.taxRegime || '',
        economicActivity: res.data.economicActivity || '',
        latitude: res.data.latitude || 4.6097, // Default: Bogotá
        longitude: res.data.longitude || -74.0817,
      }))
      .catch(err => toast.error("Error cargando el Tenant"));

    api.get<DocType[]>('/billing/document-types')
      .then(res => setDocTypes(res.data))
      .catch(() => toast.error("Error cargando los tipos de documentos"));

    api.get(`/billing/tenant/${id}/pricing`)
      .then(res => {
        const p: Record<string, number> = {};
        res.data.forEach((item: any) => {
          p[item.documentTypeCode] = item.pricePerDocument;
        });
        setPricings(p);
      })
      .catch(() => toast.error("Error cargando el tarifario"));

    loadUsers();
  }, [id]);

  const loadUsers = () => {
    api.get(`/tenants/${id}/users`)
      .then(res => setUsers(res.data))
      .catch(() => toast.error("Error cargando los administradores"));
  };

  const handleCreateUser = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await api.post(`/tenants/${id}/users`, newUser);
      toast.success("Usuario administrador creado exitosamente");
      setShowUserModal(false);
      setNewUser({ name: '', email: '', password: '' });
      loadUsers();
    } catch (err: any) {
      toast.error(err.response?.data || "Error al crear usuario");
    }
  };

  const onLoadAutocomplete = (autocomplete: google.maps.places.Autocomplete) => {
    autocompleteRef.current = autocomplete;
  };

  const onPlaceChanged = () => {
    if (autocompleteRef.current !== null) {
      const place = autocompleteRef.current.getPlace();
      if (place.geometry && place.geometry.location) {
        const lat = place.geometry.location.lat();
        const lng = place.geometry.location.lng();
        
        let city = '';
        place.address_components?.forEach(component => {
          if (component.types.includes('locality')) {
            city = component.long_name;
          }
        });

        setFormData(prev => prev ? {
          ...prev,
          address: place.formatted_address || '',
          city: city || prev.city,
          latitude: lat,
          longitude: lng
        } : null);
      }
    }
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    try {
      // 1. Guardar Tenant
      await api.put(`/tenants/${id}`, formData);
      
      // 2. Guardar Tarifas
      const pricingPromises = Object.entries(pricings).map(([code, price]) => {
        return api.post(`/billing/tenant/${id}/pricing`, { documentTypeCode: code, price });
      });
      await Promise.all(pricingPromises);

      toast.success("Configuración y Tarifario guardados exitosamente.");
      navigate('/tenants');
    } catch (err) {
      toast.error("Error al guardar la configuración.");
    } finally {
      setSaving(false);
    }
  };

  const applyGlobalPrice = (entity: string, e: React.MouseEvent) => {
    e.preventDefault();
    const inputEl = document.getElementById(`global_price_${entity}`) as HTMLInputElement;
    if (!inputEl || inputEl.value === '') return;
    const price = parseFloat(inputEl.value) || 0;
    
    const docsInEntity = docTypes.filter(d => (d.governingEntity || 'DIAN') === entity);
    setPricings(prev => {
      const next = { ...prev };
      docsInEntity.forEach(d => {
        next[d.code] = price;
      });
      return next;
    });
    toast.success(`Precio global de $${price} aplicado a ${entity}.`);
  };

  const groupedDocTypes = docTypes.reduce((acc, doc) => {
    const entity = doc.governingEntity || 'DIAN';
    if (!acc[entity]) acc[entity] = [];
    acc[entity].push(doc);
    return acc;
  }, {} as Record<string, DocType[]>);

  if (!formData) return <div className="p-10 text-slate-500 animate-pulse">Cargando perfil del tenant...</div>;

  return (
    <div className="p-10 animate-in fade-in slide-in-from-bottom-4 duration-500 max-w-5xl mx-auto">
      <div className="flex items-center space-x-4 mb-8">
        <button onClick={() => navigate('/tenants')} className="p-2 bg-slate-800/50 rounded-xl border border-slate-700 hover:bg-slate-700 transition">
          <ArrowLeft className="w-5 h-5 text-slate-300" />
        </button>
        <div>
          <h1 className="text-3xl font-extrabold text-white tracking-tight">Edición Fiscal y Facturación</h1>
          <p className="text-slate-400 font-medium">{formData.commercialName}</p>
        </div>
      </div>

      <form onSubmit={handleSave} className="space-y-8">
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-8">
          
          {/* Columna 1: Información Fiscal */}
          <div className="glass-panel p-8 rounded-3xl border border-slate-700/50">
            <div className="flex items-center space-x-3 mb-6">
              <div className="bg-blue-500/20 p-2 rounded-lg border border-blue-500/30"><Building2 className="w-5 h-5 text-blue-400" /></div>
              <h2 className="text-xl font-bold text-white">Perfil Fiscal (DIAN)</h2>
            </div>
            
            <div className="space-y-5">
              <div className="flex space-x-4">
                <div className="flex-1">
                  <label className="block text-sm font-semibold text-slate-400 mb-1">NIT</label>
                  <div className="relative">
                    <Hash className="absolute left-3 top-3.5 w-4 h-4 text-slate-500" />
                    <input required type="text" className="w-full pl-10 pr-4 py-3 bg-slate-900/50 border border-slate-700 rounded-xl text-white focus:ring-2 focus:ring-blue-500" value={formData.taxId} onChange={e => setFormData({...formData, taxId: e.target.value})} />
                  </div>
                </div>
                <div className="w-24">
                  <label className="block text-sm font-semibold text-slate-400 mb-1">DV</label>
                  <input required type="text" className="w-full px-4 py-3 bg-slate-900/50 border border-slate-700 rounded-xl text-white focus:ring-2 focus:ring-blue-500 text-center" maxLength={1} value={formData.verificationDigit} onChange={e => setFormData({...formData, verificationDigit: e.target.value})} />
                </div>
              </div>

              <div>
                <label className="block text-sm font-semibold text-slate-400 mb-1">Razón Social Jurídica</label>
                <input required type="text" className="w-full px-4 py-3 bg-slate-900/50 border border-slate-700 rounded-xl text-white focus:ring-2 focus:ring-blue-500" value={formData.name} onChange={e => setFormData({...formData, name: e.target.value})} />
              </div>

              <div>
                <label className="block text-sm font-semibold text-slate-400 mb-1">Régimen Tributario</label>
                <select className="w-full px-4 py-3 bg-slate-900/50 border border-slate-700 rounded-xl text-white focus:ring-2 focus:ring-blue-500" value={formData.taxRegime} onChange={e => setFormData({...formData, taxRegime: e.target.value})}>
                  <option value="">Selecciona un régimen...</option>
                  <option value="Responsable de IVA">Responsable de IVA (Antiguo Común)</option>
                  <option value="No Responsable de IVA">No Responsable de IVA (Antiguo Simplificado)</option>
                  <option value="Régimen Simple">Régimen Simple de Tributación</option>
                </select>
              </div>

              <div>
                <label className="block text-sm font-semibold text-slate-400 mb-1">Actividad Económica (CIIU)</label>
                <div className="relative">
                  <FileText className="absolute left-3 top-3.5 w-4 h-4 text-slate-500" />
                  <input type="text" placeholder="Ej. 6201" className="w-full pl-10 pr-4 py-3 bg-slate-900/50 border border-slate-700 rounded-xl text-white focus:ring-2 focus:ring-blue-500" value={formData.economicActivity} onChange={e => setFormData({...formData, economicActivity: e.target.value})} />
                </div>
              </div>
            </div>
          </div>

          {/* Columna 2: Ubicación y Contacto */}
          <div className="glass-panel p-8 rounded-3xl border border-slate-700/50">
            <div className="flex items-center space-x-3 mb-6">
              <div className="bg-emerald-500/20 p-2 rounded-lg border border-emerald-500/30"><MapPin className="w-5 h-5 text-emerald-400" /></div>
              <h2 className="text-xl font-bold text-white">Ubicación (Google Maps)</h2>
            </div>
            
            <div className="space-y-5">
              <div>
                <label className="block text-sm font-semibold text-slate-400 mb-1">Buscar Dirección Oficial</label>
                {isLoaded ? (
                  <Autocomplete onLoad={onLoadAutocomplete} onPlaceChanged={onPlaceChanged}>
                    <input 
                      type="text" 
                      placeholder="Busca en Google Maps..."
                      className="w-full px-4 py-3 bg-slate-900/50 text-white border border-slate-700 rounded-xl focus:ring-2 focus:ring-emerald-500"
                      value={formData.address}
                      onChange={e => setFormData({...formData, address: e.target.value})}
                    />
                  </Autocomplete>
                ) : (
                  <input type="text" disabled placeholder="Cargando mapas..." className="w-full px-4 py-3 bg-slate-800 text-slate-500 border border-slate-700 rounded-xl" />
                )}
              </div>

              <div className="h-48 rounded-2xl overflow-hidden border border-slate-200 shadow-inner">
                {isLoaded && formData.latitude && formData.longitude ? (
                  <GoogleMap
                    mapContainerStyle={{ width: '100%', height: '100%' }}
                    center={{ lat: formData.latitude, lng: formData.longitude }}
                    zoom={15}
                    options={{ disableDefaultUI: true, zoomControl: true }}
                  >
                    <Marker position={{ lat: formData.latitude, lng: formData.longitude }} />
                  </GoogleMap>
                ) : (
                  <div className="w-full h-full bg-slate-100 flex items-center justify-center text-slate-400 font-medium">Mapa no disponible</div>
                )}
              </div>

              <div className="flex space-x-4">
                <div className="flex-1">
                  <label className="block text-sm font-semibold text-slate-400 mb-1">Ciudad</label>
                  <input type="text" className="w-full px-4 py-3 bg-slate-900/50 text-white border border-slate-700 rounded-xl focus:ring-2 focus:ring-emerald-500" value={formData.city} onChange={e => setFormData({...formData, city: e.target.value})} />
                </div>
                <div className="flex-1">
                  <label className="block text-sm font-semibold text-slate-400 mb-1">Teléfono</label>
                  <div className="relative">
                    <Phone className="absolute left-3 top-3.5 w-4 h-4 text-slate-500" />
                    <input type="tel" className="w-full pl-10 pr-4 py-3 bg-slate-900/50 text-white border border-slate-700 rounded-xl focus:ring-2 focus:ring-emerald-500" value={formData.phone} onChange={e => setFormData({...formData, phone: e.target.value})} />
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* Administradores del Tenant */}
        <div className="glass-panel p-8 rounded-3xl border border-slate-700/50 mt-8">
          <div className="flex items-center justify-between mb-6">
            <div className="flex items-center space-x-3">
              <div className="bg-amber-500/20 p-2 rounded-lg border border-amber-500/30"><Users className="w-5 h-5 text-amber-400" /></div>
              <div>
                <h2 className="text-xl font-bold text-white">Administradores del Tenant</h2>
                <p className="text-sm text-slate-400 font-medium">Credenciales de acceso para {formData.commercialName}</p>
              </div>
            </div>
            <button 
              type="button" 
              onClick={() => setShowUserModal(true)}
              className="flex items-center gap-2 bg-slate-800 hover:bg-slate-700 text-white px-4 py-2 rounded-xl transition-colors border border-slate-600"
            >
              <UserPlus className="w-4 h-4" />
              Nuevo Admin
            </button>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {users.length === 0 ? (
              <div className="col-span-full p-4 bg-slate-800/50 text-slate-400 rounded-xl text-center border border-slate-700/50 border-dashed">
                Aún no has creado administradores para este Tenant. No podrán iniciar sesión en su portal.
              </div>
            ) : (
              users.map(u => (
                <div key={u.id} className="bg-slate-900/60 p-4 rounded-xl border border-slate-700 flex flex-col">
                  <div className="flex items-center justify-between mb-2">
                    <span className="font-bold text-slate-200">{u.name}</span>
                    <span className={`px-2 py-1 text-xs font-bold rounded-lg ${u.isActive ? 'bg-emerald-500/20 text-emerald-400' : 'bg-red-500/20 text-red-400'}`}>
                      {u.isActive ? 'Activo' : 'Inactivo'}
                    </span>
                  </div>
                  <span className="text-sm text-slate-400">{u.email}</span>
                </div>
              ))
            )}
          </div>
        </div>

        {/* Columna Ancha: Tarifario Transaccional */}
        <div className="glass-panel p-8 rounded-3xl border border-slate-700/50 mt-8">
          <div className="flex items-center space-x-3 mb-6">
            <div className="bg-purple-500/20 p-2 rounded-lg border border-purple-500/30"><Coins className="w-5 h-5 text-purple-400" /></div>
            <div>
              <h2 className="text-xl font-bold text-white">Tarifario Transaccional (Pricing)</h2>
              <p className="text-sm text-slate-400 font-medium">Define el costo unitario de cada documento (en COP).</p>
            </div>
          </div>
          
          <div className="space-y-4">
            {Object.entries(groupedDocTypes).map(([entity, docs]) => (
              <details key={entity} className="group" open>
                <summary className="flex items-center justify-between p-4 bg-slate-800/60 rounded-xl cursor-pointer list-none border border-slate-700/50 hover:bg-slate-800 transition-colors relative z-10">
                  <div className="flex items-center space-x-3">
                    <ChevronDown className="w-5 h-5 text-slate-400 group-open:rotate-180 transition-transform" />
                    <span className="font-bold text-white text-lg tracking-wide">{entity}</span>
                    <span className="bg-slate-700 text-slate-300 text-xs px-2 py-0.5 rounded-full">{docs.length} docs</span>
                  </div>
                  
                  {/* Master Pricing Input */}
                  <div className="flex items-center space-x-2" onClick={e => e.preventDefault()}>
                    <span className="text-sm font-semibold text-slate-400">Tarifa Global:</span>
                    <div className="relative w-32">
                      <span className="absolute left-2.5 top-1.5 text-slate-400 font-bold text-sm">$</span>
                      <input 
                        type="number" 
                        id={`global_price_${entity}`}
                        min="0" step="0.01"
                        placeholder="0.00"
                        className="w-full pl-6 pr-8 py-1.5 bg-slate-900 border border-slate-600 rounded-lg text-white text-sm focus:ring-1 focus:ring-purple-500"
                        onClick={e => e.stopPropagation()}
                      />
                      <button 
                        type="button"
                        onClick={(e) => applyGlobalPrice(entity, e)}
                        className="absolute right-1 top-1 bottom-1 px-1.5 bg-purple-500 hover:bg-purple-600 text-white rounded flex items-center justify-center transition-colors"
                        title="Aplicar a todos"
                      >
                        <Check className="w-3.5 h-3.5" />
                      </button>
                    </div>
                  </div>
                </summary>
                <div className="bg-slate-900/40 border border-slate-800 rounded-b-xl -mt-2 pt-4 pb-2 px-2">
                  <div className="divide-y divide-slate-800/50">
                    {docs.map(doc => (
                      <div key={doc.code} className="flex items-center justify-between p-3 hover:bg-slate-800/30 transition-colors rounded-lg">
                        <div className="flex flex-col">
                          <span className="font-bold text-slate-300">{doc.name}</span>
                          <span className="text-xs text-slate-500 font-mono mt-0.5">{doc.code}</span>
                        </div>
                        <div className="relative w-36">
                          <span className="absolute left-3 top-2 text-slate-400 font-bold">$</span>
                          <input 
                            type="number" 
                            min="0"
                            step="0.01"
                            className="w-full pl-7 pr-3 py-2 bg-slate-800 border border-slate-700 rounded-xl text-white focus:ring-2 focus:ring-purple-500 font-semibold" 
                            value={pricings[doc.code] || 0} 
                            onChange={e => setPricings({...pricings, [doc.code]: parseFloat(e.target.value) || 0})}
                          />
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              </details>
            ))}
            {docTypes.length === 0 && (
              <div className="p-4 bg-yellow-500/10 text-yellow-400 rounded-xl border border-yellow-500/20 text-sm">
                No hay tipos de documentos registrados en el Core de FEL. Se debe popular la tabla DocumentTypes.
              </div>
            )}
          </div>
        </div>

        <div className="flex justify-end pt-4">
          <button 
            type="submit" 
            disabled={saving}
            className="flex items-center px-8 py-4 bg-slate-900 text-white font-bold rounded-2xl hover:bg-slate-800 transition-all shadow-xl shadow-slate-900/20 transform hover:-translate-y-0.5 disabled:opacity-50"
          >
            <Save className="w-5 h-5 mr-3" />
            {saving ? 'Guardando...' : 'Guardar Perfil Fiscal'}
          </button>
        </div>
      </form>

      {/* Modal Crear Usuario */}
      {showUserModal && (
        <div className="fixed inset-0 bg-slate-950/80 backdrop-blur-sm z-50 flex items-center justify-center animate-in fade-in duration-200">
          <div className="bg-slate-900 border border-slate-800 rounded-3xl p-8 max-w-md w-full mx-4 shadow-2xl">
            <div className="flex justify-between items-center mb-6">
              <h3 className="text-xl font-bold text-white flex items-center gap-2">
                <UserPlus className="w-5 h-5 text-amber-500" />
                Nuevo Administrador
              </h3>
              <button onClick={() => setShowUserModal(false)} className="text-slate-500 hover:text-white transition-colors">
                <X className="w-5 h-5" />
              </button>
            </div>
            
            <form onSubmit={handleCreateUser} className="space-y-4">
              <div>
                <label className="block text-sm font-semibold text-slate-400 mb-1">Nombre Completo</label>
                <input required type="text" className="w-full px-4 py-3 bg-slate-950 border border-slate-800 rounded-xl text-white focus:ring-2 focus:ring-amber-500" value={newUser.name} onChange={e => setNewUser({...newUser, name: e.target.value})} />
              </div>
              <div>
                <label className="block text-sm font-semibold text-slate-400 mb-1">Correo Electrónico (Login)</label>
                <input required type="email" className="w-full px-4 py-3 bg-slate-950 border border-slate-800 rounded-xl text-white focus:ring-2 focus:ring-amber-500" value={newUser.email} onChange={e => setNewUser({...newUser, email: e.target.value})} />
              </div>
              <div>
                <label className="block text-sm font-semibold text-slate-400 mb-1">Contraseña Inicial</label>
                <input required type="password" minLength={6} className="w-full px-4 py-3 bg-slate-950 border border-slate-800 rounded-xl text-white focus:ring-2 focus:ring-amber-500" value={newUser.password} onChange={e => setNewUser({...newUser, password: e.target.value})} />
                <p className="text-xs text-slate-500 mt-1">Comparte esta contraseña de forma segura con tu cliente.</p>
              </div>

              <button type="submit" className="w-full mt-6 bg-amber-500 hover:bg-amber-600 text-white font-bold py-3 rounded-xl transition-colors shadow-lg shadow-amber-900/20">
                Otorgar Acceso
              </button>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
