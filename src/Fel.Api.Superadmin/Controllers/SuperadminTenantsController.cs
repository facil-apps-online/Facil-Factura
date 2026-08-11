using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        public SuperadminTenantsController(
            FelDbContext dbContext,
            Fel.Core.Interfaces.ICoreApiClient core,
            ILogger<SuperadminTenantsController> logger)
        {
            _dbContext = dbContext;
            _core = core;
            _logger = logger;
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
                    t.CreatedAt
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

        [HttpPost]
        public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequest request)
        {
            if (await _dbContext.Tenants.AnyAsync(t => t.Slug == request.Slug))
            {
                return BadRequest("El slug ya está en uso.");
            }

            // Validar el admin antes de persistir el tenant: si no, un email duplicado
            // aborta el flujo dejando el tenant creado y bloqueando el slug en reintentos.
            if (!string.IsNullOrWhiteSpace(request.AdminEmail)
                && await _dbContext.TenantUsers.AnyAsync(u => u.Email == request.AdminEmail))
            {
                return BadRequest("El email del administrador ya está registrado.");
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
            await _dbContext.SaveChangesAsync();

            // Crear el usuario administrador del tenant si se proporcionó
            if (!string.IsNullOrWhiteSpace(request.AdminEmail))
            {
                string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.AdminPassword ?? string.Empty);

                _dbContext.TenantUsers.Add(new TenantUser
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    Name = string.IsNullOrWhiteSpace(request.AdminName) ? request.AdminEmail : request.AdminName,
                    Email = request.AdminEmail,
                    PasswordHash = passwordHash,
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
            t.CreatedAt
        };

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateTenant(Guid id, [FromBody] UpdateTenantRequest request)
        {
            var tenant = await _dbContext.Tenants.FindAsync(id);
            if (tenant == null) return NotFound();

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

        [HttpGet("{id:guid}/users")]
        public async Task<IActionResult> GetTenantUsers(Guid id)
        {
            var users = await _dbContext.TenantUsers
                .Where(u => u.TenantId == id)
                .Select(u => new { u.Id, u.Name, u.Email, u.IsActive, u.CreatedAt })
                .ToListAsync();

            return Ok(users);
        }

        [HttpPost("{id:guid}/users")]
        public async Task<IActionResult> CreateTenantUser(Guid id, [FromBody] CreateTenantUserRequest request)
        {
            if (!await _dbContext.Tenants.AnyAsync(t => t.Id == id)) return NotFound("Tenant no existe.");
            if (await _dbContext.TenantUsers.AnyAsync(u => u.Email == request.Email)) return BadRequest("Email ya registrado.");

            // Hashing the password using BCrypt (assuming it's used or simple fallback)
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            var user = new TenantUser
            {
                Id = Guid.NewGuid(),
                TenantId = id,
                Name = request.Name,
                Email = request.Email,
                PasswordHash = passwordHash,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.TenantUsers.Add(user);
            await _dbContext.SaveChangesAsync();

            return Ok(new { user.Id, user.Name, user.Email, user.IsActive });
        }
    }

    public class CreateTenantUserRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class CreateTenantRequest
    {
        public string Name { get; set; } = string.Empty;
        public string CommercialName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? LegalName { get; set; }
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
        public string? AdminPassword { get; set; }
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
    }
}
