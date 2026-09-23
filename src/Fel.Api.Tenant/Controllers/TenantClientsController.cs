using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fel.Core.Entities;
using Fel.Core.Interfaces;
using Fel.Infrastructure.Data;
using Fel.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Fel.Api.Tenant.Controllers
{
    [ApiController]
    [Route("api/tenant/clients")]
    public class TenantClientsController : ControllerBase
    {
        private readonly FelDbContext _dbContext;
        private readonly ICryptoService _cryptoService;
        private readonly PasswordResetService _passwordResetService;
        private readonly string _clientPortalUrl;

        public TenantClientsController(FelDbContext dbContext, ICryptoService cryptoService, PasswordResetService passwordResetService, IConfiguration config)
        {
            _dbContext = dbContext;
            _cryptoService = cryptoService;
            _passwordResetService = passwordResetService;
            _clientPortalUrl = config["ClientPortalUrl"] ?? "https://clients.facil-factura.pro";
        }

        // Simulación: en producción, esto vendría del JWT o Claims.
        private Guid GetCurrentTenantId()
        {
            if (Request.Headers.TryGetValue("x-tenant-id", out var tenantIdStr))
            {
                if (Guid.TryParse(tenantIdStr, out var tenantId))
                    return tenantId;
            }
            // Fallback (debería retornar 401, pero para efectos de prueba asumimos que viene en el header)
            throw new UnauthorizedAccessException("x-tenant-id Header is missing");
        }

        [HttpGet]
        public async Task<IActionResult> GetClients()
        {
            try
            {
                var tenantId = GetCurrentTenantId();
                var clients = await _dbContext.Clients
                    .Where(c => c.TenantId == tenantId && !c.IsDeveloperSandbox)
                    .Select(c => new
                    {
                        c.Id,
                        c.CompanyName,
                        c.TaxId,
                        c.Email,
                        c.IsActive,
                        c.CreatedAt,
                        c.AssociateId,
                        AssociateName = c.Associate != null ? c.Associate.Name : null,
                        NearestResolutionExpiry = c.Resolutions
                            .Where(r => r.IsActive)
                            .OrderBy(r => r.ValidTo)
                            .Select(r => (DateTime?)r.ValidTo)
                            .FirstOrDefault(),
                        // Estado del acceso al portal — para la columna de invitación y para que
                        // el envío masivo sepa a quién ya no hace falta invitarle.
                        PortalUserStatus = _dbContext.ClientUsers
                            .Where(u => u.ClientId == c.Id)
                            .Select(u => u.IsActive ? "Active" : "Revoked")
                            .FirstOrDefault() ?? "NotInvited"
                    })
                    .ToListAsync();

                return Ok(clients);
            }
            catch (Exception ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetClient(Guid id)
        {
            var tenantId = GetCurrentTenantId();
            var client = await _dbContext.Clients
                .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);
                
            if (client == null) return NotFound();
            return Ok(new {
                client.Id,
                client.CompanyName,
                client.CommercialName,
                client.TaxId,
                client.VerificationDigit,
                client.Email,
                client.Phone,
                client.Address,
                client.City,
                client.TaxRegime,
                client.EconomicActivity,
                client.AppliesRetentions,
                client.AssociateId,
                client.Latitude,
                client.Longitude,
                client.IsActive,
                client.SubscriptionRate,
                BillingFrequency = client.BillingFrequency.ToString(),
                client.LiveApiKey,
                client.LiveApiSecret,
                client.TestApiKey,
                client.TestApiSecret
            });
        }

        [HttpPost]
        public async Task<IActionResult> CreateClient([FromBody] CreateClientRequest request)
        {
            var tenantId = GetCurrentTenantId();

            if (await _dbContext.Clients.AnyAsync(c => c.TaxId == request.TaxId && c.TenantId == tenantId))
            {
                return BadRequest("El NIT ya está registrado en este Tenant.");
            }

            var client = new Client
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CompanyName = request.CompanyName,
                CommercialName = request.CommercialName,
                TaxId = request.TaxId,
                VerificationDigit = request.VerificationDigit,
                Email = request.Email,
                Phone = request.Phone,
                Address = request.Address,
                City = request.City,
                TaxRegime = request.TaxRegime,
                EconomicActivity = request.EconomicActivity,
                AssociateId = request.AssociateId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                // Por defecto factura directo con la DIAN; se cambia a un integrador desde
                // /{id}/document-provider si el Client necesita Dataico u otro.
                IntegratorId = Guid.Parse("00000000-0000-0000-0000-000000000101")
            };

            _dbContext.Clients.Add(client);
            _dbContext.ClientIntegratorAssignments.Add(new ClientIntegratorAssignment
            {
                Id = Guid.NewGuid(),
                ClientId = client.Id,
                IntegratorId = client.IntegratorId,
                EffectiveFrom = client.CreatedAt,
                EffectiveTo = null
            });

            // Set estándar al crear el Client — el Tenant amplía/reduce esto después desde
            // /{id}/enabled-document-types y /{id}/enabled-retention-concepts.
            foreach (var documentTypeId in DefaultCatalogSets.StandardDocumentTypeIds)
            {
                _dbContext.ClientEnabledDocumentTypes.Add(new ClientEnabledDocumentType
                {
                    Id = Guid.NewGuid(),
                    ClientId = client.Id,
                    DocumentTypeId = documentTypeId
                });
            }
            foreach (var retentionConceptId in DefaultCatalogSets.StandardRetentionConceptIds)
            {
                _dbContext.ClientEnabledRetentionConcepts.Add(new ClientEnabledRetentionConcept
                {
                    Id = Guid.NewGuid(),
                    ClientId = client.Id,
                    RetentionConceptId = retentionConceptId
                });
            }

            await _dbContext.SaveChangesAsync();

            return Ok(new { client.Id, client.CompanyName, client.TaxId, client.Email, client.IsActive });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateClient(Guid id, [FromBody] UpdateClientRequest request)
        {
            var tenantId = GetCurrentTenantId();
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);
            
            if (client == null) return NotFound();

            client.CompanyName = request.CompanyName;
            client.CommercialName = request.CommercialName;
            client.Email = request.Email;
            client.Phone = request.Phone;
            client.TaxId = request.TaxId;
            client.VerificationDigit = request.VerificationDigit;
            client.Address = request.Address;
            client.City = request.City;
            client.TaxRegime = request.TaxRegime;
            client.EconomicActivity = request.EconomicActivity;
            client.AppliesRetentions = request.AppliesRetentions;
            client.AssociateId = request.AssociateId;
            client.Latitude = request.Latitude;
            client.Longitude = request.Longitude;
            client.SubscriptionRate = request.SubscriptionRate;
            if (Enum.TryParse<BillingFrequency>(request.BillingFrequency, out var frequency))
            {
                client.BillingFrequency = frequency;
            }

            await _dbContext.SaveChangesAsync();

            return Ok(new { client.Id, client.CompanyName, client.TaxId, client.Email, client.IsActive });
        }
        
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteClient(Guid id)
        {
            var tenantId = GetCurrentTenantId();
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);
            
            if (client == null) return NotFound();

            // Lógica de Soft Delete
            client.IsActive = false;
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("{id}/portal-user")]
        public async Task<IActionResult> GetPortalUser(Guid id)
        {
            var tenantId = GetCurrentTenantId();
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);
            if (client == null) return NotFound();

            var user = await _dbContext.ClientUsers.FirstOrDefaultAsync(u => u.ClientId == id);
            if (user == null) return Ok(null);

            return Ok(new { user.Id, user.Name, user.Email, user.IsActive, user.CreatedAt });
        }

        [HttpPut("{id}/portal-user")]
        public async Task<IActionResult> UpsertPortalUser(Guid id, [FromBody] UpsertPortalUserRequest request)
        {
            var tenantId = GetCurrentTenantId();
            var client = await _dbContext.Clients.Include(c => c.Tenant).FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);
            if (client == null) return NotFound();

            if (await _dbContext.ClientUsers.AnyAsync(u => u.Email == request.Email && u.ClientId != id))
            {
                return BadRequest("Ese email ya está en uso por otro acceso de cliente.");
            }

            var user = await _dbContext.ClientUsers.FirstOrDefaultAsync(u => u.ClientId == id);
            var isNew = user == null;
            if (isNew)
            {
                // Sin contraseña manual: se crea con un hash aleatorio inutilizable (nadie la
                // conoce) y se invita al cliente a que la establezca él mismo desde el enlace.
                var initialHash = string.IsNullOrWhiteSpace(request.Password)
                    ? BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString())
                    : BCrypt.Net.BCrypt.HashPassword(request.Password);

                user = new ClientUser
                {
                    Id = Guid.NewGuid(),
                    ClientId = id,
                    Name = request.Name,
                    Email = request.Email,
                    PasswordHash = initialHash,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                _dbContext.ClientUsers.Add(user);
            }
            else
            {
                user.Name = request.Name;
                user.Email = request.Email;
                if (!string.IsNullOrWhiteSpace(request.Password))
                {
                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
                }
            }

            await _dbContext.SaveChangesAsync();

            if (isNew && string.IsNullOrWhiteSpace(request.Password))
            {
                await _passwordResetService.RequestAsync(
                    PortalUserType.Client, user.Id, user.Email, user.Name, _clientPortalUrl, "invitation", client.Tenant?.CoreTenantId,
                    client.Tenant?.LogoLightUrl, client.Tenant?.CommercialName);
            }

            return Ok(new { user.Id, user.Name, user.Email, user.IsActive });
        }

        // Mismo patrón que TenantDevelopersController y SuperadminTenantsController: reenviar,
        // revocar y reactivar. Va sobre /portal-user/... (no DELETE /{id}) porque ese verbo ya
        // significa "desactivar la empresa Client completa" — aquí solo se toca el login del
        // portal, no el Client dueño de la facturación.
        [HttpPost("{id}/portal-user/resend-invitation")]
        public async Task<IActionResult> ResendPortalUserInvitation(Guid id)
        {
            var tenantId = GetCurrentTenantId();
            var client = await _dbContext.Clients.Include(c => c.Tenant).FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);
            if (client == null) return NotFound();

            var user = await _dbContext.ClientUsers.FirstOrDefaultAsync(u => u.ClientId == id);
            if (user == null) return NotFound("Este cliente todavía no tiene un acceso de portal invitado.");

            if (!user.IsActive)
            {
                return BadRequest("Este acceso fue revocado; reactívalo antes de reenviar la invitación.");
            }

            await _passwordResetService.RequestAsync(
                PortalUserType.Client, user.Id, user.Email, user.Name, _clientPortalUrl, "invitation", client.Tenant?.CoreTenantId,
                client.Tenant?.LogoLightUrl, client.Tenant?.CommercialName);

            return Ok(new { message = "Invitación reenviada." });
        }

        [HttpPost("{id}/portal-user/revoke")]
        public async Task<IActionResult> RevokePortalUser(Guid id)
        {
            var tenantId = GetCurrentTenantId();
            if (!await _dbContext.Clients.AnyAsync(c => c.Id == id && c.TenantId == tenantId)) return NotFound();

            var user = await _dbContext.ClientUsers.FirstOrDefaultAsync(u => u.ClientId == id);
            if (user == null) return NotFound("Este cliente todavía no tiene un acceso de portal invitado.");

            user.IsActive = false;
            await _dbContext.SaveChangesAsync();

            return Ok(new { user.Id, user.Name, user.Email, user.IsActive });
        }

        [HttpPost("{id}/portal-user/reactivate")]
        public async Task<IActionResult> ReactivatePortalUser(Guid id)
        {
            var tenantId = GetCurrentTenantId();
            if (!await _dbContext.Clients.AnyAsync(c => c.Id == id && c.TenantId == tenantId)) return NotFound();

            var user = await _dbContext.ClientUsers.FirstOrDefaultAsync(u => u.ClientId == id);
            if (user == null) return NotFound("Este cliente todavía no tiene un acceso de portal invitado.");

            user.IsActive = true;
            await _dbContext.SaveChangesAsync();

            return Ok(new { user.Id, user.Name, user.Email, user.IsActive });
        }

        // Envío masivo: invita de una sola vez a todos los Clients señalados que todavía no
        // tienen acceso de portal (o, si no se envían ids, a todos los que les falte). Usa el
        // Email/CompanyName ya registrados en cada Client como destinatario — no pide un
        // formulario por cliente, que es justo lo que el envío masivo busca evitar. Los que ya
        // tienen portal user, o no tienen email cargado, se reportan aparte sin frenar al resto.
        [HttpPost("bulk-invite")]
        public async Task<IActionResult> BulkInviteClients([FromBody] BulkInviteClientsRequest request)
        {
            var tenantId = GetCurrentTenantId();
            var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
            if (tenant == null) return NotFound("Tenant no encontrado.");

            var clientsQuery = _dbContext.Clients.Where(c => c.TenantId == tenantId && !c.IsDeveloperSandbox && c.IsActive);
            if (request.ClientIds != null && request.ClientIds.Count > 0)
            {
                clientsQuery = clientsQuery.Where(c => request.ClientIds.Contains(c.Id));
            }
            var candidates = await clientsQuery.ToListAsync();

            var existingUserClientIds = new HashSet<Guid>(
                await _dbContext.ClientUsers.Where(u => candidates.Select(c => c.Id).Contains(u.ClientId)).Select(u => u.ClientId).ToListAsync());

            var newUsers = new List<(ClientUser User, string CompanyName)>();
            var skippedAlreadyInvited = new List<object>();
            var skippedNoEmail = new List<object>();

            foreach (var client in candidates)
            {
                if (existingUserClientIds.Contains(client.Id))
                {
                    skippedAlreadyInvited.Add(new { client.Id, client.CompanyName });
                    continue;
                }
                if (string.IsNullOrWhiteSpace(client.Email))
                {
                    skippedNoEmail.Add(new { client.Id, client.CompanyName });
                    continue;
                }

                var user = new ClientUser
                {
                    Id = Guid.NewGuid(),
                    ClientId = client.Id,
                    Name = string.IsNullOrWhiteSpace(client.CommercialName) ? client.CompanyName : client.CommercialName,
                    Email = client.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                _dbContext.ClientUsers.Add(user);
                newUsers.Add((user, client.CompanyName));
            }

            await _dbContext.SaveChangesAsync();

            foreach (var (user, _) in newUsers)
            {
                await _passwordResetService.RequestAsync(
                    PortalUserType.Client, user.Id, user.Email, user.Name, _clientPortalUrl, "invitation", tenant.CoreTenantId,
                    tenant.LogoLightUrl, tenant.CommercialName);
            }

            return Ok(new
            {
                invitedCount = newUsers.Count,
                invited = newUsers.Select(u => new { ClientId = u.User.ClientId, u.CompanyName, u.User.Email }),
                skippedAlreadyInvited,
                skippedNoEmail
            });
        }

        [HttpGet("{id}/document-provider")]
        public async Task<IActionResult> GetDocumentProvider(Guid id)
        {
            var tenantId = GetCurrentTenantId();
            var client = await _dbContext.Clients.Include(c => c.Integrator).FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);
            if (client == null) return NotFound();

            return Ok(new
            {
                // El portal de Tenant hoy solo conoce estos dos valores fijos (Native/Dataico); el
                // selector dinámico sobre el catálogo completo de Integrator queda para cuando se
                // aborde esa UI.
                documentProvider = client.Integrator.Code == "DATAICO" ? "Dataico" : "Native",
                dataicoApiUser = client.DataicoApiUser,
                dataicoAccountId = client.DataicoAccountId,
                dataicoEnvironment = client.DataicoEnvironment,
                hasApiPassword = !string.IsNullOrEmpty(client.DataicoApiPasswordEncrypted),
                hasAuthToken = !string.IsNullOrEmpty(client.DataicoAuthTokenEncrypted)
            });
        }

        [HttpPut("{id}/document-provider")]
        public async Task<IActionResult> UpdateDocumentProvider(Guid id, [FromBody] UpdateDocumentProviderRequest request)
        {
            var tenantId = GetCurrentTenantId();
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);
            if (client == null) return NotFound();

            var integratorCode = request.DocumentProvider == "Dataico" ? "DATAICO" : "NATIVE";
            var integrator = await _dbContext.Integrators.FirstOrDefaultAsync(i => i.Code == integratorCode);
            if (integrator == null)
            {
                return BadRequest("Proveedor de documentos inválido.");
            }

            if (client.IntegratorId != integrator.Id)
            {
                var now = DateTime.UtcNow;
                var openAssignment = await _dbContext.ClientIntegratorAssignments
                    .FirstOrDefaultAsync(a => a.ClientId == client.Id && a.EffectiveTo == null);
                if (openAssignment != null)
                {
                    openAssignment.EffectiveTo = now;
                }

                _dbContext.ClientIntegratorAssignments.Add(new ClientIntegratorAssignment
                {
                    Id = Guid.NewGuid(),
                    ClientId = client.Id,
                    IntegratorId = integrator.Id,
                    EffectiveFrom = now,
                    EffectiveTo = null
                });
            }

            client.IntegratorId = integrator.Id;
            client.DataicoApiUser = request.DataicoApiUser ?? string.Empty;
            client.DataicoAccountId = request.DataicoAccountId ?? string.Empty;
            client.DataicoEnvironment = string.IsNullOrWhiteSpace(request.DataicoEnvironment) ? "PRUEBAS" : request.DataicoEnvironment;

            if (!string.IsNullOrEmpty(request.DataicoApiPassword))
            {
                client.DataicoApiPasswordEncrypted = _cryptoService.Encrypt(request.DataicoApiPassword);
            }

            if (!string.IsNullOrEmpty(request.DataicoAuthToken))
            {
                client.DataicoAuthTokenEncrypted = _cryptoService.Encrypt(request.DataicoAuthToken);
            }

            await _dbContext.SaveChangesAsync();

            return Ok(new { Message = "Configuración de proveedor de documentos actualizada." });
        }

        /// <summary>
        /// Catálogos del LoginSISPRO (tipos de documento, tipos de usuario y ambientes). No
        /// dependen del Client: se sirven desde acá para que los portales no los lleven escritos
        /// dentro de sus propios selects.
        /// </summary>
        [HttpGet("minsalud-catalogs")]
        public IActionResult GetMinSaludCatalogs()
        {
            // En este controlador la autorización la impone cada acción al pedir el tenant de la
            // sesión, no un [Authorize] de clase. Se exige aquí aunque los catálogos no dependan
            // del tenant: sin esto el endpoint quedaría público por omisión, y ese es el patrón
            // que alguien termina copiando para algo que sí es sensible. GetCurrentTenantId lanza
            // en vez de devolver 401, así que hay que capturarlo como hacen las demás acciones.
            try
            {
                GetCurrentTenantId();
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { message = "No se pudo resolver el tenant de la sesión." });
            }

            return Ok(new
            {
                documentTypes = Fel.Core.Entities.MinSaludCatalogs.TiposDeDocumento
                    .Select(o => new { code = o.Codigo, name = o.Nombre }),
                userTypes = Fel.Core.Entities.MinSaludCatalogs.TiposDeUsuario
                    .Select(o => new { code = o.Codigo, name = o.Nombre }),
                environments = Fel.Core.Entities.MinSaludCatalogs.Ambientes
                    .Select(o => new { code = o.Codigo, name = o.Nombre })
            });
        }

        [HttpGet("{id}/minsalud-config")]
        public async Task<IActionResult> GetMinSaludConfig(Guid id)
        {
            var tenantId = GetCurrentTenantId();
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);
            if (client == null) return NotFound();

            return Ok(new
            {
                minSaludEnvironment = client.MinSaludEnvironment,
                minSaludUserType = client.MinSaludUserType,
                // Producción
                minSaludIdentificationType = client.MinSaludIdentificationType,
                minSaludIdentificationNumber = client.MinSaludIdentificationNumber,
                hasPassword = !string.IsNullOrEmpty(client.MinSaludPasswordEncrypted),
                // Pruebas
                minSaludTestIdentificationType = client.MinSaludTestIdentificationType,
                minSaludTestIdentificationNumber = client.MinSaludTestIdentificationNumber,
                hasTestPassword = !string.IsNullOrEmpty(client.MinSaludTestPasswordEncrypted)
            });
        }

        [HttpPut("{id}/minsalud-config")]
        public async Task<IActionResult> UpdateMinSaludConfig(Guid id, [FromBody] UpdateMinSaludConfigRequest request)
        {
            var tenantId = GetCurrentTenantId();
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);
            if (client == null) return NotFound();

            if (!Fel.Core.Entities.MinSaludEnvironments.EsValido(request.MinSaludEnvironment))
            {
                return BadRequest(new { message = "El ambiente debe ser 'Test' o 'Production'." });
            }

            client.MinSaludEnvironment = request.MinSaludEnvironment!;
            client.MinSaludUserType = request.MinSaludUserType;

            client.MinSaludIdentificationType = request.MinSaludIdentificationType;
            client.MinSaludIdentificationNumber = request.MinSaludIdentificationNumber;
            client.MinSaludTestIdentificationType = request.MinSaludTestIdentificationType;
            client.MinSaludTestIdentificationNumber = request.MinSaludTestIdentificationNumber;

            // Las claves solo se sobrescriben si vienen: dejarlas vacías significa "no cambiar",
            // para no obligar a reescribirlas cada vez que se toca otro campo.
            if (!string.IsNullOrEmpty(request.MinSaludPassword))
            {
                client.MinSaludPasswordEncrypted = _cryptoService.Encrypt(request.MinSaludPassword);
            }
            if (!string.IsNullOrEmpty(request.MinSaludTestPassword))
            {
                client.MinSaludTestPasswordEncrypted = _cryptoService.Encrypt(request.MinSaludTestPassword);
            }

            await _dbContext.SaveChangesAsync();

            return Ok(new { Message = "Configuración de MinSalud (RIPS) actualizada." });
        }

        [HttpPost("{id}/generate-key")]
        public async Task<IActionResult> GenerateApiKey(Guid id, [FromQuery] string env)
        {
            var tenantId = GetCurrentTenantId();
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);
            
            if (client == null) return NotFound();

            var newKey = $"sk_{env.ToLower()}_{Guid.NewGuid().ToString("N")}";
            var newSecret = Guid.NewGuid().ToString("N");

            if (env.Equals("live", StringComparison.OrdinalIgnoreCase))
            {
                client.LiveApiKey = newKey;
                client.LiveApiSecret = newSecret;
            }
            else if (env.Equals("test", StringComparison.OrdinalIgnoreCase))
            {
                client.TestApiKey = newKey;
                client.TestApiSecret = newSecret;
            }
            else
            {
                return BadRequest("Invalid environment. Use 'live' or 'test'.");
            }

            await _dbContext.SaveChangesAsync();

            return Ok(new { key = newKey, secret = newSecret });
        }

        // Tipos de documento que este Client puede emitir desde su formulario de facturación —
        // por defecto solo el set estándar (Factura/NC/ND, ver DefaultCatalogSets), el Tenant
        // amplía o reduce esto según lo que ese Client realmente necesite.
        [HttpGet("{id}/enabled-document-types")]
        public async Task<IActionResult> GetEnabledDocumentTypes(Guid id)
        {
            var tenantId = GetCurrentTenantId();
            if (!await _dbContext.Clients.AnyAsync(c => c.Id == id && c.TenantId == tenantId)) return NotFound();

            var enabledIds = await _dbContext.ClientEnabledDocumentTypes
                .Where(e => e.ClientId == id)
                .Select(e => e.DocumentTypeId)
                .ToListAsync();

            var types = await _dbContext.DocumentTypes
                .Where(d => d.IsActive)
                .OrderBy(d => d.GoverningEntity).ThenBy(d => d.Name)
                .Select(d => new { d.Id, d.Code, d.Name, d.GoverningEntity })
                .ToListAsync();

            return Ok(types.Select(t => new { t.Id, t.Code, t.Name, t.GoverningEntity, Enabled = enabledIds.Contains(t.Id) }));
        }

        [HttpPut("{id}/enabled-document-types")]
        public async Task<IActionResult> SetEnabledDocumentTypes(Guid id, [FromBody] SetEnabledIdsRequest request)
        {
            var tenantId = GetCurrentTenantId();
            if (!await _dbContext.Clients.AnyAsync(c => c.Id == id && c.TenantId == tenantId)) return NotFound();

            var existing = await _dbContext.ClientEnabledDocumentTypes.Where(e => e.ClientId == id).ToListAsync();
            _dbContext.ClientEnabledDocumentTypes.RemoveRange(existing);

            foreach (var documentTypeId in request.Ids.Distinct())
            {
                _dbContext.ClientEnabledDocumentTypes.Add(new ClientEnabledDocumentType { Id = Guid.NewGuid(), ClientId = id, DocumentTypeId = documentTypeId });
            }

            await _dbContext.SaveChangesAsync();
            return Ok(new { message = "Tipos de documento actualizados." });
        }

        // Conceptos de retención que este Client puede elegir por línea de factura — por defecto
        // Compras/Servicios generales y Honorarios (ver DefaultCatalogSets); ReteIVA y el resto del
        // catálogo quedan disponibles para que el Tenant los habilite solo donde aplique.
        [HttpGet("{id}/enabled-retention-concepts")]
        public async Task<IActionResult> GetEnabledRetentionConcepts(Guid id)
        {
            var tenantId = GetCurrentTenantId();
            if (!await _dbContext.Clients.AnyAsync(c => c.Id == id && c.TenantId == tenantId)) return NotFound();

            var enabledIds = await _dbContext.ClientEnabledRetentionConcepts
                .Where(e => e.ClientId == id)
                .Select(e => e.RetentionConceptId)
                .ToListAsync();

            var concepts = await _dbContext.RetentionConcepts
                .Where(c => c.IsActive)
                .OrderBy(c => c.GroupLabel).ThenBy(c => c.Name)
                .Select(c => new { c.Id, c.GroupLabel, c.Name, c.TaxCategory, c.Rate })
                .ToListAsync();

            return Ok(concepts.Select(c => new { c.Id, c.GroupLabel, c.Name, c.TaxCategory, c.Rate, Enabled = enabledIds.Contains(c.Id) }));
        }

        [HttpPut("{id}/enabled-retention-concepts")]
        public async Task<IActionResult> SetEnabledRetentionConcepts(Guid id, [FromBody] SetEnabledIdsRequest request)
        {
            var tenantId = GetCurrentTenantId();
            if (!await _dbContext.Clients.AnyAsync(c => c.Id == id && c.TenantId == tenantId)) return NotFound();

            var existing = await _dbContext.ClientEnabledRetentionConcepts.Where(e => e.ClientId == id).ToListAsync();
            _dbContext.ClientEnabledRetentionConcepts.RemoveRange(existing);

            foreach (var retentionConceptId in request.Ids.Distinct())
            {
                _dbContext.ClientEnabledRetentionConcepts.Add(new ClientEnabledRetentionConcept { Id = Guid.NewGuid(), ClientId = id, RetentionConceptId = retentionConceptId });
            }

            await _dbContext.SaveChangesAsync();
            return Ok(new { message = "Retenciones actualizadas." });
        }
    }

    public class SetEnabledIdsRequest
    {
        public List<Guid> Ids { get; set; } = new();
    }

    public class CreateClientRequest
    {
        public string CompanyName { get; set; } = string.Empty;
        public string CommercialName { get; set; } = string.Empty;
        public string TaxId { get; set; } = string.Empty;
        public string VerificationDigit { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string TaxRegime { get; set; } = string.Empty;
        public string EconomicActivity { get; set; } = string.Empty;
        public Guid? AssociateId { get; set; }
    }

    public class UpdateDocumentProviderRequest
    {
        public string DocumentProvider { get; set; } = "Native";
        public string? DataicoApiUser { get; set; }
        public string? DataicoApiPassword { get; set; }
        public string? DataicoAuthToken { get; set; }
        public string? DataicoAccountId { get; set; }
        public string? DataicoEnvironment { get; set; }
    }

    public class UpdateMinSaludConfigRequest
    {
        /// <summary>"Test" o "Production": decide contra cuál MUV emite este Client.</summary>
        public string? MinSaludEnvironment { get; set; }

        /// <summary>tipoUsuario del LoginSISPRO: RE, PIN, PINx o PIE.</summary>
        public string? MinSaludUserType { get; set; }

        // Credenciales de producción.
        public string? MinSaludIdentificationType { get; set; }
        public string? MinSaludIdentificationNumber { get; set; }
        public string? MinSaludPassword { get; set; }

        // Credenciales de pruebas: el Ministerio tiene ambientes separados y las de uno no sirven
        // en el otro.
        public string? MinSaludTestIdentificationType { get; set; }
        public string? MinSaludTestIdentificationNumber { get; set; }
        public string? MinSaludTestPassword { get; set; }
    }

    public class UpsertPortalUserRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Password { get; set; }
    }

    public class BulkInviteClientsRequest
    {
        // Vacío o null = todos los clientes activos del Tenant que aún no tengan acceso de portal.
        public List<Guid>? ClientIds { get; set; }
    }

    public class UpdateClientRequest
    {
        public string CompanyName { get; set; } = string.Empty;
        public string CommercialName { get; set; } = string.Empty;
        public string TaxId { get; set; } = string.Empty;
        public string VerificationDigit { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string TaxRegime { get; set; } = string.Empty;
        public string EconomicActivity { get; set; } = string.Empty;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public decimal SubscriptionRate { get; set; }
        public string BillingFrequency { get; set; } = "Monthly";
        public bool AppliesRetentions { get; set; } = true;
        public Guid? AssociateId { get; set; }
    }
}
