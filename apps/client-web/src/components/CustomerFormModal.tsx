import React, { useState, useEffect, useRef } from 'react';
import { GoogleMap, useJsApiLoader, Autocomplete, Marker } from '@react-google-maps/api';
import { MapPin, FileUp, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { api, getErrorMessage } from '../lib/api';
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
  email: '', phone: '', address: '', cityCode: '', cityName: '', postalCode: '',
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
  const [uploadingRut, setUploadingRut] = useState(false);
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
    api.get('/client/identification-types')
      .then(res => setIdentificationTypeOptions(res.data.map((t: any) => ({ value: t.code, label: t.name }))))
      .catch(() => {});
  }, []);

  useEffect(() => {
    if (!open) return;
    if (editingCustomer) {
      // No hay un campo persistido de tipo de persona; se infiere de si trae nombre partido
      // (persona natural) o solo razón social, con el tipo de identificación como respaldo.
      const inferredPersonType: 'Natural' | 'Juridica' =
        editingCustomer.firstName || editingCustomer.firstLastName ? 'Natural' : (editingCustomer.identificationType === '31' ? 'Juridica' : 'Natural');
      setFormData({
        ...initialForm,
        ...editingCustomer,
        personType: inferredPersonType,
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
  const { isLoaded: isMapsLoaded } = useJsApiLoader({
    id: 'google-map-script',
    googleMapsApiKey: GOOGLE_MAPS_API_KEY,
    libraries: GOOGLE_MAPS_LIBRARIES
  });

  const onLoadAutocomplete = (autocomplete: google.maps.places.Autocomplete) => {
    autocompleteRef.current = autocomplete;
  };

  const onPlaceChanged = () => {
    const place = autocompleteRef.current?.getPlace();
    if (!place?.geometry?.location) return;

    let cityName = '';
    let postalCode = '';
    place.address_components?.forEach(component => {
      if (component.types.includes('locality')) cityName = component.long_name;
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

            <div>
              <label className="block text-sm font-bold text-slate-700 mb-1">Email</label>
              <input type="email" required value={formData.email} onChange={e => setFormData({...formData, email: e.target.value})} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none" />
            </div>
            <div>
              <label className="block text-sm font-bold text-slate-700 mb-1">Teléfono</label>
              <input type="text" value={formData.phone} onChange={e => setFormData({...formData, phone: e.target.value})} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none" />
            </div>

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

            <div className="sm:col-span-2">
              <label className="block text-sm font-bold text-slate-700 mb-1">Dirección</label>
              {isMapsLoaded ? (
                <Autocomplete onLoad={onLoadAutocomplete} onPlaceChanged={onPlaceChanged}>
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
            <div>
              <label className="block text-sm font-bold text-slate-700 mb-1">Ciudad</label>
              <input type="text" value={formData.cityName} onChange={e => setFormData({...formData, cityName: e.target.value})} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none" />
            </div>
            <div>
              <label className="block text-sm font-bold text-slate-700 mb-1">Código Postal</label>
              <input type="text" value={formData.postalCode} onChange={e => setFormData({...formData, postalCode: e.target.value})} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none font-mono" />
            </div>
            <div>
              <label className="block text-sm font-bold text-slate-700 mb-1">Código DANE</label>
              <input type="text" placeholder="Ej. 11001" value={formData.cityCode} onChange={e => setFormData({...formData, cityCode: e.target.value})} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none font-mono" />
            </div>
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
