using Fel.Core.Entities;

namespace Fel.Infrastructure.Services
{
    // Cuándo lo que envía un cliente es sandbox: no cuenta como emitido ni se cobra (ver Document.SentToSandbox). Se evalúa en el momento del envío y queda
    // guardado en el documento.
    //
    // - Con Dataico es sandbox su ambiente de pruebas, la cuenta demo de Dataico (aunque figure en producción) y un cliente de demostración.
    // - Con la emisión directa a la DIAN no hay ambiente de pruebas real que usar (el ambiente de habilitación no lo ofrece como tal): solo lo son un cliente de
    //   demostración y un sandbox de developers.
    public static class SandboxRule
    {
        public const string DataicoDemoUser = "demo@dataico.com";

        public static bool ForDataico(Client client) =>
            client.IsDemo || client.IsDeveloperSandbox
            || string.Equals(client.DataicoEnvironment, "PRUEBAS", StringComparison.OrdinalIgnoreCase)
            || string.Equals(client.DataicoApiUser, DataicoDemoUser, StringComparison.OrdinalIgnoreCase);

        public static bool ForNative(Client client) => client.IsDemo || client.IsDeveloperSandbox;
    }
}
