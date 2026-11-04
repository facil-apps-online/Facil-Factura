using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Fel.Infrastructure.MinSalud;

namespace Fel.Tests
{
    // Confianza en el certificado autofirmado del proxy del MUV por huella SHA-256: solo el que coincide; sin huella configurada no se abre la puerta.
    public class MuvCertificatePinningTests
    {
        private static X509Certificate2 SelfSigned(string name)
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest($"CN={name}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
        }

        private static string Fingerprint(X509Certificate2 certificate) => Convert.ToHexString(certificate.GetCertHash(HashAlgorithmName.SHA256));

        // Los errores que .NET reporta para un certificado autofirmado.
        private const SslPolicyErrors SelfSignedErrors = SslPolicyErrors.RemoteCertificateChainErrors;

        [Fact]
        public void A_self_signed_certificate_is_trusted_only_when_its_fingerprint_is_configured()
        {
            using var certificate = SelfSigned("fevrips-api");
            var pins = MuvCertificatePinning.ParsePins(Fingerprint(certificate));

            Assert.True(MuvCertificatePinning.IsTrusted(certificate, SelfSignedErrors, pins));
        }

        [Fact]
        public void Another_certificate_is_rejected_even_if_it_looks_the_same()
        {
            using var pinned = SelfSigned("fevrips-api");
            using var other = SelfSigned("fevrips-api");
            var pins = MuvCertificatePinning.ParsePins(Fingerprint(pinned));

            Assert.False(MuvCertificatePinning.IsTrusted(other, SelfSignedErrors, pins));
        }

        [Fact]
        public void Without_a_configured_fingerprint_a_self_signed_certificate_fails()
        {
            using var certificate = SelfSigned("fevrips-api");

            Assert.False(MuvCertificatePinning.IsTrusted(certificate, SelfSignedErrors, MuvCertificatePinning.ParsePins(null)));
            Assert.False(MuvCertificatePinning.IsTrusted(certificate, SelfSignedErrors, MuvCertificatePinning.ParsePins("   ")));
        }

        [Fact]
        public void Without_a_configured_fingerprint_a_normally_valid_certificate_is_accepted()
        {
            using var certificate = SelfSigned("valido");

            Assert.True(MuvCertificatePinning.IsTrusted(certificate, SslPolicyErrors.None, MuvCertificatePinning.ParsePins(null)));
        }

        [Fact]
        public void With_a_configured_fingerprint_a_missing_certificate_is_rejected()
        {
            var pins = MuvCertificatePinning.ParsePins(new string('A', 64));

            Assert.False(MuvCertificatePinning.IsTrusted(null, SslPolicyErrors.RemoteCertificateNotAvailable, pins));
        }

        [Fact]
        public void The_fingerprint_is_accepted_with_colons_in_lowercase_and_in_a_list()
        {
            using var certificate = SelfSigned("fevrips-api");
            var hex = Fingerprint(certificate);
            var withColons = string.Join(":", Enumerable.Range(0, 32).Select(i => hex.Substring(i * 2, 2))).ToLowerInvariant();

            Assert.True(MuvCertificatePinning.IsTrusted(certificate, SelfSignedErrors, MuvCertificatePinning.ParsePins(withColons)));
            Assert.True(MuvCertificatePinning.IsTrusted(certificate, SelfSignedErrors, MuvCertificatePinning.ParsePins($"{new string('B', 64)}, {withColons}")));
        }

        [Fact]
        public void Values_that_are_not_a_sha256_fingerprint_are_ignored()
        {
            var pins = MuvCertificatePinning.ParsePins("123, zzzz, " + new string('C', 64));

            Assert.Single(pins);
            Assert.Contains(new string('C', 64), pins);
        }

        // La huella que hoy usa producción (la del proxy del MUV en Bogotá) se normaliza a los 64 caracteres hexadecimales de un SHA-256.
        [Fact]
        public void The_production_fingerprint_is_a_valid_pin()
        {
            const string production = "09:21:95:95:E7:7E:8E:85:C8:75:56:9A:19:EA:76:CE:34:CE:64:06:80:5E:88:BD:A0:06:70:46:F6:AB:B6:5C";

            var pins = MuvCertificatePinning.ParsePins(production);

            Assert.Single(pins);
            Assert.Equal("09219595E77E8E85C875569A19EA76CE34CE6406805E88BDA0067046F6ABB65C", pins.Single());
        }
    }
}
