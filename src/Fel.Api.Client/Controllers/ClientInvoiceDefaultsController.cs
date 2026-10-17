using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Fel.Api.Security;

namespace Fel.Api.Client.Controllers
{
    // Valores por defecto del formulario de "Nueva factura" del Client. Los lee cualquier usuario del portal (el formulario los aplica al
    // abrirse); solo el administrador los cambia. Son del Client, no de una sucursal.
    [ApiController]
    [Route("api/client/invoice-defaults")]
    [AllowAllBranches]
    public class ClientInvoiceDefaultsController : ClientPortalControllerBase
    {
        private const string Cop = "COP";
        private static readonly string[] SupportedDianCodes = { "01", "02", "91", "92" };

        private readonly FelDbContext _dbContext;

        public ClientInvoiceDefaultsController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var clientId = GetCurrentClientId();
            var row = await _dbContext.ClientInvoiceDefaults.AsNoTracking().FirstOrDefaultAsync(d => d.ClientId == clientId);
            return Ok(ToDto(row));
        }

        [ClientRole(ClientUserRoles.Administrator)]
        [HttpPut]
        public async Task<IActionResult> Put([FromBody] InvoiceDefaultsRequest request)
        {
            var clientId = GetCurrentClientId();

            var documentTypeId = request.DocumentTypeId == Guid.Empty ? null : request.DocumentTypeId;
            var paymentMeans = Clean(request.PaymentMeans);
            var paymentMeansType = Clean(request.PaymentMeansType);
            var currency = Clean(request.Currency)?.ToUpperInvariant();
            var notes = Clean(request.Notes);

            if (documentTypeId != null)
            {
                var enabled = await _dbContext.ClientEnabledDocumentTypes
                    .AnyAsync(e => e.ClientId == clientId && e.DocumentTypeId == documentTypeId);
                var supported = await _dbContext.DocumentTypes
                    .AnyAsync(d => d.Id == documentTypeId && d.IsActive && SupportedDianCodes.Contains(d.DianCode));
                if (!enabled || !supported) return BadRequest("El tipo de documento no está habilitado para este cliente.");
            }

            if (paymentMeans != null && !await InCatalog(TaxCatalogKind.PaymentMeans, paymentMeans))
                return BadRequest("El medio de pago no existe en el catálogo.");

            if (paymentMeansType != null && !await InCatalog(TaxCatalogKind.FormaPago, paymentMeansType))
                return BadRequest("La forma de pago no existe en el catálogo.");

            // El plazo solo tiene sentido a crédito: contado o sin forma de pago lo deja vacío.
            int? termDays = paymentMeansType == "CREDITO" ? request.PaymentTermDays : null;
            if (termDays is < 0 or > 3650) return BadRequest("El plazo de pago debe estar entre 0 y 3650 días.");

            if (currency == Cop) currency = null;
            if (currency != null)
            {
                var foreignEnabled = await _dbContext.Clients.AnyAsync(c => c.Id == clientId && c.ForeignCurrencyEnabled);
                if (!foreignEnabled) return BadRequest("El cliente no tiene habilitada la facturación en moneda extranjera.");
                if (!await InCatalog(TaxCatalogKind.Currency, currency)) return BadRequest("La moneda no existe en el catálogo.");
            }

            if (notes != null && notes.Length > 500) return BadRequest("La observación no puede pasar de 500 caracteres.");

            var row = await _dbContext.ClientInvoiceDefaults.FirstOrDefaultAsync(d => d.ClientId == clientId);
            if (row == null)
            {
                row = new ClientInvoiceDefaults { Id = Guid.NewGuid(), ClientId = clientId };
                _dbContext.ClientInvoiceDefaults.Add(row);
            }

            row.DocumentTypeId = documentTypeId;
            row.PaymentMeans = paymentMeans;
            row.PaymentMeansType = paymentMeansType;
            row.PaymentTermDays = termDays;
            row.Currency = currency;
            row.Notes = notes;
            row.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            return Ok(ToDto(row));
        }

        private Task<bool> InCatalog(TaxCatalogKind kind, string category) =>
            _dbContext.TaxCatalogItems.AnyAsync(i => i.IsActive && i.Kind == kind && i.Category == category);

        private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static object ToDto(ClientInvoiceDefaults? row) => new
        {
            documentTypeId = row?.DocumentTypeId,
            paymentMeans = row?.PaymentMeans,
            paymentMeansType = row?.PaymentMeansType,
            paymentTermDays = row?.PaymentTermDays,
            currency = row?.Currency,
            notes = row?.Notes
        };

        public class InvoiceDefaultsRequest
        {
            public Guid? DocumentTypeId { get; set; }
            public string? PaymentMeans { get; set; }
            public string? PaymentMeansType { get; set; }
            public int? PaymentTermDays { get; set; }
            public string? Currency { get; set; }
            public string? Notes { get; set; }
        }
    }
}
