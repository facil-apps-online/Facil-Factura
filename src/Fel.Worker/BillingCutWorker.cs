using System;
using System.Threading;
using System.Threading.Tasks;
using Fel.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fel.Worker
{
    // Dispara automáticamente el corte mensual de facturación (MonthlyBillingCutService) del mes
    // que acaba de terminar, sin depender de que alguien lo llame a mano desde Superadmin.
    //
    // Revisa cada hora en vez de calcular el instante exacto del día 1 00:00 UTC: RunAsync ya es
    // idempotente (no hace nada si el corte de ese mes ya existe), así que no hay riesgo de cobrar
    // dos veces por revisar de más — solo hace falta que, dentro de la primera hora después de que
    // empiece el mes, alguna revisión encuentre que el corte anterior todavía no existe y lo genere.
    public class BillingCutWorker : BackgroundService
    {
        private readonly ILogger<BillingCutWorker> _logger;
        private readonly IServiceProvider _serviceProvider;
        private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

        public BillingCutWorker(ILogger<BillingCutWorker> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("BillingCutWorker iniciado: revisa cada hora si hay que generar el corte del mes anterior (día 1 00:00 UTC).");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.UtcNow;
                    // El mes recién cerrado, el único que puede tener el corte pendiente en un
                    // momento dado — el mes en curso todavía no ha terminado.
                    var previousMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1);

                    using var scope = _serviceProvider.CreateScope();
                    var cutService = scope.ServiceProvider.GetRequiredService<MonthlyBillingCutService>();
                    var result = await cutService.RunAsync(previousMonth.Year, previousMonth.Month);

                    if (!result.AlreadyExisted)
                    {
                        _logger.LogInformation(
                            "Corte mensual {Year}-{Month} generado automáticamente para {Count} tenants.",
                            previousMonth.Year, previousMonth.Month, result.TenantsBilled);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error revisando/generando el corte mensual automático.");
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
