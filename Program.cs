using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WhatsappBusiness.CloudApi.Configurations;
using WhatsappBusiness.CloudApi.Extensions;

namespace Raticketbot;

public class Program
{
    public static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((ctx, cfg) =>
            {
                cfg.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                cfg.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);
                cfg.AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                services.AddHttpClient();

                // Bind WhatsApp config from configuration or environment variables
                var whatsAppSection = context.Configuration.GetSection("WhatsAppBusinessCloudApiConfiguration");
                var whatsAppConfig = new WhatsAppBusinessCloudApiConfig()
                {
                    WhatsAppBusinessPhoneNumberId = whatsAppSection["WhatsAppBusinessPhoneNumberId"],
                    WhatsAppBusinessAccountId = whatsAppSection["WhatsAppBusinessAccountId"],
                    WhatsAppBusinessId = whatsAppSection["WhatsAppBusinessId"],
                    AccessToken = whatsAppSection["AccessToken"]
                };

                // Register the WhatsApp Business Cloud Api client (extension from package)
                services.AddWhatsAppBusinessCloudApiService(whatsAppConfig);

                // App services
                services.AddTransient<Poller>();
                services.AddSingleton<Notifier>();
                services.AddHostedService<PollingHostedService>();
            })
            .ConfigureLogging((ctx, logging) => logging.AddConsole())
            .Build();

        await host.RunAsync();
    }
}
