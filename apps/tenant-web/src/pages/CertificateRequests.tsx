import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { ExternalLink, FileClock, RefreshCw } from 'lucide-react';
import { toast } from 'sonner';
import { api, getErrorMessage } from '../lib/api';
import { useModules } from '../lib/modules';
import { CHARGE_STATUS, money, requestStatus } from '../lib/certificateFormat';
import { CertificateDeliveryButtons } from '../components/CertificateDelivery';
import DataTable, { type DataTableColumn } from '@shared/components/DataTable';

interface RequestRow {
  id: string;
  clientId: string;
  clientName: string;
  clientTaxId: string;
  profileTitle: string;
  environment: 'Sandbox' | 'Production';
  status: string;
  kycUrl: string;
  lastErrorMessage: string;
  createdAt: string;
  completedAt: string | null;
  charge: { totalAmount: number; status: string } | null;
  certificate: { id: string; expirationDate: string } | null;
}

const IN_PROGRESS = new Set(['Draft', 'Submitted', 'WaitingForIdentity', 'IdentityReview', 'DocumentsRequired', 'DocumentsSubmitted', 'ProviderReview', 'CertificateProcessing', 'ReadyToDownload', 'Downloaded', 'Installing']);

// Seguimiento de las solicitudes de certificado de todos los clientes o titulares: estado en Viafirma, enlace de validación de identidad para
// reenviar al titular, cobro y, cuando se emite, descarga del .p12 y su contraseña.
export default function CertificateRequests() {
  const { invoicing } = useModules();
  const [rows, setRows] = useState<RequestRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [sendToHolder, setSendToHolder] = useState<boolean | null>(null);
  const holderLabel = invoicing ? 'cliente' : 'titular';

  const load = () => {
    setLoading(true);
    api.get<RequestRow[]>('/tenant/certificates/requests')
      .then(res => setRows(res.data))
      .catch(err => toast.error(getErrorMessage(err, 'No se pudieron cargar las solicitudes')))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    load();
    api.get('/tenant/certificates/settings').then(res => setSendToHolder(!!res.data.sendToHolderByEmail)).catch(() => {});
  }, []);

  const toggleSendToHolder = async () => {
    if (sendToHolder === null) return;
    try {
      const res = await api.put('/tenant/certificates/settings', { sendToHolderByEmail: !sendToHolder });
      setSendToHolder(!!res.data.sendToHolderByEmail);
      toast.success(res.data.sendToHolderByEmail ? 'El titular recibirá su certificado por correo' : 'Ya no se enviará el certificado al titular; descárgalo desde aquí');
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo guardar la preferencia'));
    }
  };

  const copyKyc = async (url: string) => {
    try {
      await navigator.clipboard.writeText(url);
      toast.success('Enlace copiado: envíaselo al titular para que valide su identidad');
    } catch {
      toast.error('No se pudo copiar el enlace');
    }
  };

  const columns: DataTableColumn<RequestRow>[] = [
    {
      key: 'client', header: invoicing ? 'Cliente' : 'Titular', primary: true,
      render: r => (
        <Link to={`/clients/edit/${r.clientId}?tab=certificate`} className="font-medium text-slate-800 hover:text-blue-600">
          {r.clientName}<span className="block text-xs font-normal text-slate-500">NIT {r.clientTaxId}</span>
        </Link>
      ),
      sortValue: r => r.clientName, searchValue: r => `${r.clientName} ${r.clientTaxId}`,
    },
    {
      key: 'profile', header: 'Certificado', cellClassName: 'text-slate-600',
      render: r => <>{r.profileTitle}{r.environment === 'Sandbox' && <span className="ml-2 rounded-full bg-violet-100 px-2 py-0.5 text-xs font-semibold text-violet-700">Sandbox</span>}</>,
      sortValue: r => r.profileTitle, searchValue: r => r.profileTitle,
    },
    { key: 'createdAt', header: 'Solicitada', cellClassName: 'text-slate-600 whitespace-nowrap', render: r => new Date(r.createdAt).toLocaleDateString('es-CO'), sortValue: r => r.createdAt },
    {
      key: 'status', header: 'Estado',
      render: r => {
        const status = requestStatus(r.status);
        return (
          <div className="space-y-1">
            <span className={`inline-flex rounded-full px-2.5 py-1 text-xs font-semibold ${status.className}`}>{status.label}</span>
            {(r.status === 'Rejected' || r.status === 'Failed') && r.lastErrorMessage && <p className="max-w-xs text-xs text-rose-600">{r.lastErrorMessage}</p>}
          </div>
        );
      },
      sortValue: r => r.status, searchValue: r => requestStatus(r.status).label,
    },
    {
      key: 'charge', header: 'Valor',
      render: r => r.charge
        ? <span className="whitespace-nowrap">{money(r.charge.totalAmount)} <span className="text-xs text-slate-500">· {CHARGE_STATUS[r.charge.status] ?? r.charge.status}</span></span>
        : <span className="text-xs text-slate-400">Sin cobro</span>,
      sortValue: r => r.charge?.totalAmount ?? 0,
    },
    {
      key: 'actions', header: 'Acciones',
      render: r => r.certificate ? (
        <div className="space-y-1">
          <CertificateDeliveryButtons certificateId={r.certificate.id} fileName={`${r.clientName}-${r.clientTaxId}`} compact />
          <p className="text-xs text-slate-500">Vence {new Date(r.certificate.expirationDate).toLocaleDateString('es-CO')}</p>
        </div>
      ) : r.kycUrl && IN_PROGRESS.has(r.status) ? (
        <div className="flex flex-wrap gap-2">
          <a href={r.kycUrl} target="_blank" rel="noreferrer" className="inline-flex items-center gap-1 text-xs font-semibold text-blue-700 hover:underline"><ExternalLink size={12} /> Validación de identidad</a>
          <button type="button" onClick={() => copyKyc(r.kycUrl)} className="text-xs font-semibold text-slate-500 hover:text-slate-800">Copiar enlace</button>
        </div>
      ) : <span className="text-xs text-slate-400">—</span>,
    },
  ];

  const inProgress = rows.filter(r => IN_PROGRESS.has(r.status)).length;

  return (
    <div className="p-8">
      <div className="mb-6 flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold text-slate-800">Solicitudes de certificados</h1>
          <p className="text-slate-500 mt-2">Para solicitar uno nuevo entra a la ficha del {holderLabel}, pestaña Certificado Digital. Viafirma actualiza el estado cada pocos minutos.</p>
        </div>
        {sendToHolder !== null && (
          <label className="flex max-w-sm cursor-pointer items-start gap-3 rounded-2xl border border-slate-200 bg-white p-4">
            <input type="checkbox" checked={sendToHolder} onChange={toggleSendToHolder} className="mt-1 h-5 w-5 rounded accent-blue-600" />
            <span>
              <span className="block text-sm font-semibold text-slate-700">Enviar el certificado al titular por correo</span>
              <span className="block text-xs text-slate-500">Al emitirse le llegan el .p12 y, en otro correo, su contraseña. Apágalo si prefieres entregarlo tú: lo descargas desde aquí.</span>
            </span>
          </label>
        )}
      </div>

      {inProgress > 0 && (
        <div className="mb-4 inline-flex items-center gap-2 rounded-xl border border-amber-200 bg-amber-50 px-4 py-2 text-sm font-semibold text-amber-700">
          <FileClock size={16} /> {inProgress} en curso
        </div>
      )}

      <DataTable
        rows={rows}
        rowKey={r => r.id}
        loading={loading}
        tableFrom="lg"
        search="Buscar por titular, NIT, certificado o estado..."
        defaultSort={{ key: 'createdAt', dir: 'desc' }}
        emptyMessage={`Aún no hay solicitudes. Solicita la primera desde la ficha de un ${holderLabel}.`}
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
