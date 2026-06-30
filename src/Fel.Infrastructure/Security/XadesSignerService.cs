using System;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using Fel.Core.Interfaces;

namespace Fel.Infrastructure.Security
{
    public class XadesSignerService : IXmlSignerService
    {
        public string SignXmlXadesEpes(string xmlString, X509Certificate2 certificate)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.PreserveWhitespace = true;
            xmlDoc.LoadXml(xmlString);

            // DIAN Requires the signature to be appended in a specific Extension element
            // For this boilerplate, we'll configure standard SignedXml
            SignedXml signedXml = new SignedXml(xmlDoc)
            {
                SigningKey = certificate.GetRSAPrivateKey()
            };

            Reference reference = new Reference
            {
                Uri = "" // Sign the whole document or specific ID
            };

            XmlDsigEnvelopedSignatureTransform env = new XmlDsigEnvelopedSignatureTransform();
            reference.AddTransform(env);

            signedXml.AddReference(reference);

            KeyInfo keyInfo = new KeyInfo();
            keyInfo.AddClause(new KeyInfoX509Data(certificate));
            signedXml.KeyInfo = keyInfo;

            // Compute signature
            signedXml.ComputeSignature();

            // Get the XML representation of the signature and save it to an XmlElement object
            XmlElement xmlDigitalSignature = signedXml.GetXml();

            // Append the element to the XML document
            // (Note: In production DIAN XAdES, this goes inside ext:ExtensionContent)
            xmlDoc.DocumentElement?.AppendChild(xmlDoc.ImportNode(xmlDigitalSignature, true));

            return xmlDoc.OuterXml;
        }
    }
}
