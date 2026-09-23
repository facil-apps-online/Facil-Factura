import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import { Mail, Loader2, ArrowLeft } from 'lucide-react';
import { api } from './api';

export const ForgotPassword = () => {
  const [email, setEmail] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [sent, setSent] = useState(false);
  const [error, setError] = useState('');

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    setError('');
    try {
      await api.post('/auth/forgot-password', { email });
      setSent(true);
    } catch (err: any) {
      setError(err.response?.data?.message || err.response?.data || 'Error al procesar la solicitud.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-[#0B1120] via-slate-900 to-[#0B1120] p-4 relative overflow-hidden">
      <div className="absolute top-[-10%] left-[-10%] w-96 h-96 bg-blue-600/20 rounded-full blur-[100px] pointer-events-none"></div>
      <div className="absolute bottom-[-10%] right-[-10%] w-96 h-96 bg-indigo-600/20 rounded-full blur-[100px] pointer-events-none"></div>

      <div className="bg-white/5 backdrop-blur-2xl border border-white/10 p-10 rounded-[2rem] shadow-2xl w-full max-w-[420px] relative z-10">
        <h1 className="text-2xl font-extrabold text-white text-center mb-2">Recuperar Contraseña</h1>
        <p className="text-slate-400 text-center text-sm mb-8">Te enviaremos un enlace para restablecerla.</p>

        {sent ? (
          <div className="bg-emerald-500/10 border border-emerald-500/20 text-emerald-400 text-sm text-center py-4 rounded-xl font-medium">
            Si el correo está registrado, te llegará un enlace para restablecer tu contraseña.
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="space-y-5">
            <div className="relative">
              <div className="absolute inset-y-0 left-0 pl-4 flex items-center pointer-events-none">
                <Mail className="h-5 w-5 text-slate-500" />
              </div>
              <input
                type="email"
                required
                placeholder="Correo electrónico maestro"
                className="w-full bg-black/20 border border-slate-700 text-white pl-12 pr-4 py-3.5 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all placeholder:text-slate-500"
                value={email}
                onChange={e => setEmail(e.target.value)}
              />
            </div>
            {error && (
              <div className="bg-red-500/10 border border-red-500/20 text-red-400 text-sm text-center py-3 rounded-xl">
                {error}
              </div>
            )}
            <button
              type="submit"
              disabled={submitting}
              className="w-full bg-gradient-to-r from-blue-600 to-indigo-600 hover:from-blue-500 hover:to-indigo-500 text-white font-bold py-3.5 rounded-xl transition-all shadow-lg shadow-blue-500/25 disabled:opacity-50 flex justify-center items-center"
            >
              {submitting ? <Loader2 className="w-5 h-5 animate-spin" /> : 'Enviar Enlace'}
            </button>
          </form>
        )}

        <Link to="/login" className="mt-6 flex items-center justify-center gap-2 text-sm text-slate-400 hover:text-slate-200 font-medium">
          <ArrowLeft size={16} /> Volver al inicio de sesión
        </Link>
      </div>
    </div>
  );
};
