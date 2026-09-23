using System.Text.Json;
using System.Text.Json.Serialization;
using Fel.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fel.Infrastructure.Data
{
    public static class RipsDataSeeder
    {
        public static async Task SeedRipsDataAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FelDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("RipsDataSeeder");

            var seedDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "SeedData");
            if (!Directory.Exists(seedDir))
            {
                logger.LogWarning("SeedData directory not found at {SeedDir}. Skipping RIPS seeding.", seedDir);
                return;
            }

            // Seed CUPS
            if (!await context.RipsCupsRules.AnyAsync())
            {
                var cupsFile = Path.Combine(seedDir, "cups.json");
                if (File.Exists(cupsFile))
                {
                    logger.LogInformation("Seeding CUPS rules...");
                    var json = await File.ReadAllTextAsync(cupsFile);
                    var cupsDtoList = JsonSerializer.Deserialize<List<CupsDto>>(json);
                    
                    if (cupsDtoList != null)
                    {
                        var entities = cupsDtoList.Select(dto => new RipsCupsRule
                        {
                            Code = dto.Codigo?.Trim() ?? string.Empty,
                            Name = dto.Nombre?.Trim() ?? string.Empty,
                            AllowedGender = dto.Sexo?.Trim() ?? "A",
                            MinAgeDays = int.TryParse(dto.Edad_Inicial_dias, out var minD) ? minD : 0,
                            MaxAgeDays = int.TryParse(dto.Edad_final_dias, out var maxD) ? maxD : 49275,
                            RequiresDiagnosis = (dto.Dx_requerido?.Trim() == "S")
                        }).ToList();

                        context.RipsCupsRules.AddRange(entities);
                        await context.SaveChangesAsync();
                        logger.LogInformation("Seeded {Count} CUPS rules.", entities.Count);
                    }
                }
            }

            // Seed CIE10
            if (!await context.RipsCie10Rules.AnyAsync())
            {
                var cieFile = Path.Combine(seedDir, "cie10.json");
                if (File.Exists(cieFile))
                {
                    logger.LogInformation("Seeding CIE10 rules...");
                    var json = await File.ReadAllTextAsync(cieFile);
                    var cieDtoList = JsonSerializer.Deserialize<List<Cie10Dto>>(json);

                    if (cieDtoList != null)
                    {
                        var entities = cieDtoList.Select(dto => new RipsCie10Rule
                        {
                            Code = dto.CODIGO?.Trim() ?? string.Empty,
                            Description = dto.DESCRIPCION?.Trim() ?? string.Empty,
                            AllowedGender = dto.SEXO?.Trim() ?? "A",
                            MinAgeDays = DecodeAgeLimitDays(dto.LIM_INF, isUpperBound: false),
                            MaxAgeDays = DecodeAgeLimitDays(dto.LIM_SUP, isUpperBound: true)
                        }).ToList();

                        context.RipsCie10Rules.AddRange(entities);
                        await context.SaveChangesAsync();
                        logger.LogInformation("Seeded {Count} CIE10 rules.", entities.Count);
                    }
                }
            }
        }

        // El catálogo oficial CIE10 (MinSalud) codifica LIM_INF/LIM_SUP como un entero de 3
        // dígitos "unidad+valor": el primer dígito es la unidad (1=Horas, 2=Días, 3=Meses,
        // 4=Años) y los dos últimos son la cantidad — ej. 227 = 2 (Días) + 27 = 27 días (encaja
        // con la definición clínica real de tétanos neonatal). El valor 0 significa "sin
        // restricción"; 599 es el centinela documentado para "sin límite superior".
        private const int NoUpperLimitDays = 47450; // ~130 años, mismo centinela que usa CUPS
        private static int DecodeAgeLimitDays(int raw, bool isUpperBound)
        {
            if (raw == 0) return isUpperBound ? NoUpperLimitDays : 0;
            if (isUpperBound && raw == 599) return NoUpperLimitDays;

            var unit = raw / 100;
            var value = raw % 100;
            return unit switch
            {
                1 => value / 24,   // Horas -> días
                2 => value,        // Días
                3 => value * 30,   // Meses -> días (aprox.)
                4 => value * 365,  // Años -> días (aprox.)
                _ => isUpperBound ? NoUpperLimitDays : 0 // unidad no reconocida: no restringir
            };
        }

        private class CupsDto
        {
            public string? Codigo { get; set; }
            public string? Nombre { get; set; }
            public string? Sexo { get; set; }
            public string? Edad_Inicial_dias { get; set; }
            
            [JsonPropertyName("Edad-final-dias")]
            public string? Edad_final_dias { get; set; }
            public string? Dx_requerido { get; set; }
        }

        private class Cie10Dto
        {
            public string? CODIGO { get; set; }
            public string? DESCRIPCION { get; set; }
            public string? SEXO { get; set; }
            public int LIM_INF { get; set; }
            public int LIM_SUP { get; set; }
        }
    }
}
