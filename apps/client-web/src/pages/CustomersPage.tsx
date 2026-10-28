import React, { useState, useEffect, useRef } from 'react';
import { Plus } from 'lucide-react';
import { toast } from 'sonner';
import { api } from '../lib/api';
import { useNumberFormat } from '../lib/numberFormat';
import BulkImportModal from '../components/BulkImportModal';
import CustomerFormModal from '../components/CustomerFormModal';
import DataTable, { type DataTableColumn, type DataTableQuery } from '@shared/components/DataTable';
import RowIconButton from '../components/RowIconButton';
import { useConfirm } from '@/components/ConfirmDialog';
import TenantSyncModal from '../components/TenantSyncModal';
import { useSession } from '../context/SessionContext';

const PARTY_TYPES = [
  { value: 'Cliente', label: 'Clientes' },
  { value: 'Proveedor', label: 'Proveedores' },
  { value: 'Empleado', label: 'Empleados' },
  // Solo para clientes con el tipo de factura de mandato habilitado (ver hasMandates).
  { value: 'Mandante', label: 'Mandantes' }
];

export default function CustomersPage() {
  const confirm = useConfirm();
  const { features, isAdministrator } = useSession();
  // Empresa propietaria del tenant: sincroniza los clientes del tenant como terceros y puede filtrarlos.
  const [syncOpen, setSyncOpen] = useState(false);
  const [importOpen, setImportOpen] = useState(false);
  const [originFilter, setOriginFilter] = useState<'' | 'tenant' | 'incomplete'>('');
  // La lista se pide por páginas al servidor (hay clientes con miles de terceros): solo se tienen los de la página que se ve.
  const [pageData, setPageData] = useState<{ items: any[]; total: number }>({ items: [], total: 0 });
  const [loading, setLoading] = useState(true);
  const lastQuery = useRef<DataTableQuery | null>(null);
  const requestId = useRef(0);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingCustomer, setEditingCustomer] = useState<any>(null);
  const [activeType, setActiveType] = useState('Cliente');
  const [identificationTypeOptions, setIdentificationTypeOptions] = useState<{ value: string, label: string }[]>([]);
  const [contractLabels, setContractLabels] = useState<Record<string, string>>({});
  const [workerLabels, setWorkerLabels] = useState<Record<string, string>>({});
  // La pestaña de mandantes solo aparece si el tenant le habilitó al cliente el tipo de factura de mandato.
  const [hasMandates, setHasMandates] = useState(false);
  const fmt = useNumberFormat();

  useEffect(() => {
    api.get('/client/identification-types')
      .then(res => setIdentificationTypeOptions(res.data.map((t: any) => ({ value: t.code, label: t.name }))))
      .catch(() => {});
    api.get('/client/invoices/document-types')
      .then(res => setHasMandates(res.data.some((t: any) => t.operationType === '11')))
      .catch(() => {});
    // Nombres legibles del tipo de contrato y de trabajador para la lista de empleados.
    api.get('/client/tax-catalog?kind=ContractType')
      .then(res => setContractLabels(Object.fromEntries(res.data.map((c: any) => [c.category, c.name]))))
      .catch(() => {});
    api.get('/client/tax-catalog?kind=WorkerType')
      .then(res => setWorkerLabels(Object.fromEntries(res.data.map((c: any) => [c.category, c.name]))))
      .catch(() => {});
  }, []);

  // La tabla avisa qué página pide el usuario (búsqueda, orden, página, tamaño, tipo de tercero o filtro de origen); las respuestas que llegan
  // tarde, de una petición ya reemplazada, se descartan.
  const fetchPage = (query: DataTableQuery) => {
    lastQuery.current = query;
    const id = ++requestId.current;
    setLoading(true);
    api.get('/client/customers', {
      params: { partyType: activeType, q: query.search || undefined, origin: originFilter || undefined, sort: query.sort?.key, dir: query.sort?.dir, page: query.page, pageSize: query.pageSize }
    })
      .then(res => { if (id === requestId.current) setPageData(res.data); })
      .catch(() => { if (id === requestId.current) toast.error('Error al cargar clientes'); })
      .finally(() => { if (id === requestId.current) setLoading(false); });
  };

  // Vuelve a pedir la página que se está viendo (después de guardar, eliminar, importar o sincronizar).
  const loadCustomers = () => { if (lastQuery.current) fetchPage(lastQuery.current); };

  const handleOpenModal = (customer?: any) => {
    setEditingCustomer(customer || null);
    setIsModalOpen(true);
  };

  const handleSaved = () => {
    setIsModalOpen(false);
    loadCustomers();
  };

  const handleDelete = async (id: string) => {
    if (!(await confirm('¿Estás seguro de eliminar este cliente?'))) return;
    try {
      await api.delete(`/client/customers/${id}`);
      toast.success('Cliente eliminado');
      loadCustomers();
    } catch (err: any) {
      toast.error('Error al eliminar');
    }
  };

  const showTenantTools = features.tenantOwner && activeType === 'Cliente';

  const columns: DataTableColumn<any>[] = [
    {
      key: 'identification',
      header: 'Identificación',
      cellClassName: 'whitespace-nowrap font-medium text-slate-700',
      render: c => (
        <>
          {identificationTypeOptions.find(o => o.value === c.identificationType)?.label || c.identificationType} {c.identificationNumber}
          {c.verificationDigit && `-${c.verificationDigit}`}
        </>
      ),
      sortValue: c => c.identificationNumber
    },
    {
      key: 'name',
      header: 'Razón Social / Nombre',
      primary: true,
      cellClassName: 'font-bold text-slate-900',
      render: c => (
        <span className="flex flex-wrap items-center gap-2">
          {c.name}
          {c.linkedClientId && <span className="rounded-full bg-blue-50 px-2 py-0.5 text-[11px] font-semibold text-blue-700" title="Es un cliente de tu tenant, sincronizado desde su lista">Cliente del tenant</span>}
        </span>
      ),
      sortValue: c => c.name
    },
    {
      key: 'contact',
      header: 'Contacto',
      cellClassName: 'text-sm text-slate-500',
      render: c => (
        <>
          {c.email}<br />{c.phone}
        </>
      ),
      sortValue: c => c.email
    },
    ...(activeType === 'Empleado'
      ? [
          {
            key: 'contract',
            header: 'Contrato',
            cellClassName: 'text-sm text-slate-500',
            render: (c: any) => (
              <>
                {contractLabels[c.contractType] || c.contractType || '—'}
                <br />
                {workerLabels[c.workerType] || c.workerType || ''}
              </>
            ),
            sortValue: (c: any) => contractLabels[c.contractType] || c.contractType
          },
          {
            key: 'baseSalary',
            header: 'Salario base',
            cellClassName: 'whitespace-nowrap text-sm text-slate-500',
            align: 'right' as const,
            render: (c: any) => (c.baseSalary ? `$${fmt.number(c.baseSalary, 3)}` : '—'),
            sortValue: (c: any) => c.baseSalary
          },
          {
            key: 'municipality',
            header: 'Municipio',
            cellClassName: 'text-sm text-slate-500',
            render: (c: any) =>
              (c.cityCode || '').trim().length >= 5 ? (
                <>
                  {c.cityName}
                  <br />
                  <span className="font-mono text-xs">{c.cityCode}</span>
                </>
              ) : (
                <span className="font-bold text-red-600">Falta código DANE</span>
              ),
            sortValue: (c: any) => c.cityName
          }
        ]
      : [
          {
            key: 'regime',
            header: 'Régimen',
            cellClassName: 'whitespace-nowrap text-sm text-slate-500',
            render: (c: any) => (c.taxRegime === '48' ? 'Resp. IVA' : 'No Resp. IVA'),
            sortValue: (c: any) => (c.taxRegime === '48' ? 'Resp. IVA' : 'No Resp. IVA')
          }
        ])
  ];

  return (
    <div className="p-3 sm:p-4">
      <div className="mb-6 flex flex-col gap-4 sm:mb-8 sm:flex-row sm:items-center sm:justify-between">
        <div className="min-w-0">
          <h1 className="text-2xl font-extrabold text-slate-800 sm:text-3xl">Terceros</h1>
          <p className="mt-1 text-slate-500">Administra clientes, proveedores y empleados.</p>
        </div>
        <div className="flex flex-wrap gap-3">
          {showTenantTools && isAdministrator && (
            <button onClick={() => setSyncOpen(true)} className="rounded-xl border border-slate-200 bg-white px-5 py-2.5 text-sm font-bold text-slate-700 shadow-sm hover:bg-slate-50">
              Sincronizar clientes del tenant
            </button>
          )}
          <button onClick={() => setImportOpen(true)} className="rounded-xl border border-slate-200 bg-white px-5 py-2.5 text-sm font-bold text-slate-700 shadow-sm hover:bg-slate-50">
            Carga masiva
          </button>
          <button
            onClick={() => handleOpenModal()}
            className="bg-primary hover:bg-primary/90 text-white px-5 py-2.5 rounded-xl font-bold flex items-center gap-2 shadow-sm transition-all"
          >
            <Plus size={20} /> Nuevo {activeType}
          </button>
        </div>
      </div>

      <div className="mb-6 flex flex-wrap items-center gap-2">
        {PARTY_TYPES.filter(pt => pt.value !== 'Mandante' || hasMandates).map(pt => (
          <button
            key={pt.value}
            onClick={() => setActiveType(pt.value)}
            className={`px-4 py-2.5 rounded-xl font-bold text-sm transition-colors ${
              activeType === pt.value ? 'bg-primary text-white shadow-sm' : 'bg-white text-slate-500 border border-slate-200 hover:bg-slate-50'
            }`}
          >
            {pt.label}
          </button>
        ))}
      </div>

      {showTenantTools && (
        <div className="mb-4 flex flex-wrap items-center gap-2">
          {([['', 'Todos'], ['tenant', 'Del tenant'], ['incomplete', 'Incompletos']] as const).map(([value, label]) => (
            <button key={value} onClick={() => setOriginFilter(value)}
              className={`rounded-full px-3 py-1.5 text-xs font-bold ${originFilter === value ? 'bg-slate-900 text-white' : 'bg-slate-100 text-slate-600 hover:bg-slate-200'}`}>{label}</button>
          ))}
        </div>
      )}

      <DataTable
        rows={pageData.items}
        server={{ total: pageData.total, onQueryChange: fetchPage }}
        loading={loading}
        resetKey={`${activeType}|${originFilter}`}
        columns={columns}
        rowKey={c => c.id}
        tableFrom="wide"
        search="Buscar por nombre, identificación, email o teléfono..."
        defaultSort={{ key: 'name', dir: 'asc' }}
        emptyMessage={`No tienes ${activeType.toLowerCase()}s registrados.`}
        actions={c => (
          <>
            <RowIconButton action="edit" label={`Editar ${c.name}`} onClick={() => handleOpenModal(c)} />
            <RowIconButton action="delete" label={`Eliminar ${c.name}`} onClick={() => handleDelete(c.id)} />
          </>
        )}
      />

      <TenantSyncModal open={syncOpen} onClose={() => setSyncOpen(false)} onDone={loadCustomers} />
      {importOpen && (<BulkImportModal title="Carga masiva de terceros" entityLabel="terceros" endpoint="/client/customers" addressOptions onDone={loadCustomers} onClose={() => setImportOpen(false)} />)}

      <CustomerFormModal
        open={isModalOpen}
        editingCustomer={editingCustomer}
        defaultPartyType={activeType}
        onClose={() => setIsModalOpen(false)}
        onSaved={handleSaved}
      />
    </div>
  );
}
