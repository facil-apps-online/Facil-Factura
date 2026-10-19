using System.Linq;
using System.Threading.Tasks;
using Fel.Infrastructure.Dian;
using Microsoft.AspNetCore.Mvc;

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
        private readonly AcquirerLookupService _lookup;

        public AcquirersController(AcquirerLookupService lookup)
        {
            _lookup = lookup;
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
            if (HttpContext.Items["ClientId"] is not string clientIdStr || !System.Guid.TryParse(clientIdStr, out var clientId))
                return Unauthorized(new { message = "No se pudo resolver el emisor autenticado." });

            // Las llaves de sandbox nunca llegan a la DIAN: respuesta simulada con el mismo formato.
            var outcome = await _lookup.LookupAsync(clientId, request.IdentificationType, request.IdentificationNumber, HttpContext.Items["IsSandbox"] is true);

            return outcome.Status switch
            {
                AcquirerLookupStatus.Ok => Ok(Result(outcome)),
                AcquirerLookupStatus.InvalidType or AcquirerLookupStatus.InvalidNumber => BadRequest(new { message = outcome.Message }),
                AcquirerLookupStatus.ClientNotFound => Unauthorized(new { message = outcome.Message }),
                AcquirerLookupStatus.NotNative or AcquirerLookupStatus.NoCertificate => UnprocessableEntity(new { message = outcome.Message }),
                AcquirerLookupStatus.TooManyQueries => StatusCode(StatusCodes.Status429TooManyRequests, new { message = outcome.Message }),
                _ => StatusCode(StatusCodes.Status502BadGateway, new { message = outcome.Message })
            };
        }

        private static object Result(AcquirerLookupOutcome outcome) => new
        {
            identificationType = outcome.IdentificationType,
            identificationNumber = outcome.IdentificationNumber,
            found = outcome.Result!.Found,
            name = outcome.Result.Name,
            emails = outcome.Result.Emails,
            message = outcome.Result.Message
        };
    }
}
