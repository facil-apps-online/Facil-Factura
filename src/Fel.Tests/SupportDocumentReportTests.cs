using Fel.Core.Entities;
using Fel.Infrastructure.Dataico;
using Fel.Infrastructure.Services;

namespace Fel.Tests
{
    // Representación gráfica del documento soporte: un documento soporte no tiene adquirente, pero los diseños estándar leen los campos Adquirente*;
    // se llenan con los datos del proveedor, completos como en la factura (documento con su dígito de verificación y país).
    public class SupportDocumentReportTests
    {
        private static Dictionary<string, object?> Report(Customer? provider)
        {
            var integrator = new Integrator { Id = Guid.NewGuid(), Code = "NATIVE", Name = "Nativo", Kind = IntegratorKind.DirectDian };
            var client = new Client { Id = Guid.NewGuid(), TaxId = "900303194", CompanyName = "OZEKI SAS", IntegratorId = integrator.Id, Integrator = integrator };
            var document = new Document
            {
                Id = Guid.NewGuid(), ClientId = client.Id, TypeCode = "DS", Prefix = "DSOZ", Number = "505", Status = "APPROVED", IssueDate = new DateTime(2026, 10, 28),
                CreatedAt = new DateTime(2026, 10, 28, 23, 0, 0, DateTimeKind.Utc),
                Subtotal = 100000m, TotalAmount = 100000m
            };
            var items = new List<DocumentItem> { new() { Id = Guid.NewGuid(), DocumentId = document.Id, Code = "S1", Name = "Servicio", Quantity = 1, UnitPrice = 100000m, TotalAmount = 100000m } };
            return SupportDocumentReportDataMapper.Build(document, provider, client, EmitterLocation.For(client, null), null, items);
        }

        private static Customer Provider(string identificationType, string number, string? verificationDigit, string? country = "CO") => new()
        {
            Id = Guid.NewGuid(), Name = "PROVEEDOR EJEMPLO", IdentificationType = identificationType, IdentificationNumber = number, VerificationDigit = verificationDigit ?? string.Empty,
            Address = "Calle 1 # 2-3", CityName = "Bogotá", Phone = "3000000000", Email = "p@correo.co", CountryCode = country
        };

        private static Dictionary<string, object?> Document(Dictionary<string, object?> report) => (Dictionary<string, object?>)report["Documento"]!;

        [Fact]
        public void The_provider_nit_is_sent_under_the_acquirer_fields_with_its_check_digit()
        {
            var doc = Document(Report(Provider("31", "800987654", "4")));

            Assert.Equal("NIT 800987654-4", doc["AdquirenteDocumento"]);
            Assert.Equal("800987654", doc["AdquirenteIdentificacion"]);
            Assert.Equal("PROVEEDOR EJEMPLO", doc["AdquirenteNombre"]);
            Assert.Equal("CO", doc["AdquirentePais"]);
            // Los campos de proveedor siguen para los diseños propios del documento soporte.
            Assert.Equal("800987654", doc["ProveedorIdentificacion"]);
            Assert.Equal("PROVEEDOR EJEMPLO", doc["ProveedorNombre"]);
        }

        [Fact]
        public void A_nit_without_a_stored_check_digit_gets_the_calculated_one()
        {
            var doc = Document(Report(Provider("31", "800987654", null)));

            Assert.Equal("NIT 800987654-4", doc["AdquirenteDocumento"]);
        }

        [Fact]
        public void A_citizen_id_has_no_check_digit()
        {
            var doc = Document(Report(Provider("13", "1020304050", null)));

            Assert.Equal("CC 1020304050", doc["AdquirenteDocumento"]);
        }

        [Fact]
        public void Without_a_provider_the_acquirer_document_is_empty_instead_of_failing()
        {
            var doc = Document(Report(null));

            Assert.Null(doc["AdquirenteDocumento"]);
            Assert.Null(doc["AdquirentePais"]);
        }
    }
}
