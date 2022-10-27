using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DiagnosticExplorer.Common;

public class RealtimeOptions
{
    public const string Realtime = "Realtime";
    public TimeSpan RenewTime { get; set; } = TimeSpan.FromSeconds(22);
    public string? RegistrationRedirect { get; set; }

}