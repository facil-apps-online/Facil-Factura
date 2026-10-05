import { BrowserRouter as Router, Routes, Route, Link, useLocation, useNavigate, Navigate } from 'react-router-dom';
import { Home, FileText, Settings, CreditCard, LogOut, FileSignature, Users, Package, Receipt, Banknote, Inbox, ChevronLeft, ChevronRight, Menu, X, CheckCircle2, Clock, AlertTriangle, DollarSign, ArrowRight, Wallet, UserCog } from 'lucide-react';
import React, { createContext, useContext, useEffect, useState } from 'react';
import { Toaster } from 'sonner';
import { ConfirmDialogProvider } from '@/components/ConfirmDialog';
import { api, BRANCH_STORAGE_KEY } from './lib/api';
import { SessionProvider, useSession } from './context/SessionContext';
import BranchSelector from './components/BranchSelector';
import { setDecimalSeparator, useNumberFormat } from './lib/numberFormat';

import TemplateSettings from './pages/TemplateSettings';
import TemplateEditor from './pages/TemplateEditor';
import ResolutionsSettings from './pages/ResolutionsSettings';
import CustomersPage from './pages/CustomersPage';
import ProductsPage from './pages/ProductsPage';
import InvoicesPage from './pages/InvoicesPage';
import SupportDocumentsPage from './pages/SupportDocumentsPage';
import PayrollPage from './pages/PayrollPage';
import ReceivedDocumentsPage from './pages/ReceivedDocumentsPage';
import UsersPage from './pages/UsersPage';
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
  taxId?: string;
  verificationDigit?: string;
  decimalSeparator?: string;
}

// NIT con separador de miles y DV con guion: 900123456 + 7 → "900.123.456-7".
function formatNit(taxId?: string, dv?: string): string {
  const digits = (taxId || '').replace(/\D/g, '');
  if (!digits) return '';
  const withDots = digits.replace(/\B(?=(\d{3})+(?!\d))/g, '.');
  return dv ? `${withDots}-${dv}` : withDots;
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
        setDecimalSeparator(res.data.decimalSeparator);
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

// Desde `lg` (1024 px) el menú es una columna fija; por debajo es un drawer.
function useIsDesktop(): boolean {
  const query = '(min-width: 1024px)';
  const [isDesktop, setIsDesktop] = useState(() => window.matchMedia(query).matches);
  useEffect(() => {
    const mql = window.matchMedia(query);
    const onChange = (e: MediaQueryListEvent) => setIsDesktop(e.matches);
    mql.addEventListener('change', onChange);
    return () => mql.removeEventListener('change', onChange);
  }, []);
  return isDesktop;
}

interface SidebarProps {
  onLogout: () => void;
  collapsed: boolean;
  onToggleCollapsed: () => void;
  isDesktop: boolean;
  mobileOpen: boolean;
  onCloseMobile: () => void;
}

function Sidebar({ onLogout, collapsed, onToggleCollapsed, isDesktop, mobileOpen, onCloseMobile }: SidebarProps) {
  const location = useLocation();
  const branding = useContext(BrandingContext);
  const logo = branding?.logoLightUrl || '/brand/isotipo-blanco.png';
  const name = branding?.companyName || 'Facil Factura';

  const { isAdministrator } = useSession();
  const allLinks: Array<{ to: string; icon: React.ReactNode; label: string; adminOnly?: boolean }> = [
    { to: "/", icon: <Home size={20} />, label: "Inicio" },
    { to: "/invoices", icon: <FileText size={20} />, label: "Mis Facturas" },
    { to: "/support-documents", icon: <Receipt size={20} />, label: "Documentos Soporte" },
    { to: "/payroll", icon: <Banknote size={20} />, label: "Nómina electrónica" },
    { to: "/received-documents", icon: <Inbox size={20} />, label: "Documentos recibidos", adminOnly: true },
    { to: "/customers", icon: <Users size={20} />, label: "Terceros" },
    { to: "/products", icon: <Package size={20} />, label: "Productos y servicios" },
    { to: "/payments", icon: <CreditCard size={20} />, label: "Pagos", adminOnly: true },
    { to: "/resolutions", icon: <FileSignature size={20} />, label: "Resoluciones DIAN", adminOnly: true },
    { to: "/settings", icon: <Settings size={20} />, label: "Diseño y Ajustes", adminOnly: true },
    { to: "/users", icon: <UserCog size={20} />, label: "Usuarios", adminOnly: true },
  ];
  const links = allLinks.filter(l => !l.adminOnly || isAdministrator);

  return (
    <aside
      aria-label="Menú principal"
      className={`bg-slate-900 text-slate-300 flex flex-col shadow-xl ${
        isDesktop
          ? `${collapsed ? 'w-20' : 'w-64'} h-full shrink-0 z-20 transition-all duration-200`
          : `fixed inset-y-0 left-0 w-64 max-w-[85vw] z-40 transition-[transform,visibility] duration-200 ${mobileOpen ? 'translate-x-0 visible' : '-translate-x-full invisible'}`
      }`}
    >
      <div className={`relative flex flex-col items-center gap-1.5 ${collapsed ? 'px-2 py-6' : 'px-6 py-6'}`}>
        {/* Logo del Tenant: solo manda el alto (máximo el mismo del logo del header, h-9) y el ancho
            no pasa del contenedor. */}
        <img src={logo} alt={name} className="max-h-9 max-w-full object-contain" />
        {!collapsed && <span className="text-xs font-medium text-slate-300 text-center max-w-full truncate">{name}</span>}
        {isDesktop && !collapsed && (
          <button
            onClick={onToggleCollapsed}
            title="Contraer menú"
            aria-label="Contraer menú"
            className="absolute top-2 right-2 p-1.5 rounded-lg text-slate-400 hover:bg-slate-800 hover:text-white transition-colors"
          >
            <ChevronLeft size={18} />
          </button>
        )}
        {!isDesktop && (
          <button
            onClick={onCloseMobile}
            aria-label="Cerrar menú"
            className="absolute top-2 right-2 p-2.5 rounded-lg text-slate-400 hover:bg-slate-800 hover:text-white transition-colors"
          >
            <X size={20} />
          </button>
        )}
      </div>
      {!collapsed && <p className="text-xs text-slate-500 mb-2 px-6 uppercase tracking-wider">Facturación</p>}
      {isDesktop && collapsed && (
        <button
          onClick={onToggleCollapsed}
          title="Expandir menú"
          aria-label="Expandir menú"
          className="mx-auto mb-2 p-1.5 rounded-lg text-slate-400 hover:bg-slate-800 hover:text-white transition-colors"
        >
          <ChevronRight size={18} />
        </button>
      )}

      <nav className="flex-1 px-4 space-y-2 mt-4 overflow-y-auto">
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
  const { selectedBranchId } = useSession();
  const location = useLocation();
  const isDesktop = useIsDesktop();
  const [mobileOpen, setMobileOpen] = useState(false);
  // Sin preferencia guardada, el menú arranca colapsado entre 1024 y 1279 px para que a ~1200 px
  // el contenido conserve ancho útil; desde 1280 px arranca expandido.
  const [collapsed, setCollapsed] = useState(() => {
    const stored = localStorage.getItem('fel_client_sidebar_collapsed');
    return stored !== null ? stored === '1' : window.innerWidth < 1280;
  });

  const toggleCollapsed = () => {
    setCollapsed(prev => {
      const next = !prev;
      localStorage.setItem('fel_client_sidebar_collapsed', next ? '1' : '0');
      return next;
    });
  };

  // El drawer se cierra al navegar, al pasar a escritorio y con Escape.
  useEffect(() => { setMobileOpen(false); }, [location.pathname, isDesktop]);
  useEffect(() => {
    if (!mobileOpen) return;
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') setMobileOpen(false); };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [mobileOpen]);

  return (
    <div className="flex h-dvh overflow-hidden bg-slate-50 font-sans">
      <Sidebar
        onLogout={onLogout}
        collapsed={isDesktop && collapsed}
        onToggleCollapsed={toggleCollapsed}
        isDesktop={isDesktop}
        mobileOpen={mobileOpen}
        onCloseMobile={() => setMobileOpen(false)}
      />
      {!isDesktop && mobileOpen && (
        <div className="fixed inset-0 z-30 bg-slate-900/50" aria-hidden="true" onClick={() => setMobileOpen(false)} />
      )}
      <main className="flex-1 flex flex-col min-w-0 relative z-10">
        <header className="h-16 shrink-0 bg-white border-b border-slate-200 flex items-center justify-between gap-3 px-4 sm:px-6 lg:px-8 shadow-sm">
          {!isDesktop && (
            <button
              onClick={() => setMobileOpen(true)}
              aria-label="Abrir menú"
              aria-expanded={mobileOpen}
              className="p-2.5 -ml-2 rounded-lg text-slate-600 hover:bg-slate-100 transition-colors shrink-0"
            >
              <Menu size={22} />
            </button>
          )}
          {/* El logo/nombre de acá es el del Client (el tercero facturador), no el del Tenant (que
              ya se muestra en el sidebar) — es lo que le deja claro al usuario en qué cliente está
              ubicado cuando el Tenant administra varios. */}
          <div className="flex items-center gap-3 min-w-0 flex-1">
            {branding?.invoiceLogoUrl && (
              <img src={branding.invoiceLogoUrl} alt={branding.clientName} className="h-9 max-w-[7rem] sm:max-w-[12rem] object-contain shrink-0" />
            )}
            <div className="min-w-0 leading-tight">
              <h1 className="text-sm font-semibold text-slate-800 truncate">{branding?.clientName}</h1>
              {formatNit(branding?.taxId, branding?.verificationDigit) && (
                <p className="text-xs text-slate-500 truncate">NIT {formatNit(branding?.taxId, branding?.verificationDigit)}</p>
              )}
            </div>
          </div>
          <div className="flex items-center gap-4 shrink-0">
            <BranchSelector />
            <div className="w-8 h-8 rounded-full bg-primary flex items-center justify-center text-white font-bold text-sm shadow-md">
              {name.substring(0, 2).toUpperCase()}
            </div>
          </div>
        </header>

        <div className="flex-1 overflow-auto bg-slate-50/50">
          <div className="max-w-screen-2xl mx-auto">
            {/* La clave por sucursal vuelve a montar la pantalla al cambiar de sucursal, así recarga sus datos. */}
            <React.Fragment key={selectedBranchId}>{children}</React.Fragment>
          </div>
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
    | { mode: 'Standard'; pricePerDocument: number }
    | null;
  pendingSetupItems: string[];
}

const SETUP_MESSAGES: Record<string, { text: string; actionLabel?: string; actionTo?: string }> = {
  'no-resolution': { text: 'Configura una resolución para empezar a facturar.', actionLabel: 'Configurar resolución', actionTo: '/resolutions' },
  'no-certificate': { text: 'Tu certificado digital no está vigente. Contacta a tu proveedor para renovarlo.' },
};

const Dashboard = () => {
  const fmt = useNumberFormat();
  const money = (value: number) => `$${fmt.number(Math.round(value), 3)}`;
  const navigate = useNavigate();
  const branding = useContext(BrandingContext);
  const { isAdministrator } = useSession();
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
    <div className="p-4 sm:p-6 lg:p-8 animate-in fade-in slide-in-from-bottom-4 duration-500">
      <div className="mb-6 flex flex-col gap-4 sm:mb-10 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex items-center gap-4 min-w-0">
          {branding?.invoiceLogoUrl ? (
            <img src={branding.invoiceLogoUrl} alt={branding.clientName} className="h-12 object-contain shrink-0" />
          ) : (
            <h1 className="text-2xl sm:text-3xl font-extrabold text-slate-800 tracking-tight truncate">{branding?.clientName || 'Tu empresa'}</h1>
          )}
          <p className="text-slate-500 hidden sm:block">Consulta la actividad de tu empresa este mes.</p>
        </div>
        <button onClick={() => navigate('/invoices')} className="shrink-0 px-5 py-3 bg-primary text-white font-bold rounded-xl shadow-primary hover:opacity-90 transition-opacity flex items-center justify-center gap-2">
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
            <div className="bg-white rounded-2xl p-4 sm:p-6 shadow-sm border border-slate-100">
              <h3 className="text-slate-400 font-bold uppercase tracking-wider text-xs mb-2">Emitidos</h3>
              <p className="text-2xl sm:text-3xl font-black text-slate-800">{summary.totalIssued}</p>
            </div>
            <div className="bg-white rounded-2xl p-4 sm:p-6 shadow-sm border border-slate-100">
              <h3 className="text-emerald-600 font-bold uppercase tracking-wider text-xs mb-2 flex items-center gap-1"><CheckCircle2 size={14} /> Aceptados</h3>
              <p className="text-2xl sm:text-3xl font-black text-slate-800">{summary.totalAccepted}</p>
            </div>
            <div className="bg-white rounded-2xl p-4 sm:p-6 shadow-sm border border-slate-100">
              <h3 className="text-amber-600 font-bold uppercase tracking-wider text-xs mb-2 flex items-center gap-1"><Clock size={14} /> En validación</h3>
              <p className="text-2xl sm:text-3xl font-black text-slate-800">{summary.totalPending}</p>
            </div>
            <div className="bg-white rounded-2xl p-4 sm:p-6 shadow-sm border border-slate-100">
              <h3 className="text-rose-600 font-bold uppercase tracking-wider text-xs mb-2 flex items-center gap-1"><AlertTriangle size={14} /> Requieren atención</h3>
              <p className="text-2xl sm:text-3xl font-black text-slate-800">{summary.totalRejected}</p>
            </div>
          </div>

          <div className="flex items-center gap-2 text-slate-500 mb-8">
            <DollarSign size={16} />
            <span className="text-sm">Total facturado este mes: <span className="font-bold text-slate-700">{money(summary.totalBilled)}</span></span>
          </div>

          <div
            className={`rounded-2xl p-4 sm:p-6 mb-8 flex items-center justify-between gap-4 flex-wrap ${
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
                      {setup.actionTo && isAdministrator && (
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

          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 lg:gap-8">
            <div className="lg:col-span-2 bg-white rounded-3xl border border-slate-100 shadow-sm overflow-hidden">
              <h3 className="text-lg font-bold text-slate-800 px-4 sm:px-6 pt-6 pb-4">Actividad reciente</h3>
              {summary.recentDocuments.length === 0 ? (
                <p className="text-slate-400 px-4 sm:px-6 pb-6">Aún no tienes documentos aceptados por la DIAN.</p>
              ) : (
                <ul>
                  {summary.recentDocuments.map(doc => (
                    <li key={doc.id} className="flex items-start justify-between gap-3 border-t border-slate-50 px-4 py-3 sm:px-6">
                      <div className="min-w-0">
                        <p className="font-medium text-slate-700">
                          {doc.typeCode}-{doc.number}
                          <span className="ml-2 text-xs font-normal text-slate-400">{new Date(doc.processedAt).toLocaleDateString('es-CO')}</span>
                        </p>
                        <p className="truncate text-sm text-slate-600">{doc.customerName || '-'}</p>
                      </div>
                      <div className="shrink-0 text-right">
                        <p className="text-sm font-bold text-slate-700">{money(doc.totalAmount)}</p>
                        <button onClick={() => navigate('/invoices')} className="px-1 py-2 text-sm font-bold text-primary hover:underline">Ver</button>
                      </div>
                    </li>
                  ))}
                </ul>
              )}
              <div className="px-4 sm:px-6 py-2 border-t border-slate-50">
                <button onClick={() => navigate('/invoices')} className="py-2 text-primary font-bold hover:underline text-sm">Ver todos los documentos</button>
              </div>
            </div>

            <div className="space-y-6">
              <div className="bg-white rounded-3xl border border-slate-100 shadow-sm p-4 sm:p-6">
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

              {summary.consumption && (
                <div className="bg-white rounded-3xl border border-slate-100 shadow-sm p-4 sm:p-6">
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
              )}
            </div>
          </div>
        </>
      )}
    </div>
  );
};

// Pantallas de administración: el Facturador no las ve en el menú y, si entra por la URL, vuelve al inicio.
function RequireAdmin({ children }: { children: React.ReactNode }) {
  const { isAdministrator } = useSession();
  return isAdministrator ? <>{children}</> : <Navigate to="/" replace />;
}

function App() {
  const [isAuthenticated, setIsAuthenticated] = useState(
    !!localStorage.getItem('fel_client_auth')
  );

  const handleLogout = () => {
    localStorage.removeItem('fel_client_auth');
    localStorage.removeItem('fel_client_id');
    localStorage.removeItem(BRANCH_STORAGE_KEY);
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
                <SessionProvider>
                  <BrandingProvider>
                    <Layout onLogout={handleLogout}>
                      <Routes>
                        <Route path="/" element={<Dashboard />} />
                        <Route path="/settings" element={<RequireAdmin><TemplateSettings /></RequireAdmin>} />
                        <Route path="/templates/editor" element={<RequireAdmin><TemplateEditor /></RequireAdmin>} />
                        <Route path="/resolutions" element={<RequireAdmin><ResolutionsSettings /></RequireAdmin>} />
                        <Route path="/customers" element={<CustomersPage />} />
                        <Route path="/products" element={<ProductsPage />} />
                        <Route path="/invoices" element={<InvoicesPage />} />
                        <Route path="/support-documents" element={<SupportDocumentsPage />} />
                        <Route path="/payroll" element={<PayrollPage />} />
                        <Route path="/received-documents" element={<RequireAdmin><ReceivedDocumentsPage /></RequireAdmin>} />
                        <Route path="/users" element={<RequireAdmin><UsersPage /></RequireAdmin>} />
                        {/* Rutas ficticias para completar el sidebar */}
                        <Route path="/payments" element={<RequireAdmin><div className="p-8">Esta sección estará disponible próximamente.</div></RequireAdmin>} />
                      </Routes>
                    </Layout>
                  </BrandingProvider>
                </SessionProvider>
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
