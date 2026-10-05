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
    /// Recepción de Documentos Equivalentes Electrónicos, en todos sus subtipos (Resolución 000165
    /// de 2023). Igual que Nómina, la DIAN valida estos documentos de forma síncrona (SendBillSync)
    /// — cada acción responde con el resultado real en la misma petición.
    /// </summary>
    [ApiController]
    [Route("api/co/dian/equivalent-documents")]
    public class EquivalentDocumentsController : ControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly ICryptoVault _cryptoVault;
        private readonly IUblGenerator _ublGenerator;
        private readonly IXmlSigner _xmlSigner;
        private readonly IDianSoapClient _dianSoapClient;

        public EquivalentDocumentsController(
            FelDbContext dbContext, ICryptoVault cryptoVault, IUblGenerator ublGenerator,
            IXmlSigner xmlSigner, IDianSoapClient dianSoapClient)
        {
            _dbContext = dbContext;
            _cryptoVault = cryptoVault;
            _ublGenerator = ublGenerator;
            _xmlSigner = xmlSigner;
            _dianSoapClient = dianSoapClient;
        }

        /// <response code="200">La DIAN validó y aceptó el documento. Incluye el CUFE.</response>
        /// <response code="400">Falta el prefijo o el número de documento.</response>
        /// <response code="401">No se pudo resolver el emisor autenticado.</response>
        /// <response code="422">El emisor no está listo para facturar directo a la DIAN, o la DIAN rechazó el documento.</response>
        [HttpPost("pos")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        public Task<IActionResult> ReceivePosDocument([FromBody] PosDocumentRequest request) =>
            EmitAsync(request, "DE-POS", "20", "POS", request.OnSite);

        /// <summary>Boleta de ingreso a cine (DEBIC).</summary>
        [HttpPost("cine")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        public Task<IActionResult> ReceiveCinemaDocument([FromBody] CinemaDocumentRequest request) =>
            EmitAsync(request, "DE-CINE", "25", "CINE");

        /// <summary>Boleta de ingreso a espectáculos públicos.</summary>
        [HttpPost("espectaculos")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        public Task<IActionResult> ReceivePublicShowDocument([FromBody] PublicShowDocumentRequest request) =>
            EmitAsync(request, "DE-ESPECTACULOS", "27", "ESP");

        /// <summary>Documento en juegos localizados y no localizados (relación diaria de control de ventas).</summary>
        [HttpPost("juegos-localizados")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        public Task<IActionResult> ReceiveLocalizedGamesDocument([FromBody] LocalizedGamesDocumentRequest request) =>
            EmitAsync(request, "DE-JUEGOSLOC", "30", "JL");

        /// <summary>Tiquete de transporte terrestre de pasajeros.</summary>
        [HttpPost("transporte-terrestre")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        public Task<IActionResult> ReceiveLandPassengerTransportDocument([FromBody] LandPassengerTransportDocumentRequest request) =>
            EmitAsync(request, "DE-PASAJEROS", "35", "TT");

        /// <summary>Documento expedido para el cobro de peajes.</summary>
        [HttpPost("peajes")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        public Task<IActionResult> ReceiveTollDocument([FromBody] TollDocumentRequest request) =>
            EmitAsync(request, "DE-PEAJE", "40", "PJ");

        /// <summary>Extracto expedido por sociedades financieras y fondos.</summary>
        [HttpPost("extracto")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        public Task<IActionResult> ReceiveFinancialStatementDocument([FromBody] FinancialStatementDocumentRequest request) =>
            EmitAsync(request, "DE-EXTRACTO", "45", "EXT");

        /// <summary>Tiquete o billete aéreo de transporte de pasajeros.</summary>
        [HttpPost("transporte-aereo")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        public Task<IActionResult> ReceiveAirTransportDocument([FromBody] AirTransportDocumentRequest request) =>
            EmitAsync(request, "DE-AEREO", "50", "AER");

        /// <summary>Comprobante de liquidación de operaciones de Bolsa de Valores, Bolsa Agropecuaria y otros commodities.</summary>
        [HttpPost("bolsa")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        public Task<IActionResult> ReceiveStockExchangeDocument([FromBody] StockExchangeDocumentRequest request) =>
            EmitAsync(request, "DE-BOLSA", "55", "BV");

        /// <summary>Documento expedido para servicios públicos y domiciliarios.</summary>
        [HttpPost("servicios-publicos")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        public Task<IActionResult> ReceivePublicUtilityDocument([FromBody] PublicUtilityDocumentRequest request) =>
            EmitAsync(request, "DE-SERVICIOSP", "60", "SP");

        // Flujo compartido por los 10 subtipos: solo cambia el DocumentType de la Resolución, el
        // DianCode/InvoiceTypeCode, y el prefijo del nombre de archivo.
        private async Task<IActionResult> EmitAsync(InvoiceRequest request, string documentTypeCode, string dianCode, string fileTag, bool onSite = false)
        {
            if (string.IsNullOrWhiteSpace(request.DocumentNumber) || string.IsNullOrWhiteSpace(request.Prefix))
                return BadRequest("El prefijo y el número de documento son obligatorios.");

            if (request.IssueDate == default) request.IssueDate = Fel.Core.Models.ColombiaTime.Now;

            var resolved = await Fel.Api.Integration.Security.EmisorResolver.ResolverAsync(
                this, _dbContext, _cryptoVault, documentTypeCode, request.Prefix, "Documento Equivalente");
            if (resolved.Error != null) return resolved.Error;

            var client = resolved.Client!;
            var resolution = resolved.Resolution ?? Fel.Infrastructure.Services.SandboxSimulation.ResolucionDePrueba(
                client.Id, documentTypeCode, request.Prefix,
                long.TryParse(request.DocumentNumber, out var numeroDoc) ? numeroDoc : 1);

            var municipalities = await _dbContext.DianMunicipalities.AsNoTracking().ToDictionaryAsync(m => m.Code);
            var ublData = DianDocumentMapper.BuildEquivalentDocumentDataFromRequest(request, client, resolved.Location!, resolution, municipalities, dianCode, onSite);

            var cufe = _ublGenerator.CalculateEquivalentDocumentCufe(ublData);
            var xml = _ublGenerator.GenerateInvoiceXml(ublData);

            // Camino simulado: el CUFE se calcula de verdad y se devuelve el XML que se habría
            // transmitido, sin firmar ni enviar nada. Ver Fel.Infrastructure SandboxSimulation.
            if (resolved.EsSandbox)
            {
                return Ok(new
                {
                    cufe,
                    status = "PROCESSED",
                    simulated = true,
                    message = Fel.Infrastructure.Services.SandboxSimulation.Mensaje(
                        usoClaveTecnicaDeRelleno: ublData.TechnicalKey == Fel.Infrastructure.Services.SandboxSimulation.ClaveTecnicaDeRelleno),
                    trackId = Fel.Infrastructure.Services.SandboxSimulation.NuevoTrackId(),
                    xml
                });
            }

            var cert = resolved.Certificate!;

            string signedXml;
            try
            {
                signedXml = _xmlSigner.SignXml(xml, cert, resolved.CertificateChain);
            }
            catch (Exception ex)
            {
                return UnprocessableEntity(new { message = $"No se pudo firmar el documento: {ex.Message}" });
            }

            try
            {
                var dianResponse = await _dianSoapClient.SendDocumentSyncAsync($"{fileTag}{request.Prefix}{request.DocumentNumber}.xml", signedXml, cert, ublData.Environment);
                return Ok(new { cufe, status = "PROCESSED", dianResponse });
            }
            catch (Exception ex)
            {
                return UnprocessableEntity(new { message = $"La DIAN rechazó el documento equivalente: {ex.Message}", cufe });
            }
        }
    }
}
