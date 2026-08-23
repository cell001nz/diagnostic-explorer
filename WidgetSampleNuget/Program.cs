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

namespace WidgetSampleNuget;

internal static class Program
{
    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
#if false // Enable after the DiagnosticExplorer package includes the fluent configuration API.
        DiagnosticConfiguration diagnostics = new();
        DiagnosticsConfiguration.ConfigureDiagnostics(diagnostics);
        DiagnosticManager.UseConfiguration(diagnostics);
#endif
        SampleLogging.Configure();
        try
        {
            Application.Run(new Form1());
        }
        finally
        {
            SampleLogging.Shutdown();
        }
    }
}
