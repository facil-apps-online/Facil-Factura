using System.Threading.Tasks;
using Fel.Infrastructure.Dian;
using Microsoft.AspNetCore.Mvc;

namespace Fel.Api.Client.Controllers
{
    public class DianLookupRequest
    {
        public string? IdentificationType { get; set; }
        public string? IdentificationNumber { get; set; }
    }

    // Busca en la DIAN el nombre y el correo de un tercero por su documento, para llenar el formulario de Tercero. La consulta la firma la
    // plataforma con el certificado del emisor de la sesión. Va bajo api/client/customers, que el middleware HMAC ya deja pasar a la sesión del portal.
    [ApiController]
    [Route("api/client/customers/dian-lookup")]
    public class CustomerDianLookupController : ClientPortalControllerBase
    {
        private readonly AcquirerLookupService _lookup;

        public CustomerDianLookupController(AcquirerLookupService lookup)
        {
            _lookup = lookup;
        }

        [HttpPost]
        public async Task<IActionResult> Lookup([FromBody] DianLookupRequest request)
        {
            var outcome = await _lookup.LookupAsync(GetCurrentClientId(), request.IdentificationType, request.IdentificationNumber, simulate: false);

            return outcome.Status switch
            {
                AcquirerLookupStatus.Ok => Ok(new
                {
                    found = outcome.Result!.Found,
                    name = outcome.Result.Name,
                    emails = outcome.Result.Emails,
                    message = outcome.Result.Message
                }),
                AcquirerLookupStatus.InvalidType or AcquirerLookupStatus.InvalidNumber => BadRequest(new { message = outcome.Message }),
                AcquirerLookupStatus.NotNative or AcquirerLookupStatus.NoCertificate or AcquirerLookupStatus.ClientNotFound => UnprocessableEntity(new { message = outcome.Message }),
                AcquirerLookupStatus.TooManyQueries => StatusCode(StatusCodes.Status429TooManyRequests, new { message = outcome.Message }),
                _ => StatusCode(StatusCodes.Status502BadGateway, new { message = outcome.Message })
            };
        }
    }
}
