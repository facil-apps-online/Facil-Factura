import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Loader2 } from 'lucide-react';
import { BRANCH_STORAGE_KEY } from '../lib/api';

export const SUPPORT_STORAGE_KEY = 'fel_client_support';

export interface SupportInfo { client: string; by: string }

export function readSupport(): SupportInfo | null {
  try {
    const raw = localStorage.getItem(SUPPORT_STORAGE_KEY);
    return raw ? JSON.parse(raw) as SupportInfo : null;
  } catch {
    return null;
  }
}

// Entrada en modo soporte: el portal del tenant abre esta dirección con la sesión en el fragmento (#token=...), que no viaja al servidor.
// Se guarda como una sesión normal del portal, junto con la marca de soporte que activa la barra y quita el menú de usuario.
export default function SupportEntry({ onAuthSuccess }: { onAuthSuccess: () => void }) {
  const navigate = useNavigate();

  useEffect(() => {
    const params = new URLSearchParams(window.location.hash.replace(/^#/, ''));
    const token = params.get('token');
    const clientId = params.get('clientId');
    // La dirección con la sesión no se queda en el historial.
    window.history.replaceState(null, '', window.location.pathname);

    if (!token || !clientId) {
      navigate('/login', { replace: true });
      return;
    }

    localStorage.setItem('fel_client_auth', token);
    localStorage.setItem('fel_client_id', clientId);
    localStorage.setItem('fel_client_tenant', params.get('tenant') ?? '');
    localStorage.removeItem(BRANCH_STORAGE_KEY);
    localStorage.setItem(SUPPORT_STORAGE_KEY, JSON.stringify({ client: params.get('client') ?? '', by: params.get('by') ?? '' } satisfies SupportInfo));
    onAuthSuccess();
    navigate('/', { replace: true });
  }, [navigate, onAuthSuccess]);

  return (
    <div className="flex h-dvh items-center justify-center">
      <Loader2 className="w-10 h-10 animate-spin text-blue-500" />
    </div>
  );
}
