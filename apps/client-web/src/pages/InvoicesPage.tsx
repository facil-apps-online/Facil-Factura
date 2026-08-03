import React, { useState, useEffect } from 'react';
import { Plus, Edit2, Trash2, Send, FileText, Loader2, ArrowLeft, PlusCircle, RotateCcw } from 'lucide-react';
import { toast } from 'sonner';
import { api } from '../lib/api';

export default function InvoicesPage() {
  const [invoices, setInvoices] = useState<any[]>([]);
  const [customers, setCustomers] = useState<any[]>([]);
  const [products, setProducts] = useState<any[]>([]);
  const [documentTypes, setDocumentTypes] = useState<any[]>([]);
  
  const [loading, setLoading] = useState(true);
  const [view, setView] = useState<'list' | 'create'>('list');
  
  const initialForm = {
    documentTypeId: '',
    customerId: '',
    notes: '',
    items: [] as any[],
    subtotal: 0,
    taxAmount: 0,
    totalAmount: 0,
    referenceDocumentId: null as string | null,
    referenceConcept: ''
  };
  const [formData, setFormData] = useState(initialForm);

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    setLoading(true);
    try {
      const [invRes, custRes, prodRes] = await Promise.all([
        api.get('/client/invoices'),
        api.get('/client/customers'),
        api.get('/client/products')
      ]);
      setInvoices(invRes.data);
      setCustomers(custRes.data);
      setProducts(prodRes.data);
      
      // En un caso real, los tipos de documento (Resoluciones/Tipos) vendrían del backend.
      // Para demo, lo hardcodeamos temporalmente.
      setDocumentTypes([
        { id: '00000000-0000-0000-0000-000000000001', code: '01', name: 'Factura Electrónica de Venta' },
        { id: '00000000-0000-0000-0000-000000000009', code: '91', name: 'Nota Crédito Electrónica' }
      ]);
      
      if (documentTypes.length > 0) {
        setFormData(f => ({ ...f, documentTypeId: documentTypes[0].id }));
      }
    } catch (err) {
      toast.error('Error cargando datos');
    }
    setLoading(false);
  };

  const handleCreateNew = () => {
    setFormData({
      ...initialForm,
      documentTypeId: documentTypes.find(d => d.code === '01')?.id || ''
    });
    setView('create');
  };

  const handleCreateCreditNote = async (originalInvoice: any) => {
    try {
      // Necesitamos cargar los ítems originales si no vienen en la lista
      const res = await api.get(`/client/invoices/${originalInvoice.id}`);
      const fullInvoice = res.data;

      setFormData({
        ...initialForm,
        documentTypeId: documentTypes.find(d => d.code === '91')?.id || '',
        customerId: fullInvoice.customerId,
        referenceDocumentId: fullInvoice.id,
        referenceConcept: 'Devolución de mercancía', // Valor por defecto
        items: fullInvoice.items.map((i: any) => ({
          productId: i.productId || '',
          code: i.code,
          name: i.name,
          quantity: i.quantity,
          unitPrice: i.unitPrice,
          taxRate: i.taxRate,
          taxAmount: i.taxAmount,
          totalAmount: i.totalAmount
        })),
        subtotal: fullInvoice.subtotal,
        taxAmount: fullInvoice.taxAmount,
        totalAmount: fullInvoice.totalAmount
      });
      setView('create');
    } catch (err) {
      toast.error('Error al cargar la factura original');
    }
  };

  const addItem = () => {
    setFormData({
      ...formData,
      items: [...formData.items, { productId: '', code: '', name: '', quantity: 1, unitPrice: 0, taxRate: 19, taxAmount: 0, totalAmount: 0 }]
    });
  };

  const removeItem = (index: number) => {
    const newItems = [...formData.items];
    newItems.splice(index, 1);
    recalculateTotals(newItems);
  };

  const updateItem = (index: number, field: string, value: any) => {
    const newItems = [...formData.items];
    const item = { ...newItems[index], [field]: value };
    
    // Si seleccionan un producto del catálogo, autocompletar
    if (field === 'productId' && value !== '') {
      const p = products.find(prod => prod.id === value);
      if (p) {
        item.code = p.code;
        item.name = p.name;
        item.unitPrice = p.unitPrice;
        item.taxRate = p.taxRate;
      }
    }

    // Recalcular montos de línea
    item.taxAmount = (item.quantity * item.unitPrice) * (item.taxRate / 100);
    item.totalAmount = (item.quantity * item.unitPrice) + item.taxAmount;
    
    newItems[index] = item;
    recalculateTotals(newItems);
  };

  const recalculateTotals = (newItems: any[]) => {
    let sub = 0;
    let tax = 0;
    newItems.forEach(i => {
      sub += (i.quantity * i.unitPrice);
      tax += i.taxAmount;
    });
    setFormData({
      ...formData,
      items: newItems,
      subtotal: sub,
      taxAmount: tax,
      totalAmount: sub + tax
    });
  };

  const handleSaveDraft = async () => {
    if (!formData.customerId) {
      toast.error('Debes seleccionar un cliente');
      return;
    }
    if (formData.items.length === 0) {
      toast.error('Agrega al menos un ítem');
      return;
    }
    
    try {
      await api.post('/client/invoices/draft', formData);
      toast.success('Borrador guardado');
      setView('list');
      loadData();
    } catch (err) {
      toast.error('Error guardando factura');
    }
  };

  const handlePublish = async (id: string) => {
    if (!confirm('¿Emitir esta factura a la DIAN? Esta acción no se puede deshacer.')) return;
    try {
      await api.post(`/client/invoices/${id}/publish`);
      toast.success('Factura enviada a procesamiento');
      loadData();
    } catch (err: any) {
      toast.error(err.response?.data || 'Error al publicar');
    }
  };

  if (loading) return <div className="flex justify-center p-12"><Loader2 className="animate-spin w-8 h-8 text-primary" /></div>;

  if (view === 'create') {
    return (
      <div className="p-8 max-w-5xl mx-auto animate-in fade-in slide-in-from-bottom-4 duration-300">
        <button onClick={() => setView('list')} className="flex items-center gap-2 text-slate-500 hover:text-slate-800 mb-6 font-medium transition-colors">
          <ArrowLeft size={20} /> Volver a mis facturas
        </button>
        
        <div className="bg-white rounded-3xl shadow-xl overflow-hidden border border-slate-100">
          <div className="p-8 border-b border-slate-100 bg-slate-50/50">
            <h2 className="text-2xl font-extrabold text-slate-800">Nueva Factura</h2>
            <p className="text-slate-500 mt-1">Ingresa los datos para emitir un nuevo documento</p>
          </div>
          
          <div className="p-8">
            <div className="grid grid-cols-2 gap-6 mb-8">
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-2">Cliente / Adquirente</label>
                <select 
                  value={formData.customerId}
                  onChange={e => setFormData({...formData, customerId: e.target.value})}
                  className="w-full p-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-primary outline-none"
                >
                  <option value="">-- Seleccionar Cliente --</option>
                  {customers.map(c => (
                    <option key={c.id} value={c.id}>{c.name} ({c.identificationNumber})</option>
                  ))}
                </select>
              </div>
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-2">Tipo de Documento</label>
                <select 
                  value={formData.documentTypeId}
                  onChange={e => setFormData({...formData, documentTypeId: e.target.value})}
                  className="w-full p-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-primary outline-none"
                >
                  {documentTypes.map(d => (
                    <option key={d.id} value={d.id}>{d.name}</option>
                  ))}
                </select>
              </div>
            </div>

            {formData.referenceDocumentId && (
              <div className="mb-8 p-6 bg-amber-50 border border-amber-200 rounded-xl">
                <h3 className="text-amber-800 font-bold mb-2 flex items-center gap-2">
                  <RotateCcw size={18} />
                  Generando Nota Crédito
                </h3>
                <p className="text-sm text-amber-700 mb-4">
                  Esta nota afectará a la factura seleccionada. Puedes emitirla por el valor total (sin tocar nada) o ajustar las líneas/cantidades para una devolución parcial.
                </p>
                <div>
                  <label className="block text-sm font-bold text-amber-800 mb-2">Concepto de la Nota</label>
                  <select 
                    value={formData.referenceConcept}
                    onChange={e => setFormData({...formData, referenceConcept: e.target.value})}
                    className="w-full p-3 bg-white border border-amber-200 rounded-xl focus:ring-2 focus:ring-amber-500 outline-none"
                  >
                    <option value="Devolución de mercancía">Devolución de mercancía</option>
                    <option value="Anulación de factura">Anulación de factura</option>
                    <option value="Rebaja o descuento parcial o total">Rebaja o descuento parcial o total</option>
                    <option value="Otros">Otros</option>
                  </select>
                </div>
              </div>
            )}

            <div className="mb-8">
              <div className="flex justify-between items-end mb-4">
                <h3 className="text-lg font-bold text-slate-800">Líneas de Factura</h3>
                <button onClick={addItem} className="text-primary font-bold flex items-center gap-2 hover:text-blue-700 transition-colors">
                  <PlusCircle size={18} /> Agregar Ítem
                </button>
              </div>
              
              <div className="border border-slate-200 rounded-2xl overflow-hidden">
                <table className="w-full text-left border-collapse">
                  <thead>
                    <tr className="bg-slate-50 text-xs uppercase tracking-wider text-slate-500 font-bold border-b border-slate-200">
                      <th className="p-4 w-1/3">Producto</th>
                      <th className="p-4">Cant.</th>
                      <th className="p-4">Precio Und.</th>
                      <th className="p-4">% IVA</th>
                      <th className="p-4 text-right">Total</th>
                      <th className="p-4"></th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {formData.items.map((item, index) => (
                      <tr key={index} className="bg-white">
                        <td className="p-2">
                          <select 
                            value={item.productId}
                            onChange={e => updateItem(index, 'productId', e.target.value)}
                            className="w-full p-2 border border-slate-200 rounded-lg text-sm outline-none"
                          >
                            <option value="">-- Seleccionar --</option>
                            {products.map(p => (
                              <option key={p.id} value={p.id}>{p.name}</option>
                            ))}
                          </select>
                          {!item.productId && (
                            <input 
                              type="text" placeholder="Nombre manual..." value={item.name} 
                              onChange={e => updateItem(index, 'name', e.target.value)}
                              className="w-full p-2 border border-slate-200 rounded-lg text-sm outline-none mt-2" 
                            />
                          )}
                        </td>
                        <td className="p-2">
                          <input type="number" min="1" value={item.quantity} onChange={e => updateItem(index, 'quantity', parseFloat(e.target.value) || 0)} className="w-20 p-2 border border-slate-200 rounded-lg text-sm outline-none text-center" />
                        </td>
                        <td className="p-2">
                          <input type="number" value={item.unitPrice} onChange={e => updateItem(index, 'unitPrice', parseFloat(e.target.value) || 0)} className="w-32 p-2 border border-slate-200 rounded-lg text-sm outline-none font-mono" />
                        </td>
                        <td className="p-2">
                          <select value={item.taxRate} onChange={e => updateItem(index, 'taxRate', parseFloat(e.target.value))} className="p-2 border border-slate-200 rounded-lg text-sm outline-none">
                            <option value="19">19%</option>
                            <option value="5">5%</option>
                            <option value="0">0%</option>
                          </select>
                        </td>
                        <td className="p-4 text-right font-mono font-bold text-slate-700">
                          ${item.totalAmount.toLocaleString('es-CO')}
                        </td>
                        <td className="p-2 text-right">
                          <button onClick={() => removeItem(index)} className="p-2 text-rose-400 hover:text-rose-600 hover:bg-rose-50 rounded-lg transition-colors">
                            <Trash2 size={18} />
                          </button>
                        </td>
                      </tr>
                    ))}
                    {formData.items.length === 0 && (
                      <tr>
                        <td colSpan={6} className="p-8 text-center text-slate-400">Sin ítems agregados.</td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>
            </div>

            <div className="flex justify-end border-t border-slate-100 pt-8">
              <div className="w-72 bg-slate-50 p-6 rounded-2xl border border-slate-200">
                <div className="flex justify-between text-slate-500 mb-2">
                  <span>Subtotal:</span>
                  <span className="font-mono">${formData.subtotal.toLocaleString('es-CO')}</span>
                </div>
                <div className="flex justify-between text-slate-500 mb-4 pb-4 border-b border-slate-200">
                  <span>Impuestos:</span>
                  <span className="font-mono">${formData.taxAmount.toLocaleString('es-CO')}</span>
                </div>
                <div className="flex justify-between font-extrabold text-xl text-slate-800">
                  <span>Total:</span>
                  <span className="font-mono">${formData.totalAmount.toLocaleString('es-CO')}</span>
                </div>
              </div>
            </div>
            
            <div className="mt-8 flex justify-end gap-4">
              <button onClick={() => setView('list')} className="px-6 py-3 font-bold text-slate-600 hover:bg-slate-100 rounded-xl transition-colors">Cancelar</button>
              <button onClick={handleSaveDraft} className="px-8 py-3 bg-slate-900 hover:bg-black text-white font-bold rounded-xl shadow-lg transition-all flex items-center gap-2">
                <FileText size={18} /> Guardar Borrador
              </button>
            </div>

          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="p-8 max-w-7xl mx-auto">
      <div className="flex justify-between items-center mb-8">
        <div>
          <h1 className="text-3xl font-extrabold text-slate-800">Mis Facturas</h1>
          <p className="text-slate-500 mt-1">Gestión de documentos electrónicos</p>
        </div>
        <button 
          onClick={handleCreateNew}
          className="bg-primary hover:bg-primary/90 text-white px-5 py-2.5 rounded-xl font-bold flex items-center gap-2 shadow-sm transition-all"
        >
          <Plus size={20} /> Nueva Factura
        </button>
      </div>

      <div className="bg-white rounded-2xl shadow-sm border border-slate-200 overflow-hidden">
        <table className="w-full text-left border-collapse">
          <thead>
            <tr className="bg-slate-50 border-b border-slate-200 text-sm font-bold text-slate-500 uppercase tracking-wider">
              <th className="p-4">Fecha</th>
              <th className="p-4">Número</th>
              <th className="p-4">Cliente</th>
              <th className="p-4 text-right">Total</th>
              <th className="p-4 text-center">Estado</th>
              <th className="p-4 text-right">Acciones</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {invoices.map(inv => (
              <tr key={inv.id} className="hover:bg-slate-50/50 transition-colors">
                <td className="p-4 text-slate-500 text-sm">{new Date(inv.createdAt).toLocaleDateString()}</td>
                <td className="p-4 font-bold text-slate-700">{inv.number || '---'}</td>
                <td className="p-4 text-slate-900">{inv.customer?.name || 'Consumidor Final'}</td>
                <td className="p-4 text-right font-mono font-medium">${inv.totalAmount?.toLocaleString('es-CO') || '0'}</td>
                <td className="p-4 text-center">
                  <span className={`px-3 py-1 rounded-full text-xs font-bold ${
                    inv.status === 'DRAFT' ? 'bg-slate-100 text-slate-600' :
                    inv.status === 'PROCESSING' ? 'bg-blue-100 text-blue-600' :
                    inv.status === 'APPROVED' ? 'bg-emerald-100 text-emerald-700' :
                    'bg-rose-100 text-rose-600'
                  }`}>
                    {inv.status}
                  </span>
                </td>
                <td className="p-4 flex items-center justify-end gap-2">
                  {inv.status === 'DRAFT' && (
                    <button onClick={() => handlePublish(inv.id)} title="Emitir a la DIAN" className="p-2 text-white bg-primary hover:bg-primary/90 rounded-lg shadow-sm transition-all flex items-center gap-1 text-sm font-bold">
                      <Send size={16} /> Emitir
                    </button>
                  )}
                  {inv.status === 'APPROVED' && (
                    <button onClick={() => handleCreateCreditNote(inv)} title="Generar Nota Crédito" className="p-2 text-amber-700 bg-amber-100 hover:bg-amber-200 rounded-lg shadow-sm transition-all flex items-center gap-1 text-sm font-bold">
                      <RotateCcw size={16} /> Nota Crédito
                    </button>
                  )}
                </td>
              </tr>
            ))}
            {invoices.length === 0 && (
              <tr>
                <td colSpan={6} className="p-8 text-center text-slate-500">No hay facturas en el sistema.</td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
