using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Core.Models;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Dian;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Integration.Controllers
{
    /// <summary>
    /// Recepción de Nómina Electrónica (documento de soporte de pago de nómina ante la DIAN) y su
    /// anulación. A diferencia de factura/notas, la DIAN valida la nómina de forma síncrona — este
    /// API responde con el resultado real de la DIAN en la misma petición, no encola nada.
    /// </summary>
    [ApiController]
    [Route("api/co/dian/payroll")]
    public class PayrollController : ControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly ICryptoVault _cryptoVault;
        private readonly IUblGenerator _ublGenerator;
        private readonly IXmlSigner _xmlSigner;
        private readonly IDianSoapClient _dianSoapClient;

        public PayrollController(
            FelDbContext dbContext, ICryptoVault cryptoVault, IUblGenerator ublGenerator,
            IXmlSigner xmlSigner, IDianSoapClient dianSoapClient)
        {
            _dbContext = dbContext;
            _cryptoVault = cryptoVault;
            _ublGenerator = ublGenerator;
            _xmlSigner = xmlSigner;
            _dianSoapClient = dianSoapClient;
        }

        /// <summary>
        /// Emite un Documento de Nómina Electrónica. El emisor siempre es el Client autenticado.
        /// </summary>
        /// <response code="200">La DIAN validó y aceptó el documento. Incluye el CUNE.</response>
        /// <response code="400">Falta el prefijo o el número de documento.</response>
        /// <response code="401">No se pudo resolver el emisor autenticado.</response>
        /// <response code="422">El emisor no está listo para facturar directo a la DIAN, o la DIAN rechazó el documento.</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> EmitPayroll([FromBody] PayrollRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.DocumentNumber) || string.IsNullOrWhiteSpace(request.Prefix))
                return BadRequest("El prefijo y el número de documento de nómina son obligatorios.");

            if (request.IssueDate == default) request.IssueDate = Fel.Core.Models.ColombiaTime.Now;

            var resolved = await Fel.Api.Integration.Security.EmisorResolver.ResolverAsync(this, _dbContext, _cryptoVault);
            if (resolved.Error != null) return resolved.Error;

            var ublData = PayrollDocumentMapper.BuildPayrollDataFromRequest(request, resolved.Client!);
            var cune = _ublGenerator.CalculateCune(ublData);
            var xml = _ublGenerator.GeneratePayrollXml(ublData, cune);

            if (resolved.EsSandbox) return RespuestaSimulada(cune, xml);

            string signedXml;
            try
            {
                signedXml = _xmlSigner.SignXml(xml, resolved.Certificate!, resolved.CertificateChain);
            }
            catch (Exception ex)
            {
                return UnprocessableEntity(new { message = $"No se pudo firmar el documento: {ex.Message}" });
            }

            try
            {
                var dianResponse = await _dianSoapClient.SendNominaSyncAsync($"NIE{request.Prefix}{request.DocumentNumber}.xml", signedXml, resolved.Certificate!, ublData.Environment);
                return Ok(new { cune, status = "PROCESSED", dianResponse });
            }
            catch (Exception ex)
            {
                return UnprocessableEntity(new { message = $"La DIAN rechazó el documento de nómina: {ex.Message}", cune });
            }
        }

        /// <summary>
        /// Anula (TipoNota=2, "Eliminar") un Documento de Nómina Electrónica ya transmitido por
        /// error. No reenvía el detalle de devengados/deducciones — solo referencia el CUNE original.
        /// </summary>
        /// <response code="200">La DIAN validó y aceptó la anulación. Incluye el nuevo CUNE.</response>
        /// <response code="400">Falta el prefijo, el número de documento, o la referencia al documento a anular.</response>
        /// <response code="401">No se pudo resolver el emisor autenticado.</response>
        /// <response code="422">El emisor no está listo para facturar directo a la DIAN, o la DIAN rechazó la anulación.</response>
        [HttpPost("void")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> VoidPayroll([FromBody] PayrollVoidRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.DocumentNumber) || string.IsNullOrWhiteSpace(request.Prefix))
                return BadRequest("El prefijo y el número de documento son obligatorios.");

            if (string.IsNullOrWhiteSpace(request.PredecessorCune) || string.IsNullOrWhiteSpace(request.PredecessorNumber))
                return BadRequest("Debes indicar PredecessorCune y PredecessorNumber del documento de nómina que se está anulando.");

            if (request.IssueDate == default) request.IssueDate = Fel.Core.Models.ColombiaTime.Now;

            var resolved = await Fel.Api.Integration.Security.EmisorResolver.ResolverAsync(this, _dbContext, _cryptoVault);
            if (resolved.Error != null) return resolved.Error;

            var ublData = PayrollDocumentMapper.BuildPayrollVoidDataFromRequest(request, resolved.Client!);
            var cune = _ublGenerator.CalculateVoidCune(ublData);
            var xml = _ublGenerator.GeneratePayrollVoidXml(ublData, cune);

            if (resolved.EsSandbox) return RespuestaSimulada(cune, xml);

            string signedXml;
            try
            {
                signedXml = _xmlSigner.SignXml(xml, resolved.Certificate!, resolved.CertificateChain);
            }
            catch (Exception ex)
            {
                return UnprocessableEntity(new { message = $"No se pudo firmar el documento: {ex.Message}" });
            }

            try
            {
                var dianResponse = await _dianSoapClient.SendNominaSyncAsync($"NIAE{request.Prefix}{request.DocumentNumber}.xml", signedXml, resolved.Certificate!, ublData.Environment);
                return Ok(new { cune, status = "PROCESSED", dianResponse });
            }
            catch (Exception ex)
            {
                return UnprocessableEntity(new { message = $"La DIAN rechazó la anulación: {ex.Message}", cune });
            }
        }

        // Camino simulado para Clients de prueba de developers: el CUNE se calcula de verdad (es
        // deterministico y no necesita certificado) y se devuelve el XML que se habria transmitido,
        // sin firmar ni enviar nada a la DIAN. Ver Fel.Infrastructure SandboxSimulation.
        private IActionResult RespuestaSimulada(string cune, string xml) => Ok(new
        {
            cune,
            status = "PROCESSED",
            simulated = true,
            message = Fel.Infrastructure.Services.SandboxSimulation.Mensaje(usoClaveTecnicaDeRelleno: false),
            trackId = Fel.Infrastructure.Services.SandboxSimulation.NuevoTrackId(),
            xml
        });

    }
}
