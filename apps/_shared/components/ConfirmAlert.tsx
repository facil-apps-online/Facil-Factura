import * as AlertDialog from '@radix-ui/react-alert-dialog';
import { Loader2 } from 'lucide-react';

interface ConfirmAlertProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description: string;
  confirmText?: string;
  // false para acciones que no destruyen nada: el botón queda azul en vez de rojo.
  destructive?: boolean;
  busy?: boolean;
  onConfirm: () => void;
  // 'dark' para el portal de superadmin; los demás son claros.
  theme?: 'light' | 'dark';
}

const THEMES = {
  light: { overlay: 'bg-slate-900/40', content: 'bg-white text-slate-800 border border-slate-200', muted: 'text-slate-500', cancel: 'border border-slate-200 bg-white text-slate-700 hover:bg-slate-50' },
  dark: { overlay: 'bg-black/60', content: 'bg-slate-900 text-slate-100 border border-slate-700', muted: 'text-slate-400', cancel: 'border border-slate-700 bg-slate-800 text-slate-200 hover:bg-slate-700' },
} as const;

const BUTTON = 'inline-flex min-h-10 items-center justify-center gap-2 rounded-xl px-4 py-2 text-sm font-semibold transition-colors disabled:cursor-not-allowed disabled:opacity-50';

// Confirmación como cuadro del sitio, en lugar de window.confirm(). Controlada: la pantalla guarda qué se va a confirmar
// y llama a onConfirm; el cuadro no se cierra solo, lo cierra la pantalla cuando termina la acción.
export default function ConfirmAlert({ open, onOpenChange, title, description, confirmText = 'Confirmar', destructive = true, busy = false, onConfirm, theme = 'light' }: ConfirmAlertProps) {
  const k = THEMES[theme];
  return (
    <AlertDialog.Root open={open} onOpenChange={v => { if (!busy) onOpenChange(v); }}>
      <AlertDialog.Portal>
        <AlertDialog.Overlay className={`fixed inset-0 z-[100] backdrop-blur-sm ${k.overlay}`} />
        <AlertDialog.Content className={`fixed left-1/2 top-1/2 z-[101] w-[calc(100%-2rem)] max-w-md -translate-x-1/2 -translate-y-1/2 space-y-4 rounded-2xl p-6 shadow-2xl ${k.content}`}>
          <div className="space-y-1.5">
            <AlertDialog.Title className="text-lg font-bold">{title}</AlertDialog.Title>
            <AlertDialog.Description className={`text-sm ${k.muted}`}>{description}</AlertDialog.Description>
          </div>
          <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
            <AlertDialog.Cancel asChild>
              <button type="button" disabled={busy} className={`${BUTTON} ${k.cancel}`}>Cancelar</button>
            </AlertDialog.Cancel>
            <button
              type="button"
              disabled={busy}
              onClick={onConfirm}
              className={`${BUTTON} text-white ${destructive ? 'bg-rose-600 hover:bg-rose-700' : 'bg-blue-600 hover:bg-blue-700'}`}
            >
              {busy && <Loader2 className="animate-spin" size={16} />}
              {confirmText}
            </button>
          </div>
        </AlertDialog.Content>
      </AlertDialog.Portal>
    </AlertDialog.Root>
  );
}
