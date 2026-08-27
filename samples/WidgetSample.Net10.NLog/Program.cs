using System;
using System.Windows.Forms;
using DiagnosticExplorer;
using DiagnosticExplorer.NLog;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NLog;
using NLog.Config;
using WidgetSample.Harness;

namespace WidgetSample.Net10.NLog;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using IHost host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((_, configuration) => configuration.AddJsonFile("config.json", optional: false, reloadOnChange: false))
            .ConfigureServices(
                (context, services) =>
                {
                    services.ConfigureDiagnosticExplorer(
                        context.Configuration,
                        diagnostics => DiagnosticsConfiguration.Configure(diagnostics, context.Configuration)
                    );
                    LoggingConfiguration logging = new();
                    logging.AddDiagnosticExplorer();
                    LogManager.Configuration = logging;
                    services.AddSingleton<Form1>();
                }
            )
            .Build();
        host.Start();

        try
        {
            Form1.InitializeLoggers();
            ApplicationConfiguration.Initialize();
            Application.Run(host.Services.GetRequiredService<Form1>());
        }
        finally
        {
            host.StopAsync().GetAwaiter().GetResult();
            LogManager.Shutdown();
        }
    }
}
