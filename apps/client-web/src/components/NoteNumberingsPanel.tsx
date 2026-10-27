import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { Check, CloudDownload, Loader2, Plus, ShieldCheck } from 'lucide-react';
import { toast } from 'sonner';
import SearchableSelect from '@shared/components/SearchableSelect';

import { api, getErrorMessage } from '../lib/api';
import { useConfirm } from '@/components/ConfirmDialog';
import Modal from './Modal';
import RowIconButton from './RowIconButton';
import { Button } from './ui/button';

export interface NoteNumberingItem {
  id: string | null;
  kind: 'CreditNote' | 'DebitNote';
  kindLabel: string;
  prefix: string;
  nextNumber: number;
  // own = numeración propia del cliente; invoice = la de su resolución de factura; branch = la propia de una sucursal.
  source: 'own' | 'invoice' | 'branch';
  branchName: string | null;
  isDefault: boolean;
  inUse: boolean;
}

interface DataicoCheck { checked: boolean; exists: boolean; message: string | null }
interface DataicoPrefix { kind: 'CreditNote' | 'DebitNote'; kindLabel: string; prefix: string }
interface Form { kind: string; prefix: string; nextNumber: string }

const INPUT_CLASS = 'w-full px-4 py-2.5 bg-white border border-slate-200 rounded-xl text-sm outline-none focus:ring-2 focus:ring-primary disabled:bg-slate-50 disabled:text-slate-500';
const LABEL_CLASS = 'block text-sm font-bold text-slate-700 mb-1.5';
const KIND_OPTIONS = [{ value: 'CreditNote', label: 'Nota crédito' }, { value: 'DebitNote', label: 'Nota débito' }];
const SOURCE_LABEL = { own: 'Propia', invoice: 'Prefijo de la factura', branch: 'Sucursal' } as const;

// Numeraciones de notas crédito y débito del cliente, agrupadas por prefijo. Sin una propia, las notas usan el prefijo de su factura; el cliente
// puede crear otras (por ejemplo NC y ND) y marcar una como predeterminada. En Dataico el prefijo debe existir en la cuenta: su API no deja crearlo.
export default function NoteNumberingsPanel() {
  const confirm = useConfirm();
  const [items, setItems] = useState<NoteNumberingItem[]>([]);
  const [usesDataico, setUsesDataico] = useState(false);
  const [loading, setLoading] = useState(true);
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<NoteNumberingItem | null>(null);
  const [form, setForm] = useState<Form>({ kind: '', prefix: '', nextNumber: '' });
  const [saving, setSaving] = useState(false);
  const [notice, setNotice] = useState<{ tone: 'ok' | 'warn'; text: string } | null>(null);
  const [importOpen, setImportOpen] = useState(false);
  const [importing, setImporting] = useState(false);
  const [dataicoPrefixes, setDataicoPrefixes] = useState<DataicoPrefix[]>([]);

  const load = useCallback(async () => {
    try {
      const res = await api.get<{ usesDataico: boolean; items: NoteNumberingItem[] }>('/client/note-numberings/overview');
      setItems(res.data.items);
      setUsesDataico(res.data.usesDataico);
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudieron cargar las numeraciones de notas.'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  // Por prefijo: cada grupo reúne lo que ese prefijo numera (crédito y débito).
  const groups = useMemo(() => {
    const byPrefix = new Map<string, NoteNumberingItem[]>();
    for (const item of items) {
      const list = byPrefix.get(item.prefix) ?? [];
      list.push(item);
      byPrefix.set(item.prefix, list);
    }
    return [...byPrefix.entries()].sort(([a], [b]) => a.localeCompare(b)).map(([prefix, rows]) => ({ prefix, rows: rows.sort((x, y) => x.kind.localeCompare(y.kind)) }));
  }, [items]);

  // Tipos sin numeración propia predeterminada: ahí las notas usan el prefijo de su factura.
  const kindHasOwnDefault = (kind: string) => items.some(i => i.kind === kind && i.source === 'own' && i.isDefault);

  const showDataico = (check: DataicoCheck | null | undefined) => {
    if (!check || !check.message) { setNotice(null); return; }
    setNotice({ tone: check.checked && !check.exists ? 'warn' : 'ok', text: check.message });
  };

  const openCreate = (preset?: Partial<Form>) => {
    setEditing(null);
    setForm({ kind: '', prefix: '', nextNumber: '', ...preset });
    setModalOpen(true);
  };

  const openEdit = (row: NoteNumberingItem) => {
    setEditing(row);
    setForm({ kind: row.kind, prefix: row.prefix, nextNumber: String(row.nextNumber) });
    setModalOpen(true);
  };

  const save = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    try {
      const payload = { kind: form.kind, prefix: form.prefix, nextNumber: form.nextNumber ? Number(form.nextNumber) : null };
      const res = editing
        ? await api.put<{ dataico: DataicoCheck | null }>(`/client/note-numberings/own/${editing.id}`, payload)
        : await api.post<{ dataico: DataicoCheck | null }>('/client/note-numberings/own', payload);
      toast.success('Numeración guardada.');
      showDataico(res.data.dataico);
      setModalOpen(false);
      await load();
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo guardar la numeración.'));
    } finally {
      setSaving(false);
    }
  };

  const remove = async (row: NoteNumberingItem) => {
    if (!(await confirm(`¿Eliminar la numeración ${row.prefix} de ${row.kindLabel.toLowerCase()}? Las notas nuevas dejarán de usarla.`))) return;
    try {
      await api.delete(`/client/note-numberings/own/${row.id}`);
      toast.success('Numeración eliminada.');
      await load();
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo eliminar la numeración.'));
    }
  };

  const makeDefault = async (row: NoteNumberingItem) => {
    try {
      await api.post(`/client/note-numberings/own/${row.id}/default`);
      toast.success(`${row.prefix} es ahora la predeterminada de ${row.kindLabel.toLowerCase()}.`);
      await load();
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo cambiar la predeterminada.'));
    }
  };

  const useInvoicePrefix = async (kind: string, label: string) => {
    try {
      await api.post(`/client/note-numberings/default/${kind}/clear`);
      toast.success(`Las ${label.toLowerCase()}s vuelven a usar el prefijo de su factura.`);
      await load();
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo cambiar la predeterminada.'));
    }
  };

  const verifyInDataico = async (row: NoteNumberingItem) => {
    try {
      const res = await api.get<DataicoCheck>(`/client/note-numberings/own/${row.id}/dataico-check`);
      showDataico(res.data);
      if (!res.data.message) toast.info('No se pudo consultar Dataico.');
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo consultar Dataico.'));
    }
  };

  const openImport = async () => {
    setImporting(true);
    try {
      const res = await api.get<DataicoPrefix[]>('/client/note-numberings/dataico-prefixes');
      setDataicoPrefixes(res.data);
      setImportOpen(true);
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo consultar la cuenta de Dataico.'));
    } finally {
      setImporting(false);
    }
  };

  const addFromDataico = (p: DataicoPrefix) => {
    setImportOpen(false);
    openCreate({ kind: p.kind, prefix: p.prefix });
  };

  return (
    <div className="bg-white rounded-3xl p-4 sm:p-6 lg:p-8 shadow-sm border border-slate-100 mt-6 lg:mt-8">
      <div className="mb-6 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div className="min-w-0">
          <h2 className="text-xl font-bold text-slate-800">Numeraciones de notas crédito y débito</h2>
          <p className="text-slate-500 mt-1 text-sm max-w-2xl">
            Por defecto las notas usan el prefijo de su factura. Crea numeraciones con otros prefijos (por ejemplo NC y ND) y marca una como predeterminada para cada tipo de nota.
            {usesDataico && ' En Dataico el prefijo debe existir en tu cuenta (Ventas, Notas crédito o débito, Numeraciones): desde aquí no se puede crear allá.'}
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          {usesDataico && (
            <Button type="button" variant="ghost" className="border border-slate-200" onClick={openImport} disabled={importing}>
              {importing ? <Loader2 size={18} className="animate-spin" /> : <CloudDownload size={18} />} Traer de Dataico
            </Button>
          )}
          <Button type="button" onClick={() => openCreate()}><Plus size={18} /> Nueva numeración</Button>
        </div>
      </div>

      {notice && (
        <div role="status" className={`mb-4 rounded-xl border px-4 py-3 text-sm ${notice.tone === 'warn' ? 'border-amber-200 bg-amber-50 text-amber-800' : 'border-emerald-200 bg-emerald-50 text-emerald-800'}`}>
          {notice.text}
        </div>
      )}

      {loading ? (
        <div className="flex justify-center p-6"><Loader2 className="animate-spin text-primary" size={24} /></div>
      ) : groups.length === 0 ? (
        <p className="text-sm text-slate-500">Aún no hay consecutivos de notas. Se crean solos con la primera nota, o agrega una numeración propia.</p>
      ) : (
        <div className="space-y-3">
          {groups.map(group => (
            <div key={group.prefix} className="rounded-2xl border border-slate-200 p-4" data-testid={`note-group-${group.prefix}`}>
              <p className="font-mono text-lg font-black text-slate-800 mb-2">{group.prefix}</p>
              <div className="divide-y divide-slate-100">
                {group.rows.map(row => {
                  const usedByDefault = row.source === 'invoice' && !kindHasOwnDefault(row.kind);
                  return (
                    <div key={`${row.source}-${row.kind}-${row.branchName ?? ''}`} className="flex flex-wrap items-center gap-x-4 gap-y-2 py-2">
                      <div className="min-w-0 sm:min-w-36">
                        <p className="font-semibold text-slate-700">{row.kindLabel}</p>
                        <p className="text-xs text-slate-500">{row.source === 'branch' ? `Sucursal ${row.branchName}` : SOURCE_LABEL[row.source]}</p>
                      </div>
                      <p className="text-sm text-slate-600">Próximo # <span className="font-mono font-bold text-slate-800">{row.nextNumber}</span></p>
                      <div className="flex flex-wrap gap-2 mr-auto">
                        {row.isDefault && <span className="inline-flex items-center gap-1 rounded-full bg-emerald-50 px-2.5 py-1 text-xs font-bold text-emerald-700"><Check size={12} /> Predeterminada</span>}
                        {usedByDefault && <span className="rounded-full bg-slate-100 px-2.5 py-1 text-xs font-bold text-slate-600">En uso por defecto</span>}
                        {row.inUse && <span className="rounded-full bg-blue-50 px-2.5 py-1 text-xs font-bold text-blue-700">Con notas emitidas</span>}
                      </div>
                      <div className="flex flex-wrap items-center gap-1">
                        {row.source === 'own' && !row.isDefault && (
                          <Button type="button" variant="ghost" size="sm" onClick={() => makeDefault(row)}>Hacer predeterminada</Button>
                        )}
                        {row.source === 'invoice' && kindHasOwnDefault(row.kind) && (
                          <Button type="button" variant="ghost" size="sm" onClick={() => useInvoicePrefix(row.kind, row.kindLabel)}>Usar por defecto</Button>
                        )}
                        {row.source === 'own' && usesDataico && (
                          <button
                            type="button" onClick={() => verifyInDataico(row)} aria-label={`Verificar ${row.prefix} en Dataico`} title="Verificar en Dataico"
                            className="flex h-11 w-11 items-center justify-center rounded-lg text-slate-400 transition-colors hover:bg-slate-100 hover:text-primary lg:h-9 lg:w-9"
                          ><ShieldCheck size={18} /></button>
                        )}
                        {row.source === 'own' && <RowIconButton action="edit" label={`Editar numeración ${row.prefix} de ${row.kindLabel}`} onClick={() => openEdit(row)} />}
                        {row.source === 'own' && !row.inUse && <RowIconButton action="delete" label={`Eliminar numeración ${row.prefix} de ${row.kindLabel}`} onClick={() => remove(row)} />}
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>
          ))}
        </div>
      )}

      <Modal
        open={modalOpen}
        onOpenChange={setModalOpen}
        title={editing ? 'Editar numeración de notas' : 'Agregar numeración de notas'}
        size="sm"
        withFloatingPickers
        footer={
          <>
            <Button type="button" variant="ghost" onClick={() => setModalOpen(false)}>Cancelar</Button>
            <Button type="submit" form="note-numbering-form" disabled={saving}>
              {saving && <Loader2 size={16} className="animate-spin" />} Guardar
            </Button>
          </>
        }
      >
        <form id="note-numbering-form" onSubmit={save} className="space-y-4">
          <div>
            <label className={LABEL_CLASS}>Tipo de nota</label>
            {editing ? (
              <div className={`${INPUT_CLASS} bg-slate-50 text-slate-500`}>{editing.kindLabel}</div>
            ) : (
              <SearchableSelect
                required
                options={KIND_OPTIONS}
                value={form.kind}
                onChange={kind => setForm(f => ({ ...f, kind }))}
                placeholder="Elige el tipo de nota"
                inputClassName={INPUT_CLASS}
              />
            )}
          </div>
          <div>
            <label className={LABEL_CLASS} htmlFor="note-numbering-prefix">Prefijo</label>
            <input
              id="note-numbering-prefix" required maxLength={10} value={form.prefix} disabled={!!editing?.inUse}
              onChange={e => setForm(f => ({ ...f, prefix: e.target.value.toUpperCase() }))}
              className={`${INPUT_CLASS} font-mono uppercase`}
            />
            {editing?.inUse && <p className="mt-1 text-xs text-slate-500">Ya hay notas emitidas con este prefijo: no se puede cambiar.</p>}
          </div>
          <div>
            <label className={LABEL_CLASS} htmlFor="note-numbering-next">Próximo consecutivo</label>
            <input
              id="note-numbering-next" type="number" min={1} value={form.nextNumber} placeholder="1"
              onChange={e => setForm(f => ({ ...f, nextNumber: e.target.value }))}
              className={`${INPUT_CLASS} font-mono`}
            />
          </div>
        </form>
      </Modal>

      <Modal open={importOpen} onOpenChange={setImportOpen} title="Numeraciones de notas en Dataico" size="sm" footer={<Button type="button" variant="ghost" onClick={() => setImportOpen(false)}>Cerrar</Button>}>
        {dataicoPrefixes.length === 0 ? (
          <p className="text-sm text-slate-600">Todos los prefijos de notas que tienes en Dataico ya están aquí.</p>
        ) : (
          <>
            <p className="text-sm text-slate-600 mb-3">Estos prefijos existen en tu cuenta de Dataico y todavía no están aquí. Elige uno para agregarlo y revisa su próximo consecutivo.</p>
            <ul className="space-y-2">
              {dataicoPrefixes.map(p => (
                <li key={`${p.kind}-${p.prefix}`} className="flex items-center justify-between gap-3 rounded-xl border border-slate-200 px-4 py-2">
                  <span><span className="font-mono font-bold text-slate-800">{p.prefix}</span> <span className="text-sm text-slate-500">· {p.kindLabel}</span></span>
                  <Button type="button" size="sm" variant="ghost" className="border border-slate-200" onClick={() => addFromDataico(p)}>Agregar</Button>
                </li>
              ))}
            </ul>
          </>
        )}
      </Modal>
    </div>
  );
}
