using System.Linq;
using System.Threading.Tasks;
using DiagnosticExplorer.Logging;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DiagnosticExplorer.Logging.Tests;

public class LogEventStoreTests
{
    [Fact]
    public void RetainsOnlyTheConfiguredMostRecentEvents()
    {
        LogEventStore store = new(new LogEventRetentionOptions().WithMaxEvents(2));

        store.Publish(new EventSinkLogEvent("Widgets", LogLevel.Information, "First"));
        store.Publish(new EventSinkLogEvent("Widgets", LogLevel.Information, "Second"));
        store.Publish(new EventSinkLogEvent("Widgets", LogLevel.Information, "Third"));

        using LogEventStore.LogEventStoreSubscription subscription = store.CreateSubscription();
        Assert.Equal(new[] { "Second", "Third" }, subscription.Initialization.ReplayEvents.Select(streamEvent => streamEvent.Message));
        Assert.Equal(3, subscription.Initialization.HighWatermark);
    }

    [Fact]
    public async Task SubscriptionSeparatesReplayFromLaterLiveEvents()
    {
        LogEventStore store = new();
        store.Publish(new EventSinkLogEvent("Widgets", LogLevel.Information, "Replay"));

        using LogEventStore.LogEventStoreSubscription subscription = store.CreateSubscription();
        store.Publish(new EventSinkLogEvent("Widgets", LogLevel.Warning, "Live"));

        LogStreamEvent liveEvent = await subscription.Events.ReadAsync();
        Assert.Single(subscription.Initialization.ReplayEvents);
        Assert.Equal(subscription.Initialization.HighWatermark + 1, liveEvent.Sequence);
        Assert.Equal("Live", liveEvent.Message);
    }

    [Fact]
    public void RouterDoesNotResetStoreRetention()
    {
        LogEventStore store = new(new LogEventRetentionOptions().WithMaxEvents(2));
        EventSinkRouter router = new(
            new EventSinkRouteOptions
            {
                Routes =
                {
                    new EventSinkRoute
                    {
                        CategoryPattern = "*",
                        Destinations =
                        {
                            new EventSinkDestination { SinkCategory = "System", SinkName = "All" },
                        },
                    },
                },
            },
            store
        );

        router.Route(new EventSinkLogEvent("Widgets", LogLevel.Information, "First"));
        router.Route(new EventSinkLogEvent("Widgets", LogLevel.Information, "Second"));
        router.Route(new EventSinkLogEvent("Widgets", LogLevel.Information, "Third"));

        using LogEventStore.LogEventStoreSubscription subscription = store.CreateSubscription();
        Assert.Equal(new[] { "Second", "Third" }, subscription.Initialization.ReplayEvents.Select(streamEvent => streamEvent.Message));
    }
}
