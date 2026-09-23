using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Fel.Core.Interfaces;
using Fel.Core.Models;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Dian;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Integration.Controllers
{
    /// <summary>
    /// Recepción de Documento Soporte electrónico (adquisiciones a sujetos no obligados a
    /// facturar) y su Nota de Ajuste. Igual que Nómina y Documento Equivalente, la DIAN valida
    /// este documento de forma síncrona (SendBillSync).
    /// </summary>
    [ApiController]
    [Route("api/co/dian/support-documents")]
    public class SupportDocumentsController : ControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly ICryptoVault _cryptoVault;
        private readonly IUblGenerator _ublGenerator;
        private readonly IXmlSigner _xmlSigner;
        private readonly IDianSoapClient _dianSoapClient;

        public SupportDocumentsController(
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
        /// Emite un Documento Soporte por una adquisición a un vendedor no obligado a facturar. El
        /// Client autenticado es siempre el ADQUIRENTE, no el vendedor — los datos del vendedor
        /// (<c>Seller</c>) van en el request porque no viven en nuestra base.
        /// </summary>
        /// <response code="200">La DIAN validó y aceptó el documento. Incluye el CUDS.</response>
        /// <response code="400">Falta el prefijo o el número de documento.</response>
        /// <response code="401">No se pudo resolver el emisor autenticado.</response>
        /// <response code="422">El emisor no está listo para facturar directo a la DIAN, o la DIAN rechazó el documento.</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> ReceiveSupportDocument([FromBody] SupportDocumentRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.DocumentNumber) || string.IsNullOrWhiteSpace(request.Prefix))
                return BadRequest("El número de documento soporte es obligatorio.");

            if (request.IssueDate == default) request.IssueDate = DateTime.UtcNow;

            var resolved = await Fel.Api.Integration.Security.EmisorResolver.ResolverAsync(
                this, _dbContext, _cryptoVault, "DS", request.Prefix, "Documento Soporte");
            if (resolved.Error != null) return resolved.Error;

            var resolution = resolved.Resolution ?? ResolucionDeRelleno(resolved.Client!.Id, "DS", request.Prefix, request.DocumentNumber);
            var municipalities = await _dbContext.DianMunicipalities.AsNoTracking().ToDictionaryAsync(m => m.Code);
            var ublData = DianDocumentMapper.BuildSupportDocumentDataFromRequest(request, resolved.Client!, resolution, municipalities);

            return await SignAndSendAsync(ublData, resolved.Certificate, resolved.CertificateChain, $"DS{request.Prefix}{request.DocumentNumber}.xml", resolved.EsSandbox);
        }

        /// <summary>
        /// Emite una Nota de Ajuste al Documento Soporte, referenciando el CUDS del documento original.
        /// </summary>
        /// <response code="200">La DIAN validó y aceptó la nota. Incluye el nuevo CUDS.</response>
        /// <response code="400">Falta el prefijo o el número de documento.</response>
        /// <response code="401">No se pudo resolver el emisor autenticado.</response>
        /// <response code="422">El emisor no está listo para facturar directo a la DIAN, o la DIAN rechazó el documento.</response>
        [HttpPost("adjustment")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> ReceiveSupportDocumentAdjustment([FromBody] SupportDocumentAdjustmentRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.DocumentNumber) || string.IsNullOrWhiteSpace(request.Prefix))
                return BadRequest("El número de documento es obligatorio.");

            if (request.IssueDate == default) request.IssueDate = DateTime.UtcNow;

            var resolved = await Fel.Api.Integration.Security.EmisorResolver.ResolverAsync(
                this, _dbContext, _cryptoVault, "DS-AJUSTE", request.Prefix, "Documento Soporte");
            if (resolved.Error != null) return resolved.Error;

            var resolution = resolved.Resolution ?? ResolucionDeRelleno(resolved.Client!.Id, "DS-AJUSTE", request.Prefix, request.DocumentNumber);
            var municipalities = await _dbContext.DianMunicipalities.AsNoTracking().ToDictionaryAsync(m => m.Code);
            var ublData = DianDocumentMapper.BuildSupportDocumentAdjustmentDataFromRequest(request, resolved.Client!, resolution, municipalities);

            return await SignAndSendAsync(ublData, resolved.Certificate, resolved.CertificateChain, $"DSAJ{request.Prefix}{request.DocumentNumber}.xml", resolved.EsSandbox);
        }

        // Resolución ficticia para Clients de prueba que no tienen una cargada: solo permite armar
        // el UBL para que el developer vea el XML de su documento. No se persiste.
        private static Fel.Core.Entities.Resolution ResolucionDeRelleno(Guid clientId, string documentType, string? prefix, string documentNumber) =>
            Fel.Infrastructure.Services.SandboxSimulation.ResolucionDePrueba(
                clientId, documentType, prefix, long.TryParse(documentNumber, out var n) ? n : 1);

        private async Task<IActionResult> SignAndSendAsync(UblInvoiceData ublData, X509Certificate2? cert, X509Certificate2Collection? certChain, string fileName, bool esSandbox)
        {
            var cuds = _ublGenerator.CalculateCuds(ublData);
            var xml = _ublGenerator.GenerateInvoiceXml(ublData);

            // Camino simulado: se devuelve el CUDS real (es determinístico, no necesita
            // certificado) y el XML que se habría transmitido, sin firmar ni enviar nada.
            if (esSandbox)
            {
                return Ok(new
                {
                    cuds,
                    status = "PROCESSED",
                    simulated = true,
                    message = Fel.Infrastructure.Services.SandboxSimulation.Mensaje(
                        usoClaveTecnicaDeRelleno: ublData.TechnicalKey == Fel.Infrastructure.Services.SandboxSimulation.ClaveTecnicaDeRelleno),
                    trackId = Fel.Infrastructure.Services.SandboxSimulation.NuevoTrackId(),
                    xml
                });
            }

            string signedXml;
            try
            {
                signedXml = _xmlSigner.SignXml(xml, cert!, certChain);
            }
            catch (Exception ex)
            {
                return UnprocessableEntity(new { message = $"No se pudo firmar el documento: {ex.Message}" });
            }

            try
            {
                var dianResponse = await _dianSoapClient.SendDocumentSyncAsync(fileName, signedXml, cert!, ublData.Environment);
                return Ok(new { cuds, status = "PROCESSED", dianResponse });
            }
            catch (Exception ex)
            {
                return UnprocessableEntity(new { message = $"La DIAN rechazó el documento soporte: {ex.Message}", cuds });
            }
        }

    }
}
