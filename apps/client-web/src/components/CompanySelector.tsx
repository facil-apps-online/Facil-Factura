import React, { useState } from 'react';
import { toast } from 'sonner';
import SearchableSelect from '@shared/components/SearchableSelect';

import { api, BRANCH_STORAGE_KEY, getErrorMessage } from '../lib/api';
import { useSession } from '../context/SessionContext';

// Empresa (cliente) con la que se trabaja, para quien tiene el mismo correo en varios clientes del tenant (un contador que lleva varios).
// Con una sola empresa no se muestra. Al cambiar se pide al servidor la sesión de esa otra empresa (sin volver a pedir la contraseña) y se recarga el portal.
export default function CompanySelector() {
  const { companies } = useSession();
  const [switching, setSwitching] = useState(false);
  const currentClientId = localStorage.getItem('fel_client_id') || '';
  if (companies.length < 2) return null;

  const change = async (clientId: string) => {
    if (clientId === currentClientId || switching) return;
    setSwitching(true);
    try {
      const res = await api.post('/client/session/switch-company', { clientId });
      localStorage.setItem('fel_client_auth', res.data.token);
      localStorage.setItem('fel_client_id', res.data.clientId);
      // La sucursal elegida era de la otra empresa: se parte de la principal de esta.
      localStorage.removeItem(BRANCH_STORAGE_KEY);
      window.location.assign('/');
    } catch (err) {
      toast.error(getErrorMessage(err, 'No se pudo cambiar de empresa'));
      setSwitching(false);
    }
  };

  return (
    <div className="w-40 shrink-0 sm:w-64" data-testid="company-selector">
      <SearchableSelect
        options={companies.map(c => ({ value: c.clientId, label: c.name }))}
        value={currentClientId}
        onChange={change}
        placeholder="Empresa"
        inputClassName="w-full px-3 py-2 bg-white border border-slate-200 rounded-xl text-sm font-semibold text-slate-800 outline-none focus:ring-2 focus:ring-primary"
      />
    </div>
  );
}
