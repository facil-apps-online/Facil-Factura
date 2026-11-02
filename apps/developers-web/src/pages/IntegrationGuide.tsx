import { useState } from 'react';
import { Copy, Check } from 'lucide-react';
import { toast } from 'sonner';
import { ERROR_CODES, LANGUAGES, OPERATIONS, type LanguageKey } from '../lib/integrationGuide';

function CodeBlock({ code }: { code: string }) {
  const [copied, setCopied] = useState(false);
  const copy = async () => {
    await navigator.clipboard.writeText(code);
    setCopied(true);
    toast.success('Código copiado');
    setTimeout(() => setCopied(false), 1500);
  };
  return (
    <div className="relative">
      <button onClick={copy} className="absolute top-2 right-2 p-1.5 rounded-md bg-slate-700 hover:bg-slate-600 text-slate-200" title="Copiar">
        {copied ? <Check size={14} /> : <Copy size={14} />}
      </button>
      <pre className="bg-slate-900 text-slate-100 text-xs rounded-xl p-4 overflow-x-auto"><code>{code}</code></pre>
    </div>
  );
}

export default function IntegrationGuide() {
  const [language, setLanguage] = useState<LanguageKey>('curl');

  return (
    <main className="flex-1 overflow-y-auto">
      <div className="max-w-4xl mx-auto px-6 py-10 space-y-8">
        <div>
          <h1 className="text-3xl font-bold text-slate-800">Guía de integración</h1>
          <p className="text-slate-500 mt-2">
            Tres pasos: emitir, consultar por el id (UUID) y descargar. La autenticación son solo dos headers, <code className="font-mono">x-api-key</code> y{' '}
            <code className="font-mono">x-api-secret</code>, siempre por HTTPS. Tú envías el consecutivo; el servidor calcula los totales y toma el emisor de tu API Key.
          </p>
        </div>

        <div className="flex gap-1 border-b border-slate-200">
          {LANGUAGES.map(l => (
            <button
              key={l.key}
              onClick={() => setLanguage(l.key)}
              className={`px-4 py-2 text-sm font-semibold border-b-2 transition-colors ${language === l.key ? 'border-primary text-primary' : 'border-transparent text-slate-500 hover:text-slate-700'}`}
            >
              {l.label}
            </button>
          ))}
        </div>

        {OPERATIONS.map((op, i) => (
          <section key={op.key} className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6 space-y-3">
            <div className="flex items-center gap-3 flex-wrap">
              <span className="text-xs font-bold text-slate-400">PASO {i + 1}</span>
              <h2 className="text-lg font-bold text-slate-800">{op.title}</h2>
              {op.comingSoon && <span className="text-xs font-semibold bg-amber-100 text-amber-700 px-2 py-0.5 rounded-full">Próximamente</span>}
            </div>
            <p className="text-sm text-slate-500">{op.description}</p>
            <p className="font-mono text-xs text-slate-700">
              <span className="font-bold">{op.method}</span> {op.path}
            </p>
            <CodeBlock code={op.samples[language]} />
          </section>
        ))}

        <section className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6">
          <h2 className="text-lg font-bold text-slate-800 mb-3">Errores frecuentes</h2>
          <table className="w-full text-sm">
            <tbody>
              {ERROR_CODES.map(e => (
                <tr key={e.code} className="border-t border-slate-100 align-top">
                  <td className="py-2 pr-3 font-mono text-xs text-slate-700">{e.code}</td>
                  <td className="py-2 pr-3 text-slate-500">{e.http}</td>
                  <td className="py-2 text-slate-600">{e.meaning}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>
      </div>
    </main>
  );
}
