import React, { useEffect, useState } from 'react';
import { Copy, Check, KeyRound, Building2, LayoutDashboard, Code2, Stethoscope, Globe } from 'lucide-react';
import { toast } from 'sonner';
import { api } from '../lib/api';
import UserMenu, { useAccountMe } from '@shared/components/session/UserMenu';

// Catálogo de países soportados y qué documentación aplica a cada uno — hoy solo Colombia.
// Al sumar un país nuevo (ej. México/SAT), se agrega aquí y sus propias pestañas, sin tocar
// las de Colombia (ver /api/{country}/{authority}/... en el backend, ej. /api/co/dian/invoices).
const COUNTRIES = [{ code: 'co', name: 'Colombia' }] as const;
type CountryCode = typeof COUNTRIES[number]['code'];

const DOC_SECTIONS: Record<CountryCode, { key: 'docs' | 'docsV1'; label: string; icon: typeof Code2 }[]> = {
  co: [
    { key: 'docs', label: 'DIAN', icon: Code2 },
    { key: 'docsV1', label: 'MinSalud', icon: Stethoscope },
  ],
};

interface DeveloperMe {
  id: string;
  name: string;
  email: string;
  tenant: { id: string; commercialName: string } | null;
  sandboxCredentials: {
    id: string;
    companyName: string;
    testApiKey: string;
    testApiSecret: string;
    dataicoEnvironment: string;
  } | null;
}

function CopyField({ label, value }: { label: string; value: string }) {
  const [copied, setCopied] = useState(false);

  const handleCopy = async () => {
    await navigator.clipboard.writeText(value);
    setCopied(true);
    toast.success(`${label} copiado`);
    setTimeout(() => setCopied(false), 1500);
  };

  return (
    <div>
      <label className="block text-xs font-bold text-slate-500 mb-1">{label}</label>
      <div className="flex items-center gap-2">
        <code className="flex-1 bg-slate-50 border border-slate-200 rounded-lg px-3 py-2.5 text-sm text-slate-700 font-mono truncate">
          {value}
        </code>
        <button
          onClick={handleCopy}
          title={`Copiar ${label}`}
          className="p-2.5 text-slate-400 hover:text-primary hover:bg-slate-100 rounded-lg transition-colors border border-slate-200"
        >
          {copied ? <Check size={16} className="text-emerald-600" /> : <Copy size={16} />}
        </button>
      </div>
    </div>
  );
}

export default function Dashboard({ onLogout, onProfile }: { onLogout: () => void; onProfile: () => void }) {
  const { me: account } = useAccountMe(api, '/developer/auth');
  const [me, setMe] = useState<DeveloperMe | null>(null);
  const [loading, setLoading] = useState(true);
  const [tab, setTab] = useState<'overview' | 'docs' | 'docsV1'>('overview');
  const [country, setCountry] = useState<CountryCode>('co');
  const sections = DOC_SECTIONS[country];

  useEffect(() => {
    api.get('/developer/me')
      .then(res => setMe(res.data))
      .catch(() => toast.error('No se pudo cargar tu perfil'))
      .finally(() => setLoading(false));
  }, []);

  return (
    <div className="h-screen flex flex-col bg-slate-50">
      <header className="bg-white border-b border-slate-200 shrink-0">
        <div className="px-6 py-4 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <img src="/brand/isotipo-color.png" alt="Facil Factura" className="w-8 h-8 object-contain" />
            <span className="font-bold text-slate-800">Portal de Developers</span>
          </div>
          <UserMenu me={account} fallbackName={localStorage.getItem('fel_developer_name') || undefined} onProfile={onProfile} onLogout={onLogout} />
        </div>
        <div className="px-6 flex items-center justify-between">
          <div className="flex gap-1">
            <button
              onClick={() => setTab('overview')}
              className={`flex items-center gap-2 px-4 py-2.5 text-sm font-semibold border-b-2 transition-colors ${tab === 'overview' ? 'border-primary text-primary' : 'border-transparent text-slate-500 hover:text-slate-700'}`}
            >
              <LayoutDashboard size={16} /> Resumen
            </button>
            {sections.map(s => (
              <button
                key={s.key}
                onClick={() => setTab(s.key)}
                className={`flex items-center gap-2 px-4 py-2.5 text-sm font-semibold border-b-2 transition-colors ${tab === s.key ? 'border-primary text-primary' : 'border-transparent text-slate-500 hover:text-slate-700'}`}
              >
                <s.icon size={16} /> {s.label}
              </button>
            ))}
          </div>
          <div className="flex items-center gap-2 pb-2">
            <Globe size={14} className="text-slate-400" />
            <select
              value={country}
              onChange={e => { setCountry(e.target.value as CountryCode); setTab('overview'); }}
              className="text-sm font-medium text-slate-600 bg-transparent outline-none cursor-pointer"
            >
              {COUNTRIES.map(c => <option key={c.code} value={c.code}>{c.name}</option>)}
            </select>
          </div>
        </div>
      </header>

      {tab === 'docs' ? (
        <iframe
          src="https://api.facil-factura.pro/swagger"
          title="Swagger API Facil Factura"
          className="flex-1 w-full border-0"
        />
      ) : tab === 'docsV1' ? (
        <iframe
          src="https://api.facil-factura.pro/swagger-v1"
          title="Swagger API Facil Factura v1 (documentos independientes)"
          className="flex-1 w-full border-0"
        />
      ) : (
        <main className="flex-1 overflow-y-auto">
          <div className="max-w-4xl mx-auto px-6 py-10 space-y-8">
            {loading ? (
              <p className="text-slate-400">Cargando...</p>
            ) : !me ? (
              <p className="text-rose-600">No se pudo cargar tu perfil. Vuelve a iniciar sesión.</p>
            ) : (
              <>
                <div>
                  <h1 className="text-3xl font-bold text-slate-800">Hola, {me.name}</h1>
                  <p className="text-slate-500 mt-1">{me.email}</p>
                  {me.tenant && (
                    <div className="mt-3 inline-flex items-center gap-2 bg-blue-50 text-blue-700 text-sm font-medium px-3 py-1.5 rounded-full border border-blue-200">
                      <Building2 size={14} /> Invitado por {me.tenant.commercialName}
                    </div>
                  )}
                </div>

                <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6">
                  <div className="flex items-center gap-2 mb-4">
                    <KeyRound size={18} className="text-amber-600" />
                    <h2 className="text-lg font-bold text-slate-800">Credenciales de Prueba</h2>
                  </div>

                  {me.sandboxCredentials ? (
                    <div className="space-y-4">
                      <p className="text-sm text-slate-500">
                        Client de prueba: <span className="font-semibold text-slate-700">{me.sandboxCredentials.companyName}</span>{' '}
                        · ambiente <span className="font-mono text-xs bg-slate-100 px-2 py-0.5 rounded">{me.sandboxCredentials.dataicoEnvironment}</span>
                      </p>
                      <CopyField label="Test API Key" value={me.sandboxCredentials.testApiKey} />
                      <CopyField label="Test API Secret" value={me.sandboxCredentials.testApiSecret} />
                      <p className="text-xs text-slate-400">
                        Úsalas en los headers <code className="font-mono">x-api-key</code>, <code className="font-mono">x-api-timestamp</code> y{' '}
                        <code className="font-mono">x-api-signature</code> — la firma HMAC está documentada en la pestaña "Documentación".
                      </p>
                    </div>
                  ) : (
                    <p className="text-sm text-slate-400">Aún no tienes credenciales de prueba asignadas.</p>
                  )}
                </div>

                <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6">
                  <h2 className="text-lg font-bold text-slate-800 mb-3">Documentación de la API</h2>
                  <p className="text-sm text-slate-500 mb-4">
                    Referencia completa de endpoints, esquemas y el mecanismo de firma HMAC para autenticar cada petición.
                    Todo lo que se emite ante la DIAN (facturas, notas, nómina, etc.) vive en una pestaña; el RIPS
                    independiente de factura, directo a MinSalud, en la otra.
                  </p>
                  <div className="flex gap-3">
                    {sections.map((s, i) => (
                      <button
                        key={s.key}
                        onClick={() => setTab(s.key)}
                        className={`inline-flex items-center gap-2 font-bold px-5 py-2.5 rounded-xl transition-colors ${i === 0 ? 'bg-slate-900 hover:bg-black text-white' : 'bg-slate-100 hover:bg-slate-200 text-slate-700'}`}
                      >
                        <s.icon size={16} /> {s.label}
                      </button>
                    ))}
                  </div>
                </div>
              </>
            )}
          </div>
        </main>
      )}
    </div>
  );
}
