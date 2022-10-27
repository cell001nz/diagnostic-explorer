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
    public static SystemStatus Instance { get; set; } = new();

    public SystemStatus()
    {
        DiagnosticManager.Register(this, "Environment", "System");

        Pid = Process.GetCurrentProcess().Id;
        User = $"{Environment.UserDomainName}\\{Environment.UserName}";
        HostMachine = Environment.MachineName;
        ProcessorCount = Environment.ProcessorCount;
        DiagnosticRequests = new RateCounter(5);
    }

    public static void Register()
    {
        Instance ??= new SystemStatus();
    }

    [Property(Category = "CPU")] public int ProcessorCount { get; private set; }

    [Property(Category = "CPU")]
    public int Threads
    {
        get {
            return Process.GetCurrentProcess().Threads.Count;
        }
    }


    [Property(Category = "CPU", FormatString = "{0:N2}")]
    public double VirtualMemory
    {
        get {
            return Process.GetCurrentProcess().PagedMemorySize64 / (1024F * 1024F);
        }
    }

    [Property(Category = "CPU", FormatString = "{0:N2}")]
    public double Memory
    {
        get {
            return Process.GetCurrentProcess().WorkingSet64 / (1024F * 1024F);
        }
    }

    public int Pid { get; set; }

    public string HostMachine { get; set; }

    public string BaseDirectory
    {
        get {
            return AppDomain.CurrentDomain.BaseDirectory;
        }
    }

    public RateCounter DiagnosticRequests { get; }

    public string User { get; set; }

    public TimeSpan UpTime
    {
        get {
            return DateTime.Now - Process.GetCurrentProcess().StartTime;
        }
    }


    [Property(FormatString = "{0:d MMM yyyy HH:mm:ss}")]
    public DateTime SystemTime
    {
        get {
            return DateTime.Now;
        }
    }


    public void RegisterDiagnosticRequest()
    {
        DiagnosticRequests?.Register(1);
    }
}