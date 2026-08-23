#region Copyright

// Diagnostic Explorer, a .Net diagnostic toolset
// Copyright (C) 2010 Cameron Elliot
//
// This file is part of Diagnostic Explorer.
//
// Diagnostic Explorer is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Diagnostic Explorer is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public License
// along with Diagnostic Explorer.  If not, see <http://www.gnu.org/licenses/>.
//
// http://diagexplorer.sourceforge.net/

#endregion

using System;
using System.Collections.Generic;
using System.Windows.Forms;
using DiagnosticExplorer;
using DiagnosticExplorer.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WidgetSampleNuget;

internal static class Program
{
    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [STAThread]
    private static void Main()
    {
        using IHost host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((_, configuration) => configuration.AddJsonFile("config.json", optional: false, reloadOnChange: false))
            .ConfigureServices((context, services) =>
            {
                services.ConfigureDiagnosticExplorer(diagnostics => DiagnosticsConfiguration.Configure(diagnostics, context.Configuration));
                services.AddLogging(builder =>
                {
                    builder.SetMinimumLevel(LogLevel.Trace);
                    builder.AddDiagnosticExplorer();
                });
                services.AddTransient<Form1>();
            })
            .Build();
        host.Start();

        try
        {
            SampleLogging.Configure(host.Services.GetRequiredService<ILoggerFactory>());
            ApplicationConfiguration.Initialize();
            Application.Run(host.Services.GetRequiredService<Form1>());
        }
        finally
        {
            SampleLogging.Shutdown();
            host.StopAsync().GetAwaiter().GetResult();
        }
    }
}
