import React, { useEffect, useMemo, useRef, useState } from 'react';
import { ArrowDown, ArrowUp, ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight, ChevronsUpDown, Search, X } from 'lucide-react';

// `wide` = 1400 px y `ultra` = 1800 px: puntos intermedios para tablas anchas, que con el menú lateral
// expandido (256 px) no caben en 1280 px aunque ahí ya sea "escritorio".
type TableFrom = 'md' | 'lg' | 'xl' | 'wide' | '2xl' | 'ultra';

type SortValue = string | number | boolean | Date | null | undefined;

export interface DataTableColumn<T> {
  key: string;
  header: string;
  // Contenido de la celda en la tabla y del valor en la tarjeta.
  render: (row: T) => React.ReactNode;
  // Alineación de la columna en la tabla (encabezado y celdas); números y montos van a la derecha.
  align?: 'left' | 'right';
  // Clases solo de las celdas de la tabla (tipografía, color); no se aplican al encabezado.
  cellClassName?: string;
  // Columna que encabeza la tarjeta (identificación/estado) en vez de ir en la cuadrícula de datos.
  primary?: boolean;
  // false para no mostrarla como dato en la tarjeta (se sigue viendo en la tabla).
  showInCard?: boolean;
  // true para que en la tarjeta ocupe todo el ancho (contenido largo, como botones o chips).
  cardFullWidth?: boolean;
  // Oculta la columna en la tabla por debajo de este ancho, para que la tabla no se desborde en
  // pantallas medianas. Es solo para datos secundarios: siguen en la tarjeta y en el detalle.
  hideBelow?: 'xl' | '2xl' | 'ultra';
  // Valor por el que se ordena. Sin él la columna no es ordenable (acciones, botones, etc.).
  // Texto se compara sin distinguir tildes ni mayúsculas y con los números en orden natural
  // ("FE-2" antes de "FE-10"); null/undefined/'' van siempre al final.
  sortValue?: (row: T) => SortValue;
  // Texto de la columna que entra en la búsqueda. Sin él la columna no se busca.
  searchValue?: (row: T) => string | number | null | undefined;
}

export interface DataTableSort {
  key: string;
  dir: 'asc' | 'desc';
}

// Lo que el servidor necesita para devolver la página que se ve (modo `server`).
export interface DataTableQuery {
  search: string;
  sort: DataTableSort | null;
  page: number;
  pageSize: number;
}

interface DataTableProps<T> {
  rows: T[];
  columns: DataTableColumn<T>[];
  rowKey: (row: T) => string;
  // Acciones de la fila (editar, eliminar...). En la tarjeta quedan al pie con objetivo táctil.
  actions?: (row: T) => React.ReactNode;
  // Desde qué ancho de pantalla se usa la tabla; por debajo se usan tarjetas. Hay que contar con el
  // menú lateral (256 px expandido, 80 px colapsado) y el padding de la página: una tabla de ~5
  // columnas cabe desde `lg`/`xl`, una de 9 columnas con acciones solo desde `xl`.
  tableFrom?: TableFrom;
  emptyMessage?: React.ReactNode;
  onRowClick?: (row: T) => void;
  // Casilla de selección por fila (y "seleccionar todas" en el encabezado de la tabla) para las acciones masivas.
  selection?: {
    isSelected: (row: T) => boolean;
    onToggle: (row: T) => void;
    // Casilla del encabezado: marca o desmarca las filas de la página que se está viendo. El componente
    // calcula si están todas marcadas; la pantalla solo agrega o quita esas filas de su selección.
    onToggleMany: (rows: T[], select: boolean) => void;
    // Nombre accesible de la casilla de una fila ("Seleccionar factura FE-12").
    label: (row: T) => string;
  };
  // Muestra el cuadro de búsqueda (texto del placeholder). Busca en las columnas con `searchValue`,
  // o en lo que devuelva `searchText` si se indica. Todas las palabras deben aparecer, en cualquier orden.
  search?: string;
  searchText?: (row: T) => string;
  // Controles propios de la pantalla (actualizar, filtros) a la derecha de la búsqueda.
  toolbar?: React.ReactNode;
  // Orden con el que arranca; sin él se respeta el orden en que llegan las filas.
  defaultSort?: DataTableSort;
  // Opciones de "filas por página" y la inicial. La elección se recuerda (por `pageSizeKey`).
  pageSizes?: number[];
  defaultPageSize?: number;
  pageSizeKey?: string;
  // false para mostrar todas las filas sin paginar (listas cortas por naturaleza).
  paginate?: boolean;
  loading?: boolean;
  // 'dark' para el portal de superadmin; los demás son claros.
  theme?: 'light' | 'dark';
  // Se avisa cuando cambia el conjunto visible tras buscar (por si la pantalla muestra totales).
  onFilteredChange?: (rows: T[]) => void;
  // Cuando cambia (filtros propios de la pantalla: estado, rango de fechas...), vuelve a la primera página.
  resetKey?: unknown;
  // Modo servidor, para listas de miles de filas: `rows` trae solo la página visible y la tabla no busca, ordena ni corta en el navegador. Cada
  // cambio (búsqueda, con una pausa breve; orden; página; filas por página; resetKey) y la primera vez se avisa en `onQueryChange` para que la
  // pantalla pida esa página; `total` es la cantidad de filas que cumplen la búsqueda en todo el servidor. Las columnas ordenables siguen
  // marcándose con `sortValue` (aquí solo indica que se puede ordenar; el orden lo hace el servidor por la `key` de la columna).
  server?: { total: number; onQueryChange: (query: DataTableQuery) => void };
}

// Los nombres de clase completos deben estar en el código para que Tailwind los genere.
const TABLE_VISIBILITY: Record<TableFrom, string> = {
  md: 'hidden md:block', lg: 'hidden lg:block', xl: 'hidden xl:block', wide: 'hidden min-[1400px]:block', '2xl': 'hidden 2xl:block', ultra: 'hidden min-[1800px]:block',
};
const CARDS_VISIBILITY: Record<TableFrom, string> = {
  md: 'md:hidden', lg: 'lg:hidden', xl: 'xl:hidden', wide: 'min-[1400px]:hidden', '2xl': '2xl:hidden', ultra: 'min-[1800px]:hidden',
};
const HIDE_BELOW: Record<'xl' | '2xl' | 'ultra', string> = {
  xl: 'hidden xl:table-cell', '2xl': 'hidden 2xl:table-cell', ultra: 'hidden min-[1800px]:table-cell',
};

// Color de marca del portal (los portales lo definen como variable CSS) con azul por defecto.
const BRAND = 'rgb(var(--color-primary, 37 99 235))';

const join = (...parts: Array<string | false | null | undefined>) => parts.filter(Boolean).join(' ');

// Sin tildes ni mayúsculas, para que "jose" encuentre "José".
const normalize = (value: string) => value.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();

const isEmpty = (v: SortValue) => v === null || v === undefined || v === '';

function compare(a: SortValue, b: SortValue): number {
  if (a instanceof Date) a = a.getTime();
  if (b instanceof Date) b = b.getTime();
  if (typeof a === 'boolean') a = a ? 1 : 0;
  if (typeof b === 'boolean') b = b ? 1 : 0;
  if (typeof a === 'number' && typeof b === 'number') return a - b;
  return String(a).localeCompare(String(b), 'es', { numeric: true, sensitivity: 'base' });
}

const DEFAULT_PAGE_SIZES = [10, 25, 50, 100];

function readPageSize(key: string, allowed: number[], fallback: number): number {
  try {
    const stored = Number(window.localStorage.getItem(key));
    if (allowed.includes(stored)) return stored;
  } catch { /* sin almacenamiento: se usa el valor por defecto */ }
  return fallback;
}

// Casilla con área táctil de 44 px; no dispara el clic de la fila.
function SelectBox({ checked, indeterminate, label, onChange, inTable }: { checked: boolean; indeterminate?: boolean; label: string; onChange: () => void; inTable?: boolean }) {
  return (
    // En la tabla el área táctil sobresale de la celda (margen negativo) para no agrandar la fila.
    <label className={join('flex h-11 w-11 cursor-pointer items-center justify-center', inTable && '-m-3')} onClick={e => e.stopPropagation()}>
      <input
        type="checkbox"
        checked={checked}
        ref={el => { if (el) el.indeterminate = !!indeterminate && !checked; }}
        onChange={onChange}
        aria-label={label}
        className="h-4 w-4 cursor-pointer"
        style={{ accentColor: BRAND }}
      />
    </label>
  );
}

// Portales claros (tenant, clientes) y el de superadmin, que es oscuro.
const THEMES = {
  light: {
    panel: 'rounded-2xl border border-slate-200 bg-white',
    head: 'bg-slate-50 text-slate-500',
    divide: 'divide-slate-100',
    rowHover: 'hover:bg-slate-50',
    rowSelected: 'bg-blue-50/60',
    cell: '',
    sortActive: 'text-slate-800',
    sortHover: 'hover:text-slate-800',
    sortIdle: 'text-slate-300 group-hover:text-slate-500',
    empty: 'rounded-2xl border border-slate-200 bg-white p-8 text-center text-sm text-slate-500',
    input: 'border-slate-200 bg-white text-slate-800 placeholder:text-slate-400',
    select: 'border-slate-200 bg-white text-slate-700',
    muted: 'text-slate-500',
    strong: 'text-slate-700',
    icon: 'text-slate-400',
    clear: 'text-slate-400 hover:text-slate-600',
    pager: 'border-slate-200 bg-white text-slate-600 hover:bg-slate-50 disabled:hover:bg-white',
    card: 'rounded-2xl border border-slate-200 bg-white shadow-sm',
    cardActive: 'active:bg-slate-50',
    cardSelected: 'border-blue-300 bg-blue-50/60',
    cardTitle: 'text-slate-800',
    cardLabel: 'text-slate-400',
    cardValue: 'text-slate-700',
    cardDivider: 'border-slate-100',
  },
  dark: {
    panel: 'glass-panel rounded-3xl',
    head: 'bg-slate-900/50 text-slate-400',
    divide: 'divide-slate-800/50',
    rowHover: 'hover:bg-slate-800/30',
    rowSelected: 'bg-indigo-500/10',
    cell: 'text-slate-300',
    sortActive: 'text-white',
    sortHover: 'hover:text-white',
    sortIdle: 'text-slate-600 group-hover:text-slate-400',
    empty: 'glass-panel rounded-3xl p-8 text-center text-sm text-slate-400',
    input: 'border-slate-700 bg-slate-800/50 text-white placeholder:text-slate-500',
    select: 'border-slate-700 bg-slate-800/50 text-slate-200',
    muted: 'text-slate-400',
    strong: 'text-slate-200',
    icon: 'text-slate-500',
    clear: 'text-slate-500 hover:text-slate-300',
    pager: 'border-slate-700 bg-slate-800/50 text-slate-300 hover:bg-slate-700/50 disabled:hover:bg-slate-800/50',
    card: 'glass-panel rounded-2xl',
    cardActive: 'active:bg-slate-800/40',
    cardSelected: 'border-indigo-500/50 bg-indigo-500/10',
    cardTitle: 'text-white',
    cardLabel: 'text-slate-500',
    cardValue: 'text-slate-300',
    cardDivider: 'border-slate-700/50',
  },
} as const;

const pagerButton = (theme: 'light' | 'dark') =>
  join('inline-flex h-9 min-w-9 items-center justify-center rounded-lg border px-2 text-sm transition-colors disabled:cursor-not-allowed disabled:opacity-40', THEMES[theme].pager);

// Tabla de consulta común a los tres portales: búsqueda, orden por encabezado, paginación y, por
// debajo de `tableFrom`, tarjetas. Las dos presentaciones se renderizan siempre y el CSS decide
// cuál se ve, así no hay parpadeo ni lógica de ancho en JS. Todo el trabajo se hace en el
// navegador sobre las filas recibidas: sirve para cientos o pocos miles de filas; una lista que
// crezca más allá de eso debe paginarse en el servidor.
export default function DataTable<T>({
  rows,
  columns,
  rowKey,
  actions,
  tableFrom = 'lg',
  emptyMessage = 'No hay registros para mostrar.',
  onRowClick,
  selection,
  search,
  searchText,
  toolbar,
  defaultSort,
  pageSizes = DEFAULT_PAGE_SIZES,
  defaultPageSize = 25,
  pageSizeKey = 'ff-table-page-size',
  paginate = true,
  loading = false,
  theme = 'light',
  onFilteredChange,
  resetKey,
  server,
}: DataTableProps<T>) {
  const k = THEMES[theme];
  const [query, setQuery] = useState('');
  const [sort, setSort] = useState<DataTableSort | null>(defaultSort ?? null);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(() => readPageSize(pageSizeKey, pageSizes, pageSizes.includes(defaultPageSize) ? defaultPageSize : pageSizes[0]));

  const serverMode = !!server;
  // En modo servidor la búsqueda se pide tras una pausa breve, no con cada tecla.
  const [debouncedQuery, setDebouncedQuery] = useState('');
  useEffect(() => {
    if (!serverMode) return;
    const timer = setTimeout(() => setDebouncedQuery(query), query.trim() === '' ? 0 : 300);
    return () => clearTimeout(timer);
  }, [serverMode, query]);

  const filtered = useMemo(() => {
    if (serverMode) return rows;
    const terms = normalize(query).split(/\s+/).filter(Boolean);
    if (terms.length === 0) return rows;
    return rows.filter(row => {
      const text = searchText
        ? searchText(row)
        : columns.map(c => c.searchValue?.(row)).filter(v => v !== null && v !== undefined).join(' ');
      const haystack = normalize(text);
      return terms.every(t => haystack.includes(t));
    });
  }, [rows, query, columns, searchText, serverMode]);

  const sorted = useMemo(() => {
    const column = sort && columns.find(c => c.key === sort.key);
    if (serverMode || !sort || !column?.sortValue) return filtered;
    const sign = sort.dir === 'asc' ? 1 : -1;
    const get = column.sortValue;
    // Array.sort es estable: filas con igual valor conservan el orden en que llegaron.
    return [...filtered].sort((x, y) => {
      const a = get(x);
      const b = get(y);
      if (isEmpty(a) && isEmpty(b)) return 0;
      if (isEmpty(a)) return 1;
      if (isEmpty(b)) return -1;
      return sign * compare(a, b);
    });
  }, [filtered, sort, columns, serverMode]);

  useEffect(() => { onFilteredChange?.(filtered); }, [filtered, onFilteredChange]);

  const total = serverMode ? server!.total : sorted.length;
  const pageCount = paginate ? Math.max(1, Math.ceil(total / pageSize)) : 1;
  // En modo servidor la página va atada a la búsqueda, el orden, el tamaño y el filtro de la pantalla con los que se eligió: si alguno cambia,
  // vuelve a la primera de una vez (sin pedir primero una página que ya no corresponde).
  const filterToken = JSON.stringify([debouncedQuery.trim(), sort, pageSize, resetKey]);
  const [serverPage, setServerPage] = useState({ page: 1, token: filterToken });
  const requestedPage = serverMode ? (serverPage.token === filterToken ? serverPage.page : 1) : page;
  const currentPage = Math.min(requestedPage, pageCount);
  const goToPage = (n: number) => (serverMode ? setServerPage({ page: n, token: filterToken }) : setPage(n));
  const start = paginate ? (currentPage - 1) * pageSize : 0;
  const visible = serverMode ? rows : (paginate ? sorted.slice(start, start + pageSize) : sorted);

  // Buscar, ordenar o cambiar el tamaño devuelve a la primera página; si la lista se encoge, se ajusta sola.
  useEffect(() => { setPage(1); }, [query, sort, pageSize, resetKey]);
  useEffect(() => { if (page > pageCount) setPage(pageCount); }, [page, pageCount]);

  // Modo servidor: avisa a la pantalla qué página pedir (también la primera vez).
  const onQueryChangeRef = useRef(server?.onQueryChange);
  onQueryChangeRef.current = server?.onQueryChange;
  useEffect(() => {
    if (!serverMode) return;
    onQueryChangeRef.current?.({ search: debouncedQuery.trim(), sort, page: currentPage, pageSize });
  }, [serverMode, debouncedQuery, sort, currentPage, pageSize, resetKey]);

  const toggleSort = (key: string) => {
    setSort(prev => {
      if (!prev || prev.key !== key) return { key, dir: 'asc' };
      if (prev.dir === 'asc') return { key, dir: 'desc' };
      // Tercer clic: vuelve al orden inicial de la pantalla.
      return defaultSort ?? null;
    });
  };

  const changePageSize = (size: number) => {
    setPageSize(size);
    try { window.localStorage.setItem(pageSizeKey, String(size)); } catch { /* ignorar */ }
  };

  const allVisibleSelected = !!selection && visible.length > 0 && visible.every(selection.isSelected);
  const someVisibleSelected = !!selection && visible.some(selection.isSelected);

  const primary = columns.find(c => c.primary);
  const dataColumns = columns.filter(c => !c.primary && c.showInCard !== false);
  const sortable = columns.filter(c => c.sortValue);
  const showToolbar = !!search || !!toolbar || sortable.length > 0;
  const searching = (serverMode ? debouncedQuery : query).trim().length > 0;
  const showFooter = paginate && total > Math.min(...pageSizes);
  const ariaSort = (c: DataTableColumn<T>) => (sort?.key === c.key ? (sort.dir === 'asc' ? 'ascending' : 'descending') : 'none');

  const emptyBox = (content: React.ReactNode) => (
    <div className={k.empty}>{content}</div>
  );

  return (
    <div className="space-y-3">
      {showToolbar && (
        <div className="flex flex-wrap items-center gap-3">
          {search && (
            <div className="relative w-full sm:w-96">
              <Search className={join('pointer-events-none absolute left-3 top-1/2 -translate-y-1/2', k.icon)} size={18} />
              <input
                type="search"
                value={query}
                onChange={e => setQuery(e.target.value)}
                placeholder={search}
                aria-label={search}
                className={join('h-10 w-full rounded-lg border pl-10 pr-9 text-sm focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-500/20 [&::-webkit-search-cancel-button]:hidden', k.input)}
              />
              {searching && (
                <button type="button" onClick={() => setQuery('')} aria-label="Limpiar búsqueda" className={join('absolute right-2 top-1/2 -translate-y-1/2 rounded p-1', k.clear)}>
                  <X size={16} />
                </button>
              )}
            </div>
          )}
          {/* En tarjetas no hay encabezados que pulsar: el orden se elige aquí. */}
          {sortable.length > 0 && (
            <div className={join('flex items-center gap-2 text-sm', k.muted, CARDS_VISIBILITY[tableFrom])}>
              <label htmlFor="datatable-sort">Ordenar por</label>
              <select
                id="datatable-sort"
                value={sort ? `${sort.key}|${sort.dir}` : ''}
                onChange={e => {
                  if (!e.target.value) { setSort(defaultSort ?? null); return; }
                  const [key, dir] = e.target.value.split('|');
                  setSort({ key, dir: dir as 'asc' | 'desc' });
                }}
                className={join('h-10 rounded-lg border px-2 text-sm', k.select)}
              >
                <option value="">Orden original</option>
                {sortable.map(c => (
                  <React.Fragment key={c.key}>
                    <option value={`${c.key}|asc`}>{c.header} (A → Z)</option>
                    <option value={`${c.key}|desc`}>{c.header} (Z → A)</option>
                  </React.Fragment>
                ))}
              </select>
            </div>
          )}
          {toolbar && <div className="ml-auto flex items-center gap-2">{toolbar}</div>}
        </div>
      )}

      {rows.length === 0 ? (
        emptyBox(loading ? 'Cargando…' : serverMode && searching ? <>Ningún resultado coincide con «{debouncedQuery.trim()}».</> : emptyMessage)
      ) : total === 0 ? (
        emptyBox(<>Ningún resultado coincide con «{query.trim()}».</>)
      ) : (
        <>
          <div className={join('overflow-x-auto', k.panel, TABLE_VISIBILITY[tableFrom])}>
            <table className="w-full text-left text-sm">
              <thead className={join('text-xs uppercase tracking-wider', k.head)}>
                <tr>
                  {selection && (
                    <th className="w-10 px-3 py-3">
                      <SelectBox inTable checked={allVisibleSelected} indeterminate={someVisibleSelected} label="Seleccionar todas las filas de la página" onChange={() => selection.onToggleMany(visible, !allVisibleSelected)} />
                    </th>
                  )}
                  {columns.map(c => (
                    <th
                      key={c.key}
                      aria-sort={c.sortValue ? ariaSort(c) : undefined}
                      className={join('px-3 py-3 font-semibold', c.align === 'right' && 'text-right', c.hideBelow && HIDE_BELOW[c.hideBelow])}
                    >
                      {c.sortValue ? (
                        <button
                          type="button"
                          onClick={() => toggleSort(c.key)}
                          className={join('group inline-flex items-center gap-1 font-semibold uppercase tracking-wider', k.sortHover, c.align === 'right' && 'flex-row-reverse', sort?.key === c.key && k.sortActive)}
                        >
                          {c.header}
                          {sort?.key === c.key
                            ? (sort.dir === 'asc' ? <ArrowUp size={13} /> : <ArrowDown size={13} />)
                            : <ChevronsUpDown size={13} className={k.sortIdle} />}
                        </button>
                      ) : c.header}
                    </th>
                  ))}
                  {actions && <th className="px-3 py-3 text-right font-semibold">Acciones</th>}
                </tr>
              </thead>
              <tbody className={join('divide-y', k.divide)}>
                {visible.map(row => (
                  <tr
                    key={rowKey(row)}
                    onClick={onRowClick ? () => onRowClick(row) : undefined}
                    className={join(k.rowHover, onRowClick && 'cursor-pointer', selection?.isSelected(row) && k.rowSelected)}
                  >
                    {selection && (
                      <td className="w-10 px-3 py-3">
                        <SelectBox inTable checked={selection.isSelected(row)} label={selection.label(row)} onChange={() => selection.onToggle(row)} />
                      </td>
                    )}
                    {columns.map(c => (
                      <td key={c.key} className={join('px-3 py-3 align-middle break-words', k.cell, c.align === 'right' && 'text-right', c.hideBelow && HIDE_BELOW[c.hideBelow], c.cellClassName)}>{c.render(row)}</td>
                    ))}
                    {actions && (
                      <td className="px-3 py-3" onClick={e => e.stopPropagation()}>
                        {/* Fila flex: sin ella los botones (display:flex) se apilan uno debajo de otro. */}
                        <div className="flex flex-wrap items-center justify-end gap-1">{actions(row)}</div>
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <ul className={join('grid gap-3 sm:grid-cols-2', CARDS_VISIBILITY[tableFrom])}>
            {visible.map(row => (
              <li
                key={rowKey(row)}
                onClick={onRowClick ? () => onRowClick(row) : undefined}
                className={join('min-w-0 p-4', k.card, onRowClick && 'cursor-pointer', onRowClick && k.cardActive, selection?.isSelected(row) && k.cardSelected)}
              >
                {selection && (
                  <div className="-mt-2 mb-2 flex items-center gap-1">
                    <SelectBox checked={selection.isSelected(row)} label={selection.label(row)} onChange={() => selection.onToggle(row)} />
                    <span className={join('text-xs font-semibold', k.muted)}>Seleccionar</span>
                  </div>
                )}
                {primary && <div className={join('break-words font-semibold', k.cardTitle)}>{primary.render(row)}</div>}
                {dataColumns.length > 0 && (
                  <dl className={join('grid grid-cols-2 gap-x-4 gap-y-2 text-sm', primary && 'mt-3')}>
                    {dataColumns.map(c => (
                      <div key={c.key} className={join('min-w-0', c.cardFullWidth && 'col-span-2')}>
                        <dt className={join('text-xs uppercase tracking-wider', k.cardLabel)}>{c.header}</dt>
                        <dd className={join('break-words', k.cardValue)}>{c.render(row)}</dd>
                      </div>
                    ))}
                  </dl>
                )}
                {actions && (
                  <div className={join('mt-3 flex flex-wrap items-center justify-end gap-2 border-t pt-3', k.cardDivider)} onClick={e => e.stopPropagation()}>
                    {actions(row)}
                  </div>
                )}
              </li>
            ))}
          </ul>
        </>
      )}

      {rows.length > 0 && total > 0 && (
        <div className={join('flex flex-wrap items-center justify-between gap-3 text-sm', k.muted)}>
          <span>
            {paginate
              ? <>Mostrando {start + 1}–{start + visible.length} de {total}{!serverMode && searching && total !== rows.length ? <> (filtrados de {rows.length})</> : null}</>
              : <>{total} registro{total === 1 ? '' : 's'}{!serverMode && searching && total !== rows.length ? <> (filtrados de {rows.length})</> : null}</>}
          </span>
          {showFooter && (
            <div className="flex flex-wrap items-center gap-3">
              <label className="flex items-center gap-2">
                Filas por página
                <select
                  value={pageSize}
                  onChange={e => changePageSize(Number(e.target.value))}
                  className={join('h-9 rounded-lg border px-2 text-sm', k.select)}
                >
                  {pageSizes.map(s => <option key={s} value={s}>{s}</option>)}
                </select>
              </label>
              <nav className="flex items-center gap-1" aria-label="Paginación">
                <button type="button" className={pagerButton(theme)} onClick={() => goToPage(1)} disabled={currentPage === 1} aria-label="Primera página"><ChevronsLeft size={16} /></button>
                <button type="button" className={pagerButton(theme)} onClick={() => goToPage(currentPage - 1)} disabled={currentPage === 1} aria-label="Página anterior"><ChevronLeft size={16} /></button>
                <span className={join('px-2', k.strong)}>Página <strong>{currentPage}</strong> de {pageCount}</span>
                <button type="button" className={pagerButton(theme)} onClick={() => goToPage(currentPage + 1)} disabled={currentPage === pageCount} aria-label="Página siguiente"><ChevronRight size={16} /></button>
                <button type="button" className={pagerButton(theme)} onClick={() => goToPage(pageCount)} disabled={currentPage === pageCount} aria-label="Última página"><ChevronsRight size={16} /></button>
              </nav>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
