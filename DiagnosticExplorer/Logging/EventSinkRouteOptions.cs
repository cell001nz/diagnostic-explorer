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

public enum RouteValueSource
{
    Fixed,
    LoggerSuffix,
}

[TypeConverter(typeof(RouteValueConverter))]
public sealed class RouteValue
{
    public RouteValueSource Source { get; set; }

    public string Value { get; set; }

    public static RouteValue LoggerSuffix => new() { Source = RouteValueSource.LoggerSuffix };

    public static RouteValue Fixed(string value) => new() { Value = value };

    public static implicit operator RouteValue(string value) => Fixed(value);
}

public sealed class RouteValueConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
    {
        return value is string text ? RouteValue.Fixed(text) : base.ConvertFrom(context, culture, value);
    }
}

public sealed class EventSinkRouteOptions
{
    public EventSinkRouteMatchMode MatchMode { get; set; } = EventSinkRouteMatchMode.AllMatches;

    public List<EventSinkRoute> Routes { get; set; } = new();

    public EventSinkRouteOptions UseMatchMode(EventSinkRouteMatchMode matchMode)
    {
        MatchMode = matchMode;
        return this;
    }

    public EventSinkRouteOptions Route(string categoryPattern, Action<EventSinkRoute> configure)
    {
        if (string.IsNullOrWhiteSpace(categoryPattern))
            throw new ArgumentException("A category pattern is required.", nameof(categoryPattern));
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        EventSinkRoute route = new() { CategoryPattern = categoryPattern };
        configure(route);
        Routes.Add(route);
        return this;
    }
}

public sealed class EventSinkRoute
{
    public string CategoryPattern { get; set; }

    public LogLevel? MinLevel { get; set; }

    public LogLevel? MaxLevel { get; set; }

    public List<EventSinkDestination> Destinations { get; set; } = new();

    public bool StopProcessing { get; set; }

    public EventSinkRoute AtLeast(LogLevel minLevel)
    {
        MinLevel = minLevel;
        return this;
    }

    public EventSinkRoute AtMost(LogLevel maxLevel)
    {
        MaxLevel = maxLevel;
        return this;
    }

    public EventSinkRoute To(string sinkCategory, string sinkName)
    {
        return To(RouteValue.Fixed(sinkCategory), RouteValue.Fixed(sinkName));
    }

    public EventSinkRoute To(RouteValue sinkCategory, RouteValue sinkName)
    {
        ValidateRouteValue(sinkCategory, nameof(sinkCategory));
        ValidateRouteValue(sinkName, nameof(sinkName));

        Destinations.Add(new EventSinkDestination { SinkCategory = sinkCategory, SinkName = sinkName });
        return this;
    }

    public EventSinkRoute StopAfterMatch(bool stopProcessing = true)
    {
        StopProcessing = stopProcessing;
        return this;
    }

    private static void ValidateRouteValue(RouteValue routeValue, string parameterName)
    {
        if (routeValue == null)
            throw new ArgumentNullException(parameterName);
        if (!Enum.IsDefined(typeof(RouteValueSource), routeValue.Source))
            throw new ArgumentOutOfRangeException(parameterName, "The route value source is invalid.");
        if (routeValue.Source == RouteValueSource.Fixed && string.IsNullOrWhiteSpace(routeValue.Value))
            throw new ArgumentException("A fixed route value is required.", parameterName);
    }
}

[TypeConverter(typeof(EventSinkDestinationConverter))]
public sealed class EventSinkDestination
{
    public RouteValue SinkName { get; set; }

    public RouteValue SinkCategory { get; set; }
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
