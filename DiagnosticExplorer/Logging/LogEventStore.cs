using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;

namespace DiagnosticExplorer.Logging;

public sealed class LogEventStore
{
    private const int DefaultLiveSubscriptionCapacity = 1024;
    private readonly object _sync = new();
    private readonly List<LogStreamEvent> _events = new();
    private readonly HashSet<LogEventStoreSubscription> _subscriptions = new();
    private LogEventRetentionOptions _retention;
    private LogStreamRoutingConfiguration _routing;
    private long _sequence;

    public LogEventStore(LogEventRetentionOptions retention = null, string streamId = null)
    {
        _retention = (retention ?? new LogEventRetentionOptions()).CloneAndValidate();
        StreamId = string.IsNullOrWhiteSpace(streamId) ? Guid.NewGuid().ToString("N") : streamId;
        _routing = new LogStreamRoutingConfiguration();
    }

    public string StreamId { get; }

    public void Configure(LogEventRetentionOptions retention, LogStreamRoutingConfiguration routing)
    {
        if (retention == null)
            throw new ArgumentNullException(nameof(retention));
        if (routing == null)
            throw new ArgumentNullException(nameof(routing));

        lock (_sync)
        {
            _retention = retention.CloneAndValidate();
            _routing = routing.Clone();
            Prune(DateTime.UtcNow);
        }
    }

    public void ConfigureRouting(LogStreamRoutingConfiguration routing)
    {
        if (routing == null)
            throw new ArgumentNullException(nameof(routing));

        lock (_sync)
            _routing = routing.Clone();
    }

    public long Publish(EventSinkLogEvent logEvent)
    {
        if (logEvent == null)
            throw new ArgumentNullException(nameof(logEvent));

        lock (_sync)
        {
            DateTime timestampUtc = DateTime.UtcNow;
            Prune(timestampUtc);

            LogStreamEvent streamEvent = new()
            {
                StreamId = StreamId,
                Sequence = ++_sequence,
                TimestampUtc = timestampUtc,
                LoggerCategory = logEvent.Category,
                Level = (int)logEvent.Level,
                Message = logEvent.Message,
                Detail = logEvent.Detail,
                EventId = logEvent.EventId.Id,
                EventName = logEvent.EventId.Name,
            };

            _events.Add(streamEvent);
            Prune(timestampUtc);

            foreach (LogEventStoreSubscription subscription in _subscriptions.ToArray())
            {
                if (subscription.TryWrite(streamEvent))
                    continue;

                _subscriptions.Remove(subscription);
                subscription.Complete();
            }

            return streamEvent.Sequence;
        }
    }

    public LogEventStoreSubscription CreateSubscription(int liveSubscriptionCapacity = DefaultLiveSubscriptionCapacity)
    {
        if (liveSubscriptionCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(liveSubscriptionCapacity));

        lock (_sync)
        {
            Prune(DateTime.UtcNow);

            LogEventStoreSubscription subscription = new(this, liveSubscriptionCapacity);
            _subscriptions.Add(subscription);
            subscription.SetInitialization(CreateInitializationLocked());
            return subscription;
        }
    }

    public LogStreamInitialization CreateInitialization()
    {
        lock (_sync)
        {
            Prune(DateTime.UtcNow);
            return CreateInitializationLocked();
        }
    }

    private void RemoveSubscription(LogEventStoreSubscription subscription)
    {
        lock (_sync)
            _subscriptions.Remove(subscription);
    }

    private LogStreamInitialization CreateInitializationLocked()
    {
        return new LogStreamInitialization
        {
            StreamId = StreamId,
            Routing = _routing.Clone(),
            ReplayEvents = _events.ToArray(),
            HighWatermark = _sequence,
            MaxEvents = _retention.MaxEvents,
            MaxAgeMinutes = _retention.MaxAgeMinutes,
        };
    }

    private void Prune(DateTime timestampUtc)
    {
        DateTime minimumTimestamp = timestampUtc - TimeSpan.FromMinutes(_retention.MaxAgeMinutes);
        int firstCurrentIndex = _events.FindIndex(streamEvent => streamEvent.TimestampUtc >= minimumTimestamp);
        if (firstCurrentIndex < 0)
            _events.Clear();
        else if (firstCurrentIndex > 0)
            _events.RemoveRange(0, firstCurrentIndex);

        int excess = _events.Count - _retention.MaxEvents;
        if (excess > 0)
            _events.RemoveRange(0, excess);
    }

    public sealed class LogEventStoreSubscription : IDisposable
    {
        private readonly LogEventStore _owner;
        private readonly Channel<LogStreamEvent> _channel;
        private bool _disposed;

        internal LogEventStoreSubscription(LogEventStore owner, int capacity)
        {
            _owner = owner;
            _channel = Channel.CreateBounded<LogStreamEvent>(
                new BoundedChannelOptions(capacity)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = true,
                    SingleWriter = true,
                }
            );
        }

        public LogStreamInitialization Initialization { get; private set; }

        public ChannelReader<LogStreamEvent> Events => _channel.Reader;

        internal bool TryWrite(LogStreamEvent streamEvent)
        {
            return _channel.Writer.TryWrite(streamEvent);
        }

        internal void SetInitialization(LogStreamInitialization initialization)
        {
            Initialization = initialization;
        }

        internal void Complete()
        {
            _channel.Writer.TryComplete();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _owner.RemoveSubscription(this);
            Complete();
        }
    }
}

public static class LogStreamRoutingConfigurationExtensions
{
    public static LogStreamRoutingConfiguration CreateSnapshot(this EventSinkRouteOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        return new LogStreamRoutingConfiguration
        {
            MatchMode = options.MatchMode,
            Routes = (options.Routes ?? new List<EventSinkRoute>())
                .Select(
                    (route, index) =>
                        new LogStreamRoute
                        {
                            Order = index,
                            LoggerName = route.CategoryPattern,
                            LoggerNameMatchMode = route.CategoryPattern == "*" ? LoggerNameMatchMode.Wildcard : LoggerNameMatchMode.Prefix,
                            MinLevel = route.MinLevel.HasValue ? (int)route.MinLevel.Value : null,
                            MaxLevel = route.MaxLevel.HasValue ? (int)route.MaxLevel.Value : null,
                            StopProcessing = route.StopProcessing,
                            Destinations = route
                                .Destinations.Select(destination => new LogStreamRouteDestination
                                {
                                    Category = destination.SinkCategory.ToSnapshot(),
                                    Name = destination.SinkName.ToSnapshot(),
                                })
                                .ToList(),
                        }
                )
                .ToList(),
        };
    }

    public static LogStreamRoutingConfiguration Clone(this LogStreamRoutingConfiguration routing)
    {
        return new LogStreamRoutingConfiguration
        {
            MatchMode = routing.MatchMode,
            Routes = (routing.Routes ?? new List<LogStreamRoute>())
                .Select(route => new LogStreamRoute
                {
                    Order = route.Order,
                    LoggerName = route.LoggerName,
                    LoggerNameMatchMode = route.LoggerNameMatchMode,
                    MinLevel = route.MinLevel,
                    MaxLevel = route.MaxLevel,
                    StopProcessing = route.StopProcessing,
                    Destinations = route
                        .Destinations.Select(destination => new LogStreamRouteDestination
                        {
                            Category = new LogStreamRouteValue { Source = destination.Category.Source, Value = destination.Category.Value },
                            Name = new LogStreamRouteValue { Source = destination.Name.Source, Value = destination.Name.Value },
                        })
                        .ToList(),
                })
                .ToList(),
        };
    }

    private static LogStreamRouteValue ToSnapshot(this RouteValue routeValue)
    {
        return new LogStreamRouteValue { Source = routeValue.Source, Value = routeValue.Value };
    }
}
