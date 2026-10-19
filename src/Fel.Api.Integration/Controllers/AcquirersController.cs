using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Dian;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Integration.Controllers
{
    public class AcquirerLookupRequest
    {
        public string? IdentificationType { get; set; }
        public string? IdentificationNumber { get; set; }
    }

    /// <summary>
    /// Consulta de adquirentes ante la DIAN (Resolución 202 de 2025): con el tipo y número de identificación devuelve el nombre o
    /// razón social y los correos de recepción de factura que el adquirente registró. La llamada a la DIAN la firma la plataforma con el
    /// certificado del emisor autenticado; el software que consume este API no necesita el certificado.
    /// </summary>
    [ApiController]
    [Route("api/co/dian/acquirers")]
    public class AcquirersController : ControllerBase
    {
        // La DIAN permite este servicio solo al momento de la venta y prohíbe el uso masivo: se limita por emisor.
        private const int MaxQueriesPerMinute = 30;
        private static readonly Regex IdentificationNumberPattern = new("^[A-Za-z0-9]{3,20}$", RegexOptions.Compiled);
        private static readonly ConcurrentDictionary<Guid, ConcurrentQueue<DateTime>> RecentQueries = new();

        private readonly FelDbContext _dbContext;
        private readonly ICryptoVault _cryptoVault;
        private readonly IDianSoapClient _dianSoapClient;
        private readonly ILogger<AcquirersController> _logger;

        public AcquirersController(FelDbContext dbContext, ICryptoVault cryptoVault, IDianSoapClient dianSoapClient, ILogger<AcquirersController> logger)
        {
            _dbContext = dbContext;
            _cryptoVault = cryptoVault;
            _dianSoapClient = dianSoapClient;
            _logger = logger;
        }

        /// <summary>
        /// Busca el nombre o razón social y el correo de un adquirente por su documento.
        /// </summary>
        /// <param name="request">Tipo de documento (código DIAN: 11, 12, 13, 21, 22, 31, 41, 42, 47, 48, 50 o 91) y número de identificación,
        /// sin puntos ni dígito de verificación. Van en el cuerpo y no en la URL para que el documento no quede en las bitácoras del servidor.</param>
        /// <response code="200">Resultado de la consulta. <c>found</c> es falso cuando la DIAN no tiene al adquirente (en ese caso hay que pedirle sus datos).</response>
        /// <response code="400">Tipo de documento no válido, o número con caracteres no permitidos.</response>
        /// <response code="401">No se pudo resolver el emisor autenticado.</response>
        /// <response code="422">El emisor no usa emisión directa a la DIAN (NATIVE) o no tiene certificado activo.</response>
        /// <response code="429">Se superó el límite de consultas por minuto.</response>
        /// <response code="502">La DIAN no respondió o rechazó la consulta.</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> Lookup([FromBody] AcquirerLookupRequest request)
        {
            var identificationType = request.IdentificationType?.Trim() ?? string.Empty;
            var identificationNumber = request.IdentificationNumber?.Trim() ?? string.Empty;

            if (!DianAcquirerResult.IdentificationTypes.Contains(identificationType))
                return BadRequest(new { message = $"Tipo de documento no válido. Use uno de: {string.Join(", ", DianAcquirerResult.IdentificationTypes.OrderBy(t => t))}." });
            if (!IdentificationNumberPattern.IsMatch(identificationNumber))
                return BadRequest(new { message = "El número de identificación debe tener entre 3 y 20 letras o dígitos, sin puntos, guiones ni dígito de verificación." });

            if (HttpContext.Items["ClientId"] is not string clientIdStr || !Guid.TryParse(clientIdStr, out var clientId))
                return Unauthorized(new { message = "No se pudo resolver el emisor autenticado." });

            var client = await _dbContext.Clients.Include(c => c.Integrator).FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null) return Unauthorized(new { message = "Emisor no encontrado." });

            if (client.Integrator.Code != "NATIVE")
                return UnprocessableEntity(new { message = $"Este emisor tiene configurado el proveedor '{client.Integrator.Code}', no emisión directa a la DIAN (NATIVE); la consulta de adquirentes usa el certificado del emisor ante la DIAN." });

            if (TooManyQueries(clientId))
                return StatusCode(StatusCodes.Status429TooManyRequests, new { message = $"Se superó el límite de {MaxQueriesPerMinute} consultas por minuto. La DIAN permite esta consulta solo al momento de la venta." });

            // Las llaves de sandbox nunca llegan a la DIAN: respuesta simulada con el mismo formato.
            if (HttpContext.Items["IsSandbox"] is true)
                return Ok(Result(identificationType, identificationNumber, new DianAcquirerResult(true, "ADQUIRENTE DE PRUEBA", new[] { "adquirente.prueba@example.com" }, null, "Respuesta simulada (sandbox)")));

            var certificate = await _dbContext.Certificates.FirstOrDefaultAsync(c => c.ClientId == clientId && c.IsActive);
            if (certificate == null)
                return UnprocessableEntity(new { message = "Este emisor no tiene un certificado digital activo cargado." });

            DianAcquirerResult result;
            try
            {
                var cert = _cryptoVault.GetCertificate(certificate.FileName, certificate.EncryptedPassword);
                var environment = client.DianHabilitationStatus == "Production" ? "1" : "2";
                var soapResponse = await _dianSoapClient.GetAcquirerAsync(identificationType, identificationNumber, cert, environment);
                result = DianAcquirerResult.FromSoapResponse(soapResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Consulta de adquirente fallida para el emisor {ClientId}, tipo {Type}.", clientId, identificationType);
                return StatusCode(StatusCodes.Status502BadGateway, new { message = "La DIAN no respondió la consulta del adquirente. Intente de nuevo." });
            }

            // Registro de quién consulta, sin guardar el documento completo.
            _logger.LogInformation("Consulta de adquirente: emisor {ClientId}, tipo {Type}, número terminado en {Last}, encontrado {Found}.",
                clientId, identificationType, identificationNumber[^Math.Min(3, identificationNumber.Length)..], result.Found);

            return Ok(Result(identificationType, identificationNumber, result));
        }

        private static object Result(string identificationType, string identificationNumber, DianAcquirerResult result) => new
        {
            identificationType,
            identificationNumber,
            found = result.Found,
            name = result.Name,
            emails = result.Emails,
            message = result.Message
        };

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
