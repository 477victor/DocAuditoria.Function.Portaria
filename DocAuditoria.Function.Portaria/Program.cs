using DocAuditoria.Function.Portaria.Interfaces;
using DocAuditoria.Function.Portaria.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OfficeOpenXml;

ExcelPackage.LicenseContext = LicenseContext.NonCommercial;


var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        services.AddHttpClient("ValideApi", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(20);
            client.BaseAddress = new Uri("https://valide-api-v1.azurewebsites.net");


            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("x-api-key", "83f0cbdfbb9056590ef2e7916b734795713a1721c28f0f0aa10d6ef8635a8905");
        });

        services.AddScoped<IMainApiIntegrationService, MainApiIntegrationService>();
        services.AddScoped<IGeradorArquivoService, GeradorArquivoService>();
        services.AddScoped<IEmailService, EmailService>();
    })
    .Build();

host.Run();