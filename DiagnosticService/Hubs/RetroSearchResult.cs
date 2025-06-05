using System;
using System.Collections.Generic;
using DiagnosticExplorer;
using Diagnostics.Service.Common.Transport;

namespace Diagnostics.Service.Common.Hubs;

public class RetroSearchResult
{
    public int SearchId { get; set; }
    public decimal Progress { get; set; }
    public string Info { get; set; }
    public IList<RetroMsg> Results { get; set; } = Array.Empty<RetroMsg>();
}