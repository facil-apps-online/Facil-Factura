import React, { useEffect, useState } from 'react';
import { Plus, Pencil, Power, PowerOff, Copy, ChevronDown, ChevronRight, X, Building2, KeyRound, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { api, getErrorMessage } from '../lib/api';

interface Branch {
  id: string;
  name: string;
  code: string;
  isMain: boolean;
  isActive: boolean;
  createdAt: string;
  deactivatedAt: string | null;
  address: string | null;
  city: string | null;
  cityCode: string | null;
  phone: string | null;
  email: string | null;
  subscriptionRate: number;
  pricePerDocument: number;
  liveApiKey: string;
  liveApiSecret: string;
  testApiKey: string;
  testApiSecret: string;
  userCount: number;
  resolutionIds: string[];
}

interface BranchForm {
  name: string;
  code: string;
  address: string;
  city: string;
  cityCode: string;
  phone: string;
  email: string;
  subscriptionRate: number;
  pricePerDocument: number;
  resolutionIds: string[];
}

const emptyForm: BranchForm = { name: '', code: '', address: '', city: '', cityCode: '', phone: '', email: '', subscriptionRate: 0, pricePerDocument: 0, resolutionIds: [] };

const inputClass = 'w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 outline-none';
const money = (n: number) => `$${(n ?? 0).toLocaleString('es-CO', { minimumFractionDigits: 0, maximumFractionDigits: 2 })}`;

function CopyField({ label, value }: { label: string; value: string }) {
  const copy = () => {
    navigator.clipboard.writeText(value);
    toast.success(`${label} copiada al portapapeles.`);
  };
  return (
    <div>
      <label className="block text-xs font-bold text-slate-500 uppercase mb-1">{label}</label>
      <div className="flex gap-2">
        <input type="text" readOnly value={value} className="flex-1 px-4 py-2 bg-white border border-slate-300 rounded-lg text-slate-600 font-mono text-sm outline-none" />
        <button type="button" onClick={copy} className="px-3 bg-white border border-slate-300 rounded-lg hover:bg-slate-50 text-slate-500 transition-colors" title={`Copiar ${label}`}>
          <Copy size={16} />
        </button>
      </div>
    </div>
  );
}

// Tarifa por integrador de UNA sucursal: por defecto se usa la tarifa plana de la sucursal; aquí se fija un override para un integrador.
function IntegratorRates({ clientId, branchId }: { clientId: string; branchId: string }) {
  const [rows, setRows] = useState<any[]>([]);
  const [savingId, setSavingId] = useState<string | null>(null);
  const base = `/tenant/clients/${clientId}/branches/${branchId}/integrator-billing`;

  const load = () => { api.get(base).then(res => setRows(res.data)).catch(() => {}); };
  useEffect(load, [clientId, branchId]);

  const patch = (rowId: string, change: any) => setRows(prev => prev.map(r => r.id === rowId ? { ...r, ...change } : r));

  const save = async (row: any) => {
    setSavingId(row.id);
    try {
      await api.put(`${base}/${row.id}`, { mode: row.mode, pricePerDocument: row.pricePerDocument, pricePerUser: row.pricePerUser });
      toast.success(`Tarifa de ${row.name} actualizada.`);
      load();
    } catch (err) {
      toast.error(getErrorMessage(err, 'Error al guardar la tarifa.'));
    } finally {
      setSavingId(null);
    }
  };

  const clear = async (row: any) => {
    try {
      await api.delete(`${base}/${row.id}`);
      toast.success(`${row.name} vuelve a la tarifa plana de la sucursal.`);
      load();
    } catch (err) {
      toast.error(getErrorMessage(err, 'Error al quitar el override.'));
    }
  };

  if (rows.length === 0) return <p className="text-sm text-slate-400">Sin integradores activos en el catálogo.</p>;

  return (
    <div className="space-y-3">
      {rows.map(row => (
        <div key={row.id} className="p-4 bg-white rounded-2xl border border-slate-200">
          <div className="flex items-center justify-between mb-3">
            <div className="flex items-center gap-2">
              <span className="font-bold text-slate-700">{row.name}</span>
              {row.hasOverride
                ? <span className="text-xs font-bold px-2 py-0.5 rounded-full bg-emerald-100 text-emerald-700">Override activo</span>
                : <span className="text-xs font-bold px-2 py-0.5 rounded-full bg-slate-200 text-slate-500">Usando tarifa plana</span>}
            </div>
            {row.hasOverride && (
              <button type="button" onClick={() => clear(row)} className="text-xs font-bold text-slate-400 hover:text-rose-500 transition-colors">Quitar override</button>
            )}
          </div>
          <div className="flex flex-wrap items-end gap-3">
            <div className="flex gap-2">
              {(['PerDocument', 'PerUser'] as const).map(mode => (
                <button key={mode} type="button" onClick={() => patch(row.id, { mode })}
                  className={`px-4 py-2 rounded-lg font-bold text-xs transition-colors ${row.mode === mode ? 'bg-blue-600 text-white shadow-sm' : 'bg-white border border-slate-200 text-slate-500 hover:bg-slate-100'}`}>
                  {mode === 'PerDocument' ? 'Por Documento' : 'Por Usuario'}
                </button>
              ))}
            </div>
            <div className="w-40">
              <label className="block text-xs font-bold text-slate-500 uppercase mb-1">{row.mode === 'PerDocument' ? 'Tarifa / documento' : 'Tarifa / usuario-mes'}</label>
              <input type="number" min="0" step="0.01"
                value={row.mode === 'PerDocument' ? row.pricePerDocument : row.pricePerUser}
                onChange={e => patch(row.id, row.mode === 'PerDocument' ? { pricePerDocument: parseFloat(e.target.value) || 0 } : { pricePerUser: parseFloat(e.target.value) || 0 })}
                className="w-full px-3 py-2 bg-white border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-blue-500 outline-none" />
            </div>
            <button type="button" onClick={() => save(row)} disabled={savingId === row.id}
              className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg font-bold text-xs transition-colors disabled:opacity-50">
              {savingId === row.id ? 'Guardando...' : 'Guardar override'}
            </button>
          </div>
        </div>
      ))}
    </div>
  );
}

export default function BranchesTab({ clientId }: { clientId: string }) {
  const [branches, setBranches] = useState<Branch[]>([]);
  const [resolutions, setResolutions] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [expanded, setExpanded] = useState<string | null>(null);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [showModal, setShowModal] = useState(false);
  const [form, setForm] = useState<BranchForm>(emptyForm);
  const [saving, setSaving] = useState(false);
  const base = `/tenant/clients/${clientId}/branches`;

  const load = () => {
    api.get(base)
      .then(res => setBranches(res.data))
      .catch(err => toast.error(getErrorMessage(err, 'No se pudieron cargar las sucursales.')))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    load();
    api.get(`/tenant/clients/${clientId}/resolutions`).then(res => setResolutions(res.data)).catch(() => {});
  }, [clientId]);

  const main = branches.find(b => b.isMain);

  const openCreate = () => {
    setEditingId(null);
    // La tarifa de una sucursal nueva arranca con la de la principal; el tenant la ajusta.
    setForm({ ...emptyForm, subscriptionRate: main?.subscriptionRate ?? 0, pricePerDocument: main?.pricePerDocument ?? 0 });
    setShowModal(true);
  };

  const openEdit = (b: Branch) => {
    setEditingId(b.id);
    setForm({
      name: b.name, code: b.code, address: b.address || '', city: b.city || '', cityCode: b.cityCode || '', phone: b.phone || '', email: b.email || '',
      subscriptionRate: b.subscriptionRate, pricePerDocument: b.pricePerDocument, resolutionIds: []
    });
    setShowModal(true);
  };

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    try {
      if (editingId) {
        await api.put(`${base}/${editingId}`, form);
        toast.success('Sucursal actualizada.');
      } else {
        await api.post(base, form);
        toast.success('Sucursal creada. Sus llaves de API ya están disponibles.');
      }
      setShowModal(false);
      load();
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo guardar la sucursal.'));
    } finally {
      setSaving(false);
    }
  };

  const toggleActive = async (b: Branch) => {
    const question = b.isActive
      ? `¿Desactivar la sucursal "${b.name}"? Se deja de cobrar desde hoy (prorrateado por días) y sus usuarios ya no podrán emitir con ella.`
      : `¿Reactivar la sucursal "${b.name}"? Vuelve a cobrarse desde hoy.`;
    if (!window.confirm(question)) return;
    try {
      await api.post(`${base}/${b.id}/${b.isActive ? 'deactivate' : 'reactivate'}`);
      toast.success(b.isActive ? 'Sucursal desactivada.' : 'Sucursal reactivada.');
      load();
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo cambiar el estado de la sucursal.'));
    }
  };

  const regenerate = async (b: Branch, env: 'live' | 'test') => {
    if (!window.confirm(`¿Regenerar las llaves de ${env === 'live' ? 'producción' : 'pruebas'} de "${b.name}"? Las actuales dejan de funcionar de inmediato.`)) return;
    try {
      await api.post(`${base}/${b.id}/generate-key?env=${env}`);
      toast.success(`Llaves de ${env === 'live' ? 'producción' : 'pruebas'} regeneradas.`);
      load();
    } catch (err) {
      toast.error(getErrorMessage(err, 'Error al generar la credencial.'));
    }
  };

  const toggleResolution = (resolutionId: string) =>
    setForm(prev => ({
      ...prev,
      resolutionIds: prev.resolutionIds.includes(resolutionId) ? prev.resolutionIds.filter(r => r !== resolutionId) : [...prev.resolutionIds, resolutionId]
    }));

  if (loading) return <div className="flex justify-center py-16"><Loader2 className="animate-spin text-blue-600" /></div>;

  return (
    <div className="space-y-6">
      <div className="bg-white rounded-3xl p-8 shadow-sm border border-slate-100">
        <div className="flex justify-between items-start mb-2">
          <div>
            <h2 className="text-xl font-bold text-slate-800">Sucursales</h2>
            <p className="text-slate-500 text-sm mt-1 max-w-2xl">
              Cada sucursal emite con sus propias llaves de API y se cobra con su tarifa. Los productos, terceros, plantillas y las bolsas o planes prepago son del cliente y los comparten todas.
            </p>
          </div>
          <button onClick={openCreate} className="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-xl font-medium shadow-md transition-colors flex items-center gap-2 text-sm shrink-0">
            <Plus size={16} /> Nueva sucursal
          </button>
        </div>

        <div className="space-y-4 mt-6">
          {branches.map(b => {
            const open = expanded === b.id;
            return (
              <div key={b.id} className={`rounded-2xl border ${b.isActive ? 'border-slate-200 bg-white' : 'border-slate-200 bg-slate-50 opacity-80'}`}>
                <div className="p-5 flex flex-wrap items-center gap-4 justify-between">
                  <div className="flex items-center gap-3 min-w-0">
                    <div className="w-10 h-10 rounded-xl bg-blue-50 text-blue-600 flex items-center justify-center shrink-0"><Building2 size={20} /></div>
                    <div className="min-w-0">
                      <div className="flex items-center gap-2 flex-wrap">
                        <span className="font-bold text-slate-800">{b.name}</span>
                        <span className="font-mono text-xs text-slate-500 bg-slate-100 px-2 py-0.5 rounded">{b.code}</span>
                        {b.isMain && <span className="text-xs font-bold px-2 py-0.5 rounded-full bg-blue-100 text-blue-700">Principal</span>}
                        <span className={`text-xs font-bold px-2 py-0.5 rounded-full ${b.isActive ? 'bg-emerald-100 text-emerald-700' : 'bg-rose-100 text-rose-700'}`}>
                          {b.isActive ? 'Activa' : 'Inactiva'}
                        </span>
                      </div>
                      <p className="text-xs text-slate-500 mt-1">
                        {b.address ? `${b.address}${b.city ? `, ${b.city}` : ''}` : 'Usa la dirección del cliente'} · {b.userCount} {b.userCount === 1 ? 'usuario' : 'usuarios'} · {b.resolutionIds.length} {b.resolutionIds.length === 1 ? 'resolución' : 'resoluciones'}
                      </p>
                      <p className="text-xs text-slate-500">
                        Cuota mensual {money(b.subscriptionRate)} · {money(b.pricePerDocument)} por documento
                        {b.deactivatedAt && ` · Desactivada el ${new Date(b.deactivatedAt).toLocaleDateString('es-CO')}`}
                      </p>
                    </div>
                  </div>
                  <div className="flex items-center gap-2">
                    <button onClick={() => openEdit(b)} className="p-2 text-slate-400 hover:text-blue-600 hover:bg-blue-50 rounded-lg transition-colors" title="Editar"><Pencil size={16} /></button>
                    {!b.isMain && (
                      <button onClick={() => toggleActive(b)}
                        className={`p-2 rounded-lg transition-colors ${b.isActive ? 'text-rose-500 hover:bg-rose-50' : 'text-emerald-600 hover:bg-emerald-50'}`}
                        title={b.isActive ? 'Desactivar' : 'Reactivar'}>
                        {b.isActive ? <PowerOff size={16} /> : <Power size={16} />}
                      </button>
                    )}
                    <button onClick={() => setExpanded(open ? null : b.id)} className="flex items-center gap-1 px-3 py-1.5 text-xs font-semibold text-slate-600 bg-slate-100 hover:bg-slate-200 rounded-lg transition-colors">
                      {open ? <ChevronDown size={14} /> : <ChevronRight size={14} />} Llaves y tarifas
                    </button>
                  </div>
                </div>

                {open && (
                  <div className="border-t border-slate-200 p-5 space-y-6 bg-slate-50/60 rounded-b-2xl">
                    <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
                      <div className="bg-white border border-slate-200 p-5 rounded-2xl space-y-3">
                        <div className="flex justify-between items-start">
                          <div>
                            <h3 className="font-semibold text-slate-800 flex items-center gap-2"><KeyRound size={16} /> Credenciales de producción</h3>
                            <p className="text-slate-500 text-xs">Para emitir documentos en producción.</p>
                          </div>
                          <button type="button" onClick={() => regenerate(b, 'live')} className="px-3 py-1.5 bg-slate-800 text-white rounded-lg text-xs font-medium hover:bg-slate-700 transition-colors">Regenerar</button>
                        </div>
                        <CopyField label="API Key" value={b.liveApiKey} />
                        <CopyField label="API Secret (HMAC)" value={b.liveApiSecret} />
                      </div>
                      <div className="bg-amber-50/50 border border-amber-200/50 p-5 rounded-2xl space-y-3">
                        <div className="flex justify-between items-start">
                          <div>
                            <h3 className="font-semibold text-slate-800 flex items-center gap-2"><KeyRound size={16} /> Credenciales de pruebas</h3>
                            <p className="text-slate-500 text-xs">Entorno de pruebas de la DIAN.</p>
                          </div>
                          <button type="button" onClick={() => regenerate(b, 'test')} className="px-3 py-1.5 bg-white border border-amber-300 text-amber-700 rounded-lg text-xs font-medium hover:bg-amber-50 transition-colors">Regenerar</button>
                        </div>
                        <CopyField label="API Key" value={b.testApiKey} />
                        <CopyField label="API Secret (HMAC)" value={b.testApiSecret} />
                      </div>
                    </div>

                    <div>
                      <h3 className="font-semibold text-slate-800 mb-1">Tarifa por integrador</h3>
                      <p className="text-slate-500 text-xs mb-3">Por defecto se cobra la tarifa plana de la sucursal. Actívalo solo para el integrador que necesite una tarifa o modo distinto.</p>
                      <IntegratorRates clientId={clientId} branchId={b.id} />
                    </div>
                  </div>
                )}
              </div>
            );
          })}
        </div>
      </div>

      {showModal && (
        <div className="fixed inset-0 bg-slate-900/40 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-3xl p-8 max-w-2xl w-full shadow-2xl max-h-[90vh] overflow-y-auto">
            <div className="flex justify-between items-center mb-6">
              <h3 className="text-xl font-bold text-slate-800 flex items-center gap-2">
                <Building2 className="text-blue-600" /> {editingId ? 'Editar sucursal' : 'Nueva sucursal'}
              </h3>
              <button onClick={() => setShowModal(false)} className="text-slate-400 hover:text-slate-600 transition-colors"><X size={24} /></button>
            </div>

            <form onSubmit={submit} className="space-y-4">
              {(() => {
                const isMainEdit = !!editingId && branches.find(b => b.id === editingId)?.isMain;
                return (
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <div>
                      <label className="block text-sm font-semibold text-slate-700 mb-1">Nombre</label>
                      <input required disabled={isMainEdit} type="text" className={`${inputClass} disabled:opacity-60`} value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} />
                    </div>
                    <div>
                      <label className="block text-sm font-semibold text-slate-700 mb-1">Código</label>
                      <input required disabled={isMainEdit} type="text" maxLength={30} className={`${inputClass} uppercase font-mono disabled:opacity-60`} value={form.code} onChange={e => setForm({ ...form, code: e.target.value })} />
                      <p className="text-xs text-slate-400 mt-1">Único dentro del cliente.</p>
                    </div>
                  </div>
                );
              })()}

              <div>
                <h4 className="text-sm font-bold text-slate-700 mb-2">Ubicación del emisor</h4>
                <p className="text-xs text-slate-400 mb-2">Si dejas la dirección vacía, los documentos de esta sucursal usan la dirección del cliente.</p>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <input type="text" placeholder="Dirección" className={`${inputClass} md:col-span-2`} value={form.address} onChange={e => setForm({ ...form, address: e.target.value })} />
                  <input type="text" placeholder="Ciudad" className={inputClass} value={form.city} onChange={e => setForm({ ...form, city: e.target.value })} />
                  <input type="text" placeholder="Código DANE del municipio (5 dígitos)" maxLength={5} className={`${inputClass} font-mono`} value={form.cityCode} onChange={e => setForm({ ...form, cityCode: e.target.value.replace(/\D/g, '') })} />
                  <input type="text" placeholder="Teléfono" className={inputClass} value={form.phone} onChange={e => setForm({ ...form, phone: e.target.value })} />
                  <input type="email" placeholder="Correo" className={inputClass} value={form.email} onChange={e => setForm({ ...form, email: e.target.value })} />
                </div>
              </div>

              <div>
                <h4 className="text-sm font-bold text-slate-700 mb-2">Tarifa</h4>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Cuota mensual</label>
                    <input type="number" min="0" step="0.01" className={`${inputClass} font-mono`} value={form.subscriptionRate} onChange={e => setForm({ ...form, subscriptionRate: parseFloat(e.target.value) || 0 })} />
                  </div>
                  <div>
                    <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Precio por documento</label>
                    <input type="number" min="0" step="0.01" className={`${inputClass} font-mono`} value={form.pricePerDocument} onChange={e => setForm({ ...form, pricePerDocument: parseFloat(e.target.value) || 0 })} />
                  </div>
                </div>
                {!editingId && <p className="text-xs text-slate-400 mt-1">Prellenada con la tarifa de la sucursal principal; ajústala si esta sucursal se cobra distinto.</p>}
              </div>

              {!editingId && resolutions.length > 0 && (
                <div>
                  <h4 className="text-sm font-bold text-slate-700 mb-2">Resoluciones que usará</h4>
                  <div className="space-y-1.5 max-h-40 overflow-y-auto border border-slate-200 rounded-xl p-3">
                    {resolutions.map(r => (
                      <label key={r.id} className="flex items-center gap-2 text-sm text-slate-700 cursor-pointer">
                        <input type="checkbox" checked={form.resolutionIds.includes(r.id)} onChange={() => toggleResolution(r.id)} />
                        <span className="font-bold">{r.documentType}</span> {r.prefix || '-'} · {r.resolutionNumber}
                      </label>
                    ))}
                  </div>
                  <p className="text-xs text-slate-400 mt-1">Puedes asignarlas después desde la pestaña Resoluciones.</p>
                </div>
              )}

              <div className="pt-4 flex justify-end gap-3">
                <button type="button" onClick={() => setShowModal(false)} className="px-5 py-2.5 text-slate-500 hover:bg-slate-100 rounded-xl font-medium transition-colors">Cancelar</button>
                <button type="submit" disabled={saving} className="bg-blue-600 hover:bg-blue-700 text-white px-6 py-2.5 rounded-xl font-semibold shadow-md transition-colors disabled:opacity-50">
                  {saving ? 'Guardando...' : editingId ? 'Guardar cambios' : 'Crear sucursal'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
