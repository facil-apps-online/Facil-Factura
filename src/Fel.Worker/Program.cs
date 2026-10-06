using Fel.Core.Interfaces;
using Fel.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices((hostContext, services) =>
{
    var configuration = hostContext.Configuration;

    // Repositories and Services
    services.AddDbContext<Fel.Infrastructure.Data.FelDbContext>(options =>
        options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));
        
    services.AddScoped<ICryptoVault, Fel.Infrastructure.Security.CryptoVault>();
    // Faltaba: UblGenerator lo pide en su constructor (lo usa para el hash SHA-384 de CUFE/CUNE/
    // CUDE) — sin esto, cualquier factura que pasara por Fel.Worker fallaba al resolver
    // IUblGenerator, antes incluso de llegar a construir el XML.
    services.AddSingleton<ICryptoService, Fel.Infrastructure.Security.CryptoService>();
    services.AddScoped<IUblGenerator, Fel.Infrastructure.Ubl.UblGenerator>();
    services.AddScoped<IXmlSigner, Fel.Infrastructure.Security.XadesSigner>();
    services.AddSingleton<IMessageQueue, Fel.Infrastructure.Messaging.RedisMessageQueue>();
    // Dian Services (WCF SOAP)
    // Faltaba: DianSoapClient pide HttpClient en su constructor — sin este registro (presente en
    // los otros 4 proyectos), cualquier documento que pasara por la cola async de Fel.Worker
    // fallaba al resolver IDianSoapClient antes de poder enviarlo a la DIAN.
    services.AddHttpClient();
    services.AddHttpClient<Fel.Core.Interfaces.ICertificateProvider, Fel.Infrastructure.Certificates.ViafirmaPkcs10Provider>();
    services.AddScoped<Fel.Core.Interfaces.ICertificateProviderContextFactory, Fel.Infrastructure.Certificates.CertificateProviderContextFactory>();
    services.AddScoped<Fel.Core.Interfaces.ICertificateCsrService, Fel.Infrastructure.Certificates.CertificateCsrService>();
    services.AddSingleton<Fel.Core.Interfaces.ICertificateStorageService, Fel.Infrastructure.Security.CertificateStorageService>();
    services.AddScoped<Fel.Core.Interfaces.ICertificateAssemblyService, Fel.Infrastructure.Certificates.CertificateAssemblyService>();
    services.AddHttpClient<Fel.Core.Interfaces.ICoreApiClient, Fel.Infrastructure.Services.CoreApiClient>();
    services.AddScoped<Fel.Core.Interfaces.IEmailSender, Fel.Infrastructure.Services.SmtpEmailSender>();
    services.AddScoped<IDianSoapClient, Fel.Infrastructure.Dian.DianSoapClient>();
    services.AddScoped<IReceptionEventService, Fel.Infrastructure.Dian.ReceptionEventService>();
    services.AddScoped<Fel.Infrastructure.Dian.ReceptionEmailPollerService>();
    services.AddScoped<Fel.Infrastructure.Services.BranchCredentialResolver>();

    services.AddScoped<Fel.Infrastructure.Services.BillingMetricsService>();
    services.AddScoped<Fel.Infrastructure.Services.MonthlyBillingCutService>();
    // Set de pruebas de habilitación DIAN: corre acá y no en la API porque son varios minutos
    // (cada documento son ~20s entre transmitir y obtener el veredicto asíncrono de la DIAN).
    services.AddScoped<Fel.Infrastructure.Services.DianTestSetSubmissionService>();
    services.AddScoped<Fel.Infrastructure.Services.DianStatusCheckService>();

    services.AddHostedService<Worker>();
    services.AddHostedService<EmailWorker>(); // 🚀 Servicio secundario para Notificaciones y PDF
    services.AddHostedService<BillingCutWorker>(); // Corte mensual automático (día 1 00:00 UTC)
    services.AddHostedService<ReceptionEmailWorker>(); // Eventos de Recepción: correo de facturación electrónica
    services.AddHostedService<HabilitationWorker>(); // Set de pruebas DIAN de punta a punta
    services.AddHostedService<DianStatusPollingWorker>(); // Veredicto real (GetStatusZip) de documentos NATIVE en PROCESSING
    services.AddHostedService<CertificatePollingWorker>(); // Sincroniza solicitudes de certificado con Viafirma, ensambla el .p12 y notifica al cliente
});

var host = builder.Build();
host.Run();
