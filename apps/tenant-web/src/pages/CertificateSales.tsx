import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Plus, RefreshCw } from 'lucide-react';
import { toast } from 'sonner';
import { api, getErrorMessage } from '../lib/api';
import { useModules } from '../lib/modules';
import { requestStatus } from '../lib/certificateFormat';
import { CertificateDeliveryButtons } from '../components/CertificateDelivery';
import DataTable, { type DataTableColumn } from '@shared/components/DataTable';

interface SaleRow {
  id: string;
  name: string;
  taxId: string;
  email: string;
  blocked: boolean;
  kind: 'Titular' | 'Nativo' | 'Dataico';
  certificate: { id: string; expirationDate: string; installed: boolean; downloadable: boolean } | null;
  lastRequest: { status: string; createdAt: string } | null;
}

const KIND_STYLES: Record<SaleRow['kind'], { label: string; className: string }> = {
  Titular: { label: 'Titular', className: 'bg-violet-100 text-violet-700' },
  Nativo: { label: 'Factura (motor propio)', className: 'bg-blue-100 text-blue-700' },
  Dataico: { label: 'Factura (Dataico)', className: 'bg-amber-100 text-amber-700' },
};

const DAY = 24 * 60 * 60 * 1000;

// Venta de certificados: los clientes y titulares a quienes el tenant les vende certificados. Muestra el certificado vigente de cada uno, su
// vencimiento y la última solicitud, para ofrecer renovaciones. Solicitar y descargar se hace en la ficha de cada uno.
export default function CertificateSales() {
  const { invoicing } = useModules();
  const navigate = useNavigate();
  const [rows, setRows] = useState<SaleRow[]>([]);
  const [loading, setLoading] = useState(true);

  const load = () => {
    setLoading(true);
    api.get<SaleRow[]>('/tenant/certificates/sales')
      .then(res => setRows(res.data))
      .catch(err => toast.error(getErrorMessage(err, 'No se pudo cargar la venta de certificados')))
      .finally(() => setLoading(false));
  };

  useEffect(load, []);

  const columns: DataTableColumn<SaleRow>[] = [
    {
      key: 'name', header: 'Cliente o titular', primary: true,
      render: r => (
        <Link to={`/clients/edit/${r.id}?tab=certificate`} className="font-medium text-slate-800 hover:text-blue-600">
          {r.name}<span className="block text-xs font-normal text-slate-500">NIT {r.taxId}</span>
        </Link>
      ),
      sortValue: r => r.name, searchValue: r => `${r.name} ${r.taxId} ${r.email}`,
    },
    {
      key: 'kind', header: 'Tipo',
      render: r => <span className={`inline-flex rounded-full px-2.5 py-1 text-xs font-semibold ${KIND_STYLES[r.kind].className}`}>{KIND_STYLES[r.kind].label}</span>,
      sortValue: r => r.kind, searchValue: r => KIND_STYLES[r.kind].label,
    },
    {
      key: 'certificate', header: 'Certificado vigente',
      render: r => {
        if (!r.certificate) return <span className="text-xs text-slate-400">Sin certificado</span>;
        const days = Math.ceil((new Date(r.certificate.expirationDate).getTime() - Date.now()) / DAY);
        const tone = days < 0 ? 'text-rose-600' : days <= 30 ? 'text-amber-600' : 'text-slate-600';
        return (
          <div className="space-y-1">
            <p className={`text-sm font-medium ${tone}`}>
              {days < 0 ? 'Vencido' : `Vence en ${days} día${days === 1 ? '' : 's'}`}
              <span className="ml-2 text-xs font-normal text-slate-500">{new Date(r.certificate.expirationDate).toLocaleDateString('es-CO')}</span>
            </p>
            {r.certificate.installed && <p className="text-xs text-slate-500">Instalado para firmar</p>}
            {r.certificate.downloadable && <CertificateDeliveryButtons certificateId={r.certificate.id} fileName={`${r.name}-${r.taxId}`} compact />}
          </div>
        );
      },
      sortValue: r => r.certificate?.expirationDate ?? '9999',
    },
    {
      key: 'lastRequest', header: 'Última solicitud',
      render: r => r.lastRequest ? (
        <div>
          <p className="text-sm text-slate-700">{requestStatus(r.lastRequest.status).label}</p>
          <p className="text-xs text-slate-500">{new Date(r.lastRequest.createdAt).toLocaleDateString('es-CO')}</p>
        </div>
      ) : <span className="text-xs text-slate-400">—</span>,
      sortValue: r => r.lastRequest?.createdAt ?? '',
    },
    {
      key: 'action', header: '', cellClassName: 'text-right',
      render: r => (
        <button
          type="button"
          disabled={r.blocked}
          onClick={() => navigate(`/clients/edit/${r.id}?tab=certificate`)}
          className="rounded-lg bg-blue-600 px-3 py-1.5 text-xs font-bold text-white hover:bg-blue-700 disabled:opacity-40"
        >
          {r.certificate ? 'Renovar' : 'Solicitar'}
        </button>
      ),
    },
  ];

  return (
    <div className="p-8">
      <div className="mb-6 flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold text-slate-800">Venta de certificados</h1>
          <p className="text-slate-500 mt-2">
            Los clientes y titulares a quienes les vendes certificados digitales. Solicita o renueva desde aquí; los certificados se descuentan de tu saldo.
            {invoicing && ' Para habilitar a un cliente, actívalo en su ficha ("Puede comprar certificados").'}
          </p>
        </div>
        {invoicing && (
          <button
            type="button"
            onClick={() => navigate('/holders?new=1')}
            className="flex items-center gap-2 rounded-xl bg-blue-600 px-4 py-2.5 text-sm font-bold text-white shadow-md hover:bg-blue-700"
          >
            <Plus size={16} /> Nuevo titular
          </button>
        )}
      </div>

      <DataTable
        rows={rows}
        rowKey={r => r.id}
        loading={loading}
        tableFrom="lg"
        search="Buscar por nombre, NIT, correo o tipo..."
        defaultSort={{ key: 'certificate', dir: 'asc' }}
        emptyMessage="Aún no hay clientes habilitados para comprar certificados."
        toolbar={(
          <button onClick={load} title="Actualizar" aria-label="Actualizar" className="text-slate-400 hover:text-blue-600 transition-colors p-2.5 bg-white rounded-lg border border-slate-200 shadow-sm">
            <RefreshCw size={18} className={loading ? 'animate-spin' : ''} />
          </button>
        )}
        columns={columns}
      />
    </div>
  );
}
