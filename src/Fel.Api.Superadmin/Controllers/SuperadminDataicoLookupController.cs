using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Dataico;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Superadmin.Controllers
{
    // Consulta de solo lectura, para soporte: qué tiene Dataico en la cuenta de un emisor para unos números dados. Usa la misma consulta que el portal
    // del cliente (DataicoLookupService); la credencial de Dataico del emisor se descifra dentro de la aplicación y nunca se devuelve. No guarda nada.
    [ApiController]
    [Route("api/superadmin/dataico-lookup")]
    public class SuperadminDataicoLookupController : ControllerBase
    {
        private const int MaxNumbers = 20;

        private readonly FelDbContext _db;
        private readonly DataicoLookupService _lookup;

        public SuperadminDataicoLookupController(FelDbContext db, DataicoLookupService lookup)
        {
            _db = db;
            _lookup = lookup;
        }

        // numbers: prefijo y número pegados, separados por coma ("N89,N90,N91"). kind: payroll (por defecto), deletion o replacement.
        [HttpGet("payroll")]
        public async Task<IActionResult> Payroll([FromQuery] Guid clientId, [FromQuery] string? numbers, [FromQuery] string? kind)
        {
            var list = (numbers ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
            if (list.Count == 0) return BadRequest("Indica al menos un número en 'numbers' (por ejemplo N89,N90).");
            if (list.Count > MaxNumbers) return BadRequest($"Máximo {MaxNumbers} números por consulta.");

            var client = await _db.Clients.AsNoTracking().Include(c => c.Integrator).FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null) return NotFound("Emisor no encontrado.");
            if (client.Integrator?.Code != "DATAICO") return BadRequest("Este emisor no usa Dataico.");

            var lookupKind = kind?.ToLowerInvariant() switch
            {
                "deletion" => DataicoLookupKind.PayrollDeletion,
                "replacement" => DataicoLookupKind.PayrollReplacement,
                _ => DataicoLookupKind.Payroll
            };

            var results = new List<object>();
            foreach (var number in list)
            {
                var result = await _lookup.LookupAsync(client, lookupKind, number);
                results.Add(new { number, found = result.Ok, status = result.Status, error = result.Error, document = result.Summary });
            }
            return Ok(new { client = client.CompanyName, environment = client.DataicoEnvironment, results });
        }
    }
}
