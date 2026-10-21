using System;
using System.Threading;
using System.Threading.Tasks;
using Fel.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fel.Worker
{
    // Revisa periódicamente el consumo de los clientes frente a su plan (PlanAlertService): avisos al llegar al porcentaje de aviso y al límite, y
    // bloqueo o desbloqueo automáticos según la acción elegida. Cada revisión es idempotente: un aviso no se repite en el mismo ciclo.
    public class PlanAlertWorker : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);

        private readonly ILogger<PlanAlertWorker> _logger;
        private readonly IServiceProvider _serviceProvider;

        public PlanAlertWorker(ILogger<PlanAlertWorker> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PlanAlertWorker iniciado: revisa el consumo de los planes cada {Minutes} minutos.", Interval.TotalMinutes);
            try { await Task.Delay(StartupDelay, stoppingToken); } catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<PlanAlertService>();
                    var created = await service.EvaluateAllAsync(stoppingToken);
                    if (created > 0) _logger.LogInformation("Revisión de planes: {Count} avisos nuevos.", created);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error revisando el consumo de los planes.");
                }

                try { await Task.Delay(Interval, stoppingToken); } catch (OperationCanceledException) { break; }
            }
        }
    }
}
