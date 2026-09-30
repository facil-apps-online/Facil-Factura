import React, { useEffect, useState, useRef } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { GoogleMap, useJsApiLoader, Autocomplete, Marker } from '@react-google-maps/api';
import { ArrowLeft, Save, MapPin, Building2, Phone, Hash, FileText, Coins, ChevronDown, Check, Users, UserPlus, X, Mail, Trash2, RotateCcw, Cable, ToggleLeft, ToggleRight } from 'lucide-react';
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
  parentTenantId: string | null;
  legalName: string;
  contactPerson: string;
  contactEmail: string;
  contactPhone: string;
  whatsAppPhone: string;
  einvoicingEmail: string;
  commercialEmail: string;
  website: string;
  physicalAddressLine1: string;
  physicalAddressLine2: string;
  physicalCity: string;
  physicalState: string;
  physicalPostalCode: string;
  billingAddress: string;
  defaultLanguageCode: string;
  defaultTimezone: string;
  defaultCurrencyId: string;
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
  const [allTenants, setAllTenants] = useState<any[]>([]);
  const [docTypes, setDocTypes] = useState<DocType[]>([]);
  const [pricings, setPricings] = useState<Record<string, number>>({});
  const [billingMode, setBillingMode] = useState<'PerDocument' | 'PerUser'>('PerDocument');
  const [pricePerUser, setPricePerUser] = useState(0);
  const [packages, setPackages] = useState<any[]>([]);
  const [newPackage, setNewPackage] = useState({ name: '', discountedPricePerUser: 0, totalPrice: 0 });
  const [integratorBilling, setIntegratorBilling] = useState<any[]>([]);
  const [savingIntegratorId, setSavingIntegratorId] = useState<string | null>(null);
  const [tenantIntegrators, setTenantIntegrators] = useState<any[]>([]);
  const [togglingIntegratorId, setTogglingIntegratorId] = useState<string | null>(null);
  const [users, setUsers] = useState<any[]>([]);
  const [showUserModal, setShowUserModal] = useState(false);
  const [newUser, setNewUser] = useState({ name: '', email: '' });
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
        parentTenantId: res.data.parentTenantId || null,
        legalName: res.data.legalName || '',
        contactPerson: res.data.contactPerson || '',
        contactEmail: res.data.contactEmail || '',
        contactPhone: res.data.contactPhone || '',
        whatsAppPhone: res.data.whatsAppPhone || '',
        einvoicingEmail: res.data.einvoicingEmail || '',
        commercialEmail: res.data.commercialEmail || '',
        website: res.data.website || '',
        physicalAddressLine1: res.data.physicalAddressLine1 || '',
        physicalAddressLine2: res.data.physicalAddressLine2 || '',
        physicalCity: res.data.physicalCity || '',
        physicalState: res.data.physicalState || '',
        physicalPostalCode: res.data.physicalPostalCode || '',
        billingAddress: res.data.billingAddress || '',
        defaultLanguageCode: res.data.defaultLanguageCode || 'es-CO',
        defaultTimezone: res.data.defaultTimezone || 'America/Bogota',
        defaultCurrencyId: res.data.defaultCurrencyId || '',
      }))
      .catch(err => toast.error("Error cargando el Tenant"));

    api.get('/tenants')
      .then(res => setAllTenants(res.data.filter((t: any) => t.id !== id)))
      .catch(() => toast.error("Error cargando la lista de tenants"));

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

    api.get(`/billing/tenant/${id}/user-pricing`)
      .then(res => {
        setBillingMode(res.data.billingMode);
        setPricePerUser(res.data.pricePerUser);
      })
      .catch(() => toast.error("Error cargando el modo de facturación"));

    loadPackages();
    loadUsers();
    loadIntegratorBilling();
    loadTenantIntegrators();
  }, [id]);

  const loadPackages = () => {
    api.get(`/billing/tenant/${id}/prepaid-packages`)
      .then(res => setPackages(res.data))
      .catch(() => toast.error("Error cargando los paquetes prepago"));
  };

  const loadIntegratorBilling = () => {
    api.get(`/billing/tenant/${id}/integrator-billing`)
      .then(res => setIntegratorBilling(res.data))
      .catch(() => toast.error("Error cargando la tarifa por integrador"));
  };

  const loadTenantIntegrators = () => {
    api.get(`/tenants/${id}/integrators`)
      .then(res => setTenantIntegrators(res.data))
      .catch(() => toast.error("Error cargando los integradores habilitados"));
  };

  const handleToggleTenantIntegrator = async (integrator: any) => {
    setTogglingIntegratorId(integrator.id);
    try {
      await api.put(`/tenants/${id}/integrators/${integrator.id}`, { enabled: !integrator.isEnabled });
      setTenantIntegrators(prev => prev.map(i => i.id === integrator.id ? { ...i, isEnabled: !i.isEnabled } : i));
      toast.success(!integrator.isEnabled ? `${integrator.name} habilitado para este tenant` : `${integrator.name} deshabilitado para este tenant`);
    } catch {
      toast.error('Error al actualizar el acceso al integrador');
    } finally {
      setTogglingIntegratorId(null);
    }
  };

  const updateIntegratorRow = (integratorId: string, patch: any) => {
    setIntegratorBilling(prev => prev.map(row => row.id === integratorId ? { ...row, ...patch } : row));
  };

  const handleSaveIntegratorBilling = async (row: any) => {
    setSavingIntegratorId(row.id);
    try {
      await api.put(`/billing/tenant/${id}/integrator-billing/${row.id}`, { mode: row.mode, pricePerUser: row.pricePerUser });
      toast.success(`Tarifa de ${row.name} actualizada.`);
      loadIntegratorBilling();
    } catch {
      toast.error('Error al guardar la tarifa.');
    } finally {
      setSavingIntegratorId(null);
    }
  };

  const handleClearIntegratorBilling = async (row: any) => {
    try {
      await api.delete(`/billing/tenant/${id}/integrator-billing/${row.id}`);
      toast.success(`${row.name} vuelve al modo por defecto del Tenant.`);
      loadIntegratorBilling();
    } catch {
      toast.error('Error al quitar el override.');
    }
  };

  const handleCreatePackage = async () => {
    if (!newPackage.name || newPackage.totalPrice <= 0) {
      toast.error("Completa nombre y precio del paquete.");
      return;
    }
    try {
      await api.post(`/billing/tenant/${id}/prepaid-packages`, newPackage);
      toast.success("Paquete creado.");
      setNewPackage({ name: '', discountedPricePerUser: 0, totalPrice: 0 });
      loadPackages();
    } catch {
      toast.error("Error creando el paquete.");
    }
  };

  const handleTogglePackage = async (pkg: any) => {
    try {
      await api.put(`/billing/prepaid-packages/${pkg.id}`, { ...pkg, isActive: !pkg.isActive });
      loadPackages();
    } catch {
      toast.error("Error actualizando el paquete.");
    }
  };

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
      setNewUser({ name: '', email: '' });
      loadUsers();
    } catch (err: any) {
      toast.error(err.response?.data || "Error al crear usuario");
    }
  };

  const handleResendUserInvitation = async (userId: string) => {
    try {
      await api.post(`/tenants/${id}/users/${userId}/resend-invitation`);
      toast.success("Invitación reenviada");
    } catch (err: any) {
      toast.error(err.response?.data || "Error al reenviar la invitación");
    }
  };

  const handleRevokeUser = async (userId: string) => {
    if (!window.confirm("¿Revocar el acceso de este administrador al portal?")) return;
    try {
      await api.delete(`/tenants/${id}/users/${userId}`);
      toast.success("Acceso revocado");
      loadUsers();
    } catch {
      toast.error("Error al revocar el acceso");
    }
  };

  const handleReactivateUser = async (userId: string) => {
    try {
      await api.post(`/tenants/${id}/users/${userId}/reactivate`);
      toast.success("Acceso reactivado");
      loadUsers();
    } catch {
      toast.error("Error al reactivar el acceso");
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

      // 3. Guardar Modo de Facturación
      await api.put(`/billing/tenant/${id}/user-pricing`, { billingMode, pricePerUser });

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
    <div className="p-10 animate-in fade-in slide-in-from-bottom-4 duration-500">
      <div className="flex items-center space-x-4 mb-8">
        <button onClick={() => navigate('/tenants')} className="p-2 bg-slate-800/50 rounded-xl border border-slate-700 hover:bg-slate-700 transition">
          <ArrowLeft className="w-5 h-5 text-slate-300" />
        </button>
        <div>
          <h1 className="text-3xl font-extrabold text-white tracking-tight">Datos fiscales y facturación</h1>
          <p className="text-slate-400 font-medium">{formData.commercialName}</p>
        </div>
      </div>

      <form onSubmit={handleSave} className="space-y-8">
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-8">
          
          {/* Columna 1: Información Fiscal */}
          <div className="glass-panel p-8 rounded-3xl border border-slate-700/50">
            <div className="flex items-center space-x-3 mb-6">
              <div className="bg-blue-500/20 p-2 rounded-lg border border-blue-500/30"><Building2 className="w-5 h-5 text-blue-400" /></div>
              <h2 className="text-xl font-bold text-white">Información fiscal</h2>
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
                <label className="block text-sm font-semibold text-slate-400 mb-1">Nombre comercial</label>
                <input required type="text" className="w-full px-4 py-3 bg-slate-900/50 border border-slate-700 rounded-xl text-white focus:ring-2 focus:ring-blue-500" value={formData.commercialName} onChange={e => setFormData({...formData, commercialName: e.target.value})} />
              </div>

              <div>
                <label className="block text-sm font-semibold text-slate-400 mb-1">Grupo empresarial</label>
                <p className="text-xs text-slate-500 mb-2">Si pertenece a un grupo, selecciona la cuenta principal para consolidar la facturación.</p>
                <TenantPicker
                  tenants={allTenants}
                  value={formData.parentTenantId}
                  onChange={(newId) => setFormData({ ...formData, parentTenantId: newId })}
                />
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
              <h2 className="text-xl font-bold text-white">Ubicación</h2>
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

        {/* Información Comercial y de Contacto — espejo de Core, capturada al crear el tenant
            pero hasta ahora sin forma de editarla después. */}
        <div className="glass-panel p-8 rounded-3xl border border-slate-700/50 mt-8">
          <div className="flex items-center space-x-3 mb-6">
            <div className="bg-purple-500/20 p-2 rounded-lg border border-purple-500/30"><Mail className="w-5 h-5 text-purple-400" /></div>
            <h2 className="text-xl font-bold text-white">Datos de contacto</h2>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
            <div>
              <label className="block text-sm font-semibold text-slate-400 mb-1">Razón Social Legal</label>
              <input type="text" className="w-full px-4 py-3 bg-slate-900/50 border border-slate-700 rounded-xl text-white focus:ring-2 focus:ring-purple-500" value={formData.legalName} onChange={e => setFormData({...formData, legalName: e.target.value})} />
            </div>
            <div>
              <label className="block text-sm font-semibold text-slate-400 mb-1">Persona de Contacto</label>
              <input type="text" className="w-full px-4 py-3 bg-slate-900/50 border border-slate-700 rounded-xl text-white focus:ring-2 focus:ring-purple-500" value={formData.contactPerson} onChange={e => setFormData({...formData, contactPerson: e.target.value})} />
            </div>
            <div>
              <label className="block text-sm font-semibold text-slate-400 mb-1">Correo de Contacto</label>
              <input type="email" className="w-full px-4 py-3 bg-slate-900/50 border border-slate-700 rounded-xl text-white focus:ring-2 focus:ring-purple-500" value={formData.contactEmail} onChange={e => setFormData({...formData, contactEmail: e.target.value})} />
            </div>
            <div>
              <label className="block text-sm font-semibold text-slate-400 mb-1">Teléfono de Contacto</label>
              <input type="tel" className="w-full px-4 py-3 bg-slate-900/50 border border-slate-700 rounded-xl text-white focus:ring-2 focus:ring-purple-500" value={formData.contactPhone} onChange={e => setFormData({...formData, contactPhone: e.target.value})} />
            </div>
            <div>
              <label className="block text-sm font-semibold text-slate-400 mb-1">WhatsApp</label>
              <input type="tel" className="w-full px-4 py-3 bg-slate-900/50 border border-slate-700 rounded-xl text-white focus:ring-2 focus:ring-purple-500" value={formData.whatsAppPhone} onChange={e => setFormData({...formData, whatsAppPhone: e.target.value})} />
            </div>
            <div>
              <label className="block text-sm font-semibold text-slate-400 mb-1">Correo de Facturación Electrónica</label>
              <input type="email" className="w-full px-4 py-3 bg-slate-900/50 border border-slate-700 rounded-xl text-white focus:ring-2 focus:ring-purple-500" value={formData.einvoicingEmail} onChange={e => setFormData({...formData, einvoicingEmail: e.target.value})} />
            </div>
            <div>
              <label className="block text-sm font-semibold text-slate-400 mb-1">Correo Comercial</label>
              <input type="email" className="w-full px-4 py-3 bg-slate-900/50 border border-slate-700 rounded-xl text-white focus:ring-2 focus:ring-purple-500" value={formData.commercialEmail} onChange={e => setFormData({...formData, commercialEmail: e.target.value})} />
            </div>
            <div>
              <label className="block text-sm font-semibold text-slate-400 mb-1">Sitio Web</label>
              <input type="text" placeholder="https://..." className="w-full px-4 py-3 bg-slate-900/50 border border-slate-700 rounded-xl text-white focus:ring-2 focus:ring-purple-500" value={formData.website} onChange={e => setFormData({...formData, website: e.target.value})} />
            </div>
          </div>

          <h3 className="text-sm font-bold text-slate-400 uppercase tracking-wide mt-8 mb-4">Dirección Física</h3>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
            <div>
              <label className="block text-sm font-semibold text-slate-400 mb-1">Dirección (línea 1)</label>
              <input type="text" className="w-full px-4 py-3 bg-slate-900/50 border border-slate-700 rounded-xl text-white focus:ring-2 focus:ring-purple-500" value={formData.physicalAddressLine1} onChange={e => setFormData({...formData, physicalAddressLine1: e.target.value})} />
            </div>
            <div>
              <label className="block text-sm font-semibold text-slate-400 mb-1">Dirección (línea 2)</label>
              <input type="text" className="w-full px-4 py-3 bg-slate-900/50 border border-slate-700 rounded-xl text-white focus:ring-2 focus:ring-purple-500" value={formData.physicalAddressLine2} onChange={e => setFormData({...formData, physicalAddressLine2: e.target.value})} />
            </div>
            <div>
              <label className="block text-sm font-semibold text-slate-400 mb-1">Ciudad</label>
              <input type="text" className="w-full px-4 py-3 bg-slate-900/50 border border-slate-700 rounded-xl text-white focus:ring-2 focus:ring-purple-500" value={formData.physicalCity} onChange={e => setFormData({...formData, physicalCity: e.target.value})} />
            </div>
            <div>
              <label className="block text-sm font-semibold text-slate-400 mb-1">Departamento</label>
              <input type="text" className="w-full px-4 py-3 bg-slate-900/50 border border-slate-700 rounded-xl text-white focus:ring-2 focus:ring-purple-500" value={formData.physicalState} onChange={e => setFormData({...formData, physicalState: e.target.value})} />
            </div>
            <div>
              <label className="block text-sm font-semibold text-slate-400 mb-1">Código Postal</label>
              <input type="text" className="w-full px-4 py-3 bg-slate-900/50 border border-slate-700 rounded-xl text-white focus:ring-2 focus:ring-purple-500" value={formData.physicalPostalCode} onChange={e => setFormData({...formData, physicalPostalCode: e.target.value})} />
            </div>
            <div>
              <label className="block text-sm font-semibold text-slate-400 mb-1">Dirección de Facturación</label>
              <input type="text" className="w-full px-4 py-3 bg-slate-900/50 border border-slate-700 rounded-xl text-white focus:ring-2 focus:ring-purple-500" value={formData.billingAddress} onChange={e => setFormData({...formData, billingAddress: e.target.value})} />
            </div>
          </div>

          <h3 className="text-sm font-bold text-slate-400 uppercase tracking-wide mt-8 mb-4">Preferencias regionales</h3>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-5">
            <div>
              <label className="block text-sm font-semibold text-slate-400 mb-1">Idioma por Defecto</label>
              <input type="text" placeholder="es-CO" className="w-full px-4 py-3 bg-slate-900/50 border border-slate-700 rounded-xl text-white focus:ring-2 focus:ring-purple-500" value={formData.defaultLanguageCode} onChange={e => setFormData({...formData, defaultLanguageCode: e.target.value})} />
            </div>
            <div>
              <label className="block text-sm font-semibold text-slate-400 mb-1">Zona Horaria por Defecto</label>
              <input type="text" placeholder="America/Bogota" className="w-full px-4 py-3 bg-slate-900/50 border border-slate-700 rounded-xl text-white focus:ring-2 focus:ring-purple-500" value={formData.defaultTimezone} onChange={e => setFormData({...formData, defaultTimezone: e.target.value})} />
            </div>
            <div>
              <label className="block text-sm font-semibold text-slate-400 mb-1">Moneda</label>
              <input type="text" className="w-full px-4 py-3 bg-slate-900/50 border border-slate-700 rounded-xl text-white focus:ring-2 focus:ring-purple-500" value={formData.defaultCurrencyId} onChange={e => setFormData({...formData, defaultCurrencyId: e.target.value})} />
            </div>
          </div>
        </div>

        {/* Administradores de la cuenta */}
        <div className="glass-panel p-8 rounded-3xl border border-slate-700/50 mt-8">
          <div className="flex items-center justify-between mb-6">
            <div className="flex items-center space-x-3">
              <div className="bg-amber-500/20 p-2 rounded-lg border border-amber-500/30"><Users className="w-5 h-5 text-amber-400" /></div>
              <div>
                <h2 className="text-xl font-bold text-white">Administradores de la cuenta</h2>
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
                      {u.isActive ? 'Activo' : 'Revocado'}
                    </span>
                  </div>
                  <span className="text-sm text-slate-400 mb-3">{u.email}</span>
                  <div className="flex items-center gap-2 mt-auto pt-2 border-t border-slate-800">
                    {u.isActive ? (
                      <>
                        <button type="button" onClick={() => handleResendUserInvitation(u.id)} className="p-1.5 text-slate-500 hover:text-blue-400 transition-colors bg-slate-800 rounded-lg border border-slate-700" title="Reenviar invitación">
                          <Mail className="w-4 h-4" />
                        </button>
                        <button type="button" onClick={() => handleRevokeUser(u.id)} className="p-1.5 text-slate-500 hover:text-red-400 transition-colors bg-slate-800 rounded-lg border border-slate-700" title="Revocar acceso">
                          <Trash2 className="w-4 h-4" />
                        </button>
                      </>
                    ) : (
                      <button type="button" onClick={() => handleReactivateUser(u.id)} className="flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-semibold text-emerald-400 hover:text-emerald-300 transition-colors bg-slate-800 rounded-lg border border-slate-700" title="Reactivar acceso">
                        <RotateCcw className="w-3.5 h-3.5" />
                        Reactivar
                      </button>
                    )}
                  </div>
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
              <h2 className="text-xl font-bold text-white">Tarifas por documento</h2>
              <p className="text-sm text-slate-400 font-medium">Define el valor por documento en COP.</p>
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

        {/* Modo de Facturación (marca blanca / por usuario) */}
        <div className="glass-panel p-8 rounded-3xl border border-slate-700/50 mt-8">
          <div className="flex items-center space-x-3 mb-6">
            <div className="bg-emerald-500/20 p-2 rounded-lg border border-emerald-500/30"><Users className="w-5 h-5 text-emerald-400" /></div>
            <div>
              <h2 className="text-xl font-bold text-white">Modo de cobro</h2>
              <p className="text-sm text-slate-400 font-medium">Elige si el cobro se calcula por documento o por usuario.</p>
            </div>
          </div>

          <div className="flex gap-3 mb-6">
            {(['PerDocument', 'PerUser'] as const).map(mode => (
              <button
                key={mode}
                type="button"
                onClick={() => setBillingMode(mode)}
                className={`px-5 py-2.5 rounded-xl font-bold text-sm transition-colors ${
                  billingMode === mode ? 'bg-emerald-500 text-white shadow-md' : 'bg-slate-800 text-slate-400 hover:bg-slate-700'
                }`}
              >
                {mode === 'PerDocument' ? 'Por Documento' : 'Por Usuario'}
              </button>
            ))}
          </div>

          {billingMode === 'PerUser' && (
            <div className="space-y-6">
              <div className="max-w-xs">
                <label className="block text-sm font-semibold text-slate-400 mb-1">Tarifa mensual por usuario (COP)</label>
                <div className="relative">
                  <span className="absolute left-3 top-3 text-slate-400 font-bold">$</span>
                  <input
                    type="number" min="0" step="0.01"
                    value={pricePerUser}
                    onChange={e => setPricePerUser(parseFloat(e.target.value) || 0)}
                    className="w-full pl-7 pr-3 py-2.5 bg-slate-800 border border-slate-700 rounded-xl text-white focus:ring-2 focus:ring-emerald-500 font-semibold"
                  />
                </div>
                <p className="text-xs text-slate-500 mt-1">Cada emisor activo cuenta como un usuario.</p>
              </div>

              <div>
                <h3 className="text-sm font-bold text-white mb-3">Paquetes Prepago</h3>
                <div className="space-y-2 mb-4">
                  {packages.map(pkg => (
                    <div key={pkg.id} className="flex items-center justify-between p-3 bg-slate-800/60 rounded-xl border border-slate-700/50">
                      <div>
                        <span className="font-bold text-slate-200">{pkg.name}</span>
                        <span className="text-xs text-slate-500 ml-2">${pkg.discountedPricePerUser.toLocaleString('es-CO')}/usuario con descuento · ${pkg.totalPrice.toLocaleString('es-CO')} bolsa</span>
                      </div>
                      <button
                        type="button"
                        onClick={() => handleTogglePackage(pkg)}
                        className={`text-xs font-bold px-3 py-1 rounded-full ${pkg.isActive ? 'bg-emerald-500/20 text-emerald-400' : 'bg-slate-700 text-slate-400'}`}
                      >
                        {pkg.isActive ? 'Activo' : 'Inactivo'}
                      </button>
                    </div>
                  ))}
                  {packages.length === 0 && <p className="text-xs text-slate-500">Sin paquetes registrados.</p>}
                </div>

                <div className="flex gap-2 items-end">
                  <div className="flex-1">
                    <label className="block text-xs font-semibold text-slate-500 mb-1">Nombre</label>
                    <input type="text" placeholder="Ej. Bolsa 50 usuarios" value={newPackage.name} onChange={e => setNewPackage({ ...newPackage, name: e.target.value })} className="w-full px-3 py-2 bg-slate-800 border border-slate-700 rounded-xl text-white text-sm" />
                  </div>
                  <div className="w-36">
                    <label className="block text-xs font-semibold text-slate-500 mb-1">Tarifa c/descuento</label>
                    <input type="number" min="0" step="0.01" value={newPackage.discountedPricePerUser} onChange={e => setNewPackage({ ...newPackage, discountedPricePerUser: parseFloat(e.target.value) || 0 })} className="w-full px-3 py-2 bg-slate-800 border border-slate-700 rounded-xl text-white text-sm" />
                  </div>
                  <div className="w-40">
                    <label className="block text-xs font-semibold text-slate-500 mb-1">Precio Bolsa</label>
                    <input type="number" min="0" step="0.01" value={newPackage.totalPrice} onChange={e => setNewPackage({ ...newPackage, totalPrice: parseFloat(e.target.value) || 0 })} className="w-full px-3 py-2 bg-slate-800 border border-slate-700 rounded-xl text-white text-sm" />
                  </div>
                  <button type="button" onClick={handleCreatePackage} className="px-4 py-2 bg-emerald-500 hover:bg-emerald-600 text-white rounded-xl font-bold text-sm">
                    Agregar
                  </button>
                </div>
              </div>
            </div>
          )}
        </div>

        {/* Integradores Habilitados (acceso) */}
        <div className="glass-panel p-8 rounded-3xl border border-slate-700/50 mt-8">
          <div className="flex items-center space-x-3 mb-6">
            <div className="bg-indigo-500/20 p-2 rounded-lg border border-indigo-500/30"><Cable className="w-5 h-5 text-indigo-400" /></div>
            <div>
              <h2 className="text-xl font-bold text-white">Integradores disponibles</h2>
              <p className="text-sm text-slate-400 font-medium">
                Qué proveedores de documentos electrónicos puede ver y usar este tenant. Uno deshabilitado no aparece en su portal — el tenant ni se entera de que existe.
              </p>
            </div>
          </div>

          <div className="space-y-2">
            {tenantIntegrators.map(i => (
              <div key={i.id} className="flex items-center justify-between p-4 bg-slate-800/40 rounded-2xl border border-slate-700/50">
                <div>
                  <span className="font-bold text-white">{i.name}</span>
                  <span className="ml-2 text-xs font-mono text-slate-500">{i.code}</span>
                </div>
                <button
                  type="button"
                  onClick={() => handleToggleTenantIntegrator(i)}
                  disabled={togglingIntegratorId === i.id}
                  className="flex items-center gap-1.5 text-xs font-bold disabled:opacity-50"
                >
                  {i.isEnabled ? (
                    <span className="inline-flex items-center gap-1 px-3 py-1.5 rounded-full bg-emerald-500/20 text-emerald-400 border border-emerald-500/30"><ToggleRight className="w-4 h-4" /> Habilitado</span>
                  ) : (
                    <span className="inline-flex items-center gap-1 px-3 py-1.5 rounded-full bg-slate-700 text-slate-400"><ToggleLeft className="w-4 h-4" /> Oculto</span>
                  )}
                </button>
              </div>
            ))}
            {tenantIntegrators.length === 0 && <p className="text-sm text-slate-500">Sin integradores activos en el catálogo.</p>}
          </div>
        </div>

        {/* Tarifa por Integrador (override) */}
        <div className="glass-panel p-8 rounded-3xl border border-slate-700/50 mt-8">
          <div className="flex items-center space-x-3 mb-6">
            <div className="bg-emerald-500/20 p-2 rounded-lg border border-emerald-500/30"><Coins className="w-5 h-5 text-emerald-400" /></div>
            <div>
              <h2 className="text-xl font-bold text-white">Tarifa del integrador</h2>
              <p className="text-sm text-slate-400 font-medium">
                Por defecto todos los integradores usan el modo de arriba. Actívalo aquí solo para el integrador que necesite un modo distinto (ej. Dataico por usuario mientras DIAN directa sigue por documento).
              </p>
            </div>
          </div>

          <div className="space-y-4">
            {integratorBilling.map(row => (
              <div key={row.id} className="p-4 bg-slate-800/40 rounded-2xl border border-slate-700/50">
                <div className="flex items-center justify-between mb-3">
                  <div className="flex items-center gap-2">
                    <span className="font-bold text-white">{row.name}</span>
                    {row.hasOverride ? (
                      <span className="text-xs font-bold px-2 py-0.5 rounded-full bg-emerald-500/20 text-emerald-400">Override activo</span>
                    ) : (
                      <span className="text-xs font-bold px-2 py-0.5 rounded-full bg-slate-700 text-slate-400">Usando tarifa de la cuenta</span>
                    )}
                  </div>
                  {row.hasOverride && (
                    <button type="button" onClick={() => handleClearIntegratorBilling(row)} className="text-xs font-bold text-slate-400 hover:text-red-400 transition-colors">
                      Quitar override
                    </button>
                  )}
                </div>

                <div className="flex flex-wrap items-end gap-3">
                  <div className="flex gap-2">
                    {(['PerDocument', 'PerUser'] as const).map(mode => (
                      <button
                        key={mode}
                        type="button"
                        onClick={() => updateIntegratorRow(row.id, { mode })}
                        className={`px-4 py-2 rounded-xl font-bold text-xs transition-colors ${
                          row.mode === mode ? 'bg-emerald-500 text-white shadow-md' : 'bg-slate-800 text-slate-400 hover:bg-slate-700'
                        }`}
                      >
                        {mode === 'PerDocument' ? 'Por Documento' : 'Por Usuario'}
                      </button>
                    ))}
                  </div>

                  {row.mode === 'PerUser' && (
                    <div className="w-44">
                      <label className="block text-xs font-semibold text-slate-500 mb-1">Tarifa mensual por usuario</label>
                      <div className="relative">
                        <span className="absolute left-3 top-2.5 text-slate-400 font-bold text-sm">$</span>
                        <input
                          type="number" min="0" step="0.01"
                          value={row.pricePerUser}
                          onChange={e => updateIntegratorRow(row.id, { pricePerUser: parseFloat(e.target.value) || 0 })}
                          className="w-full pl-7 pr-3 py-2 bg-slate-800 border border-slate-700 rounded-xl text-white text-sm focus:ring-2 focus:ring-emerald-500 font-semibold"
                        />
                      </div>
                    </div>
                  )}

                  <button
                    type="button"
                    onClick={() => handleSaveIntegratorBilling(row)}
                    disabled={savingIntegratorId === row.id}
                    className="px-4 py-2 bg-emerald-500 hover:bg-emerald-600 text-white rounded-xl font-bold text-xs transition-colors disabled:opacity-50"
                  >
                    {savingIntegratorId === row.id ? 'Guardando...' : 'Guardar override'}
                  </button>
                </div>

                {row.mode === 'PerDocument' && (
                  <p className="text-xs text-slate-500 mt-2">
                    La tarifa por documento sale del Tarifario por Volumen — crea ahí un tier específico para este integrador si necesita un precio distinto al global.
                  </p>
                )}
              </div>
            ))}
            {integratorBilling.length === 0 && <p className="text-sm text-slate-500">Sin integradores activos en el catálogo.</p>}
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
                <p className="text-xs text-slate-500 mt-1">Enviaremos una invitación para crear la contraseña.</p>
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

// Combobox buscable (no un <select> nativo) para elegir el tenant padre — la lista de tenants
// puede crecer bastante y un <select> plano se vuelve incómodo de recorrer.
function TenantPicker({ tenants, value, onChange }: { tenants: any[]; value: string | null; onChange: (id: string | null) => void }) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const selected = tenants.find(t => t.id === value);
  const filtered = tenants.filter(t =>
    (t.commercialName || t.name || '').toLowerCase().includes(query.toLowerCase())
  );

  return (
    <div className="relative" ref={containerRef}>
      <button
        type="button"
        onClick={() => setOpen(o => !o)}
        className="w-full flex items-center justify-between px-4 py-3 bg-slate-900/50 border border-slate-700 rounded-xl text-white text-left focus:ring-2 focus:ring-blue-500"
      >
        <span className={selected ? 'text-white' : 'text-slate-500'}>
          {selected ? (selected.commercialName || selected.name) : 'Sin grupo empresarial (independiente)'}
        </span>
        <ChevronDown className="w-4 h-4 text-slate-500" />
      </button>

      {open && (
        <div className="absolute z-20 mt-1 w-full bg-slate-900 border border-slate-700 rounded-xl shadow-xl overflow-hidden">
          <input
            autoFocus
            type="text"
            placeholder="Buscar tenant..."
            className="w-full px-4 py-2 bg-slate-950 border-b border-slate-700 text-white text-sm focus:outline-none"
            value={query}
            onChange={e => setQuery(e.target.value)}
          />
          <div className="max-h-56 overflow-y-auto">
            <button
              type="button"
              onClick={() => { onChange(null); setOpen(false); setQuery(''); }}
              className="w-full text-left px-4 py-2 text-sm text-slate-400 hover:bg-slate-800 flex items-center justify-between"
            >
              Sin grupo empresarial (independiente)
              {!value && <Check className="w-4 h-4 text-blue-400" />}
            </button>
            {filtered.map(t => (
              <button
                key={t.id}
                type="button"
                onClick={() => { onChange(t.id); setOpen(false); setQuery(''); }}
                className="w-full text-left px-4 py-2 text-sm text-white hover:bg-slate-800 flex items-center justify-between"
              >
                {t.commercialName || t.name}
                {value === t.id && <Check className="w-4 h-4 text-blue-400" />}
              </button>
            ))}
            {filtered.length === 0 && <p className="px-4 py-3 text-sm text-slate-500">Sin resultados.</p>}
          </div>
        </div>
      )}
    </div>
  );
}
