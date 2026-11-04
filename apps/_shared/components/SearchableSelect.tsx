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
  // Datos de la fila que la pantalla necesita al elegirla (en la búsqueda asíncrona no quedan en una lista local que consultar).
  data?: unknown;
}

interface CreateOption {
  label: string;
  onSelect: () => void;
}

interface SearchableSelectProps {
  options: SearchableSelectOption[];
  value: string;
  // Segundo argumento: la opción elegida (en modo asíncrono, para que la pantalla conserve su texto sin volver a pedirlo).
  onChange: (value: string, option?: SearchableSelectOption) => void;
  // Búsqueda en el servidor para listas que pueden tener miles de filas (terceros, productos): al abrir y al escribir (con una pausa breve) se piden
  // las primeras coincidencias, y `options` se ignora. Con una fila ya elegida que no aparece en los resultados, `selected` aporta su texto.
  loadOptions?: (query: string) => Promise<SearchableSelectOption[]>;
  selected?: SearchableSelectOption | null;
  // Cantidad de resultados con la que se avisa que hay más y conviene escribir para acotar.
  searchLimit?: number;
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

export default function SearchableSelect({ options, value, onChange, loadOptions, selected, searchLimit = 20, placeholder, emptyLabel, className, inputClassName, required, disabled, createOptions }: SearchableSelectProps) {
  const [query, setQuery] = useState('');
  const [isOpen, setIsOpen] = useState(false);
  const [highlighted, setHighlighted] = useState(0);
  const [coords, setCoords] = useState({ top: 0, left: 0, width: 0 });
  const containerRef = useRef<HTMLDivElement>(null);
  const dropdownRef = useRef<HTMLDivElement>(null);

  const [results, setResults] = useState<SearchableSelectOption[]>([]);
  const [searching, setSearching] = useState(false);
  // La última búsqueda falló (red, sesión, servidor): se avisa y se puede reintentar sin tener que cambiar el texto. reloadKey fuerza una búsqueda nueva.
  const [searchError, setSearchError] = useState(false);
  // Texto con el que se obtuvieron los resultados que hay en pantalla: sirve para saber si el Enter actuaría sobre una lista vieja.
  const [loadedQuery, setLoadedQuery] = useState<string | null>(null);
  const [reloadKey, setReloadKey] = useState(0);
  const [chosen, setChosen] = useState<SearchableSelectOption | null>(null);

  const selectedOption: SearchableSelectOption | undefined = (loadOptions ? undefined : options.find(o => o.value === value))
    ?? (selected && selected.value === value ? selected : undefined)
    ?? (chosen && chosen.value === value ? chosen : undefined)
    ?? (loadOptions ? results.find(o => o.value === value) : undefined);

  // Búsqueda asíncrona: al abrir (sin texto) trae lo primero; al escribir espera una pausa y descarta las respuestas que llegan tarde.
  useEffect(() => {
    if (!loadOptions || !isOpen) return;
    const text = query.trim();
    const q = selectedOption && text === closedValueOf(selectedOption) ? '' : text;
    let cancelled = false;
    setSearching(true);
    const timer = setTimeout(async () => {
      try {
        const found = await loadOptions(q);
        if (!cancelled) { setResults(found); setSearchError(false); setLoadedQuery(q); }
      } catch {
        if (!cancelled) { setResults([]); setSearchError(true); setLoadedQuery(q); }
      } finally {
        if (!cancelled) setSearching(false);
      }
    }, q === '' || reloadKey > 0 ? 0 : 250);
    return () => { cancelled = true; clearTimeout(timer); };
  }, [isOpen, query, reloadKey]); // eslint-disable-line react-hooks/exhaustive-deps

  // También cuando cambia el texto de la opción elegida sin cambiar su valor (p. ej. el tercero se editó y cambió de nombre).
  const closedText = selectedOption ? closedValueOf(selectedOption) : '';
  useEffect(() => {
    if (!isOpen) setQuery(closedText);
  }, [value, isOpen, closedText]);

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
  // Se vuelve a medir en cada cuadro mientras la lista está abierta: un diálogo que se anima al abrirse (entra desplazado y con escala) mueve el campo
  // después de que su lista ya se abrió (el campo recibe el foco al abrir el diálogo), y ni el scroll ni el cambio de tamaño avisan de ese movimiento.
  // Solo se actualiza el estado cuando la posición cambió, y el ciclo termina al cerrar la lista.
  useLayoutEffect(() => {
    if (!isOpen) return;
    let frame = 0;
    const updateCoords = () => {
      if (!containerRef.current) return;
      const rect = containerRef.current.getBoundingClientRect();
      const next = { top: rect.bottom + 4, left: rect.left, width: rect.width };
      setCoords((prev: { top: number; left: number; width: number }) => (prev.top === next.top && prev.left === next.left && prev.width === next.width ? prev : next));
    };
    const follow = () => {
      updateCoords();
      frame = requestAnimationFrame(follow);
    };
    follow();
    return () => cancelAnimationFrame(frame);
  }, [isOpen]);

  const filtered = useMemo(() => {
    if (loadOptions) return results;
    const q = query.trim().toLowerCase();
    if (!q || (selectedOption && q === closedValueOf(selectedOption).toLowerCase())) return options;
    return options.filter(o => o.label.toLowerCase().includes(q));
  }, [options, query, selectedOption, results, loadOptions]);

  const selectOption = (opt: SearchableSelectOption) => {
    setChosen(opt);
    onChange(opt.value, opt);
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
      // Un lector de códigos de barras escribe todo el código y cierra con Enter antes de que termine la búsqueda de ese texto: la lista en pantalla
      // todavía es la de otro texto, y elegir su primer ítem insertaba un producto equivocado. Mientras la búsqueda esté pendiente, el Enter no elige.
      const text = query.trim();
      const currentQuery = selectedOption && text === closedValueOf(selectedOption) ? '' : text;
      const resultsAreStale = !!loadOptions && (searching || loadedQuery !== currentQuery);
      if (isOpen && !resultsAreStale && filtered[highlighted]) selectOption(filtered[highlighted]);
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
          // Un campo que ya tiene el foco no vuelve a disparar onFocus (tras elegir una opción, Escape o un error): el clic lo reabre, y si la última búsqueda
          // falló la repite.
          onClick={() => {
            if (disabled) return;
            if (!isOpen) { setIsOpen(true); setHighlighted(0); }
            else if (searchError) setReloadKey(k => k + 1);
          }}
          // Salir con Tab deja de mostrar la lista (el clic fuera ya la cerraba).
          onBlur={() => { setIsOpen(false); setQuery(selectedOption ? closedValueOf(selectedOption) : ''); }}
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
          // Tocar la lista (barra de desplazamiento, márgenes) no debe quitarle el foco al campo: con onBlur cerraría la lista a media búsqueda.
          onMouseDown={e => e.preventDefault()}
          className="max-h-60 overflow-y-auto bg-white border border-slate-200 rounded-xl shadow-lg py-1"
        >
          {loadOptions && searchError && !searching && filtered.length === 0 && (
            <div
              onMouseDown={e => { e.preventDefault(); setReloadKey(k => k + 1); }}
              className="px-4 py-2 text-sm cursor-pointer text-red-600 hover:bg-red-50"
            >
              No se pudo buscar. Toca aquí para reintentar.
            </div>
          )}
          {filtered.length === 0 && !showCreateNew && !(loadOptions && searchError && !searching) && (
            <div className="px-4 py-2 text-sm text-slate-400">{loadOptions && searching ? 'Buscando…' : (emptyLabel || 'Sin resultados')}</div>
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
          {loadOptions && filtered.length >= searchLimit && (
            <div className="px-4 py-2 text-xs text-slate-400 border-t border-slate-100">Hay más resultados: escribe para acotar la búsqueda.</div>
          )}
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
