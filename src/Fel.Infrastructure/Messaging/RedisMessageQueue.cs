using System.Text.Json;
using System.Threading.Tasks;
using Fel.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace Fel.Infrastructure.Messaging
{
    public class RedisMessageQueue : IMessageQueue
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _db;

        public RedisMessageQueue(IConfiguration configuration)
        {
            string connectionString = configuration.GetConnectionString("RedisConnection") ?? "localhost:6379";
            _redis = ConnectionMultiplexer.Connect(connectionString);
            _db = _redis.GetDatabase();
        }

        public async Task EnqueueAsync<T>(string queueName, T message)
        {
            string json = JsonSerializer.Serialize(message);
            // RPUSH: Agregar al final de la lista
            await _db.ListRightPushAsync(queueName, json);
        }

        public async Task<T?> DequeueAsync<T>(string queueName)
        {
            // BLPOP: Extrae del inicio de la lista y bloquea si está vacía
            // Para .NET StackExchange.Redis, ListLeftPopAsync no bloquea, pero en un BackgroundService se usa un loop con delay.
            var value = await _db.ListLeftPopAsync(queueName);
            
            if (value.HasValue)
            {
                return JsonSerializer.Deserialize<T>(value!);
            }
            return default;
        }
    }
}
