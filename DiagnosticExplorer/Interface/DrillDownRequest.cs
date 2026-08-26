using System.Collections.Generic;
using DiagnosticExplorer.Logging;
using ProtoBuf;

namespace DiagnosticExplorer;

[ProtoContract(UseProtoMembersOnly = true)]
public sealed class DrillDownRequest
{
    [ProtoMember(1)]
    public List<string> ObjectPaths { get; set; } = new();
}

[ProtoContract(UseProtoMembersOnly = true)]
public sealed class DrillDownResponse
{
    [ProtoMember(1)]
    public DiagnosticResponse Diagnostics { get; set; } = new();

    [ProtoMember(2)]
    public int DisplayedCount { get; set; }

    [ProtoMember(3)]
    public int? TotalCount { get; set; }

    [ProtoMember(4)]
    public bool IsTruncated { get; set; }

    [ProtoMember(5)]
    public string ErrorMessage { get; set; }

    [ProtoMember(6)]
    public string ErrorDetail { get; set; }

    [ProtoMember(7)]
    public List<DrillDownEventViewDefinition> EventViews { get; set; } = new();
}

[ProtoContract(UseProtoMembersOnly = true)]
public sealed class DrillDownEventViewDefinition
{
    [ProtoMember(1)]
    public string Id { get; set; }

    [ProtoMember(2)]
    public string Category { get; set; }

    [ProtoMember(3)]
    public string Name { get; set; }

    [ProtoMember(4)]
    public List<DrillDownEventMatcher> Matchers { get; set; } = new();
}

[ProtoContract(UseProtoMembersOnly = true)]
public sealed class DrillDownEventMatcher
{
    [ProtoMember(1)]
    public string LoggerName { get; set; }

    [ProtoMember(2)]
    public LoggerNameMatchMode MatchMode { get; set; }

    [ProtoMember(3)]
    public int? MinLevel { get; set; }

    [ProtoMember(4)]
    public int? MaxLevel { get; set; }
}
