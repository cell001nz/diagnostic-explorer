using System;
using System.Windows.Forms;
using DiagnosticExplorer;
using DiagnosticExplorer.Log4Net;
using log4net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WidgetSample.Harness;

namespace WidgetSample.Net48.Log4Net
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            using (
                IHost host = Host.CreateDefaultBuilder()
                    .ConfigureAppConfiguration((_, configuration) => configuration.AddJsonFile("config.json", optional: false, reloadOnChange: false))
                    .ConfigureServices(
                        (context, services) =>
                        {
                            services.ConfigureDiagnosticExplorer(diagnostics =>
                                DiagnosticsConfiguration.Configure(diagnostics, context.Configuration)
                            );
                            services.AddSingleton<Form1>();
                        }
                    )
                    .Build()
            )
            {
                LogManager.GetRepository().ConfigureDiagnosticExplorer();
                host.Start();

                try
                {
                    Form1.InitializeLoggers();
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(host.Services.GetRequiredService<Form1>());
                }
                finally
                {
                    host.StopAsync().GetAwaiter().GetResult();
                }
            }
        }
    }
}
