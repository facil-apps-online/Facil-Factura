using System;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using Fel.Core.Interfaces;

namespace Fel.Infrastructure.Security
{
    public class XadesSigner : IXmlSigner
    {
        public string SignXml(string xmlContent, X509Certificate2 certificate)
        {
            // Cargar el XML en un XmlDocument preserving whitespace
            XmlDocument xmlDoc = new XmlDocument { PreserveWhitespace = true };
            xmlDoc.LoadXml(xmlContent);

            // 1. Crear el objeto SignedXml
            SignedXml signedXml = new SignedXml(xmlDoc) { SigningKey = certificate.GetRSAPrivateKey() };
            signedXml.Signature.Id = "Signature-Update-ID"; // Normalmente un Guid o id único
            signedXml.SignedInfo.SignatureMethod = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";

            // 2. Referencia al documento entero
            Reference reference = new Reference { Uri = "" };
            reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
            reference.DigestMethod = "http://www.w3.org/2001/04/xmlenc#sha256";
            signedXml.AddReference(reference);

            // 3. KeyInfo (Certificado público)
            KeyInfo keyInfo = new KeyInfo();
            keyInfo.AddClause(new KeyInfoX509Data(certificate));
            signedXml.KeyInfo = keyInfo;

            // 4. Agregar Propiedades XAdES-EPES exigidas por la DIAN
            string certThumbprint = Convert.ToBase64String(certificate.GetCertHash(System.Security.Cryptography.HashAlgorithmName.SHA256));
            // A veces Issuer name en X509 incluye CN=..., OU=..., hay que pasarlo tal cual
            string certIssuer = certificate.Issuer; 
            // Serial en decimal (no hexadecimal), BigInteger parse a veces es necesario pero para simplificar:
            var serialBytes = certificate.GetSerialNumber();
            Array.Reverse(serialBytes); // SerialNumber is little-endian in .NET, XMLDSIG requires big-endian
            string certSerial = new System.Numerics.BigInteger(serialBytes).ToString(); 

            // Política oficial DIAN (Hash estático publicado en el anexo técnico)
            string dianPolicyHash = "dMoMvtcG5aIzgYo0tIsSQeVJBDnUnfSOfBpxXrmor0Y=";

            string xadesXml = $@"
                <xades:QualifyingProperties xmlns:xades=""http://uri.etsi.org/01903/v1.3.2#"" Target=""#{signedXml.Signature.Id}"">
                    <xades:SignedProperties Id=""SignedProperties-ID"">
                        <xades:SignedSignatureProperties>
                            <xades:SigningTime>{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}</xades:SigningTime>
                            <xades:SigningCertificate>
                                <xades:Cert>
                                    <xades:CertDigest>
                                        <ds:DigestMethod Algorithm=""http://www.w3.org/2001/04/xmlenc#sha256"" xmlns:ds=""http://www.w3.org/2000/09/xmldsig#"" />
                                        <ds:DigestValue xmlns:ds=""http://www.w3.org/2000/09/xmldsig#"">{certThumbprint}</ds:DigestValue>
                                    </xades:CertDigest>
                                    <xades:IssuerSerial>
                                        <ds:X509IssuerName xmlns:ds=""http://www.w3.org/2000/09/xmldsig#"">{certIssuer}</ds:X509IssuerName>
                                        <ds:X509SerialNumber xmlns:ds=""http://www.w3.org/2000/09/xmldsig#"">{certSerial}</ds:X509SerialNumber>
                                    </xades:IssuerSerial>
                                </xades:Cert>
                            </xades:SigningCertificate>
                            <xades:SignaturePolicyIdentifier>
                                <xades:SignaturePolicyId>
                                    <xades:SigPolicyId>
                                        <xades:Identifier>https://facturaelectronica.dian.gov.co/politicadefirma/v2/politicadefirmav2.pdf</xades:Identifier>
                                    </xades:SigPolicyId>
                                    <xades:SigPolicyHash>
                                        <ds:DigestMethod Algorithm=""http://www.w3.org/2001/04/xmlenc#sha256"" xmlns:ds=""http://www.w3.org/2000/09/xmldsig#"" />
                                        <ds:DigestValue xmlns:ds=""http://www.w3.org/2000/09/xmldsig#"">{dianPolicyHash}</ds:DigestValue>
                                    </xades:SigPolicyHash>
                                </xades:SignaturePolicyId>
                            </xades:SignaturePolicyIdentifier>
                            <xades:SignerRole>
                                <xades:ClaimedRoles>
                                    <xades:ClaimedRole>supplier</xades:ClaimedRole>
                                </xades:ClaimedRoles>
                            </xades:SignerRole>
                        </xades:SignedSignatureProperties>
                    </xades:SignedProperties>
                </xades:QualifyingProperties>";

            XmlDocument tempDoc = new XmlDocument();
            tempDoc.LoadXml(xadesXml);

            DataObject dataObject = new DataObject();
            dataObject.Data = tempDoc.DocumentElement.SelectNodes(".");
            signedXml.AddObject(dataObject);

            // Referencia al objeto SignedProperties para prevenir alteraciones
            Reference refXades = new Reference { Uri = "#SignedProperties-ID" };
            refXades.DigestMethod = "http://www.w3.org/2001/04/xmlenc#sha256";
            signedXml.AddReference(refXades);

            // 5. Calcular firma
            signedXml.ComputeSignature();
            XmlElement xmlDigitalSignature = signedXml.GetXml();

            // Insertar la firma en la segunda UBLExtension generada (según Anexo V1.8)
            XmlNamespaceManager nsMgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsMgr.AddNamespace("ext", "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2");
            
            XmlNodeList extNodes = xmlDoc.SelectNodes("//ext:ExtensionContent", nsMgr);
            if (extNodes != null && extNodes.Count > 1)
            {
                extNodes[1].InnerXml = ""; // Limpiar placeholder de firma si existía
                extNodes[1].AppendChild(xmlDoc.ImportNode(xmlDigitalSignature, true));
            }

            return xmlDoc.OuterXml;
        }
    }
}
