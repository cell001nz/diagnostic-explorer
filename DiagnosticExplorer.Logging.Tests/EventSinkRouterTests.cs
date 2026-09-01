using System.Linq;
using DiagnosticExplorer.Logging;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DiagnosticExplorer.Logging.Tests;

public class EventSinkRouterTests
{
    [Fact]
    public void MinimumOnlyRouteAcceptsItsLevelAndAllMoreSevereLevels()
    {
        EventSinkRouter router = new(new EventSinkRouteOptions { Routes = { CreateRoute("Widgets", "Warnings").AtLeast(LogLevel.Warning) } });

        Assert.False(router.IsEnabled("Widgets", LogLevel.Information));
        Assert.True(router.IsEnabled("Widgets", LogLevel.Warning));
        Assert.True(router.IsEnabled("Widgets", LogLevel.Error));
        Assert.True(router.IsEnabled("Widgets", LogLevel.Critical));
    }

    [Fact]
    public void AllMatchesFansOutToNamespaceAndSeverityDestinations()
    {
        LogEventStore store = new();
        EventSinkRouter router = new(
            new EventSinkRouteOptions
            {
                Routes =
                {
                    new EventSinkRoute
                    {
                        CategoryPattern = "Widgets",
                        Destinations =
                        {
                            new EventSinkDestination { SinkName = "Widgets", SinkCategory = "Application" },
                        },
                    },
                    new EventSinkRoute
                    {
                        CategoryPattern = "*",
                        MinLevel = LogLevel.Warning,
                        Destinations =
                        {
                            new EventSinkDestination { SinkName = "Warnings", SinkCategory = "System" },
                        },
                    },
                },
            },
            store
        );

        int writes = router.Route(new EventSinkLogEvent("Widgets.Rendering", LogLevel.Warning, "Paint failed"));

        Assert.Equal(1, writes);
        LogStreamEvent streamEvent = GetReplayEvents(store).Single();
        Assert.Equal("Widgets.Rendering", streamEvent.LoggerCategory);
        Assert.Equal((int)LogLevel.Warning, streamEvent.Level);
    }

    [Fact]
    public void MostSpecificSelectsTheLongestNamespacePrefix()
    {
        LogEventStore store = new();
        EventSinkRouter router = new(
            new EventSinkRouteOptions
            {
                MatchMode = EventSinkRouteMatchMode.MostSpecific,
                Routes = { CreateRoute("Widgets", "Widgets"), CreateRoute("Widgets.Rendering", "Rendering") },
            },
            store
        );

        router.Route(new EventSinkLogEvent("Widgets.Rendering.Canvas", LogLevel.Information, "Rendered"));

        Assert.Single(GetReplayEvents(store));
    }

    [Fact]
    public void FirstMatchUsesDeclarationOrderAndPrefixBoundaries()
    {
        LogEventStore store = new();
        EventSinkRouter router = new(
            new EventSinkRouteOptions
            {
                MatchMode = EventSinkRouteMatchMode.FirstMatch,
                Routes = { CreateRoute("Widgets", "Widgets"), CreateRoute("*", "Fallback") },
            },
            store
        );

        router.Route(new EventSinkLogEvent("WidgetShop", LogLevel.Information, "Not a widget"));

        Assert.Single(GetReplayEvents(store));
    }

    [Fact]
    public void DuplicateDestinationsAreWrittenOnce()
    {
        LogEventStore store = new();
        EventSinkRouter router = new(new EventSinkRouteOptions { Routes = { CreateRoute("Widgets", "Shared"), CreateRoute("*", "Shared") } }, store);

        int writes = router.Route(new EventSinkLogEvent("Widgets", LogLevel.Information, "Created"));

        Assert.Equal(1, writes);
        Assert.Single(GetReplayEvents(store));
    }

    [Fact]
    public void LoggerSuffixCanProvideDestinationCategory()
    {
        LogEventStore store = new();
        EventSinkRouteOptions options = new();
        options.Route("WidgetSample.Harness.Widget", route => route.To(RouteValue.LoggerSuffix, "Widget Events"));
        EventSinkRouter router = new(options, store);

        int writes = router.Route(new EventSinkLogEvent("WidgetSample.Harness.Widget.Widget X(1)", LogLevel.Information, "Refreshed"));

        Assert.Equal(1, writes);
        LogStreamRoute route = GetReplayRouting(store).Routes.Single();
        Assert.Equal(RouteValueSource.LoggerSuffix, route.Destinations.Single().Category.Source);
    }

    [Fact]
    public void LoggerSuffixCanProvideDestinationName()
    {
        LogEventStore store = new();
        EventSinkRouteOptions options = new();
        options.Route("WidgetSample.Harness.Widget", route => route.To("Widgets", RouteValue.LoggerSuffix));
        EventSinkRouter router = new(options, store);

        int writes = router.Route(new EventSinkLogEvent("WidgetSample.Harness.Widget.Widget X(1)", LogLevel.Information, "Refreshed"));

        Assert.Equal(1, writes);
        LogStreamRoute route = GetReplayRouting(store).Routes.Single();
        Assert.Equal(RouteValueSource.LoggerSuffix, route.Destinations.Single().Name.Source);
    }

    [Fact]
    public void DisabledDiagnosticsSuppressesDirectAndRoutedEventWrites()
    {
        bool wasEnabled = DiagnosticManager.Enabled;
        try
        {
            DiagnosticManager.Enabled = false;
            EventSinkRepo repo = new();
            EventSinkRouter router = new(new EventSinkRouteOptions { Routes = { CreateRoute("Widgets", "Widgets") } }, new LogEventStore());

            int writes = router.Route(new EventSinkLogEvent("Widgets", LogLevel.Information, "Ignored"));
            repo.GetSink("Direct", "Application").Info("Ignored");

            Assert.Equal(0, writes);
            Assert.Empty(repo.GetEvents());
        }
        finally
        {
            DiagnosticManager.Enabled = wasEnabled;
        }
    }

    private static EventSinkRoute CreateRoute(string categoryPattern, string sinkName)
    {
        return new EventSinkRoute
        {
            CategoryPattern = categoryPattern,
            Destinations =
            {
                new EventSinkDestination { SinkName = sinkName, SinkCategory = "Application" },
            },
        };
    }

    private static LogStreamEvent[] GetReplayEvents(LogEventStore store)
    {
        using LogEventStore.LogEventStoreSubscription subscription = store.CreateSubscription();
        return subscription.Initialization.ReplayEvents;
    }

    private static LogStreamRoutingConfiguration GetReplayRouting(LogEventStore store)
    {
        using LogEventStore.LogEventStoreSubscription subscription = store.CreateSubscription();
        return subscription.Initialization.Routing;
    }
}
