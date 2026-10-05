import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { Loader2, Plus } from 'lucide-react';
import { toast } from 'sonner';
import SearchableSelect from '@shared/components/SearchableSelect';

import { api, getErrorMessage } from '../lib/api';
import { useSession } from '../context/SessionContext';
import { useConfirm } from '@/components/ConfirmDialog';
import Modal from './Modal';
import ResponsiveList, { type ResponsiveListColumn } from './ResponsiveList';
import RowIconButton from './RowIconButton';
import { Button } from './ui/button';

interface BranchNumbering {
  branchId: string;
  branchName: string;
  kind: string;
  kindLabel: string;
  prefix: string;
  nextNumber: number;
}

interface Form {
  branchId: string;
  kind: string;
  prefix: string;
  nextNumber: string;
}

const INPUT_CLASS = 'w-full px-4 py-2.5 bg-white border border-slate-200 rounded-xl text-sm outline-none focus:ring-2 focus:ring-primary disabled:bg-slate-50 disabled:text-slate-500';
const LABEL_CLASS = 'block text-sm font-bold text-slate-700 mb-1.5';

// Numeración de notas propia de una sucursal: su propio prefijo y su propio consecutivo. Las sucursales que no la tienen usan los
// consecutivos compartidos del cliente.
export default function BranchNoteNumberings() {
  const { branches, noteKinds } = useSession();
  const confirm = useConfirm();

  const [rows, setRows] = useState<BranchNumbering[]>([]);
  const [loading, setLoading] = useState(true);
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<BranchNumbering | null>(null);
  const [form, setForm] = useState<Form>({ branchId: '', kind: '', prefix: '', nextNumber: '' });
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    try {
      const res = await api.get<BranchNumbering[]>('/client/note-numberings');
      setRows(res.data);
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo cargar la numeración por sucursal.'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  const openCreate = () => {
    setEditing(null);
    setForm({ branchId: '', kind: '', prefix: '', nextNumber: '' });
    setModalOpen(true);
  };

  const openEdit = (row: BranchNumbering) => {
    setEditing(row);
    setForm({ branchId: row.branchId, kind: row.kind, prefix: row.prefix, nextNumber: String(row.nextNumber) });
    setModalOpen(true);
  };

  const save = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    try {
      await api.put(`/client/note-numberings/${form.branchId}/${form.kind}`, {
        prefix: form.prefix,
        nextNumber: form.nextNumber ? Number(form.nextNumber) : null,
      });
      toast.success('Numeración guardada.');
      setModalOpen(false);
      await load();
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo guardar la numeración.'));
    } finally {
      setSaving(false);
    }
  };

  const remove = async (row: BranchNumbering) => {
    if (!(await confirm(`¿Quitar la numeración propia de ${row.branchName} para ${row.kindLabel}? La sucursal volverá a usar el consecutivo compartido.`))) return;
    try {
      await api.delete(`/client/note-numberings/${row.branchId}/${row.kind}`);
      toast.success('Numeración quitada.');
      await load();
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo quitar la numeración.'));
    }
  };

  const columns = useMemo<ResponsiveListColumn<BranchNumbering>[]>(() => [
    { key: 'branch', header: 'Sucursal', primary: true, render: r => <span className="font-semibold text-slate-800">{r.branchName}</span> },
    { key: 'kind', header: 'Tipo de nota', render: r => r.kindLabel },
    { key: 'prefix', header: 'Prefijo', cellClassName: 'font-mono font-bold text-slate-700', render: r => r.prefix },
    { key: 'next', header: 'Próximo #', cellClassName: 'font-mono font-bold text-slate-700', render: r => r.nextNumber },
  ], []);

  return (
    <div className="bg-white rounded-3xl p-4 sm:p-6 lg:p-8 shadow-sm border border-slate-100 mt-6 lg:mt-8">
      <div className="mb-6 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div className="min-w-0">
          <h2 className="text-xl font-bold text-slate-800">Numeración propia por sucursal</h2>
          <p className="text-slate-500 mt-1 text-sm max-w-2xl">Las sucursales sin numeración propia usan los consecutivos compartidos de arriba.</p>
        </div>
        <Button type="button" onClick={openCreate}><Plus size={18} /> Agregar numeración</Button>
      </div>

      {loading ? (
        <div className="flex justify-center p-6"><Loader2 className="animate-spin text-primary" size={24} /></div>
      ) : (
        <ResponsiveList
          rows={rows}
          columns={columns}
          rowKey={r => `${r.branchId}-${r.kind}`}
          tableFrom="lg"
          emptyMessage="Ninguna sucursal tiene numeración propia."
          actions={r => (
            <>
              <RowIconButton action="edit" label={`Editar numeración de ${r.branchName} para ${r.kindLabel}`} onClick={() => openEdit(r)} />
              <RowIconButton action="delete" label={`Quitar numeración de ${r.branchName} para ${r.kindLabel}`} onClick={() => remove(r)} />
            </>
          )}
        />
      )}

      <Modal
        open={modalOpen}
        onOpenChange={setModalOpen}
        title={editing ? 'Editar numeración propia' : 'Agregar numeración propia'}
        size="sm"
        withFloatingPickers
        footer={
          <>
            <Button type="button" variant="ghost" onClick={() => setModalOpen(false)}>Cancelar</Button>
            <Button type="submit" form="branch-numbering-form" disabled={saving}>
              {saving && <Loader2 size={16} className="animate-spin" />} Guardar
            </Button>
          </>
        }
      >
        <form id="branch-numbering-form" onSubmit={save} className="space-y-4">
          <div>
            <label className={LABEL_CLASS}>Sucursal</label>
            {editing ? (
              <div className={`${INPUT_CLASS} bg-slate-50 text-slate-500`}>{editing.branchName}</div>
            ) : (
              <SearchableSelect
                required
                options={branches.map(b => ({ value: b.id, label: b.name }))}
                value={form.branchId}
                onChange={branchId => setForm(f => ({ ...f, branchId }))}
                placeholder="Elige una sucursal"
                inputClassName={INPUT_CLASS}
              />
            )}
          </div>
          <div>
            <label className={LABEL_CLASS}>Tipo de nota</label>
            {editing ? (
              <div className={`${INPUT_CLASS} bg-slate-50 text-slate-500`}>{editing.kindLabel}</div>
            ) : (
              <SearchableSelect
                required
                options={noteKinds.map(k => ({ value: k.value, label: k.label }))}
                value={form.kind}
                onChange={kind => setForm(f => ({ ...f, kind }))}
                placeholder="Elige el tipo de nota"
                inputClassName={INPUT_CLASS}
              />
            )}
          </div>
          <div>
            <label className={LABEL_CLASS} htmlFor="numbering-prefix">Prefijo</label>
            <input
              id="numbering-prefix" required maxLength={10} value={form.prefix}
              onChange={e => setForm(f => ({ ...f, prefix: e.target.value.toUpperCase() }))}
              className={`${INPUT_CLASS} font-mono uppercase`}
            />
          </div>
          <div>
            <label className={LABEL_CLASS} htmlFor="numbering-next">Próximo consecutivo</label>
            <input
              id="numbering-next" type="number" min={1} value={form.nextNumber} placeholder="1"
              onChange={e => setForm(f => ({ ...f, nextNumber: e.target.value }))}
              className={`${INPUT_CLASS} font-mono`}
            />
          </div>
        </form>
      </Modal>
    </div>
  );
}
