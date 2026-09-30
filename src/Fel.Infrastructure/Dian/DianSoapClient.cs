using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Fel.Core.Interfaces;

namespace Fel.Infrastructure.Dian
{
    // Construye el sobre SOAP exactamente como lo exige el binding real de la DIAN (wsHttpBinding
    // con seguridad de mensaje X.509 + WS-Addressing 1.0) — confirmado contra una librería pública
    // real y ya usada en producción para este mismo servicio (movaltech/cofacture-php,
    // github.com/movaltech/cofacture-php), no solo contra el anexo técnico (que solo muestra
    // ejemplos simplificados sin los headers WS-Addressing). Antes solo firmábamos el <Body> con
    // WS-Security "a secas" (patrón de basicHttpBinding) y sin los headers wsa:Action/To/
    // ReplyTo/MessageID — la DIAN aceptaba la conexión TCP/TLS y el POST completo, pero nunca
    // respondía nada (colgado hasta el timeout), consistente con un binding que no reconoce el
    // mensaje sin esos headers y no lo rechaza limpio.
    public class DianSoapClient : IDianSoapClient
    {
        private const string ProductionUrl = "https://vpfe.dian.gov.co/WcfDianCustomerServices.svc";
        private const string HabilitationUrl = "https://vpfe-hab.dian.gov.co/WcfDianCustomerServices.svc";

        private const string NsSoap12 = "http://www.w3.org/2003/05/soap-envelope";
        private const string NsWcf = "http://wcf.dian.colombia";
        private const string NsWsa = "http://www.w3.org/2005/08/addressing";
        private const string NsWsse = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
        private const string NsWsu = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";
        private const string ContentTypeSoap12 = "application/soap+xml";

        private static string ResolveUrl(string environment) => environment == "1" ? ProductionUrl : HabilitationUrl;

        // El numeral 7.5 del anexo técnico exige "autenticación mutua a través de certificados
        // digitales". Se descartó que fuera TLS mutuo a nivel de conexión (confirmado con curl: la
        // DIAN nunca pide el certificado durante el handshake — y la propia librería de referencia
        // documenta "RequireClientCertificate=false" en la política real del WSDL); el certificado
        // solo se usa para firmar el mensaje (WS-Security), no para autenticar la conexión TLS. Aun
        // así se arma un HttpClient por llamada porque el certificado cambia por cada Client/Tenant.
        private static HttpClient CreateHttpClient()
        {
            return new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        }

        private static StringContent BuildSoap12Content(string envelopeXml, string action)
        {
            var content = new StringContent(envelopeXml, Encoding.UTF8, ContentTypeSoap12);
            content.Headers.ContentType!.Parameters.Add(new NameValueHeaderValue("action", $"\"{action}\""));
            return content;
        }

        private static async Task<string> BuildZipBase64Async(string fileName, string signedXml)
        {
            using var memoryStream = new System.IO.MemoryStream();
            using (var zip = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = zip.CreateEntry(fileName, System.IO.Compression.CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                // Encoding.UTF8 escribe BOM al inicio del archivo por defecto — el XML dentro del zip
                // queda con 3 bytes antes de "<?xml ...?>", lo que puede tumbar un parser que no lo
                // maneje. UTF8Encoding(false) omite el BOM.
                using var writer = new System.IO.StreamWriter(entryStream, new UTF8Encoding(false));
                // UblGenerator/XadesSigner arman el XML con XElement/XmlDocument.OuterXml, que nunca
                // incluyen el prólogo <?xml ...?> — confirmado comparando contra un XML real ya
                // aceptado por la DIAN y contra el ejemplo oficial de la Caja de Herramientas: ambos
                // sí lo traen. Se agrega acá, al escribir el archivo final, sin tocar CUFE ni firma
                // (que operan sobre el contenido del documento, no sobre el prólogo).
                if (!signedXml.TrimStart().StartsWith("<?xml", StringComparison.Ordinal))
                    await writer.WriteAsync("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                await writer.WriteAsync(signedXml);
            }
            return Convert.ToBase64String(memoryStream.ToArray());
        }

        // Arma el sobre completo (Header con WS-Addressing + WS-Security firmado, Body con el
        // fragmento del caller), lo firma y lo envía. Todas las operaciones pasan por acá — antes
        // GetStatus/GetStatusZip ni siquiera firmaban nada, un vacío real frente al anexo técnico.
        // errorContext: si viene no-nulo, se lanza excepción cuando la respuesta HTTP no es 2xx
        // (igual que antes hacían los métodos "Send*"); si es nulo, se devuelve el body tal cual
        // (igual que antes hacían GetStatus/GetStatusZip, que nunca lanzaban).
        private async Task<string> SendSignedAsync(string operationName, string bodyInnerXml, X509Certificate2 certificate, string environment, string? errorContext = null)
        {
            var url = ResolveUrl(environment);
            var action = $"http://wcf.dian.colombia/IWcfDianCustomerServices/{operationName}";
            var messageId = "urn:uuid:" + Guid.NewGuid();

            // wsa:Action/To SIN mustUnderstand — confirmado contra la captura oficial de la DIAN
            // (Caja de Herramientas FE v19, numeral 15.8, pestaña WS-A: "Must understand: NONE").
            // wsse:Security sí mantiene mustUnderstand="1" (así lo muestra el propio ejemplo de
            // respuesta de la DIAN en el numeral 7.8.3 del anexo).
            var soapEnvelope = new XmlDocument { PreserveWhitespace = true };
            soapEnvelope.LoadXml($@"<soap:Envelope xmlns:soap=""{NsSoap12}"" xmlns:wcf=""{NsWcf}"" xmlns:wsa=""{NsWsa}"" xmlns:wsu=""{NsWsu}"">
                <soap:Header>
                    <wsa:Action>{action}</wsa:Action>
                    <wsa:To>{url}</wsa:To>
                    <wsa:ReplyTo>
                        <wsa:Address>http://www.w3.org/2005/08/addressing/anonymous</wsa:Address>
                    </wsa:ReplyTo>
                    <wsa:MessageID>{messageId}</wsa:MessageID>
                    <wsse:Security xmlns:wsse=""{NsWsse}"" soap:mustUnderstand=""1"">
                        <!-- Placeholder para el Timestamp y la firma WS-Security -->
                    </wsse:Security>
                </soap:Header>
                <soap:Body>
                    {bodyInnerXml}
                </soap:Body>
            </soap:Envelope>");

            SignSoapEnvelope(soapEnvelope, certificate);

            var content = BuildSoap12Content(soapEnvelope.OuterXml, action);
            using var client = CreateHttpClient();
            var response = await client.PostAsync(url, content);
            string responseString = await response.Content.ReadAsStringAsync();

            if (errorContext != null && !response.IsSuccessStatusCode)
                throw new Exception($"{errorContext}: {response.StatusCode} - {responseString}");

            return responseString;
        }

        public async Task<string> SendBillAsync(string zipFileName, string entryFileName, string signedXml, X509Certificate2 certificate, string environment)
        {
            var zipContentBase64 = await BuildZipBase64Async(entryFileName, signedXml);
            var body = $"<wcf:SendBillAsync><wcf:fileName>{zipFileName}</wcf:fileName><wcf:contentFile>{zipContentBase64}</wcf:contentFile></wcf:SendBillAsync>";
            return await SendSignedAsync("SendBillAsync", body, certificate, environment, "Error comunicando con la DIAN");
        }

        public async Task<string> SendNominaSyncAsync(string fileName, string signedXml, X509Certificate2 certificate, string environment)
        {
            var zipContentBase64 = await BuildZipBase64Async(fileName, signedXml);
            var body = $"<wcf:SendNominaSync><wcf:contentFile>{zipContentBase64}</wcf:contentFile></wcf:SendNominaSync>";
            return await SendSignedAsync("SendNominaSync", body, certificate, environment, "Error comunicando con la DIAN (nómina)");
        }

        public async Task<string> SendDocumentSyncAsync(string fileName, string signedXml, X509Certificate2 certificate, string environment)
        {
            var zipContentBase64 = await BuildZipBase64Async(fileName, signedXml);
            var body = $"<wcf:SendBillSync><wcf:fileName>{fileName}</wcf:fileName><wcf:contentFile>{zipContentBase64}</wcf:contentFile></wcf:SendBillSync>";
            return await SendSignedAsync("SendBillSync", body, certificate, environment, "Error comunicando con la DIAN (SendBillSync)");
        }

        public async Task<string> SendEventUpdateStatusAsync(string fileName, string signedXml, X509Certificate2 certificate, string environment)
        {
            var zipContentBase64 = await BuildZipBase64Async(fileName, signedXml);
            var body = $"<wcf:SendEventUpdateStatus><wcf:contentFile>{zipContentBase64}</wcf:contentFile></wcf:SendEventUpdateStatus>";
            return await SendSignedAsync("SendEventUpdateStatus", body, certificate, environment, "Error comunicando con la DIAN (evento RADIAN)");
        }

        public async Task<string> GetStatusAsync(string trackId, X509Certificate2 certificate, string environment)
        {
            var body = $"<wcf:GetStatus><wcf:trackId>{trackId}</wcf:trackId></wcf:GetStatus>";
            return await SendSignedAsync("GetStatus", body, certificate, environment);
        }

        public async Task<string> SendTestSetAsync(string zipFileName, string entryFileName, string signedXml, string testSetId, X509Certificate2 certificate, string environment)
        {
            var zipContentBase64 = await BuildZipBase64Async(entryFileName, signedXml);
            var body = $"<wcf:SendTestSetAsync><wcf:fileName>{zipFileName}</wcf:fileName><wcf:contentFile>{zipContentBase64}</wcf:contentFile><wcf:testSetId>{testSetId}</wcf:testSetId></wcf:SendTestSetAsync>";
            return await SendSignedAsync("SendTestSetAsync", body, certificate, environment, "Error comunicando con la DIAN (set de pruebas)");
        }

        public async Task<string> GetStatusZipAsync(string trackId, X509Certificate2 certificate, string environment)
        {
            var body = $"<wcf:GetStatusZip><wcf:trackId>{trackId}</wcf:trackId></wcf:GetStatusZip>";
            return await SendSignedAsync("GetStatusZip", body, certificate, environment);
        }

        // Numeral 7.15 — solo existe en producción en operación ("1"), no en habilitación.
        public async Task<string> GetNumberingRangeAsync(string accountCode, string accountCodeT, string softwareCode, X509Certificate2 certificate)
        {
            var body = $"<wcf:GetNumberingRange><wcf:accountCode>{accountCode}</wcf:accountCode><wcf:accountCodeT>{accountCodeT}</wcf:accountCodeT><wcf:softwareCode>{softwareCode}</wcf:softwareCode></wcf:GetNumberingRange>";
            return await SendSignedAsync("GetNumberingRange", body, certificate, "1", "Error consultando rangos de numeración en la DIAN");
        }

        // Firma SOLO el header <wsa:To> (no el Body ni el Timestamp) — confirmado contra la
        // librería de referencia real: es el patrón que usa wsHttpBinding con seguridad de mensaje
        // X.509 + WS-Addressing, distinto del patrón "firmar el Body" de basicHttpBinding que
        // habíamos asumido antes (y que producía el colgado sin respuesta).
        private void SignSoapEnvelope(XmlDocument soapEnvelope, X509Certificate2 certificate)
        {
            var nsmgr = new XmlNamespaceManager(soapEnvelope.NameTable);
            nsmgr.AddNamespace("soap", NsSoap12);
            nsmgr.AddNamespace("wsa", NsWsa);
            nsmgr.AddNamespace("wsse", NsWsse);

            var headerNode = soapEnvelope.SelectSingleNode("//wsse:Security", nsmgr) as XmlElement;
            var toNode = soapEnvelope.SelectSingleNode("//wsa:To", nsmgr) as XmlElement;
            if (headerNode == null || toNode == null) return;

            // wsa:To necesita un Id propio ("_to", igual que la librería de referencia) para que la
            // firma pueda referenciarlo. Se declara xmlns:wsu EXPLÍCITAMENTE en el propio nodo (no
            // solo heredado del root) — con solo la herencia, ComputeSignature() calcula el digest
            // usando la resolución de prefijo del documento en memoria, pero al re-parsear el sobre
            // completo (como lo hace un verificador remoto real) esa resolución puede diferir,
            // produciendo un digest distinto y una firma que no se autovalida.
            toNode.SetAttribute("xmlns:wsu", NsWsu);
            toNode.SetAttribute("Id", NsWsu, "_to");

            // 0. wsu:Timestamp — presente en el header de seguridad (igual que el ejemplo de
            // respuesta del propio anexo técnico, numeral 7.8.3) pero SIN firmar, igual que la
            // librería de referencia.
            var now = DateTime.UtcNow;
            var timestampNode = soapEnvelope.CreateElement("wsu", "Timestamp", NsWsu);
            timestampNode.SetAttribute("Id", NsWsu, "_ts");
            var createdNode = soapEnvelope.CreateElement("wsu", "Created", NsWsu);
            createdNode.InnerText = now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            var expiresNode = soapEnvelope.CreateElement("wsu", "Expires", NsWsu);
            expiresNode.InnerText = now.AddMinutes(5).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            timestampNode.AppendChild(createdNode);
            timestampNode.AppendChild(expiresNode);
            headerNode.AppendChild(timestampNode);

            // 1. Token X509 (Direct Reference — el certificado va embebido, no solo su huella)
            string certBase64 = Convert.ToBase64String(certificate.Export(X509ContentType.Cert));
            string tokenId = "X509-" + Guid.NewGuid();

            var bstNode = soapEnvelope.CreateElement("wsse", "BinarySecurityToken", NsWsse);
            bstNode.SetAttribute("EncodingType", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary");
            bstNode.SetAttribute("ValueType", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-x509-token-profile-1.0#X509v3");
            bstNode.SetAttribute("Id", NsWsu, tokenId);
            bstNode.InnerText = certBase64;
            headerNode.AppendChild(bstNode);

            // 2. Firmar únicamente wsa:To — C14N exclusiva PURA (sin InclusiveNamespaces), Signature
            // Algorithm rsa-sha256, Digest sha256: confirmado contra la propia guía oficial de la DIAN
            // (Caja de Herramientas FE v19, numeral 15.6 "Configurar WS-Security Signature",
            // Ilustración 5 — captura real de la configuración WS-Security Signature en SoapUI que la
            // propia DIAN documenta). Antes copiamos de una librería comunitaria una lista
            // InclusiveNamespaces="wsa soap wcf" que la guía oficial NO usa en ningún lado.
            var signedXml = new SignedXmlWithWsuId(soapEnvelope) { SigningKey = certificate.GetRSAPrivateKey() };

            var reference = new Reference { Uri = "#_to" };
            reference.AddTransform(new XmlDsigExcC14NTransform());
            reference.DigestMethod = "http://www.w3.org/2001/04/xmlenc#sha256";
            signedXml.AddReference(reference);
            signedXml.SignedInfo.SignatureMethod = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";
            signedXml.SignedInfo.CanonicalizationMethod = SignedXml.XmlDsigExcC14NTransformUrl;

            // 3. KeyInfo apunta al BinarySecurityToken (Direct Reference)
            var keyInfo = new KeyInfo();
            var strNode = soapEnvelope.CreateElement("wsse", "SecurityTokenReference", NsWsse);
            var refNode = soapEnvelope.CreateElement("wsse", "Reference", NsWsse);
            refNode.SetAttribute("URI", "#" + tokenId);
            refNode.SetAttribute("ValueType", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-x509-token-profile-1.0#X509v3");
            strNode.AppendChild(refNode);
            keyInfo.AddClause(new KeyInfoNode(strNode));
            signedXml.KeyInfo = keyInfo;

            signedXml.ComputeSignature();
            headerNode.AppendChild(soapEnvelope.ImportNode(signedXml.GetXml(), true));
        }
    }

    // SignedXml.GetIdElement de .NET solo reconoce atributos "Id"/"ID"/"id" sin namespace — nunca
    // "wsu:Id" (el que usa WS-Security/WS-Addressing para identificar wsa:To), así que sin esto
    // ComputeSignature() nunca resuelve la referencia y truena con "Malformed reference element."
    internal class SignedXmlWithWsuId : SignedXml
    {
        private const string WsuNamespace = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";

        public SignedXmlWithWsuId(XmlDocument document) : base(document) { }

        public override XmlElement? GetIdElement(XmlDocument? document, string idValue)
        {
            var elem = base.GetIdElement(document, idValue);
            if (elem != null) return elem;

            if (document == null) return null;
            var nsMgr = new XmlNamespaceManager(document.NameTable);
            nsMgr.AddNamespace("wsu", WsuNamespace);
            return document.SelectSingleNode($"//*[@wsu:Id='{idValue}']", nsMgr) as XmlElement;
        }
    }
}
