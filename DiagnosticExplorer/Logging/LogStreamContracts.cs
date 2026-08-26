using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace DiagnosticExplorer.Logging;

[DataContract]
public sealed class LogStreamEvent
{
    [DataMember(Order = 1)]
    public string StreamId { get; set; }

    [DataMember(Order = 2)]
    public long Sequence { get; set; }

    [DataMember(Order = 3)]
    public DateTime TimestampUtc { get; set; }

    [DataMember(Order = 4)]
    public string LoggerCategory { get; set; }

    [DataMember(Order = 5)]
    public int Level { get; set; }

    [DataMember(Order = 6)]
    public string Message { get; set; }

    [DataMember(Order = 7)]
    public string Detail { get; set; }

    [DataMember(Order = 8)]
    public int EventId { get; set; }

    [DataMember(Order = 9)]
    public string EventName { get; set; }
}

public enum LoggerNameMatchMode
{
    Exact,
    Prefix,
    Contains,
    Wildcard,
}

[DataContract]
public sealed class LogStreamRouteValue
{
    [DataMember(Order = 1)]
    public RouteValueSource Source { get; set; }

    [DataMember(Order = 2)]
    public string Value { get; set; }
}

[DataContract]
public sealed class LogStreamRouteDestination
{
    [DataMember(Order = 1)]
    public LogStreamRouteValue Category { get; set; }

    [DataMember(Order = 2)]
    public LogStreamRouteValue Name { get; set; }
}

[DataContract]
public sealed class LogStreamRoute
{
    [DataMember(Order = 1)]
    public int Order { get; set; }

    [DataMember(Order = 2)]
    public string LoggerName { get; set; }

    [DataMember(Order = 3)]
    public LoggerNameMatchMode LoggerNameMatchMode { get; set; }

    [DataMember(Order = 4)]
    public int? MinLevel { get; set; }

    [DataMember(Order = 5)]
    public int? MaxLevel { get; set; }

    [DataMember(Order = 6)]
    public bool StopProcessing { get; set; }

    [DataMember(Order = 7)]
    public List<LogStreamRouteDestination> Destinations { get; set; } = new();
}

[DataContract]
public sealed class LogStreamRoutingConfiguration
{
    [DataMember(Order = 1)]
    public EventSinkRouteMatchMode MatchMode { get; set; }

    [DataMember(Order = 2)]
    public List<LogStreamRoute> Routes { get; set; } = new();
}

[DataContract]
public sealed class LogStreamInitialization
{
    [DataMember(Order = 1)]
    public string StreamId { get; set; }

    [DataMember(Order = 2)]
    public LogStreamRoutingConfiguration Routing { get; set; }

    [DataMember(Order = 3)]
    public LogStreamEvent[] ReplayEvents { get; set; } = Array.Empty<LogStreamEvent>();

    [DataMember(Order = 4)]
    public long HighWatermark { get; set; }
}
