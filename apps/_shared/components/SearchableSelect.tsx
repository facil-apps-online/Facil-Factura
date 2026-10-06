import React, { useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { ChevronDown, PlusCircle } from 'lucide-react';

export interface SearchableSelectOption {
  value: string;
  // Texto contra el que se filtra al escribir, y valor de respaldo para la fila del desplegable
  // y el valor ya seleccionado si no se indican displayLabel/shortLabel.
  label: string;
  // Si se indica, reemplaza a `label` en la fila del desplegable y como valor ya seleccionado
  // (útil cuando `label` necesita ser más largo solo para que la búsqueda encuentre por más
  // texto, ej. "CODIGO - Nombre", pero mostrar solo "CODIGO").
  displayLabel?: string;
  // Si se indica, reemplaza a displayLabel/label SOLO como valor ya seleccionado (la fila del
  // desplegable sigue usando displayLabel/label) — útil para colapsar un detalle largo (ej.
  // "Retención de industria y comercio (ReteICA 1%)") a algo corto una vez elegido (ej. "1%").
  shortLabel?: string;
}

interface CreateOption {
  label: string;
  onSelect: () => void;
}

interface SearchableSelectProps {
  options: SearchableSelectOption[];
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  emptyLabel?: string;
  className?: string;
  inputClassName?: string;
  required?: boolean;
  // Bloqueado: se ve el valor pero no se abre ni se puede cambiar (formularios de credenciales en modo solo lectura).
  disabled?: boolean;
  // Una o más acciones "+ Agregar ..." al final de la lista (cuando hay texto escrito) — por
  // ejemplo, ofrecer "crear como código" y "crear como nombre" a la vez, en vez de una sola.
  createOptions?: (query: string) => CreateOption[];
}

// <select> nativo no permite filtrar escribiendo (solo salta a la primera opción que empieza con
// la letra tecleada). Este combobox liviano reemplaza esos selects donde la lista puede crecer
// mucho (terceros, productos, empleados) sin agregar ninguna librería nueva al proyecto.
// Valor a mostrar cuando el combobox está cerrado (colapsado): shortLabel si viene, si no
// displayLabel, si no label.
function closedValueOf(opt: SearchableSelectOption): string {
  return opt.shortLabel ?? opt.displayLabel ?? opt.label;
}

// Valor a mostrar en cada fila del desplegable: displayLabel si viene, si no label (shortLabel
// NO aplica aquí — es solo para el valor ya colapsado).
function rowValueOf(opt: SearchableSelectOption): string {
  return opt.displayLabel ?? opt.label;
}

export default function SearchableSelect({ options, value, onChange, placeholder, emptyLabel, className, inputClassName, required, disabled, createOptions }: SearchableSelectProps) {
  const [query, setQuery] = useState('');
  const [isOpen, setIsOpen] = useState(false);
  const [highlighted, setHighlighted] = useState(0);
  const [coords, setCoords] = useState({ top: 0, left: 0, width: 0 });
  const containerRef = useRef<HTMLDivElement>(null);
  const dropdownRef = useRef<HTMLDivElement>(null);

  const selectedOption = options.find(o => o.value === value);

  useEffect(() => {
    if (!isOpen) setQuery(selectedOption ? closedValueOf(selectedOption) : '');
  }, [value, isOpen]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      const target = e.target as Node;
      const insideContainer = containerRef.current && containerRef.current.contains(target);
      const insideDropdown = dropdownRef.current && dropdownRef.current.contains(target);
      if (!insideContainer && !insideDropdown) {
        setIsOpen(false);
        setQuery(selectedOption ? closedValueOf(selectedOption) : '');
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [selectedOption]);

  // El popup se renderiza en un portal sobre <body> con position:fixed, para que nunca quede
  // recortado por un ancestro con overflow:hidden/auto (tablas, modales, filas expandibles).
  // Como ya no vive dentro del flujo del contenedor, hay que recalcular su posición a mano.
  useLayoutEffect(() => {
    if (!isOpen) return;
    const updateCoords = () => {
      if (!containerRef.current) return;
      const rect = containerRef.current.getBoundingClientRect();
      setCoords({ top: rect.bottom + 4, left: rect.left, width: rect.width });
    };
    updateCoords();
    window.addEventListener('scroll', updateCoords, true);
    window.addEventListener('resize', updateCoords);
    return () => {
      window.removeEventListener('scroll', updateCoords, true);
      window.removeEventListener('resize', updateCoords);
    };
  }, [isOpen]);

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q || (selectedOption && q === closedValueOf(selectedOption).toLowerCase())) return options;
    return options.filter(o => o.label.toLowerCase().includes(q));
  }, [options, query, selectedOption]);

  const selectOption = (opt: SearchableSelectOption) => {
    onChange(opt.value);
    setQuery(closedValueOf(opt));
    setIsOpen(false);
  };

  const trimmedQuery = query.trim();
  const createOptionsList = createOptions && trimmedQuery.length > 0 && (!selectedOption || trimmedQuery.toLowerCase() !== closedValueOf(selectedOption).toLowerCase())
    ? createOptions(trimmedQuery)
    : [];
  const showCreateNew = createOptionsList.length > 0;

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      setIsOpen(true);
      setHighlighted(h => Math.min(h + 1, filtered.length - 1));
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setHighlighted(h => Math.max(h - 1, 0));
    } else if (e.key === 'Enter') {
      e.preventDefault();
      if (isOpen && filtered[highlighted]) selectOption(filtered[highlighted]);
    } else if (e.key === 'Escape') {
      setIsOpen(false);
      setQuery(selectedOption ? closedValueOf(selectedOption) : '');
    }
  };

  return (
    <div ref={containerRef} className={`relative ${className || ''}`}>
      <div className="relative">
        <input
          type="text"
          required={required && !value}
          value={query}
          placeholder={placeholder || 'Buscar...'}
          readOnly={disabled}
          autoComplete="off"
          onFocus={() => { if (disabled) return; setIsOpen(true); setHighlighted(0); }}
          onChange={e => { if (disabled) return; setQuery(e.target.value); setIsOpen(true); setHighlighted(0); if (value) onChange(''); }}
          onKeyDown={handleKeyDown}
          className={inputClassName || 'w-full px-4 py-2 pr-8 border rounded-xl focus:ring-2 focus:ring-primary outline-none bg-white'}
        />
        <ChevronDown size={16} className="absolute right-2.5 top-1/2 -translate-y-1/2 text-slate-400 pointer-events-none" />
      </div>
      {isOpen && !disabled && createPortal(
        <div
          ref={dropdownRef}
          style={{ position: 'fixed', top: coords.top, left: coords.left, width: coords.width, zIndex: 9999 }}
          className="max-h-60 overflow-y-auto bg-white border border-slate-200 rounded-xl shadow-lg py-1"
        >
          {filtered.length === 0 && !showCreateNew && (
            <div className="px-4 py-2 text-sm text-slate-400">{emptyLabel || 'Sin resultados'}</div>
          )}
          {filtered.map((opt, idx) => (
            <div
              key={opt.value}
              onMouseDown={e => { e.preventDefault(); selectOption(opt); }}
              onMouseEnter={() => setHighlighted(idx)}
              className={`px-4 py-2 text-sm cursor-pointer ${idx === highlighted ? 'bg-primary/10 text-primary' : 'text-slate-700'} ${opt.value === value ? 'font-bold' : ''}`}
            >
              {rowValueOf(opt)}
            </div>
          ))}
          {createOptionsList.map((createOpt, idx) => (
            <div
              key={`create-${idx}`}
              onMouseDown={e => { e.preventDefault(); createOpt.onSelect(); setIsOpen(false); }}
              className="px-4 py-2 text-sm cursor-pointer text-primary font-bold flex items-center gap-1.5 border-t border-slate-100"
            >
              <PlusCircle size={14} />
              {createOpt.label}
            </div>
          ))}
        </div>,
        document.body
      )}
    </div>
  );
}
