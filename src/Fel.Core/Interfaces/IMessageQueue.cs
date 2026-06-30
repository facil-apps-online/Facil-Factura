using System.Threading.Tasks;

namespace Fel.Core.Interfaces
{
    public interface IMessageQueue
    {
        /// <summary>
        /// Encola un mensaje en la cola especificada.
        /// </summary>
        Task EnqueueAsync<T>(string queueName, T message);
        
        /// <summary>
        /// Desencola un mensaje de la cola especificada (Bloqueante o asíncrono).
        /// </summary>
        Task<T?> DequeueAsync<T>(string queueName);
    }
}
