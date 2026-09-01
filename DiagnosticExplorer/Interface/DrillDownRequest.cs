using System.Collections.Generic;
using DiagnosticExplorer.Logging;

namespace DiagnosticExplorer;
public sealed class DrillDownRequest
{
    public List<string> ObjectPaths { get; set; } = new();
    public bool JsonHover { get; set; }
    public bool ExcludeEventViews { get; set; }
}
public sealed class DrillDownResponse
{
    public DiagnosticResponse Diagnostics { get; set; } = new();
    public int DisplayedCount { get; set; }
    public int? TotalCount { get; set; }
    public bool IsTruncated { get; set; }
    public string ErrorMessage { get; set; }
    public string ErrorDetail { get; set; }
    public List<DrillDownEventViewDefinition> EventViews { get; set; } = new();
    public string Json { get; set; }
}
public sealed class DrillDownEventViewDefinition
{
    public string Id { get; set; }
    public string Category { get; set; }
    public string Name { get; set; }
    public List<DrillDownEventMatcher> Matchers { get; set; } = new();
}
public sealed class DrillDownEventMatcher
{
    public string LoggerName { get; set; }
    public LoggerNameMatchMode MatchMode { get; set; }
    public int? MinLevel { get; set; }
    public int? MaxLevel { get; set; }
}
