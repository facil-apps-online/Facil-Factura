import { useState } from 'react';
import { Info } from 'lucide-react';
import Modal from './Modal';
import { Button } from './ui/button';

// Marca de "precio con IVA incluido" de una línea de factura. Al hacer clic abre un cuadro con la explicación (igual que la información de Dataico),
// en vez de un tooltip: en móvil y con teclado un tooltip no se puede leer.
export default function IncludedIvaInfo({ taxRate }: { taxRate?: number }) {
  const [open, setOpen] = useState(false);
  return (
    <>
      <button
        type="button"
        onClick={e => { e.stopPropagation(); setOpen(true); }}
        aria-label="Ver por qué esta línea tiene el IVA incluido"
        className="inline-flex shrink-0 items-center align-text-bottom text-emerald-600 hover:text-emerald-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40 rounded"
      >
        <Info size={13} />
      </button>
      <Modal
        open={open}
        onOpenChange={setOpen}
        title={<span className="flex items-center gap-2"><Info size={18} className="shrink-0 text-emerald-600" /> Precio con IVA incluido</span>}
        size="sm"
        footer={<Button type="button" onClick={() => setOpen(false)}>Entendido</Button>}
      >
        <div className="space-y-3 text-sm text-slate-600">
          <p>El precio de este producto está registrado <b>con el IVA incluido</b>{taxRate ? <> ({taxRate} %)</> : null}.</p>
          <p>Al agregarlo a la factura, el sistema separa el valor: el <b>valor unitario</b> de la línea es el precio sin IVA y el IVA se calcula aparte, de modo que el total de la línea coincide con el precio del producto.</p>
          <p>Si escribe un valor a mano en esta línea, se toma como el <b>precio final con IVA</b> y el sistema vuelve a calcular la base.</p>
        </div>
      </Modal>
    </>
  );
}
