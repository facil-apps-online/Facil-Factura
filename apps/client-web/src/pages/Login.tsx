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
  const navigate = useNavigate();

  useEffect(() => {
    if (!tenantSlug) return;
    api.get(`/tenant/branding/${tenantSlug}`)
      .then(res => setBranding(res.data))
      .catch(() => setBranding(null));
  }, [tenantSlug]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!tenantSlug) {
      setError('No se identificó la empresa. Ingresa por el enlace que te compartió tu proveedor de facturación.');
      return;
    }

    setIsSubmitting(true);
    setError('');

    try {
      const res = await api.post('/client/auth/login', { tenantSlug, email, password });

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

  return (
    <div className="min-h-screen flex items-center justify-center bg-slate-50 p-4 relative overflow-hidden">
      <div className="absolute top-[-10%] left-[-10%] w-96 h-96 rounded-full blur-[100px] pointer-events-none" style={{ backgroundColor: `${primaryColor}1a` }}></div>
      <div className="absolute bottom-[-10%] right-[-10%] w-96 h-96 rounded-full blur-[100px] pointer-events-none" style={{ backgroundColor: `${primaryColor}1a` }}></div>

      <div className="bg-white border border-slate-200 p-10 rounded-[2rem] shadow-xl w-full max-w-[420px] relative z-10 animate-in fade-in zoom-in-95 duration-500">
        <div className="flex justify-center mb-6">
          {branding?.logoLightUrl ? (
            <img src={branding.logoLightUrl} alt={branding.commercialName} className="max-h-20 max-w-full object-contain" />
          ) : (
            <img src="/brand/isotipo-color.png" alt="Facil Factura" className="w-20 h-20 object-contain" />
          )}
        </div>

        <h1 className="text-3xl font-extrabold text-slate-800 text-center tracking-tight mb-2">
          {branding?.commercialName || 'Portal de Facturación'}
        </h1>
        <p className="text-slate-500 text-center text-sm mb-8 px-2">
          Ingresa para emitir y consultar tus documentos electrónicos.
        </p>

        {!tenantSlug && (
          <div className="bg-amber-50 border border-amber-200 text-amber-700 text-sm text-center py-3 rounded-xl mb-5 font-medium">
            No se identificó tu empresa. Usa el enlace que te compartió tu proveedor de facturación.
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-5">
          <div className="relative">
            <div className="absolute inset-y-0 left-0 pl-4 flex items-center pointer-events-none">
              <Mail className="h-5 w-5 text-slate-400" />
            </div>
            <input
              type="email"
              required
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
              placeholder="Contraseña"
              className="w-full bg-slate-50 border border-slate-200 text-slate-800 pl-12 pr-4 py-3.5 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:bg-white transition-all placeholder:text-slate-400"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
          </div>

          <div className="text-right -mt-2">
            <Link to={`/forgot-password${tenantSlug ? `?tenant=${tenantSlug}` : ''}`} className="text-sm text-slate-500 hover:text-primary font-medium">
              ¿Olvidaste tu contraseña?
            </Link>
          </div>

          {error && (
            <div className="bg-rose-50 border border-rose-200 text-rose-600 text-sm text-center py-3 rounded-xl font-medium">
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
      </div>
    </div>
  );
}
