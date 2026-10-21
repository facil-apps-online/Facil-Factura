using System;
using System.Threading;
using System.Threading.Tasks;
using Fel.Core.Models;
using Fel.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fel.Worker
{
    // Genera los borradores de cobro de los planes de los tenants que activaron la facturación automática (PlanInvoicingService). Revisa cada hora:
    // la generación es idempotente (un cobro por cliente y ciclo), así que revisar de más no duplica nada y, si el servicio estuvo caído el día del ciclo,
    // el siguiente ciclo de revisión lo completa. Solo crea borradores: nunca emite.
    public class PlanInvoicingWorker : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
        private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(2);

        private readonly ILogger<PlanInvoicingWorker> _logger;
        private readonly IServiceProvider _serviceProvider;

        public PlanInvoicingWorker(ILogger<PlanInvoicingWorker> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PlanInvoicingWorker iniciado: revisa cada hora si hay borradores de cobro de planes por generar.");
            try { await Task.Delay(StartupDelay, stoppingToken); } catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<PlanInvoicingService>();
                    var created = await service.RunAllAsync(ColombiaTime.Now.Date, stoppingToken);
                    if (created > 0) _logger.LogInformation("Facturación de planes: {Count} borradores generados.", created);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error generando los borradores de cobro de los planes.");
                }

                try { await Task.Delay(Interval, stoppingToken); } catch (OperationCanceledException) { break; }
            }
        }
    }
}
