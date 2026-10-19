using System;
using System.Collections.Generic;
using System.Linq;
using Fel.Infrastructure.Security;

namespace Fel.Infrastructure.Dian
{
    // Respuesta de GetAcquirer (AdquirienteResponse del WSDL de la DIAN): StatusCode, Message, ReceiverName y ReceiverEmail.
    // El adquirente "existe" cuando la DIAN devuelve un nombre; el correo puede traer varias direcciones.
    public sealed record DianAcquirerResult(bool Found, string? Name, IReadOnlyList<string> Emails, string? StatusCode, string? Message)
    {
        // Códigos de tipo de documento que acepta el servicio (Guía Herramienta para el Consumo de Web Services, nota de identificationType).
        public static readonly IReadOnlySet<string> IdentificationTypes = new HashSet<string>
        {
            "11", "12", "13", "21", "22", "31", "41", "42", "47", "48", "50", "91"
        };

        public static DianAcquirerResult FromSoapResponse(string soapResponse)
        {
            var document = SafeXml.Parse(soapResponse);
            string? Value(string localName) => document.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == localName)?.Value.Trim() is { Length: > 0 } text ? text : null;

            var name = Value("ReceiverName");
            var emails = (Value("ReceiverEmail") ?? string.Empty)
                .Split(new[] { ';', ',', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return new DianAcquirerResult(name != null, name, emails, Value("StatusCode"), Value("Message"));
        }
    }
}
