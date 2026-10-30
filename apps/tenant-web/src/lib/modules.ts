import React from 'react';

// Módulos de la cuenta, los define el superadmin (los entrega /tenant/auth/block-status). Se combinan: con Facturación es el portal de
// siempre; solo con Certificados los clientes se ven como titulares y desaparece todo lo de facturación. El backend rechaza igual lo que
// no corresponde; esto solo arma el menú, las rutas y las pantallas.
export interface TenantModules {
  invoicing: boolean;
  certificates: boolean;
}

// Si no se pudieron consultar, los de siempre (el backend igual rechaza lo que no corresponde). El portal no pinta nada hasta saberlos,
// para que un tenant de certificados no alcance a ver ni a consultar pantallas de facturación.
export const DEFAULT_MODULES: TenantModules = { invoicing: true, certificates: true };

export const ModulesContext = React.createContext<TenantModules>(DEFAULT_MODULES);

export function useModules(): TenantModules {
  return React.useContext(ModulesContext);
}
