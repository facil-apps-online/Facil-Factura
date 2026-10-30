import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { ShieldCheck, AlertTriangle, Clock, FileKey } from 'lucide-react';
import { api } from '../lib/api';
import { useModules } from '../lib/modules';
import { CertificateDeliveryButtons } from '../components/CertificateDelivery';

interface CertificateRow {
  id: string;
  clientId: string;
  clientName: string;
  fileName: string;
  expirationDate: string;
  createdAt: string;
  environment: 'Sandbox' | 'Production';
  // Emitido por la plataforma (con su .p12 y contraseña guardados): se puede descargar.
  downloadable: boolean;
}

const DAYS_SOON_THRESHOLD = 30;

function daysUntil(dateStr: string): number {
  const diffMs = new Date(dateStr).getTime() - Date.now();
  return Math.ceil(diffMs / (1000 * 60 * 60 * 24));
}

function statusFor(days: number): { label: string; badgeClass: string; icon: React.ReactNode; rowClass: string } {
  if (days < 0) {
    return {
      label: `Expirado hace ${Math.abs(days)} día${Math.abs(days) === 1 ? '' : 's'}`,
      badgeClass: 'bg-rose-100 text-rose-700',
      icon: <AlertTriangle size={24} />,
      rowClass: 'border-rose-200',
    };
  }
  if (days <= DAYS_SOON_THRESHOLD) {
    return {
      label: `Vence en ${days} día${days === 1 ? '' : 's'}`,
      badgeClass: 'bg-amber-100 text-amber-700',
      icon: <Clock size={24} />,
      rowClass: 'border-amber-200',
    };
  }
  return {
    label: 'Válido',
    badgeClass: 'bg-emerald-100 text-emerald-700',
    icon: <ShieldCheck size={24} />,
    rowClass: 'border-slate-200',
  };
}

export default function Certificates() {
  const { invoicing } = useModules();
  const [certificates, setCertificates] = useState<CertificateRow[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get('/tenant/certificates')
      .then(res => setCertificates(res.data))
      .catch(() => setCertificates([]))
      .finally(() => setLoading(false));
  }, []);

  const expiredCount = certificates.filter(c => daysUntil(c.expirationDate) < 0).length;
  const soonCount = certificates.filter(c => { const d = daysUntil(c.expirationDate); return d >= 0 && d <= DAYS_SOON_THRESHOLD; }).length;

  return (
    <div className="p-8">
      <div className="mb-8">
        <h1 className="text-3xl font-bold text-slate-800">Certificados digitales</h1>
        <p className="text-slate-500 mt-2">{invoicing ? 'Consulta los certificados de tus clientes y sus fechas de vencimiento.' : 'Consulta los certificados de tus titulares y sus fechas de vencimiento.'}</p>
      </div>

      {!loading && certificates.length > 0 && (
        <div className="flex gap-4 mb-6">
          {expiredCount > 0 && (
            <div className="px-4 py-2 rounded-xl bg-rose-50 border border-rose-200 text-rose-700 text-sm font-semibold">
              {expiredCount} expirado{expiredCount === 1 ? '' : 's'}
            </div>
          )}
          {soonCount > 0 && (
            <div className="px-4 py-2 rounded-xl bg-amber-50 border border-amber-200 text-amber-700 text-sm font-semibold">
              {soonCount} por vencer (≤{DAYS_SOON_THRESHOLD} días)
            </div>
          )}
        </div>
      )}

      {loading ? (
        <div className="text-slate-400">Cargando...</div>
      ) : certificates.length === 0 ? (
        <div className="bg-white rounded-2xl border border-slate-200 p-10 text-center text-slate-400">
          <FileKey className="mx-auto mb-3 text-slate-300" size={40} />
          {invoicing
            ? 'Ningún cliente tiene un certificado cargado todavía. Súbelos desde la ficha de cada cliente.'
            : 'Ningún titular tiene un certificado todavía. Solicítalos desde la ficha de cada titular.'}
        </div>
      ) : (
        <div className="space-y-4">
          {certificates.map(cert => {
            const days = daysUntil(cert.expirationDate);
            const status = statusFor(days);
            return (
              <div
                key={cert.id}
                className={`bg-white p-5 rounded-2xl shadow-sm border ${status.rowClass} flex flex-wrap items-center justify-between gap-4 hover:shadow-md transition-shadow`}
              >
                <Link to={`/clients/edit/${cert.clientId}?tab=certificate`} className="flex items-center gap-4">
                  <div className={`w-12 h-12 rounded-full flex items-center justify-center ${status.badgeClass}`}>
                    {status.icon}
                  </div>
                  <div>
                    <h3 className="font-bold text-slate-800">
                      {cert.clientName}
                      {cert.environment === 'Sandbox' && <span className="ml-2 rounded-full bg-violet-100 px-2 py-0.5 text-xs font-semibold text-violet-700">Sandbox</span>}
                    </h3>
                    <p className="text-sm text-slate-500">
                      Vence: {new Date(cert.expirationDate).toLocaleDateString('es-CO', { day: 'numeric', month: 'long', year: 'numeric' })}
                    </p>
                  </div>
                </Link>
                <div className="flex flex-wrap items-center gap-3">
                  {cert.downloadable && <CertificateDeliveryButtons certificateId={cert.id} fileName={cert.clientName} compact />}
                  <span className={`inline-flex px-3 py-1 rounded-full text-xs font-semibold ${status.badgeClass}`}>
                    {status.label}
                  </span>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
