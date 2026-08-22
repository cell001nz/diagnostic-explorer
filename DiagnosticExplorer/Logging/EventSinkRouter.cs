using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace DiagnosticExplorer.Logging;

public sealed class EventSinkRouter
{
    private static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;
    private readonly EventSinkRepo _sinkRepo;
    private readonly CompiledRoute[] _routes;
    private readonly EventSinkRouteMatchMode _matchMode;

    public EventSinkRouter(EventSinkRouteOptions options, EventSinkRepo sinkRepo = null)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        if (!Enum.IsDefined(typeof(EventSinkRouteMatchMode), options.MatchMode))
            throw new ArgumentOutOfRangeException(nameof(options), "The configured match mode is invalid.");

        _sinkRepo = sinkRepo ?? EventSinkRepo.Default;
        _matchMode = options.MatchMode;
        _routes = options.Routes?.Select((route, index) => new CompiledRoute(route, index)).ToArray() ?? Array.Empty<CompiledRoute>();
    }

    public bool IsEnabled(string category, LogLevel level)
    {
        return FindMatchingRoutes(category, level).Count != 0;
    }

    public int Route(EventSinkLogEvent logEvent)
    {
        if (logEvent == null)
            throw new ArgumentNullException(nameof(logEvent));
        if (!global::DiagnosticExplorer.DiagnosticManager.Enabled)
            return 0;

        List<CompiledRoute> routes = FindMatchingRoutes(logEvent.Category, logEvent.Level);
        if (routes.Count == 0)
            return 0;

        HashSet<string> destinations = new(Comparer);
        int writes = 0;
        foreach (CompiledRoute route in SelectRoutes(routes))
        {
            foreach (EventSinkDestination destination in route.Destinations)
            {
                string key = $"{destination.SinkName}\u001f{destination.SinkCategory}";
                if (!destinations.Add(key))
                    continue;

                _sinkRepo.GetSink(destination.SinkName, destination.SinkCategory).LogEvent((int)logEvent.Level, logEvent.Message, logEvent.Detail);
                writes++;
            }
        }

        return writes;
    }

    private List<CompiledRoute> FindMatchingRoutes(string category, LogLevel level)
    {
        category ??= string.Empty;
        List<CompiledRoute> matches = new();
        foreach (CompiledRoute route in _routes)
        {
            if (!route.Matches(category, level))
                continue;

            matches.Add(route);
            if (route.StopProcessing)
                break;
        }

        return matches;
    }

    private IEnumerable<CompiledRoute> SelectRoutes(List<CompiledRoute> routes)
    {
        switch (_matchMode)
        {
            case EventSinkRouteMatchMode.AllMatches:
                return routes;

            case EventSinkRouteMatchMode.MostSpecific:
                return new[] { routes.OrderByDescending(route => route.Specificity).ThenBy(route => route.Order).First() };

            case EventSinkRouteMatchMode.FirstMatch:
                return new[] { routes[0] };

            default:
                throw new InvalidOperationException("The configured match mode is invalid.");
        }
    }

    private sealed class CompiledRoute
    {
        public CompiledRoute(EventSinkRoute route, int order)
        {
            if (route == null)
                throw new ArgumentException("A route cannot be null.", nameof(route));
            if (string.IsNullOrWhiteSpace(route.CategoryPattern))
                throw new ArgumentException("A route category pattern is required.", nameof(route));
            if (route.CategoryPattern != "*" && route.CategoryPattern.EndsWith(".", StringComparison.Ordinal))
                throw new ArgumentException("A route category pattern cannot end with a period.", nameof(route));
            if (route.MinLevel > route.MaxLevel)
                throw new ArgumentException("A route minimum level cannot exceed its maximum level.", nameof(route));
            if (route.Destinations == null || route.Destinations.Count == 0)
                throw new ArgumentException("A route must define at least one destination.", nameof(route));
            if (
                route.Destinations.Any(destination =>
                    destination == null || string.IsNullOrWhiteSpace(destination.SinkName) || string.IsNullOrWhiteSpace(destination.SinkCategory)
                )
            )
                throw new ArgumentException("Each route destination requires a sink name and category.", nameof(route));

            CategoryPattern = route.CategoryPattern.Trim();
            MinLevel = route.MinLevel;
            MaxLevel = route.MaxLevel;
            Destinations = route.Destinations.ToArray();
            StopProcessing = route.StopProcessing;
            Order = order;
            Specificity = CategoryPattern == "*" ? 0 : CategoryPattern.Length;
        }

        public string CategoryPattern { get; }

        public LogLevel? MinLevel { get; }

        public LogLevel? MaxLevel { get; }

        public EventSinkDestination[] Destinations { get; }

        public bool StopProcessing { get; }

        public int Order { get; }

        public int Specificity { get; }

        public bool Matches(string category, LogLevel level)
        {
            if (MinLevel.HasValue && level < MinLevel.Value)
                return false;
            if (MaxLevel.HasValue && level > MaxLevel.Value)
                return false;
            if (CategoryPattern == "*")
                return true;
            if (Comparer.Equals(category, CategoryPattern))
                return true;

            return category.Length > CategoryPattern.Length
                && category.StartsWith(CategoryPattern, StringComparison.OrdinalIgnoreCase)
                && category[CategoryPattern.Length] == '.';
        }
    }
}
