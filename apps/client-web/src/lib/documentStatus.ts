// Etiquetas en español de los estados de un documento (facturas, documentos soporte y nómina). Solo cambia lo que se ve: el valor
// (DRAFT/PROCESSING/APPROVED/REJECTED) sigue igual porque el resto del código lo usa en comparaciones.
export const STATUS_LABELS: Record<string, string> = {
  DRAFT: 'Borrador',
  PROCESSING: 'Procesando',
  APPROVED: 'Emitida',
  REJECTED: 'Rechazada'
};

export const statusLabel = (status: string): string => STATUS_LABELS[status] || status;
