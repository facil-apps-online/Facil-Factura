import { toast } from 'sonner';
import { api, getErrorMessage } from './api';

const CLIENT_PORTAL_URL = (import.meta.env.VITE_CLIENT_PORTAL_URL || 'https://clients.facil-factura.pro').replace(/\/$/, '');

// Modo soporte: entra al portal de clientes dentro de la empresa indicada, sin que el cliente haya dado ningún acceso. El servidor emite la
// sesión; el portal la recibe en el fragmento de la dirección (no viaja al servidor ni queda en sus registros). La pestaña se abre antes de
// pedir la sesión para que el navegador no la bloquee como ventana emergente.
export async function enterClientPortal(clientId: string, nextPath?: string): Promise<void> {
  const tab = window.open('', '_blank');
  if (tab) tab.opener = null;
  try {
    const res = await api.post(`/tenant/clients/${clientId}/support-session`);
    const { token, clientName, tenantSlug, userName } = res.data;
    const fragment = new URLSearchParams({ token, clientId: res.data.clientId, tenant: tenantSlug ?? '', client: clientName ?? '', by: userName ?? '' });
    if (nextPath) fragment.set('next', nextPath);
    const url = `${CLIENT_PORTAL_URL}/support-entry#${fragment.toString()}`;
    if (tab) tab.location.href = url;
    else window.location.href = url;
  } catch (err) {
    tab?.close();
    toast.error(getErrorMessage(err, 'No se pudo entrar al portal del cliente'));
  }
}
