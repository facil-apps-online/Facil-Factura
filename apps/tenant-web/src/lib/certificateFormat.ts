// Formato y etiquetas de certificados, compartidos por las pantallas de certificados del tenant.
export const money = (value: number | null | undefined) =>
  `$${Number(value ?? 0).toLocaleString('es-CO', { maximumFractionDigits: 2 })}`;

// Estados de CertificateRequestStatus (backend) en palabras del tenant, con su color.
const REQUEST_STATUS: Record<string, { label: string; className: string }> = {
  Draft: { label: 'Enviando', className: 'bg-slate-100 text-slate-600' },
  Submitted: { label: 'Enviada a Viafirma', className: 'bg-blue-100 text-blue-700' },
  WaitingForIdentity: { label: 'Esperando validación de identidad', className: 'bg-amber-100 text-amber-700' },
  IdentityReview: { label: 'Identidad en revisión', className: 'bg-blue-100 text-blue-700' },
  DocumentsRequired: { label: 'Faltan documentos', className: 'bg-amber-100 text-amber-700' },
  DocumentsSubmitted: { label: 'Documentos enviados', className: 'bg-blue-100 text-blue-700' },
  ProviderReview: { label: 'En revisión de Viafirma', className: 'bg-blue-100 text-blue-700' },
  CertificateProcessing: { label: 'Emitiendo certificado', className: 'bg-blue-100 text-blue-700' },
  ReadyToDownload: { label: 'Emitido, preparando archivo', className: 'bg-blue-100 text-blue-700' },
  Downloaded: { label: 'Descargado', className: 'bg-blue-100 text-blue-700' },
  Installing: { label: 'Instalando', className: 'bg-blue-100 text-blue-700' },
  InstalledPendingActivation: { label: 'Instalado', className: 'bg-emerald-100 text-emerald-700' },
  Active: { label: 'Emitido', className: 'bg-emerald-100 text-emerald-700' },
  Rejected: { label: 'Rechazada', className: 'bg-rose-100 text-rose-700' },
  Failed: { label: 'Fallida', className: 'bg-rose-100 text-rose-700' },
  RevocationRequested: { label: 'Revocación solicitada', className: 'bg-amber-100 text-amber-700' },
  Revoked: { label: 'Revocado', className: 'bg-slate-100 text-slate-600' },
  Expired: { label: 'Vencido', className: 'bg-slate-100 text-slate-600' },
};

export function requestStatus(status: string) {
  return REQUEST_STATUS[status] ?? { label: status, className: 'bg-slate-100 text-slate-600' };
}

// Estados del cargo (CertificateChargeStatus): reservado al solicitar, cobrado al emitirse, devuelto si no se emitió.
export const CHARGE_STATUS: Record<string, string> = { Pending: 'Reservado', Accrued: 'Cobrado', Voided: 'Devuelto', Invoiced: 'Cobrado', Refunded: 'Devuelto' };

export const MOVEMENT_LABELS: Record<string, string> = { Deposit: 'Abono', Adjustment: 'Ajuste', Reserve: 'Reserva', Release: 'Devolución' };
