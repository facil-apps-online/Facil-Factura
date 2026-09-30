using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Fel.Api.Superadmin.Controllers
{
    [ApiController]
    [Route("api/superadmin/tenants")]
    public class SuperadminTenantsController : ControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly Fel.Core.Interfaces.ICoreApiClient _core;
        private readonly ILogger<SuperadminTenantsController> _logger;
        private readonly PasswordResetService _passwordResetService;
        private readonly Fel.Infrastructure.Services.DianRutParserService _rutParser;
        private readonly string _tenantPortalUrl;

        public SuperadminTenantsController(
            FelDbContext dbContext,
            Fel.Core.Interfaces.ICoreApiClient core,
            ILogger<SuperadminTenantsController> logger,
            PasswordResetService passwordResetService,
            Fel.Infrastructure.Services.DianRutParserService rutParser,
            IConfiguration config)
        {
            _dbContext = dbContext;
            _core = core;
            _logger = logger;
            _passwordResetService = passwordResetService;
            _rutParser = rutParser;
            _tenantPortalUrl = config["TenantPortalUrl"] ?? "https://tenants.facil-factura.pro";
        }

        /// <summary>
        /// Lee un RUT (Formulario 001 de la DIAN) y devuelve sus datos para prellenar el alta de un
        /// tenant. No crea nada: el superadmin revisa y confirma en el formulario.
        /// </summary>
        [HttpPost("parse-rut")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ParseRut([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No se adjuntó ningún archivo." });

            if (!string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "El RUT debe ser el PDF que descarga el portal de la DIAN." });

            using var stream = file.OpenReadStream();
            var rut = await _rutParser.ParsePdfAsync(stream);

            if (!rut.IsSuccess)
                return BadRequest(new { message = rut.ErrorMessage });

            // El NIT ya en uso es lo único que se valida acá: evita que el superadmin llene todo
            // el formulario para que el guardado falle al final por un tenant duplicado.
            var yaExiste = await _dbContext.Tenants.AnyAsync(t => t.TaxId == rut.TaxId);

            return Ok(new
            {
                taxId = rut.TaxId,
                verificationDigit = rut.VerificationDigit,
                name = rut.LegalName,
                legalName = rut.LegalName,
                commercialName = rut.CommercialName,
                firstName = rut.FirstName,
                secondName = rut.SecondName,
                firstLastName = rut.FirstLastName,
                secondLastName = rut.SecondLastName,
                address = rut.Address,
                city = rut.City,
                department = rut.Department,
                country = rut.Country,
                email = rut.Email,
                phone = rut.Phone,
                economicActivity = rut.EconomicActivity,
                taxRegime = RegimenDesdeResponsabilidades(rut.ResponsibilityCodes),
                responsibilities = rut.Responsibilities,
                alreadyRegistered = yaExiste
            });
        }

        /// <summary>
        /// Deduce el régimen tributario de las responsabilidades del RUT: la 47 es Régimen Simple
        /// y la 48 marca responsable de IVA. Es solo una sugerencia para el formulario.
        /// </summary>
        private static string RegimenDesdeResponsabilidades(List<string> codigos)
        {
            if (codigos.Contains("47")) return "Régimen Simple de Tributación";
            if (codigos.Contains("48")) return "Responsable de IVA";
            return "No responsable de IVA";
        }

        [HttpGet]
        public async Task<IActionResult> GetTenants()
        {
            var tenants = await _dbContext.Tenants
                .Select(t => new
                {
                    t.Id,
                    t.Name,
                    t.CommercialName,
                    t.Slug,
                    t.IsActive,
                    t.CreatedAt,
                    t.ParentTenantId
                })
                .ToListAsync();

            return Ok(tenants);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetTenant(Guid id)
        {
            var tenant = await _dbContext.Tenants.FindAsync(id);
            if (tenant == null) return NotFound();
            return Ok(Project(tenant));
        }

        // Core exige el slug en formato [a-z0-9]+(-[a-z0-9]+)* (constraint valid_slug_format).
        // El formulario ya lo normaliza, pero esta es la última barrera antes de llamar a Core:
        // un slug inválido ahí revienta la creación después de ya haber reservado el registro local.
        private static string NormalizeSlug(string value)
        {
            var withoutDiacritics = value.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder();
            foreach (var c in withoutDiacritics)
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }

            var slug = System.Text.RegularExpressions.Regex.Replace(sb.ToString().ToLowerInvariant(), "[^a-z0-9]+", "-");
            return slug.Trim('-');
        }

        [HttpPost]
        public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequest request)
        {
            request.Slug = NormalizeSlug(request.Slug);
            if (string.IsNullOrEmpty(request.Slug))
            {
                return BadRequest("El slug no puede quedar vacío después de normalizarlo.");
            }

            if (await _dbContext.Tenants.AnyAsync(t => t.Slug == request.Slug))
            {
                return BadRequest("El slug ya está en uso.");
            }

            // Si el correo del admin ya existe, se reutiliza esa identidad (misma persona,
            // administrando otro tenant) en vez de rechazar el alta. Antes esto bloqueaba, por
            // ejemplo, dar de alta en un tenant nuevo a alguien a quien se le acababa de revocar
            // el acceso en otro: su correo quedaba ocupado para siempre. TenantUser.Email es
            // único a nivel de base (ver FelDbContext) precisamente porque el login del portal de
            // tenants resuelve solo por correo, sin pedir slug — por eso no se crea un TenantUser
            // nuevo con el mismo correo, se reutiliza el existente y se le agrega una asignación.
            TenantUser? adminExistente = null;
            if (!string.IsNullOrWhiteSpace(request.AdminEmail))
            {
                adminExistente = await _dbContext.TenantUsers
                    .FirstOrDefaultAsync(u => u.Email == request.AdminEmail);
            }

            var tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                CommercialName = request.CommercialName,
                Email = request.Email,
                Slug = request.Slug,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                LegalName = request.LegalName,
                FirstName = request.FirstName,
                SecondName = request.SecondName,
                FirstLastName = request.FirstLastName,
                SecondLastName = request.SecondLastName,
                // Columnas NOT NULL en Tenants: el DTO las expone como opcionales.
                TaxId = request.TaxId ?? string.Empty,
                VerificationDigit = request.VerificationDigit ?? string.Empty,
                ContactPerson = request.ContactPerson,
                ContactEmail = request.ContactEmail,
                ContactPhone = request.ContactPhone,
                WhatsAppPhone = request.WhatsAppPhone,
                EinvoicingEmail = request.EinvoicingEmail,
                CommercialEmail = request.CommercialEmail,
                Website = request.Website,
                PhysicalAddressLine1 = request.PhysicalAddressLine1,
                PhysicalAddressLine2 = request.PhysicalAddressLine2,
                PhysicalCity = request.PhysicalCity,
                PhysicalState = request.PhysicalState,
                PhysicalPostalCode = request.PhysicalPostalCode,
                BillingAddress = request.BillingAddress,
                DefaultLanguageCode = string.IsNullOrWhiteSpace(request.DefaultLanguageCode) ? "es-CO" : request.DefaultLanguageCode,
                DefaultTimezone = string.IsNullOrWhiteSpace(request.DefaultTimezone) ? "America/Bogota" : request.DefaultTimezone,
                DefaultCurrencyId = request.DefaultCurrencyId,
                Latitude = request.Latitude,
                Longitude = request.Longitude
            };

            // Todo el alta va en una transacción y solo se confirma si Core responde bien.
            // Antes se confirmaba antes de llamar a Core, así que un fallo allí dejaba el
            // tenant local huérfano y la petición devolvía 200 igualmente.
            // Se mantiene abierta durante la llamada HTTP a Core: el volumen de altas es
            // bajo y la alternativa es volver a tener escrituras sin correlación.
            await using var tx = await _dbContext.Database.BeginTransactionAsync();

            _dbContext.Tenants.Add(tenant);

            // Todo Tenant nuevo arranca con acceso a la emisión directa DIAN — cualquier otro
            // integrador (Dataico, etc.) queda oculto hasta que Superadmin lo habilite
            // explícitamente para este Tenant (ver SuperadminTenantIntegratorsController).
            var nativeIntegrator = await _dbContext.Integrators.FirstOrDefaultAsync(i => i.Code == "NATIVE");
            if (nativeIntegrator != null)
            {
                _dbContext.TenantEnabledIntegrators.Add(new TenantEnabledIntegrator
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    IntegratorId = nativeIntegrator.Id
                });
            }

            await _dbContext.SaveChangesAsync();

            // Crear (o reutilizar) el usuario administrador del tenant si se proporcionó.
            // Sin contraseña manual: igual que CreateTenantUser/UpsertPortalUser/InviteDeveloper,
            // se crea con un hash aleatorio inutilizable y se invita por correo a que la persona
            // establezca la suya. La invitación se manda después de confirmar la transacción (ver
            // más abajo) — mandarla antes arriesgaría invitar a un tenant que termina
            // descartándose si Core falla.
            TenantUser? adminNuevo = null;
            if (!string.IsNullOrWhiteSpace(request.AdminEmail))
            {
                TenantUser admin;
                if (adminExistente != null)
                {
                    // La identidad ya existe: no se toca su nombre ni su contraseña — son de la
                    // persona, no de este tenant — solo se le agrega acceso al nuevo. No aplica
                    // invitación: ya tiene credenciales propias.
                    admin = adminExistente;

                    // Si esta identidad quedó desactivada por una revocación de la época en que
                    // "revocar" apagaba la cuenta completa (antes de TenantUserAssignments), se
                    // reactiva acá: se le está otorgando acceso a un tenant nuevo a propósito, así
                    // que no tiene sentido que la asignación quede activa y el login bloqueado por
                    // un IsActive de identidad que nadie volvió a tocar.
                    admin.IsActive = true;
                }
                else
                {
                    admin = new TenantUser
                    {
                        Id = Guid.NewGuid(),
                        // TenantId queda como el tenant por defecto (el que se abre al entrar
                        // cuando la persona tiene acceso a varios).
                        TenantId = tenant.Id,
                        Name = string.IsNullOrWhiteSpace(request.AdminName) ? request.AdminEmail : request.AdminName,
                        Email = request.AdminEmail,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    _dbContext.TenantUsers.Add(admin);
                    await _dbContext.SaveChangesAsync();
                    adminNuevo = admin;
                }

                _dbContext.TenantUserAssignments.Add(new TenantUserAssignment
                {
                    Id = Guid.NewGuid(),
                    TenantUserId = admin.Id,
                    TenantId = tenant.Id,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
                await _dbContext.SaveChangesAsync();
            }

            // Dualidad con Core: crear el tenant comercial en la base Core (Supabase)
            var coreResult = await _core.CreateTenantAsync(new Core.Interfaces.CoreTenantCreate
            {
                Name = tenant.Name,
                Slug = tenant.Slug,
                LegalName = tenant.LegalName,
                TaxId = tenant.TaxId,
                ContactPhone = tenant.ContactPhone,
                WhatsAppPhone = tenant.WhatsAppPhone,
                EinvoicingEmail = tenant.EinvoicingEmail,
                CommercialEmail = tenant.CommercialEmail,
                Website = tenant.Website,
                PhysicalAddressLine1 = tenant.PhysicalAddressLine1,
                PhysicalAddressLine2 = tenant.PhysicalAddressLine2,
                PhysicalCity = tenant.PhysicalCity,
                PhysicalState = tenant.PhysicalState,
                PhysicalPostalCode = tenant.PhysicalPostalCode,
                BillingAddress = tenant.BillingAddress,
                DefaultLanguageCode = tenant.DefaultLanguageCode,
                DefaultTimezone = tenant.DefaultTimezone,
                DefaultCurrencyId = tenant.DefaultCurrencyId,
                CountryId = request.CountryId,
                Latitude = tenant.Latitude,
                Longitude = tenant.Longitude
            });
            if (coreResult.IsFailed)
            {
                await tx.RollbackAsync();
                return StatusCode(StatusCodes.Status502BadGateway,
                    $"No se pudo crear el tenant en Core, se descartó el alta. Detalle: {coreResult.Error}");
            }

            if (coreResult.IsSuccess && coreResult.Value?.Id != null)
            {
                tenant.CoreTenantId = coreResult.Value.Id;
                await _dbContext.SaveChangesAsync();
            }

            try
            {
                await tx.CommitAsync();
            }
            catch
            {
                // Core ya confirmó pero el commit local falló. Si la ficha la creamos nosotros,
                // se deshace; si fue adoptada, NO se toca: es preexistente y borrarla destruiría
                // datos ajenos al alta.
                if (coreResult.IsSuccess && coreResult.Value is { Adopted: false, Id: not null } created)
                {
                    var undo = await _core.DeleteTenantAsync(created.Id);
                    if (!undo.IsSuccess)
                    {
                        _logger.LogError(
                            "Commit local falló y no se pudo revertir la ficha {CoreId} en Core: {Error}. Requiere limpieza manual.",
                            created.Id, undo.Error);
                    }
                }
                throw;
            }

            // Recién aquí, con la transacción ya confirmada, es seguro invitar: si el commit
            // hubiera fallado arriba, este código nunca se alcanza y no se manda nada para un
            // tenant que terminó descartado.
            if (adminNuevo != null)
            {
                await _passwordResetService.RequestAsync(
                    PortalUserType.Tenant, adminNuevo.Id, adminNuevo.Email, adminNuevo.Name, _tenantPortalUrl, "invitation",
                    tenant.CoreTenantId, tenant.LogoLightUrl, tenant.CommercialName);
            }

            return Ok(new
            {
                tenant = Project(tenant),
                coreSync = new
                {
                    status = coreResult.Outcome.ToString(),
                    coreTenantId = tenant.CoreTenantId,
                    adopted = coreResult.Value?.Adopted ?? false
                }
            });
        }

        /// <summary>
        /// Proyecta el tenant a un objeto plano para la respuesta.
        /// Nunca devolver la entidad directamente: al crear el usuario administrador, EF
        /// enlaza las navegaciones (tenant.Users -> user.Tenant -> tenant) y System.Text.Json
        /// entra en ciclo, tumbando la respuesta con la transaccion ya confirmada. El cliente
        /// veria un fallo de red con el tenant realmente creado.
        /// </summary>
        private static object Project(Tenant t) => new
        {
            t.Id,
            t.Name,
            t.CommercialName,
            t.LegalName,
            t.Email,
            t.Slug,
            t.CoreTenantId,
            t.TaxId,
            t.VerificationDigit,
            t.ContactPerson,
            t.ContactEmail,
            t.ContactPhone,
            t.WhatsAppPhone,
            t.EinvoicingEmail,
            t.CommercialEmail,
            t.Website,
            t.PhysicalAddressLine1,
            t.PhysicalAddressLine2,
            t.PhysicalCity,
            t.PhysicalState,
            t.PhysicalPostalCode,
            t.BillingAddress,
            t.Address,
            t.City,
            t.Phone,
            t.TaxRegime,
            t.EconomicActivity,
            t.DefaultLanguageCode,
            t.DefaultTimezone,
            t.DefaultCurrencyId,
            t.Latitude,
            t.Longitude,
            t.IsActive,
            t.CreatedAt,
            t.ParentTenantId
        };

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateTenant(Guid id, [FromBody] UpdateTenantRequest request)
        {
            var tenant = await _dbContext.Tenants.FindAsync(id);
            if (tenant == null) return NotFound();

            // Grupo empresarial: un tenant no puede ser su propio padre, ni el padre de un tenant
            // que ya es su propio padre (ciclo de 2). No se valida más profundidad porque el caso
            // de uso real es de un solo nivel (ej. R&W agrupa a DGS).
            if (request.ParentTenantId.HasValue)
            {
                if (request.ParentTenantId.Value == id)
                {
                    return BadRequest("Un tenant no puede ser su propio grupo empresarial.");
                }

                var parent = await _dbContext.Tenants.FindAsync(request.ParentTenantId.Value);
                if (parent == null) return BadRequest("El tenant padre indicado no existe.");
                if (parent.ParentTenantId == id)
                {
                    return BadRequest("Esa asignación crearía un ciclo entre los dos tenants.");
                }
            }
            tenant.ParentTenantId = request.ParentTenantId;

            tenant.Name = request.Name;
            tenant.CommercialName = request.CommercialName;
            tenant.Email = request.Email;

            // Fiscal Info
            tenant.TaxId = request.TaxId;
            tenant.VerificationDigit = request.VerificationDigit;
            tenant.Address = request.Address;
            tenant.City = request.City;
            tenant.Phone = request.Phone;
            tenant.TaxRegime = request.TaxRegime;
            tenant.EconomicActivity = request.EconomicActivity;
            tenant.Latitude = request.Latitude;
            tenant.Longitude = request.Longitude;

            // Campos comerciales espejo de Core. Antes UpdateTenant los ignoraba, así que
            // toda edición divergía de Core de forma permanente.
            // Sólo se aplican si vienen informados: el formulario de edición actual no los envía
            // todos, y machacarlos con null vaciaría datos ya cargados.
            if (request.LegalName is not null) tenant.LegalName = request.LegalName;
            if (request.ContactPerson is not null) tenant.ContactPerson = request.ContactPerson;
            if (request.ContactEmail is not null) tenant.ContactEmail = request.ContactEmail;
            if (request.ContactPhone is not null) tenant.ContactPhone = request.ContactPhone;
            if (request.WhatsAppPhone is not null) tenant.WhatsAppPhone = request.WhatsAppPhone;
            if (request.EinvoicingEmail is not null) tenant.EinvoicingEmail = request.EinvoicingEmail;
            if (request.CommercialEmail is not null) tenant.CommercialEmail = request.CommercialEmail;
            if (request.Website is not null) tenant.Website = request.Website;
            if (request.PhysicalAddressLine1 is not null) tenant.PhysicalAddressLine1 = request.PhysicalAddressLine1;
            if (request.PhysicalAddressLine2 is not null) tenant.PhysicalAddressLine2 = request.PhysicalAddressLine2;
            if (request.PhysicalCity is not null) tenant.PhysicalCity = request.PhysicalCity;
            if (request.PhysicalState is not null) tenant.PhysicalState = request.PhysicalState;
            if (request.PhysicalPostalCode is not null) tenant.PhysicalPostalCode = request.PhysicalPostalCode;
            if (request.BillingAddress is not null) tenant.BillingAddress = request.BillingAddress;
            if (!string.IsNullOrWhiteSpace(request.DefaultLanguageCode)) tenant.DefaultLanguageCode = request.DefaultLanguageCode;
            if (!string.IsNullOrWhiteSpace(request.DefaultTimezone)) tenant.DefaultTimezone = request.DefaultTimezone;
            if (request.DefaultCurrencyId is not null) tenant.DefaultCurrencyId = request.DefaultCurrencyId;

            // El Slug no se edita: identifica la ficha en Core dentro de
            // unique_platform_country_slug y cambiarlo rompería el enlace.

            await using var tx = await _dbContext.Database.BeginTransactionAsync();
            await _dbContext.SaveChangesAsync();

            // Propagar a Core si el tenant está enlazado.
            var coreStatus = "Skipped";
            if (!string.IsNullOrWhiteSpace(tenant.CoreTenantId))
            {
                var coreResult = await _core.UpdateTenantAsync(tenant.CoreTenantId, ToCoreTenant(tenant, null));
                if (coreResult.IsFailed)
                {
                    await tx.RollbackAsync();
                    return StatusCode(StatusCodes.Status502BadGateway,
                        $"No se pudo actualizar el tenant en Core, se descartaron los cambios. Detalle: {coreResult.Error}");
                }
                coreStatus = coreResult.Outcome.ToString();
            }

            await tx.CommitAsync();

            return Ok(new { tenant = Project(tenant), coreSync = new { status = coreStatus, coreTenantId = tenant.CoreTenantId } });
        }

        /// <summary>
        /// Proyecta el tenant local al contrato comercial de Core. Un único sitio para el
        /// mapeo, usado tanto por el alta como por la actualización y la reparación.
        /// </summary>
        private static CoreTenantCreate ToCoreTenant(Tenant tenant, string? countryId) => new()
        {
            Name = tenant.Name,
            Slug = tenant.Slug,
            LegalName = tenant.LegalName,
            TaxId = tenant.TaxId,
            ContactPhone = tenant.ContactPhone,
            WhatsAppPhone = tenant.WhatsAppPhone,
            EinvoicingEmail = tenant.EinvoicingEmail,
            CommercialEmail = tenant.CommercialEmail,
            Website = tenant.Website,
            PhysicalAddressLine1 = tenant.PhysicalAddressLine1,
            PhysicalAddressLine2 = tenant.PhysicalAddressLine2,
            PhysicalCity = tenant.PhysicalCity,
            PhysicalState = tenant.PhysicalState,
            PhysicalPostalCode = tenant.PhysicalPostalCode,
            BillingAddress = tenant.BillingAddress,
            DefaultLanguageCode = tenant.DefaultLanguageCode,
            DefaultTimezone = tenant.DefaultTimezone,
            DefaultCurrencyId = tenant.DefaultCurrencyId,
            CountryId = countryId,
            Latitude = tenant.Latitude,
            Longitude = tenant.Longitude
        };

        /// <summary>
        /// Diagnóstico de la dualidad con Core: tenants sin enlazar y punteros colgantes.
        /// CoreTenantId es un nvarchar suelto y al ser bases distintas no puede haber clave
        /// foránea, así que nada garantiza que la ficha referenciada siga existiendo.
        /// </summary>
        [HttpGet("core-sync")]
        public async Task<IActionResult> GetCoreSyncReport(CancellationToken ct)
        {
            var tenants = await _dbContext.Tenants
                .Select(t => new { t.Id, t.Name, t.Slug, t.CoreTenantId })
                .ToListAsync(ct);

            var unlinked = tenants.Where(t => string.IsNullOrWhiteSpace(t.CoreTenantId)).ToList();
            var linked = tenants.Where(t => !string.IsNullOrWhiteSpace(t.CoreTenantId)).ToList();

            var existing = await _core.GetExistingTenantIdsAsync(linked.Select(t => t.CoreTenantId!), ct);

            if (existing.IsFailed)
            {
                return StatusCode(StatusCodes.Status502BadGateway,
                    $"No se pudo verificar el estado contra Core. Detalle: {existing.Error}");
            }

            // Sin Core configurado no se puede afirmar nada sobre los enlaces; se reporta
            // lo que sí se sabe en local y se marca la verificación como no realizada.
            if (existing.IsNotConfigured)
            {
                return Ok(new
                {
                    coreConfigured = false,
                    verified = false,
                    unlinked,
                    dangling = Array.Empty<object>(),
                    healthy = Array.Empty<object>()
                });
            }

            var alive = new HashSet<string>(existing.Value ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var dangling = linked.Where(t => !alive.Contains(t.CoreTenantId!)).ToList();
            var healthy = linked.Where(t => alive.Contains(t.CoreTenantId!)).ToList();

            return Ok(new
            {
                coreConfigured = true,
                verified = true,
                summary = new { total = tenants.Count, healthy = healthy.Count, unlinked = unlinked.Count, dangling = dangling.Count },
                unlinked,
                dangling,
                healthy
            });
        }

        /// <summary>
        /// Repara la dualidad de un tenant concreto: adopta la ficha de Core si ya existe con
        /// el mismo slug, o la crea. Es el camino que faltaba para enlazar tenants creados
        /// antes de que existiera la dualidad, que hasta ahora exigía SQL a mano.
        /// </summary>
        [HttpPost("{id:guid}/core-sync")]
        public async Task<IActionResult> RepairCoreSync(Guid id, [FromQuery] string? countryId, CancellationToken ct)
        {
            var tenant = await _dbContext.Tenants.FindAsync(new object?[] { id }, ct);
            if (tenant == null) return NotFound();

            var result = await _core.CreateTenantAsync(ToCoreTenant(tenant, countryId), ct);

            if (result.IsNotConfigured)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    "Core no está configurado en esta instancia; no se puede reparar el enlace.");
            }
            if (result.IsFailed)
            {
                return StatusCode(StatusCodes.Status502BadGateway,
                    $"No se pudo reparar el enlace con Core. Detalle: {result.Error}");
            }

            var previous = tenant.CoreTenantId;
            tenant.CoreTenantId = result.Value?.Id;
            await _dbContext.SaveChangesAsync(ct);

            return Ok(new
            {
                tenantId = tenant.Id,
                previousCoreTenantId = previous,
                coreTenantId = tenant.CoreTenantId,
                adopted = result.Value?.Adopted ?? false
            });
        }

        // El estado que importa acá es el de la ASIGNACIÓN a este tenant, no el de la identidad
        // (TenantUser.IsActive): la misma persona puede tener acceso vigente a un tenant y
        // revocado en otro. Antes esto listaba/activaba/revocaba sobre la identidad completa, así
        // que revocar a alguien en un tenant lo habría dejado sin acceso a TODOS los tenants a los
        // que administrara.

        [HttpGet("{id:guid}/users")]
        public async Task<IActionResult> GetTenantUsers(Guid id)
        {
            var users = await _dbContext.TenantUserAssignments
                .Where(a => a.TenantId == id)
                .Select(a => new
                {
                    a.TenantUser.Id,
                    a.TenantUser.Name,
                    a.TenantUser.Email,
                    IsActive = a.IsActive,
                    a.CreatedAt
                })
                .ToListAsync();

            return Ok(users);
        }

        [HttpPost("{id:guid}/users")]
        public async Task<IActionResult> CreateTenantUser(Guid id, [FromBody] CreateTenantUserRequest request)
        {
            var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == id);
            if (tenant == null) return NotFound("Tenant no existe.");

            var existente = await _dbContext.TenantUsers.FirstOrDefaultAsync(u => u.Email == request.Email);

            if (existente != null)
            {
                var asignacion = await _dbContext.TenantUserAssignments
                    .FirstOrDefaultAsync(a => a.TenantUserId == existente.Id && a.TenantId == id);

                if (asignacion != null)
                {
                    if (asignacion.IsActive) return BadRequest("Este administrador ya tiene acceso a este tenant.");

                    // Tenía acceso, se lo habían revocado y se le está dando de nuevo: se
                    // reactiva la asignación existente en vez de duplicarla (el índice único
                    // sobre TenantUserId+TenantId lo impediría de todos modos).
                    asignacion.IsActive = true;
                    asignacion.RevokedAt = null;
                    // Idem: si la identidad venía apagada de una revocación de antes de que
                    // existieran las asignaciones, se reactiva — se le está devolviendo acceso
                    // a propósito, no debe quedar bloqueada por un campo que nadie más toca.
                    existente.IsActive = true;
                    await _dbContext.SaveChangesAsync();
                    return Ok(new { existente.Id, existente.Name, existente.Email, IsActive = true });
                }

                // Identidad ya registrada en otro tenant: se reutiliza, no se duplica el correo.
                // Misma reactivación que arriba, por si esta identidad venía apagada.
                existente.IsActive = true;
                _dbContext.TenantUserAssignments.Add(new TenantUserAssignment
                {
                    Id = Guid.NewGuid(),
                    TenantUserId = existente.Id,
                    TenantId = id,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
                await _dbContext.SaveChangesAsync();
                return Ok(new { existente.Id, existente.Name, existente.Email, IsActive = true });
            }

            // Sin contraseña manual: se crea con un hash aleatorio inutilizable (nadie la conoce)
            // y se invita al Tenant a que la establezca él mismo desde el enlace.
            var user = new TenantUser
            {
                Id = Guid.NewGuid(),
                TenantId = id,
                Name = request.Name,
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.TenantUsers.Add(user);
            _dbContext.TenantUserAssignments.Add(new TenantUserAssignment
            {
                Id = Guid.NewGuid(),
                TenantUserId = user.Id,
                TenantId = id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync();

            await _passwordResetService.RequestAsync(
                PortalUserType.Tenant, user.Id, user.Email, user.Name, _tenantPortalUrl, "invitation", tenant.CoreTenantId,
                tenant.LogoLightUrl, tenant.CommercialName);

            return Ok(new { user.Id, user.Name, user.Email, user.IsActive });
        }

        // Mismo patrón que TenantDevelopersController (Tenant -> Developer): reenviar, revocar
        // y reactivar, para que el ciclo de vida de la invitación quede al mismo nivel en los
        // tres flujos (Superadmin -> Tenant, Tenant -> Developer, Tenant -> Cliente).
        [HttpPost("{id:guid}/users/{userId:guid}/resend-invitation")]
        public async Task<IActionResult> ResendUserInvitation(Guid id, Guid userId)
        {
            var user = await _dbContext.TenantUsers.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return NotFound();

            var asignacion = await _dbContext.TenantUserAssignments
                .FirstOrDefaultAsync(a => a.TenantUserId == userId && a.TenantId == id);
            if (asignacion == null) return NotFound();

            if (!asignacion.IsActive)
            {
                return BadRequest("Este administrador fue revocado en este tenant; reactívalo antes de reenviar la invitación.");
            }

            var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == id);
            if (tenant == null) return NotFound("Tenant no encontrado.");

            await _passwordResetService.RequestAsync(
                PortalUserType.Tenant, user.Id, user.Email, user.Name, _tenantPortalUrl, "invitation", tenant.CoreTenantId,
                tenant.LogoLightUrl, tenant.CommercialName);

            return Ok(new { message = "Invitación reenviada." });
        }

        [HttpDelete("{id:guid}/users/{userId:guid}")]
        public async Task<IActionResult> RevokeTenantUser(Guid id, Guid userId)
        {
            var asignacion = await _dbContext.TenantUserAssignments
                .FirstOrDefaultAsync(a => a.TenantUserId == userId && a.TenantId == id);
            if (asignacion == null) return NotFound();

            asignacion.IsActive = false;
            asignacion.RevokedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }

        [HttpPost("{id:guid}/users/{userId:guid}/reactivate")]
        public async Task<IActionResult> ReactivateTenantUser(Guid id, Guid userId)
        {
            var asignacion = await _dbContext.TenantUserAssignments
                .FirstOrDefaultAsync(a => a.TenantUserId == userId && a.TenantId == id);
            if (asignacion == null) return NotFound();

            var user = await _dbContext.TenantUsers.FirstAsync(u => u.Id == userId);

            asignacion.IsActive = true;
            asignacion.RevokedAt = null;
            // Misma reactivación de la identidad que en CreateTenantUser: si venía apagada de
            // una revocación de antes de las asignaciones, no debe seguir bloqueando el login.
            user.IsActive = true;
            await _dbContext.SaveChangesAsync();

            return Ok(new { user.Id, user.Name, user.Email, IsActive = true });
        }
    }

    public class CreateTenantUserRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class CreateTenantRequest
    {
        public string Name { get; set; } = string.Empty;
        public string CommercialName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? LegalName { get; set; }
        public string? FirstName { get; set; }
        public string? SecondName { get; set; }
        public string? FirstLastName { get; set; }
        public string? SecondLastName { get; set; }
        public string? TaxId { get; set; }
        public string? VerificationDigit { get; set; }
        public string? ContactPerson { get; set; }
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }
        public string? WhatsAppPhone { get; set; }
        public string? EinvoicingEmail { get; set; }
        public string? CommercialEmail { get; set; }
        public string? Website { get; set; }
        public string? PhysicalAddressLine1 { get; set; }
        public string? PhysicalAddressLine2 { get; set; }
        public string? PhysicalCity { get; set; }
        public string? PhysicalState { get; set; }
        public string? PhysicalPostalCode { get; set; }
        public string? BillingAddress { get; set; }
        public string? DefaultLanguageCode { get; set; }
        public string? DefaultTimezone { get; set; }
        public string? DefaultCurrencyId { get; set; }
        public string? CountryId { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? AdminName { get; set; }
        public string? AdminEmail { get; set; }
    }

    public class UpdateTenantRequest
    {
        public string Name { get; set; } = string.Empty;
        public string CommercialName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        
        public string TaxId { get; set; } = string.Empty;
        public string VerificationDigit { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string TaxRegime { get; set; } = string.Empty;
        public string EconomicActivity { get; set; } = string.Empty;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        // Campos comerciales espejo de Core. Nulo significa "no tocar".
        public string? LegalName { get; set; }
        public string? ContactPerson { get; set; }
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }
        public string? WhatsAppPhone { get; set; }
        public string? EinvoicingEmail { get; set; }
        public string? CommercialEmail { get; set; }
        public string? Website { get; set; }
        public string? PhysicalAddressLine1 { get; set; }
        public string? PhysicalAddressLine2 { get; set; }
        public string? PhysicalCity { get; set; }
        public string? PhysicalState { get; set; }
        public string? PhysicalPostalCode { get; set; }
        public string? BillingAddress { get; set; }
        public string? DefaultLanguageCode { get; set; }
        public string? DefaultTimezone { get; set; }
        public string? DefaultCurrencyId { get; set; }
        public Guid? ParentTenantId { get; set; }
    }
}
