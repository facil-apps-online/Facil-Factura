import React, { useState, useEffect } from 'react';
import { toast } from 'sonner';
import { Palette, Image as ImageIcon, Save, CheckCircle2, XCircle, Loader2, Copy } from 'lucide-react';
import { api } from '../lib/api';

export default function Branding({ tenant, setTenant }: { tenant: any, setTenant: any }) {
  const [formData, setFormData] = useState({
    slug: '',
    primaryColorLight: '#3b82f6',
    logoLightUrl: ''
  });
  const [slugStatus, setSlugStatus] = useState<'idle' | 'checking' | 'available' | 'unavailable'>('idle');

  useEffect(() => {
    if (tenant) {
      setFormData({
        slug: tenant.slug || '',
        primaryColorLight: tenant.primaryColorLight || '#3b82f6',
        logoLightUrl: tenant.logoLightUrl || ''
      });
    }
  }, [tenant]);

  useEffect(() => {
    if (!formData.slug) {
      setSlugStatus('idle');
      return;
    }
    
    // Si el slug es igual al original del tenant, está disponible para él
    if (tenant && formData.slug === tenant.slug) {
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
  }, [formData.slug, tenant]);

  const hexToRgb = (hex: string) => {
    const result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
    return result ? `${parseInt(result[1], 16)} ${parseInt(result[2], 16)} ${parseInt(result[3], 16)}` : '59 130 246';
  };

  const handleColorChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const newColor = e.target.value;
    setFormData({ ...formData, primaryColorLight: newColor });
    document.documentElement.style.setProperty('--color-primary', hexToRgb(newColor));
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await api.put('/tenant/branding/my-branding', formData);
      toast.success('Identidad y Branding actualizados exitosamente');
      setTenant({ ...tenant, ...formData });
    } catch (error: any) {
      toast.error(error.response?.data || 'Error guardando la configuración');
    }
  };

  return (
    <div className="p-10 max-w-4xl mx-auto animate-in fade-in slide-in-from-bottom-4 duration-500">
      <div className="mb-10">
        <h1 className="text-3xl font-extrabold text-slate-800 tracking-tight flex items-center gap-3">
          <Palette className="w-8 h-8 text-primary" />
          Apariencia y Branding
        </h1>
        <p className="text-slate-500 mt-2 text-lg">Personaliza los colores y el logotipo para la vista de tus clientes.</p>
      </div>

      <div className="bg-white rounded-3xl shadow-sm border border-slate-200 overflow-hidden">
        <form onSubmit={handleSave} className="p-8 space-y-8">
          
          {/* Identidad / Slug */}
          <div className="space-y-4 pb-8 border-b border-slate-100">
            <label className="block text-sm font-bold text-slate-700">Identificador del Micrositio (Slug)</label>
            <div className="flex items-center w-full max-w-2xl shadow-sm rounded-xl">
              <span className="px-4 py-3.5 bg-slate-100 border border-slate-200 border-r-0 rounded-l-xl text-slate-500 font-mono text-sm shrink-0">
                https://facil-factura.pro/
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
                  navigator.clipboard.writeText(`https://facil-factura.pro/${formData.slug}`);
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
              <p className="text-xs text-slate-500">Este será el enlace público donde tus clientes ingresarán. Solo usa minúsculas y guiones.</p>
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
              </div>
              <p className="text-xs text-slate-500">Este color dominará los botones y el menú de tu portal y el de tus clientes (Nivel 3).</p>
            </div>

            <div className="space-y-4">
              <label className="block text-sm font-bold text-slate-700">URL del Logotipo</label>
              <div className="relative">
                <ImageIcon className="absolute left-3 top-3.5 w-5 h-5 text-slate-400" />
                <input 
                  type="text" 
                  placeholder="https://midominio.com/logo.png"
                  value={formData.logoLightUrl}
                  onChange={e => setFormData({...formData, logoLightUrl: e.target.value})}
                  className="w-full pl-10 pr-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-primary focus:border-primary" 
                />
              </div>
              {formData.logoLightUrl && (
                <div className="mt-4 p-4 border border-slate-100 rounded-xl bg-slate-50 flex items-center justify-center h-32">
                  <img src={formData.logoLightUrl} alt="Logo Preview" className="max-h-full max-w-full object-contain" />
                </div>
              )}
            </div>
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
    </div>
  );
}
