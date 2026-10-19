using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fel.Infrastructure.Dian
{
    public enum AcquirerLookupStatus
    {
        Ok,
        InvalidType,
        InvalidNumber,
        ClientNotFound,
        NotNative,
        TooManyQueries,
        NoCertificate,
        DianError
    }

    public sealed record AcquirerLookupOutcome(AcquirerLookupStatus Status, string IdentificationType, string IdentificationNumber, string? Message = null, DianAcquirerResult? Result = null);

    /// <summary>
    /// Consulta de adquirentes ante la DIAN (Resolución 202 de 2025) con el certificado del emisor. La usan el API de integración (software
    /// de terceros autenticado por HMAC) y el portal de clientes (sesión del usuario): las reglas son las mismas, así que viven acá.
    /// </summary>
    public class AcquirerLookupService
    {
        // La DIAN permite este servicio solo al momento de la venta y prohíbe el uso masivo: se limita por emisor.
        public const int MaxQueriesPerMinute = 30;
        private static readonly Regex IdentificationNumberPattern = new("^[A-Za-z0-9]{3,20}$", RegexOptions.Compiled);
        private static readonly ConcurrentDictionary<Guid, ConcurrentQueue<DateTime>> RecentQueries = new();

        private readonly FelDbContext _dbContext;
        private readonly ICryptoVault _cryptoVault;
        private readonly IDianSoapClient _dianSoapClient;
        private readonly ILogger<AcquirerLookupService> _logger;

        public AcquirerLookupService(FelDbContext dbContext, ICryptoVault cryptoVault, IDianSoapClient dianSoapClient, ILogger<AcquirerLookupService> logger)
        {
            _dbContext = dbContext;
            _cryptoVault = cryptoVault;
            _dianSoapClient = dianSoapClient;
            _logger = logger;
        }

        /// <summary>
        /// El servicio solo aplica a emisores con emisión directa a la DIAN (NATIVE) que tengan certificado activo; un emisor de sandbox de
        /// desarrollador lo tiene siempre (la respuesta es simulada). El portal lo usa para mostrar u ocultar el botón.
        /// </summary>
        public async Task<bool> IsAvailableAsync(Guid clientId)
        {
            var client = await _dbContext.Clients.AsNoTracking().Include(c => c.Integrator).FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null || client.Integrator.Code != "NATIVE") return false;
            return client.IsDeveloperSandbox || await _dbContext.Certificates.AnyAsync(c => c.ClientId == clientId && c.IsActive);
        }

        /// <param name="simulate">Llaves de sandbox del API o emisor de sandbox: respuesta simulada con el mismo formato, sin llegar a la DIAN.</param>
        public async Task<AcquirerLookupOutcome> LookupAsync(Guid clientId, string? identificationType, string? identificationNumber, bool simulate)
        {
            var type = identificationType?.Trim() ?? string.Empty;
            var number = identificationNumber?.Trim() ?? string.Empty;

            if (!DianAcquirerResult.IdentificationTypes.Contains(type))
                return new(AcquirerLookupStatus.InvalidType, type, number, $"Tipo de documento no válido. Use uno de: {string.Join(", ", DianAcquirerResult.IdentificationTypes.OrderBy(t => t))}.");
            if (!IdentificationNumberPattern.IsMatch(number))
                return new(AcquirerLookupStatus.InvalidNumber, type, number, "El número de identificación debe tener entre 3 y 20 letras o dígitos, sin puntos, guiones ni dígito de verificación.");

            var client = await _dbContext.Clients.Include(c => c.Integrator).FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null) return new(AcquirerLookupStatus.ClientNotFound, type, number, "Emisor no encontrado.");

            if (client.Integrator.Code != "NATIVE")
                return new(AcquirerLookupStatus.NotNative, type, number, $"Este emisor tiene configurado el proveedor '{client.Integrator.Code}', no emisión directa a la DIAN (NATIVE); la consulta de adquirentes usa el certificado del emisor ante la DIAN.");

            if (TooManyQueries(clientId))
                return new(AcquirerLookupStatus.TooManyQueries, type, number, $"Se superó el límite de {MaxQueriesPerMinute} consultas por minuto. La DIAN permite esta consulta solo al momento de la venta.");

            if (simulate || client.IsDeveloperSandbox)
                return new(AcquirerLookupStatus.Ok, type, number, Result: new DianAcquirerResult(true, "ADQUIRENTE DE PRUEBA", new[] { "adquirente.prueba@example.com" }, null, "Respuesta simulada (sandbox)"));

            var certificate = await _dbContext.Certificates.FirstOrDefaultAsync(c => c.ClientId == clientId && c.IsActive);
            if (certificate == null)
                return new(AcquirerLookupStatus.NoCertificate, type, number, "Este emisor no tiene un certificado digital activo cargado.");

            DianAcquirerResult result;
            try
            {
                var cert = _cryptoVault.GetCertificate(certificate.FileName, certificate.EncryptedPassword);
                var environment = client.DianHabilitationStatus == "Production" ? "1" : "2";
                var soapResponse = await _dianSoapClient.GetAcquirerAsync(type, number, cert, environment);
                result = DianAcquirerResult.FromSoapResponse(soapResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Consulta de adquirente fallida para el emisor {ClientId}, tipo {Type}.", clientId, type);
                return new(AcquirerLookupStatus.DianError, type, number, "La DIAN no respondió la consulta del adquirente. Intente de nuevo.");
            }

            // Registro de quién consulta, sin guardar el documento completo.
            _logger.LogInformation("Consulta de adquirente: emisor {ClientId}, tipo {Type}, número terminado en {Last}, encontrado {Found}.",
                clientId, type, number[^Math.Min(3, number.Length)..], result.Found);

            return new(AcquirerLookupStatus.Ok, type, number, Result: result);
        }

        private static bool TooManyQueries(Guid clientId)
        {
            var now = DateTime.UtcNow;
            var queue = RecentQueries.GetOrAdd(clientId, _ => new ConcurrentQueue<DateTime>());
            lock (queue)
            {
                while (queue.TryPeek(out var oldest) && now - oldest > TimeSpan.FromMinutes(1)) queue.TryDequeue(out _);
                if (queue.Count >= MaxQueriesPerMinute) return true;
                queue.Enqueue(now);
                return false;
            }
        }
    }
}
