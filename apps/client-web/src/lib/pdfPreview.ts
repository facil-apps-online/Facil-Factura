import { useState } from 'react';
import { toast } from 'sonner';
import { api, getErrorMessage } from './api';

// Vista previa de impresión: pide el PDF a `${basePath}/{id}/preview` y lo abre en una pestaña nueva. Lo comparten facturas,
// documentos soporte y nómina. La API responde el PDF o, si falla, un texto/JSON con el motivo (llega como Blob por el responseType).
export function usePdfPreview(basePath: string) {
  const [previewingId, setPreviewingId] = useState<string | null>(null);

  const preview = async (id: string) => {
    setPreviewingId(id);
    try {
      const res = await api.get(`${basePath}/${id}/preview`, { responseType: 'blob' });
      const url = URL.createObjectURL(new Blob([res.data], { type: 'application/pdf' }));
      window.open(url, '_blank');
    } catch (err: any) {
      if (err.response?.data instanceof Blob) {
        const text = await err.response.data.text();
        try {
          toast.error(getErrorMessage({ response: { data: JSON.parse(text) } }, 'Error al generar la vista previa'));
        } catch {
          toast.error(text || 'Error al generar la vista previa');
        }
      } else {
        toast.error(getErrorMessage(err, 'Error al generar la vista previa'));
      }
    } finally {
      setPreviewingId(null);
    }
  };

  return { previewingId, preview };
}
