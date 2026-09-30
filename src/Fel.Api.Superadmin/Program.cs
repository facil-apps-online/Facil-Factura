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
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// [Authorize] por defecto en todos los controladores — antes ningún middleware validaba el JWT
// que SuperadminAuthController.Login sí emite, así que cualquiera podía llamar cualquier endpoint
// de superadmin (el portal de más privilegio, administra todos los Tenants) sin credenciales.
// Las acciones que deben quedar públicas (login, setup inicial, recuperar contraseña) llevan
// [AllowAnonymous] explícito en SuperadminAuthController.
builder.Services.AddControllers(options =>
{
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.Authorization.AuthorizeFilter());
});
builder.Services.AddAuthorization();

// Misma llave y algoritmo que usa SuperadminAuthController.Login para firmar el JWT — si se
// cambia una, hay que cambiar la otra.
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
// WCF SOAP Client
builder.Services.AddScoped<Fel.Core.Interfaces.IDianSoapClient, Fel.Infrastructure.Dian.DianSoapClient>();

builder.Services.AddDbContext<FelDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
    b => b.MigrationsAssembly("Fel.Infrastructure")));

// Dependency Injection for Security Services
builder.Services.AddSingleton<Fel.Core.Interfaces.ICryptoService, Fel.Infrastructure.Security.CryptoService>();
builder.Services.AddHttpClient<Fel.Infrastructure.Services.DianHabilitationScraperService>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        UseCookies = true,
        CookieContainer = new System.Net.CookieContainer(),
        AllowAutoRedirect = true
    });
builder.Services.AddScoped<Fel.Infrastructure.Services.DianTestSetSubmissionService>();
builder.Services.AddScoped<Fel.Infrastructure.Services.BillingMetricsService>();
builder.Services.AddScoped<Fel.Infrastructure.Services.MonthlyBillingCutService>();
builder.Services.AddSingleton<Fel.Core.Interfaces.ICertificateStorageService, Fel.Infrastructure.Security.CertificateStorageService>();
builder.Services.AddTransient<Fel.Core.Interfaces.IXmlSignerService, Fel.Infrastructure.Security.XadesSignerService>();

// Dependency Injection for XML Builder
builder.Services.AddTransient<Fel.Core.Interfaces.IXmlBuilderService, Fel.Infrastructure.Services.XmlBuilderService>();
// Lee el RUT en PDF para prellenar el alta de tenants sin transcribir a mano.
builder.Services.AddTransient<Fel.Infrastructure.Services.DianRutParserService>();

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

// 3. Validar el JWT (debe ir antes de Authorization, y antes de MapControllers)
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "FEL API is running.");
app.MapControllers();

app.Run();

