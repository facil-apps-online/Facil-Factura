import DocumentsPage from './DocumentsPage';

// Mis Facturas: el formulario y el listado son los mismos de Documentos Soporte (DocumentsPage).
export default function InvoicesPage() {
  return <DocumentsPage mode="invoice" />;
}
