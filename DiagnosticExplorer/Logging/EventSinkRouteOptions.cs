using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
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

[TypeConverter(typeof(EventSinkDestinationConverter))]
public sealed class EventSinkDestination
{
    public string SinkName { get; set; }

    public string SinkCategory { get; set; }
}

public sealed class EventSinkDestinationConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
    {
        if (value is not string destination)
            return base.ConvertFrom(context, culture, value);

        string[] parts = destination.Split('/');
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            throw new FormatException("A destination string must use the format 'SinkCategory/SinkName'.");
        }

        return new EventSinkDestination { SinkCategory = parts[0].Trim(), SinkName = parts[1].Trim() };
    }
}
