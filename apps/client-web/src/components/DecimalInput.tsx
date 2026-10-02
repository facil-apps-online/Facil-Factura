import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import type { InputHTMLAttributes } from 'react';
import { useNumberFormat } from '../lib/numberFormat';

interface Props extends Omit<InputHTMLAttributes<HTMLInputElement>, 'value' | 'onChange' | 'type'> {
  value: number;
  onValueChange: (value: number) => void;
  /** Decimales máximos que acepta (2 para dinero, 3 para porcentajes y cantidades). */
  maxDecimals?: number;
  /** Mostrar el campo vacío cuando el valor es 0 (cargos, descuentos). */
  blankWhenZero?: boolean;
}

// Campo numérico con separador de miles aplicado mientras se escribe ("1,000,000" en vez de "1000000"),
// en el formato configurado para el cliente. Reemplaza a <input type="number">, que muestra y acepta el
// separador según el idioma del navegador y no agrupa miles.
export default function DecimalInput({ value, onValueChange, maxDecimals = 2, blankWhenZero = false, onBlur, onFocus, ...rest }: Props) {
  const fmt = useNumberFormat();
  const ref = useRef<HTMLInputElement>(null);
  const caretCount = useRef<number | null>(null);
  const render = (v: number) => (blankWhenZero && !v ? '' : fmt.number(v, maxDecimals));
  const [text, setText] = useState(() => render(value));

  // Valor cambiado desde fuera (otra fila, reinicio del formulario) o cambio de formato: se vuelve a
  // dibujar. Si el texto actual ya representa ese valor no se toca, para no perder "1,234." a medias.
  useEffect(() => {
    if (fmt.typing(text, maxDecimals).value !== value) setText(render(value));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [value]);
  useEffect(() => {
    setText(render(value));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [fmt]);

  // Agrupar cambia la longitud del texto: el cursor se recoloca contando los dígitos a su izquierda.
  useLayoutEffect(() => {
    const el = ref.current;
    const target = caretCount.current;
    if (!el || target === null || document.activeElement !== el) return;
    caretCount.current = null;
    let seen = 0;
    let pos = 0;
    while (pos < text.length && seen < target) {
      if (/\d/.test(text[pos]) || text[pos] === fmt.decimal) seen++;
      pos++;
    }
    el.setSelectionRange(pos, pos);
  }, [text, fmt.decimal]);

  return (
    <input
      {...rest}
      ref={ref}
      type="text"
      inputMode="decimal"
      value={text}
      onChange={(e) => {
        const raw = e.target.value;
        const caret = e.target.selectionStart ?? raw.length;
        caretCount.current = caret >= raw.length ? Infinity : raw.slice(0, caret).split('').filter(ch => /\d/.test(ch) || ch === fmt.decimal).length;
        const { display, value: parsed } = fmt.typing(raw, maxDecimals);
        setText(display);
        onValueChange(parsed);
      }}
      onFocus={(e) => { e.target.select(); onFocus?.(e); }}
      onBlur={(e) => { setText(render(fmt.typing(text, maxDecimals).value)); onBlur?.(e); }}
    />
  );
}
