using System;
using System.Windows.Forms;
using DiagnosticExplorer;
using DiagnosticExplorer.Serilog;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using WidgetSample.Harness;

namespace WidgetSample.Net10.Serilog;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        global::Serilog.Core.Logger logger = null;
        using IHost host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((_, configuration) => configuration.AddJsonFile("config.json", optional: false, reloadOnChange: false))
            .ConfigureServices(
                (context, services) =>
                {
                    services.ConfigureDiagnosticExplorer(diagnostics => DiagnosticsConfiguration.Configure(diagnostics, context.Configuration));
                    logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.DiagnosticExplorer().CreateLogger();
                    services.AddSingleton<Form1>();
                }
            )
            .Build();
        host.Start();

        try
        {
            Form1.InitializeLoggers(logger);
            ApplicationConfiguration.Initialize();
            Application.Run(host.Services.GetRequiredService<Form1>());
        }
        finally
        {
            host.StopAsync().GetAwaiter().GetResult();
            logger.Dispose();
        }
    }
}
