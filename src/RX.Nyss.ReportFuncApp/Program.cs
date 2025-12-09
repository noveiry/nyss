using System.Reflection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RX.Nyss.ReportFuncApp.Configuration;

namespace RX.Nyss.ReportFuncApp;

public class Program
{
    public static void Main()
    {
        var host = new HostBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                var currentDirectory = Directory.GetCurrentDirectory();
                var localSettingsFile = Path.Combine(currentDirectory, "local.settings.json");

                config
                    .AddJsonFile(localSettingsFile, optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables()
                    .AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true);
            })
            .ConfigureServices((context, services) =>
            {
                var configuration = context.Configuration;

                // Application Insights for Azure Functions
                services.AddApplicationInsightsTelemetryWorkerService();
                services.ConfigureFunctionsApplicationInsights();

                // Bind configuration
                var nyssFuncAppConfig = configuration.Get<NyssReportFuncAppConfig>();
                services.AddSingleton<IConfig>(nyssFuncAppConfig);

                // HTTP Client
                services.AddHttpClient();
            })
            .Build();

        host.Run();
    }
}