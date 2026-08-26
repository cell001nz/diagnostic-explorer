using System.Collections.Generic;
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
}
