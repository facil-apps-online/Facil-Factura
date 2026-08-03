import { useState } from 'react';
import { FileText, ShieldCheck, Zap, BarChart3, Smartphone, Users, Menu, X, Check, ArrowRight } from 'lucide-react';

function Navbar() {
  const [open, setOpen] = useState(false);

  const links = [
    { label: 'Características', href: '#features' },
    { label: 'Cómo funciona', href: '#how' },
    { label: 'Planes', href: '#pricing' },
    { label: 'Contacto', href: '#contact' },
  ];

  return (
    <header className="fixed top-0 inset-x-0 z-50 bg-white/80 backdrop-blur border-b border-slate-200">
      <nav className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 h-16 flex items-center justify-between">
        <a href="#" className="flex items-center gap-2">
          <span className="w-8 h-8 rounded-lg bg-primary flex items-center justify-center">
            <FileText size={18} className="text-white" />
          </span>
          <span className="text-lg font-bold tracking-tight">Facil <span className="text-primary">Factura</span></span>
        </a>

        <div className="hidden md:flex items-center gap-8">
          {links.map((l) => (
            <a key={l.href} href={l.href} className="text-sm font-medium text-slate-600 hover:text-slate-900">
              {l.label}
            </a>
          ))}
          <a
            href="https://tenants.facil-factura.pro"
            className="text-sm font-semibold text-slate-700 hover:text-primary"
          >
            Iniciar sesión
          </a>
          <a
            href="https://tenants.facil-factura.pro"
            className="bg-primary text-white text-sm font-semibold px-4 py-2 rounded-lg hover:bg-sky-700 transition"
          >
            Empieza gratis
          </a>
        </div>

        <button className="md:hidden p-2" onClick={() => setOpen(!open)} aria-label="Menú">
          {open ? <X size={24} /> : <Menu size={24} />}
        </button>
      </nav>

      {open && (
        <div className="md:hidden border-t border-slate-200 bg-white px-4 py-4 space-y-3">
          {links.map((l) => (
            <a key={l.href} href={l.href} onClick={() => setOpen(false)} className="block text-sm font-medium text-slate-700">
              {l.label}
            </a>
          ))}
          <a
            href="https://tenants.facil-factura.pro"
            className="block text-center bg-primary text-white text-sm font-semibold px-4 py-2 rounded-lg"
          >
            Empieza gratis
          </a>
        </div>
      )}
    </header>
  );
}

const features = [
  {
    icon: <Zap size={22} />,
    title: 'Emisión instantánea',
    desc: 'Genera facturas electrónicas al instante, con validación automática ante la DIAN y respuesta en segundos.',
  },
  {
    icon: <ShieldCheck size={22} />,
    title: 'Cumplimiento DIAN',
    desc: 'Totalmente alineado con la Facturación Electrónica de la DIAN: factura de venta, nota crédito, débito y más.',
  },
  {
    icon: <BarChart3 size={22} />,
    title: 'Reportes y estadísticas',
    desc: 'Visualiza tus ventas, impuestos y documentos emitidos con reportes claros y exportables.',
  },
  {
    icon: <Users size={22} />,
    title: 'Multi-empresa',
    desc: 'Gestiona varios NIT y empresas desde una sola cuenta. Ideal para contadores y holdings.',
  },
  {
    icon: <Smartphone size={22} />,
    title: 'Desde cualquier lugar',
    desc: 'Accede desde tu navegador en computadora, tableta o celular. Sin instalaciones.',
  },
  {
    icon: <FileText size={22} />,
    title: 'Documentos ilimitados',
    desc: 'Facturas, notas de crédito, débito, anulaciones y más. Plantillas personalizables con tu marca.',
  },
];

const steps = [
  {
    step: '1',
    title: 'Crea tu cuenta',
    desc: 'Regístrate en minutos y configura los datos de tu empresa (NIT, razón social y resolución DIAN).',
  },
  {
    step: '2',
    title: 'Configura tu certificado',
    desc: 'Carga tu certificado digital y resolución de la DIAN una sola vez. Nosotros gestionamos el resto.',
  },
  {
    step: '3',
    title: 'Emite y comparte',
    desc: 'Genera facturas con tu marca, envíalas por correo a tus clientes y archiva todo automáticamente.',
  },
];

const plans = [
  {
    name: 'Básico',
    price: '$39.900',
    period: '/mes',
    desc: 'Para emprendedores que emiten poco volumen.',
    features: ['Hasta 50 facturas/mes', '1 empresa', 'Factura de venta', 'Soporte por correo'],
    highlight: false,
  },
  {
    name: 'Profesional',
    price: '$99.900',
    period: '/mes',
    desc: 'Para negocios en crecimiento.',
    features: ['Facturas ilimitadas', 'Hasta 3 empresas', 'Notas crédito y débito', 'Reportes avanzados', 'Soporte prioritario'],
    highlight: true,
  },
  {
    name: 'Empresarial',
    price: '$199.900',
    period: '/mes',
    desc: 'Para contadores y empresas grandes.',
    features: ['Facturas ilimitadas', 'Empresas ilimitadas', 'API para integraciones', 'Soporte dedicado', 'Múltiples usuarios'],
    highlight: false,
  },
];

function App() {
  return (
    <div className="min-h-screen">
      <Navbar />

      {/* Hero */}
      <section className="pt-32 pb-20 bg-gradient-to-b from-sky-50 to-slate-50">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 text-center">
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-sky-100 text-sky-700 text-xs font-semibold mb-6">
            <ShieldCheck size={14} />
            Autorizado DIAN · Facturación Electrónica en Colombia
          </div>
          <h1 className="text-4xl sm:text-5xl lg:text-6xl font-extrabold tracking-tight text-slate-900 leading-tight">
            Facturación electrónica{' '}
            <span className="text-primary">sin complicaciones</span>
          </h1>
          <p className="mt-6 max-w-2xl mx-auto text-lg text-slate-600">
            Emite facturas electrónicas validadas por la DIAN en segundos, desde cualquier dispositivo.
            Facil Factura hace que facturar sea tan fácil como enviar un mensaje.
          </p>
          <div className="mt-8 flex flex-col sm:flex-row items-center justify-center gap-4">
            <a
              href="https://tenants.facil-factura.pro"
              className="bg-primary text-white font-semibold px-6 py-3 rounded-lg shadow-primary hover:bg-sky-700 transition inline-flex items-center gap-2"
            >
              Empieza gratis <ArrowRight size={18} />
            </a>
            <a
              href="#how"
              className="text-slate-700 font-semibold px-6 py-3 rounded-lg border border-slate-300 hover:border-slate-400 transition"
            >
              Ver cómo funciona
            </a>
          </div>
          <p className="mt-6 text-sm text-slate-500">Sin tarjeta de crédito · Configuración en minutos</p>
        </div>
      </section>

      {/* Características */}
      <section id="features" className="py-20 bg-white">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="text-center max-w-2xl mx-auto">
            <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-slate-900">
              Todo lo que necesitas para facturar
            </h2>
            <p className="mt-4 text-slate-600">
              Una plataforma completa para emitir, validar y gestionar tus documentos electrónicos ante la DIAN.
            </p>
          </div>
          <div className="mt-14 grid sm:grid-cols-2 lg:grid-cols-3 gap-8">
            {features.map((f) => (
              <div key={f.title} className="p-6 rounded-2xl border border-slate-200 hover:shadow-lg hover:border-sky-200 transition">
                <div className="w-12 h-12 rounded-xl bg-sky-100 text-primary flex items-center justify-center mb-4">
                  {f.icon}
                </div>
                <h3 className="text-lg font-semibold text-slate-900">{f.title}</h3>
                <p className="mt-2 text-sm text-slate-600 leading-relaxed">{f.desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Cómo funciona */}
      <section id="how" className="py-20 bg-slate-50">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="text-center max-w-2xl mx-auto">
            <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-slate-900">Cómo funciona</h2>
            <p className="mt-4 text-slate-600">Empieza a facturar en menos de 15 minutos.</p>
          </div>
          <div className="mt-14 grid md:grid-cols-3 gap-8">
            {steps.map((s) => (
              <div key={s.step} className="relative p-6 rounded-2xl bg-white border border-slate-200">
                <div className="w-10 h-10 rounded-full bg-primary text-white flex items-center justify-center font-bold mb-4">
                  {s.step}
                </div>
                <h3 className="text-lg font-semibold text-slate-900">{s.title}</h3>
                <p className="mt-2 text-sm text-slate-600 leading-relaxed">{s.desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Planes */}
      <section id="pricing" className="py-20 bg-white">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="text-center max-w-2xl mx-auto">
            <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-slate-900">Planes simples y claros</h2>
            <p className="mt-4 text-slate-600">Empieza gratis y escala cuando lo necesites.</p>
          </div>
          <div className="mt-14 grid md:grid-cols-3 gap-8">
            {plans.map((p) => (
              <div
                key={p.name}
                className={`relative p-8 rounded-2xl border transition ${
                  p.highlight
                    ? 'border-primary bg-gradient-to-b from-sky-50 to-white shadow-primary scale-105'
                    : 'border-slate-200 hover:border-slate-300'
                }`}
              >
                {p.highlight && (
                  <span className="absolute -top-3 left-1/2 -translate-x-1/2 bg-primary text-white text-xs font-bold px-3 py-1 rounded-full">
                    MÁS POPULAR
                  </span>
                )}
                <h3 className="text-lg font-semibold text-slate-900">{p.name}</h3>
                <p className="mt-1 text-sm text-slate-500">{p.desc}</p>
                <div className="mt-4 flex items-baseline gap-1">
                  <span className="text-4xl font-extrabold text-slate-900">{p.price}</span>
                  <span className="text-sm text-slate-500">{p.period}</span>
                </div>
                <ul className="mt-6 space-y-3">
                  {p.features.map((f) => (
                    <li key={f} className="flex items-start gap-2 text-sm text-slate-700">
                      <Check size={18} className="text-primary shrink-0 mt-0.5" />
                      {f}
                    </li>
                  ))}
                </ul>
                <a
                  href="https://tenants.facil-factura.pro"
                  className={`mt-8 block text-center font-semibold px-4 py-2.5 rounded-lg transition ${
                    p.highlight
                      ? 'bg-primary text-white hover:bg-sky-700'
                      : 'border border-slate-300 text-slate-700 hover:border-slate-400'
                  }`}
                >
                  Elegir {p.name}
                </a>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* CTA */}
      <section className="py-20 bg-slate-900">
        <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 text-center">
          <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-white">
            ¿Listo para facturar sin estrés?
          </h2>
          <p className="mt-4 text-slate-400 text-lg">
            Únete a las empresas que ya facturan con Facil Factura.
          </p>
          <a
            href="https://tenants.facil-factura.pro"
            className="mt-8 inline-flex items-center gap-2 bg-primary text-white font-semibold px-8 py-3.5 rounded-lg shadow-primary hover:bg-sky-600 transition"
          >
            Crear mi cuenta gratis <ArrowRight size={18} />
          </a>
        </div>
      </section>

      {/* Contacto */}
      <section id="contact" className="py-16 bg-white">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 grid md:grid-cols-3 gap-8 text-center md:text-left">
          <div>
            <h3 className="font-semibold text-slate-900">Facil Factura</h3>
            <p className="mt-2 text-sm text-slate-600">
              Plataforma de facturación electrónica DIAN para Colombia.
            </p>
          </div>
          <div>
            <h3 className="font-semibold text-slate-900">Producto</h3>
            <ul className="mt-2 space-y-1 text-sm text-slate-600">
              <li><a href="#features" className="hover:text-primary">Características</a></li>
              <li><a href="#pricing" className="hover:text-primary">Planes</a></li>
              <li><a href="https://clients.facil-factura.pro" className="hover:text-primary">Portal del cliente</a></li>
            </ul>
          </div>
          <div>
            <h3 className="font-semibold text-slate-900">Contacto</h3>
            <p className="mt-2 text-sm text-slate-600">
              ¿Dudas? Escríbenos a <a href="mailto:soporte@facil-apps.online" className="text-primary hover:underline">soporte@facil-apps.online</a>
            </p>
          </div>
        </div>
      </section>

      <footer className="py-6 border-t border-slate-200 bg-white">
        <p className="text-center text-sm text-slate-500">
          © {new Date().getFullYear()} Facil Apps Online. Todos los derechos reservados.
        </p>
      </footer>
    </div>
  );
}

export default App;
