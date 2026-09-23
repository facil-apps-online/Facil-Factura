using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DevExpress.AspNetCore;
using DevExpress.AspNetCore.Reporting;
using FluentValidation;
using FluentValidation.AspNetCore;
using Fel.Api.Validations;

using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Fel.Api.Security;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddFluentValidationAutoValidation()
                .AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssemblyContaining<InvoiceRequestValidator>();

builder.Services.AddHttpClient(); // Necesario para DianSoapClient
builder.Services.AddOpenApi(); // .NET 9 json endpoint
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Facil-Factura.pro API (B2B)",
        Version = "v1",
        Description = """
            API de integración para recibir Documentos Electrónicos ante la DIAN (Colombia). Las rutas
            llevan el prefijo `co/dian` para poder sumar otros países/autoridades más adelante sin romper
            las existentes (ej. `api/co/dian/invoices`).

            Hoy solo `api/co/dian/invoices` (factura de venta estándar) está implementado de extremo a
            extremo. El resto de tipos de documento aparecen documentados en este Swagger a medida que
            se construye su generación UBL — mientras tanto no están expuestos.

            ## Autenticación (HMAC)
            Cada request debe llevar tres headers:

            - `x-api-key`: la Live API Key del emisor, o su Test API Key (prefijo `test_`) para el ambiente
              de pruebas — se obtienen y rotan desde el portal de tenant.
            - `x-api-timestamp`: hora actual en segundos Unix (UTC). Se rechaza si difiere más de 5
              minutos de la hora del servidor (protección contra replay attacks).
            - `x-api-signature`: `Base64(HMAC-SHA256(secret, "{x-api-timestamp}.{cuerpo JSON exacto del request}"))`,
              donde `secret` es el Live/Test API Secret correspondiente a la key usada. El cuerpo debe
              firmarse tal cual se envía, byte a byte — cualquier diferencia de formato invalida la firma.

            Cada endpoint de recepción devuelve `202 Accepted` con un `TrackingId`; consulta el resultado
            con `GET /api/co/dian/documents/{trackId}/status`.
            """
    });

    // URL base explícita: sin esto, el JSON no dice dónde vive la API — funciona igual dentro del
    // navegador (Swagger UI asume el origen de la página), pero un developer que descargue el JSON
    // para importarlo en Postman o generar un cliente no tendría cómo saberlo.
    c.AddServer(new OpenApiServer { Url = "https://api.facil-factura.pro" });

    // Configurar Swagger para que pida el API Key en la interfaz gráfica
    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Name = "x-api-key",
        Type = SecuritySchemeType.ApiKey,
        Description = "Live o Test API Key del emisor. Requiere además los headers x-api-timestamp y x-api-signature (ver la descripción del API) — Swagger UI no los agrega automáticamente, pruébalo desde tu propio cliente HTTP firmando la petición."
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
            PermitLimit = 100, // Max 100 request (Ajustado según solicitud)
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

builder.Services.AddDevExpressControls();
// Repositories and Services
builder.Services.AddScoped<Fel.Core.Interfaces.ICryptoVault, Fel.Infrastructure.Security.CryptoVault>();
builder.Services.AddScoped<Fel.Core.Interfaces.IUblGenerator, Fel.Infrastructure.Ubl.UblGenerator>();
builder.Services.AddScoped<Fel.Core.Interfaces.IXmlSigner, Fel.Infrastructure.Security.XadesSigner>();
builder.Services.AddSingleton<Fel.Core.Interfaces.IMessageQueue, Fel.Infrastructure.Messaging.RedisMessageQueue>();
// WCF SOAP Client
builder.Services.AddScoped<Fel.Core.Interfaces.IDianSoapClient, Fel.Infrastructure.Dian.DianSoapClient>();
builder.Services.ConfigureReportingServices(configurator => {
    configurator.ConfigureReportDesigner(designerConfigurator => {
        designerConfigurator.RegisterDataSourceWizardConfigFileConnectionStringsProvider();
    });
    configurator.ConfigureWebDocumentViewer(viewerConfigurator => {
        viewerConfigurator.UseCachedReportSourceBuilder();
    });
});

builder.Services.AddDbContext<FelDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
    b => b.MigrationsAssembly("Fel.Infrastructure")));

// Dependency Injection for Security Services
builder.Services.AddSingleton<Fel.Core.Interfaces.ICryptoService, Fel.Infrastructure.Security.CryptoService>();
builder.Services.AddSingleton<Fel.Core.Interfaces.ICertificateStorageService, Fel.Infrastructure.Security.CertificateStorageService>();
builder.Services.AddTransient<Fel.Core.Interfaces.IXmlSignerService, Fel.Infrastructure.Security.XadesSignerService>();

// Dependency Injection for XML Builder
builder.Services.AddTransient<Fel.Core.Interfaces.IXmlBuilderService, Fel.Infrastructure.Services.XmlBuilderService>();

var app = builder.Build();

// Documentación pública del API B2B: los tenants la necesitan para construir su integración
// contra este API, así que va disponible en todos los ambientes, no solo en Desarrollo. El API
// en sí sigue protegido por HMAC (x-api-key/timestamp/signature) — publicar la forma de las
// rutas no expone datos, solo el contrato.
app.MapOpenApi();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Facil Factura API v1");
    c.DocumentTitle = "Facil Factura API";
    c.RoutePrefix = "swagger"; // Interfaz disponible en https://api.facil-factura.pro/swagger
});

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseStaticFiles();

// 1. Activar el limitador de velocidad (Rate Limiting)
app.UseRateLimiter();

// 2. Activar el interceptor de Seguridad HMAC (Firmas de payload)
app.UseMiddleware<HmacAuthenticationMiddleware>();

app.UseDevExpressControls();

app.MapGet("/", () => "Facil Factura API is running.").ExcludeFromDescription();
app.MapControllers();

app.Run();
