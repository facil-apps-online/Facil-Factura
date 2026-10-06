import React, { useState, useEffect } from 'react';
import { toast } from 'sonner';
import { Palette, Image as ImageIcon, Save, CheckCircle2, XCircle, Loader2, Copy } from 'lucide-react';
import SmtpSettingsCard from '@shared/components/SmtpSettingsCard';
import { api } from '../lib/api';

export default function Branding() {
  const [formData, setFormData] = useState({
    slug: '',
    primaryColorLight: '#3b82f6',
    logoLightUrl: '',
    showUsageToClients: false
  });
  const [originalSlug, setOriginalSlug] = useState('');
  const [loading, setLoading] = useState(true);
  const [slugStatus, setSlugStatus] = useState<'idle' | 'checking' | 'available' | 'unavailable'>('idle');
  const [uploadingLogo, setUploadingLogo] = useState(false);
  const [billingMode, setBillingMode] = useState('PerDocument');
  const [bags, setBags] = useState<any[]>([]);
  const [packageCatalog, setPackageCatalog] = useState<any[]>([]);
  const [selectedPackageId, setSelectedPackageId] = useState('');
  const [purchasingBag, setPurchasingBag] = useState(false);

  const loadPrepaidInfo = () => {
    api.get('/tenant/prepaid/bags').then(res => setBags(res.data)).catch(() => {});
    api.get('/tenant/prepaid/packages').then(res => setPackageCatalog(res.data)).catch(() => {});
  };

  const handlePurchaseBag = async () => {
    if (!selectedPackageId) {
      toast.error('Selecciona un paquete.');
      return;
    }
    setPurchasingBag(true);
    try {
      await api.post('/tenant/prepaid/bags', { packageId: selectedPackageId });
      toast.success('Bolsa activada correctamente.');
      setSelectedPackageId('');
      loadPrepaidInfo();
    } catch (err: any) {
      toast.error(err.response?.data || 'Error activando la bolsa.');
    } finally {
      setPurchasingBag(false);
    }
  };

  useEffect(() => {
    api.get('/tenant/branding/my-branding')
      .then(res => {
        setFormData({
          slug: res.data.slug || '',
          primaryColorLight: res.data.primaryColorLight || '#3b82f6',
          logoLightUrl: res.data.logoLightUrl || '',
          showUsageToClients: res.data.showUsageToClients || false
        });
        setOriginalSlug(res.data.slug || '');
        setBillingMode(res.data.billingMode || 'PerDocument');
        if (res.data.billingMode === 'PerUser') loadPrepaidInfo();
      })
      .catch(() => toast.error('No se pudo cargar la configuración de apariencia'))
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    if (!formData.slug) {
      setSlugStatus('idle');
      return;
    }

    // Si el slug es igual al original del tenant, está disponible para él
    if (formData.slug === originalSlug) {
      setSlugStatus('available');
      return;
    }

    setSlugStatus('checking');
    const timer = setTimeout(async () => {
      try {
        const res = await api.get(`/tenant/branding/check-slug?slug=${formData.slug}`);
        setSlugStatus(res.data.isAvailable ? 'available' : 'unavailable');
      } catch {
        setSlugStatus('idle');
      }
    }, 600);

    return () => clearTimeout(timer);
  }, [formData.slug, originalSlug]);

  const handleColorChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setFormData({ ...formData, primaryColorLight: e.target.value });
  };

  const handleLogoUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setUploadingLogo(true);
    try {
      const uploadData = new FormData();
      uploadData.append('file', file);
      const res = await api.post('/tenant/branding/logo', uploadData, {
        headers: { 'Content-Type': 'multipart/form-data' }
      });
      setFormData(prev => ({ ...prev, logoLightUrl: res.data.logoLightUrl }));
      toast.success('Logo actualizado correctamente');
    } catch (err: any) {
      toast.error(err.response?.data || 'Error al subir el logo');
    } finally {
      setUploadingLogo(false);
      e.target.value = '';
    }
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await api.put('/tenant/branding/my-branding', formData);
      toast.success('Identidad y Branding actualizados exitosamente');
      setOriginalSlug(formData.slug);
    } catch (error: any) {
      toast.error(error.response?.data || 'Error guardando la configuración');
    }
  };

  if (loading) {
    return <div className="p-10 text-slate-500 animate-pulse">Cargando apariencia...</div>;
  }

  return (
    <div className="p-10 animate-in fade-in slide-in-from-bottom-4 duration-500">
      <div className="mb-10">
        <h1 className="text-3xl font-extrabold text-slate-800 tracking-tight flex items-center gap-3">
          <Palette className="w-8 h-8 text-primary" />
          Apariencia y Branding
        </h1>
        <p className="text-slate-500 mt-2 text-lg">Personaliza el logo y los colores que verán tus clientes.</p>
      </div>

      <div className="bg-white rounded-3xl shadow-sm border border-slate-200 overflow-hidden">
        <form onSubmit={handleSave} className="p-8 space-y-8">
          
          {/* Identidad / Slug */}
          <div className="space-y-4 pb-8 border-b border-slate-100">
            <label className="block text-sm font-bold text-slate-700">Identificador del sitio</label>
            <div className="flex items-center w-full max-w-2xl shadow-sm rounded-xl">
              <span className="px-4 py-3.5 bg-slate-100 border border-slate-200 border-r-0 rounded-l-xl text-slate-500 font-mono text-sm shrink-0">
                https://clients.facil-factura.pro/
              </span>
              <div className="relative flex-1">
                <input 
                  type="text" 
                  value={formData.slug} 
                  onChange={(e) => setFormData({...formData, slug: e.target.value.toLowerCase().replace(/[^a-z0-9-]/g, '')})}
                  placeholder="mi-empresa"
                  className="w-full pl-4 pr-12 py-3.5 bg-slate-50 border border-slate-200 focus:ring-2 focus:ring-primary focus:border-primary focus:z-10 relative font-mono text-slate-800 outline-none transition-all" 
                />
                <div className="absolute right-3 top-3.5 z-20">
                  {slugStatus === 'checking' && <Loader2 className="w-5 h-5 text-blue-500 animate-spin" />}
                  {slugStatus === 'available' && <CheckCircle2 className="w-5 h-5 text-emerald-500" />}
                  {slugStatus === 'unavailable' && <XCircle className="w-5 h-5 text-rose-500" />}
                </div>
              </div>
              <button 
                type="button"
                onClick={() => {
                  navigator.clipboard.writeText(`https://clients.facil-factura.pro/${formData.slug}`);
                  toast.success('Enlace copiado al portapapeles');
                }}
                disabled={!formData.slug}
                className="px-5 py-3.5 bg-white border border-slate-200 border-l-0 rounded-r-xl text-slate-500 hover:text-blue-600 hover:bg-blue-50 transition-colors shrink-0 focus:z-10 relative disabled:opacity-50 disabled:hover:bg-white disabled:hover:text-slate-500"
                title="Copiar enlace"
              >
                <Copy size={18} />
              </button>
            </div>
            {slugStatus === 'unavailable' ? (
              <p className="text-sm text-rose-500 font-medium">❌ Este identificador ya está en uso por otra empresa. Por favor elige otro.</p>
            ) : (
              <p className="text-xs text-slate-500">Será el enlace público para tus clientes. Usa minúsculas y guiones.</p>
            )}
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
            <div className="space-y-4">
              <label className="block text-sm font-bold text-slate-700">Color Primario de tu Marca</label>
              <div className="flex items-center gap-4">
                <input 
                  type="color" 
                  value={formData.primaryColorLight} 
                  onChange={handleColorChange}
                  className="w-16 h-16 rounded cursor-pointer border-0 p-0"
                />
                <input
                  type="text"
                  value={formData.primaryColorLight}
                  onChange={handleColorChange}
                  className="px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-primary focus:border-primary font-mono"
                />
                <button
                  type="button"
                  className="px-5 py-3 rounded-xl font-bold text-white shadow-sm"
                  style={{ backgroundColor: formData.primaryColorLight }}
                >
                  Vista previa
                </button>
              </div>
              <p className="text-xs text-slate-500">Este color se usará en el portal de tus clientes.</p>
            </div>

            <div className="space-y-4">
              <label className="block text-sm font-bold text-slate-700">Logotipo</label>
              <div className="relative">
                <ImageIcon className="absolute left-3 top-3.5 w-5 h-5 text-slate-400 pointer-events-none" />
                <input
                  type="file"
                  accept="image/png,image/jpeg,image/webp,image/svg+xml"
                  disabled={uploadingLogo}
                  onChange={handleLogoUpload}
                  className="w-full pl-10 pr-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-primary focus:border-primary file:mr-4 file:py-1.5 file:px-3 file:rounded-lg file:border-0 file:bg-primary file:text-white file:font-semibold disabled:opacity-50"
                />
              </div>
              {uploadingLogo && <p className="text-xs text-slate-500">Subiendo...</p>}
              {formData.logoLightUrl && (
                <div className="mt-4 p-4 border border-slate-100 rounded-xl bg-slate-50 flex items-center justify-center h-32">
                  <img src={formData.logoLightUrl} alt="Logo Preview" className="max-h-full max-w-full object-contain" />
                </div>
              )}
            </div>
          </div>

          <div className="pt-6 border-t border-slate-100">
            <label className="flex items-center gap-3 cursor-pointer max-w-md">
              <input
                type="checkbox"
                checked={formData.showUsageToClients}
                onChange={e => setFormData({ ...formData, showUsageToClients: e.target.checked })}
                className="w-5 h-5 rounded border-slate-300 text-primary focus:ring-primary"
              />
              <span>
                <span className="block text-sm font-bold text-slate-700">Mostrar consumo a mis clientes</span>
                <span className="block text-xs text-slate-500">Define si tus clientes pueden consultar su consumo y facturación.</span>
              </span>
            </label>
          </div>

          <div className="pt-6 border-t border-slate-100 flex justify-end">
            <button 
              type="submit" 
              className="bg-primary hover:bg-primary-hover text-white px-8 py-3 rounded-xl font-bold transition-all shadow-primary flex items-center gap-2"
            >
              <Save className="w-5 h-5" />
              Guardar Apariencia
            </button>
          </div>
        </form>
      </div>

      <SmtpSettingsCard
        api={api}
        basePath="/tenant/smtp-settings"
        className="bg-white rounded-3xl shadow-sm border border-slate-200 overflow-hidden mt-8 p-8"
        description="SMTP por defecto para tus clientes que emiten directo a la DIAN (sin Dataico) y no configuraron su propio SMTP."
      />

      {billingMode === 'PerUser' && (
        <div className="bg-white rounded-3xl shadow-sm border border-slate-200 overflow-hidden mt-8 p-8">
          <h2 className="text-xl font-bold text-slate-800 mb-1">Bolsa Prepago</h2>
          <p className="text-slate-500 text-sm mb-6">
            Compra por adelantado una bolsa de dinero. Mientras tenga saldo, tu tarifa por usuario baja a la tarifa con descuento del paquete; el saldo se descuenta cada mes según el uso real de tus clientes. Al agotarse, vuelve a aplicarse la tarifa estándar.
          </p>

          <div className="space-y-2 mb-6">
            {bags.map(bag => (
              <div key={bag.id} className="flex items-center justify-between p-4 bg-slate-50 border border-slate-200 rounded-xl">
                <div>
                  <p className="font-bold text-slate-800">{bag.packageName}</p>
                  <p className="text-sm text-slate-500">Saldo: ${bag.remainingBalance.toLocaleString('es-CO')} de ${bag.amountPaid.toLocaleString('es-CO')} · tarifa con descuento ${bag.discountedPricePerUser.toLocaleString('es-CO')}/usuario</p>
                </div>
                <span className={`text-xs font-bold px-3 py-1 rounded-full ${bag.status === 'Active' ? 'bg-emerald-100 text-emerald-700' : 'bg-slate-200 text-slate-500'}`}>
                  {bag.status === 'Active' ? 'Activa' : bag.status === 'Depleted' ? 'Agotada' : 'Cancelada'}
                </span>
              </div>
            ))}
            {bags.length === 0 && <p className="text-sm text-slate-400">Aún no tienes paquetes prepago.</p>}
          </div>

          <div className="flex gap-2 items-end max-w-lg">
            <div className="flex-1">
              <label className="block text-xs font-semibold text-slate-500 mb-1">Comprar nueva bolsa</label>
              <select className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-primary outline-none bg-white" value={selectedPackageId} onChange={e => setSelectedPackageId(e.target.value)}>
                <option value="">Seleccionar paquete...</option>
                {packageCatalog.map(pkg => (
                  <option key={pkg.id} value={pkg.id}>{pkg.name} (${pkg.discountedPricePerUser.toLocaleString('es-CO')}/usuario · ${pkg.totalPrice.toLocaleString('es-CO')} bolsa)</option>
                ))}
              </select>
            </div>
            <button type="button" onClick={handlePurchaseBag} disabled={purchasingBag} className="px-5 py-2.5 bg-primary hover:bg-primary-hover text-white rounded-xl font-semibold disabled:opacity-50">
              {purchasingBag ? 'Activando...' : 'Activar'}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
