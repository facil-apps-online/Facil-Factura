import React from 'react';
import SearchableSelect from '@shared/components/SearchableSelect';

import { ALL_BRANCHES, useSession } from '../context/SessionContext';

// Sucursal con la que se trabaja. Con una sola sucursal no se muestra; "Todas" solo existe para quien puede ver todas.
export default function BranchSelector() {
  const { branches, allBranches, hasMultipleBranches, selectedBranchId, selectBranch } = useSession();
  if (!hasMultipleBranches) return null;

  const options = [
    ...branches.map(b => ({ value: b.id, label: b.name })),
    ...(allBranches ? [{ value: ALL_BRANCHES, label: 'Todas las sucursales' }] : []),
  ];

  return (
    <div className="w-36 shrink-0 sm:w-56" data-testid="branch-selector">
      <SearchableSelect
        options={options}
        value={selectedBranchId}
        onChange={selectBranch}
        placeholder="Sucursal"
        inputClassName="w-full px-3 py-2 bg-white border border-slate-200 rounded-xl text-sm font-medium text-slate-700 outline-none focus:ring-2 focus:ring-primary"
      />
    </div>
  );
}
