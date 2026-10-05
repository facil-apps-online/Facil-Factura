import React, { useEffect, useState } from 'react';
import { Plus, Pencil, Mail, Power, PowerOff, X, UserRound, Loader2, CheckCircle2, ShieldAlert } from 'lucide-react';
import { toast } from 'sonner';
import { api, getErrorMessage } from '../lib/api';

interface ClientUserRow {
  id: string;
  name: string;
  email: string;
  role: string;
  allBranches: boolean;
  isActive: boolean;
  createdAt: string;
  branchIds: string[];
}

interface BranchOption { id: string; name: string; isActive: boolean }
interface RoleOption { value: string; label: string }

interface UserForm {
  name: string;
  email: string;
  role: string;
  allBranches: boolean;
  branchIds: string[];
}

const inputClass = 'w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none';

export default function ClientUsersTab({ clientId }: { clientId: string }) {
  const [users, setUsers] = useState<ClientUserRow[]>([]);
  const [branches, setBranches] = useState<BranchOption[]>([]);
  const [roles, setRoles] = useState<RoleOption[]>([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState<UserForm>({ name: '', email: '', role: '', allBranches: true, branchIds: [] });
  const [saving, setSaving] = useState(false);
  const [invitation, setInvitation] = useState<{ sent: boolean; at: string; detail?: string | null; email: string } | null>(null);
  const base = `/tenant/clients/${clientId}/users`;

  const load = () => {
    api.get(base)
      .then(res => setUsers(res.data))
      .catch(err => toast.error(getErrorMessage(err, 'No se pudieron cargar los usuarios.')))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    load();
    api.get(`${base}/roles`).then(res => setRoles(res.data)).catch(() => {});
    api.get(`/tenant/clients/${clientId}/branches`).then(res => setBranches(res.data)).catch(() => {});
  }, [clientId]);

  const branchName = (branchId: string) => branches.find(b => b.id === branchId)?.name ?? '—';

  const openCreate = () => {
    setEditingId(null);
    setForm({ name: '', email: '', role: roles[0]?.value ?? '', allBranches: true, branchIds: [] });
    setShowModal(true);
  };

  const openEdit = (u: ClientUserRow) => {
    setEditingId(u.id);
    setForm({ name: u.name, email: u.email, role: u.role, allBranches: u.allBranches, branchIds: u.branchIds });
    setShowModal(true);
  };

  const toggleBranch = (branchId: string) =>
    setForm(prev => ({ ...prev, branchIds: prev.branchIds.includes(branchId) ? prev.branchIds.filter(b => b !== branchId) : [...prev.branchIds, branchId] }));

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    try {
      if (editingId) {
        await api.put(`${base}/${editingId}`, form);
        toast.success('Usuario actualizado.');
      } else {
        const res = await api.post(base, form);
        toast.success('Usuario creado.');
        setInvitation({ sent: res.data.invitationSent, at: new Date().toISOString(), detail: res.data.invitationError, email: form.email });
      }
      setShowModal(false);
      load();
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo guardar el usuario.'));
    } finally {
      setSaving(false);
    }
  };

  const resend = async (u: ClientUserRow) => {
    try {
      const res = await api.post(`${base}/${u.id}/resend-invitation`);
      setInvitation({ sent: res.data.sent, at: new Date().toISOString(), detail: res.data.detail, email: u.email });
      if (res.data.sent) toast.success('Invitación reenviada.');
      else toast.error(res.data.message || 'No se pudo enviar el correo.');
    } catch (err) {
      toast.error(getErrorMessage(err, 'Error al reenviar la invitación.'));
    }
  };

  const setActive = async (u: ClientUserRow, active: boolean) => {
    if (!active && !window.confirm(`¿Desactivar el acceso de ${u.name}?`)) return;
    try {
      await api.post(`${base}/${u.id}/${active ? 'reactivate' : 'deactivate'}`);
      toast.success(active ? 'Acceso reactivado.' : 'Acceso desactivado.');
      load();
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo cambiar el estado del acceso.'));
    }
  };

  if (loading) return <div className="flex justify-center py-16"><Loader2 className="animate-spin text-blue-600" /></div>;

  return (
    <div className="space-y-6">
      <div className="bg-white rounded-3xl p-8 shadow-sm border border-slate-100">
        <div className="flex justify-between items-start mb-2">
          <div>
            <h2 className="text-xl font-bold text-slate-800">Usuarios del portal</h2>
            <p className="text-slate-500 text-sm mt-1 max-w-2xl">
              Quienes ingresan a <span className="font-mono">clients.facil-factura.pro</span> con el slug de tu empresa. El Administrador configura y emite; el Facturador solo emite y consulta los documentos de sus sucursales.
              Siempre debe quedar un Administrador activo con acceso a todas las sucursales.
            </p>
          </div>
          <button onClick={openCreate} className="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-xl font-medium shadow-md transition-colors flex items-center gap-2 text-sm shrink-0">
            <Plus size={16} /> Nuevo usuario
          </button>
        </div>

        {invitation && (
          <div className={`flex items-start gap-2 my-4 px-3 py-2 rounded-lg border text-xs font-medium ${invitation.sent ? 'text-emerald-700 bg-emerald-50 border-emerald-200' : 'text-rose-700 bg-rose-50 border-rose-200'}`}>
            {invitation.sent ? <CheckCircle2 className="w-4 h-4 shrink-0 mt-0.5" /> : <ShieldAlert className="w-4 h-4 shrink-0 mt-0.5" />}
            <span>
              {invitation.sent
                ? `Correo de invitación enviado a ${invitation.email} (${new Date(invitation.at).toLocaleTimeString('es-CO')}).`
                : `No se pudo enviar el correo a ${invitation.email}${invitation.detail ? `: ${invitation.detail}` : '.'}`}
            </span>
          </div>
        )}

        {users.length === 0 ? (
          <div className="flex flex-col items-center justify-center text-center h-48 border-2 border-dashed border-slate-200 rounded-2xl bg-slate-50/50 mt-6">
            <UserRound className="text-slate-300 mb-2" size={32} />
            <h3 className="text-lg font-bold text-slate-700">Sin usuarios</h3>
            <p className="text-slate-500 max-w-sm mt-1 text-sm">Invita al primer Administrador para que el cliente pueda ingresar al portal.</p>
          </div>
        ) : (
          <div className="overflow-x-auto mt-6">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="bg-slate-50 text-slate-500 text-sm border-y border-slate-200">
                  <th className="font-semibold py-3 px-4 rounded-tl-xl">Usuario</th>
                  <th className="font-semibold py-3 px-4">Rol</th>
                  <th className="font-semibold py-3 px-4">Sucursales</th>
                  <th className="font-semibold py-3 px-4">Estado</th>
                  <th className="font-semibold py-3 px-4 text-center rounded-tr-xl">Acciones</th>
                </tr>
              </thead>
              <tbody>
                {users.map(u => (
                  <tr key={u.id} className="border-b border-slate-100 hover:bg-slate-50/50 transition-colors">
                    <td className="py-4 px-4">
                      <div className="font-bold text-slate-700">{u.name}</div>
                      <div className="text-xs text-slate-500">{u.email}</div>
                    </td>
                    <td className="py-4 px-4 text-sm text-slate-600">{u.role}</td>
                    <td className="py-4 px-4 text-sm text-slate-600">{u.allBranches ? 'Todas' : u.branchIds.map(branchName).join(', ')}</td>
                    <td className="py-4 px-4">
                      <span className={`px-3 py-1 rounded-full text-xs font-semibold border ${u.isActive ? 'text-emerald-700 bg-emerald-50 border-emerald-200' : 'text-rose-700 bg-rose-50 border-rose-200'}`}>
                        {u.isActive ? 'Activo' : 'Desactivado'}
                      </span>
                    </td>
                    <td className="py-4 px-4">
                      <div className="flex items-center justify-center gap-1">
                        <button onClick={() => openEdit(u)} className="p-2 text-slate-400 hover:text-blue-600 hover:bg-blue-50 rounded-lg transition-colors" title="Editar"><Pencil size={16} /></button>
                        {u.isActive && (
                          <button onClick={() => resend(u)} className="p-2 text-slate-400 hover:text-blue-600 hover:bg-blue-50 rounded-lg transition-colors" title="Reenviar invitación"><Mail size={16} /></button>
                        )}
                        {u.isActive ? (
                          <button onClick={() => setActive(u, false)} className="p-2 text-rose-500 hover:bg-rose-50 rounded-lg transition-colors" title="Desactivar"><PowerOff size={16} /></button>
                        ) : (
                          <button onClick={() => setActive(u, true)} className="p-2 text-emerald-600 hover:bg-emerald-50 rounded-lg transition-colors" title="Reactivar"><Power size={16} /></button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {showModal && (
        <div className="fixed inset-0 bg-slate-900/40 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-3xl p-8 max-w-xl w-full shadow-2xl max-h-[90vh] overflow-y-auto">
            <div className="flex justify-between items-center mb-6">
              <h3 className="text-xl font-bold text-slate-800 flex items-center gap-2">
                <UserRound className="text-blue-600" /> {editingId ? 'Editar usuario' : 'Nuevo usuario'}
              </h3>
              <button onClick={() => setShowModal(false)} className="text-slate-400 hover:text-slate-600 transition-colors"><X size={24} /></button>
            </div>

            <form onSubmit={submit} className="space-y-4">
              <div>
                <label className="block text-sm font-semibold text-slate-700 mb-1">Nombre</label>
                <input required type="text" className={inputClass} value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} />
              </div>
              <div>
                <label className="block text-sm font-semibold text-slate-700 mb-1">Correo de acceso</label>
                <input required type="email" disabled={!!editingId} className={`${inputClass} disabled:opacity-60`} value={form.email} onChange={e => setForm({ ...form, email: e.target.value })} />
                {!editingId && <p className="text-xs text-slate-400 mt-1">Le enviaremos un correo de invitación para que establezca su propia contraseña.</p>}
              </div>
              <div>
                <label className="block text-sm font-semibold text-slate-700 mb-1">Rol</label>
                <select required className={inputClass} value={form.role} onChange={e => setForm({ ...form, role: e.target.value })}>
                  {roles.map(r => <option key={r.value} value={r.value}>{r.label}</option>)}
                </select>
              </div>
              <div>
                <label className="block text-sm font-semibold text-slate-700 mb-2">Sucursales</label>
                <label className="flex items-center gap-2 text-sm text-slate-700 cursor-pointer mb-2">
                  <input type="checkbox" checked={form.allBranches} onChange={e => setForm({ ...form, allBranches: e.target.checked })} />
                  Todas las sucursales (incluidas las que se creen después)
                </label>
                {!form.allBranches && (
                  <div className="space-y-1.5 max-h-40 overflow-y-auto border border-slate-200 rounded-xl p-3">
                    {branches.filter(b => b.isActive).map(b => (
                      <label key={b.id} className="flex items-center gap-2 text-sm text-slate-700 cursor-pointer">
                        <input type="checkbox" checked={form.branchIds.includes(b.id)} onChange={() => toggleBranch(b.id)} />
                        {b.name}
                      </label>
                    ))}
                  </div>
                )}
              </div>

              <div className="pt-4 flex justify-end gap-3">
                <button type="button" onClick={() => setShowModal(false)} className="px-5 py-2.5 text-slate-500 hover:bg-slate-100 rounded-xl font-medium transition-colors">Cancelar</button>
                <button type="submit" disabled={saving} className="bg-blue-600 hover:bg-blue-700 text-white px-6 py-2.5 rounded-xl font-semibold shadow-md transition-colors disabled:opacity-50">
                  {saving ? 'Guardando...' : editingId ? 'Guardar cambios' : 'Crear e invitar'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
