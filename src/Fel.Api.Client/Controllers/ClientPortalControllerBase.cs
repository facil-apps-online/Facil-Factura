using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Fel.Api.Security;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fel.Api.Client.Controllers
{
    // Base de los controladores del portal de clientes: ClientPortalFilter resuelve cliente, sucursal y rol antes de cada
    // acción (y corta con 401/403 si la sesión o el rol no corresponden), así que acá solo se leen ya resueltos.
    [ServiceFilter(typeof(ClientPortalFilter))]
    public abstract class ClientPortalControllerBase : ControllerBase
    {
        // Máximo de documentos por lote: cada uno es una llamada al integrador, y el lote se procesa de a uno.
        public const int MaxBatchSize = 50;

        protected BranchContext Branch =>
            HttpContext.Items[BranchContext.ItemKey] as BranchContext
            ?? throw new UnauthorizedAccessException("Sesión inválida o expirada. Vuelve a iniciar sesión.");

        protected Guid GetCurrentClientId() => Branch.ClientId;

        // Sucursal por la que se filtran las consultas. Null = todas las sucursales del usuario.
        protected Guid? CurrentBranchScope => Branch.BranchId;

        // Sucursal concreta para crear o emitir.
        protected Guid GetCurrentBranchId() => Branch.RequireBranchId();

        // Consulta de un documento ya emitido en la cuenta de Dataico del cliente (factura, documento soporte, nómina y sus notas), y descarga de su XML o su
        // representación gráfica. Cada controlador solo elige el tipo de documento; ver DataicoLookupService.
        protected async Task<IActionResult> DataicoLookupAsync(Fel.Infrastructure.Dataico.DataicoLookupKind kind, string? number)
        {
            var client = await LoadLookupClientAsync();
            if (client == null) return NotFound(new { message = "Cliente no encontrado." });
            var result = await HttpContext.RequestServices.GetRequiredService<Fel.Infrastructure.Dataico.DataicoLookupService>().LookupAsync(client, kind, number);
            return result.Ok ? Ok(result.Summary) : StatusCode(result.Status, new { message = result.Error });
        }

        protected async Task<IActionResult> DataicoLookupFileAsync(Fel.Infrastructure.Dataico.DataicoLookupKind kind, string? number, string? type)
        {
            var client = await LoadLookupClientAsync();
            if (client == null) return NotFound(new { message = "Cliente no encontrado." });
            var result = await HttpContext.RequestServices.GetRequiredService<Fel.Infrastructure.Dataico.DataicoLookupService>().DownloadAsync(client, kind, number, type);
            return result.Ok ? File(result.Bytes!, result.ContentType!, result.FileName!) : StatusCode(result.Status, new { message = result.Error });
        }

        protected async Task<IActionResult> DataicoInvoiceNoteDraftAsync(string? number)
        {
            var client = await LoadLookupClientAsync();
            if (client == null) return NotFound(new { message = "Cliente no encontrado." });
            var result = await HttpContext.RequestServices.GetRequiredService<Fel.Infrastructure.Dataico.DataicoLookupService>().GetInvoiceNoteDraftAsync(client, number);
            if (result.Draft == null) return StatusCode(result.Status, new { message = result.Error });

            // El tercero y los productos de la factura se crean en el sistema si el cliente todavía no los tiene.
            var created = await Fel.Infrastructure.Dataico.DataicoExternalInvoiceMaterializer.EnsureAsync(
                HttpContext.RequestServices.GetRequiredService<FelDbContext>(), client.Id, result.Draft);
            return Ok(new
            {
                draft = result.Draft,
                customerId = created.CustomerId,
                customerCreated = created.CustomerCreated,
                productIds = created.ProductIds,
                createdProducts = created.CreatedProducts,
                notes = created.Notes
            });
        }

        private Task<Fel.Core.Entities.Client?> LoadLookupClientAsync() =>
            HttpContext.RequestServices.GetRequiredService<FelDbContext>().Clients.Include(c => c.Integrator).FirstOrDefaultAsync(c => c.Id == Branch.ClientId);

        // Respuesta cuando se intenta emitir por Dataico sin plantilla elegida; el portal reconoce el código para ofrecer ir a elegirla.
        public const string TemplateRequiredCode = "TEMPLATE_REQUIRED";

        protected IActionResult TemplateRequired() => BadRequest(new
        {
            message = "Falta seleccionar la plantilla.",
            detail = "Antes de emitir debe seleccionar la plantilla de esta clase de documento en Diseño de mis Facturas.",
            code = TemplateRequiredCode
        });

        // Valida la lista de un lote y la deja sin repetidos. Devuelve null si es válida.
        protected IActionResult? ValidateBatch(BatchRequest? request, out List<Guid> ids)
        {
            ids = request?.Ids?.Distinct().ToList() ?? new List<Guid>();
            if (ids.Count == 0) return BadRequest("Selecciona al menos un documento.");
            if (ids.Count > MaxBatchSize) return BadRequest($"Puedes procesar hasta {MaxBatchSize} documentos a la vez.");
            return null;
        }

        // Procesa los documentos de a uno, en orden de creación (el consecutivo de cada emisión sale en ese orden), reutilizando la
        // misma acción que el portal usa para un documento suelto, y va respondiendo una línea JSON por documento apenas termina.
        // Esa respuesta continua mantiene viva la conexión (el proxy corta tras 60 s sin bytes) y deja mostrar el avance. Si el
        // navegador se desconecta a la mitad, el lote sigue hasta el final: el estado de cada documento queda guardado.
        protected async Task StreamBatchAsync(FelDbContext db, IEnumerable<Guid> ids, IQueryable<Guid> knownIdsInCreationOrder, Func<Guid, Task<IActionResult>> action)
        {
            var requested = ids.ToHashSet();
            var ordered = (await knownIdsInCreationOrder.ToListAsync()).Where(requested.Contains).ToList();
            // Los que no aparecen (de otro cliente o inexistentes) pasan por la misma acción, que responde "no encontrado".
            ordered.AddRange(requested.Except(ordered));

            Response.StatusCode = StatusCodes.Status200OK;
            Response.ContentType = "application/x-ndjson; charset=utf-8";
            Response.Headers["X-Accel-Buffering"] = "no";
            Response.Headers["Cache-Control"] = "no-store";

            var connected = true;
            async Task WriteAsync(object line)
            {
                if (!connected) return;
                try
                {
                    await Response.WriteAsync(JsonSerializer.Serialize(line, JsonOptions) + "\n");
                    await Response.Body.FlushAsync();
                }
                catch (Exception)
                {
                    connected = false;
                }
            }

            await WriteAsync(new { type = "start", total = ordered.Count });

            int succeeded = 0, failed = 0;
            foreach (var id in ordered)
            {
                BatchItemResult result;
                try
                {
                    result = Describe(id, await action(id));
                }
                catch (Exception ex)
                {
                    result = new BatchItemResult(id, false, "Error inesperado al procesar el documento: " + ex.Message, null);
                }
                // Cada documento arranca con el contexto limpio: no arrastra entidades del anterior.
                db.ChangeTracker.Clear();

                if (result.Ok) succeeded++; else failed++;
                await WriteAsync(new { type = "item", id = result.Id, ok = result.Ok, message = result.Message, detail = result.Detail });
            }

            await WriteAsync(new { type = "done", succeeded, failed });
        }

        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        private sealed record BatchItemResult(Guid Id, bool Ok, string Message, string? Detail);

        // Traduce la respuesta de la acción individual (texto, o { message, detail } cuando el integrador rechaza) a una línea del lote.
        private static BatchItemResult Describe(Guid id, IActionResult result)
        {
            var status = (result as IStatusCodeActionResult)?.StatusCode ?? StatusCodes.Status200OK;
            var ok = status is >= 200 and < 300;
            var value = (result as ObjectResult)?.Value;

            string? message = null, detail = null;
            if (value is string text)
            {
                message = text;
            }
            else if (value != null)
            {
                var json = JsonSerializer.SerializeToElement(value, JsonOptions);
                if (json.ValueKind == JsonValueKind.Object)
                {
                    message = json.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
                    detail = json.TryGetProperty("detail", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null;
                }
            }

            return new BatchItemResult(id, ok, message ?? (ok ? "Listo." : $"No se pudo procesar (código {status})."), detail);
        }
    }

    public class BatchRequest
    {
        public List<Guid>? Ids { get; set; }
    }
}
