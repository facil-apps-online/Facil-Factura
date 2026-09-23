using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Fel.Core.Interfaces
{
    // Cliente del servicio centralizado Facil Reports (reports.facil-apps.online), que renderiza
    // PDFs a partir de una plantilla .repx (identificada por templateKey, el mismo valor guardado
    // en DocumentTemplate.RepxTemplateKey) y datos JSON. FacilFactura es una plataforma más entre
    // las que consume ese servicio (junto con Glamtica, TattooSuite, Nexu), cada una con su propia
    // API key.
    public interface IFacilReportsClient
    {
        // data se bindea por nombre a los Parameters del reporte (los objetos anidados se aplanan
        // como "padre.hijo"); un arreglo bajo la clave "DataSource" alimenta las tablas del reporte
        // (ej. las líneas de una factura). Devuelve null si la plantilla no existe o el servicio no
        // está configurado (sin API key) — no lanza excepción para no tumbar el flujo de emisión.
        Task<byte[]?> GenerateReportAsync(string templateKey, Dictionary<string, object?> data, CancellationToken ct = default);

        // Sube un archivo .repx a Facil Reports bajo templateKey (POST /api/templates/save), para
        // que quede disponible antes de referenciarlo desde un DocumentTemplate. Devuelve false si
        // el servicio no está configurado o si Facil Reports rechazó la subida.
        Task<bool> UploadTemplateAsync(string templateKey, byte[] repxBytes, CancellationToken ct = default);
    }
}
