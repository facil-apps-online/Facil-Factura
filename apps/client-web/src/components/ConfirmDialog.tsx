import React, { createContext, useCallback, useContext, useRef, useState } from 'react';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { buttonVariants } from '@/components/ui/button';

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
      <AlertDialog open={state.open} onOpenChange={open => { if (!open) settle(false); }}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{state.title}</AlertDialogTitle>
            <AlertDialogDescription>{state.message}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel onClick={() => settle(false)}>{state.cancelText}</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => settle(true)}
              className={buttonVariants({ variant: state.destructive ? 'destructive' : 'default' })}
            >
              {state.confirmText}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </ConfirmContext.Provider>
  );
}

export function useConfirm(): ConfirmFn {
  const confirm = useContext(ConfirmContext);
  if (!confirm) throw new Error('useConfirm debe usarse dentro de <ConfirmDialogProvider>');
  return confirm;
}
