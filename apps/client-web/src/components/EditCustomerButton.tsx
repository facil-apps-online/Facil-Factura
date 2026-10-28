import React, { useState } from 'react';
import { Edit2, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { api, getErrorMessage } from '../lib/api';
import CustomerFormModal from './CustomerFormModal';

// Botón junto al selector de un tercero en los formularios de factura, nota, documento soporte y nómina: abre el mismo formulario de Terceros para
// corregir sus datos sin salir del documento. El selector solo conoce el nombre y la identificación, así que el tercero completo se pide al abrir.
export default function EditCustomerButton({ customerId, partyType, onSaved, className, iconSize = 20, title = 'Editar tercero' }: {
  customerId: string;
  partyType: string;
  // Recibe el tercero ya guardado, para que la pantalla actualice lo que muestra.
  onSaved: (customer: any) => void;
  className?: string;
  iconSize?: number;
  title?: string;
}) {
  const [customer, setCustomer] = useState<any | null>(null);
  const [loading, setLoading] = useState(false);

  const open = async () => {
    setLoading(true);
    try {
      const res = await api.get(`/client/customers/${customerId}`);
      setCustomer(res.data);
    } catch (err: any) {
      toast.error(getErrorMessage(err, 'No se pudo cargar el tercero'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <>
      <button type="button" onClick={open} disabled={loading} title={title} aria-label={title} className={className ?? 'p-3 bg-slate-100 hover:bg-slate-200 text-slate-600 rounded-xl transition-colors disabled:opacity-50'}>
        {loading ? <Loader2 size={iconSize} className="animate-spin" /> : <Edit2 size={iconSize} />}
      </button>
      {/* El formulario del tercero va dentro del formulario del documento en el árbol de React: su envío no debe llegar al del documento. */}
      <span onSubmit={e => e.stopPropagation()}>
        <CustomerFormModal
          open={!!customer}
          editingCustomer={customer}
          defaultPartyType={partyType}
          onClose={() => setCustomer(null)}
          onSaved={saved => { setCustomer(null); onSaved(saved); }}
        />
      </span>
    </>
  );
}
