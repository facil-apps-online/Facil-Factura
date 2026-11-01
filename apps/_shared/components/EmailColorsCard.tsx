import { useEffect, useRef, useState } from 'react';
import { Mail, Loader2 } from 'lucide-react';
import { toast } from 'sonner';

// Colores del correo con el que se entrega un documento electrónico. Lo usan los tres niveles que pueden definirlos (el cliente, el tenant y la
// plataforma, en ese orden de prioridad): cada uno se carga y se guarda con su propia ruta, así que el componente solo recibe las tres
// funciones. El visor muestra el correo tal cual saldría (lo arma el servidor con la misma plantilla que el envío real) y se actualiza al
// mover los colores, sin guardar ni enviar nada.

export interface EmailColors {
  light: string | null;
  dark: string | null;
  // El color que se usaría si este nivel no definiera los suyos (el del nivel superior o el azul por defecto).
  inherited: string;
}

const HEX = /^#[0-9a-fA-F]{6}$/;
const input = 'px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl outline-none focus:ring-2 focus:ring-primary font-mono text-sm w-28';

function messageOf(err: any, fallback: string): string {
  const data = err?.response?.data;
  if (typeof data?.message === 'string') return data.message;
  if (typeof data?.Message === 'string') return data.Message;
  if (typeof data === 'string' && data) return data;
  return fallback;
}

function ColorField({ label, value, onChange }: { label: string; value: string; onChange: (v: string) => void }) {
  return (
    <div>
      <label className="block text-sm font-bold text-slate-700 mb-1.5">{label}</label>
      <div className="flex items-center gap-3">
        <input type="color" value={HEX.test(value) ? value : '#000000'} onChange={e => onChange(e.target.value)} className="w-12 h-12 rounded cursor-pointer border-0 p-0" aria-label={label} />
        <input type="text" value={value} onChange={e => onChange(e.target.value)} maxLength={7} placeholder="#rrggbb" className={`${input} ${value && !HEX.test(value) ? 'border-rose-300' : ''}`} />
      </div>
    </div>
  );
}

export default function EmailColorsCard({ load, save, preview, inheritLabel, className = 'bg-white rounded-3xl p-8 shadow-sm border border-slate-100', buttonClass = 'bg-primary hover:opacity-90', iconClass = 'text-primary' }: {
  load: () => Promise<EmailColors>;
  save: (light: string | null, dark: string | null) => Promise<unknown>;
  preview: (light: string | null, dark: string | null) => Promise<string>;
  // Cómo se llama lo que se hereda: "los de tu proveedor", "los de la plataforma"...
  inheritLabel: string;
  className?: string;
  // Los portales con marca blanca usan su color principal; el de superadmin no lo tiene y pasa colores fijos.
  buttonClass?: string;
  iconClass?: string;
}) {
  const [loaded, setLoaded] = useState(false);
  const [custom, setCustom] = useState(false);
  const [light, setLight] = useState('');
  const [dark, setDark] = useState('');
  const [inherited, setInherited] = useState('#2563eb');
  const [html, setHtml] = useState('');
  const [saving, setSaving] = useState(false);
  const requestId = useRef(0);

  const colors = custom ? { light: light.trim() || null, dark: dark.trim() || null } : { light: null, dark: null };
  const valid = (!colors.light || HEX.test(colors.light)) && (!colors.dark || HEX.test(colors.dark)) && (!custom || !!colors.light || !!colors.dark);

  const refresh = () => {
    load()
      .then(c => {
        setInherited(c.inherited);
        setCustom(!!c.light || !!c.dark);
        setLight(c.light ?? '');
        setDark(c.dark ?? '');
        setLoaded(true);
      })
      .catch(() => toast.error('No se pudieron cargar los colores del correo'));
  };
  useEffect(refresh, []);

  // Visor en vivo: espera un momento a que se deje de mover el color y descarta respuestas que llegan tarde.
  useEffect(() => {
    if (!loaded || !valid) return;
    const id = ++requestId.current;
    const timer = setTimeout(() => {
      preview(colors.light, colors.dark)
        .then(result => { if (id === requestId.current) setHtml(result); })
        .catch(() => { if (id === requestId.current) setHtml(''); });
    }, 300);
    return () => clearTimeout(timer);
  }, [loaded, custom, light, dark]);

  const startCustom = () => {
    setCustom(true);
    if (!light && !dark) { setLight(inherited); setDark(''); }
  };

  const submit = async () => {
    setSaving(true);
    try {
      await save(colors.light, colors.dark);
      toast.success('Colores del correo guardados');
      refresh();
    } catch (err) {
      toast.error(messageOf(err, 'No se pudieron guardar los colores'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className={className}>
      <h3 className="text-lg font-bold text-slate-900 flex items-center gap-2 mb-1"><Mail className={`w-5 h-5 ${iconClass}`} /> Colores del correo de documentos</h3>
      <p className="text-sm text-slate-500 mb-5">
        Es el color de fondo del correo con el que se entregan las facturas, notas y demás documentos. El contenido no cambia. Si defines un color claro y uno oscuro,
        el correo usa el que más contraste tenga con la tarjeta blanca.
      </p>

      {!loaded ? (
        <div className="flex items-center gap-2 text-sm text-slate-400"><Loader2 className="w-4 h-4 animate-spin" /> Cargando...</div>
      ) : (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-8">
          <div className="space-y-5">
            <div className="space-y-2">
              <label className="flex items-center gap-2 text-sm text-slate-700 cursor-pointer">
                <input type="radio" name="email-colors-mode" checked={!custom} onChange={() => setCustom(false)} />
                Usar {inheritLabel} <span className="inline-block w-4 h-4 rounded border border-slate-200 align-middle" style={{ backgroundColor: inherited }} />
              </label>
              <label className="flex items-center gap-2 text-sm text-slate-700 cursor-pointer">
                <input type="radio" name="email-colors-mode" checked={custom} onChange={startCustom} />
                Definir mis colores
              </label>
            </div>

            {custom && (
              <div className="space-y-4">
                <ColorField label="Color claro" value={light} onChange={setLight} />
                <ColorField label="Color oscuro" value={dark} onChange={setDark} />
                {!valid && <p className="text-xs text-rose-500">Usa el formato #rrggbb (por ejemplo, #2563eb) y define al menos un color.</p>}
              </div>
            )}

            <button type="button" onClick={submit} disabled={saving || !valid} className={`px-5 py-3 rounded-xl font-bold text-white disabled:opacity-50 ${buttonClass}`}>
              {saving ? 'Guardando...' : 'Guardar colores'}
            </button>
          </div>

          <div>
            <p className="text-xs font-bold text-slate-400 uppercase mb-2">Así se ve el correo (con un documento de ejemplo)</p>
            {html
              ? <iframe title="Vista previa del correo" srcDoc={html} sandbox="" className="w-full h-[520px] rounded-xl border border-slate-200 bg-white" />
              : <div className="w-full h-[520px] rounded-xl border border-dashed border-slate-200 flex items-center justify-center text-sm text-slate-400">{valid ? 'Preparando la vista previa...' : 'Corrige los colores para ver la vista previa'}</div>}
          </div>
        </div>
      )}
    </div>
  );
}
