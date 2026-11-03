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
    // Emite un Documento Soporte (adquisiciones a no obligados a facturar) o su Nota de Ajuste ya guardados (borrador o rechazados) por el proveedor del
    // emisor: la DIAN directa o un integrador. Es la lógica que antes vivía en SupportDocumentController.Publish y solo servía con Dataico; la comparten
    // el portal y la API de integración. Igual que InvoicePublishingService, quien llama toma el bloqueo del documento, lo carga con sus ítems,
    // retenciones, tercero (el vendedor) y tipo, y convierte el resultado en la respuesta HTTP.
    public class SupportDocumentPublishingService
    {
        public const string TypeCode = "DS";
        public const string AdjustmentTypeCode = "DS-AJUSTE";

        private readonly FelDbContext _dbContext;
        private readonly IEnumerable<ISupportDocumentSubmissionProvider> _providers;
        private readonly DataicoCustomPdfService _customPdfService;

        public SupportDocumentPublishingService(FelDbContext dbContext, IEnumerable<ISupportDocumentSubmissionProvider> providers, DataicoCustomPdfService customPdfService)
        {
            _dbContext = dbContext;
            _providers = providers;
            _customPdfService = customPdfService;
        }

        public static bool IsEditable(string status) => InvoicePublishingService.IsEditable(status);

        // Antes de usar el consecutivo de la resolución, el borrador se numeraba con DateTime.Ticks (18 dígitos). Esos borradores o rechazados viejos
        // se renumeran al publicar.
        private static bool NeedsConsecutive(string? number) =>
            string.IsNullOrEmpty(number) || (number.Length >= 15 && number.All(char.IsDigit));

        // El documento soporte permite fechas anteriores, pero solo dentro de la vigencia de su resolución y nunca posteriores al día actual. Las notas
        // de ajuste agregan el límite del documento original.
        public async Task<string?> ValidateDatesAsync(
            Guid clientId, Guid branchId, Guid? resolutionId, DateTime? issueDate, Guid? referenceDocumentId, bool requireCurrentResolution)
        {
            if (!issueDate.HasValue) return "Debes indicar la fecha de emisión.";
            if (!resolutionId.HasValue) return "Debes seleccionar la resolución de documento soporte.";

            var resolution = await _dbContext.Resolutions.ForBranch(_dbContext, branchId)
                .FirstOrDefaultAsync(r => r.Id == resolutionId.Value && r.ClientId == clientId && r.IsActive && (r.DocumentType == TypeCode || r.DocumentType == "POS"));
            if (resolution == null) return "La resolución seleccionada no es válida para este documento soporte o esta sucursal.";

            var today = Fel.Core.Models.ColombiaTime.Today;
            var minimumDate = resolution.ValidFrom.Date;
            if (referenceDocumentId.HasValue)
            {
                var original = await _dbContext.Documents
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == referenceDocumentId.Value && d.ClientId == clientId && d.BranchId == branchId && d.TypeCode == TypeCode);
                if (original == null) return "El documento soporte original no existe o no pertenece a esta sucursal.";
                if (original.IssueDate.Date > minimumDate) minimumDate = original.IssueDate.Date;
            }

            if (issueDate.Value.Date < minimumDate || issueDate.Value.Date > today)
                return referenceDocumentId.HasValue
                    ? "La fecha de la nota debe estar entre la fecha del documento soporte original y hoy, dentro de la vigencia de la resolución."
                    : "La fecha del documento debe estar dentro de la vigencia de la resolución y no puede ser futura.";
            if (issueDate.Value.Date > resolution.ValidTo.Date)
                return "La fecha del documento está fuera de la vigencia de la resolución.";
            if (requireCurrentResolution && (today < resolution.ValidFrom.Date || today > resolution.ValidTo.Date))
                return "La resolución está vencida o todavía no está vigente; no se puede emitir este documento.";
            return null;
        }

        public async Task<PublishOutcome> PublishAsync(Document document, Guid clientId)
        {
            if (!IsEditable(document.Status))
                return new PublishOutcome(PublishOutcomeKind.BadRequest, "El documento ya fue emitido o está en proceso.");
            if (document.Customer == null)
                return new PublishOutcome(PublishOutcomeKind.BadRequest, "El documento no tiene un proveedor (vendedor) asociado.");

            var client = await _dbContext.Clients.Include(c => c.Integrator).FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null) return new PublishOutcome(PublishOutcomeKind.NotFound, "Emisor no encontrado.");

            var provider = _providers.FirstOrDefault(p => p.IntegratorCode == client.Integrator.Code);
            if (provider == null)
                return new PublishOutcome(PublishOutcomeKind.BadRequest, "Este emisor no tiene configurado un proveedor de documentos electrónicos habilitado.");

            // Con Dataico, sin plantilla elegida el correo saldría con el diseño predeterminado de Dataico: se exige elegirla antes.
            if (client.Integrator.Code == "DATAICO" && await _customPdfService.IsPublishedTemplateMissingAsync(clientId, document.DocumentTypeId, document.TypeCode))
                return new PublishOutcome(PublishOutcomeKind.TemplateRequired);

            // El cliente puede tener varias resoluciones activas (una por prefijo); si no se eligió una al crear el borrador, se usa la marcada como
            // predeterminada, y si ninguna lo está, la más reciente por vigencia.
            var resolution = document.ResolutionId.HasValue
                ? await _dbContext.Resolutions.ForBranch(_dbContext, document.BranchId).FirstOrDefaultAsync(r => r.Id == document.ResolutionId.Value && r.ClientId == clientId && r.IsActive)
                : await _dbContext.Resolutions.ForBranch(_dbContext, document.BranchId)
                    .Where(r => r.ClientId == clientId && r.IsActive && (r.DocumentType == TypeCode || r.DocumentType == "POS"))
                    .OrderByDescending(r => r.IsDefault)
                    .ThenByDescending(r => r.ValidTo)
                    .FirstOrDefaultAsync();
            if (resolution == null)
                return new PublishOutcome(PublishOutcomeKind.BadRequest, "No hay una resolución de documento soporte activa registrada para este emisor.");

            var dateError = await ValidateDatesAsync(clientId, document.BranchId, resolution.Id, document.IssueDate, document.ReferenceDocumentId, requireCurrentResolution: true);
            if (dateError != null) return new PublishOutcome(PublishOutcomeKind.BadRequest, dateError);

            if (string.IsNullOrWhiteSpace(document.PaymentMeansType))
                return new PublishOutcome(PublishOutcomeKind.BadRequest, "Debes indicar la forma de pago (Contado/Crédito).");
            // DEBITO ("Contado") sí necesita el medio de pago concreto; CREDITO solo maneja plazo de pago.
            if (document.PaymentMeansType == "DEBITO" && string.IsNullOrWhiteSpace(document.PaymentMeans))
                return new PublishOutcome(PublishOutcomeKind.BadRequest, "Debes indicar el medio de pago.");

            Document? adjustedDocument = null;
            if (document.TypeCode == AdjustmentTypeCode)
            {
                if (!document.ReferenceDocumentId.HasValue)
                    return new PublishOutcome(PublishOutcomeKind.BadRequest, "Esta nota de ajuste debe referenciar el documento soporte que ajusta.");
                adjustedDocument = await _dbContext.Documents.FindAsync(document.ReferenceDocumentId.Value);
                if (adjustedDocument == null || string.IsNullOrEmpty(adjustedDocument.Cufe))
                    return new PublishOutcome(PublishOutcomeKind.BadRequest, "El documento soporte original no tiene un CUDS registrado (puede no haberse emitido). No se puede generar la nota.");
            }

            // Antes de reservar el consecutivo: un documento que no se puede enviar no debe consumirlo.
            var claimConflict = DocumentSubmissionGuard.ClaimConflictMessage(await DocumentSubmissionGuard.TryClaimAsync(_dbContext, document));
            if (claimConflict != null) return new PublishOutcome(PublishOutcomeKind.Conflict, claimConflict);

            // Consecutivo real, reclamado recién ahora que ya pasaron las validaciones (un documento rechazado que se reintenta conserva el que tiene). El
            // soporte usa el rango de su resolución; la nota de ajuste, su contador propio del Client. Un documento que ya trae número (el de la API de
            // integración o un reintento) conserva también su prefijo.
            if (NeedsConsecutive(document.Number))
            {
                try
                {
                    document.Number = document.TypeCode == AdjustmentTypeCode
                        ? (await ResolutionNumbering.ClaimNextNoteAsync(_dbContext, clientId, document.BranchId, NoteKind.SupportAdjustment)).Number.ToString()
                        : (await ResolutionNumbering.ClaimNextNumberAsync(_dbContext, resolution.Id)).ToString();
                }
                catch (ResolutionExhaustedException ex)
                {
                    // Numeración agotada: no se emite ni se consume nada; el documento sigue editable.
                    DocumentSubmissionGuard.MarkFailed(document, ex.Message);
                    await _dbContext.SaveChangesAsync();
                    return new PublishOutcome(PublishOutcomeKind.BadRequest, ex.Message);
                }
                document.Prefix = null;
            }

            // Prefijo del documento: se guarda en el propio documento y es el que se envía. El del soporte es el de su resolución. La nota de ajuste usa el
            // configurado en Consecutivos de Notas; sin uno configurado conserva el que ya tenga el documento y, si no tiene, el de la resolución.
            if (document.TypeCode != AdjustmentTypeCode) document.Prefix = resolution.Prefix;
            else if (string.IsNullOrWhiteSpace(document.Prefix))
                document.Prefix = await ResolutionNumbering.GetNotePrefixAsync(_dbContext, clientId, document.BranchId, NoteKind.SupportAdjustment) ?? resolution.Prefix;

            DocumentSubmissionResult result;
            try
            {
                result = await DocumentSubmissionGuard.SendClaimedAsync(_dbContext, document,
                    () => provider.SubmitSupportAsync(document, document.Customer, document.Items, client, resolution, adjustedDocument));
            }
            catch (NotSupportedException ex)
            {
                return new PublishOutcome(PublishOutcomeKind.BadRequest, ex.Message);
            }

            if (result.Success)
            {
                DocumentSubmissionGuard.MarkCompleted(document);
                document.IntegratorId = client.IntegratorId;
            }
            else
            {
                DocumentSubmissionGuard.MarkFailed(document, document.DianResponseMessage);
            }

            await _dbContext.SaveChangesAsync();

            if (!result.Success)
            {
                object? sent = null;
                if (!string.IsNullOrWhiteSpace(result.RequestBody))
                {
                    try { sent = System.Text.Json.JsonDocument.Parse(result.RequestBody).RootElement.Clone(); }
                    catch (System.Text.Json.JsonException) { sent = result.RequestBody; }
                }
                return new PublishOutcome(PublishOutcomeKind.ProviderRejected, "El proveedor de documentos electrónicos rechazó el documento soporte.", DataicoDianOutcome.Readable(document.DianResponseMessage), Sent: sent);
            }

            // La dirección del proveedor sin ciudad DANE no viaja (Dataico la rechaza): se avisa para que no desaparezca sin que se note.
            var seller = document.Customer;
            var cityWarning = Fel.Infrastructure.Dian.CustomerAddressRules.IncompleteCityMessage(
                !string.IsNullOrWhiteSpace(seller.CountryCode) && seller.CountryCode != "CO", seller.Address, seller.CityCode, seller.CityName, seller.StateName) == null
                ? null
                : "El proveedor tiene dirección pero no ciudad (código DANE): el documento se emitió sin su dirección. Complétala en el tercero para incluirla.";
            return new PublishOutcome(PublishOutcomeKind.Published, "Documento soporte emitido correctamente.", Warning: cityWarning ?? result.Warning);
        }
    }
}
