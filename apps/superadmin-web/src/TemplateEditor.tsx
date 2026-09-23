import React, { useEffect, useRef } from 'react';
import { useParams, Link } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import { toast } from 'sonner';

import 'devextreme/dist/css/dx.light.css';
import '@devexpress/analytics-core/dist/css/dx-analytics.common.css';
import '@devexpress/analytics-core/dist/css/dx-analytics.light.css';
import 'devexpress-reporting/dist/css/dx-webdocumentviewer.css';
import 'devexpress-reporting/dist/css/dx-reportdesigner.css';

const REPORTS_URL = import.meta.env.VITE_FACIL_REPORTS_URL || 'http://localhost:5000';
const REPORTS_API_KEY = import.meta.env.VITE_FACIL_REPORTS_API_KEY || '';

export const TemplateEditor = () => {
  const { templateKey, typeId } = useParams();
  const designerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    document.title = `Editar plantilla — ${templateKey}`;
  }, [templateKey]);

  // El backend está fijado a DevExpress v22.1.15 (ver devexpress-support-reply-2.txt) y esa
  // versión no trae el componente React `DxReportDesigner` (eso llegó en v24+) — el punto de
  // entrada real de esta serie es `JSReportDesignerBinding` (devexpress-reporting/dx-reportdesigner),
  // una clase imperativa que se ata a un <div> con applyBindings()/dispose(), no un componente JSX,
  // y que además espera jQuery/Knockout como globales de `window` (patrón de script-tag clásico).
  // Todo eso se importa dinámicamente DESPUÉS de fijar los globales — un `import` estático se
  // ejecuta antes que cualquier código normal del módulo sin importar dónde quede escrito en el
  // archivo, así que un `window.jQuery = ...` puesto "antes" en el código fuente en realidad
  // corre demasiado tarde si el paquete de DevExpress también se importó de forma estática.
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
      // El Designer usa jQuery UI (mouse/draggable/resizable/sortable/dialog) para el arrastrar y
      // soltar del toolbox y el redimensionado de paneles — el bundle completo se importa solo
      // por su efecto secundario de colgar esos widgets en el objeto jQuery ya global.
      await import('jquery-ui/dist/jquery-ui.js' as any);

      const [{ ajaxSetup }, { JSReportDesignerBinding }] = await Promise.all([
        import('@devexpress/analytics-core/analytics-utils'),
        import('devexpress-reporting/dx-reportdesigner'),
      ]);
      if (cancelled) return;

      ajaxSetup.ajaxSettings.headers = { 'X-API-Key': REPORTS_API_KEY };

      designer = new JSReportDesignerBinding({
        reportUrl: templateKey,
        // Confirmado con soporte de DevExpress: para ASP.NET Core esta acción SIEMPRE es
        // "/DXXRD/GetDesignerModel" — no basta con la ruta base "/DXXRD" (eso daba
        // {"success":false} sin modelo, porque GetDesignerModel es una acción que hay que escribir
        // del lado del servidor; ver CustomReportDesignerController.GetDesignerModel en FacilReports).
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
    return <div className="p-10 text-slate-400">Falta el identificador de la plantilla.</div>;
  }

  return (
    <div className="h-screen flex flex-col bg-[#0B1120]">
      <div className="p-4 border-b border-slate-800 flex items-center gap-4">
        <Link to={`/document-types/${typeId}/templates`} className="text-indigo-400 hover:text-indigo-300 font-semibold flex items-center gap-1">
          <ArrowLeft className="w-4 h-4" /> Volver
        </Link>
        <h1 className="text-white font-bold">Editando: {templateKey}</h1>
      </div>
      <div className="flex-1" ref={designerRef}></div>
    </div>
  );
};

export default TemplateEditor;
