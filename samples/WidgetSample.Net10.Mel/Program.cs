using System;
using System.Windows.Forms;
using DiagnosticExplorer;
using DiagnosticExplorer.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WidgetSample.Harness;

namespace WidgetSample.Net10.Mel;

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
                    services.ConfigureDiagnosticExplorer(diagnostics => DiagnosticsConfiguration.Configure(diagnostics, context.Configuration));
                    services.AddLogging(builder =>
                    {
                        builder.SetMinimumLevel(LogLevel.Trace);
                        builder.AddDiagnosticExplorer();
                    });
                    services.AddSingleton<Form1>();
                }
            )
            .Build();
        host.Start();

        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(host.Services.GetRequiredService<Form1>());
        }
        finally
        {
            host.StopAsync().GetAwaiter().GetResult();
        }
    }
}
