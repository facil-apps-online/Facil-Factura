using System;
using System.Threading;
using System.Threading.Tasks;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fel.Worker
{
    // Corre el set de pruebas de habilitación DIAN de punta a punta.
    //
    // Vive en el Worker y no en la API porque cada documento son ~20 segundos entre transmitirlo y
    // obtener el veredicto de la DIAN (la validación es asíncrona: primero devuelve un ZipKey y el
    // resultado se consulta aparte). Con las rachas que exigimos —10 facturas, 3 notas débito y 3
    // notas crédito aceptadas seguidas— el proceso completo toma varios minutos, muy por encima de
    // lo que aguanta una petición HTTP.
    //
    // El portal no espera: consulta el avance que el orquestador va persistiendo en el cliente
    // (DianHabilitationStatus / Progress / Message).
    public class HabilitationWorker : BackgroundService
    {
        public const string QueueName = "fel:habilitation:queue";

        private readonly ILogger<HabilitationWorker> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IMessageQueue _messageQueue;

        public HabilitationWorker(
            ILogger<HabilitationWorker> logger,
            IServiceProvider serviceProvider,
            IMessageQueue messageQueue)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _messageQueue = messageQueue;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("HabilitationWorker iniciado. Escuchando en la cola: {QueueName}", QueueName);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var mensaje = await _messageQueue.DequeueAsync<HabilitationRequest>(QueueName);
                    if (mensaje == null || mensaje.ClientId == Guid.Empty)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                        continue;
                    }

                    _logger.LogInformation("Iniciando set de pruebas de habilitación para el cliente {ClientId}.", mensaje.ClientId);

                    using var scope = _serviceProvider.CreateScope();
                    var submissionService = scope.ServiceProvider.GetRequiredService<DianTestSetSubmissionService>();

                    var resultado = await submissionService.RunFullTestSetAsync(mensaje.ClientId, stoppingToken);

                    if (resultado.IsSuccess)
                        _logger.LogInformation("Set de pruebas superado para el cliente {ClientId}.", mensaje.ClientId);
                    else
                        _logger.LogWarning("Set de pruebas no superado para el cliente {ClientId}: {Motivo}", mensaje.ClientId, resultado.ErrorMessage);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Un fallo procesando un cliente no debe tumbar el worker: se registra y se sigue
                    // atendiendo la cola.
                    _logger.LogError(ex, "Error ejecutando el set de pruebas de habilitación.");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }
    }

    public class HabilitationRequest
    {
        public Guid ClientId { get; set; }
    }
}
