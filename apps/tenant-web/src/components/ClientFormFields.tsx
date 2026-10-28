import React, { useRef } from 'react';
import { GoogleMap, useJsApiLoader, Autocomplete, Marker } from '@react-google-maps/api';
import { FileUp, Loader2, MapPin } from 'lucide-react';
import { toast } from 'sonner';
import { api } from '../lib/api';
import SearchableSelect from '@shared/components/SearchableSelect';

const libraries: "places"[] = ['places'];
const GOOGLE_MAPS_API_KEY = import.meta.env.VITE_GOOGLE_MAPS_API_KEY || "AIzaSy_TU_LLAVE_DE_PRUEBA_AQUI";

// Cómo ve el cliente las retenciones al elegirlas en sus formularios (las de cada línea y las del pie, por separado). Por defecto, el nombre corto.
const RETENTION_LABEL_STYLE_OPTIONS = [
  { value: 'Short', label: 'Nombre corto (RETE FUENTE 2.5%)' },
  { value: 'Full', label: 'Nombre completo del concepto' }
];

const ROUNDING_ADJUSTMENT_OPTIONS = [
  { value: 'None', label: 'Sin ajuste' },
  { value: 'Peso', label: 'Ajuste al peso (quita los centavos)' },
  { value: 'Decena', label: 'Ajuste a la decena' },
  { value: 'Centena', label: 'Ajuste a la centena' }
];

const LEGAL_DOCUMENT_COUNTRIES = [
  ['CO', 'Colombia'], ['AR', 'Argentina'], ['BO', 'Bolivia'], ['BR', 'Brasil'],
  ['CA', 'Canadá'], ['CL', 'Chile'], ['CR', 'Costa Rica'], ['DO', 'República Dominicana'],
  ['EC', 'Ecuador'], ['ES', 'España'], ['GT', 'Guatemala'], ['HN', 'Honduras'],
  ['MX', 'México'], ['NI', 'Nicaragua'], ['PA', 'Panamá'], ['PE', 'Perú'],
  ['PR', 'Puerto Rico'], ['PY', 'Paraguay'], ['SV', 'El Salvador'],
  ['US', 'Estados Unidos'], ['UY', 'Uruguay'], ['VE', 'Venezuela'], ['OTHER', 'Otro país']
] as const;

const LEGAL_DOCUMENT_TYPES = [
  ['CC', 'Cédula de ciudadanía'], ['CE', 'Cédula de extranjería'],
  ['PAS', 'Pasaporte'], ['NIT', 'NIT'], ['OTHER', 'Otro documento']
] as const;

const VIAFIRMA_ORGANIZATION_TYPES = [
  ['RM', 'Registro mercantil'], ['PROP', 'Proponentes'],
  ['RUNEOL', 'Registro único nacional de entidades operadoras de libranza'],
  ['RNT', 'Registro nacional de turismo'], ['ESAL', 'Entidad sin ánimo de lucro'],
  ['ESOL', 'Entidades de economía solidaria'],
  ['JUEGOS', 'Vendedores de juegos de suerte y azar'],
  ['EXTRANJERAS', 'Entidades extranjeras de derecho privado sin ánimo de lucro']
] as const;

interface ClientFormFieldsProps {
  client: any;
  setClient: React.Dispatch<React.SetStateAction<any>>;
  associates: { id: string; name: string; isActive: boolean }[];
  showBillingSection?: boolean;
  showRutUpload?: boolean;
}

// Campos de identidad/ubicación de un Client, compartidos entre el alta (Clients.tsx) y la edición
// ("Info Básica" en ClientEdit.tsx) — antes el alta pedía solo 3 campos y el resto se llenaba
// después en edición; ahora ambos usan exactamente el mismo formulario, con carga de RUT.
export default function ClientFormFields({ client, setClient, associates, showBillingSection = false, showRutUpload = true }: ClientFormFieldsProps) {
  const [uploadingRut, setUploadingRut] = React.useState(false);
  const autocompleteRef = useRef<google.maps.places.Autocomplete | null>(null);
  const { isLoaded } = useJsApiLoader({ id: 'google-map-script', googleMapsApiKey: GOOGLE_MAPS_API_KEY, libraries });

  const onLoadAutocomplete = (autocomplete: google.maps.places.Autocomplete) => {
    autocompleteRef.current = autocomplete;
  };

  const onPlaceChanged = () => {
    const place = autocompleteRef.current?.getPlace();
    if (!place?.geometry?.location) return;
    setClient((prev: any) => ({
      ...prev,
      address: place.formatted_address || prev.address,
      organizationDepartment: place.address_components?.find(component => component.types.includes('administrative_area_level_1'))?.long_name || prev.organizationDepartment,
      latitude: place.geometry!.location!.lat(),
      longitude: place.geometry!.location!.lng()
    }));
  };

  // El RUT imprime la dirección abreviada y sin el "#" ("CR 5 7 24 ED PAOLA BABON OF 503"), que
  // Google Maps no reconoce — hay que expandir la vía y reescribir el número cruzado en el
  // formato colombiano ("Carrera 5 # 7-24") antes de geocodificar.
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
    const m = s.match(/^(Carrera|Calle|Avenida|Diagonal|Transversal)\s+(\d+\s?[A-Za-z]?)\s*#?\s*(\d+)[\s-]+(\d+)/i);
    return m ? `${m[1]} ${m[2].trim()} # ${m[3]}-${m[4]}` : s;
  };

  const geocodeRutAddress = (address?: string, cityName?: string, department?: string) => {
    if (!isLoaded || !address) return;
    const query = [normalizeAddressForGeocoding(address), cityName, department, 'Colombia'].filter(Boolean).join(', ');
    new google.maps.Geocoder().geocode({ address: query }, (results, status) => {
      if (status === 'OK' && results && results[0]?.geometry?.location) {
        const loc = results[0].geometry.location;
        const departmentName = results[0].address_components?.find(component => component.types.includes('administrative_area_level_1'))?.long_name;
        setClient((prev: any) => ({ ...prev, organizationDepartment: departmentName || prev.organizationDepartment, latitude: loc.lat(), longitude: loc.lng() }));
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
      const parseUrl = client.id
        ? `/tenant/clients/parse-rut?clientId=${encodeURIComponent(client.id)}`
        : '/tenant/clients/parse-rut';
      const res = await api.post(parseUrl, uploadData, {
        headers: { 'Content-Type': 'multipart/form-data' }
      });
      const rut = res.data;
      const normalizeTaxId = (value: unknown) => String(value || '').replace(/[^0-9]/g, '');
      const currentTaxId = normalizeTaxId(client.taxId);
      const rutTaxId = normalizeTaxId(rut.taxId);

      // En edición nunca permitimos que un RUT de otra entidad contamine el Client actual.
      // La comparación se hace antes de tocar el estado del formulario.
      if (client.id && currentTaxId && rutTaxId && currentTaxId !== rutTaxId) {
        toast.error(`El RUT pertenece al NIT ${rut.taxId}, pero este cliente tiene el NIT ${client.taxId}. No se modificó ningún dato.`);
        return;
      }

      // El RUT es la fuente de verdad: todo dato que trae con valor reemplaza al del formulario
      // (una empresa puede cambiar de razón social, dirección o representante). Un dato que el
      // RUT trae vacío no borra el que ya hay, y el NIT con su DV no cambian nunca en un cliente
      // existente (ya se comprobó arriba que el RUT es del mismo NIT).
      const isExistingClient = Boolean(client.id);
      const hasValue = (value: unknown) => String(value ?? '').trim() !== '';
      const changed: string[] = [];
      const fromRut = (label: string, rutValue: unknown, existingValue: unknown) => {
        if (!hasValue(rutValue)) return existingValue;
        if (isExistingClient && hasValue(existingValue) && String(rutValue).trim() !== String(existingValue).trim()) changed.push(label);
        return rutValue;
      };
      // Las responsabilidades solo se toman si el RUT las trae (casilla 53 legible): si no, un RUT
      // incompleto dejaría todo en "No somos…".
      const rutHasResponsibilities = rut.hasResponsibilities === true;
      const flagFromRut = (label: string, rutValue: boolean, existingValue: boolean) => {
        if (!rutHasResponsibilities) return existingValue;
        if (isExistingClient && Boolean(rutValue) !== Boolean(existingValue)) changed.push(label);
        return Boolean(rutValue);
      };
      const prev = client;
      setClient({
        ...prev,
        companyName: fromRut('razón social', rut.companyName, prev.companyName),
        personType: fromRut('tipo de persona', rut.personType, prev.personType),
        commercialName: fromRut('nombre comercial', rut.commercialName, prev.commercialName),
        taxId: isExistingClient ? prev.taxId : (rut.taxId || prev.taxId),
        verificationDigit: isExistingClient ? prev.verificationDigit : (rut.verificationDigit || prev.verificationDigit),
        email: fromRut('correo', rut.email, prev.email),
        phone: fromRut('teléfono', rut.phone, prev.phone),
        address: fromRut('dirección', rut.address, prev.address),
        city: fromRut('ciudad', rut.city, prev.city),
        cityCode: fromRut('código DANE', rut.cityCode, prev.cityCode),
        organizationDepartment: fromRut('departamento', rut.department, prev.organizationDepartment),
        legalRepresentativeFirstName: fromRut('representante legal', rut.legalRepresentativeFirstName, prev.legalRepresentativeFirstName),
        legalRepresentativeOtherNames: fromRut('representante legal', rut.legalRepresentativeOtherNames, prev.legalRepresentativeOtherNames),
        legalRepresentativeFirstLastName: fromRut('representante legal', rut.legalRepresentativeFirstLastName, prev.legalRepresentativeFirstLastName),
        legalRepresentativeSecondLastName: fromRut('representante legal', rut.legalRepresentativeSecondLastName, prev.legalRepresentativeSecondLastName),
        legalRepresentativeDocumentType: fromRut('documento del representante', rut.legalRepresentativeDocumentType, prev.legalRepresentativeDocumentType),
        legalRepresentativeDocumentNumber: fromRut('documento del representante', rut.legalRepresentativeDocumentNumber, prev.legalRepresentativeDocumentNumber),
        legalRepresentativeRepresentationCode: fromRut('representación legal', rut.legalRepresentativeRepresentationCode, prev.legalRepresentativeRepresentationCode),
        legalRepresentativeStartDate: fromRut('fecha de inicio del representante', rut.legalRepresentativeStartDate, prev.legalRepresentativeStartDate),
        economicActivity: fromRut('actividad económica', rut.economicActivity, prev.economicActivity),
        taxRegime: rutHasResponsibilities ? fromRut('régimen de IVA', rut.taxRegime, prev.taxRegime) : prev.taxRegime,
        incomeTaxRegime: rutHasResponsibilities ? fromRut('régimen de renta', rut.incomeTaxRegime, prev.incomeTaxRegime) : prev.incomeTaxRegime,
        isGranContribuyente: flagFromRut('Gran Contribuyente', rut.isGranContribuyente, prev.isGranContribuyente),
        isAgenteRetenedorIva: flagFromRut('Agente Retenedor de IVA', rut.isAgenteRetenedorIva, prev.isAgenteRetenedorIva),
        isAutorretenedorRenta: flagFromRut('Autorretenedor de renta', rut.isAutorretenedorRenta, prev.isAutorretenedorRenta)
      });
      if (!isExistingClient) {
        toast.success('Datos del RUT cargados — revísalos antes de guardar.');
      } else if (changed.length > 0) {
        const unique = Array.from(new Set(changed));
        toast.success(`RUT cargado — se actualizó: ${unique.join(', ')}. Revísalo y pulsa Guardar para aplicar los cambios.`);
      } else {
        toast.success('RUT cargado — los datos ya coincidían, no hay cambios.');
      }
      geocodeRutAddress(rut.address, rut.city, rut.department);
    } catch (err: any) {
      toast.error(err.response?.data?.message || err.response?.data || 'No se pudo leer el RUT');
    } finally {
      setUploadingRut(false);
      e.target.value = '';
    }
  };

  return (
    <>
      {showRutUpload && (
        <div className="mb-6 p-4 bg-blue-50 border border-blue-100 rounded-xl flex items-center justify-between gap-3">
          <div>
            <p className="text-sm font-bold text-slate-700">Cargar datos desde el RUT</p>
            <p className="text-xs text-slate-500">Completa algunos datos automáticamente. Revísalos antes de guardar.</p>
          </div>
          <label className={`shrink-0 flex items-center gap-2 px-4 py-2 rounded-xl font-bold text-sm cursor-pointer transition-colors ${uploadingRut ? 'bg-slate-200 text-slate-400' : 'bg-blue-600 text-white hover:bg-blue-700'}`}>
            {uploadingRut ? <Loader2 size={16} className="animate-spin" /> : <FileUp size={16} />}
            {uploadingRut ? 'Leyendo...' : 'Cargar RUT'}
            <input type="file" accept="application/pdf" className="hidden" disabled={uploadingRut} onChange={handleRutUpload} />
          </label>
        </div>
      )}

      <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
        {/* Identidad */}
        <div className="space-y-6">
          <h3 className="text-sm font-bold text-slate-400 uppercase tracking-wider border-b border-slate-100 pb-2">Información tributaria</h3>

          <div>
            <label className="block text-sm font-semibold text-slate-700 mb-2">Razón Social</label>
            <input type="text" required className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none transition-all" value={client.companyName} onChange={e => setClient({ ...client, companyName: e.target.value })} />
          </div>

          <div>
            <label className="block text-sm font-semibold text-slate-700 mb-2">Tipo de persona</label>
            <select required className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none transition-all appearance-none" value={client.personType || 'PJ'} onChange={e => setClient({ ...client, personType: e.target.value })}>
              <option value="PJ">Persona Jurídica</option>
              <option value="PN">Persona Natural</option>
            </select>
            <p className="text-xs text-slate-400 mt-1">Define automáticamente el perfil PN o PJ del certificado.</p>
          </div>

          <div>
            <label className="block text-sm font-semibold text-slate-700 mb-2">Nombre Comercial</label>
            <input type="text" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none transition-all" value={client.commercialName} onChange={e => setClient({ ...client, commercialName: e.target.value })} />
          </div>

          <div className="grid grid-cols-4 gap-4">
            <div className="col-span-3">
              <label className="block text-sm font-semibold text-slate-700 mb-2">NIT</label>
              <input type="text" required className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none transition-all" value={client.taxId} onChange={e => setClient({ ...client, taxId: e.target.value })} />
            </div>
            <div className="col-span-1">
              <label className="block text-sm font-semibold text-slate-700 mb-2">DV</label>
              <input type="text" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none transition-all text-center" value={client.verificationDigit} onChange={e => setClient({ ...client, verificationDigit: e.target.value })} />
            </div>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-semibold text-slate-700 mb-2">Régimen tributario</label>
              <select className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none transition-all appearance-none" value={client.taxRegime} onChange={e => setClient({ ...client, taxRegime: e.target.value })}>
                <option value="">Seleccione...</option>
                <option value="48">Resp. de IVA (48)</option>
                <option value="49">No Resp. de IVA (49)</option>
              </select>
            </div>
            <div>
              <label className="block text-sm font-semibold text-slate-700 mb-2">CIIU</label>
              <input type="text" placeholder="Ej. 6201" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none transition-all" value={client.economicActivity} onChange={e => setClient({ ...client, economicActivity: e.target.value })} />
            </div>
          </div>

          <div>
            <label className="block text-sm font-semibold text-slate-700 mb-2">Régimen de renta</label>
            <select className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none transition-all appearance-none" value={client.incomeTaxRegime || ''} onChange={e => setClient({ ...client, incomeTaxRegime: e.target.value })}>
              <option value="">Sin definir</option>
              <option value="05">Ordinario (05)</option>
              <option value="04">Especial (04)</option>
              <option value="47">Régimen Simple de Tributación (47)</option>
            </select>
          </div>

          <div>
            <label className="block text-sm font-semibold text-slate-700 mb-2">Responsabilidades tributarias</label>
            <div className="space-y-2">
              <label className="flex items-center gap-2 text-sm text-slate-600">
                <input type="checkbox" checked={!!client.isGranContribuyente} onChange={e => setClient({ ...client, isGranContribuyente: e.target.checked })} />
                Gran Contribuyente (13)
              </label>
              <label className="flex items-center gap-2 text-sm text-slate-600">
                <input type="checkbox" checked={!!client.isAgenteRetenedorIva} onChange={e => setClient({ ...client, isAgenteRetenedorIva: e.target.checked })} />
                Agente Retenedor de IVA (09)
              </label>
              <label className="flex items-center gap-2 text-sm text-slate-600">
                <input type="checkbox" checked={!!client.isAutorretenedorRenta} onChange={e => setClient({ ...client, isAutorretenedorRenta: e.target.checked })} />
                Autorretenedor de Renta (15)
              </label>
            </div>
          </div>

          <div>
            <label className="flex items-start gap-3 cursor-pointer">
              <input type="checkbox" checked={!!client.foreignCurrencyEnabled} onChange={e => setClient({ ...client, foreignCurrencyEnabled: e.target.checked })} className="w-5 h-5 mt-0.5 rounded accent-blue-600" />
              <span>
                <span className="font-semibold text-slate-700 text-sm">Factura también en moneda extranjera</span>
                <span className="block text-xs text-slate-400 mt-0.5">Su formulario de factura permite elegir la moneda (USD, EUR...) e indicar la TRM. Apagado, todo sale en pesos.</span>
              </span>
            </label>
          </div>

          <div>
            <label className="block text-sm font-semibold text-slate-700 mb-2">Ajuste de centavos en las facturas</label>
            <SearchableSelect
              options={ROUNDING_ADJUSTMENT_OPTIONS}
              value={client.roundingAdjustment || 'None'}
              onChange={v => setClient({ ...client, roundingAdjustment: v })}
              inputClassName="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none transition-all"
            />
            <p className="text-xs text-slate-400 mt-1">Para clientes que facturan a entidades que no manejan centavos. A cada factura en pesos se le crea un descuento general con lo que sobre del valor a pagar (siempre hacia abajo). No cambia el IVA y se informa en el XML.</p>
          </div>

          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div>
              <label className="block text-sm font-semibold text-slate-700 mb-2">Retenciones de cada línea (cómo se ven)</label>
              <SearchableSelect
                options={RETENTION_LABEL_STYLE_OPTIONS}
                value={client.lineRetentionLabelStyle || 'Short'}
                onChange={v => setClient({ ...client, lineRetentionLabelStyle: v || 'Short' })}
                inputClassName="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none transition-all"
              />
            </div>
            <div>
              <label className="block text-sm font-semibold text-slate-700 mb-2">Retenciones del pie (cómo se ven)</label>
              <SearchableSelect
                options={RETENTION_LABEL_STYLE_OPTIONS}
                value={client.footerRetentionLabelStyle || 'Short'}
                onChange={v => setClient({ ...client, footerRetentionLabelStyle: v || 'Short' })}
                inputClassName="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none transition-all"
              />
            </div>
            <p className="text-xs text-slate-400 md:col-span-2 -mt-2">En los formularios de factura y documento soporte del cliente. El propio cliente también lo puede cambiar en "Valores por defecto"; manda el último cambio. Los nombres cortos los define la administración de la plataforma.</p>
          </div>

          <div>
            <label className="block text-sm font-semibold text-slate-700 mb-2">Formato de números</label>
            <select className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none transition-all appearance-none" value={client.decimalSeparator || '.'} onChange={e => setClient({ ...client, decimalSeparator: e.target.value })}>
              <option value=".">Punto decimal, coma de miles (1,234,567.89)</option>
              <option value=",">Coma decimal, punto de miles (1.234.567,89)</option>
            </select>
            <p className="text-xs text-slate-400 mt-1">Cómo ve y escribe los valores en su portal y cómo salen en los PDF de sus documentos.</p>
          </div>

          <div>
            <label className="block text-sm font-semibold text-slate-700 mb-2">Responsable comercial</label>
            <select className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none transition-all appearance-none" value={client.associateId || ''} onChange={e => setClient({ ...client, associateId: e.target.value })}>
              <option value="">Sin asociado</option>
              {associates.map(a => (
                <option key={a.id} value={a.id}>{a.name}{!a.isActive ? ' (inactivo)' : ''}</option>
              ))}
            </select>
          </div>
        </div>

        {/* Contacto y Ubicación */}
        <div className="space-y-6">
          <h3 className="text-sm font-bold text-slate-400 uppercase tracking-wider border-b border-slate-100 pb-2">Contacto y ubicación</h3>

          <div>
            <label className="block text-sm font-semibold text-slate-700 mb-2">Correo de notificaciones</label>
            <input type="email" required className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none transition-all" value={client.email} onChange={e => setClient({ ...client, email: e.target.value })} />
          </div>

          <div>
            <label className="block text-sm font-semibold text-slate-700 mb-2">Teléfono Celular o Fijo</label>
            <input type="text" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none transition-all" value={client.phone} onChange={e => setClient({ ...client, phone: e.target.value })} />
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
                  onChange={e => setClient({ ...client, address: e.target.value })}
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
              <div className="w-full h-full bg-slate-100 flex flex-col items-center justify-center text-slate-400 font-medium text-sm">
                <MapPin className="w-8 h-8 opacity-50 mb-2" />
                <span>Ubique el negocio en el mapa</span>
              </div>
            )}
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-semibold text-slate-700 mb-2">Municipio / Ciudad</label>
              <input type="text" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none transition-all" value={client.city} onChange={e => setClient({ ...client, city: e.target.value })} />
            </div>
            <div>
              <label className="block text-sm font-semibold text-slate-700 mb-2">Código DANE del municipio</label>
              <input type="text" placeholder="Ej. 11001" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none transition-all font-mono" value={client.cityCode || ''} onChange={e => setClient({ ...client, cityCode: e.target.value })} />
            </div>
          </div>
          <div className="mt-4">
            <label className="block text-sm font-semibold text-slate-700">Departamento de la organización</label>
            <input value={client.organizationDepartment || ''} onChange={e => setClient({ ...client, organizationDepartment: e.target.value })} placeholder="Ej. Valle del Cauca" className="mt-2 w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none transition-all" />
            <p className="text-xs text-slate-400 mt-1">Se completa desde el RUT o Google Maps cuando la dirección es geocodificada.</p>
          </div>
          <p className="text-xs text-amber-600 -mt-4">Este código es necesario para emitir directamente a la DIAN.</p>
        </div>
      </div>

      <div className="mt-8 pt-6 border-t border-slate-100 space-y-5">
        <div>
          <h3 className="text-sm font-bold text-slate-400 uppercase tracking-wider">Representante legal</h3>
          <p className="text-xs text-slate-500 mt-1">Opcional. Si el RUT incluye la hoja de representación, estos datos se cargan automáticamente; si no, puedes completarlos aquí.</p>
        </div>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4 items-start">
          <label className="block text-sm font-semibold text-slate-700">Primer nombre<input value={client.legalRepresentativeFirstName || ''} onChange={e => setClient({ ...client, legalRepresentativeFirstName: e.target.value })} className="mt-2 w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl font-normal" /></label>
          <label className="block text-sm font-semibold text-slate-700">Otros nombres<input value={client.legalRepresentativeOtherNames || ''} onChange={e => setClient({ ...client, legalRepresentativeOtherNames: e.target.value })} className="mt-2 w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl font-normal" /></label>
          <label className="block text-sm font-semibold text-slate-700">Primer apellido<input value={client.legalRepresentativeFirstLastName || ''} onChange={e => setClient({ ...client, legalRepresentativeFirstLastName: e.target.value })} className="mt-2 w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl font-normal" /></label>
          <label className="block text-sm font-semibold text-slate-700">Segundo apellido<input value={client.legalRepresentativeSecondLastName || ''} onChange={e => setClient({ ...client, legalRepresentativeSecondLastName: e.target.value })} className="mt-2 w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl font-normal" /></label>
          <label className="block text-sm font-semibold text-slate-700">Tipo de documento<select aria-label="Tipo de documento del representante" value={client.legalRepresentativeDocumentType === 'IDC' ? 'CC' : (client.legalRepresentativeDocumentType || '')} onChange={e => setClient({ ...client, legalRepresentativeDocumentType: e.target.value })} className="mt-2 w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl font-normal"><option value="">Selecciona un tipo</option>{LEGAL_DOCUMENT_TYPES.map(([code, name]) => <option key={code} value={code}>{name}</option>)}</select></label>
          <label className="block text-sm font-semibold text-slate-700">Número de documento<input value={client.legalRepresentativeDocumentNumber || ''} onChange={e => setClient({ ...client, legalRepresentativeDocumentNumber: e.target.value })} className="mt-2 w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl font-normal" /></label>
          <label className="block text-sm font-semibold text-slate-700">País de expedición<select aria-label="País donde se expidió el documento" value={client.legalRepresentativeDocumentCountryCode || 'CO'} onChange={e => setClient({ ...client, legalRepresentativeDocumentCountryCode: e.target.value })} className="mt-2 w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl font-normal"><option value="">Selecciona un país</option>{LEGAL_DOCUMENT_COUNTRIES.map(([code, name]) => <option key={code} value={code}>{name}</option>)}</select></label>
          <label className="block text-sm font-semibold text-slate-700">Correo del representante<input type="email" value={client.legalRepresentativeEmail || ''} onChange={e => setClient({ ...client, legalRepresentativeEmail: e.target.value })} className="mt-2 w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl font-normal" /></label>
          <label className="block text-sm font-semibold text-slate-700">Código de representación (casilla 98)<input value={client.legalRepresentativeRepresentationCode || ''} onChange={e => setClient({ ...client, legalRepresentativeRepresentationCode: e.target.value })} className="mt-2 w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl font-normal" /></label>
          <label className="block text-sm font-semibold text-slate-700">Departamento<input value={client.legalRepresentativeOrganizationalArea || ''} onChange={e => setClient({ ...client, legalRepresentativeOrganizationalArea: e.target.value })} placeholder="Ej. Gerencia General" className="mt-2 w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl font-normal" /></label>
          <label className="block text-sm font-semibold text-slate-700">Tipo de organización<select value={client.organizationType || 'RM'} onChange={e => setClient({ ...client, organizationType: e.target.value })} className="mt-2 w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl font-normal"><option value="">Selecciona un tipo</option>{VIAFIRMA_ORGANIZATION_TYPES.map(([code, name]) => <option key={code} value={code}>{name}</option>)}</select></label>
          <label className="text-sm text-slate-600">
            <span className="block mb-1 font-semibold">Fecha de inicio como representante legal</span>
            <input type="date" title="Fecha de inicio como representante legal (casilla 99 del RUT)" value={client.legalRepresentativeStartDate ? String(client.legalRepresentativeStartDate).slice(0, 10) : ''} onChange={e => setClient({ ...client, legalRepresentativeStartDate: e.target.value || null })} className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl" />
            <span className="block mt-1 text-xs text-slate-400">Fecha de nombramiento registrada en el RUT, no la fecha de creación del cliente.</span>
          </label>
        </div>
      </div>

      {showBillingSection && (
        <div className="pt-6 border-t border-slate-100">
          <h3 className="text-sm font-bold text-slate-400 uppercase tracking-wider mb-4">Tarifa para este cliente</h3>
          <div className="grid grid-cols-2 gap-4 max-w-md">
            <div>
              <label className="block text-sm font-semibold text-slate-700 mb-2">Frecuencia</label>
              <select className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none transition-all appearance-none" value={client.billingFrequency} onChange={e => setClient({ ...client, billingFrequency: e.target.value })}>
                <option value="Monthly">Mensual</option>
                <option value="Annual">Anual</option>
              </select>
            </div>
          </div>
          <p className="text-xs text-slate-500 mt-2">La cuota mensual y el precio por documento se fijan por sucursal, en la pestaña Sucursales.</p>
        </div>
      )}
    </>
  );
}
