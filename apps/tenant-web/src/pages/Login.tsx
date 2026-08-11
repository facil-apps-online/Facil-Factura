import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Mail, Lock, Loader2, Building2 } from 'lucide-react';
import { api } from '../lib/api';

export default function Login({ onAuthSuccess }: { onAuthSuccess: () => void }) {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const navigate = useNavigate();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);
    setError('');

    try {
      const res = await api.post('/tenant/auth/login', { email, password });
      
      // Guardar sesión y datos del tenant
      localStorage.setItem('fel_tenant_auth', res.data.token);
      localStorage.setItem('fel_tenant_name', res.data.commercialName);
      
      onAuthSuccess();
      navigate('/');
    } catch (err: any) {
      setError(err.response?.data || "Credenciales incorrectas o error de conexión.");
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-slate-50 p-4 relative overflow-hidden">
      {/* Esferas decorativas */}
      <div className="absolute top-[-10%] left-[-10%] w-96 h-96 bg-blue-500/10 rounded-full blur-[100px] pointer-events-none"></div>
      <div className="absolute bottom-[-10%] right-[-10%] w-96 h-96 bg-indigo-500/10 rounded-full blur-[100px] pointer-events-none"></div>

      <div className="bg-white border border-slate-200 p-10 rounded-[2rem] shadow-xl w-full max-w-[420px] relative z-10 animate-in fade-in zoom-in-95 duration-500">
        <div className="flex justify-center mb-6">
          <img src="/brand/isotipo-color.png" alt="Facil Factura" className="w-20 h-20 object-contain" />
        </div>
        
        <h1 className="text-3xl font-extrabold text-slate-800 text-center tracking-tight mb-2">
          Facil Factura
        </h1>
        <p className="text-slate-500 text-center text-sm mb-8 px-2">
          Accede a tu cuenta de distribuidor para gestionar emisores y facturación electrónica.
        </p>
        
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

          {error && (
            <div className="bg-rose-50 border border-rose-200 text-rose-600 text-sm text-center py-3 rounded-xl animate-in shake duration-300 font-medium">
              {error}
            </div>
          )}

          <button 
            type="submit"
            disabled={isSubmitting}
            className="w-full bg-blue-600 hover:bg-blue-700 text-white font-bold py-3.5 rounded-xl transition-all shadow-lg shadow-blue-500/25 transform hover:-translate-y-0.5 disabled:opacity-50 disabled:transform-none flex justify-center items-center">
            {isSubmitting ? <Loader2 className="w-5 h-5 animate-spin" /> : 'Ingresar al Portal'}
          </button>
        </form>
      </div>
    </div>
  );
}
