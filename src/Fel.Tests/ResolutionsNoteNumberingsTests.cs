using System.Text.Json;
using Fel.Api.Integration.Controllers;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Tests
{
    // GET /resolutions devuelve, junto a las resoluciones, el prefijo y el próximo consecutivo de las notas que aplican a la sucursal.
    public class ResolutionsNoteNumberingsTests
    {
        private static async Task<JsonElement> ListAsync(Action<FelDbContext, Client, Guid, Resolution> seed)
        {
            var options = new DbContextOptionsBuilder<FelDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            var db = new FelDbContext(options);
            var client = new Client { Id = Guid.NewGuid(), TaxId = "900123456" };
            var branchId = Guid.NewGuid();
            var resolution = new Resolution
            {
                Id = Guid.NewGuid(), ClientId = client.Id, ResolutionNumber = "18760000001", Prefix = "SETT", DocumentType = "FE", IsActive = true,
                NumberStart = 1, NumberEnd = 5000, ValidFrom = DateTime.Today.AddYears(-1), ValidTo = DateTime.Today.AddYears(1)
            };
            db.Clients.Add(client);
            db.Resolutions.Add(resolution);
            db.ResolutionBranches.Add(new ResolutionBranch { ResolutionId = resolution.Id, BranchId = branchId });
            seed(db, client, branchId, resolution);
            await db.SaveChangesAsync();

            var controller = new ResolutionsController(db, null!)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
            controller.HttpContext.Items["ClientId"] = client.Id.ToString();
            controller.HttpContext.Items["BranchId"] = branchId.ToString();

            var ok = Assert.IsType<OkObjectResult>(await controller.List());
            return JsonSerializer.SerializeToElement(ok.Value);
        }

        [Fact]
        public async Task Returns_the_resolutions_and_the_note_numberings_of_the_branch()
        {
            var body = await ListAsync((db, client, branchId, resolution) =>
            {
                db.NoteNumberings.Add(NoteNumbering.Shared(client.Id, NoteKind.CreditNote, null, 7, resolution.Id));
                db.NoteNumberings.Add(NoteNumbering.Shared(client.Id, NoteKind.DebitNote, "ND", 3));
                db.NoteNumberings.Add(new NoteNumbering { Id = Guid.NewGuid(), ClientId = client.Id, BranchId = branchId, Kind = NoteKind.CreditNote, Prefix = "NCB", NextNumber = 12 });
                db.NoteNumberings.Add(new NoteNumbering { Id = Guid.NewGuid(), ClientId = client.Id, Kind = NoteKind.SupportAdjustment, Prefix = null });
                // Numeración de otra sucursal: no debe aparecer.
                db.NoteNumberings.Add(new NoteNumbering { Id = Guid.NewGuid(), ClientId = client.Id, BranchId = Guid.NewGuid(), Kind = NoteKind.CreditNote, Prefix = "OTRA", NextNumber = 99 });
            });

            Assert.Equal(1, body.GetProperty("resolutions").GetArrayLength());
            var notes = body.GetProperty("noteNumberings").EnumerateArray().ToList();
            Assert.DoesNotContain(notes, n => n.GetProperty("prefix").GetString() == "OTRA");

            var own = notes.First(n => n.GetProperty("scope").GetString() == "branch");
            Assert.Equal("CREDIT_NOTE", own.GetProperty("kind").GetString());
            Assert.Equal("NCB", own.GetProperty("prefix").GetString());
            Assert.Equal(12, own.GetProperty("nextNumber").GetInt64());

            // Sin prefijo propio la nota sale con el de su resolución.
            var shared = notes.First(n => n.GetProperty("kind").GetString() == "CREDIT_NOTE" && n.GetProperty("scope").GetString() == "shared");
            Assert.Equal("SETT", shared.GetProperty("prefix").GetString());
            Assert.Equal(7, shared.GetProperty("nextNumber").GetInt64());
            Assert.Equal("18760000001", shared.GetProperty("resolutionNumber").GetString());

            var debit = notes.First(n => n.GetProperty("kind").GetString() == "DEBIT_NOTE");
            Assert.Equal("ND", debit.GetProperty("prefix").GetString());

            var adjustment = notes.First(n => n.GetProperty("kind").GetString() == "SUPPORT_ADJUSTMENT");
            Assert.Equal(1, adjustment.GetProperty("nextNumber").GetInt64());
        }

        [Fact]
        public async Task The_client_level_default_is_marked()
        {
            var body = await ListAsync((db, client, _, _) =>
            {
                db.NoteNumberings.Add(new NoteNumbering { Id = Guid.NewGuid(), ClientId = client.Id, Kind = NoteKind.CreditNote, Prefix = "NC1", NextNumber = 5, IsDefault = true });
                db.NoteNumberings.Add(new NoteNumbering { Id = Guid.NewGuid(), ClientId = client.Id, Kind = NoteKind.CreditNote, Prefix = "NC2", NextNumber = 2 });
            });

            var notes = body.GetProperty("noteNumberings").EnumerateArray().Where(n => n.GetProperty("scope").GetString() == "client").ToList();
            Assert.Equal(new[] { "NC1", "NC2" }, notes.Select(n => n.GetProperty("prefix").GetString()));
            Assert.True(notes[0].GetProperty("isDefault").GetBoolean());
            Assert.False(notes[1].GetProperty("isDefault").GetBoolean());
        }
    }
}
