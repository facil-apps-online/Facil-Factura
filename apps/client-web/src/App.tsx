import { BrowserRouter as Router, Routes, Route, Link, useLocation } from 'react-router-dom';
import { Home, FileText, Settings, CreditCard, LogOut, FileSignature, Users, Package } from 'lucide-react';
import React, { createContext, useContext, useEffect, useState } from 'react';
import { Toaster } from 'sonner';
import { api } from './lib/api';

import TemplateSettings from './pages/TemplateSettings';
import ResolutionsSettings from './pages/ResolutionsSettings';
import CustomersPage from './pages/CustomersPage';
import ProductsPage from './pages/ProductsPage';
import InvoicesPage from './pages/InvoicesPage';

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

function Sidebar() {
  const location = useLocation();
  const branding = useContext(BrandingContext);
  const logo = branding?.logoLightUrl || '/brand/isotipo-blanco.png';
  const name = branding?.companyName || 'Facil Factura';

  const links = [
    { to: "/", icon: <Home size={20} />, label: "Inicio" },
    { to: "/invoices", icon: <FileText size={20} />, label: "Mis Facturas" },
    { to: "/customers", icon: <Users size={20} />, label: "Mis Clientes" },
    { to: "/products", icon: <Package size={20} />, label: "Mis Productos" },
    { to: "/payments", icon: <CreditCard size={20} />, label: "Pagos" },
    { to: "/resolutions", icon: <FileSignature size={20} />, label: "Resoluciones DIAN" },
    { to: "/settings", icon: <Settings size={20} />, label: "Diseño y Ajustes" },
  ];

  return (
    <aside className="w-64 bg-slate-900 text-slate-300 flex flex-col h-full shrink-0 z-20 shadow-xl">
      <div className="p-6">
        <h2 className="text-2xl font-bold text-white flex items-center gap-2">
          {branding?.logoLightUrl ? (
            <img src={branding.logoLightUrl} alt={name} className="w-7 h-7 object-contain" />
          ) : (
            <img src={logo} alt={name} className="w-7 h-7 object-contain" />
          )}
          {name}
        </h2>
        <p className="text-xs text-slate-500 mt-1 uppercase tracking-wider">Portal de Facturación</p>
      </div>
      
      <nav className="flex-1 px-4 space-y-2 mt-4">
        {links.map((link) => {
          const isActive = location.pathname === link.to;
          return (
            <Link
              key={link.to}
              to={link.to}
              className={`flex items-center gap-3 px-4 py-3 rounded-xl transition-all duration-200 ${
                isActive 
                  ? 'bg-primary text-white shadow-primary' 
                  : 'hover:bg-slate-800 hover:text-white'
              }`}
            >
              {link.icon}
              <span className="font-medium">{link.label}</span>
            </Link>
          );
        })}
      </nav>

      <div className="p-4 border-t border-slate-800">
        <button className="flex items-center gap-3 px-4 py-3 w-full rounded-xl hover:bg-rose-500/10 hover:text-rose-400 transition-colors text-left">
          <LogOut size={20} />
          <span>Cerrar Sesión</span>
        </button>
      </div>
    </aside>
  );
}

function Layout({ children }: { children: React.ReactNode }) {
  const branding = useContext(BrandingContext);
  const name = branding?.companyName || 'Facil Factura';

  return (
    <div className="flex h-screen overflow-hidden bg-slate-50 font-sans">
      <Sidebar />
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
  return (
    <BrandingProvider>
      <Router>
        <Toaster position="top-right" richColors />
        <Routes>
          <Route path="/*" element={
            <Layout>
              <Routes>
                <Route path="/" element={<Dashboard />} />
                <Route path="/settings" element={<TemplateSettings />} />
                <Route path="/resolutions" element={<ResolutionsSettings />} />
                <Route path="/customers" element={<CustomersPage />} />
                <Route path="/products" element={<ProductsPage />} />
                <Route path="/invoices" element={<InvoicesPage />} />
                {/* Rutas ficticias para completar el sidebar */}
                <Route path="/payments" element={<div className="p-8">Módulo en construcción...</div>} />
              </Routes>
            </Layout>
          } />
        </Routes>
      </Router>
    </BrandingProvider>
  );
}

export default App;
