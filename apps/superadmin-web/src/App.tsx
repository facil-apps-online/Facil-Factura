import React, { useEffect, useState } from 'react';
import { BrowserRouter as Router, Routes, Route, Link, useNavigate, Navigate } from 'react-router-dom';
import { LayoutDashboard, Users, Receipt, Settings, Plus, LogOut, ShieldCheck, Mail, Lock, Loader2 } from 'lucide-react';
import { Toaster, toast } from 'sonner';
import { api } from './api';

import { TenantEdit } from './TenantEdit';
import { DocumentTypes } from './DocumentTypes';
import { DocumentTemplates } from './DocumentTemplates';
import { Billing } from './Billing';

// --- Types ---
interface DashboardMetrics {
  activeTenants: number;
  documentsThisMonth: number;
  estimatedBilling: number;
}

interface Tenant {
  id: string;
  name: string;
  commercialName: string;
  slug: string;
  isActive: boolean;
  createdAt: string;
}

// --- Auth Components ---
const AuthScreen = ({ onAuthSuccess }: { onAuthSuccess: () => void }) => {
  const [mode, setMode] = useState<'loading' | 'setup' | 'login'>('loading');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const navigate = useNavigate();

  useEffect(() => {
    api.get('/auth/status')
      .then(res => {
        if (res.data.setupRequired) {
          setMode('setup');
        } else {
          setMode('login');
        }
      })
      .catch(() => setError("Error de conexión con el motor FEL."));
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);
    setError('');

    try {
      if (mode === 'setup') {
        await api.post('/auth/setup', { email, password });
        // After setup, automatically log them in or show success
        const loginRes = await api.post('/auth/login', { email, password });
        localStorage.setItem('fel_superadmin_auth', loginRes.data.token);
        onAuthSuccess();
        navigate('/');
      } else {
        const loginRes = await api.post('/auth/login', { email, password });
        localStorage.setItem('fel_superadmin_auth', loginRes.data.token);
        onAuthSuccess();
        navigate('/');
      }
    } catch (err: any) {
      setError(err.response?.data || "Credenciales incorrectas o error en el servidor.");
    } finally {
      setIsSubmitting(false);
    }
  };

  if (mode === 'loading') {
    return (
      <div className="min-h-screen flex flex-col items-center justify-center bg-[#0B1120]">
        <Loader2 className="w-12 h-12 text-blue-500 animate-spin mb-4" />
        <p className="text-slate-400 font-medium animate-pulse">Conectando al Motor Central...</p>
      </div>
    );
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-[#0B1120] via-slate-900 to-[#0B1120] p-4 relative overflow-hidden">
      {/* Decorative Blur Orbs */}
      <div className="absolute top-[-10%] left-[-10%] w-96 h-96 bg-blue-600/20 rounded-full blur-[100px] pointer-events-none"></div>
      <div className="absolute bottom-[-10%] right-[-10%] w-96 h-96 bg-indigo-600/20 rounded-full blur-[100px] pointer-events-none"></div>

      <div className="bg-white/5 backdrop-blur-2xl border border-white/10 p-10 rounded-[2rem] shadow-2xl w-full max-w-[420px] relative z-10 animate-in fade-in zoom-in-95 duration-500">
        <div className="flex justify-center mb-6">
          <div className="bg-gradient-to-b from-blue-500/20 to-transparent p-4 rounded-2xl border border-blue-500/20 shadow-inner">
            <ShieldCheck className="w-12 h-12 text-blue-400" />
          </div>
        </div>
        
        <h1 className="text-3xl font-extrabold text-white text-center tracking-tight mb-2">
          {mode === 'setup' ? 'Inicialización FEL' : 'Bóveda FEL'}
        </h1>
        <p className="text-slate-400 text-center text-sm mb-8 px-2">
          {mode === 'setup' 
            ? 'Detectamos que el motor es nuevo. Crea la credencial maestra de Superadministrador.' 
            : 'Acceso seguro y exclusivo al Hub Central de Operaciones.'}
        </p>
        
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
              onChange={(e) => setEmail(e.target.value)}
            />
          </div>
          <div className="relative">
            <div className="absolute inset-y-0 left-0 pl-4 flex items-center pointer-events-none">
              <Lock className="h-5 w-5 text-slate-500" />
            </div>
            <input 
              type="password" 
              required
              placeholder="Contraseña de alta seguridad"
              className="w-full bg-black/20 border border-slate-700 text-white pl-12 pr-4 py-3.5 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all placeholder:text-slate-500"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
          </div>

          {error && (
            <div className="bg-red-500/10 border border-red-500/20 text-red-400 text-sm text-center py-3 rounded-xl animate-in shake duration-300">
              {error}
            </div>
          )}

          <button 
            type="submit"
            disabled={isSubmitting}
            className="w-full bg-gradient-to-r from-blue-600 to-indigo-600 hover:from-blue-500 hover:to-indigo-500 text-white font-bold py-3.5 rounded-xl transition-all shadow-lg shadow-blue-500/25 transform hover:-translate-y-0.5 disabled:opacity-50 disabled:transform-none flex justify-center items-center">
            {isSubmitting ? <Loader2 className="w-5 h-5 animate-spin" /> : (mode === 'setup' ? 'Forjar Credenciales' : 'Desbloquear Bóveda')}
          </button>
        </form>
      </div>
    </div>
  );
};

// --- Dashboard Component ---
const Dashboard = () => {
  const [metrics, setMetrics] = useState<DashboardMetrics>({
    activeTenants: 0,
    documentsThisMonth: 0,
    estimatedBilling: 0
  });

  useEffect(() => {
    api.get<DashboardMetrics>('/dashboard/metrics')
      .then(res => setMetrics(res.data))
      .catch(err => console.error("Error loading metrics", err));
  }, []);

  return (
    <div className="p-10 animate-in fade-in slide-in-from-bottom-4 duration-500">
      <h1 className="text-3xl font-extrabold text-white mb-8 tracking-tight">Panel de Control</h1>
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        <div className="glass-panel p-6 rounded-2xl hover:shadow-[0_0_20px_rgba(37,99,235,0.2)] transition-all">
          <div className="flex items-center justify-between">
            <p className="text-slate-400 text-sm font-semibold uppercase tracking-wider">Tenants Activos</p>
            <div className="bg-blue-500/10 p-2 rounded-lg border border-blue-500/20"><Users className="w-5 h-5 text-blue-400" /></div>
          </div>
          <p className="text-4xl font-extrabold text-white mt-4">{metrics.activeTenants}</p>
        </div>
        <div className="glass-panel p-6 rounded-2xl hover:shadow-[0_0_20px_rgba(16,185,129,0.2)] transition-all">
          <div className="flex items-center justify-between">
            <p className="text-slate-400 text-sm font-semibold uppercase tracking-wider">Docs Procesados</p>
            <div className="bg-emerald-500/10 p-2 rounded-lg border border-emerald-500/20"><Receipt className="w-5 h-5 text-emerald-400" /></div>
          </div>
          <p className="text-4xl font-extrabold text-white mt-4">{metrics.documentsThisMonth}</p>
        </div>
        <div className="glass-panel p-6 rounded-2xl hover:shadow-[0_0_20px_rgba(99,102,241,0.2)] transition-all">
          <div className="flex items-center justify-between">
            <p className="text-slate-400 text-sm font-semibold uppercase tracking-wider">Proyección Mes</p>
            <div className="bg-indigo-500/10 p-2 rounded-lg border border-indigo-500/20"><span className="text-indigo-400 font-bold">$</span></div>
          </div>
          <p className="text-4xl font-extrabold text-indigo-400 mt-4">
            ${metrics.estimatedBilling.toLocaleString('es-CO')}
          </p>
        </div>
      </div>
    </div>
  );
};

// --- Tenants Component ---
const TenantsList = () => {
  const [tenants, setTenants] = useState<Tenant[]>([]);
  const [showModal, setShowModal] = useState(false);
  const [formData, setFormData] = useState({ name: '', commercialName: '', email: '', slug: '' });
  const navigate = useNavigate();

  const loadTenants = () => {
    api.get<Tenant[]>('/tenants')
      .then(res => setTenants(res.data))
      .catch(err => console.error(err));
  };

  useEffect(() => {
    loadTenants();
  }, []);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await api.post('/tenants', formData);
      setShowModal(false);
      setFormData({ name: '', commercialName: '', email: '', slug: '' });
      toast.success("Tenant registrado con éxito.");
      loadTenants();
    } catch (err) {
      toast.error("Error al crear Tenant. Verifica el slug.");
    }
  };

  return (
    <div className="p-10 animate-in fade-in slide-in-from-bottom-4 duration-500">
      <div className="flex justify-between items-center mb-8">
        <h1 className="text-3xl font-extrabold text-white tracking-tight">Gestión de Tenants</h1>
        <button 
          onClick={() => setShowModal(true)}
          className="flex items-center bg-slate-900 text-white px-5 py-2.5 rounded-xl font-semibold hover:bg-slate-800 transition-all shadow-md hover:shadow-lg transform hover:-translate-y-0.5">
          <Plus className="w-5 h-5 mr-2" /> Registrar Tenant
        </button>
      </div>

      {showModal && (
        <div className="fixed inset-0 bg-slate-900/40 backdrop-blur-sm flex items-center justify-center z-50 animate-in fade-in duration-200">
          <div className="bg-white p-8 rounded-3xl w-full max-w-md shadow-2xl animate-in zoom-in-95 duration-200">
            <h2 className="text-2xl font-bold text-slate-900 mb-6">Nuevo Tenant</h2>
            <form onSubmit={handleCreate} className="space-y-5">
              <div>
                <label className="block text-sm font-semibold text-slate-700 mb-1">Razón Social</label>
                <input required type="text" placeholder="Ej. Glamtica SAS" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all" value={formData.name} onChange={e => setFormData({...formData, name: e.target.value})} />
              </div>
              <div>
                <label className="block text-sm font-semibold text-slate-700 mb-1">Nombre Comercial</label>
                <input required type="text" placeholder="Ej. Glamtica" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all" value={formData.commercialName} onChange={e => setFormData({...formData, commercialName: e.target.value})} />
              </div>
              <div>
                <label className="block text-sm font-semibold text-slate-700 mb-1">Email de Facturación</label>
                <input required type="email" placeholder="admin@glamtica.com" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all" value={formData.email} onChange={e => setFormData({...formData, email: e.target.value})} />
              </div>
              <div>
                <label className="block text-sm font-semibold text-slate-700 mb-1">Slug (Identificador URL)</label>
                <input required type="text" placeholder="glamtica" className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all font-mono text-sm" value={formData.slug} onChange={e => setFormData({...formData, slug: e.target.value.toLowerCase().replace(/\s+/g, '-')})} />
              </div>
              <div className="flex justify-end space-x-3 mt-8">
                <button type="button" onClick={() => setShowModal(false)} className="px-5 py-2.5 font-semibold text-slate-600 hover:bg-slate-100 rounded-xl transition-colors">Cancelar</button>
                <button type="submit" className="px-5 py-2.5 bg-blue-600 font-semibold text-white rounded-xl shadow-md shadow-blue-500/30 hover:bg-blue-500 transition-all">Guardar</button>
              </div>
            </form>
          </div>
        </div>
      )}

      <div className="glass-panel rounded-2xl overflow-hidden mt-8">
        <table className="w-full text-left border-collapse">
          <thead className="bg-slate-900/50">
            <tr>
              <th className="px-6 py-4 text-slate-400 font-semibold text-sm border-b border-slate-700/50">Empresa</th>
              <th className="px-6 py-4 text-slate-400 font-semibold text-sm border-b border-slate-700/50">Slug</th>
              <th className="px-6 py-4 text-slate-400 font-semibold text-sm border-b border-slate-700/50">Estado</th>
              <th className="px-6 py-4 text-slate-400 font-semibold text-sm border-b border-slate-700/50 text-right">Acciones</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-800/50">
            {tenants.map(t => (
              <tr key={t.id} className="hover:bg-slate-800/30 transition-colors group">
                <td className="px-6 py-4">
                  <p className="text-white font-semibold">{t.commercialName}</p>
                  <p className="text-xs text-slate-400 mt-0.5">{t.name}</p>
                </td>
                <td className="px-6 py-4 text-slate-300 font-mono text-sm">{t.slug}</td>
                <td className="px-6 py-4">
                  <span className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-bold border ${t.isActive ? 'bg-emerald-50 text-emerald-700 border-emerald-200/50' : 'bg-red-50 text-red-700 border-red-200/50'}`}>
                    <span className={`w-1.5 h-1.5 rounded-full mr-1.5 ${t.isActive ? 'bg-emerald-500' : 'bg-red-500'}`}></span>
                    {t.isActive ? 'Operativo' : 'Suspendido'}
                  </span>
                </td>
                <td className="px-6 py-4 text-right">
                  <button onClick={() => navigate(`/tenants/edit/${t.id}`)} className="text-blue-600 font-semibold text-sm hover:text-blue-700 opacity-0 group-hover:opacity-100 transition-opacity">
                    Configurar &rarr;
                  </button>
                </td>
              </tr>
            ))}
            {tenants.length === 0 && (
              <tr><td colSpan={4} className="text-center py-12 text-slate-500 font-medium">No hay tenants registrados en el sistema.</td></tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
};

// --- Main App Layout ---
const ProtectedLayout = ({ children }: { children: React.ReactNode }) => {
  const navigate = useNavigate();

  const handleLogout = () => {
    localStorage.removeItem('fel_superadmin_auth');
    navigate('/login');
  };

  return (
    <div className="flex h-screen bg-[#0B1120] text-slate-200 font-sans overflow-hidden">
      <aside className="w-64 bg-[#060B14] text-slate-300 flex flex-col border-r border-slate-800/50 relative z-20 shadow-2xl">
        <div className="p-8">
          <div className="flex items-center space-x-3 mb-2">
            <div className="w-8 h-8 bg-gradient-to-br from-blue-500 to-indigo-600 rounded-lg flex items-center justify-center shadow-lg shadow-blue-500/20">
              <span className="text-white font-bold text-lg">F</span>
            </div>
            <h2 className="text-2xl font-extrabold text-white tracking-tight">FEL Hub</h2>
          </div>
          <p className="text-[10px] text-slate-500 uppercase font-bold tracking-[0.2em] ml-11">Superadmin</p>
        </div>
        
        <nav className="space-y-2 mb-8">
          <Link to="/" className="flex items-center px-4 py-3 text-slate-300 hover:bg-slate-800 hover:text-white rounded-xl transition-all">
            <LayoutDashboard className="w-5 h-5 mr-3" /> Dashboard
          </Link>
          <Link to="/tenants" className="flex items-center px-4 py-3 text-slate-300 hover:bg-slate-800 hover:text-white rounded-xl transition-all">
            <Users className="w-5 h-5 mr-3" /> Tenants (Clientes)
          </Link>
          <Link to="/document-types" className="flex items-center px-4 py-3 text-slate-300 hover:bg-slate-800 hover:text-white rounded-xl transition-all">
            <Settings className="w-5 h-5 mr-3" /> Tipos de Documento
          </Link>
          <Link to="/billing" className="flex items-center px-4 py-3 text-slate-300 hover:bg-slate-800 hover:text-white rounded-xl transition-all">
            <Receipt className="w-5 h-5 mr-3" /> Facturación
          </Link>
        </nav>

        <div className="p-4 mt-auto border-t border-slate-800">
          <button 
            onClick={handleLogout}
            className="flex items-center w-full px-4 py-3 rounded-xl font-medium text-slate-400 hover:bg-red-500/10 hover:text-red-400 transition-colors">
            <LogOut className="w-5 h-5 mr-3 opacity-70" /> Cerrar Sesión
          </button>
        </div>
      </aside>

      <main className="flex-1 overflow-y-auto relative z-10">
        {children}
      </main>
    </div>
  );
};

const App = () => {
  const [isAuthenticated, setIsAuthenticated] = useState(
    !!localStorage.getItem('fel_superadmin_auth')
  );

  return (
    <Router>
      <Toaster position="top-right" richColors />
      <Routes>
        <Route 
          path="/login" 
          element={
            isAuthenticated ? <Navigate to="/" /> : <AuthScreen onAuthSuccess={() => setIsAuthenticated(true)} />
          } 
        />
        <Route 
          path="/*" 
          element={
            isAuthenticated ? (
              <ProtectedLayout>
                <Routes>
                  <Route path="/" element={<Dashboard />} />
                  <Route path="/tenants" element={<TenantsList />} />
                  <Route path="/tenants/edit/:id" element={<TenantEdit />} />
                  <Route path="/document-types" element={<DocumentTypes />} />
                  <Route path="/document-types/:typeId/templates" element={<DocumentTemplates />} />
                  <Route path="/billing" element={<Billing />} />
                  <Route path="/settings" element={<div className="p-10"><h1 className="text-3xl font-extrabold text-slate-900 tracking-tight">Configuración del Motor</h1></div>} />
                </Routes>
              </ProtectedLayout>
            ) : (
              <Navigate to="/login" />
            )
          } 
        />
      </Routes>
    </Router>
  );
};

export default App;
