using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fel.Api.Client.Controllers
{
    // Busca el municipio del catálogo DIAN que corresponde a la ciudad que devuelve Google Maps, para llenar el código DANE del tercero.
    // Va bajo api/client/customers, que el middleware HMAC ya deja pasar a la sesión del portal.
    [ApiController]
    [Route("api/client/customers/municipality-match")]
    public class MunicipalityMatchController : ClientPortalControllerBase
    {
        private readonly FelDbContext _dbContext;

        public MunicipalityMatchController(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // name = ciudad; department = departamento que devolvió Google (opcional); preferDepartment = código de 2 dígitos del municipio de la
        // empresa, para desempatar nombres repetidos (opcional). Responde 404 si no hay una coincidencia única.
        [HttpGet]
        public async Task<IActionResult> Match([FromQuery] string? name, [FromQuery] string? department, [FromQuery] string? preferDepartment)
        {
            var city = Normalize(name);
            if (city.Length == 0) return NotFound();

            var candidates = (await _dbContext.DianMunicipalities.AsNoTracking().ToListAsync())
                .Where(m => Normalize(m.Name) == city)
                .ToList();

            if (candidates.Count > 1)
            {
                var dept = Normalize(department);
                var byDepartment = dept.Length == 0 ? candidates : candidates.Where(m => Normalize(m.DepartmentName) == dept).ToList();
                candidates = byDepartment.Count > 0 ? byDepartment : candidates;
            }
            if (candidates.Count > 1 && !string.IsNullOrWhiteSpace(preferDepartment))
            {
                var preferred = candidates.Where(m => m.DepartmentCode == preferDepartment!.Trim()).ToList();
                if (preferred.Count > 0) candidates = preferred;
            }

            if (candidates.Count != 1) return NotFound();
            var match = candidates[0];
            return Ok(new { code = match.Code, name = match.Name, departmentCode = match.DepartmentCode, departmentName = match.DepartmentName });
        }

        // Mayúsculas sin tildes ni puntuación, y sin el sufijo del distrito ("BOGOTÁ, D.C." -> "BOGOTA"): Google y el catálogo lo escriben distinto.
        private static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var text = value.Split(',')[0].Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in text)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                sb.Append(char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : ' ');
            }
            return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
