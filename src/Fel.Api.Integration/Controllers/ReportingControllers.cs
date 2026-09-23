using DevExpress.AspNetCore.Reporting.ReportDesigner;
using DevExpress.AspNetCore.Reporting.ReportDesigner.Native.Services;
using DevExpress.AspNetCore.Reporting.WebDocumentViewer;
using DevExpress.AspNetCore.Reporting.WebDocumentViewer.Native.Services;
using Microsoft.AspNetCore.Mvc;

namespace Fel.Api.Integration.Controllers
{
    // IgnoreApi: su acción Invoke (heredada de DevExpress) no tiene un verbo HTTP explícito,
    // lo que hace que Swashbuckle falle al generar swagger.json para TODO el API (no solo para
    // este controlador) con "Ambiguous HTTP method for action".
    [ApiController]
    [Route("DXXRD")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class CustomReportDesignerController : ReportDesignerController
    {
        public CustomReportDesignerController(IReportDesignerMvcControllerService controllerService) : base(controllerService)
        {
        }
    }

    [ApiController]
    [Route("DXXRDV")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class CustomWebDocumentViewerController : WebDocumentViewerController
    {
        public CustomWebDocumentViewerController(IWebDocumentViewerMvcControllerService controllerService) : base(controllerService)
        {
        }
    }
}
