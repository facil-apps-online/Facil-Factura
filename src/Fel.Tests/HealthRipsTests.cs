using System.Text.Json;
using Fel.Core.Entities;
using Fel.Core.Models;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Dian;
using Fel.Infrastructure.MinSalud;
using Fel.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Fel.Tests
{
    // RIPS soporte de la factura de salud: el prevalidador (cruces con la factura), la lectura de la respuesta del Ministerio y el servicio que lo envía y
    // guarda el resultado.
    public class HealthRipsTests
    {
        private static HealthSectorData Health(decimal copay = 2000m, decimal moderator = 3000m) => new()
        {
            ProviderCode = "1100100001", PaymentModality = "04", Coverage = "01", ContractNumber = "CUCON-1",
            PeriodStart = new DateTime(2026, 11, 1), PeriodEnd = new DateTime(2026, 11, 1),
            Prepaid = new List<HealthPrepaid>
            {
                new() { ConceptCode = "01", Amount = copay, ReceivedDate = new DateTime(2026, 11, 1) },
                new() { ConceptCode = "02", Amount = moderator, ReceivedDate = new DateTime(2026, 11, 1) }
            }
        };

        private static JsonElement Rips(Func<string, string>? tweak = null)
        {
            var json = """
            {
              "numDocumentoIdObligado": "900303194",
              "numFactura": "SETT1001",
              "tipoNota": null,
              "numNota": null,
              "usuarios": [
                {
                  "consecutivo": 1,
                  "tipoDocumentoIdentificacion": "CC",
                  "numDocumentoIdentificacion": "1020304050",
                  "tipoUsuario": "01",
                  "fechaNacimiento": "1990-05-14",
                  "codSexo": "F",
                  "servicios": {
                    "consultas": [
                      { "codPrestador": "110010000101", "vrServicio": 20000, "conceptoRecaudo": "01", "valorPagoModerador": 2000, "consecutivo": 1 },
                      { "codPrestador": "110010000101", "vrServicio": 18000, "conceptoRecaudo": "02", "valorPagoModerador": 3000, "consecutivo": 2 }
                    ],
                    "procedimientos": [
                      { "codPrestador": "110010000101", "vrServicio": 0, "conceptoRecaudo": "05", "valorPagoModerador": 0, "consecutivo": 1 }
                    ]
                  }
                }
              ]
            }
            """;
            return JsonDocument.Parse(tweak == null ? json : tweak(json)).RootElement.Clone();
        }

        private static RipsValidation Check(JsonElement rips, HealthSectorData? health = null, decimal? total = 38000m) =>
            HealthRipsValidator.Validate(rips, health ?? Health(), "900303194-7", "SETT1001", total);

        // ---- Prevalidador ----

        [Fact]
        public void A_rips_that_matches_the_invoice_is_valid()
        {
            var result = Check(Rips());
            Assert.True(result.IsValid, string.Join("; ", result.Errors));
            Assert.Empty(result.Warnings);
        }

        [Fact]
        public void The_issuer_and_the_invoice_number_must_match()
        {
            var wrongIssuer = Check(Rips(j => j.Replace("\"900303194\"", "\"800111222\"")));
            Assert.Contains(wrongIssuer.Errors, e => e.Contains("numDocumentoIdObligado"));

            var wrongNumber = Check(Rips(j => j.Replace("SETT1001", "SETT9999")));
            Assert.Contains(wrongNumber.Errors, e => e.Contains("numFactura"));
        }

        [Fact]
        public void The_provider_code_of_every_service_must_start_with_the_invoice_provider()
        {
            var result = Check(Rips(j => j.Replace("110010000101", "220020000101")));
            Assert.Contains(result.Errors, e => e.Contains("codPrestador") && e.Contains("provider_code"));

            var shortCode = Check(Rips(j => j.Replace("110010000101", "1100100001")));
            Assert.Contains(shortCode.Errors, e => e.Contains("12 dígitos"));
        }

        [Fact]
        public void The_collections_of_the_invoice_must_equal_the_sum_in_the_rips()
        {
            var result = Check(Rips(), Health(copay: 2500m));
            Assert.Contains(result.Errors, e => e.Contains("copago") && e.Contains("2500.00") && e.Contains("2000.00"));

            var moderator = Check(Rips(), Health(moderator: 0m));
            Assert.Contains(moderator.Errors, e => e.Contains("cuota moderadora"));
        }

        [Fact]
        public void A_collected_value_needs_its_concept_and_the_concept_must_be_known()
        {
            var withoutConcept = Check(Rips(j => j.Replace("\"conceptoRecaudo\": \"02\", ", "")));
            Assert.Contains(withoutConcept.Errors, e => e.Contains("conceptoRecaudo"));

            var unknown = Check(Rips(j => j.Replace("\"conceptoRecaudo\": \"02\"", "\"conceptoRecaudo\": \"09\"")));
            Assert.Contains(unknown.Errors, e => e.Contains("09"));
        }

        [Fact]
        public void The_users_need_their_identification_dates_and_unique_consecutives()
        {
            Assert.Contains(Check(Rips(j => j.Replace("1990-05-14", "14/05/1990"))).Errors, e => e.Contains("fechaNacimiento"));
            Assert.Contains(Check(Rips(j => j.Replace("\"numDocumentoIdentificacion\": \"1020304050\"", "\"numDocumentoIdentificacion\": \"\""))).Errors, e => e.Contains("numDocumentoIdentificacion"));

            var twoUsers = Check(Rips(j => j.Replace("\"usuarios\": [", "\"usuarios\": [ { \"consecutivo\": 1, \"tipoDocumentoIdentificacion\": \"CC\", \"numDocumentoIdentificacion\": \"7\", \"tipoUsuario\": \"01\", \"fechaNacimiento\": \"1980-01-01\", \"servicios\": { \"consultas\": [ { \"codPrestador\": \"110010000101\", \"vrServicio\": 0, \"conceptoRecaudo\": \"05\", \"valorPagoModerador\": 0 } ] } },")));
            Assert.Contains(twoUsers.Errors, e => e.Contains("repetido"));
        }

        [Fact]
        public void An_unknown_id_type_only_warns_and_a_different_total_only_warns()
        {
            var idType = Check(Rips(j => j.Replace("\"CC\"", "\"ZZ\"")));
            Assert.True(idType.IsValid);
            Assert.Contains(idType.Warnings, w => w.Contains("TipoIdPISIS"));

            var total = Check(Rips(), total: 99000m);
            Assert.True(total.IsValid);
            Assert.Contains(total.Warnings, w => w.Contains("vrServicio"));
        }

        [Fact]
        public void A_rips_without_users_or_not_an_object_is_rejected()
        {
            Assert.Contains(Check(Rips(j => j.Replace("\"usuarios\": [", "\"otros\": ["))).Errors, e => e.Contains("usuarios"));
            Assert.NotEmpty(Check(JsonDocument.Parse("[1,2]").RootElement).Errors);
        }

        // ---- Respuesta del Ministerio ----

        [Fact]
        public void An_accepted_response_carries_the_cuv_and_the_process_id()
        {
            var cuv = new string('a', 96);
            var result = MinSaludFevRipsClient.Parse($"{{\"ResultState\":true,\"ProcesoId\":1254,\"NumFactura\":\"SETT1001\",\"CodigoUnicoValidacion\":\"{cuv}\",\"ResultadosValidacion\":[{{\"Clase\":\"NOTIFICACION\",\"Codigo\":\"CIN001\",\"Descripcion\":\"d\",\"Observaciones\":\"o\",\"Fuente\":\"RIPS\"}}]}}", 200, true);
            Assert.True(result.Accepted);
            Assert.Equal(cuv, result.Cuv);
            Assert.Equal(1254, result.ProcessId);
            Assert.Single(result.Findings);
        }

        [Fact]
        public void The_misspelled_process_id_of_the_manual_is_also_read()
        {
            var result = MinSaludFevRipsClient.Parse("{\"ResultState\":false,\"PorcesoID\":7,\"CodigoUnicoValidacion\":\"No aplica\",\"ResultadosValidacion\":[]}", 200, true);
            Assert.Equal(7, result.ProcessId);
        }

        [Fact]
        public void A_rejected_response_lists_only_the_rejections_in_the_message()
        {
            var result = MinSaludFevRipsClient.Parse("{\"ResultState\":false,\"ResultadosValidacion\":[{\"Clase\":\"RECHAZADO\",\"Codigo\":\"FED129\",\"Observaciones\":\"falta contrato\"},{\"Clase\":\"NOTIFICACION\",\"Codigo\":\"CIN001\",\"Observaciones\":\"aviso\"}]}", 200, true);
            Assert.False(result.Accepted);
            Assert.False(result.TransportFailure);
            Assert.Null(result.Cuv);
            Assert.Contains("FED129", result.Message);
            Assert.DoesNotContain("CIN001", result.Message);
            Assert.Equal(2, result.Findings.Count);
        }

        [Fact]
        public void A_response_that_is_not_a_verdict_is_a_transport_failure()
        {
            Assert.True(MinSaludFevRipsClient.Parse("<html>502</html>", 502, false).TransportFailure);
            Assert.True(MinSaludFevRipsClient.Parse("{\"message\":\"x\"}", 500, false).TransportFailure);
        }

        [Fact]
        public void The_login_token_is_read_whatever_its_field_is_called()
        {
            Assert.Equal("abc", MinSaludFevRipsClient.ReadToken("{\"token\":\"abc\"}"));
            Assert.Equal("abc", MinSaludFevRipsClient.ReadToken("{\"Token\":\"abc\"}"));
            Assert.Equal("abc", MinSaludFevRipsClient.ReadToken("\"abc\""));
            Assert.Null(MinSaludFevRipsClient.ReadToken("{\"x\":1}"));
        }

        // ---- Servicio de envío ----

        private sealed class FakeClient : IMinSaludFevRipsClient
        {
            public Queue<FevRipsResult> Answers { get; } = new();
            public List<(string Rips, string Xml)> Sent { get; } = new();

            public Task<FevRipsResult> SendInvoiceAsync(string ripsJson, string attachedDocumentXml, Client client, MinSaludCredentials credentials)
            {
                Sent.Add((ripsJson, attachedDocumentXml));
                return Task.FromResult(Answers.Dequeue());
            }
        }

        private sealed class Fixture
        {
            public FelDbContext Db { get; } = new(new DbContextOptionsBuilder<FelDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            public Client Client { get; } = new() { Id = Guid.NewGuid(), TaxId = "900303194", CompanyName = "SOFACTORY SAS" };
            public Document Document { get; }
            public FakeClient Ministry { get; } = new();
            public HealthRipsService Service { get; }

            public Fixture(bool withAttachedDocument = true)
            {
                Document = new Document { Id = Guid.NewGuid(), ClientId = Client.Id, BranchId = Guid.NewGuid(), Prefix = "SETT", Number = "1001", TypeCode = "FE-SALUD", Status = "APPROVED" };
                Db.Clients.Add(Client);
                Db.Branches.Add(new Branch { Id = Document.BranchId, ClientId = Client.Id, Name = "Principal", Code = "001", IsMain = true, IsActive = true });
                Db.Documents.Add(Document);
                Db.SaveChanges();
                if (withAttachedDocument)
                {
                    DocumentSignedXmlStore.SaveSignedAsync(Db, Document.Id, "<Invoice/>", 1).GetAwaiter().GetResult();
                    DocumentSignedXmlStore.SaveAttachedDocumentAsync(Db, Document.Id, "<AttachedDocument/>").GetAwaiter().GetResult();
                }
                Service = new HealthRipsService(Db, Ministry);
            }
        }

        private static FevRipsResult Accepted(string cuv) => new() { Accepted = true, Cuv = cuv, ProcessId = 5, Message = "CUV generado", ResponseJson = "{}" };

        [Fact]
        public async Task An_accepted_rips_is_stored_with_its_cuv_and_is_not_sent_twice()
        {
            var f = new Fixture();
            f.Ministry.Answers.Enqueue(Accepted(new string('b', 96)));

            var first = await f.Service.SubmitAsync(f.Document, f.Client, "{\"numFactura\":\"SETT1001\"}");
            Assert.Equal(DocumentRips.Accepted, first.Status);
            Assert.Equal(new string('b', 96), first.Cuv);
            Assert.Equal(1, first.Attempts);
            Assert.Equal("<AttachedDocument/>", f.Ministry.Sent[0].Xml);
            Assert.Contains("SETT1001", f.Ministry.Sent[0].Rips);

            var again = await f.Service.SubmitAsync(f.Document, f.Client, null);
            Assert.Equal(DocumentRips.Accepted, again.Status);
            Assert.Single(f.Ministry.Sent);
        }

        [Fact]
        public async Task A_rejected_rips_keeps_the_findings_and_a_corrected_one_replaces_it()
        {
            var f = new Fixture();
            f.Ministry.Answers.Enqueue(new FevRipsResult { Message = "FED129: falta contrato", ResponseJson = "{\"ResultState\":false}" });
            f.Ministry.Answers.Enqueue(Accepted(new string('c', 96)));

            var rejected = await f.Service.SubmitAsync(f.Document, f.Client, "{\"v\":1}");
            Assert.Equal(DocumentRips.Rejected, rejected.Status);
            Assert.Null(rejected.Cuv);
            Assert.Contains("FED129", rejected.Message);

            var fixedOne = await f.Service.SubmitAsync(f.Document, f.Client, "{\"v\":2}");
            Assert.Equal(DocumentRips.Accepted, fixedOne.Status);
            Assert.Equal(2, fixedOne.Attempts);
            Assert.Equal("{\"v\":2}", f.Ministry.Sent[1].Rips);
        }

        [Fact]
        public async Task A_transport_failure_stays_pending_and_resends_the_stored_rips_without_a_body()
        {
            var f = new Fixture();
            f.Ministry.Answers.Enqueue(new FevRipsResult { TransportFailure = true, Message = "sin conexión" });
            f.Ministry.Answers.Enqueue(Accepted(new string('d', 96)));

            var pending = await f.Service.SubmitAsync(f.Document, f.Client, "{\"v\":1}");
            Assert.Equal(DocumentRips.Pending, pending.Status);

            var retry = await f.Service.SubmitAsync(f.Document, f.Client, null);
            Assert.Equal(DocumentRips.Accepted, retry.Status);
            Assert.Equal("{\"v\":1}", f.Ministry.Sent[1].Rips);
        }

        [Fact]
        public async Task Without_the_attached_document_nothing_is_sent_and_the_rips_waits()
        {
            var f = new Fixture(withAttachedDocument: false);
            var row = await f.Service.SubmitAsync(f.Document, f.Client, "{\"v\":1}");
            Assert.Equal(DocumentRips.Pending, row.Status);
            Assert.Contains("AttachedDocument", row.Message);
            Assert.Empty(f.Ministry.Sent);
        }
    }
}
