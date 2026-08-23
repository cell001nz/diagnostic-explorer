using System;
using System.IO;
using System.Windows.Forms;
using DiagnosticExplorer;
using DiagnosticExplorer.Log4Net;
using log4net;
using Microsoft.Extensions.Configuration;
using WidgetSample.Harness;

namespace WidgetSample.Net48.Log4Net
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "config.json"), optional: false, reloadOnChange: false)
                .Build();
            DiagnosticManager.Configure(diagnostics => DiagnosticsConfiguration.Configure(diagnostics, configuration));
            LogManager.GetRepository().ConfigureDiagnosticExplorer();
            Form1.InitializeLoggers();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}
