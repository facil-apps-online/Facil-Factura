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
    services.AddScoped<IUblGenerator, Fel.Infrastructure.Ubl.UblGenerator>();
    services.AddScoped<IXmlSigner, Fel.Infrastructure.Security.XadesSigner>();
    services.AddSingleton<IMessageQueue, Fel.Infrastructure.Messaging.RedisMessageQueue>();
    // Dian Services (WCF SOAP)
    services.AddScoped<IDianSoapClient, Fel.Infrastructure.Dian.DianSoapClient>();

    services.AddHostedService<Worker>();
    services.AddHostedService<EmailWorker>(); // 🚀 Servicio secundario para Notificaciones y PDF
});

var host = builder.Build();
host.Run();
