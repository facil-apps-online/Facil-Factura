using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fel.Worker
{
    // Revisa periódicamente los documentos que quedaron en "PROCESSING" tras emitir directo a la
    // DIAN (NativeDianSubmissionProvider) y les pregunta el veredicto real con GetStatusZip — sin
    // esto, esos documentos se quedaban en PROCESSING para siempre, sin importar si la DIAN los
    // había aceptado o rechazado (ver DianStatusCheckService).
    public class DianStatusPollingWorker : BackgroundService
    {
        private readonly ILogger<DianStatusPollingWorker> _logger;
        private readonly IServiceProvider _serviceProvider;

        // Mismo orden de magnitud que DianTestSetSubmissionService.WaitForOutcomeAsync (consulta
        // cada 5s, hasta ~1 minuto) — la DIAN suele resolver el veredicto en unos ~20s, así que un
        // ciclo de minutos dejaba al usuario sin saber si su factura se emitió mucho más tiempo del
        // necesario. La consulta a la BD es barata y solo llama a la DIAN si hay algo pendiente.
        private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(15);

        public DianStatusPollingWorker(ILogger<DianStatusPollingWorker> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("DianStatusPollingWorker iniciado: revisa cada 15s los documentos en PROCESSING de la vía directa a la DIAN.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<FelDbContext>();
                    var statusCheckService = scope.ServiceProvider.GetRequiredService<DianStatusCheckService>();

                    // Solo la vía directa a la DIAN (NATIVE) deja el trackId propio de SendBillAsync —
                    // Dataico resuelve su propio estado por otro camino, no le compete a este worker.
                    var pending = await dbContext.Documents
                        .Include(d => d.Client)
                        .Where(d => d.Status == "PROCESSING" && d.DianTrackId != null && d.Client!.Integrator.Code == "NATIVE")
                        .ToListAsync(stoppingToken);

                    if (pending.Count > 0)
                    {
                        _logger.LogInformation("DianStatusPollingWorker: {Count} documento(s) pendiente(s) de veredicto.", pending.Count);
                    }

                    foreach (var document in pending)
                    {
                        try
                        {
                            await statusCheckService.CheckAndUpdateAsync(document, document.Client!);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error consultando el estado del documento {DocumentId}.", document.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error en la ronda de DianStatusPollingWorker.");
                }

                try
                {
                    await Task.Delay(CheckInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Apagado normal del servicio.
                }
            }
        }
    }
}
