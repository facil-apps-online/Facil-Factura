using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fel.Infrastructure.Services
{
    // Revisa el consumo de los clientes con plan y, según las preferencias del tenant (TenantPlanSettings), de su plan y del propio cliente:
    // genera avisos al llegar al porcentaje de aviso y al límite de documentos incluidos, avisa por correo y, si la acción es "bloquear",
    // bloquea al cliente al llegar al límite y lo desbloquea cuando ya no corresponde (ciclo nuevo, cambio de plan o de acción).
    //
    // No corre en el momento de emitir: lo ejecuta un proceso periódico (PlanAlertWorker), así que un cliente puede pasarse por unos pocos
    // documentos entre una revisión y otra. Solo toca los bloqueos que puso él mismo (Client.BlockedBy = "plan"), nunca los del tenant.
    public class PlanAlertService
    {
        // Marca de los bloqueos puestos por este servicio.
        public const string SystemBlockMarker = "plan";

        private readonly FelDbContext _db;
        private readonly ClientPlanUsageService _usage;
        private readonly IEmailOutbox _email;
        private readonly ILogger<PlanAlertService> _logger;

        public PlanAlertService(FelDbContext db, ClientPlanUsageService usage, IEmailOutbox email, ILogger<PlanAlertService> logger)
        {
            _db = db;
            _usage = usage;
            _email = email;
            _logger = logger;
        }

        public static PlanExceedAction EffectiveAction(ClientPlanAssignment assignment, TenantPlan plan, TenantPlanSettings settings) =>
            assignment.ExceedActionOverride ?? plan.ExceedAction ?? settings.DefaultExceedAction;

        // Devuelve cuántos avisos nuevos generó.
        public async Task<int> EvaluateAllAsync(CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            var today = now.Date;

            var assignments = await _db.ClientPlanAssignments.AsNoTracking()
                .Include(a => a.Plan)
                .Where(a => a.StartDate <= today && (a.EndDate == null || a.EndDate >= today) && a.Plan!.IncludedDocuments > 0)
                .ToListAsync(ct);

            var clientIds = assignments.Select(a => a.ClientId).ToList();
            var clients = await _db.Clients.Include(c => c.Tenant)
                .Where(c => clientIds.Contains(c.Id) && c.IsActive && !c.IsDeveloperSandbox)
                .ToDictionaryAsync(c => c.Id, ct);
            var settingsByTenant = await _db.TenantPlanSettings.AsNoTracking().ToDictionaryAsync(s => s.TenantId, ct);

            var created = 0;
            var stillBlocked = new HashSet<Guid>();

            foreach (var assignment in assignments)
            {
                if (!clients.TryGetValue(assignment.ClientId, out var client)) continue;
                var plan = assignment.Plan!;
                var settings = settingsByTenant.GetValueOrDefault(client.TenantId) ?? new TenantPlanSettings { TenantId = client.TenantId };
                var action = EffectiveAction(assignment, plan, settings);

                var usage = await _usage.GetUsageAsync(client.Id, now);
                if (usage == null) continue;

                var reachedLimit = false;
                foreach (var unit in usage.Units.Where(u => u.Included is > 0))
                {
                    var included = unit.Included!.Value;
                    var limit = unit.Documents >= included;
                    reachedLimit |= limit;

                    PlanAlertKind? kind = limit ? PlanAlertKind.LimitReached
                        : unit.Documents * 100 >= included * Math.Clamp(settings.WarnPercent, 1, 99) ? PlanAlertKind.Warning
                        : null;
                    if (kind == null) continue;

                    if (settings.AlertsEnabled && await TryCreateAlertAsync(client, assignment, usage, unit, kind.Value, plan, settings, ct)) created++;
                }

                if (action == PlanExceedAction.Block && reachedLimit)
                {
                    stillBlocked.Add(client.Id);
                    if (client.BlockedAt == null)
                    {
                        client.BlockedAt = now;
                        client.BlockReason = $"Plan «{plan.Name}»: se alcanzó el límite de {plan.IncludedDocuments} documentos del ciclo.";
                        client.BlockedBy = SystemBlockMarker;
                        await _db.SaveChangesAsync(ct);
                        var unit = usage.Units.First(u => u.Included is > 0 && u.Documents >= u.Included);
                        if (await TryCreateAlertAsync(client, assignment, usage, unit, PlanAlertKind.Blocked, plan, settings, ct)) created++;
                    }
                }
            }

            // Bloqueos del sistema que ya no corresponden: ciclo nuevo, plan o acción cambiados, o el cliente ya no tiene ese plan.
            var systemBlocked = await _db.Clients.Include(c => c.Tenant)
                .Where(c => c.BlockedAt != null && c.BlockedBy == SystemBlockMarker && !stillBlocked.Contains(c.Id))
                .ToListAsync(ct);
            foreach (var client in systemBlocked)
            {
                client.BlockedAt = null;
                client.BlockReason = null;
                client.BlockedBy = null;
                var assignment = assignments.FirstOrDefault(a => a.ClientId == client.Id);
                _db.PlanAlerts.Add(new PlanAlert
                {
                    Id = Guid.NewGuid(), TenantId = client.TenantId, ClientId = client.Id, AssignmentId = assignment?.Id ?? Guid.Empty,
                    PeriodStart = today, Kind = PlanAlertKind.Unblocked, Message = $"{ClientName(client)}: se levantó el bloqueo automático por límite de documentos.", CreatedAt = now
                });
                created++;
                _logger.LogInformation("Bloqueo automático por plan levantado para el cliente {ClientId}.", client.Id);
            }
            if (systemBlocked.Count > 0) await _db.SaveChangesAsync(ct);

            return created;
        }

        private async Task<bool> TryCreateAlertAsync(Client client, ClientPlanAssignment assignment, PlanUsage usage, PlanUsageUnit unit, PlanAlertKind kind,
            TenantPlan plan, TenantPlanSettings settings, CancellationToken ct)
        {
            var exists = await _db.PlanAlerts.AnyAsync(a => a.AssignmentId == assignment.Id && a.PeriodStart == usage.PeriodStart && a.Kind == kind && a.BranchId == unit.BranchId, ct);
            if (exists) return false;

            var included = unit.Included ?? 0;
            var scopeText = unit.BranchId != null ? $" (sucursal {unit.Name})" : string.Empty;
            var text = kind switch
            {
                PlanAlertKind.Warning => $"ha usado {unit.Documents} de {included} documentos incluidos en su plan «{plan.Name}»{scopeText} ({unit.Documents * 100 / Math.Max(1, included)}%).",
                PlanAlertKind.LimitReached => $"llegó al límite de {included} documentos incluidos en su plan «{plan.Name}»{scopeText}."
                    + (usage.PricePerDocument > 0 ? $" Los documentos adicionales se cobran a ${usage.PricePerDocument:N0} cada uno." : string.Empty),
                PlanAlertKind.Blocked => $"fue bloqueado automáticamente al llegar al límite de {included} documentos de su plan «{plan.Name}»{scopeText}.",
                _ => string.Empty
            };
            var message = $"{ClientName(client)}: {text}";

            _db.PlanAlerts.Add(new PlanAlert
            {
                Id = Guid.NewGuid(), TenantId = client.TenantId, ClientId = client.Id, AssignmentId = assignment.Id, PeriodStart = usage.PeriodStart,
                Kind = kind, BranchId = unit.BranchId, Documents = unit.Documents, Included = included, Message = message.Length > 500 ? message[..500] : message,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);

            if (settings.EmailTenant && !string.IsNullOrWhiteSpace(client.Tenant?.Email))
                await SendAsync(client, client.Tenant!.Email, "Alerta de consumo: " + ClientName(client), message, ct);
            if (settings.EmailClient && !string.IsNullOrWhiteSpace(client.Email))
                await SendAsync(client, client.Email, "Aviso de consumo de su plan", $"Su empresa {text}", ct);

            return true;
        }

        private async Task SendAsync(Client client, string to, string subject, string text, CancellationToken ct)
        {
            try
            {
                var body = $"<p>{WebUtility.HtmlEncode(text)}</p>";
                // Queda en la bandeja de salida; el worker lo envía y reintenta.
                var queued = _email.Add(new OutboundEmail { Kind = EmailKind.Notification, Client = client, RecipientEmail = to, Subject = subject, BodyHtml = body });
                await _db.SaveChangesAsync(ct);
                await _email.NotifyAsync(queued);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error enviando el aviso de consumo a {To}.", to);
            }
        }

        private static string ClientName(Client client) => !string.IsNullOrEmpty(client.CommercialName) ? client.CommercialName : client.CompanyName;
    }
}
