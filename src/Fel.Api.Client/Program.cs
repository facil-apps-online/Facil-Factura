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
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<Fel.Core.Interfaces.ISessionTokenService, Fel.Infrastructure.Security.SessionTokenService>();

// Misma llave/algoritmo que SessionTokenService usa para firmar — ver SessionHeaderGuardMiddleware
// para cómo se aplica (solo exige el JWT cuando la petición ya trae x-client-id o x-developer-id).
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
    });

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.Converters.Add(new EmptyStringAsNullGuidConverter());
        options.JsonSerializerOptions.Converters.Add(new EmptyStringAsNullInt32Converter());
        // EF Core hace fixup automático de navegaciones bidireccionales en el mismo DbContext
        // (ej. DocumentItem.Document apunta de vuelta al Document que lo cargó vía Include). El
        // serializador por defecto no tolera esos ciclos y tira una excepción a mitad de la
        // respuesta ya iniciada, lo que tumba la conexión (ERR_HTTP2_PROTOCOL_ERROR en el cliente)
        // en vez de devolver un error limpio. IgnoreCycles serializa la propiedad que cierra el
        // ciclo como null y sigue, sin tener que anotar cada navegación con [JsonIgnore] a mano.
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddFluentValidationAutoValidation()
                .AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssemblyContaining<InvoiceRequestValidator>();

builder.Services.AddHttpClient(); // Necesario para DianSoapClient
builder.Services.AddHttpClient<Fel.Core.Interfaces.ICertificateProvider, Fel.Infrastructure.Certificates.ViafirmaPkcs10Provider>();
builder.Services.AddScoped<Fel.Core.Interfaces.ICertificateProviderContextFactory, Fel.Infrastructure.Certificates.CertificateProviderContextFactory>();
builder.Services.AddScoped<Fel.Core.Interfaces.ICertificateCsrService, Fel.Infrastructure.Certificates.CertificateCsrService>();
builder.Services.AddOpenApi(); // .NET 9 json endpoint
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "Facil-Factura.pro API (B2B)", 
        Version = "v1",
        Description = "API de IntegraciÃ³n para emisiÃ³n de Documentos ElectrÃ³nicos (DIAN)."
    });

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
builder.Services.AddHttpClient<Fel.Core.Interfaces.ICoreApiClient, Fel.Infrastructure.Services.CoreApiClient>();
builder.Services.AddHttpClient<Fel.Core.Interfaces.IFacilReportsClient, Fel.Infrastructure.Services.FacilReportsClient>();
builder.Services.AddScoped<Fel.Infrastructure.Services.PasswordResetService>();
builder.Services.AddScoped<Fel.Infrastructure.Services.ClientUserAdminService>();
builder.Services.AddScoped<Fel.Infrastructure.Services.ResolutionBranchService>();
// WCF SOAP Client
builder.Services.AddScoped<Fel.Core.Interfaces.IDianSoapClient, Fel.Infrastructure.Dian.DianSoapClient>();
builder.Services.AddScoped<Fel.Core.Interfaces.IReceptionEventService, Fel.Infrastructure.Dian.ReceptionEventService>();

builder.Services.AddDbContext<FelDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
    b => b.MigrationsAssembly("Fel.Infrastructure")));

// Dependency Injection for Security Services
builder.Services.AddSingleton<Fel.Core.Interfaces.ICryptoService, Fel.Infrastructure.Security.CryptoService>();
builder.Services.AddSingleton<Fel.Core.Interfaces.ICertificateStorageService, Fel.Infrastructure.Security.CertificateStorageService>();
builder.Services.AddSingleton<Fel.Core.Interfaces.IPublicFileStorageService, Fel.Infrastructure.Storage.PublicFileStorageService>();
builder.Services.AddSingleton<Fel.Infrastructure.Dataico.IDataicoApiService, Fel.Infrastructure.Dataico.DataicoApiService>();
builder.Services.AddScoped<Fel.Infrastructure.Dataico.DataicoCustomPdfService>();
builder.Services.AddScoped<Fel.Core.Interfaces.IEmailSender, Fel.Infrastructure.Services.SmtpEmailSender>();
builder.Services.AddTransient<Fel.Infrastructure.Services.DianRutParserService>();
builder.Services.AddTransient<Fel.Core.Interfaces.IXmlSignerService, Fel.Infrastructure.Security.XadesSignerService>();

// Dependency Injection for XML Builder
builder.Services.AddTransient<Fel.Core.Interfaces.IXmlBuilderService, Fel.Infrastructure.Services.XmlBuilderService>();

// Dependency Injection for DIAN Integration
builder.Services.AddTransient<Fel.Infrastructure.Services.DianResolutionParserService>();
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
builder.Services.AddScoped<Fel.Infrastructure.Services.RetentionCalculationService>();
builder.Services.AddScoped<Fel.Infrastructure.Services.DocumentLegendService>();
builder.Services.AddScoped<ClientPortalFilter>();

// Proveedores de emisión de documentos (uno por Client.DocumentProvider). InvoiceController
// resuelve cuál usar en tiempo de ejecución, así se agrega un integrador nuevo sin tocarlo.
builder.Services.AddScoped<Fel.Core.Interfaces.IDocumentSubmissionProvider, Fel.Infrastructure.Dataico.DataicoSubmissionProvider>();
builder.Services.AddScoped<Fel.Core.Interfaces.IDocumentSubmissionProvider, Fel.Infrastructure.Dian.NativeDianSubmissionProvider>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(c => 
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "FEL API v1");
        c.RoutePrefix = "swagger"; // Interfaz disponible en http://localhost:port/swagger
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseStaticFiles();

// 1. Activar el limitador de velocidad (Rate Limiting)
app.UseRateLimiter();

// 2. Activar el interceptor de Seguridad HMAC (Firmas de payload)
app.UseMiddleware<HmacAuthenticationMiddleware>();

// 3. Validar el JWT y, cuando la petición trae x-client-id o x-developer-id, exigir que
// coincida con la claim del token — antes esos headers se aceptaban sin ninguna verificación.
app.UseAuthentication();
app.UseMiddleware<SessionHeaderGuardMiddleware>();
app.UseAuthorization();

app.MapGet("/", () => "FEL API is running.");
app.MapControllers();

app.Run();
