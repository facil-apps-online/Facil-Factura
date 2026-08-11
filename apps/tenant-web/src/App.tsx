import { BrowserRouter as Router, Routes, Route, Link, useLocation, Navigate, useNavigate } from 'react-router-dom';
import { Home, Users, FileKey, FileSignature, LogOut, Settings, Palette, FileText } from 'lucide-react';
import React, { useEffect, useState } from 'react';
import { api } from './lib/api';
import { Toaster } from 'sonner';

import Dashboard from './pages/Dashboard';
import Clients from './pages/Clients';
import ClientEdit from './pages/ClientEdit';
import Certificates from './pages/Certificates';
import Resolutions from './pages/Resolutions';
import Branding from './pages/Branding';
import Login from './pages/Login';
import DocumentTemplates from './pages/DocumentTemplates';

function Sidebar({ tenantBranding }: { tenantBranding: TenantBranding | null }) {
  const location = useLocation();
  const tenantName = tenantBranding?.commercialName || 'Tenant';

  const links = [
    { to: "/", icon: <Home size={20} />, label: "Dashboard" },
    { to: "/clients", icon: <Users size={20} />, label: "Clientes" },
    { to: "/branding", icon: <Palette size={20} />, label: "Apariencia (Branding)" },
    { to: "/templates", icon: <FileText size={20} />, label: "Modelos de Documentos" },
  ];

  const selfBillingLinks = [
    { to: "/resolutions", icon: <FileSignature size={20} />, label: "Mis Resoluciones" },
    { to: "/certificates", icon: <FileKey size={20} />, label: "Mi Certificado" },
  ];

  return (
    <aside className="w-64 bg-slate-900 text-slate-300 flex flex-col h-full shrink-0 z-20 shadow-xl">
      <div className="p-6">
        <div className="flex items-center gap-2 mb-2">
          <img src="/brand/isotipo-blanco.png" alt="Facil Factura" className="w-7 h-7 object-contain" />
          <span className="text-lg font-bold text-white tracking-tight">Facil Factura</span>
        </div>
        {tenantBranding?.logoLightUrl && (
          <div className="flex items-center gap-2 mt-2 pt-3 border-t border-slate-800/80">
            <img src={tenantBranding.logoLightUrl} alt={tenantName} className="w-6 h-6 object-contain" />
            <span className="text-sm font-semibold text-slate-200">{tenantName}</span>
          </div>
        )}
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

        <div className="pt-6 pb-2 px-4">
          <p className="text-[10px] uppercase font-bold tracking-wider text-slate-500">Facturación Propia (Opcional)</p>
        </div>
        {selfBillingLinks.map((link) => {
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
        <button 
          onClick={() => {
            localStorage.removeItem('fel_tenant_auth');
            localStorage.removeItem('fel_tenant_name');
            window.location.href = '/login';
          }}
          className="flex items-center gap-3 px-4 py-3 w-full rounded-xl hover:bg-rose-500/10 hover:text-rose-400 transition-colors text-left"
        >
          <LogOut size={20} />
          <span>Cerrar Sesión</span>
        </button>
      </div>
    </aside>
  );
}

interface TenantBranding {
  commercialName: string;
  logoLightUrl: string;
  primaryColorLight: string;
}

function ProtectedLayout({ children }: { children: React.ReactNode }) {
  const [tenantName, setTenantName] = useState('Cargando...');
  const [tenantBranding, setTenantBranding] = useState<TenantBranding | null>(null);

  useEffect(() => {
    // Cargamos el branding basado en la sesión del Tenant
    api.get('/tenant/branding/my-branding').then(res => {
      // Configuraciones de color globales
      const hexToRgb = (hex: string) => {
        const result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
        return result ? `${parseInt(result[1], 16)} ${parseInt(result[2], 16)} ${parseInt(result[3], 16)}` : '59 130 246';
      };
      
      if (res.data.primaryColorLight) {
        document.documentElement.style.setProperty('--color-primary', hexToRgb(res.data.primaryColorLight));
      }
      setTenantBranding({
        commercialName: res.data.commercialName || localStorage.getItem('fel_tenant_name') || 'Tenant',
        logoLightUrl: res.data.logoLightUrl || '',
        primaryColorLight: res.data.primaryColorLight || '',
      });
    }).catch(err => console.error("No se pudo cargar el branding", err));

    const name = localStorage.getItem('fel_tenant_name') || 'Tenant';
    setTenantName(name);
  }, []);

  return (
    <div className="flex h-screen overflow-hidden bg-slate-50 font-sans">
      <Sidebar tenantBranding={tenantBranding} />
      <main className="flex-1 flex flex-col min-w-0 relative z-10">
        <header className="h-16 bg-white border-b border-slate-200 flex items-center justify-between px-8 shadow-sm">
          <h1 className="text-lg font-semibold text-slate-700">
            Bienvenido, {tenantName}
          </h1>
          <div className="flex items-center gap-4">
            <button className="p-2 text-slate-400 hover:text-primary transition-colors">
              <Settings size={20} />
            </button>
            <div className="w-8 h-8 rounded-full bg-primary flex items-center justify-center text-white font-bold text-sm shadow-md">
              {tenantName.substring(0, 2).toUpperCase()}
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

function App() {
  const [isAuthenticated, setIsAuthenticated] = useState(
    !!localStorage.getItem('fel_tenant_auth')
  );

  return (
    <Router>
      <Toaster position="top-right" richColors />
      <Routes>
        <Route 
          path="/login" 
          element={isAuthenticated ? <Navigate to="/" /> : <Login onAuthSuccess={() => setIsAuthenticated(true)} />} 
        />
        <Route 
          path="/*" 
          element={
            isAuthenticated ? (
              <ProtectedLayout>
                <Routes>
                  <Route path="/" element={<Dashboard />} />
                  <Route path="/clients" element={<Clients />} />
                  <Route path="/clients/edit/:id" element={<ClientEdit />} />
                  <Route path="/certificates" element={<Certificates />} />
                  <Route path="/resolutions" element={<Resolutions />} />
                  <Route path="/branding" element={<Branding tenant={null} setTenant={() => {}} />} />
                  <Route path="/templates" element={<DocumentTemplates />} />
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
}

export default App;
