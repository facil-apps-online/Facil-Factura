using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Fel.Infrastructure.Services;
using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace Fel.Api.Tenant.Controllers
{
    [ApiController]
    [Route("api/v1/{country}/{entity}/[controller]")]
    public class TenantHabilitationController : ControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly DianHabilitationScraperService _scraperService;
        private readonly Fel.Core.Interfaces.IMessageQueue _messageQueue;

        // Cola que consume HabilitationWorker (Fel.Worker) para correr el set de pruebas completo.
        private const string HabilitationQueue = "fel:habilitation:queue";

        private readonly DianTestSetSubmissionService _submissionService;

        public TenantHabilitationController(
            FelDbContext dbContext,
            DianHabilitationScraperService scraperService,
            Fel.Core.Interfaces.IMessageQueue messageQueue,
            DianTestSetSubmissionService submissionService)
        {
            _dbContext = dbContext;
            _scraperService = scraperService;
            _messageQueue = messageQueue;
            _submissionService = submissionService;
        }

        // Todo este flujo aplica SOLO a clientes que facturan con NUESTRO software propio ante la
        // DIAN (IntegratorKind.DirectDian). Cuando el cliente opera a través de un proveedor
        // tecnológico, la habilitación, la numeración y la asociación de prefijos las tramita ese
        // proveedor: correr el flujo ahí registraría un software que el cliente no usa y, sobre
        // todo, el paso de migrar el prefijo le quitaría la facturación a su proveedor real.
        private static ObjectResult? BloqueoSiNoEsDirectoDian(Fel.Core.Entities.Client client)
        {
            if (client.Integrator?.Kind == Fel.Core.Entities.IntegratorKind.DirectDian)
                return null;

            var proveedor = string.IsNullOrWhiteSpace(client.Integrator?.Name) ? "un proveedor tecnológico" : client.Integrator!.Name;
            return new BadRequestObjectResult(new
            {
                message = $"Este cliente factura a través de {proveedor}, no con software propio. La habilitación, la numeración y la asociación de prefijos las tramita ese proveedor.",
                integratorKind = client.Integrator?.Kind.ToString()
            });
        }

        public class HabilitationRequestDto
        {
            /// <summary>
            /// El enlace de acceso que la DIAN envió al correo del representante legal.
            /// Ejemplo: https://catalogo-vpfe.dian.gov.co/User/Login?token=...
            /// </summary>
            public string MagicLink { get; set; } = string.Empty;

            /// <summary>
            /// El identificador único del Software Propio registrado en la DIAN.
            /// </summary>
            public string SoftwareId { get; set; } = string.Empty;

            /// <summary>
            /// El PIN de asociación del Software Propio.
            /// </summary>
            public string SoftwarePin { get; set; } = string.Empty;

            /// <summary>
            /// El NIT del cliente que se está habilitando (sin dígito de verificación).
            /// </summary>
            public string Nit { get; set; } = string.Empty;
        }

        /// <summary>
        /// Automatiza la extracción del TestSetId y la ejecución del Set de Pruebas de la DIAN usando el Magic Link.
        /// </summary>
        [HttpPost("auto")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> StartAutoHabilitation([FromRoute] string country, [FromRoute] string entity, [FromBody] HabilitationRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.MagicLink))
                return BadRequest(new { message = "El MagicLink es requerido." });

            if (string.IsNullOrWhiteSpace(request.Nit))
                return BadRequest(new { message = "El Nit del cliente es requerido para registrar el progreso." });

            if (!Guid.TryParse(entity, out var tenantGuid))
                return BadRequest(new { message = "El entity (TenantId) no es un GUID válido." });

            // 1. Buscar al cliente en la BD
            var client = await _dbContext.Clients.Include(c => c.Integrator).FirstOrDefaultAsync(c => c.TenantId == tenantGuid && c.TaxId == request.Nit);
            
            if (client == null)
            {
                return NotFound(new { message = $"No se encontró un cliente con NIT {request.Nit} bajo el tenant {entity}." });
            }

            var bloqueo = BloqueoSiNoEsDirectoDian(client);
            if (bloqueo != null) return bloqueo;

            // Actualizar estado a IN PROCESS
            client.DianHabilitationStatus = "Processing";
            client.DianHabilitationProgress = 10;
            client.DianHabilitationMessage = "Registrando software propio ante la DIAN...";
            await _dbContext.SaveChangesAsync();

            // 2. Ejecutar Scraper: asocia "Software propio" en habilitación (si no estaba) y lee del
            // portal real el TestSetId, la llave técnica y el rango de prueba asignados.
            var softwareName = $"FF - {client.CompanyName}";
            var scrapeResult = await _scraperService.RegisterSoftwareAndReadTestSetAsync(request.MagicLink, softwareName, client.SoftwareId);

            if (!scrapeResult.IsSuccess)
            {
                client.DianHabilitationStatus = "Failed";
                client.DianHabilitationProgress = 0;
                client.DianHabilitationMessage = scrapeResult.ErrorMessage;
                await _dbContext.SaveChangesAsync();

                return StatusCode(StatusCodes.Status502BadGateway, new
                {
                    message = "Fallo en la automatización del portal DIAN",
                    detail = scrapeResult.ErrorMessage
                });
            }

            // 3. Persistir lo que la DIAN devolvió y la resolución de prueba (DocumentType "FE-TEST").
            if (!string.IsNullOrEmpty(scrapeResult.SoftwareId)) client.SoftwareId = scrapeResult.SoftwareId;
            if (!string.IsNullOrEmpty(scrapeResult.SoftwarePin)) client.SoftwarePin = scrapeResult.SoftwarePin;
            client.TestSetId = scrapeResult.TestSetId;

            var testResolution = await _dbContext.Resolutions
                .FirstOrDefaultAsync(r => r.ClientId == client.Id && r.DocumentType == "FE-TEST");

            if (testResolution == null)
            {
                testResolution = new Fel.Core.Entities.Resolution { Id = Guid.NewGuid(), ClientId = client.Id, DocumentType = "FE-TEST" };
                _dbContext.Resolutions.Add(testResolution);
            }

            testResolution.Prefix = scrapeResult.Prefix;
            testResolution.ResolutionNumber = scrapeResult.ResolutionNumber;
            testResolution.TechnicalKey = scrapeResult.TechnicalKey;
            testResolution.NumberStart = scrapeResult.RangeFromNumber;
            testResolution.NumberEnd = scrapeResult.RangeToNumber;
            testResolution.NextNumber = null;
            testResolution.ValidFrom = scrapeResult.ValidFrom;
            testResolution.ValidTo = scrapeResult.ValidTo;
            testResolution.IsActive = true;
            testResolution.IsDefault = false;

            // El set de pruebas no puede arrancar sin certificado: los documentos van firmados. Si ya
            // está cargado se encola de una y el proceso sigue solo; si no, se deja el cliente
            // esperando y el portal lo dispara después con POST run-test-set.
            var tieneCertificado = await _dbContext.Certificates.AnyAsync(c => c.ClientId == client.Id && c.IsActive);

            client.DianHabilitationProgress = 30;
            client.DianHabilitationMessage = tieneCertificado
                ? $"Set de pruebas listo ({scrapeResult.RequiredInvoices} intentos de factura disponibles). Iniciando envíos..."
                : $"Set de pruebas listo: {scrapeResult.RequiredAcceptedInvoices} factura(s) aceptada(s) requerida(s) de hasta {scrapeResult.RequiredInvoices} intentos. Falta el certificado digital para poder enviarlas.";
            await _dbContext.SaveChangesAsync();

            if (tieneCertificado)
                await _messageQueue.EnqueueAsync(HabilitationQueue, new { ClientId = client.Id });

            return Accepted(new
            {
                message = tieneCertificado
                    ? "Software propio registrado. El set de pruebas ya está corriendo en segundo plano."
                    : "Software propio registrado y set de pruebas leído exitosamente. Carga el certificado digital para iniciar los envíos.",
                testSetRunning = tieneCertificado,
                testSetId = scrapeResult.TestSetId,
                prefix = scrapeResult.Prefix,
                requiredInvoices = scrapeResult.RequiredInvoices,
                requiredCreditNotes = scrapeResult.RequiredCreditNotes,
                requiredDebitNotes = scrapeResult.RequiredDebitNotes,
                requiredAcceptedInvoices = scrapeResult.RequiredAcceptedInvoices,
                statusUrl = $"/api/v1/{country}/{entity}/TenantHabilitation/status/{request.Nit}"
            });
        }

        /// <summary>
        /// Inicia (o reintenta) el set de pruebas completo de la DIAN para un cliente que ya tiene
        /// el software propio registrado y el certificado digital cargado. El proceso corre en
        /// segundo plano; el avance se consulta con GET status/{nit}.
        /// </summary>
        [HttpPost("run-test-set/{nit}")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RunTestSet([FromRoute] string country, [FromRoute] string entity, [FromRoute] string nit)
        {
            if (!Guid.TryParse(entity, out var tenantGuid))
                return BadRequest(new { message = "El entity (TenantId) no es un GUID válido." });

            var client = await _dbContext.Clients.Include(c => c.Integrator).FirstOrDefaultAsync(c => c.TenantId == tenantGuid && c.TaxId == nit);
            if (client == null)
                return NotFound(new { message = $"No se encontró un cliente con NIT {nit} bajo el tenant {entity}." });

            var bloqueo = BloqueoSiNoEsDirectoDian(client);
            if (bloqueo != null) return bloqueo;

            if (string.IsNullOrWhiteSpace(client.TestSetId))
                return BadRequest(new { message = "Este cliente todavía no tiene un set de pruebas. Registra primero el software propio con el enlace mágico." });

            if (!await _dbContext.Certificates.AnyAsync(c => c.ClientId == client.Id && c.IsActive))
                return BadRequest(new { message = "Este cliente no tiene un certificado digital activo. Los documentos del set de pruebas van firmados, así que es requisito." });

            if (client.DianHabilitationStatus == "Processing")
                return Accepted(new { message = "El set de pruebas ya está corriendo.", statusUrl = $"/api/v1/{country}/{entity}/TenantHabilitation/status/{nit}" });

            client.DianHabilitationStatus = "Processing";
            client.DianHabilitationMessage = "Set de pruebas encolado. Iniciando envíos...";
            await _dbContext.SaveChangesAsync();

            await _messageQueue.EnqueueAsync(HabilitationQueue, new { ClientId = client.Id });

            return Accepted(new
            {
                message = "Set de pruebas iniciado en segundo plano.",
                statusUrl = $"/api/v1/{country}/{entity}/TenantHabilitation/status/{nit}"
            });
        }

        public class DiagnosticoRequestDto
        {
            /// <summary>Enlace mágico del portal de HABILITACIÓN.</summary>
            public string MagicLinkHabilitacion { get; set; } = string.Empty;
            /// <summary>Enlace mágico del portal de PRODUCCIÓN.</summary>
            public string MagicLinkProduccion { get; set; } = string.Empty;
        }

        public class PasoDelPlan
        {
            public string Paso { get; set; } = string.Empty;
            /// <summary>"hecho", "pendiente" o "bloqueado".</summary>
            public string Estado { get; set; } = string.Empty;
            public string Detalle { get; set; } = string.Empty;
            public string? Advertencia { get; set; }
        }

        /// <summary>
        /// Lee el estado real del cliente en los dos portales de la DIAN y lo cruza con lo que
        /// tenemos guardado, devolviendo el plan de lo que falta. Es SOLO LECTURA: no registra, no
        /// envía documentos y no asocia nada. Pensado para mostrarle al tenant qué va a pasar antes
        /// de que autorice ejecutarlo.
        /// </summary>
        [HttpPost("diagnostico/{nit}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Diagnosticar([FromRoute] string country, [FromRoute] string entity, [FromRoute] string nit, [FromBody] DiagnosticoRequestDto request)
        {
            if (!Guid.TryParse(entity, out var tenantGuid))
                return BadRequest(new { message = "El entity (TenantId) no es un GUID válido." });

            var client = await _dbContext.Clients.Include(c => c.Integrator).FirstOrDefaultAsync(c => c.TenantId == tenantGuid && c.TaxId == nit);
            if (client == null)
                return NotFound(new { message = $"No se encontró un cliente con NIT {nit} bajo el tenant {entity}." });

            var bloqueo = BloqueoSiNoEsDirectoDian(client);
            if (bloqueo != null) return bloqueo;

            var diag = await _scraperService.DiagnosticarAsync(
                request.MagicLinkHabilitacion, request.MagicLinkProduccion, client.SoftwareId);

            if (!diag.IsSuccess)
                return BadRequest(new { message = diag.ErrorMessage });

            var tieneCertificado = await _dbContext.Certificates.AnyAsync(c => c.ClientId == client.Id && c.IsActive);
            var resolucionesLocales = await _dbContext.Resolutions
                .Where(r => r.ClientId == client.Id && r.IsActive && r.DocumentType != "FE-TEST")
                .ToListAsync();

            var plan = new List<PasoDelPlan>();

            plan.Add(new PasoDelPlan
            {
                Paso = "certificado",
                Estado = tieneCertificado ? "hecho" : "bloqueado",
                Detalle = tieneCertificado
                    ? "Certificado digital activo."
                    : "Falta cargar el certificado digital. Sin él no se pueden firmar los documentos del set de pruebas."
            });

            plan.Add(new PasoDelPlan
            {
                Paso = "registrar-software",
                Estado = diag.NuestroSoftware != null ? "hecho" : "pendiente",
                Detalle = diag.NuestroSoftware != null
                    ? $"\"{diag.NuestroSoftware.NombreSoftware}\" registrado el {diag.NuestroSoftware.FechaRegistro}."
                    : "Nuestro software propio no está registrado en habilitación.",
                // Un software propio ajeno no nos estorba, pero conviene que el tenant sepa que está.
                Advertencia = diag.NuestroSoftware == null && diag.ModosEnHabilitacion.Any(m => m.Modo.Equals("Software propio", StringComparison.OrdinalIgnoreCase))
                    ? "El contribuyente ya tiene un software propio registrado que no es el nuestro. Se creará uno aparte, no se va a reutilizar el existente."
                    : null
            });

            var setAprobado = diag.NuestroSoftware?.Estado.Equals("Aceptado", StringComparison.OrdinalIgnoreCase) == true;
            plan.Add(new PasoDelPlan
            {
                Paso = "set-de-pruebas",
                Estado = setAprobado ? "hecho" : "pendiente",
                Detalle = setAprobado
                    ? "Set de pruebas superado."
                    : diag.NuestroSoftware == null
                        ? "Se ejecutará después de registrar el software."
                        : $"Software en estado \"{diag.NuestroSoftware.Estado}\": falta completar el set de pruebas."
            });

            plan.Add(new PasoDelPlan
            {
                Paso = "sincronizar-produccion",
                Estado = diag.SincronizadoAProduccion ? "hecho" : "pendiente",
                Detalle = diag.SincronizadoAProduccion
                    ? $"Contribuyente en producción desde {diag.FechaInicioProduccion}."
                    : "El contribuyente nunca se ha sincronizado a producción."
            });

            // Una entrada por resolución: es donde está el detalle que el tenant necesita ver antes
            // de autorizar, porque migrar un prefijo deja sin facturar al software que lo tenía.
            foreach (var resolucion in resolucionesLocales)
            {
                var enDian = diag.PrefijosAsociados.FirstOrDefault(p =>
                    p.Prefijo.Equals(resolucion.Prefix, StringComparison.OrdinalIgnoreCase) &&
                    p.NumeroResolucion == resolucion.ResolutionNumber);

                var esNuestro = enDian != null && enDian.SoftwareId.Equals(client.SoftwareId, StringComparison.OrdinalIgnoreCase);

                plan.Add(new PasoDelPlan
                {
                    Paso = $"asociar-resolucion:{resolucion.Prefix}",
                    Estado = esNuestro ? "hecho" : enDian != null ? "pendiente" : "bloqueado",
                    Detalle = esNuestro
                        ? $"{resolucion.Prefix} ({resolucion.ResolutionNumber}) ya está asociada a nuestro software."
                        : enDian != null
                            ? $"{resolucion.Prefix} ({resolucion.ResolutionNumber}) está asociada a \"{enDian.NombreSoftware}\"."
                            : $"{resolucion.Prefix} ({resolucion.ResolutionNumber}) está registrada en nuestro sistema pero no aparece en la DIAN.",
                    Advertencia = !esNuestro && enDian != null
                        ? $"Al migrarla, \"{enDian.NombreSoftware}\" dejará de poder facturar con este rango. Coordínalo con el cliente antes."
                        : enDian == null
                            ? "Verifica el número de resolución y el prefijo contra el portal de la DIAN."
                            : null
                });
            }

            // Resoluciones que la DIAN conoce y nosotros no: el cliente no podría emitir con ellas.
            var faltantesEnNuestraBase = diag.PrefijosAsociados
                .Where(p => !resolucionesLocales.Any(r =>
                    r.Prefix.Equals(p.Prefijo, StringComparison.OrdinalIgnoreCase) &&
                    r.ResolutionNumber == p.NumeroResolucion))
                .Select(p => new { p.Prefijo, p.NumeroResolucion, p.NombreSoftware, p.FechaExpiracion })
                .ToList();

            return Ok(new
            {
                cliente = new { client.CompanyName, client.TaxId, estado = client.DianHabilitationStatus },
                dian = new
                {
                    diag.ContributorId,
                    diag.EstadoAprobacion,
                    diag.FechaInicioProduccion,
                    diag.SincronizadoAProduccion,
                    modosEnHabilitacion = diag.ModosEnHabilitacion,
                    modosEnProduccion = diag.ModosEnProduccion,
                    prefijos = diag.PrefijosAsociados
                },
                plan,
                resumen = new
                {
                    pendientes = plan.Count(p => p.Estado == "pendiente"),
                    bloqueados = plan.Count(p => p.Estado == "bloqueado"),
                    advertencias = plan.Count(p => p.Advertencia != null)
                },
                resolucionesEnDianQueNoTenemos = faltantesEnNuestraBase
            });
        }

        /// <summary>
        /// Sincroniza el contribuyente al ambiente de producción de la DIAN. Se ejecuta DESPUÉS de
        /// superar el set de pruebas y ANTES de asociar los prefijos, y usa el enlace mágico de
        /// HABILITACIÓN (el botón vive en la ficha del contribuyente de ese portal).
        /// </summary>
        [HttpPost("sync-production/{nit}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SincronizarAProduccion([FromRoute] string country, [FromRoute] string entity, [FromRoute] string nit, [FromBody] HabilitationRequestDto request)
        {
            if (!Guid.TryParse(entity, out var tenantGuid))
                return BadRequest(new { message = "El entity (TenantId) no es un GUID válido." });

            var client = await _dbContext.Clients.Include(c => c.Integrator).FirstOrDefaultAsync(c => c.TenantId == tenantGuid && c.TaxId == nit);
            if (client == null)
                return NotFound(new { message = $"No se encontró un cliente con NIT {nit} bajo el tenant {entity}." });

            var bloqueo = BloqueoSiNoEsDirectoDian(client);
            if (bloqueo != null) return bloqueo;

            if (client.DianHabilitationStatus != "Passed" && client.DianHabilitationStatus != "Production")
                return BadRequest(new { message = "El cliente todavía no ha superado el set de pruebas. No tiene sentido sincronizarlo a producción." });

            if (!DianHabilitationScraperService.EsEnlaceValido(request.MagicLink))
                return BadRequest(new { message = "El enlace no parece un enlace mágico válido de la DIAN." });

            if (DianHabilitationScraperService.EsEnlaceDeProduccion(request.MagicLink))
                return BadRequest(new { message = "Para sincronizar se necesita el enlace mágico de HABILITACIÓN (catalogo-vpfe-hab.dian.gov.co): el botón vive en la ficha del contribuyente de ese portal." });

            var resultado = await _scraperService.SincronizarContribuyenteAProduccionAsync(request.MagicLink);
            if (!resultado.IsSuccess)
                return BadRequest(new { message = resultado.ErrorMessage });

            client.DianHabilitationMessage = "Contribuyente sincronizado a producción. Falta asociar las resoluciones al software.";
            await _dbContext.SaveChangesAsync();

            return Ok(new
            {
                message = "Contribuyente sincronizado a producción.",
                siguientePaso = $"POST /api/v1/{country}/{entity}/TenantHabilitation/production/{nit} con el enlace mágico de PRODUCCIÓN para asociar las resoluciones."
            });
        }

        public class ProduccionRequestDto
        {
            /// <summary>
            /// Enlace mágico del portal de PRODUCCIÓN (catalogo-vpfe.dian.gov.co). Es distinto del
            /// que se usó para habilitación: son dos ambientes separados de la DIAN, cada uno con su
            /// propio enlace.
            /// </summary>
            public string MagicLink { get; set; } = string.Empty;

            /// <summary>
            /// Resoluciones del cliente que se asocian al software en producción. La DIAN permite
            /// asociar una o varias al mismo software (por ejemplo, prefijos distintos), así que la
            /// selección es explícita: no se asumen todas las que el cliente tenga cargadas.
            /// </summary>
            public List<Guid> ResolutionIds { get; set; } = new();

            /// <summary>
            /// Autoriza desasociar el prefijo del software que lo tenga hoy (por ejemplo, el
            /// proveedor tecnológico anterior). Es una acción sobre producción real: ese software
            /// deja de poder facturar con ese rango, así que el tenant debe coordinarlo con el
            /// cliente antes. Por eso viene en false salvo que se pida explícitamente.
            /// </summary>
            public bool DesasociarDeOtroSoftware { get; set; }
        }

        public class ResultadoAsociacion
        {
            public Guid Id { get; set; }
            public string Prefix { get; set; } = string.Empty;
            public string ResolutionNumber { get; set; } = string.Empty;
            public bool Asociada { get; set; }
            public bool YaEstaba { get; set; }
            public string SoftwareAnterior { get; set; } = string.Empty;
            public bool Desasociado { get; set; }
            public string Error { get; set; } = string.Empty;
        }

        /// <summary>
        /// Pasa el cliente a producción: exige el set de pruebas superado, un enlace mágico del
        /// portal de producción y al menos una resolución de facturación real asignada.
        /// </summary>
        [HttpPost("production/{nit}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> PasarAProduccion([FromRoute] string country, [FromRoute] string entity, [FromRoute] string nit, [FromBody] ProduccionRequestDto request)
        {
            if (!Guid.TryParse(entity, out var tenantGuid))
                return BadRequest(new { message = "El entity (TenantId) no es un GUID válido." });

            var client = await _dbContext.Clients.Include(c => c.Integrator).FirstOrDefaultAsync(c => c.TenantId == tenantGuid && c.TaxId == nit);
            if (client == null)
                return NotFound(new { message = $"No se encontró un cliente con NIT {nit} bajo el tenant {entity}." });

            var bloqueo = BloqueoSiNoEsDirectoDian(client);
            if (bloqueo != null) return bloqueo;

            if (client.DianHabilitationStatus != "Passed")
                return BadRequest(new { message = "El cliente todavía no ha superado el set de pruebas. No se puede pasar a producción." });

            // Los dos ambientes de la DIAN son independientes y cada uno entrega su propio enlace
            // mágico. Exigir explícitamente el de producción evita el error silencioso de reutilizar
            // el de habilitación y creer que quedó en producción cuando no.
            if (!DianHabilitationScraperService.EsEnlaceValido(request.MagicLink))
                return BadRequest(new { message = "El enlace no parece un enlace mágico válido de la DIAN." });

            if (!DianHabilitationScraperService.EsEnlaceDeProduccion(request.MagicLink))
                return BadRequest(new { message = "Este es el enlace del ambiente de habilitación. Para pasar a producción se necesita el enlace mágico del portal de producción (catalogo-vpfe.dian.gov.co)." });

            // Sin resolución de facturación real no hay numeración válida con la cual emitir: la
            // resolución de pruebas (FE-TEST) solo sirve dentro del set de pruebas.
            var disponibles = await _dbContext.Resolutions
                .Where(r => r.ClientId == client.Id && r.IsActive && r.DocumentType != "FE-TEST")
                .ToListAsync();

            if (disponibles.Count == 0)
                return BadRequest(new
                {
                    message = "Este cliente no tiene ninguna resolución de facturación activa. Asígnale la resolución autorizada por la DIAN antes de pasarlo a producción.",
                    resolutionsUrl = $"/api/tenant/clients/{client.Id}/resolutions"
                });

            if (request.ResolutionIds == null || request.ResolutionIds.Count == 0)
                return BadRequest(new
                {
                    message = "Debes seleccionar cuál o cuáles resoluciones se asocian al software en producción.",
                    disponibles = disponibles.Select(r => new { r.Id, r.DocumentType, r.Prefix, r.ResolutionNumber, r.NumberStart, r.NumberEnd, r.ValidTo, r.IsDefault })
                });

            var seleccionadas = disponibles.Where(r => request.ResolutionIds.Contains(r.Id)).ToList();

            var noEncontradas = request.ResolutionIds.Except(seleccionadas.Select(r => r.Id)).ToList();
            if (noEncontradas.Count > 0)
                return BadRequest(new
                {
                    message = "Alguna de las resoluciones seleccionadas no existe, no está activa o no pertenece a este cliente.",
                    noEncontradas
                });

            var vencidas = seleccionadas.Where(r => r.ValidTo < DateTime.UtcNow.Date).ToList();
            if (vencidas.Count > 0)
                return BadRequest(new
                {
                    message = "No se puede pasar a producción con resoluciones vencidas.",
                    vencidas = vencidas.Select(r => new { r.Id, r.Prefix, r.ResolutionNumber, r.ValidTo })
                });

            if (string.IsNullOrWhiteSpace(client.SoftwareId))
                return BadRequest(new { message = "El cliente no tiene SoftwareId registrado. Completa primero la habilitación." });

            // Asociar en la DIAN cada prefijo seleccionado a NUESTRO software. Sin esto el software
            // queda habilitado pero sin rango de numeración, y no puede emitir nada.
            var asociaciones = new List<ResultadoAsociacion>();
            foreach (var resolucion in seleccionadas)
            {
                var asociacion = await _scraperService.AsociarPrefijoAlSoftwareAsync(
                    request.MagicLink,
                    client.SoftwareId,
                    resolucion.Prefix,
                    resolucion.ResolutionNumber,
                    desasociarDeOtroSoftware: request.DesasociarDeOtroSoftware);

                asociaciones.Add(new ResultadoAsociacion
                {
                    Id = resolucion.Id,
                    Prefix = resolucion.Prefix,
                    ResolutionNumber = resolucion.ResolutionNumber,
                    Asociada = asociacion.IsSuccess,
                    YaEstaba = asociacion.YaEstabaAsociado,
                    SoftwareAnterior = asociacion.SoftwareAnterior,
                    Desasociado = asociacion.RequirioDesasociar,
                    Error = asociacion.ErrorMessage
                });
            }

            var fallidas = asociaciones.Count(a => !a.Asociada);
            if (fallidas > 0)
                return BadRequest(new
                {
                    message = $"{fallidas} de {seleccionadas.Count} resolución(es) no se pudieron asociar al software en la DIAN. El cliente NO quedó en producción.",
                    asociaciones
                });

            client.DianHabilitationStatus = "Production";
            client.DianHabilitationProgress = 100;
            client.DianHabilitationMessage = $"En producción con {seleccionadas.Count} resolución(es) asociada(s) al software.";
            await _dbContext.SaveChangesAsync();

            return Ok(new
            {
                message = "Cliente en producción con sus resoluciones asociadas al software en la DIAN.",
                asociaciones,
                resolutions = seleccionadas.Select(r => new { r.Id, r.DocumentType, r.Prefix, r.ResolutionNumber, r.NumberStart, r.NumberEnd, r.ValidTo, r.IsDefault }),
                // Pendiente: activar el modo producción en el portal de la DIAN con el scraper. Se
                // completa cuando podamos recorrer el portal de producción con un enlace real, igual
                // que se hizo con habilitación — no se arma a ciegas.
                dianProductionActivated = false
            });
        }

        /// <summary>
        /// Consulta el estado actual de la Auto-Habilitación de un cliente.
        /// </summary>
        [HttpGet("status/{nit}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetHabilitationStatus([FromRoute] string country, [FromRoute] string entity, [FromRoute] string nit)
        {
            if (!Guid.TryParse(entity, out var tenantGuid))
                return BadRequest(new { message = "El entity (TenantId) no es un GUID válido." });

            var client = await _dbContext.Clients.Include(c => c.Integrator).FirstOrDefaultAsync(c => c.TenantId == tenantGuid && c.TaxId == nit);
            if (client == null) return NotFound(new { message = "Cliente no encontrado" });

            return Ok(new
            {
                nit = client.TaxId,
                status = client.DianHabilitationStatus,
                progress = client.DianHabilitationProgress,
                message = client.DianHabilitationMessage
            });
        }
    }
}
