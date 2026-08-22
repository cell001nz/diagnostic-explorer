using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace DiagnosticExplorer.Logging;

public enum EventSinkRouteMatchMode
{
    AllMatches,
    MostSpecific,
    FirstMatch,
}

public sealed class EventSinkRouteOptions
{
    public EventSinkRouteMatchMode MatchMode { get; set; } = EventSinkRouteMatchMode.AllMatches;

    public List<EventSinkRoute> Routes { get; set; } = new();
}

public sealed class EventSinkRoute
{
    public string CategoryPattern { get; set; }

    public LogLevel? MinLevel { get; set; }

    public LogLevel? MaxLevel { get; set; }

    public List<EventSinkDestination> Destinations { get; set; } = new();

    public bool StopProcessing { get; set; }
}

public sealed class EventSinkDestination
{
    public string SinkName { get; set; }

    public string SinkCategory { get; set; }
}
