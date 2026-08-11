import React, { useEffect, useState, useRef } from 'react';
import { BrowserRouter as Router, Routes, Route, Link, useNavigate, Navigate } from 'react-router-dom';
import { LayoutDashboard, Users, Receipt, Settings, Plus, LogOut, ShieldCheck, Mail, Lock, Loader2, MapPin, Building2, Hash, Phone, Globe, FileText, X } from 'lucide-react';
import { Toaster, toast } from 'sonner';
import axios from 'axios';
import { api } from './api';
import { useJsApiLoader, Autocomplete, GoogleMap, Marker } from '@react-google-maps/api';

import { TenantEdit } from './TenantEdit';
import { DocumentTypes } from './DocumentTypes';
import { DocumentTemplates } from './DocumentTemplates';
import { Billing } from './Billing';

const libraries: "places"[] = ['places'];
const GOOGLE_MAPS_API_KEY = import.meta.env.VITE_GOOGLE_MAPS_API_KEY || "AIzaSy_TU_LLAVE_DE_PRUEBA_AQUI";

// --- Types ---
interface DashboardMetrics {
  activeTenants: number;
  documentsThisMonth: number;
  estimatedBilling: number;
}

interface Tenant {
  id: string;
  name: string;
  commercialName: string;
  slug: string;
  isActive: boolean;
  createdAt: string;
}

// --- Auth Components ---
const AuthScreen = ({ onAuthSuccess }: { onAuthSuccess: () => void }) => {
  const [mode, setMode] = useState<'loading' | 'setup' | 'login'>('loading');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const navigate = useNavigate();

  useEffect(() => {
    api.get('/auth/status')
      .then(res => {
        if (res.data.setupRequired) {
          setMode('setup');
        } else {
          setMode('login');
        }
      })
      .catch(() => setError("Error de conexión con el motor FEL."));
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);
    setError('');

    try {
      if (mode === 'setup') {
        await api.post('/auth/setup', { email, password });
        // After setup, automatically log them in or show success
        const loginRes = await api.post('/auth/login', { email, password });
        localStorage.setItem('fel_superadmin_auth', loginRes.data.token);
        onAuthSuccess();
        navigate('/');
      } else {
        const loginRes = await api.post('/auth/login', { email, password });
        localStorage.setItem('fel_superadmin_auth', loginRes.data.token);
        onAuthSuccess();
        navigate('/');
      }
    } catch (err: any) {
      setError(err.response?.data || "Credenciales incorrectas o error en el servidor.");
    } finally {
      setIsSubmitting(false);
    }
  };

  if (mode === 'loading') {
    return (
      <div className="min-h-screen flex flex-col items-center justify-center bg-[#0B1120]">
        <Loader2 className="w-12 h-12 text-blue-500 animate-spin mb-4" />
        <p className="text-slate-400 font-medium animate-pulse">Conectando al Motor Central...</p>
      </div>
    );
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-[#0B1120] via-slate-900 to-[#0B1120] p-4 relative overflow-hidden">
      {/* Decorative Blur Orbs */}
      <div className="absolute top-[-10%] left-[-10%] w-96 h-96 bg-blue-600/20 rounded-full blur-[100px] pointer-events-none"></div>
      <div className="absolute bottom-[-10%] right-[-10%] w-96 h-96 bg-indigo-600/20 rounded-full blur-[100px] pointer-events-none"></div>

      <div className="bg-white/5 backdrop-blur-2xl border border-white/10 p-10 rounded-[2rem] shadow-2xl w-full max-w-[420px] relative z-10 animate-in fade-in zoom-in-95 duration-500">
        <div className="flex justify-center mb-6">
          <img src="/brand/isotipo-color.png" alt="Facil Factura" className="w-20 h-20 object-contain drop-shadow-lg" />
        </div>
        
        <h1 className="text-3xl font-extrabold text-white text-center tracking-tight mb-2">
          {mode === 'setup' ? 'Inicialización Facil Factura' : 'Acceso Superadmin'}
        </h1>
        <p className="text-slate-400 text-center text-sm mb-8 px-2">
          {mode === 'setup' 
            ? 'Detectamos que el motor es nuevo. Crea la credencial maestra de Superadministrador.' 
            : 'Acceso seguro y exclusivo al Hub Central de Operaciones.'}
        </p>
        
        <form onSubmit={handleSubmit} className="space-y-5">
          <div className="relative">
            <div className="absolute inset-y-0 left-0 pl-4 flex items-center pointer-events-none">
              <Mail className="h-5 w-5 text-slate-500" />
            </div>
            <input 
              type="email" 
              required
              placeholder="Correo electrónico maestro"
              className="w-full bg-black/20 border border-slate-700 text-white pl-12 pr-4 py-3.5 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all placeholder:text-slate-500"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
            />
          </div>
          <div className="relative">
            <div className="absolute inset-y-0 left-0 pl-4 flex items-center pointer-events-none">
              <Lock className="h-5 w-5 text-slate-500" />
            </div>
            <input 
              type="password" 
              required
              placeholder="Contraseña de alta seguridad"
              className="w-full bg-black/20 border border-slate-700 text-white pl-12 pr-4 py-3.5 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all placeholder:text-slate-500"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
          </div>

          {error && (
            <div className="bg-red-500/10 border border-red-500/20 text-red-400 text-sm text-center py-3 rounded-xl animate-in shake duration-300">
              {error}
            </div>
          )}

          <button 
            type="submit"
            disabled={isSubmitting}
            className="w-full bg-gradient-to-r from-blue-600 to-indigo-600 hover:from-blue-500 hover:to-indigo-500 text-white font-bold py-3.5 rounded-xl transition-all shadow-lg shadow-blue-500/25 transform hover:-translate-y-0.5 disabled:opacity-50 disabled:transform-none flex justify-center items-center">
            {isSubmitting ? <Loader2 className="w-5 h-5 animate-spin" /> : (mode === 'setup' ? 'Forjar Credenciales' : 'Desbloquear Bóveda')}
          </button>
        </form>
      </div>
    </div>
  );
};

// --- Dashboard Component ---
const Dashboard = () => {
  const [metrics, setMetrics] = useState<DashboardMetrics>({
    activeTenants: 0,
    documentsThisMonth: 0,
    estimatedBilling: 0
  });

  useEffect(() => {
    api.get<DashboardMetrics>('/dashboard/metrics')
      .then(res => setMetrics(res.data))
      .catch(err => console.error("Error loading metrics", err));
  }, []);

  return (
    <div className="p-10 animate-in fade-in slide-in-from-bottom-4 duration-500">
      <h1 className="text-3xl font-extrabold text-white mb-8 tracking-tight">Panel de Control</h1>
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        <div className="glass-panel p-6 rounded-2xl hover:shadow-[0_0_20px_rgba(37,99,235,0.2)] transition-all">
          <div className="flex items-center justify-between">
            <p className="text-slate-400 text-sm font-semibold uppercase tracking-wider">Tenants Activos</p>
            <div className="bg-blue-500/10 p-2 rounded-lg border border-blue-500/20"><Users className="w-5 h-5 text-blue-400" /></div>
          </div>
          <p className="text-4xl font-extrabold text-white mt-4">{metrics.activeTenants}</p>
        </div>
        <div className="glass-panel p-6 rounded-2xl hover:shadow-[0_0_20px_rgba(16,185,129,0.2)] transition-all">
          <div className="flex items-center justify-between">
            <p className="text-slate-400 text-sm font-semibold uppercase tracking-wider">Docs Procesados</p>
            <div className="bg-emerald-500/10 p-2 rounded-lg border border-emerald-500/20"><Receipt className="w-5 h-5 text-emerald-400" /></div>
          </div>
          <p className="text-4xl font-extrabold text-white mt-4">{metrics.documentsThisMonth}</p>
        </div>
        <div className="glass-panel p-6 rounded-2xl hover:shadow-[0_0_20px_rgba(99,102,241,0.2)] transition-all">
          <div className="flex items-center justify-between">
            <p className="text-slate-400 text-sm font-semibold uppercase tracking-wider">Proyección Mes</p>
            <div className="bg-indigo-500/10 p-2 rounded-lg border border-indigo-500/20"><span className="text-indigo-400 font-bold">$</span></div>
          </div>
          <p className="text-4xl font-extrabold text-indigo-400 mt-4">
            ${metrics.estimatedBilling.toLocaleString('es-CO')}
          </p>
        </div>
      </div>
    </div>
  );
};

// --- Tenants Component ---
const emptyTenantForm = {
  name: '',
  commercialName: '',
  legalName: '',
  email: '',
  slug: '',
  taxId: '',
  verificationDigit: '',
  contactPerson: '',
  contactEmail: '',
  contactPhone: '',
  whatsappPhone: '',
  einvoicingEmail: '',
  commercialEmail: '',
  website: '',
  countryId: '4a2b129d-85cd-4069-97e2-2aafd96d5b05',
  physicalAddressLine1: '',
  physicalAddressLine2: '',
  physicalCity: '',
  physicalState: '',
  physicalPostalCode: '',
  billingAddress: '',
  latitude: null as number | null,
  longitude: null as number | null,
  defaultLanguageCode: 'es-CO',
  defaultTimezone: 'America/Bogota',
  defaultCurrencyId: '284d016a-80ba-4667-a3d0-a23989eb2733',
  adminName: '',
  adminEmail: '',
  adminPassword: '',
};

interface RegCountry {
  id: string;
  name: string;
  isoCode: string;
  defaultCurrencyId: string | null;
  defaultLanguageIsoCode: string | null;
  defaultLocalizationId: string | null;
  timezones: string[];
  defaultLatitude: number | null;
  defaultLongitude: number | null;
}
interface RegLanguage { id: string; name: string; isoCode: string; }
interface RegCurrency { id: string; name: string; code: string; symbol?: string; }
interface RegistrationData {
  countries: RegCountry[];
  languages: RegLanguage[];
  currencies: RegCurrency[];
}

const TenantsList = () => {
  const [tenants, setTenants] = useState<Tenant[]>([]);
  const [showModal, setShowModal] = useState(false);
  const [formData, setFormData] = useState({ ...emptyTenantForm });
  const [saving, setSaving] = useState(false);
  const [regData, setRegData] = useState<RegistrationData | null>(null);
  const navigate = useNavigate();
  const autocompleteRef = useRef<google.maps.places.Autocomplete | null>(null);

  const { isLoaded } = useJsApiLoader({
    id: 'google-map-script',
    googleMapsApiKey: GOOGLE_MAPS_API_KEY,
    libraries
  });

  const loadTenants = () => {
    api.get<Tenant[]>('/tenants')
      .then(res => setTenants(res.data))
      .catch(err => console.error(err));
  };

  useEffect(() => {
    loadTenants();
  }, []);

  const openModal = () => {
    setShowModal(true);
    setFormData({ ...emptyTenantForm });
    if (!regData) {
      api.get<RegistrationData>('/registration-data')
        .then(res => setRegData(res.data))
        .catch(() => toast.error("No se pudieron cargar los países disponibles."));
    }
  };

  const selectedCountry = regData?.countries.find(c => c.id === formData.countryId) || null;

  const handleCountryChange = (id: string) => {
    const country = regData?.countries.find(c => c.id === id);
    if (!country) { setField('countryId', id); return; }

    const defaults: Record<string, string> = { countryId: id };

    if (country.defaultCurrencyId) {
      defaults.defaultCurrencyId = country.defaultCurrencyId;
    }
    const lang = country.defaultLanguageIsoCode
      ? regData?.languages.find(l => l.isoCode === country.defaultLanguageIsoCode)
      : country.defaultLocalizationId
        ? regData?.languages.find(l => l.id === country.defaultLocalizationId)
        : null;
    if (lang) {
      defaults.defaultLanguageCode = lang.isoCode;
    }
    if (country.timezones?.length) {
      defaults.defaultTimezone = country.timezones[0];
    }
    setFormData(prev => ({ ...prev, ...defaults }));
  };

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    try {
      await api.post('/tenants', formData);
      setShowModal(false);
      setFormData({ ...emptyTenantForm });
      toast.success("Tenant registrado con éxito.");
      loadTenants();
    } catch (err) {
      const detail = axios.isAxiosError(err)
        ? (typeof err.response?.data === 'string' ? err.response.data : err.response?.data?.title) ?? err.message
        : null;
      toast.error(detail ? `Error al crear Tenant: ${detail}` : "Error al crear Tenant.");
      console.error('CreateTenant failed', err);
    } finally {
      setSaving(false);
    }
  };

  const setField = (field: string, value: string) => setFormData(prev => ({ ...prev, [field]: value }));

  const onLoadAutocomplete = (autocomplete: google.maps.places.Autocomplete) => {
    autocompleteRef.current = autocomplete;
  };

  const onPlaceChanged = () => {
    if (!autocompleteRef.current) return;
    const place = autocompleteRef.current.getPlace();
    if (!place.geometry) return;

    let line1 = '', line2 = '', city = '', state = '', postal = '', countryIso = '';
    place.address_components?.forEach(component => {
      const t = component.types;
      if (t.includes('street_number')) line1 = component.long_name + ' ' + line1;
      if (t.includes('route')) line1 += component.long_name;
      if (t.includes('sublocality_level_1') || t.includes('neighborhood') || t.includes('sublocality')) line2 = component.long_name;
      if (t.includes('locality')) city = component.long_name;
      if (t.includes('administrative_area_level_1')) state = component.long_name;
      if (t.includes('postal_code')) postal = component.long_name;
      if (t.includes('country')) countryIso = component.short_name;
    });

    const lat = place.geometry.location?.lat() ?? null;
    const lng = place.geometry.location?.lng() ?? null;

    setFormData(prev => {
      const country = countryIso ? regData?.countries.find(c => c.isoCode === countryIso) : null;
      return {
        ...prev,
        physicalAddressLine1: line1.trim() || place.formatted_address || prev.physicalAddressLine1,
        physicalAddressLine2: line2,
        physicalCity: city || prev.physicalCity,
        physicalState: state || prev.physicalState,
        physicalPostalCode: postal || prev.physicalPostalCode,
        billingAddress: place.formatted_address || prev.billingAddress,
        latitude: lat,
        longitude: lng,
        countryId: country?.id || prev.countryId,
      };
    });
  };

  return (
    <div className="p-10 animate-in fade-in slide-in-from-bottom-4 duration-500">
      <div className="flex justify-between items-center mb-8">
        <h1 className="text-3xl font-extrabold text-white tracking-tight">Gestión de Tenants</h1>
        <button 
          onClick={openModal}
          className="flex items-center bg-slate-900 text-white px-5 py-2.5 rounded-xl font-semibold hover:bg-slate-800 transition-all shadow-md hover:shadow-lg transform hover:-translate-y-0.5">
          <Plus className="w-5 h-5 mr-2" /> Registrar Tenant
        </button>
      </div>

      {showModal && (
        <div className="fixed inset-0 bg-slate-900/40 backdrop-blur-sm flex items-center justify-center z-50 animate-in fade-in duration-200 p-4 overflow-y-auto">
          <div className="modal-light bg-white p-8 rounded-3xl w-full max-w-3xl shadow-2xl animate-in zoom-in-95 duration-200 max-h-[90vh] overflow-y-auto">
            <div className="flex justify-between items-center mb-6">
              <h2 className="text-2xl font-bold text-slate-900">Registrar Tenant</h2>
              <button onClick={() => setShowModal(false)} className="text-slate-400 hover:text-slate-700 transition-colors">
                <X className="w-6 h-6" />
              </button>
            </div>
            <form onSubmit={handleCreate} className="space-y-6">

              {/* Información Principal */}
              <section className="border border-slate-200 rounded-2xl p-5">
                <h3 className="text-lg font-bold text-slate-900 mb-4 flex items-center gap-2"><Building2 className="w-5 h-5 text-blue-600" /> Información Principal</h3>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1">Nombre Comercial</label>
                    <input required type="text" placeholder="Ej. Glamtica" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all" value={formData.name} onChange={e => setField('name', e.target.value)} />
                  </div>
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1">Razón Social (Legal Name)</label>
                    <input type="text" placeholder="Ej. Glamtica SAS" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all" value={formData.legalName} onChange={e => setField('legalName', e.target.value)} />
                  </div>
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1">Nombre Comercial Secundario</label>
                    <input type="text" placeholder="Ej. Glamtica Spa" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all" value={formData.commercialName} onChange={e => setField('commercialName', e.target.value)} />
                  </div>
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1">Slug (Identificador URL)</label>
                    <input required type="text" placeholder="glamtica" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all font-mono text-sm" value={formData.slug} onChange={e => setField('slug', e.target.value.toLowerCase().replace(/\s+/g, '-'))} />
                  </div>
                </div>
              </section>

              {/* Información Fiscal */}
              <section className="border border-slate-200 rounded-2xl p-5">
                <h3 className="text-lg font-bold text-slate-900 mb-4 flex items-center gap-2"><Hash className="w-5 h-5 text-blue-600" /> Información Fiscal (DIAN)</h3>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div className="flex gap-3">
                    <div className="flex-1">
                      <label className="block text-sm font-semibold text-slate-700 mb-1">NIT / ID Fiscal</label>
                      <input required type="text" placeholder="Ej. 901958059" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all" value={formData.taxId} onChange={e => setField('taxId', e.target.value)} />
                    </div>
                    <div className="w-24">
                      <label className="block text-sm font-semibold text-slate-700 mb-1">DV</label>
                      <input required type="text" maxLength={1} placeholder="4" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all text-center" value={formData.verificationDigit} onChange={e => setField('verificationDigit', e.target.value)} />
                    </div>
                  </div>
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1">Email Facturación Electrónica</label>
                    <input required type="email" placeholder="admin@glamtica.com" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all" value={formData.einvoicingEmail} onChange={e => setField('einvoicingEmail', e.target.value)} />
                  </div>
                </div>
              </section>

              {/* Configuración Regional */}
              <section className="border border-slate-200 rounded-2xl p-5">
                <h3 className="text-lg font-bold text-slate-900 mb-4 flex items-center gap-2"><Globe className="w-5 h-5 text-blue-600" /> Configuración Regional</h3>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1">País</label>
                    <select required className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all" value={formData.countryId} onChange={e => handleCountryChange(e.target.value)}>
                      {!regData && <option value="">Cargando países...</option>}
                      {(regData?.countries || []).map(c => (
                        <option key={c.id} value={c.id}>{c.name}</option>
                      ))}
                    </select>
                    <p className="text-xs text-slate-500 mt-1">Filtra las direcciones y sugiere idioma, moneda y zona horaria.</p>
                  </div>
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1">Idioma</label>
                    <select className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all" value={formData.defaultLanguageCode} onChange={e => setField('defaultLanguageCode', e.target.value)}>
                      {(regData?.languages || []).filter(l => !selectedCountry || l.id === selectedCountry.defaultLocalizationId || l.isoCode === selectedCountry.defaultLanguageIsoCode).map(l => (
                        <option key={l.id} value={l.isoCode}>{l.name}</option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1">Moneda</label>
                    <select className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all" value={formData.defaultCurrencyId} onChange={e => setField('defaultCurrencyId', e.target.value)}>
                      {(regData?.currencies || []).filter(c => !selectedCountry || c.id === selectedCountry.defaultCurrencyId).map(c => (
                        <option key={c.id} value={c.id}>{c.code} - {c.name}</option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1">Zona Horaria</label>
                    <select className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all" value={formData.defaultTimezone} onChange={e => setField('defaultTimezone', e.target.value)}>
                      {(selectedCountry?.timezones?.length ? selectedCountry.timezones : ['America/Bogota']).map(tz => (
                        <option key={tz} value={tz}>{tz}</option>
                      ))}
                    </select>
                  </div>
                </div>
              </section>

              {/* Dirección Física con Google Autocomplete */}
              <section className="border border-slate-200 rounded-2xl p-5">
                <h3 className="text-lg font-bold text-slate-900 mb-4 flex items-center gap-2"><MapPin className="w-5 h-5 text-emerald-600" /> Dirección Física</h3>
                <div className="space-y-4">
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1">Buscar Dirección</label>
                    {isLoaded ? (
                      <Autocomplete
                        onLoad={onLoadAutocomplete}
                        onPlaceChanged={onPlaceChanged}
                        restrictions={selectedCountry ? { country: [selectedCountry.isoCode] } : undefined}
                      >
                        <input
                          type="text"
                          placeholder={selectedCountry ? `Busca dirección en ${selectedCountry.name}...` : "Selecciona un país para buscar..."}
                          disabled={!selectedCountry}
                          className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-emerald-500 transition-all disabled:bg-slate-100 disabled:text-slate-400"
                        />
                      </Autocomplete>
                    ) : (
                      <input type="text" disabled placeholder="Cargando mapas..." className="w-full px-4 py-3 bg-slate-100 text-slate-400 border border-slate-200 rounded-xl" />
                    )}
                  </div>
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <div>
                      <label className="block text-sm font-semibold text-slate-700 mb-1">Dirección (Línea 1)</label>
                      <input type="text" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-emerald-500 transition-all" value={formData.physicalAddressLine1} onChange={e => setField('physicalAddressLine1', e.target.value)} />
                    </div>
                    <div>
                      <label className="block text-sm font-semibold text-slate-700 mb-1">Dirección (Línea 2, Opcional)</label>
                      <input type="text" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-emerald-500 transition-all" value={formData.physicalAddressLine2} onChange={e => setField('physicalAddressLine2', e.target.value)} />
                    </div>
                    <div>
                      <label className="block text-sm font-semibold text-slate-700 mb-1">Ciudad</label>
                      <input type="text" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-emerald-500 transition-all" value={formData.physicalCity} onChange={e => setField('physicalCity', e.target.value)} />
                    </div>
                    <div>
                      <label className="block text-sm font-semibold text-slate-700 mb-1">Estado/Departamento</label>
                      <input type="text" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-emerald-500 transition-all" value={formData.physicalState} onChange={e => setField('physicalState', e.target.value)} />
                    </div>
                    <div>
                      <label className="block text-sm font-semibold text-slate-700 mb-1">Código Postal</label>
                      <input type="text" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-emerald-500 transition-all" value={formData.physicalPostalCode} onChange={e => setField('physicalPostalCode', e.target.value)} />
                    </div>
                    <div>
                      <label className="block text-sm font-semibold text-slate-700 mb-1">Dirección de Facturación</label>
                      <input type="text" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-emerald-500 transition-all" value={formData.billingAddress} onChange={e => setField('billingAddress', e.target.value)} />
                    </div>
                  </div>
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1">Vista previa del mapa</label>
                    <div className="h-48 rounded-xl overflow-hidden border border-slate-200 bg-slate-100">
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
                        <div className="w-full h-full flex items-center justify-center text-slate-400 font-medium">Selecciona una dirección para ver el mapa</div>
                      )}
                    </div>
                  </div>
                </div>
              </section>

              {/* Contacto */}
              <section className="border border-slate-200 rounded-2xl p-5">
                <h3 className="text-lg font-bold text-slate-900 mb-4 flex items-center gap-2"><Phone className="w-5 h-5 text-blue-600" /> Contacto</h3>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1">Persona de Contacto</label>
                    <input type="text" placeholder="Ej. Ana García" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all" value={formData.contactPerson} onChange={e => setField('contactPerson', e.target.value)} />
                  </div>
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1">Email Comercial</label>
                    <input type="email" placeholder="comercial@glamtica.com" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all" value={formData.commercialEmail} onChange={e => setField('commercialEmail', e.target.value)} />
                  </div>
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1">Teléfono</label>
                    <input type="tel" placeholder="+57 321 000 0000" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all" value={formData.contactPhone} onChange={e => setField('contactPhone', e.target.value)} />
                  </div>
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1">WhatsApp</label>
                    <input type="tel" placeholder="+57 321 000 0000" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all" value={formData.whatsappPhone} onChange={e => setField('whatsappPhone', e.target.value)} />
                  </div>
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1">Sitio Web</label>
                    <input type="url" placeholder="https://glamtica.com" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all" value={formData.website} onChange={e => setField('website', e.target.value)} />
                  </div>
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1">Email de Facturación (cuenta)</label>
                    <input type="email" placeholder="facturacion@glamtica.com" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all" value={formData.email} onChange={e => setField('email', e.target.value)} />
                  </div>
                </div>
              </section>

              {/* Usuario Administrador del Tenant */}
              <section className="border border-slate-200 rounded-2xl p-5 bg-blue-50/40">
                <h3 className="text-lg font-bold text-slate-900 mb-4 flex items-center gap-2"><Users className="w-5 h-5 text-blue-600" /> Cuenta de Administrador</h3>
                <p className="text-sm text-slate-500 mb-4">Estas serán las credenciales para que el tenant inicie sesión en su portal (tenants.facil-factura.pro).</p>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1">Nombre Completo</label>
                    <input type="text" placeholder="Ej. Ana García" className="w-full px-4 py-3 bg-white border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all" value={formData.adminName} onChange={e => setField('adminName', e.target.value)} />
                  </div>
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1">Email (Login)</label>
                    <input type="email" placeholder="admin@glamtica.com" className="w-full px-4 py-3 bg-white border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all" value={formData.adminEmail} onChange={e => setField('adminEmail', e.target.value)} />
                  </div>
                  <div className="md:col-span-2">
                    <label className="block text-sm font-semibold text-slate-700 mb-1">Contraseña Inicial</label>
                    <input type="password" placeholder="Mínimo 6 caracteres" className="w-full px-4 py-3 bg-white border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all" value={formData.adminPassword} onChange={e => setField('adminPassword', e.target.value)} />
                    <p className="text-xs text-slate-500 mt-1">Comparte esta contraseña de forma segura con tu cliente.</p>
                  </div>
                </div>
              </section>

              <div className="flex justify-end space-x-3 mt-8">
                <button type="button" onClick={() => setShowModal(false)} className="px-5 py-2.5 font-semibold text-slate-600 hover:bg-slate-100 rounded-xl transition-colors">Cancelar</button>
                <button type="submit" disabled={saving} className="px-5 py-2.5 bg-blue-600 font-semibold text-white rounded-xl shadow-md shadow-blue-500/30 hover:bg-blue-500 transition-all">
                  {saving ? 'Guardando...' : 'Guardar'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      <div className="glass-panel rounded-2xl overflow-hidden mt-8">
        <table className="w-full text-left border-collapse">
          <thead className="bg-slate-900/50">
            <tr>
              <th className="px-6 py-4 text-slate-400 font-semibold text-sm border-b border-slate-700/50">Empresa</th>
              <th className="px-6 py-4 text-slate-400 font-semibold text-sm border-b border-slate-700/50">Slug</th>
              <th className="px-6 py-4 text-slate-400 font-semibold text-sm border-b border-slate-700/50">Estado</th>
              <th className="px-6 py-4 text-slate-400 font-semibold text-sm border-b border-slate-700/50 text-right">Acciones</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-800/50">
            {tenants.map(t => (
              <tr key={t.id} className="hover:bg-slate-800/30 transition-colors group">
                <td className="px-6 py-4">
                  <p className="text-white font-semibold">{t.commercialName}</p>
                  <p className="text-xs text-slate-400 mt-0.5">{t.name}</p>
                </td>
                <td className="px-6 py-4 text-slate-300 font-mono text-sm">{t.slug}</td>
                <td className="px-6 py-4">
                  <span className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-bold border ${t.isActive ? 'bg-emerald-50 text-emerald-700 border-emerald-200/50' : 'bg-red-50 text-red-700 border-red-200/50'}`}>
                    <span className={`w-1.5 h-1.5 rounded-full mr-1.5 ${t.isActive ? 'bg-emerald-500' : 'bg-red-500'}`}></span>
                    {t.isActive ? 'Operativo' : 'Suspendido'}
                  </span>
                </td>
                <td className="px-6 py-4 text-right">
                  <button onClick={() => navigate(`/tenants/edit/${t.id}`)} className="text-blue-600 font-semibold text-sm hover:text-blue-700 opacity-0 group-hover:opacity-100 transition-opacity">
                    Configurar &rarr;
                  </button>
                </td>
              </tr>
            ))}
            {tenants.length === 0 && (
              <tr><td colSpan={4} className="text-center py-12 text-slate-500 font-medium">No hay tenants registrados en el sistema.</td></tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
};

// --- Main App Layout ---
const ProtectedLayout = ({ children }: { children: React.ReactNode }) => {
  const navigate = useNavigate();

  const handleLogout = () => {
    localStorage.removeItem('fel_superadmin_auth');
    navigate('/login');
  };

  return (
    <div className="flex h-screen bg-[#0B1120] text-slate-200 font-sans overflow-hidden">
      <aside className="w-64 bg-[#060B14] text-slate-300 flex flex-col border-r border-slate-800/50 relative z-20 shadow-2xl">
        <div className="p-8">
          <div className="flex items-center space-x-3 mb-2">
            <img src="/brand/isotipo-blanco.png" alt="Facil Factura" className="w-8 h-8 object-contain" />
            <h2 className="text-2xl font-extrabold text-white tracking-tight">Facil Factura</h2>
          </div>
          <p className="text-[10px] text-slate-500 uppercase font-bold tracking-[0.2em] ml-11">Superadmin</p>
        </div>
        
        <nav className="space-y-2 mb-8">
          <Link to="/" className="flex items-center px-4 py-3 text-slate-300 hover:bg-slate-800 hover:text-white rounded-xl transition-all">
            <LayoutDashboard className="w-5 h-5 mr-3" /> Dashboard
          </Link>
          <Link to="/tenants" className="flex items-center px-4 py-3 text-slate-300 hover:bg-slate-800 hover:text-white rounded-xl transition-all">
            <Users className="w-5 h-5 mr-3" /> Tenants (Clientes)
          </Link>
          <Link to="/document-types" className="flex items-center px-4 py-3 text-slate-300 hover:bg-slate-800 hover:text-white rounded-xl transition-all">
            <Settings className="w-5 h-5 mr-3" /> Tipos de Documento
          </Link>
          <Link to="/billing" className="flex items-center px-4 py-3 text-slate-300 hover:bg-slate-800 hover:text-white rounded-xl transition-all">
            <Receipt className="w-5 h-5 mr-3" /> Facturación
          </Link>
        </nav>

        <div className="p-4 mt-auto border-t border-slate-800">
          <button 
            onClick={handleLogout}
            className="flex items-center w-full px-4 py-3 rounded-xl font-medium text-slate-400 hover:bg-red-500/10 hover:text-red-400 transition-colors">
            <LogOut className="w-5 h-5 mr-3 opacity-70" /> Cerrar Sesión
          </button>
        </div>
      </aside>

      <main className="flex-1 overflow-y-auto relative z-10">
        {children}
      </main>
    </div>
  );
};

const App = () => {
  const [isAuthenticated, setIsAuthenticated] = useState(
    !!localStorage.getItem('fel_superadmin_auth')
  );

  return (
    <Router>
      <Toaster position="top-right" richColors />
      <Routes>
        <Route 
          path="/login" 
          element={
            isAuthenticated ? <Navigate to="/" /> : <AuthScreen onAuthSuccess={() => setIsAuthenticated(true)} />
          } 
        />
        <Route 
          path="/*" 
          element={
            isAuthenticated ? (
              <ProtectedLayout>
                <Routes>
                  <Route path="/" element={<Dashboard />} />
                  <Route path="/tenants" element={<TenantsList />} />
                  <Route path="/tenants/edit/:id" element={<TenantEdit />} />
                  <Route path="/document-types" element={<DocumentTypes />} />
                  <Route path="/document-types/:typeId/templates" element={<DocumentTemplates />} />
                  <Route path="/billing" element={<Billing />} />
                  <Route path="/settings" element={<div className="p-10"><h1 className="text-3xl font-extrabold text-slate-900 tracking-tight">Configuración del Motor</h1></div>} />
                </Routes>
              </ProtectedLayout>
            ) : (
              <Navigate to="/login" />
            )
          } 
        />
      </Routes>
    </Router>
  );
};

export default App;
