using Fel.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Fel.Infrastructure.Messaging
{
    // Aviso Pub/Sub de que hay un correo en la bandeja de salida. La conexión se abre al primer uso, para que un Redis caído no impida
    // arrancar y, sobre todo, para que no rompa la transacción de quien origina el correo: el aviso es best effort y el barrido del
    // worker recoge lo que no llegó.
    public class RedisEmailSignal : IEmailSignal
    {
        public const string Channel = "fel:email:outbox";

        private readonly string _connectionString;
        private readonly ILogger<RedisEmailSignal> _logger;
        private readonly object _lock = new();
        private IConnectionMultiplexer? _redis;

        public RedisEmailSignal(IConfiguration configuration, ILogger<RedisEmailSignal> logger)
        {
            _connectionString = configuration.GetConnectionString("RedisConnection") ?? "localhost:6379";
            _logger = logger;
        }

        private IConnectionMultiplexer Connection()
        {
            lock (_lock) return _redis ??= ConnectionMultiplexer.Connect(_connectionString);
        }

        public async Task PublishAsync(Guid emailId)
        {
            try
            {
                await Connection().GetSubscriber().PublishAsync(RedisChannel.Literal(Channel), emailId.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo avisar por Redis del correo {EmailId}; lo enviará el barrido del worker.", emailId);
            }
        }

        public void Subscribe(Func<Guid, Task> handler)
        {
            Connection().GetSubscriber().Subscribe(RedisChannel.Literal(Channel), (channel, value) =>
            {
                if (Guid.TryParse(value.ToString(), out var id)) _ = handler(id);
            });
        }
    }
}
