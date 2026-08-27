using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using DiagnosticExplorer.Logging;

namespace DiagnosticExplorer;

/// <summary>Bridges the in-process diagnostic manager to connected browser clients.</summary>
public sealed class SelfHostManager : IDisposable
{
    public const string LocalProcessId = "self";

    private const int OutboundQueueCapacity = 256;
    private const int MinimumDiagnosticsRefreshIntervalSeconds = 1;
    private const int MaximumDiagnosticsRefreshIntervalSeconds = 10;
    private const int DefaultDiagnosticsRefreshIntervalSeconds = 2;
    private readonly ConcurrentDictionary<string, SelfHostClientHandler> _clients = new();
    private readonly CancellationTokenSource _stopToken = new();
    private readonly LogEventStore.LogEventStoreSubscription _eventStream;
    private readonly Task _eventTask;
    private readonly object _eventLock = new();
    private readonly object _diagnosticsLock = new();
    private CancellationTokenSource _diagnosticsStopToken;
    private CancellationTokenSource _diagnosticsRefreshDelayToken;
    private Task _diagnosticsTask;
    private int _diagnosticsRefreshIntervalSeconds = DefaultDiagnosticsRefreshIntervalSeconds;
    private int _disposed;
    private readonly IServiceProvider _serviceProvider;

    public SelfHostManager(IServiceProvider serviceProvider = null)
    {
        _serviceProvider = serviceProvider;
        _eventStream = DiagnosticManager.LogEventStore.CreateSubscription();
        _eventTask = Task.Run(StreamEventsAsync);
    }

    public string ProcessId => LocalProcessId;

    public string ProcessName => Process.GetCurrentProcess().ProcessName;

    public int GetDiagnosticsRefreshInterval() => Volatile.Read(ref _diagnosticsRefreshIntervalSeconds);

    public int SetDiagnosticsRefreshInterval(int seconds)
    {
        int interval = Math.Max(MinimumDiagnosticsRefreshIntervalSeconds, Math.Min(MaximumDiagnosticsRefreshIntervalSeconds, seconds));
        Volatile.Write(ref _diagnosticsRefreshIntervalSeconds, interval);
        lock (_diagnosticsLock)
        {
            if (_disposed == 0)
                _diagnosticsRefreshDelayToken?.Cancel();
        }
        return interval;
    }

    public SelfHostProcessInfo GetProcessInfo() =>
        new()
        {
            Id = LocalProcessId,
            Name = ProcessName,
            MachineName = Environment.MachineName,
            UserName = Environment.UserName,
        };

    public void AddClient(string connectionId, ISelfHostClient client)
    {
        if (connectionId == null)
            throw new ArgumentNullException(nameof(connectionId));
        if (client == null)
            throw new ArgumentNullException(nameof(client));
        _clients.AddOrUpdate(
            connectionId,
            _ => new SelfHostClientHandler(client),
            (_, existing) =>
            {
                existing.Dispose();
                return new SelfHostClientHandler(client);
            }
        );
        StartDiagnosticsLoop();
    }

    public void RemoveClient(string connectionId)
    {
        if (connectionId != null && _clients.TryRemove(connectionId, out SelfHostClientHandler client))
        {
            client.Dispose();
            StopDiagnosticsLoopIfIdle();
        }
    }

    public async Task SubscribeAsync(string connectionId, string processId)
    {
        SelfHostClientHandler client = GetClient(connectionId);
        if (!IsLocalProcess(processId))
        {
            await client.ShowDiagnosticsError(processId, "This self-host viewer exposes only its owning process.");
            return;
        }

        try
        {
            await client.ShowDiagnostics(LocalProcessId, DiagnosticManager.GetDiagnostics(_serviceProvider));
            lock (_eventLock)
                client.InitializeLogStream(LocalProcessId, DiagnosticManager.LogEventStore.CreateInitialization());
        }
        catch (Exception ex)
        {
            await client.ShowDiagnosticsError(LocalProcessId, ex.Message);
        }
    }

    public void Unsubscribe(string connectionId, string processId)
    {
        if (IsLocalProcess(processId))
            RemoveClient(connectionId);
    }

    public Task<DrillDownResponse> GetDrillDownAsync(string processId, DrillDownRequest request)
    {
        if (!IsLocalProcess(processId))
            return Task.FromResult(new DrillDownResponse { ErrorMessage = "The requested process is not hosted here." });

        return Task.FromResult(DiagnosticManager.GetDrillDown(_serviceProvider, request));
    }

    public Task<OperationResponse> SetPropertyAsync(string processId, SetPropertyRequest request)
    {
        if (!IsLocalProcess(processId))
            return Task.FromResult(OperationResponse.Error("The requested process is not hosted here."));
        if (request == null || string.IsNullOrWhiteSpace(request.Path))
            return Task.FromResult(OperationResponse.Error("A property path is required."));

        try
        {
            return Task.FromResult(DiagnosticManager.SetProperty(_serviceProvider, request));
        }
        catch (Exception ex)
        {
            return Task.FromResult(OperationResponse.Error(ex.Message, ex.ToString()));
        }
    }

    public async Task<OperationResponse> ExecuteOperationAsync(string processId, OperationRequest request)
    {
        if (!IsLocalProcess(processId))
            return OperationResponse.Error("The requested process is not hosted here.");
        if (request == null || string.IsNullOrWhiteSpace(request.Path) || string.IsNullOrWhiteSpace(request.Operation))
            return OperationResponse.Error("An operation path and signature are required.");

        try
        {
            return await DiagnosticManager.ExecuteOperation(_serviceProvider, request);
        }
        catch (Exception ex)
        {
            return OperationResponse.Error(ex.Message, ex.ToString());
        }
    }

    private async Task StreamEventsAsync()
    {
        try
        {
            while (await _eventStream.Events.WaitToReadAsync(_stopToken.Token))
            {
                List<LogStreamEvent> events = new();
                while (events.Count < 100 && _eventStream.Events.TryRead(out LogStreamEvent streamEvent))
                    events.Add(streamEvent);

                if (events.Count == 0)
                    continue;

                await Task.Delay(TimeSpan.FromMilliseconds(50), _stopToken.Token);
                while (events.Count < 100 && _eventStream.Events.TryRead(out LogStreamEvent streamEvent))
                    events.Add(streamEvent);

                BroadcastEvents(events.ToArray());
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task RequestDiagnosticsAsync(CancellationToken stopToken)
    {
        try
        {
            while (!stopToken.IsCancellationRequested)
            {
                try
                {
                    if (!_clients.IsEmpty)
                    {
                        DiagnosticResponse diagnostics = DiagnosticManager.GetDiagnostics(_serviceProvider);
                        await Task.WhenAll(_clients.Values.Select(client => TrySendDiagnostics(client, diagnostics)));
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    await Task.WhenAll(_clients.Values.Select(client => TrySendDiagnosticsError(client, ex.Message)));
                }

                await WaitForNextDiagnosticsAsync(stopToken);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task WaitForNextDiagnosticsAsync(CancellationToken stopToken)
    {
        using CancellationTokenSource refreshChangeToken = new();
        lock (_diagnosticsLock)
        {
            if (_disposed != 0 || stopToken.IsCancellationRequested)
                return;
            _diagnosticsRefreshDelayToken = refreshChangeToken;
        }

        try
        {
            using CancellationTokenSource delayToken = CancellationTokenSource.CreateLinkedTokenSource(stopToken, refreshChangeToken.Token);
            await Task.Delay(TimeSpan.FromSeconds(GetDiagnosticsRefreshInterval()), delayToken.Token);
        }
        catch (OperationCanceledException) when (!stopToken.IsCancellationRequested) { }
        finally
        {
            lock (_diagnosticsLock)
            {
                if (ReferenceEquals(_diagnosticsRefreshDelayToken, refreshChangeToken))
                    _diagnosticsRefreshDelayToken = null;
            }
        }
    }

    private void StartDiagnosticsLoop()
    {
        lock (_diagnosticsLock)
        {
            if (_disposed != 0 || _diagnosticsTask != null)
                return;

            _diagnosticsStopToken = new CancellationTokenSource();
            _diagnosticsTask = Task.Run(() => RequestDiagnosticsAsync(_diagnosticsStopToken.Token));
        }
    }

    private void StopDiagnosticsLoopIfIdle()
    {
        CancellationTokenSource stopToken;
        Task diagnosticsTask;

        lock (_diagnosticsLock)
        {
            if (!_clients.IsEmpty || _diagnosticsTask == null)
                return;

            stopToken = _diagnosticsStopToken;
            diagnosticsTask = _diagnosticsTask;
            _diagnosticsStopToken = null;
            _diagnosticsTask = null;
        }

        stopToken?.Cancel();
        if (stopToken != null)
            _ = DisposeDiagnosticsStopTokenAsync(diagnosticsTask, stopToken);
    }

    private static async Task DisposeDiagnosticsStopTokenAsync(Task diagnosticsTask, CancellationTokenSource stopToken)
    {
        try
        {
            await diagnosticsTask;
        }
        catch { }
        finally
        {
            stopToken.Dispose();
        }
    }

    private async Task TrySendDiagnostics(SelfHostClientHandler client, DiagnosticResponse diagnostics)
    {
        try
        {
            await client.ShowDiagnostics(LocalProcessId, diagnostics);
        }
        catch { }
    }

    private async Task TrySendDiagnosticsError(SelfHostClientHandler client, string message)
    {
        try
        {
            await client.ShowDiagnosticsError(LocalProcessId, message);
        }
        catch { }
    }

    private void BroadcastEvents(LogStreamEvent[] events)
    {
        lock (_eventLock)
        {
            LogStreamInitialization resynchronization = DiagnosticManager.LogEventStore.CreateInitialization();
            foreach (SelfHostClientHandler client in _clients.Values)
                client.QueueLogEvents(LocalProcessId, events, resynchronization);
        }
    }

    private SelfHostClientHandler GetClient(string connectionId)
    {
        if (connectionId == null || !_clients.TryGetValue(connectionId, out SelfHostClientHandler client))
            throw new InvalidOperationException("The viewer connection is no longer available.");

        return client;
    }

    private static bool IsLocalProcess(string processId) => string.Equals(processId, LocalProcessId, StringComparison.Ordinal);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        CancellationTokenSource diagnosticsStopToken;
        Task diagnosticsTask;
        lock (_diagnosticsLock)
        {
            diagnosticsStopToken = _diagnosticsStopToken;
            diagnosticsTask = _diagnosticsTask;
            _diagnosticsStopToken = null;
            _diagnosticsTask = null;
            _diagnosticsRefreshDelayToken?.Cancel();
        }

        diagnosticsStopToken?.Cancel();
        if (diagnosticsStopToken != null)
            _ = DisposeDiagnosticsStopTokenAsync(diagnosticsTask, diagnosticsStopToken);

        _stopToken.Cancel();
        _eventStream.Dispose();
        foreach (SelfHostClientHandler client in _clients.Values)
            client.Dispose();
        _clients.Clear();
        _stopToken.Dispose();
    }

    private sealed class SelfHostClientHandler : IDisposable
    {
        private readonly ISelfHostClient _client;
        private readonly Channel<LogStreamFrame> _channel = Channel.CreateBounded<LogStreamFrame>(
            new BoundedChannelOptions(OutboundQueueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true,
            }
        );
        private readonly CancellationTokenSource _stopToken = new();
        private readonly object _sync = new();
        private readonly Task _senderTask;
        private bool _subscribed;

        public SelfHostClientHandler(ISelfHostClient client)
        {
            _client = client;
            _senderTask = Task.Run(SendAsync);
        }

        public Task ShowDiagnostics(string processId, DiagnosticResponse diagnostics) => _client.ShowDiagnostics(processId, diagnostics);

        public Task ShowDiagnosticsError(string processId, string message) => _client.ShowDiagnosticsError(processId, message);

        public void InitializeLogStream(string processId, LogStreamInitialization initialization)
        {
            lock (_sync)
            {
                _subscribed = true;
                ReplacePendingFrames(LogStreamFrame.ForInitialization(processId, initialization));
            }
        }

        public void QueueLogEvents(string processId, LogStreamEvent[] events, LogStreamInitialization resynchronization)
        {
            lock (_sync)
            {
                if (!_subscribed)
                    return;
                if (_channel.Writer.TryWrite(LogStreamFrame.ForEvents(processId, events)))
                    return;

                ReplacePendingFrames(LogStreamFrame.ForInitialization(processId, resynchronization));
            }
        }

        private void ReplacePendingFrames(LogStreamFrame initialization)
        {
            while (_channel.Reader.TryRead(out _)) { }
            _channel.Writer.TryWrite(initialization);
        }

        private async Task SendAsync()
        {
            try
            {
                while (await _channel.Reader.WaitToReadAsync(_stopToken.Token))
                {
                    while (_channel.Reader.TryRead(out LogStreamFrame frame))
                    {
                        if (frame.Initialization != null)
                            await _client.InitializeLogStream(frame.ProcessId, frame.Initialization);
                        else
                            await _client.StreamLogEvents(frame.ProcessId, frame.Events!);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch { }
        }

        public void Dispose()
        {
            _stopToken.Cancel();
            _channel.Writer.TryComplete();
            _stopToken.Dispose();
        }
    }

    private sealed class LogStreamFrame
    {
        private LogStreamFrame(string processId, LogStreamInitialization? initialization, LogStreamEvent[]? events)
        {
            ProcessId = processId;
            Initialization = initialization;
            Events = events;
        }

        public string ProcessId { get; }
        public LogStreamInitialization? Initialization { get; }
        public LogStreamEvent[]? Events { get; }

        public static LogStreamFrame ForInitialization(string processId, LogStreamInitialization initialization)
        {
            return new LogStreamFrame(processId, initialization, null);
        }

        public static LogStreamFrame ForEvents(string processId, LogStreamEvent[] events)
        {
            return new LogStreamFrame(processId, null, events);
        }
    }
}
