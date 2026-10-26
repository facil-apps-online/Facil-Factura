import { toast } from 'sonner';
import { getErrorMessage } from './api';

// Código con el que el servidor rechaza una emisión por Dataico mientras el cliente no haya elegido la plantilla de ese tipo de documento.
const TEMPLATE_REQUIRED = 'TEMPLATE_REQUIRED';

// Muestra el error de una emisión. Si falta la plantilla, el aviso trae un botón para ir a elegirla.
export function toastPublishError(err: any, fallback: string, goTo: (path: string) => void) {
  if (err?.response?.data?.code === TEMPLATE_REQUIRED) {
    toast.error(getErrorMessage(err, fallback), {
      duration: 12000,
      action: { label: 'Ir a Diseño de mis Facturas', onClick: () => goTo('/settings') },
    });
    return;
  }
  toast.error(getErrorMessage(err, fallback));
}
