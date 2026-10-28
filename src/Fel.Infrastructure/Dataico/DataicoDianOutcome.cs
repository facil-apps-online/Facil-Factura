using System.Text.Json;
using Fel.Core.Entities;
using Fel.Infrastructure.Dataico.Models;

namespace Fel.Infrastructure.Dataico;

// Una respuesta 2xx de Dataico solo dice que creó el documento; la DIAN lo revisa después y el resultado viene en la misma respuesta (dian_status). Si la
// DIAN lo rechaza, el documento debe quedar rechazado y editable (Dataico lo reemplaza al reenviarlo con el mismo número), no aprobado.
public static class DataicoDianOutcome
{
    public const string RejectedStatus = "DIAN_RECHAZADO";

    // Usuario de la cuenta de demostración de Dataico: sus documentos llegan al sandbox y la DIAN los rechaza por diseño (NIT no autorizado).
    public const string DemoUser = "demo@dataico.com";

    // Clientes de pruebas: la cuenta demo de Dataico, los clientes de demostración de un tenant y los sandbox de developers. Ahí un rechazo de la DIAN es lo
    // esperado y todo sigue como antes.
    public static bool IsSandbox(Client client) =>
        client.IsDeveloperSandbox || client.IsDemo || string.Equals(client.DataicoApiUser, DemoUser, StringComparison.OrdinalIgnoreCase);

    // Solo para el envío de un documento (nunca para consultas). Devuelve true si el resultado pasó a rechazado.
    public static bool Apply(DataicoResult result, Client client)
    {
        if (!result.Success || !string.Equals(result.DianStatus, RejectedStatus, StringComparison.OrdinalIgnoreCase) || IsSandbox(client)) return false;
        result.Success = false;
        result.DianRejected = true;
        return true;
    }

    // Motivo legible para el usuario cuando lo que se guardó es una respuesta de Dataico rechazada por la DIAN; cualquier otro texto se devuelve igual.
    public static string? Readable(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored)) return stored;
        try
        {
            using var doc = JsonDocument.Parse(stored);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return stored;
            if (!root.TryGetProperty("dian_status", out var status) || !string.Equals(status.GetString(), RejectedStatus, StringComparison.OrdinalIgnoreCase)) return stored;

            var reasons = new List<string>();
            if (root.TryGetProperty("dian_messages", out var messages) && messages.ValueKind == JsonValueKind.Array)
                foreach (var message in messages.EnumerateArray())
                {
                    var text = message.GetString();
                    if (!string.IsNullOrWhiteSpace(text) && text.Contains("Rechazo", StringComparison.OrdinalIgnoreCase)) reasons.Add(text);
                }
            return reasons.Count > 0
                ? "La DIAN rechazó el documento. " + string.Join(" ", reasons)
                : "La DIAN rechazó el documento. Revisa el motivo en la respuesta de la DIAN.";
        }
        catch (JsonException)
        {
            return stored;
        }
    }
}
