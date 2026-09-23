import React, { useState, useEffect } from 'react';
import { GoogleMap, useJsApiLoader, Autocomplete, Marker } from '@react-google-maps/api';
import { Plus, Edit2, Trash2, X, Loader2, Search, MapPin } from 'lucide-react';
import { toast } from 'sonner';
import { api, getErrorMessage } from '../lib/api';
import ImportExcelButton from '../components/ImportExcelButton';
import SearchableSelect from '@shared/components/SearchableSelect';

const GOOGLE_MAPS_LIBRARIES: "places"[] = ['places'];
const GOOGLE_MAPS_API_KEY = import.meta.env.VITE_GOOGLE_MAPS_API_KEY || '';

const PARTY_TYPES = [
  { value: 'Cliente', label: 'Clientes' },
  { value: 'Proveedor', label: 'Proveedores' },
  { value: 'Empleado', label: 'Empleados' }
];

const IDENTIFICATION_TYPES = [
  { value: '13', label: 'Cédula de Ciudadanía' },
  { value: '22', label: 'Cédula de Extranjería' },
  { value: '42', label: 'Documento de Identificación Extranjero' },
  { value: '31', label: 'NIT' },
  { value: '50', label: 'NIT de Otro País' },
  { value: '91', label: 'NUIP' },
  { value: '41', label: 'Pasaporte' },
  { value: '11', label: 'Registro Civil' },
  { value: '21', label: 'Tarjeta de Extranjería' },
  { value: '12', label: 'Tarjeta de Identidad' }
];

export default function CustomersPage() {
  const [customers, setCustomers] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingCustomer, setEditingCustomer] = useState<any>(null);
  const [activeType, setActiveType] = useState('Cliente');
  const [search, setSearch] = useState('');
  const [taxLevelCatalog, setTaxLevelCatalog] = useState<{ id: string, category: string, name: string }[]>([]);
  const [regimenCatalog, setRegimenCatalog] = useState<{ id: string, category: string, name: string }[]>([]);
  const [workerTypeCatalog, setWorkerTypeCatalog] = useState<{ id: string, category: string, name: string }[]>([]);
  const [contractTypeCatalog, setContractTypeCatalog] = useState<{ id: string, category: string, name: string }[]>([]);
  const [payrollPaymentMeansCatalog, setPayrollPaymentMeansCatalog] = useState<{ id: string, category: string, name: string }[]>([]);
  const [accountTypeCatalog, setAccountTypeCatalog] = useState<{ id: string, category: string, name: string }[]>([]);

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
  const [formData, setFormData] = useState(initialForm);

  useEffect(() => {
    loadCustomers();
  }, [activeType]);

  useEffect(() => {
    const loadCatalog = (kind: string, setter: (v: any) => void) =>
      api.get(`/client/tax-catalog?kind=${kind}`).then(res => setter(res.data)).catch(() => {});
    loadCatalog('TaxLevelCode', setTaxLevelCatalog);
    loadCatalog('Regimen', setRegimenCatalog);
    loadCatalog('WorkerType', setWorkerTypeCatalog);
    loadCatalog('ContractType', setContractTypeCatalog);
    loadCatalog('PayrollPaymentMeans', setPayrollPaymentMeansCatalog);
    loadCatalog('AccountType', setAccountTypeCatalog);
  }, []);

  const loadCustomers = () => {
    setLoading(true);
    api.get(`/client/customers?partyType=${activeType}`)
      .then(res => {
        setCustomers(res.data);
        setLoading(false);
      })
      .catch(() => {
        toast.error('Error al cargar clientes');
        setLoading(false);
      });
  };

  const handleOpenModal = (customer?: any) => {
    if (customer) {
      setEditingCustomer(customer);
      // No hay un campo persistido de tipo de persona; se infiere de si trae nombre partido
      // (persona natural) o solo razón social, con el NIT como respaldo para registros viejos.
      const inferredPersonType: 'Natural' | 'Juridica' =
        customer.firstName || customer.firstLastName ? 'Natural' : (customer.identificationType === '31' ? 'Juridica' : 'Natural');
      setFormData({
        ...initialForm,
        ...customer,
        personType: inferredPersonType,
        firstName: customer.firstName || '',
        secondName: customer.secondName || '',
        firstLastName: customer.firstLastName || '',
        secondLastName: customer.secondLastName || ''
      });
    } else {
      setEditingCustomer(null);
      setFormData({ ...initialForm, partyType: activeType });
    }
    setIsModalOpen(true);
  };

  const isJuridica = formData.personType === 'Juridica';

  // Persona natural: Name se recalcula como la concatenación de los 4 campos, para que listados
  // y búsquedas (que usan Name) sigan funcionando sin cambios; persona jurídica usa Name directo.
  const updateNamePart = (field: 'firstName' | 'secondName' | 'firstLastName' | 'secondLastName', value: string) => {
    const next = { ...formData, [field]: value };
    next.name = [next.firstName, next.secondName, next.firstLastName, next.secondLastName].filter(Boolean).join(' ');
    setFormData(next);
  };

  const autocompleteRef = React.useRef<google.maps.places.Autocomplete | null>(null);
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

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      if (editingCustomer) {
        await api.put(`/client/customers/${editingCustomer.id}`, formData);
        toast.success('Cliente actualizado');
      } else {
        await api.post('/client/customers', formData);
        toast.success('Cliente creado');
      }
      setIsModalOpen(false);
      loadCustomers();
    } catch (err: any) {
      toast.error(getErrorMessage(err, 'Error guardando cliente'));
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm('¿Estás seguro de eliminar este cliente?')) return;
    try {
      await api.delete(`/client/customers/${id}`);
      toast.success('Cliente eliminado');
      loadCustomers();
    } catch (err: any) {
      toast.error('Error al eliminar');
    }
  };

  if (loading) return <div className="flex justify-center p-12"><Loader2 className="animate-spin w-8 h-8 text-primary" /></div>;

  const q = search.trim().toLowerCase();
  const filteredCustomers = q
    ? customers.filter(c => [c.name, c.identificationNumber, c.email, c.phone].some((f: string) => f?.toLowerCase().includes(q)))
    : customers;

  return (
    <div className="p-8">
      <div className="flex justify-between items-center mb-8">
        <div>
          <h1 className="text-3xl font-extrabold text-slate-800">Mis Terceros</h1>
          <p className="text-slate-500 mt-1">Clientes, proveedores y empleados para tus documentos electrónicos</p>
        </div>
        <div className="flex gap-3">
          <ImportExcelButton endpoint="/client/customers/import" templateEndpoint="/client/customers/template" label="Importar Excel" onDone={loadCustomers} />
          <button
            onClick={() => handleOpenModal()}
            className="bg-primary hover:bg-primary/90 text-white px-5 py-2.5 rounded-xl font-bold flex items-center gap-2 shadow-sm transition-all"
          >
            <Plus size={20} /> Nuevo {activeType}
          </button>
        </div>
      </div>

      <div className="flex gap-2 mb-6 items-center">
        {PARTY_TYPES.map(pt => (
          <button
            key={pt.value}
            onClick={() => setActiveType(pt.value)}
            className={`px-4 py-2 rounded-xl font-bold text-sm transition-colors ${
              activeType === pt.value ? 'bg-primary text-white shadow-sm' : 'bg-white text-slate-500 border border-slate-200 hover:bg-slate-50'
            }`}
          >
            {pt.label}
          </button>
        ))}
        <div className="relative ml-auto w-80">
          <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
          <input
            type="text"
            placeholder="Buscar por nombre, identificación, email o teléfono..."
            value={search}
            onChange={e => setSearch(e.target.value)}
            className="w-full pl-9 pr-3 py-2 bg-white border border-slate-200 rounded-xl text-sm outline-none focus:ring-2 focus:ring-primary"
          />
        </div>
      </div>

      <div className="bg-white rounded-2xl shadow-sm border border-slate-200 overflow-hidden">
        <table className="w-full text-left border-collapse">
          <thead>
            <tr className="bg-slate-50 border-b border-slate-200 text-sm font-bold text-slate-500 uppercase tracking-wider">
              <th className="p-4">Identificación</th>
              <th className="p-4">Razón Social / Nombre</th>
              <th className="p-4">Contacto</th>
              <th className="p-4">Régimen</th>
              <th className="p-4 text-right">Acciones</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {filteredCustomers.map(c => (
              <tr key={c.id} className="hover:bg-slate-50/50 transition-colors">
                <td className="p-4 font-medium text-slate-700">
                  {c.identificationType === '31' ? 'NIT' : 'CC'} {c.identificationNumber}
                  {c.verificationDigit && `-${c.verificationDigit}`}
                </td>
                <td className="p-4 text-slate-900 font-bold">{c.name}</td>
                <td className="p-4 text-slate-500 text-sm">
                  {c.email}<br/>{c.phone}
                </td>
                <td className="p-4 text-slate-500 text-sm">
                  {c.taxRegime === '48' ? 'Resp. IVA' : 'No Resp. IVA'}
                </td>
                <td className="p-4 flex items-center justify-end gap-2">
                  <button onClick={() => handleOpenModal(c)} className="p-2 text-slate-400 hover:text-blue-600 hover:bg-blue-50 rounded-lg transition-colors">
                    <Edit2 size={18} />
                  </button>
                  <button onClick={() => handleDelete(c.id)} className="p-2 text-slate-400 hover:text-rose-600 hover:bg-rose-50 rounded-lg transition-colors">
                    <Trash2 size={18} />
                  </button>
                </td>
              </tr>
            ))}
            {filteredCustomers.length === 0 && (
              <tr>
                <td colSpan={5} className="p-8 text-center text-slate-500">
                  {q ? 'Ningún resultado para tu búsqueda.' : `No tienes ${activeType.toLowerCase()}s registrados.`}
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {isModalOpen && (
        <div className="fixed inset-0 bg-slate-900/50 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-3xl w-full max-w-2xl shadow-2xl overflow-hidden animate-in zoom-in-95 duration-200 max-h-[90vh] flex flex-col">
            <div className="px-6 py-4 border-b border-slate-100 flex justify-between items-center bg-slate-50/50 shrink-0">
              <h3 className="text-xl font-bold text-slate-800">{editingCustomer ? `Editar ${activeType}` : `Nuevo ${activeType}`}</h3>
              <button onClick={() => setIsModalOpen(false)} className="text-slate-400 hover:text-slate-600 p-2"><X size={20} /></button>
            </div>
            <form onSubmit={handleSubmit} className="p-6 overflow-y-auto">
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
              <div className="grid grid-cols-2 gap-4 mb-4">
                {isJuridica ? (
                  <div className="col-span-2">
                    <label className="block text-sm font-bold text-slate-700 mb-1">Razón Social</label>
                    <input type="text" required value={formData.name} onChange={e => setFormData({...formData, name: e.target.value})} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none" />
                  </div>
                ) : (
                  <div className="col-span-2 grid grid-cols-2 gap-4">
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
                    options={IDENTIFICATION_TYPES}
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

                <div className="col-span-2">
                  <label className="block text-sm font-bold text-slate-700 mb-1">Dirección (Google Maps)</label>
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
                  <div className="col-span-2 h-40 rounded-xl overflow-hidden border border-slate-200">
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
                  <label className="block text-sm font-bold text-slate-700 mb-1">Ciudad (auto-completado por Google)</label>
                  <input type="text" value={formData.cityName} onChange={e => setFormData({...formData, cityName: e.target.value})} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none" />
                </div>
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-1">Código Postal</label>
                  <input type="text" value={formData.postalCode} onChange={e => setFormData({...formData, postalCode: e.target.value})} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none font-mono" />
                </div>
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-1">Código DANE (Depto+Municipio)</label>
                  <input type="text" placeholder="Ej. 11001" value={formData.cityCode} onChange={e => setFormData({...formData, cityCode: e.target.value})} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none font-mono" />
                </div>
              </div>

              {formData.partyType === 'Empleado' && (
                <div className="mt-4 pt-4 border-t border-slate-100">
                  <h4 className="text-sm font-bold text-slate-700 mb-3">Datos de Nómina Electrónica</h4>
                  <div className="grid grid-cols-2 gap-4">
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
                      <input type="number" value={formData.baseSalary} onChange={e => setFormData({...formData, baseSalary: parseFloat(e.target.value) || 0})} className="w-full px-4 py-2 border rounded-xl focus:ring-2 focus:ring-primary outline-none font-mono" />
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
                    <div className="flex items-center gap-4 col-span-2">
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

              <div className="mt-6 flex justify-end gap-3 pt-4 border-t border-slate-100">
                <button type="button" onClick={() => setIsModalOpen(false)} className="px-5 py-2 text-slate-600 font-bold hover:bg-slate-100 rounded-xl transition-colors">Cancelar</button>
                <button type="submit" className="px-5 py-2 bg-primary text-white font-bold rounded-xl hover:bg-primary/90 transition-colors shadow-md">Guardar {activeType}</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
