using System.Threading.Tasks;
using Fel.Core.Entities;

namespace Fel.Core.Interfaces
{
    // Dispara un Evento de Recepción RADIAN sobre un documento de un tercero ya ingresado
    // (ReceivedDocument) — lo usan tanto la carga manual como (más adelante) el worker de correo,
    // para no repetir la lógica de firmar/enviar/registrar en cada lugar.
    public interface IReceptionEventService
    {
        Task<ReceivedDocumentEvent> TriggerEventAsync(ReceivedDocument document, Client client, string eventCode);

        // Dispara todos los eventos que el Client tenga marcados para autoenviar (AutoSendAcuseRecibo,
        // etc.) sobre este documento recién ingresado.
        Task TriggerEnabledEventsAsync(ReceivedDocument document, Client client);
    }
}
