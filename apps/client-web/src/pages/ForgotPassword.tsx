import React, { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Mail, Loader2, ArrowLeft } from 'lucide-react';
import { api, getErrorMessage } from '../lib/api';

export default function ForgotPassword() {
  const [searchParams] = useSearchParams();
  const tenantSlug = searchParams.get('tenant') || localStorage.getItem('fel_client_tenant') || '';
  const [email, setEmail] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [sent, setSent] = useState(false);
  const [error, setError] = useState('');

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!tenantSlug) {
      setError('No se identificó tu empresa. Ingresa por el enlace que te compartió tu proveedor de facturación.');
      return;
    }
    setSubmitting(true);
    setError('');
    try {
      await api.post('/client/auth/forgot-password', { tenantSlug, email });
      setSent(true);
    } catch (err: any) {
      setError(getErrorMessage(err, 'Error al procesar la solicitud.'));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-slate-50 p-4">
      <div className="bg-white border border-slate-200 p-10 rounded-[2rem] shadow-xl w-full max-w-[420px]">
        <h1 className="text-2xl font-extrabold text-slate-800 text-center mb-2">Recuperar Contraseña</h1>
        <p className="text-slate-500 text-center text-sm mb-8">Te enviaremos un enlace para restablecerla.</p>

        {sent ? (
          <div className="bg-emerald-50 border border-emerald-200 text-emerald-700 text-sm text-center py-4 rounded-xl font-medium">
            Si el correo está registrado, te llegará un enlace para restablecer tu contraseña.
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
                placeholder="Correo de acceso"
                className="w-full bg-slate-50 border border-slate-200 text-slate-800 pl-12 pr-4 py-3.5 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:bg-white transition-all placeholder:text-slate-400"
                value={email}
                onChange={e => setEmail(e.target.value)}
              />
            </div>
            {error && (
              <div className="bg-rose-50 border border-rose-200 text-rose-600 text-sm text-center py-3 rounded-xl font-medium">
                {error}
              </div>
            )}
            <button
              type="submit"
              disabled={submitting}
              className="w-full bg-primary text-white font-bold py-3.5 rounded-xl transition-all shadow-lg hover:opacity-90 disabled:opacity-50 flex justify-center items-center"
            >
              {submitting ? <Loader2 className="w-5 h-5 animate-spin" /> : 'Enviar Enlace'}
            </button>
          </form>
        )}

        <Link to={`/login${tenantSlug ? `?tenant=${tenantSlug}` : ''}`} className="mt-6 flex items-center justify-center gap-2 text-sm text-slate-500 hover:text-slate-700 font-medium">
          <ArrowLeft size={16} /> Volver al inicio de sesión
        </Link>
      </div>
    </div>
  );
}
