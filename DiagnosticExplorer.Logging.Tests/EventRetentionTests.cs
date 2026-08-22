using System;
using System.Linq;
using Xunit;

namespace DiagnosticExplorer.Logging.Tests;

public class EventRetentionTests
{
    [Fact]
    public void KeepsOnlyTheConfiguredNumberOfEventsPerSink()
    {
        EventSinkRepo repo = new(new EventRetentionOptions { MaxEventsPerSink = 2 });
        EventSink sink = repo.GetSink("Widgets", "Application");

        sink.Info("First");
        sink.Info("Second");
        sink.Info("Third");

        Assert.Equal(new[] { "Second", "Third" }, sink.Events.Select(evt => evt.Message));
    }

    [Fact]
    public void ApplyingRetentionImmediatelyPurgesExistingEvents()
    {
        EventSinkRepo repo = new();
        EventSink sink = repo.GetSink("Widgets", "Application");
        sink.Info("First");
        sink.Info("Second");

        repo.ConfigureEventRetention(new EventRetentionOptions { MaxEventsPerSink = 1 });

        Assert.Equal("Second", Assert.Single(sink.Events).Message);
    }

    [Fact]
    public void PurgesEventsOlderThanTheConfiguredAge()
    {
        EventSinkRepo repo = new();
        repo.LogEvent(
            new SystemEvent
            {
                Date = DateTime.UtcNow.AddMinutes(-2),
                Message = "Expired",
                SinkName = "Widgets",
                SinkCategory = "Application",
            }
        );

        repo.ConfigureEventRetention(new EventRetentionOptions { MaxAgeMinutes = 1 });

        Assert.Empty(repo.GetEvents());
    }
}
