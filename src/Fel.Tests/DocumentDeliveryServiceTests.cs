using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Fel.Tests
{
    // Las validaciones del envío por correo de un documento aprobado, que se resuelven antes de tocar el correo o el proveedor.
    public class DocumentDeliveryServiceTests
    {
        private sealed class Fixture
        {
            public FelDbContext Db { get; }
            public Client Client { get; } = new() { Id = Guid.NewGuid(), TaxId = "900123456" };
            public Document Document { get; }
            public DocumentDeliveryService Service { get; }

            public Fixture(string status = "APPROVED", string? customerEmail = "cliente@acme.co")
            {
                Db = new FelDbContext(new DbContextOptionsBuilder<FelDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
                Db.Clients.Add(Client);
                var customer = new Customer { Id = Guid.NewGuid(), ClientId = Client.Id, Email = customerEmail ?? string.Empty };
                Document = new Document { Id = Guid.NewGuid(), ClientId = Client.Id, CustomerId = customer.Id, TypeCode = "FE-STD", Status = status, Prefix = "SETT", Number = "1" };
                Db.Customers.Add(customer);
                Db.Documents.Add(Document);
                Db.SaveChanges();
                // Los demás servicios no se tocan en los caminos que estas pruebas recorren.
                Service = new DocumentDeliveryService(Db, null!, null!, null!, null!, null!);
            }
        }

        private static DocumentSendRequest Request(string? to = null, params string[] cc) =>
            new(to, cc, null, new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }, Array.Empty<(string, byte[])>());

        [Fact]
        public async Task Only_approved_documents_are_sent()
        {
            var f = new Fixture(status: "REJECTED");
            var result = await f.Service.SendAsync(f.Document.Id, f.Client.Id, Request());

            Assert.False(result.Success);
            Assert.Equal("DOCUMENT_NOT_APPROVED", result.Code);
        }

        [Fact]
        public async Task A_document_of_another_issuer_is_not_found()
        {
            var f = new Fixture();
            var result = await f.Service.SendAsync(f.Document.Id, Guid.NewGuid(), Request());

            Assert.Equal("DOCUMENT_NOT_FOUND", result.Code);
        }

        [Fact]
        public async Task Without_a_recipient_and_without_the_customer_email_it_says_so()
        {
            var f = new Fixture(customerEmail: null);
            var result = await f.Service.SendAsync(f.Document.Id, f.Client.Id, Request());

            Assert.Equal("NO_RECIPIENT", result.Code);
        }

        [Theory]
        [InlineData("sin-arroba")]
        [InlineData("a@b")]
        [InlineData("con espacio@acme.co")]
        public async Task An_invalid_email_is_rejected(string to)
        {
            var f = new Fixture();
            var result = await f.Service.SendAsync(f.Document.Id, f.Client.Id, Request(to));

            Assert.Equal("INVALID_EMAIL", result.Code);
        }

        [Fact]
        public async Task More_than_three_copies_are_rejected()
        {
            var f = new Fixture();
            var result = await f.Service.SendAsync(f.Document.Id, f.Client.Id, Request("a@acme.co", "b@acme.co", "c@acme.co", "d@acme.co", "e@acme.co"));

            Assert.Equal("TOO_MANY_RECIPIENTS", result.Code);
        }
    }
}
