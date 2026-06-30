using System;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Fel.Core.Interfaces;

namespace Fel.Infrastructure.Dian
{
    public class DianSoapClient : IDianSoapClient
    {
        private readonly HttpClient _httpClient;
        private const string DianUrl = "https://vpfe-hab.dian.gov.co/WcfDianCustomerServices.svc";

        public DianSoapClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> SendBillAsync(string fileName, string xmlContentBase64, X509Certificate2 certificate)
        {
            // Construcción profesional del SOAP Envelope en XmlDocument para firmarlo con WS-Security
            var soapEnvelope = new XmlDocument();
            soapEnvelope.PreserveWhitespace = true;
            soapEnvelope.LoadXml($@"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:wcf=""http://wcf.dian.colombia"">
                <soapenv:Header>
                    <wsse:Security xmlns:wsse=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd"" soapenv:mustUnderstand=""1"">
                        <!-- Placeholder para la firma WS-Security -->
                    </wsse:Security>
                </soapenv:Header>
                <soapenv:Body wsu:Id=""SoapBody"" xmlns:wsu=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd"">
                    <wcf:SendBillAsync>
                        <wcf:fileName>{fileName}</wcf:fileName>
                        <wcf:contentFile>{xmlContentBase64}</wcf:contentFile>
                    </wcf:SendBillAsync>
                </soapenv:Body>
            </soapenv:Envelope>");

            SignSoapEnvelope(soapEnvelope, certificate); // Firma del nodo Body inyectando BinarySecurityToken

            var content = new StringContent(soapEnvelope.OuterXml, Encoding.UTF8, "text/xml");
            content.Headers.Add("SOAPAction", "http://wcf.dian.colombia/IWcfDianCustomerServices/SendBillAsync");

            var response = await _httpClient.PostAsync(DianUrl, content);
            string responseString = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Error comunicando con la DIAN: {response.StatusCode} - {responseString}");

            return responseString;
        }

        private void SignSoapEnvelope(XmlDocument soapEnvelope, X509Certificate2 certificate)
        {
            var nsmgr = new XmlNamespaceManager(soapEnvelope.NameTable);
            nsmgr.AddNamespace("soapenv", "http://schemas.xmlsoap.org/soap/envelope/");
            nsmgr.AddNamespace("wsse", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd");

            var headerNode = soapEnvelope.SelectSingleNode("//wsse:Security", nsmgr) as XmlElement;
            if (headerNode == null) return;

            // 1. Crear Token X509
            string certBase64 = Convert.ToBase64String(certificate.Export(X509ContentType.Cert));
            string tokenId = "X509-" + Guid.NewGuid().ToString();
            
            var bstNode = soapEnvelope.CreateElement("wsse", "BinarySecurityToken", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd");
            bstNode.SetAttribute("EncodingType", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary");
            bstNode.SetAttribute("ValueType", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-x509-token-profile-1.0#X509v3");
            bstNode.SetAttribute("Id", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd", tokenId);
            bstNode.InnerText = certBase64;
            headerNode.AppendChild(bstNode);

            // 2. Configurar SignedXml para firmar el Body
            var signedXml = new SignedXml(soapEnvelope) { SigningKey = certificate.GetRSAPrivateKey() };
            
            var reference = new Reference { Uri = "#SoapBody" };
            // WCF WS-Security exige Excluive C14N
            reference.AddTransform(new XmlDsigExcC14NTransform()); 
            signedXml.AddReference(reference);

            // 3. KeyInfo apuna al BinarySecurityToken
            var keyInfo = new KeyInfo();
            var strNode = soapEnvelope.CreateElement("wsse", "SecurityTokenReference", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd");
            var refNode = soapEnvelope.CreateElement("wsse", "Reference", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd");
            refNode.SetAttribute("URI", "#" + tokenId);
            refNode.SetAttribute("ValueType", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-x509-token-profile-1.0#X509v3");
            strNode.AppendChild(refNode);

            keyInfo.AddClause(new KeyInfoNode(strNode));
            signedXml.KeyInfo = keyInfo;

            // Compute Signature y adjuntar
            signedXml.ComputeSignature();
            headerNode.AppendChild(soapEnvelope.ImportNode(signedXml.GetXml(), true));
        }

        public async Task<string> GetStatusAsync(string trackId, X509Certificate2 certificate)
        {
            var soapEnvelope = new XmlDocument();
            soapEnvelope.LoadXml($@"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:wcf=""http://wcf.dian.colombia"">
                <soapenv:Header/>
                <soapenv:Body>
                    <wcf:GetStatus>
                        <wcf:trackId>{trackId}</wcf:trackId>
                    </wcf:GetStatus>
                </soapenv:Body>
            </soapenv:Envelope>");

            var content = new StringContent(soapEnvelope.OuterXml, Encoding.UTF8, "text/xml");
            content.Headers.Add("SOAPAction", "http://wcf.dian.colombia/IWcfDianCustomerServices/GetStatus");

            var response = await _httpClient.PostAsync(DianUrl, content);
            return await response.Content.ReadAsStringAsync();
        }
    }
}
