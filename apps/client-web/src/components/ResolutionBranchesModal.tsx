import React, { useState } from 'react';
import { Loader2 } from 'lucide-react';
import { toast } from 'sonner';

import { api, getErrorMessage } from '../lib/api';
import { useSession } from '../context/SessionContext';
import Modal from './Modal';
import { Button } from './ui/button';

interface Props {
  resolution: { id: string; prefix?: string; resolutionNumber?: string; branchIds?: string[] };
  onClose: () => void;
  onSaved: () => void;
}

// Sucursales en las que se puede emitir con una resolución. Debe quedar al menos una.
export default function ResolutionBranchesModal({ resolution, onClose, onSaved }: Props) {
  const { branches } = useSession();
  const [selected, setSelected] = useState<string[]>(resolution.branchIds ?? []);
  const [saving, setSaving] = useState(false);

  const toggle = (id: string) => setSelected(s => (s.includes(id) ? s.filter(b => b !== id) : [...s, id]));

  const save = async (e: React.FormEvent) => {
    e.preventDefault();
    if (selected.length === 0) {
      toast.error('La resolución debe estar disponible en al menos una sucursal.');
      return;
    }
    setSaving(true);
    try {
      await api.put(`/client/resolutions/${resolution.id}/branches`, { branchIds: selected });
      toast.success('Sucursales de la resolución actualizadas.');
      onSaved();
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudieron actualizar las sucursales.'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal
      open
      onOpenChange={open => { if (!open) onClose(); }}
      title={`Sucursales de la resolución ${[resolution.prefix, resolution.resolutionNumber].filter(Boolean).join(' ')}`.trim()}
      size="sm"
      preventDismiss
      footer={
        <>
          <Button type="button" variant="ghost" onClick={onClose}>Cancelar</Button>
          <Button type="submit" form="resolution-branches-form" disabled={saving}>
            {saving && <Loader2 size={16} className="animate-spin" />} Guardar
          </Button>
        </>
      }
    >
      <form id="resolution-branches-form" onSubmit={save} className="space-y-2">
        {branches.map(b => (
          <label key={b.id} className="flex min-h-11 items-center gap-2 text-sm text-slate-700">
            <input type="checkbox" className="h-4 w-4 accent-primary" checked={selected.includes(b.id)} onChange={() => toggle(b.id)} />
            {b.name}
          </label>
        ))}
      </form>
    </Modal>
  );
}
