import React, { useState, useEffect, useRef } from 'react';
import { Plus, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { api, getErrorMessage } from '../lib/api';
import { useConfirm } from '@/components/ConfirmDialog';
import BulkImportModal from '../components/BulkImportModal';
import Modal from '../components/Modal';
import DataTable, { type DataTableColumn, type DataTableQuery } from '@shared/components/DataTable';
import RowIconButton from '../components/RowIconButton';
import SearchableSelect from '@shared/components/SearchableSelect';
import { useNumberFormat } from '../lib/numberFormat';
import ProductFormModal, { PRODUCT_SCOPES, type ProductScope } from '../components/ProductFormModal';

const IVA_TREATMENTS = [
  { value: 'Gravado', label: 'Gravado' },
  { value: 'Exento', label: 'Exento (tarifa 0%)' },
  { value: 'Excluido', label: 'Excluido (no aplica IVA)' }
];

interface ProductTax {
  taxCategory: string;
  rate: number;
}

export default function ProductsPage() {
  const fmt = useNumberFormat();
  const confirm = useConfirm();
  // La lista se pide por páginas al servidor (hay clientes con miles de productos): solo se tienen los de la página que se ve.
  const [pageData, setPageData] = useState<{ items: any[]; total: number; pendingCount: number }>({ items: [], total: 0, pendingCount: 0 });
  const [loading, setLoading] = useState(true);
  const lastQuery = useRef<DataTableQuery | null>(null);
  const requestId = useRef(0);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingProduct, setEditingProduct] = useState<any>(null);
  const [ivaRateCatalog, setIvaRateCatalog] = useState<{ id: string, category: string, name: string }[]>([]);
  const [scope, setScope] = useState<ProductScope>('Invoice');
  const [importOpen, setImportOpen] = useState(false);
  // Productos que llegaron de una carga masiva sin impuestos definidos: se pueden filtrar y completar en bloque.
  const [onlyPending, setOnlyPending] = useState(false);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [bulkTaxOpen, setBulkTaxOpen] = useState(false);
  const [bulkTax, setBulkTax] = useState({ ivaTreatment: 'Gravado', ivaRate: 19 });
  const [bulkSaving, setBulkSaving] = useState(false);

  useEffect(() => {
    api.get('/client/tax-catalog?kind=IvaRate')
      .then(res => setIvaRateCatalog(res.data))
      .catch(() => {});
  }, []);

  // La tabla avisa qué página pide el usuario (búsqueda, orden, página, tamaño, pestaña o filtro de pendientes); las respuestas que llegan tarde,
  // de una petición ya reemplazada, se descartan.
  const fetchPage = (query: DataTableQuery) => {
    lastQuery.current = query;
    const id = ++requestId.current;
    setLoading(true);
    api.get('/client/products', {
      params: { scope, q: query.search || undefined, onlyPending: onlyPending || undefined, sort: query.sort?.key, dir: query.sort?.dir, page: query.page, pageSize: query.pageSize }
    })
      .then(res => { if (id === requestId.current) setPageData(res.data); })
      .catch(() => { if (id === requestId.current) toast.error('Error al cargar productos'); })
      .finally(() => { if (id === requestId.current) setLoading(false); });
  };

  // Vuelve a pedir la página que se está viendo (después de guardar, eliminar o importar).
  const loadProducts = () => { if (lastQuery.current) fetchPage(lastQuery.current); };

  const handleOpenModal = (product?: any) => {
    setEditingProduct(product ?? null);
    setIsModalOpen(true);
  };

  const applyBulkTax = async () => {
    setBulkSaving(true);
    try {
      const res = await api.post('/client/products/bulk-taxes', { ids: Array.from(selectedIds), ivaTreatment: bulkTax.ivaTreatment, ivaRate: bulkTax.ivaTreatment === 'Gravado' ? bulkTax.ivaRate : 0 });
      toast.success(`Se actualizaron ${res.data.updated} productos`);
      setBulkTaxOpen(false);
      setSelectedIds(new Set());
      loadProducts();
    } catch (err: any) {
      toast.error(getErrorMessage(err, 'No se pudieron asignar los impuestos'));
    } finally {
      setBulkSaving(false);
    }
  };

  const handleDelete = async (id: string) => {
    if (!(await confirm('¿Estás seguro de eliminar este producto?'))) return;
    try {
      await api.delete(`/client/products/${id}`);
      toast.success('Producto eliminado');
      loadProducts();
    } catch (err: any) {
      toast.error('Error al eliminar');
    }
  };

  const pendingCount = pageData.pendingCount;

  const columns: DataTableColumn<any>[] = [
    {
      key: 'code',
      header: 'Código (SKU)',
      cellClassName: 'font-mono text-sm font-medium text-slate-500',
      render: p => p.code,
      sortValue: p => p.code
    },
    {
      key: 'name',
      header: 'Nombre o descripción',
      primary: true,
      cellClassName: 'font-bold text-slate-900',
      render: p => p.name,
      sortValue: p => p.name
    },
    {
      key: 'price',
      header: 'Precio',
      align: 'right',
      cellClassName: 'font-medium',
      render: p => <>${fmt.number(p.unitPrice, 3)}{p.priceIncludesIva && p.ivaTreatment === 'Gravado' && <span className="ml-1 text-xs text-slate-400">IVA incl.</span>}</>,
      sortValue: p => p.unitPrice
    },
    {
      key: 'iva',
      header: 'IVA',
      align: 'right',
      cellClassName: 'text-sm text-slate-500',
      render: p => p.taxesPending
        ? <span className="rounded-full bg-amber-100 px-2 py-0.5 text-xs font-bold text-amber-700" title="Llegó de una carga masiva sin impuestos definidos: complétalos">Impuestos por completar</span>
        : (p.ivaTreatment === 'Gravado' ? `${p.ivaRate}%` : IVA_TREATMENTS.find(t => t.value === p.ivaTreatment)?.label || p.ivaTreatment),
      sortValue: p => (p.taxesPending ? -2 : p.ivaTreatment === 'Gravado' ? p.ivaRate : -1)
    },
    {
      key: 'otherTaxes',
      header: 'Otros Impuestos',
      align: 'right',
      cellClassName: 'text-sm text-slate-500',
      render: p => (p.taxes?.length ? p.taxes.map((t: ProductTax) => `${t.taxCategory} ${t.rate}%`).join(', ') : '—')
    }
  ];

  return (
    <div className="p-3 sm:p-4">
      <div className="mb-6 flex flex-col gap-4 sm:mb-8 sm:flex-row sm:items-center sm:justify-between">
        <div className="min-w-0">
          <h1 className="text-2xl font-extrabold text-slate-800 sm:text-3xl">Productos y servicios</h1>
          <p className="mt-1 text-slate-500">Catálogo de productos y servicios</p>
        </div>
        <div className="flex flex-wrap gap-3">
          <button onClick={() => setImportOpen(true)} className="rounded-xl border border-slate-200 bg-white px-5 py-2.5 text-sm font-bold text-slate-700 shadow-sm hover:bg-slate-50">
            Carga masiva
          </button>
          <button
            onClick={() => handleOpenModal()}
            className="bg-primary hover:bg-primary/90 text-white px-5 py-2.5 rounded-xl font-bold flex items-center gap-2 shadow-sm transition-all"
          >
            <Plus size={20} /> Nuevo Producto
          </button>
        </div>
      </div>

      {/* Pestañas: si no caben en el ancho (320 px) la tira se desplaza dentro de sí misma, sin mover la página. */}
      <div className="mb-6 flex overflow-x-auto border-b border-slate-200">
        {PRODUCT_SCOPES.map(option => (
          <button
            key={option.value}
            onClick={() => setScope(option.value)}
            className={`shrink-0 whitespace-nowrap px-4 py-3 font-bold text-sm border-b-2 transition-colors ${scope === option.value ? 'border-primary text-primary' : 'border-transparent text-slate-500 hover:text-slate-700'}`}
          >
            {option.label}
          </button>
        ))}
      </div>

      {pendingCount > 0 && (
        <div className="mb-4 flex flex-wrap items-center gap-3 rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
          <span className="font-semibold">{pendingCount} producto{pendingCount === 1 ? '' : 's'} con impuestos por completar.</span>
          <button onClick={() => setOnlyPending(!onlyPending)} className="font-bold underline">{onlyPending ? 'Ver todos' : 'Ver solo esos'}</button>
          {selectedIds.size > 0 && (
            <button onClick={() => setBulkTaxOpen(true)} className="ml-auto rounded-lg bg-primary px-4 py-1.5 font-bold text-white hover:bg-primary/90">
              Asignar IVA a {selectedIds.size} seleccionado{selectedIds.size === 1 ? '' : 's'}
            </button>
          )}
        </div>
      )}

      <DataTable
        rows={pageData.items}
        server={{ total: pageData.total, onQueryChange: fetchPage }}
        loading={loading}
        resetKey={`${scope}|${onlyPending}`}
        columns={columns}
        rowKey={p => p.id}
        tableFrom="wide"
        selection={scope === 'Invoice' ? {
          isSelected: p => selectedIds.has(p.id),
          onToggle: p => setSelectedIds(prev => { const next = new Set(prev); if (next.has(p.id)) next.delete(p.id); else next.add(p.id); return next; }),
          onToggleMany: (items, select) => setSelectedIds(prev => { const next = new Set(prev); items.forEach(p => select ? next.add(p.id) : next.delete(p.id)); return next; }),
          label: p => `Seleccionar ${p.name}`
        } : undefined}
        search="Buscar por código o nombre..."
        defaultSort={{ key: 'name', dir: 'asc' }}
        emptyMessage="No tienes productos registrados."
        actions={p => (
          <>
            <RowIconButton action="edit" label={`Editar ${p.name}`} onClick={() => handleOpenModal(p)} />
            <RowIconButton action="delete" label={`Eliminar ${p.name}`} onClick={() => handleDelete(p.id)} />
          </>
        )}
      />

      {importOpen && (
        <BulkImportModal
          title="Carga masiva de productos"
          entityLabel="productos"
          endpoint="/client/products"
          query={`scope=${scope}`}
          onDone={loadProducts}
          onClose={() => setImportOpen(false)}
        />
      )}

      <Modal
        open={bulkTaxOpen}
        onOpenChange={setBulkTaxOpen}
        title="Asignar IVA a los productos seleccionados"
        size="sm"
        withFloatingPickers
        footer={
          <div className="flex w-full justify-end gap-2">
            <button onClick={() => setBulkTaxOpen(false)} className="rounded-xl border border-slate-200 px-5 py-2.5 font-bold text-slate-600 hover:bg-slate-50">Cancelar</button>
            <button onClick={applyBulkTax} disabled={bulkSaving} className="flex items-center gap-2 rounded-xl bg-primary px-5 py-2.5 font-bold text-white hover:bg-primary/90 disabled:opacity-50">
              {bulkSaving && <Loader2 size={16} className="animate-spin" />} Asignar a {selectedIds.size}
            </button>
          </div>
        }
      >
        <div className="space-y-4">
          <p className="text-sm text-slate-500">El mismo tratamiento de IVA se aplica a los {selectedIds.size} productos seleccionados y dejan de figurar como "impuestos por completar".</p>
          <div>
            <label className="mb-1 block text-sm font-bold text-slate-700">Tratamiento</label>
            <SearchableSelect
              options={IVA_TREATMENTS}
              value={bulkTax.ivaTreatment}
              onChange={v => setBulkTax({ ...bulkTax, ivaTreatment: v })}
              placeholder="Tratamiento de IVA"
              inputClassName="w-full px-3 py-2 bg-white border border-slate-200 rounded-xl text-sm outline-none focus:ring-2 focus:ring-primary"
            />
          </div>
          {bulkTax.ivaTreatment === 'Gravado' && (
            <div>
              <label className="mb-1 block text-sm font-bold text-slate-700">Tarifa de IVA</label>
              <SearchableSelect
                options={ivaRateCatalog.map(r => ({ value: r.category, label: r.name }))}
                value={String(bulkTax.ivaRate)}
                onChange={v => setBulkTax({ ...bulkTax, ivaRate: Number(v) })}
                placeholder="Tarifa"
                inputClassName="w-full px-3 py-2 bg-white border border-slate-200 rounded-xl text-sm outline-none focus:ring-2 focus:ring-primary"
              />
            </div>
          )}
        </div>
      </Modal>

      <ProductFormModal
        open={isModalOpen}
        product={editingProduct}
        scope={scope}
        onClose={() => setIsModalOpen(false)}
        onSaved={() => { setIsModalOpen(false); loadProducts(); }}
      />
    </div>
  );
}
