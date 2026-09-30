import { BrowserRouter as Router, Routes, Route, Link, useLocation, useNavigate, Navigate } from 'react-router-dom';
import { Home, FileText, Settings, CreditCard, LogOut, FileSignature, Users, Package, Receipt, Banknote, Inbox, ChevronLeft, ChevronRight, CheckCircle2, Clock, AlertTriangle, DollarSign, ArrowRight, Wallet } from 'lucide-react';
import React, { createContext, useContext, useEffect, useState } from 'react';
import { Toaster } from 'sonner';
import { ConfirmDialogProvider } from '@shared/components/ConfirmDialog';
import { api } from './lib/api';

import TemplateSettings from './pages/TemplateSettings';
import TemplateEditor from './pages/TemplateEditor';
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
  invoiceLogoUrl: string;
  clientName: string;
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
    { to: "/payroll", icon: <Banknote size={20} />, label: "Nómina electrónica" },
    { to: "/received-documents", icon: <Inbox size={20} />, label: "Documentos recibidos" },
    { to: "/customers", icon: <Users size={20} />, label: "Terceros" },
    { to: "/products", icon: <Package size={20} />, label: "Productos y servicios" },
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
            title="Contraer menú"
            className="p-1.5 rounded-lg text-slate-400 hover:bg-slate-800 hover:text-white transition-colors shrink-0"
          >
            <ChevronLeft size={18} />
          </button>
        )}
      </div>
      {!collapsed && <p className="text-xs text-slate-500 -mt-4 mb-2 px-6 uppercase tracking-wider">Facturación</p>}
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
        <button onClick={onLogout} title={collapsed ? 'Cerrar sesión' : undefined} className={`flex items-center gap-3 px-4 py-3 w-full rounded-xl hover:bg-rose-500/10 hover:text-rose-400 transition-colors text-left ${collapsed ? 'justify-center px-0' : ''}`}>
          <LogOut size={20} />
          {!collapsed && <span>Cerrar sesión</span>}
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
          {/* El logo/nombre de acá es el del Client (el tercero facturador), no el del Tenant (que
              ya se muestra en el sidebar) — es lo que le deja claro al usuario en qué cliente está
              ubicado cuando el Tenant administra varios. */}
          {branding?.invoiceLogoUrl ? (
            <img src={branding.invoiceLogoUrl} alt={branding.clientName} className="h-9 object-contain" />
          ) : (
            <h1 className="text-lg font-semibold text-slate-700">{branding?.clientName}</h1>
          )}
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

interface DashboardSummary {
  period: { start: string; end: string };
  totalIssued: number;
  totalAccepted: number;
  totalPending: number;
  totalRejected: number;
  totalBilled: number;
  recentDocuments: Array<{
    id: string;
    typeCode: string;
    number: string;
    processedAt: string;
    customerName: string | null;
    totalAmount: number;
  }>;
  consumption:
    | { mode: 'PrepaidBag'; bagStartDate: string; amountPaid: number; remainingBalance: number; discountedPricePerDocument: number }
    | { mode: 'Standard'; pricePerDocument: number };
  pendingSetupItems: string[];
}

const money = (value: number) => `$${Math.round(value).toLocaleString('es-CO')}`;

const SETUP_MESSAGES: Record<string, { text: string; actionLabel?: string; actionTo?: string }> = {
  'no-resolution': { text: 'Configura una resolución para empezar a facturar.', actionLabel: 'Configurar resolución', actionTo: '/resolutions' },
  'no-certificate': { text: 'Tu certificado digital no está vigente. Contacta a tu proveedor para renovarlo.' },
};

const Dashboard = () => {
  const navigate = useNavigate();
  const branding = useContext(BrandingContext);
  const [summary, setSummary] = React.useState<DashboardSummary | null>(null);

  React.useEffect(() => {
    import('./lib/api').then(m => m.api.get('/v1/dashboard/summary'))
      .then(res => setSummary(res.data))
      .catch(e => console.error(e));
  }, []);

  const quickActions = [
    { label: 'Emitir factura', to: '/invoices' },
    { label: 'Crear documento soporte', to: '/support-documents' },
    { label: 'Agregar cliente', to: '/customers' },
    { label: 'Agregar producto', to: '/products' },
  ];

  return (
    <div className="p-8 animate-in fade-in slide-in-from-bottom-4 duration-500">
      <div className="flex items-center justify-between mb-10">
        <div className="flex items-center gap-4 min-w-0">
          {branding?.invoiceLogoUrl ? (
            <img src={branding.invoiceLogoUrl} alt={branding.clientName} className="h-12 object-contain shrink-0" />
          ) : (
            <h1 className="text-3xl font-extrabold text-slate-800 tracking-tight truncate">{branding?.clientName || 'Tu empresa'}</h1>
          )}
          <p className="text-slate-500 hidden sm:block">Consulta la actividad de tu empresa este mes.</p>
        </div>
        <button onClick={() => navigate('/invoices')} className="shrink-0 px-5 py-3 bg-primary text-white font-bold rounded-xl shadow-primary hover:opacity-90 transition-opacity flex items-center gap-2">
          Emitir documento <ArrowRight size={18} />
        </button>
      </div>

      {!summary ? (
        <div className="flex justify-center items-center h-48 bg-white rounded-3xl border border-slate-100">
          <div className="animate-spin rounded-full h-8 w-8 border-t-2 border-b-2 border-primary"></div>
        </div>
      ) : (
        <>
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
            <div className="bg-white rounded-2xl p-6 shadow-sm border border-slate-100">
              <h3 className="text-slate-400 font-bold uppercase tracking-wider text-xs mb-2">Emitidos</h3>
              <p className="text-3xl font-black text-slate-800">{summary.totalIssued}</p>
            </div>
            <div className="bg-white rounded-2xl p-6 shadow-sm border border-slate-100">
              <h3 className="text-emerald-600 font-bold uppercase tracking-wider text-xs mb-2 flex items-center gap-1"><CheckCircle2 size={14} /> Aceptados</h3>
              <p className="text-3xl font-black text-slate-800">{summary.totalAccepted}</p>
            </div>
            <div className="bg-white rounded-2xl p-6 shadow-sm border border-slate-100">
              <h3 className="text-amber-600 font-bold uppercase tracking-wider text-xs mb-2 flex items-center gap-1"><Clock size={14} /> En validación</h3>
              <p className="text-3xl font-black text-slate-800">{summary.totalPending}</p>
            </div>
            <div className="bg-white rounded-2xl p-6 shadow-sm border border-slate-100">
              <h3 className="text-rose-600 font-bold uppercase tracking-wider text-xs mb-2 flex items-center gap-1"><AlertTriangle size={14} /> Requieren atención</h3>
              <p className="text-3xl font-black text-slate-800">{summary.totalRejected}</p>
            </div>
          </div>

          <div className="flex items-center gap-2 text-slate-500 mb-8">
            <DollarSign size={16} />
            <span className="text-sm">Total facturado este mes: <span className="font-bold text-slate-700">{money(summary.totalBilled)}</span></span>
          </div>

          <div
            className={`rounded-2xl p-6 mb-8 flex items-center justify-between gap-4 flex-wrap ${
              summary.pendingSetupItems.length === 0 ? 'bg-emerald-50 border border-emerald-100' : 'bg-amber-50 border border-amber-100'
            }`}
          >
            {summary.pendingSetupItems.length === 0 ? (
              <p className="text-emerald-700 font-medium flex items-center gap-2"><CheckCircle2 size={18} /> Tu cuenta está lista para facturar.</p>
            ) : (
              <div className="space-y-2">
                {summary.pendingSetupItems.map(item => {
                  const setup = SETUP_MESSAGES[item];
                  if (!setup) return null;
                  return (
                    <div key={item} className="flex items-center gap-3 flex-wrap">
                      <p className="text-amber-700 font-medium flex items-center gap-2"><AlertTriangle size={18} /> {setup.text}</p>
                      {setup.actionTo && (
                        <button onClick={() => navigate(setup.actionTo!)} className="text-sm font-bold text-amber-700 underline hover:text-amber-900">
                          {setup.actionLabel}
                        </button>
                      )}
                    </div>
                  );
                })}
              </div>
            )}
          </div>

          <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
            <div className="lg:col-span-2 bg-white rounded-3xl border border-slate-100 shadow-sm overflow-hidden">
              <h3 className="text-lg font-bold text-slate-800 px-6 pt-6 pb-4">Actividad reciente</h3>
              {summary.recentDocuments.length === 0 ? (
                <p className="text-slate-400 px-6 pb-6">Aún no tienes documentos aceptados por la DIAN.</p>
              ) : (
                <table className="w-full text-sm">
                  <tbody>
                    {summary.recentDocuments.map(doc => (
                      <tr key={doc.id} className="border-t border-slate-50">
                        <td className="px-6 py-3 text-slate-400 whitespace-nowrap">{new Date(doc.processedAt).toLocaleDateString('es-CO')}</td>
                        <td className="px-3 py-3 font-medium text-slate-700 whitespace-nowrap">{doc.typeCode}-{doc.number}</td>
                        <td className="px-3 py-3 text-slate-600 truncate max-w-[200px]">{doc.customerName || '-'}</td>
                        <td className="px-3 py-3 text-right font-bold text-slate-700 whitespace-nowrap">{money(doc.totalAmount)}</td>
                        <td className="px-6 py-3 text-right whitespace-nowrap">
                          <button onClick={() => navigate('/invoices')} className="text-primary font-bold hover:underline">Ver</button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
              <div className="px-6 py-4 border-t border-slate-50">
                <button onClick={() => navigate('/invoices')} className="text-primary font-bold hover:underline text-sm">Ver todos los documentos</button>
              </div>
            </div>

            <div className="space-y-6">
              <div className="bg-white rounded-3xl border border-slate-100 shadow-sm p-6">
                <h3 className="text-lg font-bold text-slate-800 mb-4">Acciones rápidas</h3>
                <div className="space-y-2">
                  {quickActions.map(action => (
                    <button
                      key={action.to}
                      onClick={() => navigate(action.to)}
                      className="w-full text-left px-4 py-3 rounded-xl border-2 border-slate-100 hover:border-primary/40 hover:bg-primary/5 transition-all font-medium text-slate-700"
                    >
                      {action.label}
                    </button>
                  ))}
                </div>
              </div>

              <div className="bg-white rounded-3xl border border-slate-100 shadow-sm p-6">
                <h3 className="text-lg font-bold text-slate-800 mb-4 flex items-center gap-2"><Wallet size={18} /> Consumo del período</h3>
                {summary.consumption.mode === 'PrepaidBag' ? (
                  <div className="space-y-2 text-sm">
                    <p className="text-slate-500">Bolsa activa desde el {new Date(summary.consumption.bagStartDate).toLocaleDateString('es-CO')}</p>
                    <p className="text-slate-700">Saldo disponible: <span className="font-bold">{money(summary.consumption.remainingBalance)}</span></p>
                    <p className="text-slate-400">de {money(summary.consumption.amountPaid)} pagados</p>
                  </div>
                ) : (
                  <p className="text-sm text-slate-500">Tarifa estándar: <span className="font-bold text-slate-700">{money(summary.consumption.pricePerDocument)}</span> por documento.</p>
                )}
              </div>
            </div>
          </div>
        </>
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
      <ConfirmDialogProvider>
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
                      <Route path="/templates/editor" element={<TemplateEditor />} />
                      <Route path="/resolutions" element={<ResolutionsSettings />} />
                      <Route path="/customers" element={<CustomersPage />} />
                      <Route path="/products" element={<ProductsPage />} />
                      <Route path="/invoices" element={<InvoicesPage />} />
                      <Route path="/support-documents" element={<SupportDocumentsPage />} />
                      <Route path="/payroll" element={<PayrollPage />} />
                      <Route path="/received-documents" element={<ReceivedDocumentsPage />} />
                      {/* Rutas ficticias para completar el sidebar */}
                      <Route path="/payments" element={<div className="p-8">Esta sección estará disponible próximamente.</div>} />
                    </Routes>
                  </Layout>
                </BrandingProvider>
              ) : (
                <Navigate to={`/login${localStorage.getItem('fel_client_tenant') ? `?tenant=${localStorage.getItem('fel_client_tenant')}` : ''}`} />
              )
            }
          />
        </Routes>
      </ConfirmDialogProvider>
    </Router>
  );
}

export default App;
