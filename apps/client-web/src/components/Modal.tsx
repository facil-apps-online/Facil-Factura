import React from 'react';

import {
  Dialog,
  DialogBody,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { cn } from '@/lib/utils';

const SIZE_CLASSES = {
  sm: 'sm:max-w-md',
  md: 'sm:max-w-2xl',
  lg: 'sm:max-w-4xl',
  xl: 'sm:max-w-6xl',
} as const;

interface ModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: React.ReactNode;
  description?: React.ReactNode;
  // Botones de acción: quedan fijos al pie, visibles aunque el cuerpo tenga mucho contenido.
  footer?: React.ReactNode;
  size?: keyof typeof SIZE_CLASSES;
  // true cuando cerrar con clic fuera o Escape puede hacer perder datos de un formulario.
  preventDismiss?: boolean;
  // true cuando el contenido tiene selectores que se pintan fuera del modal (SearchableSelect y el
  // autocompletado de Google Maps usan portales sobre <body>). Un Dialog modal de Radix les quita
  // los clics y les roba el foco, así que se usa en modo no modal, con overlay propio. Como el clic
  // en esos selectores cuenta como "fuera", implica `preventDismiss`.
  withFloatingPickers?: boolean;
  children: React.ReactNode;
}

// Contrato común de los modales del portal: panel casi a pantalla completa en móvil, alto máximo
// en dvh, encabezado y pie fijos y scroll solo en el cuerpo. Foco, Escape, bloqueo de scroll y
// retorno del foco los resuelve Radix.
export default function Modal({
  open,
  onOpenChange,
  title,
  description,
  footer,
  size = 'md',
  preventDismiss = false,
  withFloatingPickers = false,
  children,
}: ModalProps) {
  const blockDismiss = preventDismiss || withFloatingPickers;
  return (
    <Dialog open={open} onOpenChange={onOpenChange} modal={!withFloatingPickers}>
      <DialogContent
        className={cn('w-[calc(100%-1rem)]', SIZE_CLASSES[size])}
        nonModalOverlay={withFloatingPickers}
        // Sin descripción, se anula el aria-describedby para que Radix no avise de que falta.
        {...(description ? {} : { 'aria-describedby': undefined })}
        onInteractOutside={blockDismiss ? e => e.preventDefault() : undefined}
        onEscapeKeyDown={blockDismiss ? e => e.preventDefault() : undefined}
      >
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          {description && <DialogDescription>{description}</DialogDescription>}
        </DialogHeader>
        <DialogBody>{children}</DialogBody>
        {footer && <DialogFooter>{footer}</DialogFooter>}
      </DialogContent>
    </Dialog>
  );
}
