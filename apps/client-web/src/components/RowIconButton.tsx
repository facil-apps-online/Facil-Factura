import React from 'react';
import { Edit2, Eye, Trash2 } from 'lucide-react';

import { cn } from '@/lib/utils';

const ICONS = { edit: Edit2, view: Eye, delete: Trash2 } as const;

const TONES = {
  edit: 'hover:bg-blue-50 hover:text-blue-600',
  view: 'hover:bg-slate-100 hover:text-primary',
  delete: 'hover:bg-rose-50 hover:text-rose-600',
} as const;

interface RowIconButtonProps {
  action: keyof typeof ICONS;
  // Texto accesible; debe nombrar el registro ("Editar Juan Pérez"), porque el botón solo tiene icono.
  label: string;
  onClick: () => void;
}

// Botón de icono de las acciones de fila de un listado: 44 px en móvil/tableta (objetivo táctil) y
// compacto desde `md`, donde el puntero es de precisión.
export default function RowIconButton({ action, label, onClick }: RowIconButtonProps) {
  const Icon = ICONS[action];
  return (
    <button
      type="button"
      onClick={onClick}
      aria-label={label}
      title={label}
      className={cn('flex h-11 w-11 items-center justify-center rounded-lg text-slate-400 transition-colors lg:h-9 lg:w-9', TONES[action])}
    >
      <Icon size={18} />
    </button>
  );
}
