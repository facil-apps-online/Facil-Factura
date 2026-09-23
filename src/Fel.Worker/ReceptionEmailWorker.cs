using System;
using System.Threading;
using System.Threading.Tasks;
using Fel.Infrastructure.Dian;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fel.Worker
{
    // Revisa periódicamente el correo de facturación electrónica de cada Client que lo tenga
    // configurado, para los Eventos de Recepción RADIAN (ver ReceptionEmailPollerService).
    public class ReceptionEmailWorker : BackgroundService
    {
        private readonly ILogger<ReceptionEmailWorker> _logger;
        private readonly IServiceProvider _serviceProvider;
        private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

        public ReceptionEmailWorker(ILogger<ReceptionEmailWorker> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ReceptionEmailWorker iniciado. Revisando correos cada {Interval}.", PollInterval);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var poller = scope.ServiceProvider.GetRequiredService<ReceptionEmailPollerService>();
                    await poller.PollAllClientsAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Cancelación normal al apagar el servicio.
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error inesperado revisando correos de recepción.");
                }

                try
                {
                    await Task.Delay(PollInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                }
            }
        }
    }
}
