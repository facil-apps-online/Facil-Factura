using System.Reflection;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Dataico;
using Fel.Infrastructure.Dataico.Models;
using Fel.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Fel.Tests
{
    // Numeración propia de las notas de eliminación y de reemplazo de nómina (Dataico): prefijo y consecutivo aparte de los de la nómina, con los
    // prefijos que ya tiene la cuenta de Dataico (módulos payroll-deletion y payroll-entry-replacement) y sin repetir prefijo entre numeraciones de nómina.
    public class PayrollNoteNumberingTests
    {
        // Dataico simulado: solo responde a la lista de prefijos por módulo.
        public class FakeDataico : DispatchProxy
        {
            public Dictionary<string, string[]> Prefixes { get; } = new();
            public List<string> ModulesAsked { get; } = new();

            protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            {
                if (targetMethod?.Name != nameof(IDataicoApiService.ListNotePrefixesAsync)) throw new NotImplementedException(targetMethod?.Name);
                var module = (string)args![0]!;
                ModulesAsked.Add(module);
                var result = new DataicoNotePrefixesResult { Success = true };
                if (Prefixes.TryGetValue(module, out var list)) result.Prefixes.AddRange(list);
                return Task.FromResult(result);
            }
        }

        private sealed class FakeCrypto : ICryptoService
        {
            public string Encrypt(string plainText) => plainText;
            public string Decrypt(string cipherText) => cipherText;
            public string GenerateCufeSha384(string dataToHash) => dataToHash;
        }

        private sealed class Env
        {
            public FelDbContext Db { get; } = new(new DbContextOptionsBuilder<FelDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            public Client Client { get; }
            public NoteNumberingAdminService Service { get; }
            public FakeDataico Dataico { get; }

            public Env(string integratorCode = "DATAICO")
            {
                var integrator = new Integrator { Id = Guid.NewGuid(), Code = integratorCode, Name = integratorCode };
                Client = new Client
                {
                    Id = Guid.NewGuid(), TaxId = "900123456", CompanyName = "EMISOR SAS", IntegratorId = integrator.Id, Integrator = integrator,
                    DataicoAuthTokenEncrypted = "token", DataicoEnvironment = "PRODUCCION"
                };
                Db.Integrators.Add(integrator);
                Db.Clients.Add(Client);
                Db.SaveChanges();
                var proxy = DispatchProxy.Create<IDataicoApiService, FakeDataico>();
                Dataico = (FakeDataico)(object)proxy;
                Service = new NoteNumberingAdminService(Db, proxy, new FakeCrypto());
            }

            public void AddResolution(string documentType, string prefix)
            {
                Db.Resolutions.Add(new Resolution
                {
                    Id = Guid.NewGuid(), ClientId = Client.Id, ResolutionNumber = "18760000001", Prefix = prefix, DocumentType = documentType, IsActive = true,
                    NumberStart = 1, NumberEnd = 100, ValidFrom = DateTime.Today.AddYears(-1), ValidTo = DateTime.Today.AddYears(1)
                });
                Db.SaveChanges();
            }

            public void AddNote(string typeCode, string prefix, string number, string status)
            {
                Db.Documents.Add(new Document { Id = Guid.NewGuid(), ClientId = Client.Id, TypeCode = typeCode, Prefix = prefix, Number = number, Status = status });
                Db.SaveChanges();
            }
        }

        [Fact]
        public async Task A_dataico_client_can_create_a_deletion_numbering_and_it_becomes_the_default()
        {
            var env = new Env();
            env.Dataico.Prefixes["payroll-deletion"] = new[] { "NDE" };

            var result = await env.Service.CreateAsync(env.Client.Id, NoteKind.PayrollDeletion, "nde", 1);

            Assert.Null(result.Error);
            Assert.Equal("NDE", result.Row!.Prefix);
            Assert.Equal(1, result.Row.NextNumber);
            Assert.True(result.Row.IsDefault);
            // El prefijo existe en la cuenta de Dataico: se verifica contra su módulo de nómina.
            Assert.True(result.Dataico!.Checked);
            Assert.True(result.Dataico.Exists);
            Assert.Contains("payroll-deletion", env.Dataico.ModulesAsked);
        }

        [Fact]
        public async Task A_prefix_that_dataico_does_not_have_is_saved_with_a_warning_that_names_the_kind()
        {
            var env = new Env();

            var result = await env.Service.CreateAsync(env.Client.Id, NoteKind.PayrollReplacement, "NRE", 1);

            Assert.Null(result.Error);
            Assert.True(result.Dataico!.Checked);
            Assert.False(result.Dataico.Exists);
            Assert.Contains("reemplazo de nómina", result.Dataico.Message);
            Assert.Contains("NRE", result.Dataico.Message);
            Assert.Contains("payroll-entry-replacement", env.Dataico.ModulesAsked);
        }

        [Fact]
        public async Task A_client_that_is_not_on_dataico_cannot_create_payroll_note_numberings()
        {
            var env = new Env("NATIVE");

            var result = await env.Service.CreateAsync(env.Client.Id, NoteKind.PayrollDeletion, "NDE", 1);

            Assert.NotNull(result.Error);
            Assert.Contains("Dataico", result.Error);
        }

        [Fact]
        public async Task A_payroll_note_prefix_cannot_repeat_the_payroll_numbering_prefix()
        {
            var env = new Env();
            env.AddResolution("NE", "N");

            var same = await env.Service.CreateAsync(env.Client.Id, NoteKind.PayrollDeletion, "N", 1);
            Assert.NotNull(same.Error);
            Assert.Contains("numeración de nómina", same.Error);
            Assert.Null((await env.Service.CreateAsync(env.Client.Id, NoteKind.PayrollDeletion, "NDE", 1)).Error);
        }

        [Fact]
        public async Task Deletion_and_replacement_notes_cannot_share_a_prefix()
        {
            var env = new Env();

            Assert.Null((await env.Service.CreateAsync(env.Client.Id, NoteKind.PayrollDeletion, "NDE", 1)).Error);
            var other = await env.Service.CreateAsync(env.Client.Id, NoteKind.PayrollReplacement, "NDE", 1);
            Assert.NotNull(other.Error);
            Assert.Contains("nota de eliminación de nómina", other.Error);
            Assert.Null((await env.Service.CreateAsync(env.Client.Id, NoteKind.PayrollReplacement, "NRE", 1)).Error);
        }

        [Fact]
        public async Task The_payroll_note_prefix_may_equal_an_invoice_prefix_but_a_credit_note_may_not()
        {
            var env = new Env();
            env.AddResolution("FE", "SETT");

            Assert.Null((await env.Service.CreateAsync(env.Client.Id, NoteKind.PayrollDeletion, "SETT", 1)).Error);
            Assert.NotNull((await env.Service.CreateAsync(env.Client.Id, NoteKind.CreditNote, "SETT", 1)).Error);
        }

        [Fact]
        public async Task Bringing_prefixes_from_dataico_includes_the_payroll_notes_that_are_not_registered_yet()
        {
            var env = new Env();
            env.AddResolution("FE", "SETT");
            env.Dataico.Prefixes["payroll-deletion"] = new[] { "NE", "NDE" };
            env.Dataico.Prefixes["payroll-entry-replacement"] = new[] { "NR" };
            env.Dataico.Prefixes["credit-note"] = new[] { "NC", "SETT" };
            Assert.Null((await env.Service.CreateAsync(env.Client.Id, NoteKind.PayrollDeletion, "NE", 1)).Error);

            var (missing, error) = await env.Service.DataicoPrefixesNotInFacilFacturaAsync(env.Client.Id);

            Assert.Null(error);
            // NE ya está registrada; SETT es de una resolución de factura y no se trae como nota crédito; las demás sí.
            Assert.Contains((NoteKind.PayrollDeletion, "NDE"), missing);
            Assert.Contains((NoteKind.PayrollReplacement, "NR"), missing);
            Assert.Contains((NoteKind.CreditNote, "NC"), missing);
            Assert.DoesNotContain((NoteKind.PayrollDeletion, "NE"), missing);
            Assert.DoesNotContain((NoteKind.CreditNote, "SETT"), missing);
        }

        [Fact]
        public async Task The_numbering_used_to_emit_is_the_default_of_that_kind_for_that_client()
        {
            var env = new Env();
            var first = (await env.Service.CreateAsync(env.Client.Id, NoteKind.PayrollDeletion, "AAA", 10)).Row!;
            var second = (await env.Service.CreateAsync(env.Client.Id, NoteKind.PayrollDeletion, "BBB", 20)).Row!;
            Assert.Null(await env.Service.SetDefaultAsync(env.Client.Id, second.Id));
            await env.Service.CreateAsync(env.Client.Id, NoteKind.PayrollReplacement, "REP", 1);

            var chosen = await NoteNumberingAdminService.PayrollNoteNumberingAsync(env.Db, env.Client.Id, NoteKind.PayrollDeletion);

            Assert.Equal("BBB", chosen!.Prefix);
            Assert.Equal(20, chosen.NextNumber);
            Assert.NotEqual(first.Id, chosen.Id);
            Assert.Equal("REP", (await NoteNumberingAdminService.PayrollNoteNumberingAsync(env.Db, env.Client.Id, NoteKind.PayrollReplacement))!.Prefix);
            Assert.Null(await NoteNumberingAdminService.PayrollNoteNumberingAsync(env.Db, Guid.NewGuid(), NoteKind.PayrollDeletion));
        }

        [Fact]
        public async Task Without_a_configured_numbering_there_is_nothing_to_emit_with()
        {
            var env = new Env();

            Assert.Null(await NoteNumberingAdminService.PayrollNoteNumberingAsync(env.Db, env.Client.Id, NoteKind.PayrollDeletion));
        }

        [Fact]
        public async Task Only_approved_notes_count_as_use_of_a_payroll_numbering()
        {
            var env = new Env();
            await env.Service.CreateAsync(env.Client.Id, NoteKind.PayrollDeletion, "NDE", 1);
            env.AddNote("NE-ELIMINACION", "NDE", "1", "REJECTED");

            var rejectedOnly = await env.Service.GetOverviewAsync(env.Client.Id);
            Assert.False(rejectedOnly.Items.Single(i => i.Kind == NoteKind.PayrollDeletion).InUse);

            env.AddNote("NE-ELIMINACION", "NDE", "1", "APPROVED");
            var approved = await env.Service.GetOverviewAsync(env.Client.Id);
            var item = approved.Items.Single(i => i.Kind == NoteKind.PayrollDeletion);
            Assert.True(item.InUse);
            Assert.Equal("own", item.Source);
            Assert.Equal("Nota de eliminación de nómina", NoteNumberingAdminService.KindLabel(item.Kind));
        }

        [Fact]
        public async Task A_payroll_numbering_in_use_cannot_be_deleted_but_an_unused_one_can()
        {
            var env = new Env();
            var used = (await env.Service.CreateAsync(env.Client.Id, NoteKind.PayrollDeletion, "USED", 1)).Row!;
            var free = (await env.Service.CreateAsync(env.Client.Id, NoteKind.PayrollReplacement, "FREE", 1)).Row!;
            env.AddNote("NE-ELIMINACION", "USED", "1", "APPROVED");

            Assert.NotNull(await env.Service.DeleteAsync(env.Client.Id, used.Id));
            Assert.Null(await env.Service.DeleteAsync(env.Client.Id, free.Id));
        }

        [Fact]
        public void The_type_code_and_the_kind_map_both_ways_for_every_note()
        {
            foreach (var kind in new[] { NoteKind.CreditNote, NoteKind.DebitNote, NoteKind.PayrollDeletion, NoteKind.PayrollReplacement })
                Assert.Equal(kind, NoteNumberingAdminService.KindOfTypeCode(NoteNumberingAdminService.TypeCodeOfKind(kind)));
            Assert.Null(NoteNumberingAdminService.KindOfTypeCode("FE-STD"));
        }

        [Fact]
        public void The_branch_numbering_catalog_does_not_offer_the_payroll_kinds()
        {
            Assert.DoesNotContain(NoteKinds.All, k => NoteKinds.IsPayroll(k.Kind));
            Assert.All(NoteKinds.Payroll, k => Assert.True(NoteKinds.IsPayroll(k.Kind)));
        }
    }
}
