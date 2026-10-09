import React from 'react';

import { cn } from '@/lib/utils';

// `wide` = 1400 px y `ultra` = 1800 px: puntos intermedios para tablas anchas, que con el menú lateral
// expandido (256 px) no caben en 1280 px aunque ahí ya sea "escritorio".
type TableFrom = 'md' | 'lg' | 'xl' | 'wide' | '2xl' | 'ultra';

export interface ResponsiveListColumn<T> {
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
}

interface ResponsiveListProps<T> {
  rows: T[];
  columns: ResponsiveListColumn<T>[];
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
    // Marca o desmarca todas las filas visibles.
    onToggleAll: () => void;
    allSelected: boolean;
    someSelected: boolean;
    // Nombre accesible de la casilla de una fila ("Seleccionar factura FE-12").
    label: (row: T) => string;
  };
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

// Casilla con área táctil de 44 px; no dispara el clic de la fila.
function SelectBox({ checked, indeterminate, label, onChange, inTable }: { checked: boolean; indeterminate?: boolean; label: string; onChange: () => void; inTable?: boolean }) {
  return (
    // En la tabla el área táctil sobresale de la celda (margen negativo) para no agrandar la fila.
    <label className={cn('flex h-11 w-11 cursor-pointer items-center justify-center', inTable && '-m-3')} onClick={e => e.stopPropagation()}>
      <input
        type="checkbox"
        checked={checked}
        ref={el => { if (el) el.indeterminate = !!indeterminate && !checked; }}
        onChange={onChange}
        aria-label={label}
        className="h-4 w-4 cursor-pointer accent-primary"
      />
    </label>
  );
}

// Listado de consulta: tabla desde `tableFrom` y tarjetas por debajo. Las dos presentaciones se
// renderizan siempre y el CSS decide cuál se ve, así no hay parpadeo ni lógica de ancho en JS.
export default function ResponsiveList<T>({
  rows,
  columns,
  rowKey,
  actions,
  tableFrom = 'lg',
  emptyMessage = 'No hay registros para mostrar.',
  onRowClick,
  selection,
}: ResponsiveListProps<T>) {
  if (rows.length === 0) {
    return <div className="rounded-2xl border border-slate-200 bg-white p-8 text-center text-sm text-slate-500">{emptyMessage}</div>;
  }

  const primary = columns.find(c => c.primary);
  const dataColumns = columns.filter(c => !c.primary && c.showInCard !== false);

  return (
    <>
      <div className={cn('overflow-x-auto rounded-2xl border border-slate-200 bg-white', TABLE_VISIBILITY[tableFrom])}>
        <table className="w-full text-left text-sm">
          <thead className="bg-slate-50 text-xs uppercase tracking-wider text-slate-500">
            <tr>
              {selection && (
                <th className="w-10 px-3 py-3">
                  <SelectBox inTable checked={selection.allSelected} indeterminate={selection.someSelected} label="Seleccionar todas las filas de la página" onChange={selection.onToggleAll} />
                </th>
              )}
              {columns.map(c => (
                <th key={c.key} className={cn('px-3 py-3 font-semibold', c.align === 'right' && 'text-right', c.hideBelow && HIDE_BELOW[c.hideBelow])}>{c.header}</th>
              ))}
              {actions && <th className="px-3 py-3 text-right font-semibold">Acciones</th>}
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {rows.map(row => (
              <tr
                key={rowKey(row)}
                onClick={onRowClick ? () => onRowClick(row) : undefined}
                className={cn('hover:bg-slate-50', onRowClick && 'cursor-pointer', selection?.isSelected(row) && 'bg-primary/5')}
              >
                {selection && (
                  <td className="w-10 px-3 py-3">
                    <SelectBox inTable checked={selection.isSelected(row)} label={selection.label(row)} onChange={() => selection.onToggle(row)} />
                  </td>
                )}
                {columns.map(c => (
                  <td key={c.key} className={cn('px-3 py-3 align-middle break-words', c.align === 'right' && 'text-right', c.hideBelow && HIDE_BELOW[c.hideBelow], c.cellClassName)}>{c.render(row)}</td>
                ))}
                {actions && (
                  <td className="px-3 py-3" onClick={e => e.stopPropagation()}>
                    {/* Fila flex: sin ella los botones (display:flex) se apilan uno debajo de otro. */}
                    <div className="flex items-center justify-end gap-1">{actions(row)}</div>
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <ul className={cn('grid gap-3 sm:grid-cols-2', CARDS_VISIBILITY[tableFrom])}>
        {rows.map(row => (
          <li
            key={rowKey(row)}
            onClick={onRowClick ? () => onRowClick(row) : undefined}
            className={cn('min-w-0 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm', onRowClick && 'cursor-pointer active:bg-slate-50', selection?.isSelected(row) && 'border-primary/50 bg-primary/5')}
          >
            {selection && (
              <div className="-mt-2 mb-2 flex items-center gap-1">
                <SelectBox checked={selection.isSelected(row)} label={selection.label(row)} onChange={() => selection.onToggle(row)} />
                <span className="text-xs font-semibold text-slate-500">Seleccionar</span>
              </div>
            )}
            {primary && <div className="break-words font-semibold text-slate-800">{primary.render(row)}</div>}
            {dataColumns.length > 0 && (
              <dl className={cn('grid grid-cols-2 gap-x-4 gap-y-2 text-sm', primary && 'mt-3')}>
                {dataColumns.map(c => (
                  <div key={c.key} className={cn('min-w-0', c.cardFullWidth && 'col-span-2')}>
                    <dt className="text-xs uppercase tracking-wider text-slate-400">{c.header}</dt>
                    <dd className="break-words text-slate-700">{c.render(row)}</dd>
                  </div>
                ))}
              </dl>
            )}
            {actions && (
              <div
                className="mt-3 flex flex-wrap items-center justify-end gap-2 border-t border-slate-100 pt-3"
                onClick={e => e.stopPropagation()}
              >
                {actions(row)}
              </div>
            )}
          </li>
        ))}
      </ul>
    </>
  );
}
