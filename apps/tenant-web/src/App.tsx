import { BrowserRouter as Router, Routes, Route, Link, useLocation, Navigate, useNavigate } from 'react-router-dom';
import { Home, Users, FileKey, FileSignature, LogOut, Settings, Palette, FileText, Briefcase, Code2, ChevronLeft, ChevronRight, Layers, Building2, Check, Loader2 } from 'lucide-react';
import React, { useEffect, useState } from 'react';
import { api } from './lib/api';
import { Toaster } from 'sonner';

import Dashboard from './pages/Dashboard';
import Clients from './pages/Clients';
import Associates from './pages/Associates';
import ClientEdit from './pages/ClientEdit';
import Certificates from './pages/Certificates';
import Resolutions from './pages/Resolutions';
import Branding from './pages/Branding';
import Login from './pages/Login';
import ForgotPassword from './pages/ForgotPassword';
import ResetPassword from './pages/ResetPassword';
import DocumentTemplates from './pages/DocumentTemplates';
import Developers from './pages/Developers';
import GroupBilling from './pages/GroupBilling';

interface TenantOption {
  id: string;
  commercialName: string;
  slug: string;
}

// Solo aparece cuando la cuenta administra más de un tenant (fel_tenant_list solo se guarda en
// ese caso, ver Login.tsx). Reemitir el token para el tenant elegido y recargar es más simple y
// más seguro que intentar refrescar en caliente cada dato que depende del tenant activo — branding,
// permisos, listados — sin arriesgarse a dejar alguno con datos del tenant anterior.
function TenantSwitcher({ currentTenantId, currentName }: { currentTenantId: string; currentName: string }) {
  const [open, setOpen] = useState(false);
  const [switching, setSwitching] = useState<string | null>(null);
  const tenants: TenantOption[] = React.useMemo(() => {
    try {
      const raw = localStorage.getItem('fel_tenant_list');
      return raw ? JSON.parse(raw) : [];
    } catch {
      return [];
    }
  }, []);

  if (tenants.length < 2) return null;

  const handleSwitch = async (tenant: TenantOption) => {
    if (tenant.id === currentTenantId) { setOpen(false); return; }
    setSwitching(tenant.id);
    try {
      const res = await api.post('/tenant/auth/switch-tenant', { tenantId: tenant.id });
      localStorage.setItem('fel_tenant_auth', res.data.token);
      localStorage.setItem('fel_tenant_id', res.data.tenantId);
      localStorage.setItem('fel_tenant_name', res.data.commercialName);
      // Recarga completa: todo lo que ya está en memoria (listados, branding cargado, etc.)
      // pertenece al tenant anterior.
      window.location.href = '/';
    } catch {
      setSwitching(null);
      setOpen(false);
    }
  };

  return (
    <div className="relative mt-2 pt-3 border-t border-slate-800/80">
      <button
        onClick={() => setOpen(o => !o)}
        className="w-full flex items-center gap-2 px-2 py-1.5 rounded-lg hover:bg-slate-800 transition-colors text-left"
      >
        <Building2 size={16} className="text-slate-400 shrink-0" />
        <span className="text-sm font-semibold text-slate-200 truncate flex-1">{currentName}</span>
        <ChevronRight size={14} className={`text-slate-500 transition-transform ${open ? 'rotate-90' : ''}`} />
      </button>
      {open && (
        <div className="absolute left-0 right-0 top-full mt-1 bg-slate-800 border border-slate-700 rounded-xl shadow-xl overflow-hidden z-30">
          {tenants.map(t => (
            <button
              key={t.id}
              onClick={() => handleSwitch(t)}
              disabled={switching !== null}
              className="w-full flex items-center gap-2 px-3 py-2.5 text-sm text-left hover:bg-slate-700 transition-colors disabled:opacity-60"
            >
              {switching === t.id
                ? <Loader2 size={14} className="animate-spin text-slate-400 shrink-0" />
                : t.id === currentTenantId
                  ? <Check size={14} className="text-primary shrink-0" />
                  : <span className="w-3.5 shrink-0" />}
              <span className="truncate">{t.commercialName}</span>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

function Sidebar({ tenantBranding, collapsed, onToggleCollapsed }: { tenantBranding: TenantBranding | null, collapsed: boolean, onToggleCollapsed: () => void }) {
  const location = useLocation();
  const tenantName = tenantBranding?.commercialName || 'Tenant';

  const links = [
    { to: "/", icon: <Home size={20} />, label: "Dashboard" },
    { to: "/clients", icon: <Users size={20} />, label: "Clientes" },
    { to: "/associates", icon: <Briefcase size={20} />, label: "Asociados" },
    { to: "/group-billing", icon: <Layers size={20} />, label: "Facturación de mi grupo" },
    { to: "/branding", icon: <Palette size={20} />, label: "Apariencia (Branding)" },
    { to: "/templates", icon: <FileText size={20} />, label: "Modelos de Documentos" },
    { to: "/developers", icon: <Code2 size={20} />, label: "Developers" },
  ];

  const selfBillingLinks = [
    { to: "/resolutions", icon: <FileSignature size={20} />, label: "Mis Resoluciones" },
    { to: "/certificates", icon: <FileKey size={20} />, label: "Mi Certificado" },
  ];

  return (
    <aside className={`${collapsed ? 'w-20' : 'w-64'} bg-slate-900 text-slate-300 flex flex-col h-full shrink-0 z-20 shadow-xl transition-all duration-200`}>
      <div className="p-6">
        <div className={`flex items-center gap-2 mb-2 ${collapsed ? 'justify-center' : 'justify-between'}`}>
          <div className="flex items-center gap-2 min-w-0">
            <img src="/brand/isotipo-blanco.png" alt="Facil Factura" className="w-7 h-7 object-contain shrink-0" />
            {!collapsed && <span className="text-lg font-bold text-white tracking-tight truncate">Facil Factura</span>}
          </div>
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
        {collapsed && (
          <button
            onClick={onToggleCollapsed}
            title="Expandir menú"
            className="mx-auto mt-2 p-1.5 rounded-lg text-slate-400 hover:bg-slate-800 hover:text-white transition-colors flex"
          >
            <ChevronRight size={18} />
          </button>
        )}
        {!collapsed && tenantBranding?.logoLightUrl && (
          <div className="flex items-center gap-2 mt-2 pt-3 border-t border-slate-800/80">
            <img src={tenantBranding.logoLightUrl} alt={tenantName} className="w-6 h-6 object-contain" />
            <span className="text-sm font-semibold text-slate-200">{tenantName}</span>
          </div>
        )}
        {!collapsed && (
          <TenantSwitcher
            currentTenantId={localStorage.getItem('fel_tenant_id') || ''}
            currentName={tenantName}
          />
        )}
      </div>

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

        {!collapsed && (
          <div className="pt-6 pb-2 px-4">
            <p className="text-[10px] uppercase font-bold tracking-wider text-slate-500">Facturación Propia (Opcional)</p>
          </div>
        )}
        {collapsed && <div className="pt-4 border-t border-slate-800/60 mx-2" />}
        {selfBillingLinks.map((link) => {
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
        <button
          onClick={() => {
            localStorage.removeItem('fel_tenant_auth');
            localStorage.removeItem('fel_tenant_id');
            localStorage.removeItem('fel_tenant_name');
            localStorage.removeItem('fel_tenant_list');
            window.location.href = '/login';
          }}
          title={collapsed ? 'Cerrar Sesión' : undefined}
          className={`flex items-center gap-3 px-4 py-3 w-full rounded-xl hover:bg-rose-500/10 hover:text-rose-400 transition-colors text-left ${collapsed ? 'justify-center px-0' : ''}`}
        >
          <LogOut size={20} />
          {!collapsed && <span>Cerrar Sesión</span>}
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
  const [collapsed, setCollapsed] = useState(() => localStorage.getItem('fel_tenant_sidebar_collapsed') === '1');

  const toggleCollapsed = () => {
    setCollapsed(prev => {
      const next = !prev;
      localStorage.setItem('fel_tenant_sidebar_collapsed', next ? '1' : '0');
      return next;
    });
  };

  useEffect(() => {
    // Solo cargamos nombre/logo para mostrarlos en el sidebar. El color de marca del
    // tenant es para el portal de sus clientes (clients.facil-factura.pro), no para
    // este panel — el panel del tenant mantiene su propio tema fijo.
    api.get('/tenant/branding/my-branding').then(res => {
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
      <Sidebar tenantBranding={tenantBranding} collapsed={collapsed} onToggleCollapsed={toggleCollapsed} />
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
        <Route path="/forgot-password" element={<ForgotPassword />} />
        <Route path="/reset-password" element={<ResetPassword />} />
        <Route
          path="/*"
          element={
            isAuthenticated ? (
              <ProtectedLayout>
                <Routes>
                  <Route path="/" element={<Dashboard />} />
                  <Route path="/clients" element={<Clients />} />
                  <Route path="/clients/edit/:id" element={<ClientEdit />} />
                  <Route path="/associates" element={<Associates />} />
                  <Route path="/group-billing" element={<GroupBilling />} />
                  <Route path="/certificates" element={<Certificates />} />
                  <Route path="/resolutions" element={<Resolutions />} />
                  <Route path="/branding" element={<Branding />} />
                  <Route path="/templates" element={<DocumentTemplates />} />
                  <Route path="/developers" element={<Developers />} />
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
