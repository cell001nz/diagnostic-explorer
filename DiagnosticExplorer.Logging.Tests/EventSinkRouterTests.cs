using System.Linq;
using DiagnosticExplorer.Logging;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DiagnosticExplorer.Logging.Tests;

public class EventSinkRouterTests
{
    [Fact]
    public void AllMatchesFansOutToNamespaceAndSeverityDestinations()
    {
        EventSinkRepo repo = new();
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
            repo
        );

        int writes = router.Route(new EventSinkLogEvent("Widgets.Rendering", LogLevel.Warning, "Paint failed"));

        Assert.Equal(2, writes);
        Assert.Single(repo.GetSink("Widgets", "Application").Events);
        Assert.Single(repo.GetSink("Warnings", "System").Events);
    }

    [Fact]
    public void MostSpecificSelectsTheLongestNamespacePrefix()
    {
        EventSinkRepo repo = new();
        EventSinkRouter router = new(
            new EventSinkRouteOptions
            {
                MatchMode = EventSinkRouteMatchMode.MostSpecific,
                Routes = { CreateRoute("Widgets", "Widgets"), CreateRoute("Widgets.Rendering", "Rendering") },
            },
            repo
        );

        router.Route(new EventSinkLogEvent("Widgets.Rendering.Canvas", LogLevel.Information, "Rendered"));

        Assert.Empty(repo.GetSink("Widgets", "Application").Events);
        Assert.Single(repo.GetSink("Rendering", "Application").Events);
    }

    [Fact]
    public void FirstMatchUsesDeclarationOrderAndPrefixBoundaries()
    {
        EventSinkRepo repo = new();
        EventSinkRouter router = new(
            new EventSinkRouteOptions
            {
                MatchMode = EventSinkRouteMatchMode.FirstMatch,
                Routes = { CreateRoute("Widgets", "Widgets"), CreateRoute("*", "Fallback") },
            },
            repo
        );

        router.Route(new EventSinkLogEvent("WidgetShop", LogLevel.Information, "Not a widget"));

        Assert.Empty(repo.GetSink("Widgets", "Application").Events);
        Assert.Single(repo.GetSink("Fallback", "Application").Events);
    }

    [Fact]
    public void DuplicateDestinationsAreWrittenOnce()
    {
        EventSinkRepo repo = new();
        EventSinkRouter router = new(new EventSinkRouteOptions { Routes = { CreateRoute("Widgets", "Shared"), CreateRoute("*", "Shared") } }, repo);

        int writes = router.Route(new EventSinkLogEvent("Widgets", LogLevel.Information, "Created"));

        Assert.Equal(1, writes);
        Assert.Single(repo.GetSink("Shared", "Application").Events);
    }

    [Fact]
    public void DisabledDiagnosticsSuppressesDirectAndRoutedEventWrites()
    {
        bool wasEnabled = DiagnosticManager.Enabled;
        try
        {
            DiagnosticManager.Enabled = false;
            EventSinkRepo repo = new();
            EventSinkRouter router = new(new EventSinkRouteOptions { Routes = { CreateRoute("Widgets", "Widgets") } }, repo);

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

    [Fact]
    public void DisabledDiagnosticsDoesNotRegisterObjects()
    {
        bool wasEnabled = DiagnosticManager.Enabled;
        object diagnosticObject = new();
        try
        {
            DiagnosticManager.Enabled = false;

            DiagnosticManager.Register(diagnosticObject, "Ignored", "Application");

            Assert.DoesNotContain(
                DiagnosticManager.GetRegisteredObjects(),
                registeredObject => ReferenceEquals(registeredObject.Object, diagnosticObject)
            );
        }
        finally
        {
            DiagnosticManager.Enabled = wasEnabled;
            DiagnosticManager.Unregister(diagnosticObject);
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
}
