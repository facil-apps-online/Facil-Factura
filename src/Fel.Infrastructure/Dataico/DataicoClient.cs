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
                Timeout = TimeSpan.FromSeconds(100)
            };
        }

        public static HttpClient Client => _httpClient;
    }
}
