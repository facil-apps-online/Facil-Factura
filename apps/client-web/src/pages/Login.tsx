import React, { useEffect, useState } from 'react';
import { useNavigate, useSearchParams, Link } from 'react-router-dom';
import { Mail, Lock, Loader2 } from 'lucide-react';
import { api, getErrorMessage } from '../lib/api';

interface PublicBranding {
  commercialName: string;
  logoLightUrl: string;
  primaryColorLight: string;
}

export default function Login({ onAuthSuccess }: { onAuthSuccess: () => void }) {
  const [searchParams] = useSearchParams();
  const tenantSlug = searchParams.get('tenant') || localStorage.getItem('fel_client_tenant') || '';

  const [branding, setBranding] = useState<PublicBranding | null>(null);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  // Quien tiene el mismo correo en varios clientes del tenant (un contador) elige en cuál trabajar: el servidor lo pide tras validar la contraseña.
  const [companies, setCompanies] = useState<Array<{ clientId: string; name: string; taxId?: string }> | null>(null);
  const navigate = useNavigate();

  useEffect(() => {
    if (!tenantSlug) return;
    api.get(`/tenant/branding/${tenantSlug}`)
      .then(res => setBranding(res.data))
      .catch(() => setBranding(null));
  }, [tenantSlug]);

  const handleSubmit = async (e: React.FormEvent, clientId?: string) => {
    e.preventDefault();
    if (!tenantSlug) {
      setError('No se identificó la empresa. Ingresa por el enlace que te compartió tu proveedor de facturación.');
      return;
    }

    setIsSubmitting(true);
    setError('');

    try {
      const res = await api.post('/client/auth/login', { tenantSlug, email, password, clientId });

      if (res.data.requiresCompany) {
        setCompanies(res.data.companies);
        return;
      }

      // Un inicio de sesión normal deja atrás cualquier marca de modo soporte.
      localStorage.removeItem('fel_client_support');
      localStorage.setItem('fel_client_auth', res.data.token);
      localStorage.setItem('fel_client_id', res.data.clientId);
      localStorage.setItem('fel_client_tenant', res.data.tenantSlug);

      onAuthSuccess();
      navigate('/');
    } catch (err: any) {
      setError(getErrorMessage(err, 'Credenciales incorrectas o error de conexión.'));
    } finally {
      setIsSubmitting(false);
    }
  };

  const primaryColor = branding?.primaryColorLight || '#2563eb';

  // min-h-dvh + overflow-x-hidden: los halos decorativos no ensanchan la página y, si la tarjeta es más
  // alta que la pantalla (teclado virtual), la página se desplaza en vez de cortarla.
  return (
    <div className="min-h-dvh flex items-center justify-center bg-slate-50 p-4 py-8 relative overflow-x-hidden">
      <div className="absolute top-[-10%] left-[-10%] w-96 h-96 rounded-full blur-[100px] pointer-events-none" style={{ backgroundColor: `${primaryColor}1a` }}></div>
      <div className="absolute bottom-[-10%] right-[-10%] w-96 h-96 rounded-full blur-[100px] pointer-events-none" style={{ backgroundColor: `${primaryColor}1a` }}></div>

      <div className="bg-white border border-slate-200 p-6 sm:p-10 rounded-[2rem] shadow-xl w-full max-w-[420px] relative z-10 animate-in fade-in zoom-in-95 duration-500">
        <div className="flex justify-center mb-6">
          {branding?.logoLightUrl ? (
            <img src={branding.logoLightUrl} alt={branding.commercialName} className="max-h-20 max-w-full object-contain" />
          ) : (
            <img src="/brand/isotipo-color.png" alt="Facil Factura" className="w-20 h-20 object-contain" />
          )}
        </div>

        <h1 className="text-2xl sm:text-3xl font-extrabold text-slate-800 text-center tracking-tight mb-2 break-words">
          {branding?.commercialName || 'Portal de Facturación'}
        </h1>
        <p className="text-slate-500 text-center text-sm mb-8 px-2">
          Ingresa para emitir y consultar tus documentos electrónicos.
        </p>

        {!tenantSlug && (
          <div className="bg-amber-50 border border-amber-200 text-amber-700 text-sm text-center py-3 px-3 rounded-xl mb-5 font-medium break-words">
            No se identificó tu empresa. Usa el enlace que te compartió tu proveedor de facturación.
          </div>
        )}

        {companies ? (
          <div className="space-y-3" data-testid="company-picker">
            <p className="text-sm font-semibold text-slate-700 text-center">¿En qué empresa vas a trabajar?</p>
            <div className="max-h-72 space-y-2 overflow-y-auto pr-1">
              {companies.map(c => (
                <button
                  key={c.clientId}
                  type="button"
                  disabled={isSubmitting}
                  onClick={e => handleSubmit(e, c.clientId)}
                  className="w-full rounded-xl border border-slate-200 bg-slate-50 px-4 py-3 text-left transition hover:border-slate-300 hover:bg-white disabled:opacity-50"
                >
                  <span className="block font-semibold text-slate-800 break-words">{c.name}</span>
                  {c.taxId && <span className="block text-xs text-slate-500">NIT {c.taxId}</span>}
                </button>
              ))}
            </div>
            {error && (
              <div role="alert" className="bg-rose-50 border border-rose-200 text-rose-600 text-sm text-center py-3 px-3 rounded-xl font-medium break-words">{error}</div>
            )}
            <button type="button" onClick={() => { setCompanies(null); setPassword(''); setError(''); }} className="w-full py-2 text-sm font-medium text-slate-500 hover:text-slate-700">
              Usar otro correo
            </button>
          </div>
        ) : (
        <form onSubmit={handleSubmit} className="space-y-5">
          <div className="relative">
            <div className="absolute inset-y-0 left-0 pl-4 flex items-center pointer-events-none">
              <Mail className="h-5 w-5 text-slate-400" />
            </div>
            <input
              type="email"
              required
              autoComplete="email"
              aria-label="Correo electrónico"
              placeholder="Correo electrónico"
              className="w-full bg-slate-50 border border-slate-200 text-slate-800 pl-12 pr-4 py-3.5 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:bg-white transition-all placeholder:text-slate-400"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
            />
          </div>
          <div className="relative">
            <div className="absolute inset-y-0 left-0 pl-4 flex items-center pointer-events-none">
              <Lock className="h-5 w-5 text-slate-400" />
            </div>
            <input
              type="password"
              required
              autoComplete="current-password"
              aria-label="Contraseña"
              placeholder="Contraseña"
              className="w-full bg-slate-50 border border-slate-200 text-slate-800 pl-12 pr-4 py-3.5 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:bg-white transition-all placeholder:text-slate-400"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
          </div>

          <div className="text-right -mt-2">
            <Link to={`/forgot-password${tenantSlug ? `?tenant=${tenantSlug}` : ''}`} className="inline-block py-2 text-sm text-slate-500 hover:text-primary font-medium">
              ¿Olvidaste tu contraseña?
            </Link>
          </div>

          {error && (
            <div role="alert" className="bg-rose-50 border border-rose-200 text-rose-600 text-sm text-center py-3 px-3 rounded-xl font-medium break-words">
              {error}
            </div>
          )}

          <button
            type="submit"
            disabled={isSubmitting || !tenantSlug}
            className="w-full text-white font-bold py-3.5 rounded-xl transition-all shadow-lg transform hover:-translate-y-0.5 disabled:opacity-50 disabled:transform-none flex justify-center items-center"
            style={{ backgroundColor: primaryColor }}>
            {isSubmitting ? <Loader2 className="w-5 h-5 animate-spin" /> : 'Ingresar al Portal'}
          </button>
        </form>
        )}
      </div>
    </div>
  );
}
