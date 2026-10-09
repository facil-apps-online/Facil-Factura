using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using FluentValidation;
using FluentValidation.AspNetCore;
using Fel.Api.Validations;

using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Fel.Api.Security;
using Microsoft.OpenApi.Models;
using System.Reflection;
using System.IO;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddMemoryCache();
builder.Services.AddScoped<Fel.Core.Interfaces.ISessionTokenService, Fel.Infrastructure.Security.SessionTokenService>();
builder.Services.AddScoped<Fel.Infrastructure.Security.SessionStampValidator>();
builder.Services.AddScoped<Fel.Infrastructure.Security.AccountSessionService>();

// Misma llave/algoritmo que SessionTokenService usa para firmar — ver SessionHeaderGuardMiddleware
// para cómo se aplica (solo exige el JWT cuando la petición ya trae x-tenant-id).
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var keyStr = builder.Configuration.GetValue<string>("MasterKey") ?? "SUPER_SECRET_FALLBACK_KEY_MUST_BE_32_CHARS_LONG_OR_MORE_123456";
        var key = Encoding.UTF8.GetBytes(keyStr.PadRight(32, '0'));

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        // Revoca tokens: el sello de seguridad del token debe ser el actual del usuario y el usuario debe seguir activo.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async ctx =>
            {
                var validator = ctx.HttpContext.RequestServices.GetRequiredService<Fel.Infrastructure.Security.SessionStampValidator>();
                if (!await validator.IsValidAsync(ctx.Principal!)) ctx.Fail("Sesión revocada o inválida.");
            }
        };
    });
builder.Services.AddFluentValidationAutoValidation()
                .AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssemblyContaining<InvoiceRequestValidator>();

builder.Services.AddHttpClient(); // Necesario para DianSoapClient
builder.Services.AddHttpClient<Fel.Core.Interfaces.ICertificateProvider, Fel.Infrastructure.Certificates.ViafirmaPkcs10Provider>();
builder.Services.AddScoped<Fel.Core.Interfaces.ICertificateProviderContextFactory, Fel.Infrastructure.Certificates.CertificateProviderContextFactory>();
builder.Services.AddScoped<Fel.Core.Interfaces.ICertificateCsrService, Fel.Infrastructure.Certificates.CertificateCsrService>();
builder.Services.AddHttpClient<Fel.Core.Interfaces.ICoreApiClient, Fel.Infrastructure.Services.CoreApiClient>();
builder.Services.AddHttpClient<Fel.Core.Interfaces.IFacilReportsClient, Fel.Infrastructure.Services.FacilReportsClient>();
builder.Services.AddScoped<Fel.Infrastructure.Services.PasswordResetService>();
builder.Services.AddScoped<Fel.Infrastructure.Services.ClientUserAdminService>();
builder.Services.AddScoped<Fel.Infrastructure.Services.ResolutionBranchService>();
builder.Services.AddScoped<Fel.Infrastructure.Services.BranchCredentialResolver>();
builder.Services.AddTransient<Fel.Infrastructure.Services.DianRutParserService>();
builder.Services.AddOpenApi(); // .NET 9 json endpoint
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Facil-Factura.pro API (B2B) - MinSalud",
        Version = "v1",
        Description = "Emisión de RIPS de forma independiente (sin atarlo a una factura electrónica), vía integración directa con MinSalud (MUV-FEV-RIPS), no con Dataico ni la DIAN."
    });

    // Este proyecto (Fel.Api.Tenant) también expone el resto de la administración interna del
    // Tenant (Clientes, certificados, branding, TenantHabilitationController, etc.) — ninguno de
    // esos controladores debe aparecer en este Swagger PÚBLICO, solo TenantDocumentsController
    // (prefijo "api/co/", igual que Fel.Api.Integration, para mantener el mismo esquema
    // país/autoridad en toda ruta que consuma un tenant/developer).
    c.DocInclusionPredicate((docName, apiDesc) => apiDesc.RelativePath?.StartsWith("api/co/") == true);

    // URL base explícita: sin esto, el JSON no dice dónde vive la API — funciona igual dentro del
    // navegador (Swagger UI asume el origen de la página), pero un developer que descargue el JSON
    // para importarlo en Postman o generar un cliente no tendría cómo saberlo.
    c.AddServer(new OpenApiServer { Url = "https://api.facil-factura.pro" });

    // Configurar Swagger para que pida el API Key en la interfaz grÃ¡fica
    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Name = "x-api-key",
        Type = SecuritySchemeType.ApiKey,
        Description = "Ingresa tu API Key. (Adicionalmente, se requerirÃ¡ x-api-timestamp y x-api-signature en cÃ³digo real)"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" }
            },
            new string[] { }
        }
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// Configurar Rate Limiting (Antispam y Anti-DDoS)
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        // Limitar basado en IP, pero idealmente se limita por el API Key o el Tenant
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 100, // Max 100 request (Ajustado segÃºn solicitud)
            Window = TimeSpan.FromSeconds(1), // Por 1 segundo
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 10
        });
    });
    options.RejectionStatusCode = 429;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });
});

// Repositories and Services
builder.Services.AddScoped<Fel.Core.Interfaces.ICryptoVault, Fel.Infrastructure.Security.CryptoVault>();
builder.Services.AddScoped<Fel.Core.Interfaces.IUblGenerator, Fel.Infrastructure.Ubl.UblGenerator>();
builder.Services.AddScoped<Fel.Core.Interfaces.IXmlSigner, Fel.Infrastructure.Security.XadesSigner>();
builder.Services.AddSingleton<Fel.Core.Interfaces.IMessageQueue, Fel.Infrastructure.Messaging.RedisMessageQueue>();
// WCF SOAP Client
builder.Services.AddScoped<Fel.Core.Interfaces.IDianSoapClient, Fel.Infrastructure.Dian.DianSoapClient>();

builder.Services.AddDbContext<FelDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
    b => b.MigrationsAssembly("Fel.Infrastructure")));

// Dependency Injection for Security Services
builder.Services.AddSingleton<Fel.Core.Interfaces.ICryptoService, Fel.Infrastructure.Security.CryptoService>();
builder.Services.AddSingleton<Fel.Core.Interfaces.ICertificateStorageService, Fel.Infrastructure.Security.CertificateStorageService>();
builder.Services.AddSingleton<Fel.Core.Interfaces.IPublicFileStorageService, Fel.Infrastructure.Storage.PublicFileStorageService>();
builder.Services.AddTransient<Fel.Core.Interfaces.IXmlSignerService, Fel.Infrastructure.Security.XadesSignerService>();

// Dependency Injection for XML Builder
builder.Services.AddTransient<Fel.Core.Interfaces.IXmlBuilderService, Fel.Infrastructure.Services.XmlBuilderService>();
builder.Services.AddTransient<Fel.Infrastructure.Services.DianResolutionParserService>();
builder.Services.AddScoped<Fel.Api.Tenant.Services.IClinicalValidationService, Fel.Api.Tenant.Services.ClinicalValidationService>();
// El contenedor FEV-RIPS corre en el VPS de Bogotá (el MinSalud bloquea por geolocalización los
// accesos desde fuera de Colombia) y se alcanza por un túnel WireGuard cifrado, en
// https://10.10.0.2:9443, con un certificado autofirmado. Nunca queda expuesto a internet: el
// puerto solo acepta conexiones desde la IP del droplet. Por eso se acepta el certificado sin
// validar, y solo para este cliente específico.
builder.Services.AddHttpClient<Fel.Api.Tenant.Services.MinSalud.IMinSaludMuvService, Fel.Api.Tenant.Services.MinSalud.MinSaludMuvService>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });

// HandlerLifetime al mínimo permitido (1 segundo — SetHandlerLifetime no acepta TimeSpan.Zero):
// sin esto, IHttpClientFactory reutiliza el mismo HttpClientHandler (y su
// CookieContainer) hasta por 2 minutos entre llamadas — si dos registros de habilitación distintos
// caen en esa ventana, las cookies de sesión de uno contaminan al otro y la DIAN responde con un
// error genérico de su aplicación en vez de la página real.
builder.Services.AddHttpClient<Fel.Infrastructure.Services.DianHabilitationScraperService>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        UseCookies = true,
        CookieContainer = new System.Net.CookieContainer(),
        AllowAutoRedirect = true
    })
    .SetHandlerLifetime(TimeSpan.FromSeconds(1));

builder.Services.AddScoped<Fel.Infrastructure.Services.DianTestSetSubmissionService>();
builder.Services.AddScoped<Fel.Infrastructure.Services.BillingMetricsService>();
builder.Services.AddScoped<Fel.Infrastructure.Services.DocumentLegendService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
// Documentación pública del API v1 (documentos independientes de factura, incl. RIPS): va
// disponible en todos los ambientes, no solo en Desarrollo — igual que el Swagger de
// Fel.Api.Integration. Ruta propia ("swagger-v1") para no chocar con la de ese otro servicio,
// que ya ocupa "/swagger" en api.facil-factura.pro.
app.MapOpenApi();
app.UseSwagger(c =>
{
    c.RouteTemplate = "swagger-v1/{documentName}/swagger.json";
});
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger-v1/v1/swagger.json", "Facil Factura API v1");
    c.DocumentTitle = "Facil Factura API v1";
    c.RoutePrefix = "swagger-v1";
});

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseStaticFiles();

// 1. Activar el limitador de velocidad (Rate Limiting)
app.UseRateLimiter();

// 2. Activar el interceptor de Seguridad HMAC (Firmas de payload)
app.UseMiddleware<HmacAuthenticationMiddleware>();

// 3. Validar el JWT y, cuando la petición trae x-tenant-id, exigir que coincida con la claim
// TenantId del token — antes ese header se aceptaba sin ninguna verificación.
app.UseAuthentication();
app.UseMiddleware<SessionHeaderGuardMiddleware>();
app.UseAuthorization();

app.MapGet("/", () => "FEL API is running.");
app.MapControllers();

// Migraciones: solo comodidad de desarrollo local.
// En producción las aplica el servicio fel-migrator antes de arrancar cualquier API
// (ver deploy/Dockerfile.migrator). Cinco servicios comparten FelDb: si migraran al
// arrancar competirían entre sí, y uno podría cambiar el esquema bajo otro ya en marcha.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<FelDbContext>();
    await db.Database.MigrateAsync();
}

// Call seeder for RIPS
await RipsDataSeeder.SeedRipsDataAsync(app.Services);

app.Run();

