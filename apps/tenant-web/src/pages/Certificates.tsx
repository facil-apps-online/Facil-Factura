import React from 'react';
import { Upload, Lock, ShieldCheck, AlertTriangle } from 'lucide-react';

export default function Certificates() {
  return (
    <div className="p-8 max-w-5xl mx-auto">
      <div className="mb-8">
        <h1 className="text-3xl font-bold text-slate-800">Certificados Digitales (.p12)</h1>
        <p className="text-slate-500 mt-2">Sube y gestiona los certificados de firma electrónica (XAdES-EPES) de tus clientes emisores.</p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
        
        {/* Upload Form */}
        <div className="lg:col-span-1 bg-white rounded-2xl shadow-sm border border-slate-200 p-6">
          <h2 className="text-lg font-bold text-slate-800 flex items-center gap-2 mb-6">
            <Upload size={20} className="text-blue-500" />
            Subir Certificado
          </h2>
          
          <form className="space-y-4">
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1">Cliente Emisor</label>
              <select className="w-full px-4 py-2.5 rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-blue-500/20 focus:border-blue-500 text-sm bg-slate-50">
                <option>Seleccione un cliente...</option>
                <option>Glamtica S.A.S</option>
                <option>Tattoo Suite</option>
              </select>
            </div>

            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1">Archivo .p12 o .pfx</label>
              <div className="border-2 border-dashed border-slate-300 rounded-xl p-6 flex flex-col items-center justify-center text-center hover:bg-slate-50 transition-colors cursor-pointer group">
                <div className="w-12 h-12 bg-blue-50 text-blue-500 rounded-full flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                  <Upload size={24} />
                </div>
                <p className="text-sm font-medium text-slate-700">Haz clic o arrastra el archivo aquí</p>
                <p className="text-xs text-slate-500 mt-1">Máximo 5MB</p>
              </div>
            </div>

            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1">Contraseña del Certificado</label>
              <div className="relative">
                <Lock className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" size={18} />
                <input 
                  type="password" 
                  placeholder="••••••••" 
                  className="w-full pl-10 pr-4 py-2.5 rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-blue-500/20 focus:border-blue-500 text-sm"
                />
              </div>
              <p className="text-xs text-slate-500 mt-2 flex items-start gap-1">
                <ShieldCheck size={14} className="text-emerald-500 shrink-0" />
                La contraseña será encriptada en la Bóveda Criptográfica antes de guardarse.
              </p>
            </div>

            <button type="button" className="w-full bg-blue-600 hover:bg-blue-700 text-white py-2.5 rounded-xl font-medium shadow-md shadow-blue-500/20 transition-colors mt-2">
              Guardar Certificado
            </button>
          </form>
        </div>

        {/* Certificates List */}
        <div className="lg:col-span-2 space-y-4">
          <h2 className="text-lg font-bold text-slate-800 mb-4">Certificados Activos</h2>
          
          <div className="bg-white p-5 rounded-2xl shadow-sm border border-slate-200 flex items-center justify-between hover:border-blue-200 transition-colors">
            <div className="flex items-center gap-4">
              <div className="w-12 h-12 bg-emerald-50 text-emerald-600 rounded-full flex items-center justify-center">
                <ShieldCheck size={24} />
              </div>
              <div>
                <h3 className="font-bold text-slate-800">Glamtica S.A.S</h3>
                <p className="text-sm text-slate-500">Expira: 15 de Noviembre, 2025</p>
              </div>
            </div>
            <div className="text-right">
              <span className="inline-flex px-3 py-1 rounded-full text-xs font-semibold text-emerald-700 bg-emerald-100">Válido</span>
            </div>
          </div>

          <div className="bg-white p-5 rounded-2xl shadow-sm border border-rose-200 flex items-center justify-between">
            <div className="flex items-center gap-4">
              <div className="w-12 h-12 bg-rose-50 text-rose-600 rounded-full flex items-center justify-center">
                <AlertTriangle size={24} />
              </div>
              <div>
                <h3 className="font-bold text-slate-800">Tattoo Suite</h3>
                <p className="text-sm text-rose-500 font-medium">Expirado hace 2 días</p>
              </div>
            </div>
            <div className="text-right">
              <span className="inline-flex px-3 py-1 rounded-full text-xs font-semibold text-rose-700 bg-rose-100">Expirado</span>
              <button className="block text-xs text-blue-600 font-medium mt-2 hover:underline">Actualizar</button>
            </div>
          </div>

        </div>
      </div>
    </div>
  );
}
