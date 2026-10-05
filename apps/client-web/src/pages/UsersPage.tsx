import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { Loader2, Plus } from 'lucide-react';
import { toast } from 'sonner';
import SearchableSelect from '@shared/components/SearchableSelect';

import { api, getErrorMessage } from '../lib/api';
import { useSession } from '../context/SessionContext';
import Modal from '../components/Modal';
import ResponsiveList, { type ResponsiveListColumn } from '../components/ResponsiveList';
import RowIconButton from '../components/RowIconButton';
import { useConfirm } from '@/components/ConfirmDialog';

interface PortalUser {
  id: string;
  name: string;
  email: string;
  role: string;
  allBranches: boolean;
  branchIds: string[];
  isActive: boolean;
  isSelf: boolean;
}

interface UserForm {
  name: string;
  email: string;
  role: string;
  allBranches: boolean;
  branchIds: string[];
}

const INPUT_CLASS = 'w-full px-4 py-2.5 bg-white border border-slate-200 rounded-xl text-sm outline-none focus:ring-2 focus:ring-primary disabled:bg-slate-50 disabled:text-slate-500';
const LABEL_CLASS = 'block text-sm font-bold text-slate-700 mb-1.5';

export default function UsersPage() {
  const { roles, branches, hasMultipleBranches } = useSession();
  const confirm = useConfirm();

  const [users, setUsers] = useState<PortalUser[]>([]);
  const [loading, setLoading] = useState(true);
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<PortalUser | null>(null);
  const [form, setForm] = useState<UserForm>({ name: '', email: '', role: '', allBranches: true, branchIds: [] });
  const [saving, setSaving] = useState(false);

  const roleLabel = useCallback((value: string) => roles.find(r => r.value === value)?.label ?? value, [roles]);
  const branchName = useCallback((id: string) => branches.find(b => b.id === id)?.name ?? '', [branches]);

  const load = useCallback(async () => {
    try {
      const res = await api.get<PortalUser[]>('/client/users');
      setUsers(res.data);
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudieron cargar los usuarios.'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  const openCreate = () => {
    setEditing(null);
    setForm({ name: '', email: '', role: roles[roles.length - 1]?.value ?? '', allBranches: true, branchIds: [] });
    setModalOpen(true);
  };

  const openEdit = (user: PortalUser) => {
    setEditing(user);
    setForm({ name: user.name, email: user.email, role: user.role, allBranches: user.allBranches, branchIds: user.branchIds });
    setModalOpen(true);
  };

  const toggleBranch = (id: string) =>
    setForm(f => ({ ...f, branchIds: f.branchIds.includes(id) ? f.branchIds.filter(b => b !== id) : [...f.branchIds, id] }));

  const save = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.allBranches && form.branchIds.length === 0) {
      toast.error('Elige al menos una sucursal o marca todas.');
      return;
    }

    setSaving(true);
    try {
      const body = { name: form.name, email: form.email, role: form.role, allBranches: form.allBranches, branchIds: form.allBranches ? [] : form.branchIds };
      if (editing) {
        await api.put(`/client/users/${editing.id}`, body);
        toast.success('Usuario actualizado.');
      } else {
        const res = await api.post('/client/users', body);
        if (res.data.invitationSent) toast.success('Usuario creado e invitación enviada.');
        else toast.warning(`Usuario creado, pero no se pudo enviar la invitación: ${res.data.invitationError ?? 'error desconocido'}`);
      }
      setModalOpen(false);
      await load();
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo guardar el usuario.'));
    } finally {
      setSaving(false);
    }
  };

  const resend = async (user: PortalUser) => {
    try {
      const res = await api.post(`/client/users/${user.id}/resend-invitation`);
      if (res.data.sent) toast.success(`Invitación enviada a ${user.email}.`);
      else toast.error(res.data.detail ?? res.data.message);
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo reenviar la invitación.'));
    }
  };

  const setActive = async (user: PortalUser, active: boolean) => {
    if (!active && !(await confirm(`¿Desactivar el acceso de ${user.name}? No podrá iniciar sesión hasta que lo reactives.`))) return;
    try {
      await api.post(`/client/users/${user.id}/${active ? 'reactivate' : 'deactivate'}`);
      toast.success(active ? 'Acceso reactivado.' : 'Acceso desactivado.');
      await load();
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo cambiar el estado del acceso.'));
    }
  };

  const columns = useMemo<ResponsiveListColumn<PortalUser>[]>(() => [
    {
      key: 'name', header: 'Nombre', primary: true,
      render: u => (
        <span className="font-semibold text-slate-800">
          {u.name}{u.isSelf && <span className="ml-2 text-xs font-medium text-slate-400">(tú)</span>}
        </span>
      ),
    },
    { key: 'email', header: 'Correo', render: u => <span className="break-all text-slate-600">{u.email}</span> },
    { key: 'role', header: 'Rol', render: u => roleLabel(u.role) },
    {
      key: 'branches', header: 'Sucursales', cardFullWidth: true,
      render: u => (u.allBranches
        ? <span className="text-slate-600">Todas</span>
        : <span className="text-slate-600">{u.branchIds.map(branchName).filter(Boolean).join(', ') || '—'}</span>),
    },
    {
      key: 'status', header: 'Estado',
      render: u => (
        <span className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-bold ${u.isActive ? 'bg-emerald-50 text-emerald-700' : 'bg-slate-100 text-slate-500'}`}>
          {u.isActive ? 'Activo' : 'Desactivado'}
        </span>
      ),
    },
  ], [roleLabel, branchName]);

  if (loading) {
    return <div className="flex justify-center p-12"><Loader2 className="animate-spin text-primary" size={28} /></div>;
  }

  return (
    <div className="p-4 sm:p-6 lg:p-8">
      <div className="mb-6 flex flex-col gap-4 sm:mb-8 sm:flex-row sm:items-center sm:justify-between">
        <div className="min-w-0">
          <h1 className="text-2xl font-extrabold text-slate-800 sm:text-3xl">Usuarios</h1>
          <p className="mt-1 text-slate-500">Quién entra al portal y a qué sucursales.</p>
        </div>
        <button
          onClick={openCreate}
          className="bg-primary hover:bg-primary/90 text-white px-5 py-2.5 rounded-xl font-bold flex items-center justify-center gap-2 shadow-sm transition-all"
        >
          <Plus size={20} /> Nuevo usuario
        </button>
      </div>

      <ResponsiveList
        rows={users}
        columns={columns}
        rowKey={u => u.id}
        tableFrom="xl"
        emptyMessage="Aún no hay usuarios."
        actions={u => (
          <>
            <RowIconButton action="edit" label={`Editar ${u.name}`} onClick={() => openEdit(u)} />
            {u.isActive && <RowIconButton action="resend" label={`Reenviar invitación a ${u.name}`} onClick={() => resend(u)} />}
            {u.isActive && !u.isSelf && <RowIconButton action="deactivate" label={`Desactivar a ${u.name}`} onClick={() => setActive(u, false)} />}
            {!u.isActive && <RowIconButton action="reactivate" label={`Reactivar a ${u.name}`} onClick={() => setActive(u, true)} />}
          </>
        )}
      />

      <Modal
        open={modalOpen}
        onOpenChange={setModalOpen}
        title={editing ? 'Editar usuario' : 'Nuevo usuario'}
        size="sm"
        withFloatingPickers
        footer={
          <>
            <button type="button" onClick={() => setModalOpen(false)} className="px-5 py-2.5 rounded-xl font-bold text-slate-600 hover:bg-slate-100">
              Cancelar
            </button>
            <button
              type="submit"
              form="user-form"
              disabled={saving}
              className="bg-primary hover:bg-primary/90 disabled:opacity-60 text-white px-6 py-2.5 rounded-xl font-bold flex items-center gap-2"
            >
              {saving && <Loader2 size={16} className="animate-spin" />}
              {editing ? 'Guardar' : 'Crear e invitar'}
            </button>
          </>
        }
      >
        <form id="user-form" onSubmit={save} className="space-y-4">
          <div>
            <label className={LABEL_CLASS} htmlFor="user-name">Nombre</label>
            <input id="user-name" required value={form.name} onChange={e => setForm(f => ({ ...f, name: e.target.value }))} className={INPUT_CLASS} />
          </div>
          <div>
            <label className={LABEL_CLASS} htmlFor="user-email">Correo</label>
            <input
              id="user-email" type="email" required disabled={!!editing}
              value={form.email} onChange={e => setForm(f => ({ ...f, email: e.target.value }))} className={INPUT_CLASS}
            />
          </div>
          <div>
            <label className={LABEL_CLASS}>Rol</label>
            <SearchableSelect
              options={roles.map(r => ({ value: r.value, label: r.label }))}
              value={form.role}
              onChange={role => setForm(f => ({ ...f, role }))}
              placeholder="Elige un rol"
              required
              inputClassName={INPUT_CLASS}
            />
          </div>

          {hasMultipleBranches && (
            <fieldset className="space-y-2">
              <legend className={LABEL_CLASS}>Sucursales</legend>
              <label className="flex items-center gap-2 text-sm text-slate-700">
                <input
                  type="checkbox" className="h-4 w-4 accent-primary"
                  checked={form.allBranches} onChange={e => setForm(f => ({ ...f, allBranches: e.target.checked }))}
                />
                Todas las sucursales
              </label>
              {!form.allBranches && branches.map(b => (
                <label key={b.id} className="flex items-center gap-2 pl-6 text-sm text-slate-700">
                  <input type="checkbox" className="h-4 w-4 accent-primary" checked={form.branchIds.includes(b.id)} onChange={() => toggleBranch(b.id)} />
                  {b.name}
                </label>
              ))}
            </fieldset>
          )}
        </form>
      </Modal>
    </div>
  );
}
