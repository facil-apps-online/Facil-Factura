import React, { useState, useEffect, useMemo, useRef } from 'react';
import { GoogleMap, useJsApiLoader, Autocomplete, Marker } from '@react-google-maps/api';
import { MapPin, FileUp, Loader2, Search } from 'lucide-react';
import { toast } from 'sonner';
import { api, getErrorMessage } from '../lib/api';
import { useSession } from '../context/SessionContext';
import SearchableSelect from '@shared/components/SearchableSelect';
import DecimalInput from './DecimalInput';
import Modal from './Modal';
import { Button } from './ui/button';

const GOOGLE_MAPS_LIBRARIES: "places"[] = ['places'];
const GOOGLE_MAPS_API_KEY = import.meta.env.VITE_GOOGLE_MAPS_API_KEY || '';

const initialForm = {
  personType: 'Natural' as 'Natural' | 'Juridica',
  name: '', firstName: '', secondName: '', firstLastName: '', secondLastName: '',
  identificationType: '13', identificationNumber: '', verificationDigit: '',
  partyType: 'Cliente',
  email: '', phone: '', address: '', countryCode: 'CO', stateName: '', cityCode: '', cityName: '', postalCode: '',
  latitude: null as number | null, longitude: null as number | null,
  taxRegime: '49', fiscalResponsibilities: 'R-99-PN',
  dataicoTaxLevelCode: '', dataicoRegimen: '',
  workerType: '', contractType: '', paymentMeans: '', bank: '', accountType: '', accountNumber: '',
  highRisk: false, integralSalary: false, baseSalary: 0, startDate: '', fireDate: ''
};

interface CustomerFormModalProps {
  open: boolean;
  editingCustomer: any | null;
  defaultPartyType: string;
  onClose: () => void;
  onSaved: (customer: any) => void;
}

// Formulario completo de Tercero (persona natural o jurídica), reutilizado tanto en "Mis Terceros"
// como en el alta rápida desde Facturas — antes Facturas tenía su propio modal reducido sin
// dirección, DV ni datos de nómina; ahora ambos comparten exactamente el mismo formulario.
export default function CustomerFormModal({ open, editingCustomer, defaultPartyType, onClose, onSaved }: CustomerFormModalProps) {
  const [formData, setFormData] = useState(initialForm);
  const [taxLevelCatalog, setTaxLevelCatalog] = useState<{ id: string, category: string, name: string }[]>([]);
  const [regimenCatalog, setRegimenCatalog] = useState<{ id: string, category: string, name: string }[]>([]);
  const [workerTypeCatalog, setWorkerTypeCatalog] = useState<{ id: string, category: string, name: string }[]>([]);
  const [contractTypeCatalog, setContractTypeCatalog] = useState<{ id: string, category: string, name: string }[]>([]);
  const [payrollPaymentMeansCatalog, setPayrollPaymentMeansCatalog] = useState<{ id: string, category: string, name: string }[]>([]);
  const [accountTypeCatalog, setAccountTypeCatalog] = useState<{ id: string, category: string, name: string }[]>([]);
  const [identificationTypeOptions, setIdentificationTypeOptions] = useState<{ value: string, label: string }[]>([]);
  // Países (tabla 13.4.1 de la DIAN): fuera de Colombia no hay código DANE; ciudad y estado se escriben.
  const [countryCatalog, setCountryCatalog] = useState<{ id: string, category: string, name: string }[]>([]);
  const [uploadingRut, setUploadingRut] = useState(false);
  const [lookingUp, setLookingUp] = useState(false);
  // Correos que la DIAN tiene registrados para el tercero buscado; si son varios, el usuario elige cuál usar.
  const [dianEmails, setDianEmails] = useState<string[]>([]);
  // Número de documento que hay escrito ahora, para descartar una respuesta de la DIAN que llega después de que el usuario lo cambió.
  const latestNumber = useRef('');
  latestNumber.current = formData.identificationNumber;
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    const loadCatalog = (kind: string, setter: (v: any) => void) =>
      api.get(`/client/tax-catalog?kind=${kind}`).then(res => setter(res.data)).catch(() => {});
    loadCatalog('TaxLevelCode', setTaxLevelCatalog);
    loadCatalog('Regimen', setRegimenCatalog);
    loadCatalog('WorkerType', setWorkerTypeCatalog);
    loadCatalog('ContractType', setContractTypeCatalog);
    loadCatalog('PayrollPaymentMeans', setPayrollPaymentMeansCatalog);
    loadCatalog('AccountType', setAccountTypeCatalog);
    loadCatalog('Country', setCountryCatalog);
    api.get('/client/identification-types')
      .then(res => setIdentificationTypeOptions(res.data.map((t: any) => ({ value: t.code, label: t.name }))))
      .catch(() => {});
  }, []);

  useEffect(() => {
    if (!open) return;
    setDianEmails([]);
    if (editingCustomer) {
      // No hay un campo persistido de tipo de persona; se infiere de si trae nombre partido
      // (persona natural) o solo razón social, con el tipo de identificación como respaldo.
      const inferredPersonType: 'Natural' | 'Juridica' =
        editingCustomer.firstName || editingCustomer.firstLastName ? 'Natural' : (editingCustomer.identificationType === '31' ? 'Juridica' : 'Natural');
      setFormData({
        ...initialForm,
        ...editingCustomer,
        personType: inferredPersonType,
        countryCode: editingCustomer.countryCode || 'CO',
        stateName: editingCustomer.stateName || '',
        firstName: editingCustomer.firstName || '',
        secondName: editingCustomer.secondName || '',
        firstLastName: editingCustomer.firstLastName || '',
        secondLastName: editingCustomer.secondLastName || ''
      });
    } else {
      setFormData({ ...initialForm, partyType: defaultPartyType });
    }
  }, [open, editingCustomer, defaultPartyType]);

  const isJuridica = formData.personType === 'Juridica';

  const updateNamePart = (field: 'firstName' | 'secondName' | 'firstLastName' | 'secondLastName', value: string) => {
    const next = { ...formData, [field]: value };
    next.name = [next.firstName, next.secondName, next.firstLastName, next.secondLastName].filter(Boolean).join(' ');
    setFormData(next);
  };

  const autocompleteRef = useRef<google.maps.places.Autocomplete | null>(null);
  const { location, branches, selectedBranchId, features } = useSession();
  // Departamento (2 primeros dígitos del DANE) de la sucursal activa: desempata nombres de ciudad repetidos.
  const branchDepartment = branches.find(b => b.id === selectedBranchId)?.cityCode?.slice(0, 2) ?? branches[0]?.cityCode?.slice(0, 2) ?? '';
  const { isLoaded: isMapsLoaded } = useJsApiLoader({
    id: 'google-map-script',
    googleMapsApiKey: GOOGLE_MAPS_API_KEY,
    libraries: GOOGLE_MAPS_LIBRARIES
  });

  // Zona de ~40 km alrededor de la empresa: Google muestra primero las direcciones de ahí, pero se puede buscar en cualquier parte de Colombia.
  const searchBias = useMemo(() => {
    if (!isMapsLoaded || !location) return undefined;
    const delta = 0.35;
    return new google.maps.LatLngBounds(
      { lat: location.latitude - delta, lng: location.longitude - delta },
      { lat: location.latitude + delta, lng: location.longitude + delta });
  }, [isMapsLoaded, location]);

  const onLoadAutocomplete = (autocomplete: google.maps.places.Autocomplete) => {
    autocompleteRef.current = autocomplete;
  };

  const onPlaceChanged = () => {
    const place = autocompleteRef.current?.getPlace();
    if (!place?.geometry?.location) return;

    let cityName = '';
    let departmentName = '';
    let postalCode = '';
    place.address_components?.forEach(component => {
      if (component.types.includes('locality')) cityName = component.long_name;
      if (component.types.includes('administrative_area_level_1')) departmentName = component.long_name;
      if (component.types.includes('postal_code')) postalCode = component.long_name;
    });

    setFormData({
      ...formData,
      address: place.formatted_address || formData.address,
      cityName: cityName || formData.cityName,
      postalCode: postalCode || formData.postalCode,
      latitude: place.geometry.location.lat(),
      longitude: place.geometry.location.lng()
    });

    // El código DANE sale del catálogo DIAN según la ciudad de la dirección; si no hay una coincidencia única se deja el que ya tenía.
    if (cityName) {
      api.get('/client/customers/municipality-match', { params: { name: cityName, department: departmentName, preferDepartment: branchDepartment } })
        .then(res => setFormData(prev => ({ ...prev, cityCode: res.data.code })))
        .catch(() => { /* sin coincidencia: el usuario puede digitar el código */ });
    }
  };

  // El RUT imprime la dirección abreviada y sin el "#" ("CR 5 7 24 ED PAOLA BABON OF 503"), que
  // Google Maps no reconoce — hay que expandir la vía y reescribir el número cruzado en el
  // formato colombiano ("Carrera 5 # 7-24") antes de geocodificar. Solo se usa para la búsqueda:
  // el texto guardado en el formulario sigue siendo el literal del RUT.
  const normalizeAddressForGeocoding = (raw: string): string => {
    let s = raw.trim();
    const abreviaturas: [RegExp, string][] = [
      [/\b(KR|CRA|CR)\b\.?/gi, 'Carrera'],
      [/\b(CLL|CL)\b\.?/gi, 'Calle'],
      [/\b(AVDA|AV)\b\.?/gi, 'Avenida'],
      [/\b(DIAG|DG)\b\.?/gi, 'Diagonal'],
      [/\b(TRANSV|TV)\b\.?/gi, 'Transversal']
    ];
    for (const [re, val] of abreviaturas) s = s.replace(re, val);

    // Si calza el patrón "Vía Número Número-Número" (con o sin "#" ya puesto), se recorta
    // cualquier texto detrás (nombre de edificio, oficina) — no aporta a la búsqueda y a veces
    // la confunde — y se asegura el "#" en el sitio correcto.
    const m = s.match(/^(Carrera|Calle|Avenida|Diagonal|Transversal)\s+(\d+\s?[A-Za-z]?)\s*#?\s*(\d+)[\s-]+(\d+)/i);
    return m ? `${m[1]} ${m[2].trim()} # ${m[3]}-${m[4]}` : s;
  };

  // El RUT trae la dirección como texto, sin coordenadas — sin esto, el mapa quedaba vacío hasta
  // que el usuario volviera a escribirla a mano en el autocompletar de Google. Geocodifica la
  // misma dirección apenas se carga el RUT, para que el pin aparezca de una vez.
  const geocodeRutAddress = (address?: string, cityName?: string, department?: string) => {
    if (!isMapsLoaded || !address) return;
    const query = [normalizeAddressForGeocoding(address), cityName, department, 'Colombia'].filter(Boolean).join(', ');
    new google.maps.Geocoder().geocode({ address: query }, (results, status) => {
      if (status === 'OK' && results && results[0]?.geometry?.location) {
        const loc = results[0].geometry.location;
        setFormData(prev => ({ ...prev, latitude: loc.lat(), longitude: loc.lng() }));
      }
    });
  };

  const handleRutUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setUploadingRut(true);
    try {
      const uploadData = new FormData();
      uploadData.append('file', file);
      const res = await api.post('/client/customers/parse-rut', uploadData, {
        headers: { 'Content-Type': 'multipart/form-data' }
      });
      const rut = res.data;
      setFormData(prev => ({
        ...prev,
        personType: rut.personType,
        identificationType: rut.identificationType,
        identificationNumber: rut.identificationNumber || prev.identificationNumber,
        verificationDigit: rut.verificationDigit || prev.verificationDigit,
        name: rut.name || prev.name,
        firstName: rut.firstName || prev.firstName,
        secondName: rut.secondName || prev.secondName,
        firstLastName: rut.firstLastName || prev.firstLastName,
        secondLastName: rut.secondLastName || prev.secondLastName,
        address: rut.address || prev.address,
        cityName: rut.cityName || prev.cityName,
        cityCode: rut.cityCode || prev.cityCode,
        email: rut.email || prev.email,
        phone: rut.phone || prev.phone,
        dataicoRegimen: rut.dataicoRegimen || prev.dataicoRegimen,
        dataicoTaxLevelCode: rut.dataicoTaxLevelCode || prev.dataicoTaxLevelCode
      }));
      toast.success('Datos del RUT cargados — revísalos antes de guardar.');
      geocodeRutAddress(rut.address, rut.cityName, rut.department);
    } catch (err: any) {
      toast.error(getErrorMessage(err, 'No se pudo leer el RUT'));
    } finally {
      setUploadingRut(false);
      e.target.value = '';
    }
  };

  // La DIAN devuelve un solo nombre completo. Para persona natural se reparte en los cuatro campos con la convención colombiana
  // (nombres primero, luego apellidos); las partículas ("DE", "DE LA", "DEL", "SAN"...) se pegan a la palabra que sigue para no partir
  // un apellido compuesto. Con 3 unidades se toma un nombre y dos apellidos. El usuario lo revisa antes de guardar.
  const splitNaturalName = (full: string) => {
    const words = full.trim().split(/\s+/).filter(Boolean);
    const parts: string[] = [];
    let pending = '';
    for (const w of words) {
      pending = pending ? `${pending} ${w}` : w;
      if (!/^(DE|DEL|LA|LAS|LOS|Y|SAN|SANTA)$/i.test(w)) { parts.push(pending); pending = ''; }
    }
    if (pending) parts.push(pending);
    if (parts.length <= 1) return { firstName: parts[0] ?? '', secondName: '', firstLastName: '', secondLastName: '' };
    if (parts.length === 2) return { firstName: parts[0], secondName: '', firstLastName: parts[1], secondLastName: '' };
    if (parts.length === 3) return { firstName: parts[0], secondName: '', firstLastName: parts[1], secondLastName: parts[2] };
    return { firstName: parts[0], secondName: parts[1], firstLastName: parts[2], secondLastName: parts.slice(3).join(' ') };
  };

  // El reparto es una suposición: hay duda cuando el nombre trae partículas (DE, DEL, LA...) o no se arma de 2 o 4 componentes
  // (con 1 o 3 no se sabe dónde termina el nombre y empiezan los apellidos; con más de 4 sobra algo).
  const nameHasDoubts = (full: string) => {
    const words = full.trim().split(/\s+/).filter(Boolean);
    const hasParticles = words.some(w => /^(DE|DEL|LA|LAS|LOS|Y|SAN|SANTA)$/i.test(w));
    const units = words.filter(w => !/^(DE|DEL|LA|LAS|LOS|Y|SAN|SANTA)$/i.test(w)).length;
    return hasParticles || (units !== 2 && units !== 4);
  };

  // Tipos de documento que la DIAN resuelve (pasaporte, tipo 42 y otros no están en su registro de adquirentes).
  const DIAN_LOOKUP_TYPES = ['12', '13', '21', '22', '31', '41', '47', '48', '50', '91'];
  const lookupNumber = formData.identificationNumber.replace(/\D/g, '');
  const canLookup = DIAN_LOOKUP_TYPES.includes(formData.identificationType) && lookupNumber.length >= 3;

  const handleDianLookup = async () => {
    const queried = formData.identificationNumber;
    setLookingUp(true);
    try {
      const res = await api.post('/client/customers/dian-lookup', {
        identificationType: formData.identificationType,
        identificationNumber: lookupNumber
      });
      const { found, name, emails } = res.data as { found: boolean; name: string | null; emails: string[] };
      if (!found || !name) {
        toast.info('La DIAN no tiene a este tercero registrado. Ingresa sus datos manualmente.');
        return;
      }
      // Si el usuario cambió el número mientras se consultaba, la respuesta ya no corresponde.
      if (latestNumber.current !== queried) return;
      setFormData(prev => {
        const next = { ...prev, email: prev.email.trim() ? prev.email : (emails[0] || '') };
        if (prev.personType === 'Juridica') return { ...next, name };
        // Persona natural con NIT (comerciante): la DIAN da un solo nombre y no se sabe dónde parten los apellidos, así que va entero en Nombre 1.
        const parts = prev.identificationType === '31'
          ? { firstName: name, secondName: '', firstLastName: '', secondLastName: '' }
          : splitNaturalName(name);
        return { ...next, ...parts, name: [parts.firstName, parts.secondName, parts.firstLastName, parts.secondLastName].filter(Boolean).join(' ') };
      });
      setDianEmails(emails);
      toast.success(formData.personType === 'Juridica'
        ? 'Razón social cargada desde la DIAN — revísala antes de guardar.'
        : formData.identificationType === '31'
          ? 'Datos de la DIAN cargados. El nombre completo quedó en Nombre 1: sepáralo en nombres y apellidos.'
          : nameHasDoubts(name)
            ? 'Datos de la DIAN cargados. El nombre tiene partículas o no se pudo repartir con certeza: revisa cómo quedaron los nombres y apellidos.'
            : 'Datos de la DIAN cargados — revísalos antes de guardar.');
      if (emails.length === 0) toast.info('La DIAN no tiene correo para este tercero: escríbelo tú.');
    } catch (err: any) {
      toast.error(err?.response?.status === 429
        ? 'Demasiadas consultas seguidas a la DIAN. Espera un momento e inténtalo de nuevo.'
        : getErrorMessage(err, 'No se pudo consultar la DIAN'));
    } finally {
      setLookingUp(false);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    try {
      // startDate/fireDate son DateTime? en el backend: un string vacío (el valor por defecto de
      // un <input type="date"> sin diligenciar) rompe la deserialización del body entero, no solo
      // esos dos campos — de ahí que un Cliente cualquiera (sin nómina) fallara al guardar.
      const payload = {
        ...formData,
        startDate: formData.startDate || null,
        fireDate: formData.fireDate || null
      };
      let saved;
      if (editingCustomer) {
        saved = await api.put(`/client/customers/${editingCustomer.id}`, payload);
        toast.success(`${formData.partyType} actualizado`);
      } else {
        saved = await api.post('/client/customers', payload);
        toast.success(`${formData.partyType} creado`);
      }
      onSaved(saved.data);
    } catch (err: any) {
      toast.error(getErrorMessage(err, 'Error guardando cliente'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal
      open={open}
      onOpenChange={o => { if (!o) onClose(); }}
      title={editingCustomer ? 'Editar Tercero' : 'Nuevo Tercero'}
      size="md"
      // Los SearchableSelect y el autocompletado de Google Maps se pintan fuera del modal.
      withFloatingPickers
      footer={
        <>
          <Button type="button" variant="ghost" onClick={onClose}>Cancelar</Button>
          <Button type="submit" form="customer-form" disabled={saving}>
            {saving ? 'Guardando...' : `Guardar ${formData.partyType}`}
          </Button>
        </>
      }
    >
        <form id="customer-form" onSubmit={handleSubmit}>
          {!editingCustomer && (
            <div className="mb-4 p-4 bg-blue-50 border border-blue-100 rounded-xl flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <p className="text-sm font-bold text-slate-700">Cargar datos desde el RUT</p>
                <p className="text-xs text-slate-500">Completa algunos datos automáticamente. Revísalos antes de guardar.</p>
              </div>
              <label className={`shrink-0 flex items-center gap-2 px-4 py-2 rounded-xl font-bold text-sm cursor-pointer transition-colors ${uploadingRut ? 'bg-slate-200 text-slate-400' : 'bg-primary text-white hover:bg-primary/90'}`}>
                {uploadingRut ? <Loader2 size={16} className="animate-spin" /> : <FileUp size={16} />}
                {uploadingRut ? 'Leyendo...' : 'Cargar RUT'}
                <input type="file" accept="application/pdf" className="hidden" disabled={uploadingRut} onChange={handleRutUpload} />
              </label>
            </div>
          )}

          <div className="mb-4">
            <label className="block text-sm font-bold text-slate-700 mb-1">Tipo de Persona</label>
            <div className="grid grid-cols-2 gap-2">
              {(['Natural', 'Juridica'] as const).map(pt => (
                <button
                  key={pt}
                  type="button"
                  onClick={() => setFormData({ ...formData, personType: pt, identificationType: pt === 'Juridica' ? '31' : '13' })}
                  className={`px-3 py-2.5 rounded-xl font-bold text-sm transition-colors ${
                    formData.personType === pt ? 'bg-primary text-white shadow-sm' : 'bg-slate-100 text-slate-500 hover:bg-slate-200'
                  }`}
                >
                  {pt === 'Natural' ? 'Persona Natural' : 'Persona Jurídica'}
                </button>
              ))}
            </div>
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 mb-4">
            {isJuridica ? (
              <div className="sm:col-span-2">
                <label className="block text-sm font-bold text-slate-700 mb-1">Razón Social</label>
                <input type="text" required value={formData.name} onChange={e => setFormData({...formData, name: e.target.value})} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none" />
              </div>
            ) : (
              <div className="sm:col-span-2 grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-1">Nombre 1</label>
                  <input type="text" required value={formData.firstName} onChange={e => updateNamePart('firstName', e.target.value)} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none" />
                </div>
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-1">Nombre 2</label>
                  <input type="text" value={formData.secondName} onChange={e => updateNamePart('secondName', e.target.value)} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none" />
                </div>
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-1">Apellido 1</label>
                  <input type="text" required value={formData.firstLastName} onChange={e => updateNamePart('firstLastName', e.target.value)} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none" />
                </div>
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-1">Apellido 2</label>
                  <input type="text" value={formData.secondLastName} onChange={e => updateNamePart('secondLastName', e.target.value)} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none" />
                </div>
              </div>
            )}

            <div>
              <label className="block text-sm font-bold text-slate-700 mb-1">Tipo de Identificación</label>
              <SearchableSelect
                value={formData.identificationType}
                onChange={v => setFormData({...formData, identificationType: v})}
                placeholder="Buscar tipo de identificación..."
                options={identificationTypeOptions}
              />
            </div>
            <div className="flex gap-2">
              <div className="flex-1">
                <label className="block text-sm font-bold text-slate-700 mb-1">Número</label>
                <input type="text" required value={formData.identificationNumber} onChange={e => setFormData({...formData, identificationNumber: e.target.value})} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none" />
              </div>
              {formData.identificationType === '31' && (
                <div className="w-20">
                  <label className="block text-sm font-bold text-slate-700 mb-1">DV</label>
                  <input type="text" value={formData.verificationDigit} onChange={e => setFormData({...formData, verificationDigit: e.target.value})} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none text-center" />
                </div>
              )}
            </div>
            {features.dianLookup && !editingCustomer && (
              <div className="sm:col-span-2 -mt-2">
                <button
                  type="button" onClick={handleDianLookup} disabled={lookingUp || !canLookup} aria-busy={lookingUp}
                  className="inline-flex items-center gap-1.5 text-sm font-bold text-primary hover:underline disabled:text-slate-400 disabled:no-underline"
                >
                  {lookingUp ? <Loader2 size={14} className="animate-spin" /> : <Search size={14} />}
                  {lookingUp ? 'Buscando en la DIAN...' : 'Buscar datos en la DIAN'}
                </button>
                <span className="ml-2 text-xs text-slate-500">{canLookup ? 'Llena nombre y correo si el tercero los registró.' : 'Escribe el número del documento para buscar.'}</span>
              </div>
            )}

            <div>
              <label className="block text-sm font-bold text-slate-700 mb-1">Email</label>
              <input type="email" required value={formData.email} onChange={e => setFormData({...formData, email: e.target.value})} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none" />
              {dianEmails.length > 1 && (
                <div className="mt-2">
                  <p className="text-xs text-slate-500 mb-1">La DIAN tiene {dianEmails.length} correos registrados. Elige cuál usar:</p>
                  <div className="flex flex-wrap gap-1.5">
                    {dianEmails.map(e => (
                      <button
                        key={e} type="button" onClick={() => setFormData(prev => ({ ...prev, email: e }))}
                        className={`px-2.5 py-1 rounded-lg text-xs font-bold break-all text-left transition-colors ${formData.email === e ? 'bg-primary text-white' : 'bg-slate-100 text-slate-600 hover:bg-slate-200'}`}
                      >
                        {e}
                      </button>
                    ))}
                  </div>
                </div>
              )}
            </div>
            <div>
              <label className="block text-sm font-bold text-slate-700 mb-1">Teléfono</label>
              <input type="text" value={formData.phone} onChange={e => setFormData({...formData, phone: e.target.value})} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none" />
            </div>

            {/* Un empleado no tiene régimen fiscal ni nivel tributario: no se piden. */}
            {formData.partyType !== 'Empleado' && (
              <>
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-1">Régimen Fiscal</label>
                <SearchableSelect
                  required
                  value={formData.dataicoRegimen}
                  onChange={v => setFormData({...formData, dataicoRegimen: v})}
                  placeholder="Buscar régimen..."
                  options={regimenCatalog.map(c => ({ value: c.category, label: c.name }))}
                />
              </div>
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-1">Nivel Tributario</label>
                <SearchableSelect
                  required
                  value={formData.dataicoTaxLevelCode}
                  onChange={v => setFormData({...formData, dataicoTaxLevelCode: v})}
                  placeholder="Buscar nivel tributario..."
                  options={taxLevelCatalog.map(c => ({ value: c.category, label: c.name }))}
                />
              </div>
              </>
            )}

            <div className="sm:col-span-2">
              <label className="block text-sm font-bold text-slate-700 mb-1">Dirección</label>
              {isMapsLoaded ? (
                <Autocomplete onLoad={onLoadAutocomplete} onPlaceChanged={onPlaceChanged} options={{ componentRestrictions: { country: 'co' }, bounds: searchBias, strictBounds: false }}>
                  <input
                    type="text" placeholder="Busca la dirección en Google Maps..."
                    value={formData.address}
                    onChange={e => setFormData({...formData, address: e.target.value})}
                    className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none"
                  />
                </Autocomplete>
              ) : (
                <input type="text" value={formData.address} onChange={e => setFormData({...formData, address: e.target.value})} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none" />
              )}
            </div>
            {formData.latitude != null && formData.longitude != null && (
              <div className="sm:col-span-2 h-40 rounded-xl overflow-hidden border border-slate-200">
                {isMapsLoaded ? (
                  <GoogleMap
                    mapContainerStyle={{ width: '100%', height: '100%' }}
                    center={{ lat: formData.latitude, lng: formData.longitude }}
                    zoom={15}
                    options={{ disableDefaultUI: true, zoomControl: true }}
                  >
                    <Marker position={{ lat: formData.latitude, lng: formData.longitude }} />
                  </GoogleMap>
                ) : (
                  <div className="w-full h-full bg-slate-100 flex items-center justify-center text-slate-400">
                    <MapPin className="w-6 h-6" />
                  </div>
                )}
              </div>
            )}
            {formData.partyType !== 'Empleado' && (
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-1">País</label>
                <SearchableSelect
                  value={formData.countryCode}
                  onChange={v => setFormData({
                    ...formData,
                    countryCode: v || 'CO',
                    // Fuera de Colombia no hay código DANE; en Colombia no hay estado como texto.
                    cityCode: v && v !== 'CO' ? '' : formData.cityCode,
                    stateName: !v || v === 'CO' ? '' : formData.stateName
                  })}
                  placeholder="Buscar país..."
                  options={countryCatalog.length === 0
                    ? [{ value: 'CO', label: 'Colombia', shortLabel: 'CO' }]
                    : countryCatalog.map(c => ({ value: c.category, label: `${c.name} (${c.category})`, shortLabel: c.category }))}
                />
              </div>
            )}
            {formData.countryCode !== 'CO' && (
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-1">Departamento o estado</label>
                <input type="text" value={formData.stateName} onChange={e => setFormData({...formData, stateName: e.target.value})} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none" />
              </div>
            )}
            <div>
              <label className="block text-sm font-bold text-slate-700 mb-1">Ciudad{formData.countryCode !== 'CO' && ' *'}</label>
              <input type="text" required={formData.countryCode !== 'CO'} value={formData.cityName} onChange={e => setFormData({...formData, cityName: e.target.value})} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none" />
            </div>
            <div>
              <label className="block text-sm font-bold text-slate-700 mb-1">Código Postal</label>
              <input type="text" value={formData.postalCode} onChange={e => setFormData({...formData, postalCode: e.target.value})} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none font-mono" />
            </div>
            {formData.countryCode === 'CO' && (
            <div>
              <label className="block text-sm font-bold text-slate-700 mb-1">Código DANE{formData.partyType === 'Empleado' && ' *'}</label>
              <input
                type="text"
                placeholder="Ej. 11001"
                value={formData.cityCode}
                onChange={e => setFormData({...formData, cityCode: e.target.value})}
                required={formData.partyType === 'Empleado'}
                pattern={formData.partyType === 'Empleado' ? '\\d{5,}' : undefined}
                title="Código DANE del municipio (5 dígitos o más)"
                className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none font-mono"
              />
              {formData.partyType === 'Empleado' && !/^\d{5,}$/.test(formData.cityCode.trim()) && (
                <p className="mt-1 text-xs font-bold text-red-600">
                  Obligatorio para empleados: Dataico exige ciudad y departamento en la nómina. Se completa al elegir la dirección o escribiendo el código.
                </p>
              )}
            </div>
            )}
          </div>

          {formData.partyType === 'Empleado' && (
            <div className="mt-4 pt-4 border-t border-slate-100">
              <h4 className="text-sm font-bold text-slate-700 mb-3">Datos de nómina</h4>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div>
                  <label className="block text-xs font-bold text-slate-500 mb-1">Tipo de Trabajador</label>
                  <SearchableSelect
                    required
                    value={formData.workerType}
                    onChange={v => setFormData({...formData, workerType: v})}
                    placeholder="Buscar tipo de trabajador..."
                    options={workerTypeCatalog.map(c => ({ value: c.category, label: c.name }))}
                  />
                </div>
                <div>
                  <label className="block text-xs font-bold text-slate-500 mb-1">Tipo de Contrato</label>
                  <SearchableSelect
                    required
                    value={formData.contractType}
                    onChange={v => setFormData({...formData, contractType: v})}
                    placeholder="Buscar tipo de contrato..."
                    options={contractTypeCatalog.map(c => ({ value: c.category, label: c.name }))}
                  />
                </div>
                <div>
                  <label className="block text-xs font-bold text-slate-500 mb-1">Medio de Pago</label>
                  <SearchableSelect
                    required
                    value={formData.paymentMeans}
                    onChange={v => setFormData({...formData, paymentMeans: v})}
                    placeholder="Buscar medio de pago..."
                    options={payrollPaymentMeansCatalog.map(c => ({ value: c.category, label: c.name }))}
                  />
                </div>
                <div>
                  <label className="block text-xs font-bold text-slate-500 mb-1">Salario Base</label>
                  <DecimalInput value={formData.baseSalary} onValueChange={v => setFormData({...formData, baseSalary: v})} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none font-mono" />
                </div>
                <div>
                  <label className="block text-xs font-bold text-slate-500 mb-1">Banco</label>
                  <input type="text" value={formData.bank} onChange={e => setFormData({...formData, bank: e.target.value})} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none" />
                </div>
                <div>
                  <label className="block text-xs font-bold text-slate-500 mb-1">Tipo de Cuenta</label>
                  <SearchableSelect
                    value={formData.accountType}
                    onChange={v => setFormData({...formData, accountType: v})}
                    placeholder="Buscar tipo de cuenta..."
                    options={accountTypeCatalog.map(c => ({ value: c.category, label: c.name }))}
                  />
                </div>
                <div>
                  <label className="block text-xs font-bold text-slate-500 mb-1">Número de Cuenta</label>
                  <input type="text" value={formData.accountNumber} onChange={e => setFormData({...formData, accountNumber: e.target.value})} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none" />
                </div>
                <div>
                  <label className="block text-xs font-bold text-slate-500 mb-1">Fecha de Ingreso</label>
                  <input type="date" value={formData.startDate?.substring(0, 10) || ''} onChange={e => setFormData({...formData, startDate: e.target.value})} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none" />
                </div>
                <div className="flex flex-wrap items-center gap-4 sm:col-span-2">
                  <label className="flex items-center gap-2 text-sm font-medium text-slate-600">
                    <input type="checkbox" checked={formData.highRisk} onChange={e => setFormData({...formData, highRisk: e.target.checked})} /> Alto Riesgo
                  </label>
                  <label className="flex items-center gap-2 text-sm font-medium text-slate-600">
                    <input type="checkbox" checked={formData.integralSalary} onChange={e => setFormData({...formData, integralSalary: e.target.checked})} /> Salario Integral
                  </label>
                </div>
              </div>
            </div>
          )}

        </form>
    </Modal>
  );
}
