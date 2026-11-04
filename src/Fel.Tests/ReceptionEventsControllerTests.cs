using System.IO.Compression;
using System.Text;
using Fel.Api.Integration.Controllers;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Tests
{
    // Los endpoints de documentos recibidos y eventos RADIAN de la API: ingreso del XML/ZIP, filtro por emisor y sucursal,
    // y la traducción del resultado del motor de eventos a códigos HTTP. El motor se reemplaza por uno falso.
    public class ReceptionEventsControllerTests
    {
        private static readonly Guid BranchId = Guid.NewGuid();

        private sealed class FakeEventService : IReceptionEventService
        {
            public string StatusToReturn { get; set; } = "SENT";
            public List<string> Triggered { get; } = new();
            public int AutoCalls { get; private set; }

            public Task<ReceivedDocumentEvent> TriggerEventAsync(ReceivedDocument document, Client client, string eventCode)
            {
                Triggered.Add(eventCode);
                return Task.FromResult(new ReceivedDocumentEvent
                {
                    Id = Guid.NewGuid(), ReceivedDocumentId = document.Id, EventCode = eventCode, Status = StatusToReturn,
                    Cude = StatusToReturn == "SENT" ? "cude-" + eventCode : string.Empty, SentAt = DateTime.UtcNow,
                    DianResponseMessage = StatusToReturn == "SENT" ? "Procesado correctamente" : "Primero debe quedar registrado el acuse de recibo (030)."
                });
            }

            public Task TriggerEnabledEventsAsync(ReceivedDocument document, Client client)
            {
                AutoCalls++;
                return Task.CompletedTask;
            }
        }

        private sealed class Fixture
        {
            public FelDbContext Db { get; }
            public Client Client { get; }
            public FakeEventService Events { get; } = new();
            public ReceptionEventsController Controller { get; }

            public Fixture(Guid? clientId = null, Guid? branchId = null)
            {
                var options = new DbContextOptionsBuilder<FelDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
                Db = new FelDbContext(options);
                var integrator = new Integrator { Id = Guid.NewGuid(), Code = "NATIVE", Name = "Directo" };
                Db.Integrators.Add(integrator);
                Client = new Client { Id = clientId ?? Guid.NewGuid(), TaxId = "900123456", IntegratorId = integrator.Id };
                Db.Clients.Add(Client);
                Db.SaveChanges();
                Controller = As(Client.Id, branchId ?? BranchId);
            }

            public ReceptionEventsController As(Guid clientId, Guid branchId)
            {
                var http = new DefaultHttpContext();
                http.Items["ClientId"] = clientId.ToString();
                http.Items["BranchId"] = branchId.ToString();
                return new ReceptionEventsController(Db, Events, null!, null!) { ControllerContext = new ControllerContext { HttpContext = http } };
            }
        }

        private static string InvoiceXml(string uuid, string number, bool credit = true) => $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Invoice xmlns=""urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"" xmlns:cbc=""urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"" xmlns:cac=""urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"">
  <cbc:ID>{number}</cbc:ID>
  <cbc:UUID schemeName=""CUFE-SHA384"">{uuid}</cbc:UUID>
  <cbc:IssueDate>2026-10-20</cbc:IssueDate>
  <cbc:InvoiceTypeCode>01</cbc:InvoiceTypeCode>
  <cac:AccountingSupplierParty>
    <cac:Party>
      <cac:PartyTaxScheme><cbc:RegistrationName>PROVEEDOR SAS</cbc:RegistrationName><cbc:CompanyID schemeName=""31"">800111222</cbc:CompanyID></cac:PartyTaxScheme>
    </cac:Party>
  </cac:AccountingSupplierParty>
  <cac:PaymentMeans><cbc:ID>{(credit ? "2" : "1")}</cbc:ID><cbc:PaymentMeansCode>ZZZ</cbc:PaymentMeansCode></cac:PaymentMeans>
  <cac:LegalMonetaryTotal><cbc:PayableAmount currencyID=""COP"">119000.00</cbc:PayableAmount></cac:LegalMonetaryTotal>
</Invoice>";

        private static ReceptionEventsController.ReceivedDocumentUploadRequest Upload(string xml, string? fileName = null) =>
            new() { File = Convert.ToBase64String(Encoding.UTF8.GetBytes(xml)), FileName = fileName };

        [Fact]
        public async Task Uploading_an_xml_stores_the_document_for_the_key_branch_and_fires_the_automatic_events()
        {
            var f = new Fixture();

            var result = await f.Controller.Upload(Upload(InvoiceXml("cufe-1", "FAC100")));

            Assert.IsType<CreatedResult>(result);
            var stored = await f.Db.ReceivedDocuments.SingleAsync();
            Assert.Equal(f.Client.Id, stored.ClientId);
            Assert.Equal(BranchId, stored.BranchId);
            Assert.Equal("Api", stored.SourceType);
            Assert.Equal("cufe-1", stored.Cufe);
            Assert.Equal("FAC100", stored.DocumentId);
            Assert.Equal(119000m, stored.TotalAmount);
            Assert.True(stored.IsCredit);
            Assert.Equal(1, f.Events.AutoCalls);
        }

        [Fact]
        public async Task Uploading_a_zip_reads_the_xml_and_keeps_the_pdf()
        {
            var f = new Fixture();
            using var buffer = new MemoryStream();
            using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            {
                using (var w = new StreamWriter(zip.CreateEntry("ad.xml").Open(), new UTF8Encoding(false))) w.Write(InvoiceXml("cufe-zip", "FAC200"));
                using (var p = zip.CreateEntry("ad.pdf").Open()) p.Write(new byte[] { 1, 2, 3 });
            }

            var result = await f.Controller.Upload(new() { File = Convert.ToBase64String(buffer.ToArray()), FileName = "paquete.bin" });

            Assert.IsType<CreatedResult>(result);
            var stored = await f.Db.ReceivedDocuments.SingleAsync();
            Assert.Equal("cufe-zip", stored.Cufe);
            Assert.Equal(new byte[] { 1, 2, 3 }, stored.OriginalPdf);
            Assert.Equal("ad.pdf", stored.OriginalPdfFileName);
        }

        [Fact]
        public async Task Uploading_the_same_cufe_twice_answers_409_with_the_existing_id()
        {
            var f = new Fixture();
            await f.Controller.Upload(Upload(InvoiceXml("cufe-dup", "FAC1")));
            var existing = (await f.Db.ReceivedDocuments.SingleAsync()).Id;

            var result = await f.Controller.Upload(Upload(InvoiceXml("cufe-dup", "FAC1")));

            var conflict = Assert.IsType<ConflictObjectResult>(result);
            Assert.Contains(existing.ToString(), System.Text.Json.JsonSerializer.Serialize(conflict.Value));
            Assert.Equal(1, await f.Db.ReceivedDocuments.CountAsync());
        }

        [Fact]
        public async Task Uploading_garbage_is_a_400_and_stores_nothing()
        {
            var f = new Fixture();

            Assert.IsType<BadRequestObjectResult>(await f.Controller.Upload(new() { File = "no-es-base64!" }));
            Assert.IsType<BadRequestObjectResult>(await f.Controller.Upload(Upload("<Foo/>")));
            Assert.IsType<BadRequestObjectResult>(await f.Controller.Upload(new() { File = "" }));
            Assert.Empty(await f.Db.ReceivedDocuments.ToListAsync());
        }

        [Fact]
        public async Task A_support_document_cannot_be_ingested_as_a_received_invoice()
        {
            var f = new Fixture();
            var xml = InvoiceXml("cufe-ds", "DS1").Replace("<cbc:InvoiceTypeCode>01<", "<cbc:InvoiceTypeCode>05<");

            Assert.IsType<BadRequestObjectResult>(await f.Controller.Upload(Upload(xml)));
        }

        [Fact]
        public async Task Triggering_an_event_returns_200_when_sent_and_422_when_the_engine_rejects_it()
        {
            var f = new Fixture();
            await f.Controller.Upload(Upload(InvoiceXml("cufe-ev", "FAC300")));
            var id = (await f.Db.ReceivedDocuments.SingleAsync()).Id;

            var ok = Assert.IsType<OkObjectResult>(await f.Controller.TriggerEvent(id, "030"));
            Assert.Contains("cude-030", System.Text.Json.JsonSerializer.Serialize(ok.Value));

            f.Events.StatusToReturn = "REJECTED";
            Assert.IsType<UnprocessableEntityObjectResult>(await f.Controller.TriggerEvent(id, "032"));

            f.Events.StatusToReturn = "PENDING";
            Assert.IsType<ConflictObjectResult>(await f.Controller.TriggerEvent(id, "033"));
        }

        [Fact]
        public async Task Events_are_refused_for_invalid_codes_and_for_documents_that_are_not_on_credit()
        {
            var f = new Fixture();
            await f.Controller.Upload(Upload(InvoiceXml("cufe-cash", "FAC400", credit: false)));
            var id = (await f.Db.ReceivedDocuments.SingleAsync()).Id;

            Assert.IsType<UnprocessableEntityObjectResult>(await f.Controller.TriggerEvent(id, "034"));
            Assert.IsType<UnprocessableEntityObjectResult>(await f.Controller.TriggerEvent(id, "030"));
            Assert.Empty(f.Events.Triggered);
        }

        [Fact]
        public async Task Another_issuer_or_branch_does_not_see_the_document()
        {
            var f = new Fixture();
            await f.Controller.Upload(Upload(InvoiceXml("cufe-priv", "FAC500")));
            var id = (await f.Db.ReceivedDocuments.SingleAsync()).Id;

            var otherClient = f.As(Guid.NewGuid(), BranchId);
            Assert.IsType<NotFoundObjectResult>(await otherClient.TriggerEvent(id, "030"));
            Assert.IsType<NotFoundObjectResult>(await otherClient.GetEvents(id));

            var otherBranch = f.As(f.Client.Id, Guid.NewGuid());
            Assert.IsType<NotFoundObjectResult>(await otherBranch.GetEvents(id));
            var list = Assert.IsType<OkObjectResult>(await otherBranch.List(null, null));
            Assert.Contains("\"total\":0", System.Text.Json.JsonSerializer.Serialize(list.Value));
        }

        [Fact]
        public async Task The_list_finds_by_text_and_by_cufe()
        {
            var f = new Fixture();
            await f.Controller.Upload(Upload(InvoiceXml("cufe-a", "FAC600")));
            await f.Controller.Upload(Upload(InvoiceXml("cufe-b", "FAC601")));

            var byText = Assert.IsType<OkObjectResult>(await f.Controller.List("PROVEEDOR 600", null));
            Assert.Contains("\"total\":1", System.Text.Json.JsonSerializer.Serialize(byText.Value));

            var byCufe = Assert.IsType<OkObjectResult>(await f.Controller.List(null, "cufe-b"));
            Assert.Contains("FAC601", System.Text.Json.JsonSerializer.Serialize(byCufe.Value));
        }

        [Fact]
        public async Task The_event_xml_of_a_direct_issuer_is_the_stored_signed_xml_and_is_missing_for_older_events()
        {
            var f = new Fixture();
            await f.Controller.Upload(Upload(InvoiceXml("cufe-file", "FAC700")));
            var received = await f.Db.ReceivedDocuments.SingleAsync();
            var withXml = new ReceivedDocumentEvent { Id = Guid.NewGuid(), ReceivedDocumentId = received.Id, EventCode = "030", Status = "SENT", SignedXml = "<ApplicationResponse/>", SentAt = DateTime.UtcNow };
            var old = new ReceivedDocumentEvent { Id = Guid.NewGuid(), ReceivedDocumentId = received.Id, EventCode = "032", Status = "SENT", SentAt = DateTime.UtcNow };
            f.Db.ReceivedDocumentEvents.AddRange(withXml, old);
            await f.Db.SaveChangesAsync();

            var file = Assert.IsType<FileContentResult>(await f.Controller.DownloadEventFile(received.Id, withXml.Id));
            Assert.Equal("application/xml", file.ContentType);
            Assert.Equal("<ApplicationResponse/>", Encoding.UTF8.GetString(file.FileContents));
            Assert.Equal("FAC700-030.xml", file.FileDownloadName);

            Assert.IsType<NotFoundObjectResult>(await f.Controller.DownloadEventFile(received.Id, old.Id));
            Assert.IsType<NotFoundObjectResult>(await f.Controller.DownloadEventFile(received.Id, Guid.NewGuid()));
        }
    }
}
