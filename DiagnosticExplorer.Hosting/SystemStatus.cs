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
using System.Diagnostics;
using log4net;

namespace DiagnosticExplorer;

[Serializable]
internal class SystemStatus
{
    private readonly Process _process;

    public static SystemStatus Instance { get; private set; }

    public SystemStatus()
    {
        _process = BestEffort(Process.GetCurrentProcess);
        Pid = ReadProcess(process => process.Id);
        User = BestEffort(() => $"{Environment.UserDomainName}\\{Environment.UserName}");
        HostMachine = BestEffort(() => Environment.MachineName);
        ProcessorCount = BestEffort(() => Environment.ProcessorCount);
        DiagnosticRequests = BestEffort(() => new RateCounter(5));
    }

    private static T BestEffort<T>(Func<T> getValue)
    {
        try
        {
            return getValue();
        }
        catch
        {
            return default;
        }
    }

    private T ReadProcess<T>(Func<Process, T> getValue)
    {
        if (_process == null)
            return default;

        return BestEffort(() =>
        {
            _process.Refresh();
            return getValue(_process);
        });
    }

    public static void Register()
    {
        Instance ??= new SystemStatus();
    }

    [DiagnosticProperty(Category = "CPU")]
    public int ProcessorCount { get; private set; }

    [DiagnosticProperty(Category = "CPU")]
    public int Threads
    {
        get { return ReadProcess(process => process.Threads.Count); }
    }

    [DiagnosticProperty(Category = "CPU", FormatString = "{0:N2}")]
    public double VirtualMemory
    {
        get { return ReadProcess(process => process.PagedMemorySize64 / (1024F * 1024F)); }
    }

    [DiagnosticProperty(Category = "CPU", FormatString = "{0:N2}")]
    public double Memory
    {
        get { return ReadProcess(process => process.WorkingSet64 / (1024F * 1024F)); }
    }

    public int Pid { get; set; }

    public string HostMachine { get; set; }

    public string BaseDirectory
    {
        get { return BestEffort(() => AppDomain.CurrentDomain.BaseDirectory); }
    }

    public RateCounter DiagnosticRequests { get; }

    public string User { get; set; }

    public TimeSpan UpTime
    {
        get { return ReadProcess(process => DateTime.Now - process.StartTime); }
    }

    [DiagnosticProperty(FormatString = "{0:d MMM yyyy HH:mm:ss}")]
    public DateTime SystemTime
    {
        get { return BestEffort(() => DateTime.Now); }
    }

    public void RegisterDiagnosticRequest()
    {
        DiagnosticRequests?.Register(1);
    }
}
