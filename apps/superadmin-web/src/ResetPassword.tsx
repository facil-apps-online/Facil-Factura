import React, { useState } from 'react';
import { useNavigate, useSearchParams, Link } from 'react-router-dom';
import { Lock, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { api } from './api';
import PasswordStrength, { evaluatePassword } from '@shared/components/PasswordStrength';

export const ResetPassword = () => {
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token') || '';
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState('');
  const [done, setDone] = useState(false);
  const navigate = useNavigate();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const strength = evaluatePassword(newPassword);
    if (!strength.meetsPolicy) {
      setError(strength.reason || 'La contraseña no cumple la política mínima.');
      return;
    }
    if (newPassword !== confirmPassword) {
      setError('Las contraseñas no coinciden.');
      return;
    }
    setSubmitting(true);
    setError('');
    try {
      await api.post('/auth/reset-password', { token, newPassword });
      setDone(true);
      toast.success('Contraseña actualizada. Ya puedes ingresar.');
    } catch (err: any) {
      setError(err.response?.data?.message || err.response?.data || 'Error al restablecer la contraseña.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-[#0B1120] via-slate-900 to-[#0B1120] p-4 relative overflow-hidden">
      <div className="absolute top-[-10%] left-[-10%] w-96 h-96 bg-blue-600/20 rounded-full blur-[100px] pointer-events-none"></div>
      <div className="absolute bottom-[-10%] right-[-10%] w-96 h-96 bg-indigo-600/20 rounded-full blur-[100px] pointer-events-none"></div>

      <div className="bg-white/5 backdrop-blur-2xl border border-white/10 p-10 rounded-[2rem] shadow-2xl w-full max-w-[420px] relative z-10">
        <h1 className="text-2xl font-extrabold text-white text-center mb-2">Crear nueva contraseña</h1>

        {!token ? (
          <div className="bg-red-500/10 border border-red-500/20 text-red-400 text-sm text-center py-4 rounded-xl">
            El enlace no incluye un token válido. Solicita uno nuevo desde "Olvidé mi contraseña".
          </div>
        ) : done ? (
          <div className="space-y-4">
            <div className="bg-emerald-500/10 border border-emerald-500/20 text-emerald-400 text-sm text-center py-4 rounded-xl font-medium">
              Tu contraseña fue actualizada correctamente.
            </div>
            <button onClick={() => navigate('/login')} className="w-full bg-gradient-to-r from-blue-600 to-indigo-600 hover:from-blue-500 hover:to-indigo-500 text-white font-bold py-3.5 rounded-xl transition-all">
              Ir a Iniciar Sesión
            </button>
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="space-y-5">
            <div className="relative">
              <div className="absolute inset-y-0 left-0 pl-4 flex items-center pointer-events-none">
                <Lock className="h-5 w-5 text-slate-500" />
              </div>
              <input
                type="password"
                required
                minLength={8}
                placeholder="Nueva contraseña"
                className="w-full bg-black/20 border border-slate-700 text-white pl-12 pr-4 py-3.5 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all placeholder:text-slate-500"
                value={newPassword}
                onChange={e => setNewPassword(e.target.value)}
              />
            </div>
            <PasswordStrength password={newPassword} />
            <div className="relative">
              <div className="absolute inset-y-0 left-0 pl-4 flex items-center pointer-events-none">
                <Lock className="h-5 w-5 text-slate-500" />
              </div>
              <input
                type="password"
                required
                minLength={8}
                placeholder="Confirmar contraseña"
                className="w-full bg-black/20 border border-slate-700 text-white pl-12 pr-4 py-3.5 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all placeholder:text-slate-500"
                value={confirmPassword}
                onChange={e => setConfirmPassword(e.target.value)}
              />
            </div>
            {error && (
              <div className="bg-red-500/10 border border-red-500/20 text-red-400 text-sm text-center py-3 rounded-xl">
                {error}
              </div>
            )}
            <button
              type="submit"
              disabled={submitting || !evaluatePassword(newPassword).meetsPolicy}
              className="w-full bg-gradient-to-r from-blue-600 to-indigo-600 hover:from-blue-500 hover:to-indigo-500 text-white font-bold py-3.5 rounded-xl transition-all shadow-lg shadow-blue-500/25 disabled:opacity-50 flex justify-center items-center"
            >
              {submitting ? <Loader2 className="w-5 h-5 animate-spin" /> : 'Guardar Contraseña'}
            </button>
          </form>
        )}

        {!done && (
          <Link to="/login" className="mt-6 block text-center text-sm text-slate-400 hover:text-slate-200 font-medium">
            Volver al inicio de sesión
          </Link>
        )}
      </div>
    </div>
  );
};
