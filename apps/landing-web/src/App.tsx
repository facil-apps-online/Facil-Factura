import { useState } from 'react';
import { FileText, ShieldCheck, Zap, BarChart3, Smartphone, Users, Menu, X, Check, ArrowRight, Layers, Code2, Banknote, Palette } from 'lucide-react';

function Navbar() {
  const [open, setOpen] = useState(false);

  const links = [
    { label: 'Documentos', href: '#documents' },
    { label: 'Para integradores', href: '#for-integrators' },
    { label: 'Tarifas', href: '#pricing' },
    { label: 'Cómo funciona', href: '#how' },
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
            Crear cuenta
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
            Crear cuenta
          </a>
        </div>
      )}
    </header>
  );
}

const documentGroups = [
  {
    icon: <FileText size={22} />,
    title: 'Facturación de venta DIAN',
    desc: 'Facturas de venta estándar, AIU, mandatos, exportación y contingencia (facturador y DIAN).',
    entity: 'DIAN',
  },
  {
    icon: <ShieldCheck size={22} />,
    title: 'Notas crédito y débito',
    desc: 'Anula, corrige o ajusta tus documentos con notas electrónicas ante la DIAN.',
    entity: 'DIAN',
  },
  {
    icon: <Layers size={22} />,
    title: 'Documentos equivalentes',
    desc: 'Tiquete POS, cine, transporte aéreo y terrestre, peajes, servicios públicos, espectáculos y más.',
    entity: 'DIAN',
  },
  {
    icon: <Users size={22} />,
    title: 'RIPS sector salud',
    desc: 'Registro Individual de Prestación de Servicios de Salud con validación clínica CIE-10 y CUPS (sexo, edad y congruencia).',
    entity: 'MINSALUD',
  },
  {
    icon: <Banknote size={22} />,
    title: 'Nómina electrónica',
    desc: 'Documentos de nómina y sus notas de ajuste, conforme a la resolución vigente.',
    entity: 'DIAN',
  },
  {
    icon: <BarChart3 size={22} />,
    title: 'Documento soporte',
    desc: 'Adquisiciones a no obligados de facturar y sus notas de ajuste.',
    entity: 'DIAN',
  },
];

const features = [
  {
    icon: <Code2 size={22} />,
    title: 'API para integración',
    desc: 'Endpoints REST con autenticación HMAC (clave/llave de prueba y producción) para emitir facturas, notas, nómina, RIPS y más desde tu propio software.',
  },
  {
    icon: <Palette size={22} />,
    title: 'Marca propia (white label)',
    desc: 'Cada integrador factura con su propia marca: logo, colores y portal personalizado para sus clientes.',
  },
  {
    icon: <Users size={22} />,
    title: 'Portal de clientes',
    desc: 'Tus clientes gestionan sus resoluciones, certificados, productos y emiten documentos sin ver el núcleo de la plataforma.',
  },
  {
    icon: <ShieldCheck size={22} />,
    title: 'Habilitación DIAN',
    desc: 'Acompañamiento para la habilitación de cada cliente ante la DIAN: software propio, PIN y entorno de pruebas.',
  },
  {
    icon: <Zap size={22} />,
    title: 'Procesamiento asíncrono',
    desc: 'Envías el documento y la plataforma lo procesa en cola, firmándolo y transmitiéndolo a la DIAN.',
  },
  {
    icon: <Smartphone size={22} />,
    title: 'Desde cualquier lugar',
    desc: 'Accede desde tu navegador en computadora, tableta o celular. Sin instalaciones.',
  },
];

const steps = [
  {
    step: '1',
    title: 'Crea tu cuenta',
    desc: 'Regístrate e ingresa tus datos fiscales (NIT, DV, régimen y actividad económica).',
  },
  {
    step: '2',
    title: 'Habilita tu cliente',
    desc: 'Cada cliente se habilita ante la DIAN con su software propio, PIN y certificado digital.',
  },
  {
    step: '3',
    title: 'Emite por API o portal',
    desc: 'Genera documentos con tu marca desde el portal o intégralos a tu software vía API.',
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
            Facturación electrónica DIAN y RIPS (MinSalud) para Colombia
          </div>
          <h1 className="text-4xl sm:text-5xl lg:text-6xl font-extrabold tracking-tight text-slate-900 leading-tight">
            Facturación electrónica{' '}
            <span className="text-primary">para integradores</span>
          </h1>
          <p className="mt-6 max-w-2xl mx-auto text-lg text-slate-600">
            Facil Factura es la plataforma de facturación electrónica de Colombia pensada para
            software houses, contadores y emprendedores que facturan para sí mismos y para sus clientes.
            Cubre facturación DIAN y RIPS del sector salud, paga por documento, integra por API y revende con tu propia marca.
          </p>
          <div className="mt-8 flex flex-col sm:flex-row items-center justify-center gap-4">
            <a
              href="https://tenants.facil-factura.pro"
              className="bg-primary text-white font-semibold px-6 py-3 rounded-lg shadow-primary hover:bg-sky-700 transition inline-flex items-center gap-2"
            >
              Crear cuenta gratis <ArrowRight size={18} />
            </a>
            <a
              href="#pricing"
              className="text-slate-700 font-semibold px-6 py-3 rounded-lg border border-slate-300 hover:border-slate-400 transition"
            >
              Ver tarifas
            </a>
          </div>
          <p className="mt-6 text-sm text-slate-500">Paga solo por documento emitido · Desde $20 COP</p>
        </div>
      </section>

      {/* Documentos soportados */}
      <section id="documents" className="py-20 bg-white">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="text-center max-w-2xl mx-auto">
            <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-slate-900">
              Todos los documentos que exigen la DIAN y MinSalud
            </h2>
            <p className="mt-4 text-slate-600">
              Soporte para 27 tipos de documento: facturación de venta, notas, documentos equivalentes,
              RIPS del sector salud, nómina y documento soporte.
            </p>
          </div>
          <div className="mt-14 grid sm:grid-cols-2 lg:grid-cols-3 gap-8">
            {documentGroups.map((f) => (
              <div key={f.title} className="p-6 rounded-2xl border border-slate-200 hover:shadow-lg hover:border-sky-200 transition">
                <div className="w-12 h-12 rounded-xl bg-sky-100 text-primary flex items-center justify-center mb-4">
                  {f.icon}
                </div>
                <div className="flex items-center gap-2 mb-2">
                  <h3 className="text-lg font-semibold text-slate-900">{f.title}</h3>
                </div>
                <span className={`inline-block text-[10px] font-bold uppercase tracking-wider px-2 py-0.5 rounded-full mb-2 ${f.entity === 'MINSALUD' ? 'bg-emerald-100 text-emerald-700' : 'bg-sky-100 text-sky-700'}`}>
                  {f.entity}
                </span>
                <p className="text-sm text-slate-600 leading-relaxed">{f.desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Para integradores */}
      <section id="for-integrators" className="py-20 bg-slate-50">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="text-center max-w-2xl mx-auto">
            <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-slate-900">
              Una plataforma, dos caminos
            </h2>
            <p className="mt-4 text-slate-600">
              Úsala para tu propia facturación o conviértela en un negocio de reventa.
            </p>
          </div>
          <div className="mt-14 grid md:grid-cols-2 gap-8">
            <div className="p-8 rounded-2xl border border-slate-200 bg-white">
              <h3 className="text-xl font-bold text-slate-900 flex items-center gap-2">
                <Code2 size={22} className="text-primary" /> Integración por API
              </h3>
              <p className="mt-3 text-slate-600">
                Conecta la facturación a tu propio software. Obtienes credenciales de prueba y producción
                con firma HMAC para emitir documentos de manera programática y rastrear su estado.
              </p>
              <ul className="mt-6 space-y-3">
                {['Endpoints REST para facturas, notas, nómina y RIPS', 'Ambientes de prueba y producción', 'Procesamiento asíncrono con tracking de estado', 'Firma XML XAdES y transmisión DIAN gestionadas'].map((t) => (
                  <li key={t} className="flex items-start gap-2 text-sm text-slate-700">
                    <Check size={18} className="text-primary shrink-0 mt-0.5" />
                    {t}
                  </li>
                ))}
              </ul>
            </div>
            <div className="p-8 rounded-2xl border border-primary bg-gradient-to-b from-sky-50 to-white">
              <h3 className="text-xl font-bold text-slate-900 flex items-center gap-2">
                <Palette size={22} className="text-primary" /> Reventa white label
              </h3>
              <p className="mt-3 text-slate-600">
                Si eres contador, software house o consultor, revende la facturación a tus clientes con tu
                propia marca. Tú fijas el precio por documento y la plataforma gestiona todo lo técnico.
              </p>
              <ul className="mt-6 space-y-3">
                {['Portal personalizado con tu logo y colores', 'Tarifas por documento por cada cliente', 'Portal de clientes con resolución y certificado', 'Cobro mensual por documentos emitidos'].map((t) => (
                  <li key={t} className="flex items-start gap-2 text-sm text-slate-700">
                    <Check size={18} className="text-primary shrink-0 mt-0.5" />
                    {t}
                  </li>
                ))}
              </ul>
            </div>
          </div>
        </div>
      </section>

      {/* Tarifas */}
      <section id="pricing" className="py-20 bg-white">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="text-center max-w-2xl mx-auto">
            <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-slate-900">
              Paga por documento, no por suscripción
            </h2>
            <p className="mt-4 text-slate-600">
              Cuanto más facturas, menos pagas. Sin cuotas fijas ni compromisos.
            </p>
          </div>
          <div className="mt-14 grid md:grid-cols-3 gap-8">
            <div className="p-8 rounded-2xl border border-slate-200 bg-slate-50 text-center">
              <h3 className="text-3xl font-extrabold text-slate-900">Por volumen</h3>
              <p className="mt-2 text-sm text-slate-600">Cobra por documento emitido, sin cuotas fijas.</p>
            </div>
            <div className="p-8 rounded-2xl border border-primary bg-gradient-to-b from-sky-50 to-white shadow-primary text-center">
              <h3 className="text-3xl font-extrabold text-primary">Desde $20</h3>
              <p className="mt-2 text-sm text-slate-700 font-medium">COP por documento</p>
            </div>
            <div className="p-8 rounded-2xl border border-slate-200 bg-slate-50 text-center">
              <h3 className="text-3xl font-extrabold text-slate-900">Cuanto más, menos</h3>
              <p className="mt-2 text-sm text-slate-600">El precio por documento baja a mayor volumen.</p>
            </div>
          </div>
          <p className="mt-8 text-sm text-slate-500 text-center">
            Los precios de reventa a tus clientes son decisión tuya. Contáctanos para volúmenes y tarifas detalladas.
          </p>
        </div>
      </section>

      {/* Cómo funciona */}
      <section id="how" className="py-20 bg-slate-50">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="text-center max-w-2xl mx-auto">
            <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-slate-900">Cómo funciona</h2>
            <p className="mt-4 text-slate-600">Empieza a facturar en minutos.</p>
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

      {/* CTA */}
      <section className="py-20 bg-slate-900">
        <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 text-center">
          <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-white">
            ¿Listo para facturar y revender?
          </h2>
          <p className="mt-4 text-slate-400 text-lg">
            Únete a los integradores que ya facturan con Facil Factura.
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
              Plataforma de facturación electrónica DIAN y RIPS (MinSalud) para Colombia.
            </p>
          </div>
          <div>
            <h3 className="font-semibold text-slate-900">Producto</h3>
            <ul className="mt-2 space-y-1 text-sm text-slate-600">
              <li><a href="#documents" className="hover:text-primary">Documentos</a></li>
              <li><a href="#pricing" className="hover:text-primary">Tarifas</a></li>
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
