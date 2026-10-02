import DocumentsPage from './DocumentsPage';

// Documentos Soporte (compras a proveedores): mismo formulario y listado que las facturas, en modo
// "support" — proveedor en vez de cliente, resolución DS y nota de ajuste en lugar de crédito/débito.
export default function SupportDocumentsPage() {
  return <DocumentsPage mode="support" />;
}
