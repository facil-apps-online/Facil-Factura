import React, { useEffect, useState } from 'react';
import { Archive, ArchiveRestore, Edit2, Plus } from 'lucide-react';
import { toast } from 'sonner';
import { api, getErrorMessage } from '../lib/api';
import DataTable, { type DataTableColumn } from '@shared/components/DataTable';
import PlanFormModal, { type Plan, CYCLE_LABELS, SCOPE_LABELS } from '../components/PlanFormModal';
import PlanSettingsCard, { EXCEED_LABELS } from '../components/PlanSettingsCard';
import InvoicingSettingsCard from '../components/InvoicingSettingsCard';

const money = (value: number) => `$${value.toLocaleString('es-CO')}`;

const includedText = (p: Plan) => (p.includedDocuments === null ? 'Ilimitados' : p.includedDocuments === 0 ? 'Ninguno (por documento)' : `${p.includedDocuments} por ciclo`);

// Planes comerciales del tenant: lo que ofrece y cobra a sus clientes. Archivar no borra: los clientes que ya lo tienen lo conservan.
export default function Plans() {
  const [plans, setPlans] = useState<Plan[] | null>(null);
  const [showArchived, setShowArchived] = useState(false);
  const [editing, setEditing] = useState<Plan | null>(null);
  const [showModal, setShowModal] = useState(false);

  const load = () =>
    api.get<Plan[]>(`/tenant/plans?includeArchived=${showArchived}`)
      .then(res => setPlans(res.data))
      .catch(err => { toast.error(getErrorMessage(err, 'No se pudieron cargar los planes')); setPlans([]); });

  useEffect(() => { load(); }, [showArchived]);

  const setArchived = async (plan: Plan, archive: boolean) => {
    if (archive && !window.confirm(`¿Archivar el plan "${plan.name}"? Deja de ofrecerse para nuevos clientes; los que ya lo tienen lo conservan.`)) return;
    try {
      await api.post(`/tenant/plans/${plan.id}/${archive ? 'archive' : 'restore'}`);
      toast.success(archive ? 'Plan archivado' : 'Plan restaurado');
      load();
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo cambiar el plan'));
    }
  };

  const columns: DataTableColumn<Plan>[] = [
    {
      key: 'name', header: 'Plan', primary: true, cellClassName: 'font-bold text-slate-800',
      render: p => (
        <div>
          <div className="flex items-center gap-2">{p.name}{!p.isActive && <span className="rounded-full bg-slate-100 px-2 py-0.5 text-xs font-semibold text-slate-500">Archivado</span>}</div>
          {p.description && <div className="text-xs font-normal text-slate-500">{p.description}</div>}
        </div>
      ),
      sortValue: p => p.name, searchValue: p => `${p.name} ${p.description ?? ''}`,
    },
    { key: 'cycle', header: 'Ciclo', render: p => CYCLE_LABELS[p.cycle], sortValue: p => p.cycle },
    { key: 'fee', header: 'Cuota', align: 'right', cellClassName: 'font-mono', render: p => money(p.fixedFee), sortValue: p => p.fixedFee },
    { key: 'included', header: 'Documentos incluidos', render: includedText, sortValue: p => p.includedDocuments ?? Number.MAX_SAFE_INTEGER },
    { key: 'price', header: 'Precio por documento', align: 'right', cellClassName: 'font-mono', render: p => (p.includedDocuments === null ? '—' : money(p.pricePerDocument)), sortValue: p => p.pricePerDocument },
    { key: 'scope', header: 'Aplica a', render: p => SCOPE_LABELS[p.scope], sortValue: p => p.scope },
    { key: 'exceed', header: 'Al llegar al límite', render: p => (p.includedDocuments ? (p.exceedAction ? EXCEED_LABELS[p.exceedAction] : 'Opción general') : '—'), sortValue: p => p.exceedAction ?? '' },
    { key: 'clients', header: 'Clientes', align: 'right', render: p => p.clients, sortValue: p => p.clients },
  ];

  return (
    <div className="p-4 sm:p-6">
      <div className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-bold text-slate-900">Planes</h1>
          <p className="mt-1 text-sm text-slate-500">Arma los planes que ofreces a tus clientes: cuota, documentos incluidos, precio por documento y qué cuenta como documento.</p>
        </div>
        <div className="flex items-center gap-4">
          <label className="flex items-center gap-2 text-sm text-slate-600">
            <input type="checkbox" checked={showArchived} onChange={e => setShowArchived(e.target.checked)} className="h-4 w-4 accent-blue-600" /> Ver archivados
          </label>
          <button onClick={() => { setEditing(null); setShowModal(true); }} className="inline-flex items-center gap-2 rounded-xl bg-blue-600 px-4 py-2.5 text-sm font-bold text-white hover:bg-blue-700">
            <Plus size={16} /> Nuevo plan
          </button>
        </div>
      </div>

      <PlanSettingsCard />
      <InvoicingSettingsCard />

      <DataTable
        rows={plans ?? []}
        rowKey={p => p.id}
        tableFrom="md"
        search="Buscar plan..."
        defaultSort={{ key: 'name', dir: 'asc' }}
        emptyMessage={plans === null ? 'Cargando...' : 'Aún no tienes planes. Crea el primero con "Nuevo plan".'}
        columns={columns}
        actions={p => (
          <>
            <button onClick={() => { setEditing(p); setShowModal(true); }} title="Editar" aria-label={`Editar ${p.name}`}
              className="rounded border border-slate-200 bg-white p-1.5 text-slate-400 shadow-sm transition-colors hover:text-blue-600"><Edit2 size={16} /></button>
            {p.isActive ? (
              <button onClick={() => setArchived(p, true)} title="Archivar" aria-label={`Archivar ${p.name}`}
                className="rounded border border-slate-200 bg-white p-1.5 text-slate-400 shadow-sm transition-colors hover:text-amber-600"><Archive size={16} /></button>
            ) : (
              <button onClick={() => setArchived(p, false)} title="Restaurar" aria-label={`Restaurar ${p.name}`}
                className="rounded border border-slate-200 bg-white p-1.5 text-slate-400 shadow-sm transition-colors hover:text-emerald-600"><ArchiveRestore size={16} /></button>
            )}
          </>
        )}
      />

      <PlanFormModal open={showModal} plan={editing} onClose={() => setShowModal(false)} onSaved={load} />
    </div>
  );
}
