using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;
using Fel.Core.Interfaces;

namespace Fel.Infrastructure.Security
{
    // Firmador XAdES-EPES para la DIAN.
    //
    // POR QUE NO SE USA SignedXml.ComputeSignature()
    // ----------------------------------------------
    // El Anexo Tecnico 1.9, numeral 6.5.10 (fila DC03), exige canonicalizacion INCLUSIVA
    // (http://www.w3.org/TR/2001/REC-xml-c14n-20010315) sobre el <ds:SignedInfo>, y advierte que
    // "el incumplimiento de alguna de dichas caracteristicas es causa de rechazo por parte de la
    // DIAN". SignedXml de .NET no puede cumplirlo: canonicaliza el SignedInfo DESACOPLADO del
    // documento anfitrion (SignedInfo.GetXml(document) crea el elemento pero nunca lo cuelga del
    // arbol del <Invoice>). Al insertar despues la firma dentro del documento, el SignedInfo pasa a
    // heredar las declaraciones de namespace del <Invoice> (cac, cbc, ext, sts, xades, xades141, ds,
    // xsi) que no estaban al firmar. Medido sobre un documento real:
    //
    //     C14N inclusiva -> aislado 1802 bytes vs en documento 1910 bytes  => firma invalida
    //     C14N exclusiva -> aislado 1294 bytes vs en documento 1294 bytes  => firma estable
    //
    // Usar la exclusiva evita el problema pero incumple DC03. Por eso aqui se arma la firma a mano:
    // se inserta primero la estructura completa con los valores en blanco, y RECIEN ENTONCES se
    // calculan los digests y el SignatureValue canonicalizando cada elemento YA EN SU CONTEXTO REAL
    // dentro del documento. Asi la firma cumple el anexo y ademas es verificable por cualquier
    // validador externo.
    //
    // Tambien se respeta el resto del numeral 6.5.10: la primera referencia (URI="") lleva un unico
    // Transform enveloped-signature, y las referencias a KeyInfo (DC10-DC12) y a SignedProperties
    // (DC13-DC15) van SIN <ds:Transforms>, tal como los define el anexo.
    public class XadesSigner : IXmlSigner
    {
        private const string NsDs = "http://www.w3.org/2000/09/xmldsig#";
        private const string NsXades = "http://uri.etsi.org/01903/v1.3.2#";
        private const string NsExt = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";

        private const string C14NInclusive = "http://www.w3.org/TR/2001/REC-xml-c14n-20010315";
        private const string AlgRsaSha256 = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";
        private const string AlgSha256 = "http://www.w3.org/2001/04/xmlenc#sha256";
        private const string AlgEnveloped = "http://www.w3.org/2000/09/xmldsig#enveloped-signature";

        // Hash oficial de la politica de firma v2 publicado por la DIAN (anexo tecnico, numeral 10.10).
        private const string DianPolicyHash = "dMoMvtcG5aIzgYo0tIsSQeVJBDnUnfSOfBpxXrmor0Y=";
        private const string DianPolicyUrl = "https://facturaelectronica.dian.gov.co/politicadefirma/v2/politicadefirmav2.pdf";
        private const string DianPolicyDescription = "Política de firma para facturas electrónicas de la República de Colombia.";

        public string SignXml(string xmlContent, X509Certificate2 certificate, X509Certificate2Collection? certificateChain = null)
        {
            var xmlDoc = new XmlDocument { PreserveWhitespace = true };
            xmlDoc.LoadXml(xmlContent);

            var nsMgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsMgr.AddNamespace("ext", NsExt);
            nsMgr.AddNamespace("ds", NsDs);
            nsMgr.AddNamespace("xades", NsXades);

            // El generador UBL deja un <SignaturePlaceholder/> en la segunda ext:ExtensionContent
            // (ver BaseUblStrategy). Hay que quitarlo ANTES de calcular el digest del documento: la
            // transformacion enveloped-signature solo remueve el propio <ds:Signature> al verificar,
            // asi que si el digest se calculara con el placeholder puesto, el documento que la DIAN
            // reconstruye al validar nunca coincidiria.
            var extNodes = xmlDoc.SelectNodes("//ext:ExtensionContent", nsMgr);
            if (extNodes == null || extNodes.Count < 2)
                throw new InvalidOperationException("El XML no trae las dos ext:ExtensionContent que exige la DIAN (datos y firma).");
            var signatureHost = (XmlElement)extNodes[1]!;
            signatureHost.IsEmpty = false;
            signatureHost.InnerXml = "";

            string signatureId = "xmldsig-" + Guid.NewGuid();
            // Formato exacto del anexo (DC10): URI="#{UUID}-KeyInfo" en mayusculas, a diferencia de
            // "-signedprops" (minusculas) de la tercera referencia (DC13).
            string keyInfoId = signatureId + "-KeyInfo";
            string signedPropsId = signatureId + "-signedprops";

            // 1. Armar la estructura completa de la firma con los valores en blanco.
            signatureHost.AppendChild(BuildSignatureSkeleton(xmlDoc, certificate, certificateChain, signatureId, keyInfoId, signedPropsId));

            var signature = (XmlElement)xmlDoc.SelectSingleNode("//ds:Signature", nsMgr)!;
            var signedInfo = (XmlElement)signature.SelectSingleNode("ds:SignedInfo", nsMgr)!;
            var references = signedInfo.SelectNodes("ds:Reference", nsMgr)!;

            // 2. Primera referencia (URI=""): documento completo SIN el elemento <ds:Signature>, que
            //    es exactamente lo que reconstruye un validador al aplicar enveloped-signature.
            var docSinFirma = new XmlDocument { PreserveWhitespace = true };
            docSinFirma.LoadXml(xmlDoc.OuterXml);
            var nsMgrClon = new XmlNamespaceManager(docSinFirma.NameTable);
            nsMgrClon.AddNamespace("ds", NsDs);
            var firmaEnClon = docSinFirma.SelectSingleNode("//ds:Signature", nsMgrClon)!;
            firmaEnClon.ParentNode!.RemoveChild(firmaEnClon);
            SetDigest((XmlElement)references[0]!, nsMgr, Canonicalize(docSinFirma.OuterXml));

            // 3. Segunda y tercera referencia: sin Transforms declarados, asi que el digest va sobre
            //    la canonicalizacion inclusiva del elemento EN SU CONTEXTO dentro del documento.
            var keyInfo = (XmlElement)signature.SelectSingleNode("ds:KeyInfo", nsMgr)!;
            SetDigest((XmlElement)references[1]!, nsMgr, Canonicalize(OuterXmlConNamespacesHeredados(keyInfo)));

            var signedProps = (XmlElement)signature.SelectSingleNode("ds:Object/xades:QualifyingProperties/xades:SignedProperties", nsMgr)!;
            SetDigest((XmlElement)references[2]!, nsMgr, Canonicalize(OuterXmlConNamespacesHeredados(signedProps)));

            // 4. Firmar el SignedInfo ya completo, canonicalizado tambien en contexto.
            byte[] signedInfoC14N = Canonicalize(OuterXmlConNamespacesHeredados(signedInfo));
            using var rsa = certificate.GetRSAPrivateKey()
                ?? throw new InvalidOperationException("El certificado no expone una clave privada RSA utilizable.");
            byte[] firma = rsa.SignData(signedInfoC14N, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            signature.SelectSingleNode("ds:SignatureValue", nsMgr)!.InnerText = Convert.ToBase64String(firma);

            return xmlDoc.OuterXml;
        }

        private static void SetDigest(XmlElement reference, XmlNamespaceManager nsMgr, byte[] canonicalBytes)
        {
            using var sha = SHA256.Create();
            reference.SelectSingleNode("ds:DigestValue", nsMgr)!.InnerText =
                Convert.ToBase64String(sha.ComputeHash(canonicalBytes));
        }

        private static byte[] Canonicalize(string xml)
        {
            var transform = new XmlDsigC14NTransform();
            using var input = new MemoryStream(Encoding.UTF8.GetBytes(xml));
            transform.LoadInput(input);
            using var output = (MemoryStream)transform.GetOutput(typeof(Stream));
            return output.ToArray();
        }

        // Canonicalizar un subarbol con C14N inclusiva implica declarar en su elemento raiz TODOS los
        // namespaces que tiene en alcance, incluidos los heredados de sus ancestros. .NET no expone
        // esa vista, asi que se clona el elemento y se le copian las declaraciones de los ancestros
        // que el mismo no redefine (la declaracion mas cercana gana).
        private static string OuterXmlConNamespacesHeredados(XmlElement element)
        {
            // Se re-parsea el subarbol en un documento aparte: asi .NET ya escribe las declaraciones
            // de los prefijos que el propio subarbol usa, y solo queda agregar las heredadas.
            var temp = new XmlDocument { PreserveWhitespace = true };
            temp.LoadXml(element.OuterXml);
            var clone = temp.DocumentElement!;

            var yaDeclarados = new HashSet<string>();
            foreach (XmlAttribute attr in clone.Attributes)
            {
                string? nombre = NombreDeDeclaracion(attr);
                if (nombre != null) yaDeclarados.Add(nombre);
            }

            for (XmlNode? nodo = element.ParentNode; nodo is XmlElement ancestro; nodo = nodo.ParentNode)
            {
                foreach (XmlAttribute attr in ancestro.Attributes)
                {
                    string? nombre = NombreDeDeclaracion(attr);
                    if (nombre != null && yaDeclarados.Add(nombre))
                        clone.SetAttribute(nombre, attr.Value);
                }
            }

            return clone.OuterXml;
        }

        private static string? NombreDeDeclaracion(XmlAttribute attr)
        {
            if (attr.Prefix == "xmlns") return "xmlns:" + attr.LocalName;
            if (attr.Name == "xmlns") return "xmlns";
            return null;
        }

        private static XmlElement BuildSignatureSkeleton(
            XmlDocument doc,
            X509Certificate2 certificate,
            X509Certificate2Collection? certificateChain,
            string signatureId,
            string keyInfoId,
            string signedPropsId)
        {
            string certBase64 = Convert.ToBase64String(certificate.RawData);
            string signingTime = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-5)).ToString("yyyy-MM-ddTHH:mm:ssK");

            var certsXml = new StringBuilder();
            foreach (var cert in OrdenarCadena(certificate, certificateChain))
            {
                var (thumbprint, issuer, serial) = DatosDelCertificado(cert);
                certsXml.Append(
                    "<xades:Cert>" +
                        "<xades:CertDigest>" +
                            $"<ds:DigestMethod Algorithm=\"{AlgSha256}\"/>" +
                            $"<ds:DigestValue>{thumbprint}</ds:DigestValue>" +
                        "</xades:CertDigest>" +
                        "<xades:IssuerSerial>" +
                            $"<ds:X509IssuerName>{Escapar(issuer)}</ds:X509IssuerName>" +
                            $"<ds:X509SerialNumber>{serial}</ds:X509SerialNumber>" +
                        "</xades:IssuerSerial>" +
                    "</xades:Cert>");
            }

            // Se arma en una sola linea a proposito: cualquier espacio o salto entre elementos forma
            // parte de la canonicalizacion, y no hay razon para introducir ruido que despues haya que
            // preservar intacto entre la firma y la validacion.
            string xml =
                $"<ds:Signature xmlns:ds=\"{NsDs}\" xmlns:xades=\"{NsXades}\" Id=\"{signatureId}\">" +
                    "<ds:SignedInfo>" +
                        $"<ds:CanonicalizationMethod Algorithm=\"{C14NInclusive}\"/>" +
                        $"<ds:SignatureMethod Algorithm=\"{AlgRsaSha256}\"/>" +
                        $"<ds:Reference Id=\"{signatureId}-ref0\" URI=\"\">" +
                            $"<ds:Transforms><ds:Transform Algorithm=\"{AlgEnveloped}\"/></ds:Transforms>" +
                            $"<ds:DigestMethod Algorithm=\"{AlgSha256}\"/>" +
                            "<ds:DigestValue></ds:DigestValue>" +
                        "</ds:Reference>" +
                        $"<ds:Reference URI=\"#{keyInfoId}\">" +
                            $"<ds:DigestMethod Algorithm=\"{AlgSha256}\"/>" +
                            "<ds:DigestValue></ds:DigestValue>" +
                        "</ds:Reference>" +
                        $"<ds:Reference Type=\"http://uri.etsi.org/01903#SignedProperties\" URI=\"#{signedPropsId}\">" +
                            $"<ds:DigestMethod Algorithm=\"{AlgSha256}\"/>" +
                            "<ds:DigestValue></ds:DigestValue>" +
                        "</ds:Reference>" +
                    "</ds:SignedInfo>" +
                    $"<ds:SignatureValue Id=\"{signatureId}-sigvalue\"></ds:SignatureValue>" +
                    $"<ds:KeyInfo Id=\"{keyInfoId}\">" +
                        $"<ds:X509Data><ds:X509Certificate>{certBase64}</ds:X509Certificate></ds:X509Data>" +
                    "</ds:KeyInfo>" +
                    "<ds:Object>" +
                        $"<xades:QualifyingProperties Target=\"#{signatureId}\">" +
                            $"<xades:SignedProperties Id=\"{signedPropsId}\">" +
                                "<xades:SignedSignatureProperties>" +
                                    $"<xades:SigningTime>{signingTime}</xades:SigningTime>" +
                                    $"<xades:SigningCertificate>{certsXml}</xades:SigningCertificate>" +
                                    "<xades:SignaturePolicyIdentifier>" +
                                        "<xades:SignaturePolicyId>" +
                                            "<xades:SigPolicyId>" +
                                                $"<xades:Identifier>{DianPolicyUrl}</xades:Identifier>" +
                                                $"<xades:Description>{Escapar(DianPolicyDescription)}</xades:Description>" +
                                            "</xades:SigPolicyId>" +
                                            "<xades:SigPolicyHash>" +
                                                $"<ds:DigestMethod Algorithm=\"{AlgSha256}\"/>" +
                                                $"<ds:DigestValue>{DianPolicyHash}</ds:DigestValue>" +
                                            "</xades:SigPolicyHash>" +
                                        "</xades:SignaturePolicyId>" +
                                    "</xades:SignaturePolicyIdentifier>" +
                                    "<xades:SignerRole><xades:ClaimedRoles><xades:ClaimedRole>supplier</xades:ClaimedRole></xades:ClaimedRoles></xades:SignerRole>" +
                                "</xades:SignedSignatureProperties>" +
                            "</xades:SignedProperties>" +
                        "</xades:QualifyingProperties>" +
                    "</ds:Object>" +
                "</ds:Signature>";

            var fragmento = new XmlDocument { PreserveWhitespace = true };
            fragmento.LoadXml(xml);
            return (XmlElement)doc.ImportNode(fragmento.DocumentElement!, true);
        }

        // xades:SigningCertificate lleva la cadena de confianza: firmante, raiz e intermedio.
        private static List<X509Certificate2> OrdenarCadena(X509Certificate2 certificate, X509Certificate2Collection? chain)
        {
            var resultado = new List<X509Certificate2> { certificate };
            if (chain == null) return resultado;

            X509Certificate2? raiz = null;
            X509Certificate2? intermedio = null;
            foreach (X509Certificate2 cert in chain)
            {
                if (cert.Thumbprint == certificate.Thumbprint) continue;
                if (cert.Subject == cert.Issuer) raiz = cert;
                else intermedio = cert;
            }
            if (raiz != null) resultado.Add(raiz);
            if (intermedio != null) resultado.Add(intermedio);
            return resultado;
        }

        private static (string thumbprint, string issuer, string serial) DatosDelCertificado(X509Certificate2 cert)
        {
            string thumbprint = Convert.ToBase64String(cert.GetCertHash(HashAlgorithmName.SHA256));

            // ds:X509IssuerName en formato RFC2253 (el RDN mas especifico primero: "CN=..." al
            // inicio, "C=CO" al final). cert.Issuer usa el orden de almacenamiento del certificado,
            // que es el inverso, y X500DistinguishedNameFlags.Reversed no tiene efecto en .NET sobre
            // Linux, asi que se invierten los RDN a mano.
            string issuer = string.Join(", ", cert.Issuer.Split(", ").Reverse());

            // GetSerialNumber() entrega los bytes en little-endian, que es justo el orden que espera
            // el constructor de BigInteger. Antes habia un Array.Reverse() aqui que los pasaba a
            // big-endian y BigInteger los releia como little-endian, publicando el serial con los
            // bytes invertidos y en negativo (para 2FC69D6F662BD2C3 = 3442612066952401603 saliamos
            // con -4336355772245096913). El anexo (DC32) exige el serial real del certificado.
            string serial = new System.Numerics.BigInteger(cert.GetSerialNumber(), isUnsigned: true, isBigEndian: false).ToString();

            return (thumbprint, issuer, serial);
        }

        private static string Escapar(string valor) =>
            valor.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
