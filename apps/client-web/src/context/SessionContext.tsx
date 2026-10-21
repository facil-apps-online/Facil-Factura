import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { Loader2 } from 'lucide-react';

import { api, BRANCH_STORAGE_KEY } from '../lib/api';

export const ALL_BRANCHES = 'all';

export interface SessionBranch {
  id: string;
  name: string;
  code: string;
  isMain: boolean;
  // Código DANE del municipio de la sucursal.
  cityCode?: string | null;
}

export interface CatalogItem {
  value: string;
  label: string;
}

interface SessionData {
  role: string;
  isAdministrator: boolean;
  // true = el usuario puede ver todas las sucursales del cliente (y por tanto elegir "Todas").
  allBranches: boolean;
  // Ubicación de la empresa, si está registrada.
  location?: { latitude: number; longitude: number } | null;
  branches: SessionBranch[];
  // Empresas (clientes) a las que puede pasar quien tiene el mismo correo en varios clientes del tenant; vacía si solo tiene una.
  companies: Array<{ clientId: string; name: string }>;
  // Módulos que el Tenant activó para este cliente (apagados por defecto).
  // foreignCurrency: el Tenant lo habilitó a facturar también en moneda extranjera.
  // dianLookup: emisión directa a la DIAN con certificado activo, así que se puede buscar un tercero por su documento en la DIAN.
  // tenantOwner: esta empresa es la propietaria de su tenant (la que factura a los demás): puede sincronizar los clientes del tenant como terceros.
  // dataico: la empresa emite con Dataico, así que puede consultar una factura en Dataico por su número.
  features: { payments: boolean; foreignCurrency: boolean; dianLookup: boolean; tenantOwner: boolean; dataico: boolean };
  roles: CatalogItem[];
  noteKinds: CatalogItem[];
}

interface SessionContextValue extends SessionData {
  // Id de la sucursal elegida o ALL_BRANCHES.
  selectedBranchId: string;
  selectBranch: (id: string) => void;
  hasMultipleBranches: boolean;
}

const SessionContext = createContext<SessionContextValue | null>(null);

// Carga la sesión (rol, sucursales y catálogos) antes de mostrar el portal, para que ninguna pantalla pida datos sin saber
// en qué sucursal está: la elegida queda guardada y el interceptor de la API la envía en cada petición.
export function SessionProvider({ children }: { children: React.ReactNode }) {
  const [data, setData] = useState<SessionData | null>(null);
  const [selected, setSelected] = useState('');
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    api.get<SessionData>('/client/session')
      .then(res => {
        const session = res.data;
        const stored = localStorage.getItem(BRANCH_STORAGE_KEY);
        const canPickAll = session.allBranches && session.branches.length > 1;
        const storedIsValid = !!stored && (session.branches.some(b => b.id === stored) || (stored === ALL_BRANCHES && canPickAll));
        // La principal va primero en la lista: es la sucursal por defecto.
        const next = storedIsValid ? stored! : session.branches[0].id;
        localStorage.setItem(BRANCH_STORAGE_KEY, next);
        setData(session);
        setSelected(next);
      })
      .catch(() => setFailed(true));
  }, []);

  const selectBranch = useCallback((id: string) => {
    localStorage.setItem(BRANCH_STORAGE_KEY, id);
    setSelected(id);
  }, []);

  const value = useMemo<SessionContextValue | null>(
    () => (data ? { ...data, selectedBranchId: selected, selectBranch, hasMultipleBranches: data.branches.length > 1 } : null),
    [data, selected, selectBranch],
  );

  if (failed) {
    return (
      <div className="flex min-h-dvh flex-col items-center justify-center gap-4 bg-slate-50 p-6 text-center">
        <p className="text-slate-600">No se pudo cargar tu sesión.</p>
        <button onClick={() => window.location.reload()} className="rounded-xl bg-primary px-5 py-2.5 font-bold text-white hover:bg-primary/90">
          Reintentar
        </button>
      </div>
    );
  }

  if (!value) {
    return (
      <div className="flex min-h-dvh items-center justify-center bg-slate-50" role="status" aria-label="Cargando">
        <Loader2 className="animate-spin text-primary" size={32} />
      </div>
    );
  }

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}

export function useSession(): SessionContextValue {
  const ctx = useContext(SessionContext);
  if (!ctx) throw new Error('useSession debe usarse dentro de SessionProvider');
  return ctx;
}
