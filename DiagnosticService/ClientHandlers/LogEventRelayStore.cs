using System;
using System.Collections.Generic;
using System.Linq;
using DiagnosticExplorer.Logging;

namespace DiagWebService.ClientHandlers;

internal sealed class LogEventRelayStore
{
    private readonly object _sync = new();
    private readonly Dictionary<long, LogStreamEvent> _events = new();
    private readonly LogEventRetentionOptions _retention = new();
    private string _streamId;
    private LogStreamRoutingConfiguration _routing = new();
    private long _highWatermark;

    public bool MergeInitialization(LogStreamInitialization initialization)
    {
        if (initialization == null)
            throw new ArgumentNullException(nameof(initialization));
        if (string.IsNullOrWhiteSpace(initialization.StreamId))
            throw new ArgumentException("A log stream initialization requires a stream ID.", nameof(initialization));

        lock (_sync)
        {
            bool streamReplaced = !string.Equals(_streamId, initialization.StreamId, StringComparison.Ordinal);
            if (streamReplaced)
            {
                _events.Clear();
                _highWatermark = 0;
                _streamId = initialization.StreamId;
            }

            _routing = (initialization.Routing ?? new LogStreamRoutingConfiguration()).Clone();
            Merge(initialization.ReplayEvents);
            _highWatermark = Math.Max(_highWatermark, initialization.HighWatermark);
            Prune(DateTime.UtcNow);
            return streamReplaced;
        }
    }

    public LogStreamEvent[] Append(IEnumerable<LogStreamEvent> events)
    {
        lock (_sync)
        {
            if (string.IsNullOrWhiteSpace(_streamId))
                return Array.Empty<LogStreamEvent>();

            LogStreamEvent[] added = Merge(events);
            Prune(DateTime.UtcNow);
            return added;
        }
    }

    public LogStreamInitialization CreateInitialization()
    {
        lock (_sync)
        {
            Prune(DateTime.UtcNow);
            return new LogStreamInitialization
            {
                StreamId = _streamId ?? string.Empty,
                Routing = _routing.Clone(),
                ReplayEvents = _events.Values.OrderBy(streamEvent => streamEvent.Sequence).ToArray(),
                HighWatermark = _highWatermark,
            };
        }
    }

    private LogStreamEvent[] Merge(IEnumerable<LogStreamEvent> events)
    {
        if (events == null)
            return Array.Empty<LogStreamEvent>();

        List<LogStreamEvent> added = new();
        foreach (LogStreamEvent streamEvent in events)
        {
            if (streamEvent == null || !string.Equals(streamEvent.StreamId, _streamId, StringComparison.Ordinal))
                continue;
            if (!_events.TryAdd(streamEvent.Sequence, streamEvent))
                continue;

            _highWatermark = Math.Max(_highWatermark, streamEvent.Sequence);
            added.Add(streamEvent);
        }

        return added.OrderBy(streamEvent => streamEvent.Sequence).ToArray();
    }

    private void Prune(DateTime nowUtc)
    {
        DateTime minimumTimestamp = nowUtc - TimeSpan.FromMinutes(_retention.MaxAgeMinutes);
        foreach (long sequence in _events.Where(pair => pair.Value.TimestampUtc < minimumTimestamp).Select(pair => pair.Key).ToArray())
            _events.Remove(sequence);

        int excess = _events.Count - _retention.MaxEvents;
        if (excess <= 0)
            return;

        foreach (long sequence in _events.Keys.OrderBy(sequence => sequence).Take(excess).ToArray())
            _events.Remove(sequence);
    }
}
