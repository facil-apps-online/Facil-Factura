import React, { useEffect, useMemo, useState } from 'react';
import { ExternalLink } from 'lucide-react';
import { toast } from 'sonner';
import { api, getErrorMessage } from '../lib/api';
import { enterClientPortal } from '../lib/support';
import DataTable, { type DataTableColumn } from '@shared/components/DataTable';
import MonthRangeBar, { currentMonthRange, type MonthRangeValue } from '@shared/components/MonthRangeBar';
import SearchableSelect from '@shared/components/SearchableSelect';

type ChargeState = 'Draft' | 'Issued' | 'Rejected' | 'InProcess' | 'Discarded' | 'NoCharge';

interface ChargeRow {
  id: string;
  clientId: string;
  clientName: string;
  planName: string;
  runPeriod: string;
  feeAmount: number;
  extraAmount: number;
  total: number;
  description: string;
  documentId: string | null;
  state: ChargeState;
  number: string | null;
}

interface ChargesData {
  ownerClientId: string | null;
  summary: { drafts: number; draftsTotal: number; issued: number; issuedTotal: number; rejected: number; inProcess: number; discarded: number };
  items: ChargeRow[];
}

const money = (value: number) => `$${value.toLocaleString('es-CO')}`;
const MONTHS = ['enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio', 'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre'];
const cycleLabel = (iso: string) => { const d = new Date(iso); return `${MONTHS[d.getMonth()]} ${d.getFullYear()}`; };

const STATE_LABELS: Record<ChargeState, { text: string; style: string }> = {
  Draft: { text: 'Borrador', style: 'bg-amber-50 text-amber-700' },
  Issued: { text: 'Emitida', style: 'bg-emerald-50 text-emerald-700' },
  Rejected: { text: 'Rechazada', style: 'bg-rose-50 text-rose-700' },
  InProcess: { text: 'En proceso', style: 'bg-blue-50 text-blue-700' },
  Discarded: { text: 'Descartado', style: 'bg-slate-100 text-slate-500' },
  NoCharge: { text: 'Sin cobro', style: 'bg-slate-100 text-slate-500' },
};

// Cobros de planes generados por el sistema: un borrador por cliente y ciclo en la empresa emisora del tenant. Se revisan y se emiten desde esa
// empresa (el botón de cada factura o el lote de 50); aquí se ve en qué va cada uno.
export default function Charges() {
  const [range, setRange] = useState<MonthRangeValue>(currentMonthRange());
  const [stateFilter, setStateFilter] = useState('');
  const [data, setData] = useState<ChargesData | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    setLoading(true);
    api.get<ChargesData>(`/tenant/invoicing/charges?from=${range.from}&to=${range.to}`)
      .then(res => setData(res.data))
      .catch(err => { toast.error(getErrorMessage(err, 'No se pudieron cargar los cobros')); setData({ ownerClientId: null, summary: { drafts: 0, draftsTotal: 0, issued: 0, issuedTotal: 0, rejected: 0, inProcess: 0, discarded: 0 }, items: [] }); })
      .finally(() => setLoading(false));
  }, [range.from, range.to]);

  const rows = useMemo(() => (data?.items ?? []).filter(i => !stateFilter || i.state === stateFilter), [data, stateFilter]);

  const columns: DataTableColumn<ChargeRow>[] = [
    { key: 'client', header: 'Cliente', primary: true, cellClassName: 'font-bold text-slate-800', render: c => c.clientName, sortValue: c => c.clientName, searchValue: c => `${c.clientName} ${c.planName}` },
    { key: 'plan', header: 'Plan', render: c => c.planName, sortValue: c => c.planName },
    { key: 'cycle', header: 'Ciclo', cellClassName: 'capitalize', render: c => cycleLabel(c.runPeriod), sortValue: c => c.runPeriod },
    { key: 'fee', header: 'Cuota', align: 'right', cellClassName: 'font-mono', render: c => money(c.feeAmount), sortValue: c => c.feeAmount },
    { key: 'extra', header: 'Excedente', align: 'right', cellClassName: 'font-mono', render: c => money(c.extraAmount), sortValue: c => c.extraAmount },
    { key: 'total', header: 'Total', align: 'right', cellClassName: 'font-mono font-bold', render: c => money(c.total), sortValue: c => c.total },
    {
      key: 'state', header: 'Estado',
      render: c => <span className={`rounded-full px-2.5 py-1 text-xs font-semibold ${STATE_LABELS[c.state].style}`}>{STATE_LABELS[c.state].text}</span>,
      sortValue: c => c.state,
    },
    { key: 'number', header: 'Factura', cellClassName: 'font-mono text-slate-600', render: c => c.number ?? '—', sortValue: c => c.number ?? '' },
  ];

  const owner = data?.ownerClientId ?? null;
  const summary = data?.summary;

  return (
    <div className={`p-4 sm:p-6 ${loading ? 'opacity-70' : ''}`}>
      <div className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-bold text-slate-900">Cobros</h1>
          <p className="mt-1 text-sm text-slate-500">Borradores de cobro que el sistema deja cada ciclo, uno por cliente. Se revisan y se emiten desde tu empresa emisora, uno a uno o por lotes de 50.</p>
        </div>
        {owner && (
          <button onClick={() => enterClientPortal(owner, '/invoices')} className="inline-flex shrink-0 items-center justify-center gap-2 rounded-xl bg-slate-900 px-4 py-2.5 text-sm font-bold text-white hover:bg-slate-700">
            <ExternalLink size={16} /> Revisar y emitir en la empresa emisora
          </button>
        )}
      </div>

      {data && !owner && (
        <div className="mb-6 rounded-2xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900">
          Aún no tienes empresa emisora. Configúrala en <b>Planes → Facturación automática de planes</b> para que el sistema genere los borradores.
        </div>
      )}

      <div className="mb-6">
        <MonthRangeBar value={range} onChange={setRange} />
      </div>

      {summary && (
        <div className="mb-6 grid grid-cols-2 gap-4 md:grid-cols-4">
          <div className="rounded-2xl border border-amber-100 bg-amber-50/60 p-4">
            <p className="text-xs font-bold uppercase tracking-wide text-amber-700">Borradores por emitir</p>
            <p className="mt-1 text-2xl font-extrabold text-slate-800">{summary.drafts}</p>
            <p className="text-xs text-slate-500">{money(summary.draftsTotal)}</p>
          </div>
          <div className="rounded-2xl border border-emerald-100 bg-emerald-50/60 p-4">
            <p className="text-xs font-bold uppercase tracking-wide text-emerald-700">Emitidas</p>
            <p className="mt-1 text-2xl font-extrabold text-slate-800">{summary.issued}</p>
            <p className="text-xs text-slate-500">{money(summary.issuedTotal)}</p>
          </div>
          <div className="rounded-2xl border border-slate-200 bg-white p-4">
            <p className="text-xs font-bold uppercase tracking-wide text-slate-500">Rechazadas / en proceso</p>
            <p className="mt-1 text-2xl font-extrabold text-slate-800">{summary.rejected} / {summary.inProcess}</p>
          </div>
          <div className="rounded-2xl border border-slate-200 bg-white p-4">
            <p className="text-xs font-bold uppercase tracking-wide text-slate-500">Descartados</p>
            <p className="mt-1 text-2xl font-extrabold text-slate-800">{summary.discarded}</p>
            <p className="text-xs text-slate-500">No se vuelven a generar solos</p>
          </div>
        </div>
      )}

      <div className="mb-4 max-w-xs">
        <SearchableSelect value={stateFilter} onChange={setStateFilter} placeholder="Todos los estados"
          options={[{ value: '', label: 'Todos los estados' }, ...Object.entries(STATE_LABELS).map(([value, v]) => ({ value, label: v.text }))]} />
      </div>

      <DataTable
        rows={rows}
        rowKey={c => c.id}
        tableFrom="md"
        search="Buscar cliente o plan..."
        defaultSort={{ key: 'cycle', dir: 'desc' }}
        emptyMessage={data === null ? 'Cargando...' : 'No hay cobros generados en este período.'}
        columns={columns}
      />
    </div>
  );
}
