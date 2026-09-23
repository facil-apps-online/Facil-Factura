import { BrowserRouter as Router, Routes, Route, Link, useLocation, Navigate } from 'react-router-dom';
import { Home, FileText, Settings, CreditCard, LogOut, FileSignature, Users, Package, Receipt, Banknote, Inbox, ChevronLeft, ChevronRight } from 'lucide-react';
import React, { createContext, useContext, useEffect, useState } from 'react';
import { Toaster } from 'sonner';
import { api } from './lib/api';

import TemplateSettings from './pages/TemplateSettings';
import ResolutionsSettings from './pages/ResolutionsSettings';
import CustomersPage from './pages/CustomersPage';
import ProductsPage from './pages/ProductsPage';
import InvoicesPage from './pages/InvoicesPage';
import SupportDocumentsPage from './pages/SupportDocumentsPage';
import PayrollPage from './pages/PayrollPage';
import ReceivedDocumentsPage from './pages/ReceivedDocumentsPage';
import Login from './pages/Login';
import ForgotPassword from './pages/ForgotPassword';
import ResetPassword from './pages/ResetPassword';

interface ClientBranding {
  companyName: string;
  logoLightUrl: string;
  logoDarkUrl: string;
  primaryColorLight: string;
  primaryColorDark: string;
  hasCustomLogo: boolean;
}

const BrandingContext = createContext<ClientBranding | null>(null);

function hexToRgb(hex: string) {
  const result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
  return result ? `${parseInt(result[1], 16)} ${parseInt(result[2], 16)} ${parseInt(result[3], 16)}` : '37 99 235';
}

function BrandingProvider({ children }: { children: React.ReactNode }) {
  const [branding, setBranding] = useState<ClientBranding | null>(null);

  useEffect(() => {
    api.get('/v1/branding/my-branding')
      .then(res => {
        setBranding(res.data);
        if (res.data.primaryColorLight) {
          document.documentElement.style.setProperty('--color-primary', hexToRgb(res.data.primaryColorLight));
        }
      })
      .catch(err => console.error("No se pudo cargar el branding", err));
  }, []);

  return (
    <BrandingContext.Provider value={branding}>
      {children}
    </BrandingContext.Provider>
  );
}

function Sidebar({ onLogout, collapsed, onToggleCollapsed }: { onLogout: () => void, collapsed: boolean, onToggleCollapsed: () => void }) {
  const location = useLocation();
  const branding = useContext(BrandingContext);
  const logo = branding?.logoLightUrl || '/brand/isotipo-blanco.png';
  const name = branding?.companyName || 'Facil Factura';

  const links = [
    { to: "/", icon: <Home size={20} />, label: "Inicio" },
    { to: "/invoices", icon: <FileText size={20} />, label: "Mis Facturas" },
    { to: "/support-documents", icon: <Receipt size={20} />, label: "Documentos Soporte" },
    { to: "/payroll", icon: <Banknote size={20} />, label: "Nómina Electrónica" },
    { to: "/received-documents", icon: <Inbox size={20} />, label: "Eventos de Recepción" },
    { to: "/customers", icon: <Users size={20} />, label: "Mis Terceros" },
    { to: "/products", icon: <Package size={20} />, label: "Mis Productos" },
    { to: "/payments", icon: <CreditCard size={20} />, label: "Pagos" },
    { to: "/resolutions", icon: <FileSignature size={20} />, label: "Resoluciones DIAN" },
    { to: "/settings", icon: <Settings size={20} />, label: "Diseño y Ajustes" },
  ];

  return (
    <aside className={`${collapsed ? 'w-20' : 'w-64'} bg-slate-900 text-slate-300 flex flex-col h-full shrink-0 z-20 shadow-xl transition-all duration-200`}>
      <div className={`p-6 flex items-center ${collapsed ? 'justify-center px-0' : 'justify-between'}`}>
        <h2 className="text-2xl font-bold text-white flex items-center gap-2 min-w-0">
          {branding?.logoLightUrl ? (
            <img src={branding.logoLightUrl} alt={name} className="w-7 h-7 object-contain shrink-0" />
          ) : (
            <img src={logo} alt={name} className="w-7 h-7 object-contain shrink-0" />
          )}
          {!collapsed && <span className="truncate">{name}</span>}
        </h2>
        {!collapsed && (
          <button
            onClick={onToggleCollapsed}
            title="Colapsar menú"
            className="p-1.5 rounded-lg text-slate-400 hover:bg-slate-800 hover:text-white transition-colors shrink-0"
          >
            <ChevronLeft size={18} />
          </button>
        )}
      </div>
      {!collapsed && <p className="text-xs text-slate-500 -mt-4 mb-2 px-6 uppercase tracking-wider">Portal de Facturación</p>}
      {collapsed && (
        <button
          onClick={onToggleCollapsed}
          title="Expandir menú"
          className="mx-auto mb-2 p-1.5 rounded-lg text-slate-400 hover:bg-slate-800 hover:text-white transition-colors"
        >
          <ChevronRight size={18} />
        </button>
      )}

      <nav className="flex-1 px-4 space-y-2 mt-4">
        {links.map((link) => {
          const isActive = location.pathname === link.to;
          return (
            <Link
              key={link.to}
              to={link.to}
              title={collapsed ? link.label : undefined}
              className={`flex items-center gap-3 px-4 py-3 rounded-xl transition-all duration-200 ${collapsed ? 'justify-center px-0' : ''} ${
                isActive
                  ? 'bg-primary text-white shadow-primary'
                  : 'hover:bg-slate-800 hover:text-white'
              }`}
            >
              {link.icon}
              {!collapsed && <span className="font-medium">{link.label}</span>}
            </Link>
          );
        })}
      </nav>

      <div className="p-4 border-t border-slate-800">
        <button onClick={onLogout} title={collapsed ? 'Cerrar Sesión' : undefined} className={`flex items-center gap-3 px-4 py-3 w-full rounded-xl hover:bg-rose-500/10 hover:text-rose-400 transition-colors text-left ${collapsed ? 'justify-center px-0' : ''}`}>
          <LogOut size={20} />
          {!collapsed && <span>Cerrar Sesión</span>}
        </button>
      </div>
    </aside>
  );
}

function Layout({ children, onLogout }: { children: React.ReactNode, onLogout: () => void }) {
  const branding = useContext(BrandingContext);
  const name = branding?.companyName || 'Facil Factura';
  const [collapsed, setCollapsed] = useState(() => localStorage.getItem('fel_client_sidebar_collapsed') === '1');

  const toggleCollapsed = () => {
    setCollapsed(prev => {
      const next = !prev;
      localStorage.setItem('fel_client_sidebar_collapsed', next ? '1' : '0');
      return next;
    });
  };

  return (
    <div className="flex h-screen overflow-hidden bg-slate-50 font-sans">
      <Sidebar onLogout={onLogout} collapsed={collapsed} onToggleCollapsed={toggleCollapsed} />
      <main className="flex-1 flex flex-col min-w-0 relative z-10">
        <header className="h-16 bg-white border-b border-slate-200 flex items-center justify-between px-8 shadow-sm">
          <h1 className="text-lg font-semibold text-slate-700">
            Portal
          </h1>
          <div className="flex items-center gap-4">
            <div className="w-8 h-8 rounded-full bg-primary flex items-center justify-center text-white font-bold text-sm shadow-md">
              {name.substring(0, 2).toUpperCase()}
            </div>
          </div>
        </header>
        
        <div className="flex-1 overflow-auto bg-slate-50/50">
          {children}
        </div>
      </main>
    </div>
  );
}

// Dashboard with actual metrics
const Dashboard = () => {
  const [metrics, setMetrics] = React.useState<any>(null);

  React.useEffect(() => {
    // Para simplificar la demo, asumo que tenemos el x-client-id configurado o un interceptor que lo añade
    const fetchMetrics = async () => {
      try {
        const res = await import('./lib/api').then(m => m.api.get('/v1/dashboard/metrics'));
        setMetrics(res.data);
      } catch (e) {
        console.error(e);
      }
    };
    fetchMetrics();
  }, []);

  return (
    <div className="p-10 animate-in fade-in slide-in-from-bottom-4 duration-500">
      <h1 className="text-3xl font-extrabold text-slate-800 tracking-tight mb-2">Bienvenido a tu Portal de Facturación</h1>
      <p className="text-slate-500 text-lg mb-10">Resumen de tu actividad en el mes actual.</p>
      
      {metrics ? (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
          <div className="bg-white rounded-[2rem] p-8 shadow-sm border border-slate-100 flex flex-col hover:shadow-xl transition-shadow">
            <h3 className="text-slate-400 font-bold uppercase tracking-wider text-sm mb-4">Total Documentos (Mes)</h3>
            <p className="text-5xl font-black text-slate-800">{metrics.totalDocuments}</p>
          </div>
          <div className="bg-blue-600 rounded-[2rem] p-8 shadow-lg shadow-blue-600/30 flex flex-col hover:shadow-2xl hover:shadow-blue-600/40 transition-all transform hover:-translate-y-1">
            <h3 className="text-blue-200 font-bold uppercase tracking-wider text-sm mb-4">Cuentas por Pagar (Servicio FEL)</h3>
            <p className="text-5xl font-black text-white">${metrics.amountDueToTenant.toLocaleString('es-CO')}</p>
          </div>
        </div>
      ) : (
        <div className="flex justify-center items-center h-48 bg-white rounded-3xl border border-slate-100">
           <div className="animate-spin rounded-full h-8 w-8 border-t-2 border-b-2 border-primary"></div>
        </div>
      )}
    </div>
  );
};

function App() {
  const [isAuthenticated, setIsAuthenticated] = useState(
    !!localStorage.getItem('fel_client_auth')
  );

  const handleLogout = () => {
    localStorage.removeItem('fel_client_auth');
    localStorage.removeItem('fel_client_id');
    setIsAuthenticated(false);
  };

  return (
    <Router>
      <Toaster position="top-right" richColors />
      <Routes>
        <Route
          path="/login"
          element={isAuthenticated ? <Navigate to="/" /> : <Login onAuthSuccess={() => setIsAuthenticated(true)} />}
        />
        <Route path="/forgot-password" element={<ForgotPassword />} />
        <Route path="/reset-password" element={<ResetPassword />} />
        <Route
          path="/*"
          element={
            isAuthenticated ? (
              <BrandingProvider>
                <Layout onLogout={handleLogout}>
                  <Routes>
                    <Route path="/" element={<Dashboard />} />
                    <Route path="/settings" element={<TemplateSettings />} />
                    <Route path="/resolutions" element={<ResolutionsSettings />} />
                    <Route path="/customers" element={<CustomersPage />} />
                    <Route path="/products" element={<ProductsPage />} />
                    <Route path="/invoices" element={<InvoicesPage />} />
                    <Route path="/support-documents" element={<SupportDocumentsPage />} />
                    <Route path="/payroll" element={<PayrollPage />} />
                    <Route path="/received-documents" element={<ReceivedDocumentsPage />} />
                    {/* Rutas ficticias para completar el sidebar */}
                    <Route path="/payments" element={<div className="p-8">Módulo en construcción...</div>} />
                  </Routes>
                </Layout>
              </BrandingProvider>
            ) : (
              <Navigate to={`/login${localStorage.getItem('fel_client_tenant') ? `?tenant=${localStorage.getItem('fel_client_tenant')}` : ''}`} />
            )
          }
        />
      </Routes>
    </Router>
  );
}

export default App;
