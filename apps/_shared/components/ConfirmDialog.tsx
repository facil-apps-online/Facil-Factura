import React, { createContext, useCallback, useContext, useRef, useState } from 'react';
import * as AlertDialog from '@radix-ui/react-alert-dialog';

interface ConfirmOptions {
  title?: string;
  confirmText?: string;
  cancelText?: string;
  // false para acciones importantes pero no destructivas (ej. "Publicar", "Enviar a la DIAN"):
  // el botón de confirmar queda en el color primario en vez de rojo.
  destructive?: boolean;
}

interface ConfirmState extends Required<ConfirmOptions> {
  open: boolean;
  message: string;
}

type ConfirmFn = (message: string, options?: ConfirmOptions) => Promise<boolean>;

const ConfirmContext = createContext<ConfirmFn | null>(null);

const initialState: ConfirmState = {
  open: false,
  message: '',
  title: 'Confirmar',
  confirmText: 'Confirmar',
  cancelText: 'Cancelar',
  destructive: true,
};

// Reemplazo de window.confirm(): mismo uso (`if (!(await confirm('¿Eliminar?'))) return;`) pero
// como diálogo propio del sitio en vez de uno del navegador. Un solo diálogo montado una vez en
// la raíz de la app resuelve la promesa que cada llamada a confirm() devuelve.
export function ConfirmDialogProvider({ children }: { children: React.ReactNode }) {
  const [state, setState] = useState<ConfirmState>(initialState);
  const resolveRef = useRef<(value: boolean) => void>(() => {});

  const confirm = useCallback<ConfirmFn>((message, options) => {
    return new Promise<boolean>(resolve => {
      resolveRef.current = resolve;
      setState({
        open: true,
        message,
        title: options?.title ?? initialState.title,
        confirmText: options?.confirmText ?? initialState.confirmText,
        cancelText: options?.cancelText ?? initialState.cancelText,
        destructive: options?.destructive ?? initialState.destructive,
      });
    });
  }, []);

  const settle = (value: boolean) => {
    setState(prev => ({ ...prev, open: false }));
    resolveRef.current(value);
  };

  return (
    <ConfirmContext.Provider value={confirm}>
      {children}
      <AlertDialog.Root open={state.open} onOpenChange={open => { if (!open) settle(false); }}>
        <AlertDialog.Portal>
          <AlertDialog.Overlay className="fixed inset-0 bg-slate-900/50 backdrop-blur-sm z-[100] data-[state=open]:animate-in data-[state=open]:fade-in" />
          <AlertDialog.Content className="fixed left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 z-[101] w-full max-w-sm bg-white rounded-3xl shadow-2xl p-6 data-[state=open]:animate-in data-[state=open]:fade-in data-[state=open]:zoom-in-95">
            <AlertDialog.Title className="text-lg font-bold text-slate-800">{state.title}</AlertDialog.Title>
            <AlertDialog.Description className="text-sm text-slate-500 mt-2">{state.message}</AlertDialog.Description>
            <div className="mt-6 flex justify-end gap-3">
              <AlertDialog.Cancel asChild>
                <button onClick={() => settle(false)} className="px-5 py-2 text-slate-600 font-bold hover:bg-slate-100 rounded-xl transition-colors">
                  {state.cancelText}
                </button>
              </AlertDialog.Cancel>
              <AlertDialog.Action asChild>
                <button
                  onClick={() => settle(true)}
                  className={`px-5 py-2 text-white font-bold rounded-xl shadow-md transition-colors ${
                    state.destructive ? 'bg-rose-600 hover:bg-rose-700' : 'bg-primary hover:bg-primary/90'
                  }`}
                >
                  {state.confirmText}
                </button>
              </AlertDialog.Action>
            </div>
          </AlertDialog.Content>
        </AlertDialog.Portal>
      </AlertDialog.Root>
    </ConfirmContext.Provider>
  );
}

export function useConfirm(): ConfirmFn {
  const confirm = useContext(ConfirmContext);
  if (!confirm) throw new Error('useConfirm debe usarse dentro de <ConfirmDialogProvider>');
  return confirm;
}
