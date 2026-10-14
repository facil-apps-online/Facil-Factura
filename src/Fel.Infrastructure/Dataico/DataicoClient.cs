using System;
using System.Net.Http;

namespace Fel.Infrastructure.Dataico
{
    public static class DataicoClient
    {
        private static readonly HttpClient _httpClient;

        static DataicoClient()
        {
            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(15),
                MaxConnectionsPerServer = 100
            };
            _httpClient = new HttpClient(handler)
            {
                // El tiempo máximo se fija por solicitud (DataicoApiService.ExecuteAsync): 100 s por defecto y 180 s en los eventos.
                Timeout = System.Threading.Timeout.InfiniteTimeSpan
            };
        }

        public static HttpClient Client => _httpClient;
    }
}
