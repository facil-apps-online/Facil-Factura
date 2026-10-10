// Etiquetas en español de los estados de un documento (facturas, documentos soporte y nómina). Solo cambia lo que se ve: el valor
// (DRAFT/PROCESSING/APPROVED/REJECTED) sigue igual porque el resto del código lo usa en comparaciones.
export const STATUS_LABELS: Record<string, string> = {
  DRAFT: 'Borrador',
  PROCESSING: 'Procesando',
  APPROVED: 'Emitida',
  REJECTED: 'Rechazada'
};

export const statusLabel = (status: string): string => STATUS_LABELS[status] || status;

// Opciones del filtro por estado (la misma lista en facturas, documentos soporte y nómina).
export const STATUS_FILTER_OPTIONS = [
  { value: '', label: 'Todos' },
  { value: 'DRAFT', label: STATUS_LABELS.DRAFT },
  { value: 'REJECTED', label: STATUS_LABELS.REJECTED },
  { value: 'PROCESSING', label: STATUS_LABELS.PROCESSING },
  { value: 'APPROVED', label: STATUS_LABELS.APPROVED }
];
