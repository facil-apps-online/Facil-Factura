import React, { useEffect, useRef } from 'react';
import { useSearchParams, Link } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import { toast } from 'sonner';

import 'devextreme/dist/css/dx.light.css';
import '@devexpress/analytics-core/dist/css/dx-analytics.common.css';
import '@devexpress/analytics-core/dist/css/dx-analytics.light.css';
import 'devexpress-reporting/dist/css/dx-webdocumentviewer.css';
import 'devexpress-reporting/dist/css/dx-reportdesigner.css';

const REPORTS_URL = import.meta.env.VITE_FACIL_REPORTS_URL || 'http://localhost:5000';
const REPORTS_API_KEY = import.meta.env.VITE_FACIL_REPORTS_API_KEY || '';

// Réplica del editor de superadmin (ver ese archivo para el detalle de por qué el binding es
// imperativo y no un componente JSX) — la llave REPX real puede traer "/" (ej. "dgs/factura1"),
// así que acá viaja como query string (?key=) en vez de segmento de ruta.
export default function TemplateEditor() {
  const [searchParams] = useSearchParams();
  const templateKey = searchParams.get('key') || '';
  const designerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    document.title = `Editar plantilla — ${templateKey}`;
  }, [templateKey]);

  useEffect(() => {
    if (!templateKey || !designerRef.current) return;
    let designer: any;
    let cancelled = false;

    (async () => {
      const jQueryModule = await import('jquery');
      const koModule = await import('knockout');
      const jQuery = (jQueryModule as any).default ?? jQueryModule;
      const ko = (koModule as any).default ?? koModule;
      (window as any).jQuery = (window as any).$ = jQuery;
      (window as any).ko = ko;
      await import('jquery-ui/dist/jquery-ui.js' as any);

      const [{ ajaxSetup }, { JSReportDesignerBinding }] = await Promise.all([
        import('@devexpress/analytics-core/analytics-utils'),
        import('devexpress-reporting/dx-reportdesigner'),
      ]);
      if (cancelled) return;

      ajaxSetup.ajaxSettings.headers = { 'X-API-Key': REPORTS_API_KEY };

      designer = new JSReportDesignerBinding({
        reportUrl: templateKey,
        requestOptions: {
          host: REPORTS_URL,
          getDesignerModelAction: '/DXXRD/GetDesignerModel',
        },
        developmentMode: true,
        callbacks: {
          designer: {
            reportSaved: () => toast.success('Plantilla guardada correctamente.'),
          },
        },
      } as any);

      if (cancelled) {
        designer.dispose();
        return;
      }
      designer.applyBindings(designerRef.current);
    })();

    return () => {
      cancelled = true;
      designer?.dispose();
    };
  }, [templateKey]);

  if (!templateKey) {
    return <div className="p-10 text-slate-500">Falta el identificador de la plantilla.</div>;
  }

  return (
    <div className="h-screen flex flex-col bg-slate-50">
      <div className="p-4 border-b border-slate-200 flex items-center gap-4 bg-white">
        <Link to="/settings" className="text-primary hover:opacity-80 font-semibold flex items-center gap-1">
          <ArrowLeft className="w-4 h-4" /> Volver
        </Link>
        <h1 className="text-slate-800 font-bold">Editando: {templateKey}</h1>
      </div>
      <div className="flex-1" ref={designerRef}></div>
    </div>
  );
}
