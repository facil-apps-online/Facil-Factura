import React, { useState } from 'react';
import { useNavigate, useSearchParams, Link } from 'react-router-dom';
import { Lock, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { api, getErrorMessage } from '../lib/api';
import PasswordStrength, { evaluatePassword } from '@shared/components/PasswordStrength';

export default function ResetPassword() {
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
      await api.post('/client/auth/reset-password', { token, newPassword });
      setDone(true);
      toast.success('Contraseña actualizada. Ya puedes ingresar.');
    } catch (err: any) {
      setError(getErrorMessage(err, 'Error al restablecer la contraseña.'));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-slate-50 p-4">
      <div className="bg-white border border-slate-200 p-10 rounded-[2rem] shadow-xl w-full max-w-[420px]">
        <h1 className="text-2xl font-extrabold text-slate-800 text-center mb-2">Crear nueva contraseña</h1>

        {!token ? (
          <div className="bg-rose-50 border border-rose-200 text-rose-600 text-sm text-center py-4 rounded-xl font-medium">
            El enlace no incluye un token válido. Solicita uno nuevo desde "Olvidé mi contraseña".
          </div>
        ) : done ? (
          <div className="space-y-4">
            <div className="bg-emerald-50 border border-emerald-200 text-emerald-700 text-sm text-center py-4 rounded-xl font-medium">
              Tu contraseña fue actualizada correctamente.
            </div>
            <button onClick={() => navigate('/login')} className="w-full bg-primary text-white font-bold py-3.5 rounded-xl hover:opacity-90 transition-all">
              Ir a Iniciar Sesión
            </button>
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="space-y-5">
            <div className="relative">
              <div className="absolute inset-y-0 left-0 pl-4 flex items-center pointer-events-none">
                <Lock className="h-5 w-5 text-slate-400" />
              </div>
              <input
                type="password"
                required
                minLength={8}
                placeholder="Nueva contraseña"
                className="w-full bg-slate-50 border border-slate-200 text-slate-800 pl-12 pr-4 py-3.5 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:bg-white transition-all placeholder:text-slate-400"
                value={newPassword}
                onChange={e => setNewPassword(e.target.value)}
              />
            </div>
            <PasswordStrength password={newPassword} />
            <div className="relative">
              <div className="absolute inset-y-0 left-0 pl-4 flex items-center pointer-events-none">
                <Lock className="h-5 w-5 text-slate-400" />
              </div>
              <input
                type="password"
                required
                minLength={8}
                placeholder="Confirmar contraseña"
                className="w-full bg-slate-50 border border-slate-200 text-slate-800 pl-12 pr-4 py-3.5 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:bg-white transition-all placeholder:text-slate-400"
                value={confirmPassword}
                onChange={e => setConfirmPassword(e.target.value)}
              />
            </div>
            {error && (
              <div className="bg-rose-50 border border-rose-200 text-rose-600 text-sm text-center py-3 rounded-xl font-medium">
                {error}
              </div>
            )}
            <button
              type="submit"
              disabled={submitting || !evaluatePassword(newPassword).meetsPolicy}
              className="w-full bg-primary text-white font-bold py-3.5 rounded-xl transition-all shadow-lg hover:opacity-90 disabled:opacity-50 flex justify-center items-center"
            >
              {submitting ? <Loader2 className="w-5 h-5 animate-spin" /> : 'Guardar Contraseña'}
            </button>
          </form>
        )}

        {!done && (
          <Link to="/login" className="mt-6 block text-center text-sm text-slate-500 hover:text-slate-700 font-medium">
            Volver al inicio de sesión
          </Link>
        )}
      </div>
    </div>
  );
}
