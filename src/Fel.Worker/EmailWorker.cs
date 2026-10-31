using System.Threading.Channels;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Email;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fel.Worker
{
    // Envía los correos de la bandeja de salida (EmailOutbox) en cuanto quien los origina avisa por Redis. El tiempo solo cumple de red de
    // seguridad: un barrido al arrancar y cada minuto recoge lo que se quedó sin aviso (Redis caído, reinicio) y los reintentos con espera.
    public class EmailWorker : BackgroundService
    {
        private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(1);

        private readonly ILogger<EmailWorker> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly Channel<Guid> _pending = Channel.CreateUnbounded<Guid>();

        public EmailWorker(ILogger<EmailWorker> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Iniciando EmailWorker (bandeja de salida de correo) - {Time}", DateTimeOffset.Now);

            _ = SubscribeWithRetryAsync(stoppingToken);
            _ = SweepLoopAsync(stoppingToken);

            try
            {
                await foreach (var id in _pending.Reader.ReadAllAsync(stoppingToken))
                {
                    try
                    {
                        using var scope = _serviceProvider.CreateScope();
                        await scope.ServiceProvider.GetRequiredService<EmailDispatcher>().ProcessAsync(id, stoppingToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex, "Error procesando el correo {EmailId}.", id);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Apagado graceful del servicio
            }
        }

        // Redis puede no estar disponible al arrancar: se reintenta la suscripción y, mientras tanto, el barrido cubre el envío.
        private async Task SubscribeWithRetryAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    _serviceProvider.GetRequiredService<IEmailSignal>().Subscribe(id =>
                    {
                        _pending.Writer.TryWrite(id);
                        return Task.CompletedTask;
                    });
                    _logger.LogInformation("EmailWorker suscrito al canal de la bandeja de salida.");
                    return;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "No se pudo suscribir EmailWorker a Redis; se reintenta en 15 s.");
                    try { await Task.Delay(TimeSpan.FromSeconds(15), ct); } catch (OperationCanceledException) { return; }
                }
            }
        }

        private async Task SweepLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    foreach (var id in await scope.ServiceProvider.GetRequiredService<EmailDispatcher>().FindDueAsync(ct))
                        _pending.Writer.TryWrite(id);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error en el barrido de la bandeja de salida de correo.");
                }

                try { await Task.Delay(SweepInterval, ct); } catch (OperationCanceledException) { return; }
            }
        }
    }
}
