using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Dataico;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Services
{
    public enum PublishOutcomeKind
    {
        Published,
        NotFound,
        BadRequest,
        // Dataico sin plantilla elegida: el portal reconoce el caso para ofrecer ir a elegirla.
        TemplateRequired,
        Conflict,
        // El proveedor (Dataico o la DIAN) rechazó el documento; Detail trae el motivo legible.
        ProviderRejected
    }

    public sealed record PublishOutcome(PublishOutcomeKind Kind, string? Message = null, string? Detail = null, string? Warning = null, object? Sent = null)
    {
        public bool Success => Kind == PublishOutcomeKind.Published;
    }

    // Emite una factura o nota ya guardada (borrador o rechazada) por el proveedor del emisor (DIAN directa o un integrador). Es la lógica que antes
    // vivía dentro de InvoiceController.Publish; la comparten el portal y la API de integración para que un documento se emita igual venga de donde venga.
    //
    // El que llama se encarga de lo que depende de la petición: tomar el bloqueo del documento (SqlAppLock) antes de cargarlo, cargarlo con sus ítems,
    // retenciones, tercero y observaciones, y convertir el resultado en la respuesta HTTP.
    public class InvoicePublishingService
    {
        private readonly FelDbContext _dbContext;
        private readonly IEnumerable<IDocumentSubmissionProvider> _submissionProviders;
        private readonly DataicoCustomPdfService _customPdfService;

        public InvoicePublishingService(FelDbContext dbContext, IEnumerable<IDocumentSubmissionProvider> submissionProviders, DataicoCustomPdfService customPdfService)
        {
            _dbContext = dbContext;
            _submissionProviders = submissionProviders;
            _customPdfService = customPdfService;
        }

        // Editable/publicable/eliminable mientras no haya llegado exitosamente al integrador o a la DIAN:
        // un DRAFT nunca se envió, y un REJECTED fue rechazado, así que tampoco llegó.
        public static bool IsEditable(string status) => status == "DRAFT" || status == "REJECTED";

        public async Task<PublishOutcome> PublishAsync(Document invoice, Guid clientId, string? requestPaymentMeans = null, string? requestPaymentMeansType = null)
        {
            if (!IsEditable(invoice.Status))
                return new PublishOutcome(PublishOutcomeKind.BadRequest, "La factura ya fue emitida o está en proceso.");

            if (invoice.Customer == null)
                return new PublishOutcome(PublishOutcomeKind.BadRequest, "La factura no tiene un cliente asociado.");

            // Factura de mandato (operación 11): cada línea debe declarar su mandante, con la DIAN directa y con Dataico.
            var operationType = invoice.DocumentTypeId.HasValue
                ? await _dbContext.DocumentTypes.AsNoTracking().Where(t => t.Id == invoice.DocumentTypeId.Value).Select(t => t.OperationType).FirstOrDefaultAsync()
                : null;
            if (operationType == DocumentType.MandateOperationType && !invoice.ReferenceDocumentId.HasValue
                && invoice.Items.Any(i => string.IsNullOrWhiteSpace(i.MandanteIdentification) || string.IsNullOrWhiteSpace(i.MandanteIdentificationType)))
            {
                return new PublishOutcome(PublishOutcomeKind.BadRequest, "En una factura de mandato cada línea debe tener su mandante.");
            }

            var client = await _dbContext.Clients.Include(c => c.Integrator).FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null) return new PublishOutcome(PublishOutcomeKind.NotFound, "Emisor no encontrado.");

            var provider = _submissionProviders.FirstOrDefault(p => p.IntegratorCode == client.Integrator.Code);
            if (provider == null)
            {
                return new PublishOutcome(PublishOutcomeKind.BadRequest, "Este emisor no tiene configurado un proveedor de documentos electrónicos habilitado.");
            }

            // Con Dataico, sin plantilla elegida el correo saldría con el diseño predeterminado de Dataico: se exige elegirla antes.
            if (client.Integrator.Code == "DATAICO" && await _customPdfService.IsPublishedTemplateMissingAsync(clientId, invoice.DocumentTypeId, invoice.TypeCode))
            {
                return new PublishOutcome(PublishOutcomeKind.TemplateRequired);
            }

            // El cliente puede tener varias resoluciones activas (una por prefijo); si no se
            // eligió una explícitamente al crear el borrador, se usa la marcada como
            // predeterminada para FE, y si ninguna lo está, se cae al comportamiento anterior
            // (la más reciente por vigencia) para no romper clientes que aún no eligieron default.
            //
            // Esto aplica IGUAL para notas crédito/débito: la DIAN no autoriza una "resolución"
            // propia para notas (confirmado contra el ejemplo oficial CreditNote.xml — no trae
            // sts:InvoiceControl/Autorización/Rango — y contra el propio comentario de
            // DataicoDocumentMapper: "Dataico no maneja numeración registrada para notas"). La
            // resolución de facturas del emisor solo presta su Prefijo (CorporateRegistrationScheme),
            // no un rango autorizado.
            var resolution = invoice.ResolutionId.HasValue
                // La resolución elegida debe ser de factura de venta (FE): sirve para la factura estándar, la de mandato y la de exportación,
                // sin exigir una específica para cada una. Las de documento soporte o nómina no se pueden usar acá.
                ? await _dbContext.Resolutions.ForBranch(_dbContext, invoice.BranchId).FirstOrDefaultAsync(r => r.Id == invoice.ResolutionId.Value && r.ClientId == clientId && r.IsActive && r.DocumentType == "FE")
                : await _dbContext.Resolutions.ForBranch(_dbContext, invoice.BranchId)
                    .Where(r => r.ClientId == clientId && r.IsActive && r.DocumentType == "FE")
                    .OrderByDescending(r => r.IsDefault)
                    .ThenByDescending(r => r.ValidTo)
                    .FirstOrDefaultAsync();

            if (resolution == null)
            {
                return new PublishOutcome(PublishOutcomeKind.BadRequest, "No hay una resolución de facturación activa disponible para esta sucursal.");
            }

            var paymentMeans = string.IsNullOrWhiteSpace(requestPaymentMeans) ? invoice.PaymentMeans : requestPaymentMeans;
            var paymentMeansType = string.IsNullOrWhiteSpace(requestPaymentMeansType) ? invoice.PaymentMeansType : requestPaymentMeansType;

            if (string.IsNullOrWhiteSpace(paymentMeansType))
            {
                return new PublishOutcome(PublishOutcomeKind.BadRequest, "Debes indicar la forma de pago (Contado/Crédito).");
            }

            // DEBITO ("Contado" en la UI) sí necesita el medio de pago concreto (efectivo,
            // transferencia, etc.). CREDITO no lo captura en ningún lado del formulario — ese
            // caso solo maneja plazo de pago — así que exigirlo aquí rechazaba toda factura a
            // crédito antes de siquiera llegar al integrador.
            if (paymentMeansType == "DEBITO" && string.IsNullOrWhiteSpace(paymentMeans))
            {
                return new PublishOutcome(PublishOutcomeKind.BadRequest, "Debes indicar el medio de pago.");
            }

            // El ajuste de centavos se recalcula con lo que realmente se va a enviar (el borrador pudo guardarse antes de activarlo en el cliente).
            Fel.Core.Models.RoundingAdjustment.Apply(invoice, invoice.Items, client.RoundingAdjustment);

            var claim = await DocumentSubmissionGuard.TryClaimAsync(_dbContext, invoice);
            var claimConflict = DocumentSubmissionGuard.ClaimConflictMessage(claim);
            if (claimConflict != null) return new PublishOutcome(PublishOutcomeKind.Conflict, claimConflict);

            DocumentSubmissionResult result;
            try
            {
                result = await DocumentSubmissionGuard.SendClaimedAsync(_dbContext, invoice,
                    () => provider.SubmitAsync(invoice, invoice.Customer, invoice.Items, client, resolution, paymentMeans!, paymentMeansType));
            }
            catch (NotSupportedException ex)
            {
                return new PublishOutcome(PublishOutcomeKind.BadRequest, ex.Message);
            }

            if (result.Success)
            {
                DocumentSubmissionGuard.MarkCompleted(invoice);
                // Tarifa vigente al momento de emitir, para que el corte mensual
                // (SuperadminBillingController.CalculateBilling) tenga con qué facturar — antes
                // quedaba en 0 y el corte real generaba cobros en $0 sin importar el volumen.
                invoice.PriceCharged = await BranchProvisioning.PricePerDocumentAsync(_dbContext, invoice.BranchId);
                invoice.IntegratorId = client.IntegratorId;
            }
            else
            {
                DocumentSubmissionGuard.MarkFailed(invoice, invoice.DianResponseMessage);
            }

            await _dbContext.SaveChangesAsync();

            if (!result.Success)
            {
                return new PublishOutcome(PublishOutcomeKind.ProviderRejected, "El proveedor de documentos electrónicos rechazó la factura.", DataicoDianOutcome.Readable(invoice.DianResponseMessage));
            }

            // Warning: el documento se emitió pero algo secundario falló (p. ej. el correo con los adjuntos no salió); el portal lo muestra.
            return new PublishOutcome(PublishOutcomeKind.Published, "Factura emitida correctamente.", Warning: result.Warning);
        }
    }
}
