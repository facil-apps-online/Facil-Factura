import React from 'react';
import { FileSignature, Plus, Hash } from 'lucide-react';

export default function Resolutions() {
  const [resolutions, setResolutions] = React.useState<any[]>([]);

  return (
    <div className="p-10 h-full overflow-y-auto animate-in fade-in slide-in-from-bottom-4 duration-500">
      <div className="flex justify-between items-center mb-8">
        <div>
          <h1 className="text-3xl font-extrabold text-slate-800 tracking-tight">Mis Resoluciones DIAN</h1>
          <p className="text-slate-500 mt-2 text-sm max-w-2xl">
            <strong className="text-slate-600">Opcional:</strong> Si deseas automatizar el cobro a tus clientes emisores y facturar directamente desde la plataforma bajo tu propia Razón Social, registra aquí tus prefijos, numeración y vigencia otorgada por la DIAN.
          </p>
        </div>
        <button className="bg-gradient-to-r from-blue-600 to-indigo-600 hover:from-blue-500 hover:to-indigo-500 text-white px-5 py-2.5 rounded-xl font-medium shadow-lg shadow-blue-500/30 flex items-center gap-2 transition-all hover:-translate-y-0.5">
          <Plus size={20} />
          <span>Registrar Resolución</span>
        </button>
      </div>

      {resolutions.length === 0 ? (
        <div className="mt-12 bg-white/60 backdrop-blur-md border border-slate-200/60 rounded-3xl p-12 text-center flex flex-col items-center justify-center max-w-3xl mx-auto shadow-sm">
          <div className="w-20 h-20 bg-blue-50 text-blue-500 rounded-full flex items-center justify-center mb-6 shadow-inner">
            <FileSignature size={40} className="opacity-80" />
          </div>
          <h3 className="text-xl font-bold text-slate-800 mb-2">Aún no facturas desde la plataforma</h3>
          <p className="text-slate-500 text-sm max-w-md mb-8">
            Para habilitar la generación de tus propias facturas de cobro a través del portal, debes registrar tu primera resolución DIAN y el ambiente de habilitación.
          </p>
          <button className="bg-white border-2 border-slate-200 text-slate-700 hover:border-blue-500 hover:text-blue-600 px-6 py-2.5 rounded-xl font-semibold transition-all">
            Ver guía de configuración
          </button>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-8 mt-8">
          {/* Aquí irá el map de las resoluciones cuando se conecte al API */}
        </div>
      )}
    </div>
  );
}
