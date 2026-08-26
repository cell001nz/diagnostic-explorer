using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using DiagnosticExplorer;
using DiagnosticExplorer.Common;
using DiagnosticExplorer.Logging;
using Diagnostics.Service.Common.Hubs;

namespace DiagWebService.ClientHandlers;

public class WebClientHandler
{
    private const int OutboundQueueCapacity = 256;
    private IWebHubClient _client;
    private IDisposable? _processSubscription;
    private IDisposable? _processRemoveSubscription;
    private Task? _eventStreamTask;
    private CancellationTokenSource? _eventStreamCancel;
    private readonly object _eventStreamLock = new();
    private Channel<LogStreamFrame>? _eventChannel;
    private string? _activeProcessId;
    private LogStreamFrame? _pendingRestartFrame;

    public WebClientHandler(string connectionId, IWebHubClient client)
    {
        ConnectionId = connectionId;
        _client = client;
    }

    public string ConnectionId { get; }

    public void Start(RealtimeManager realtimeManager)
    {
        _client.SetProcesses(realtimeManager.GetProcesses().ToArray());
        _processSubscription = realtimeManager.ProcessChanged.Subscribe(HandleProcessesChanged);
        _processRemoveSubscription = realtimeManager.ProcessRemoved.Subscribe(HandleProcessRemoved);
    }

    public void Stop()
    {
        _processSubscription?.Dispose();
        _processRemoveSubscription?.Dispose();
        StopStreamingEvents();
    }

    private void HandleProcessesChanged(DiagProcess changed)
    {
        _client.UpdateProcess(changed);
    }

    private void HandleProcessRemoved(DiagProcess changed)
    {
        _client.RemoveProcess(changed.Id);
    }

    public async Task ShowDiagnostics(string id, DiagnosticResponse response)
    {
        await _client.ShowDiagnostics(id, response);
    }

    public async Task ShowDiagnosticsError(string id, string message)
    {
        await _client.ShowDiagnosticsError(id, message);
    }

    public void InitializeLogStream(string id, LogStreamInitialization initialization)
    {
        if (initialization == null)
            throw new ArgumentNullException(nameof(initialization));

        LogStreamFrame frame = LogStreamFrame.ForInitialization(id, initialization);
        lock (_eventStreamLock)
        {
            if (_eventStreamTask is { IsCompleted: false })
            {
                if (string.Equals(_activeProcessId, id, StringComparison.Ordinal) && _eventStreamCancel?.IsCancellationRequested != true)
                {
                    ReplacePendingFramesLocked(frame);
                    return;
                }

                _pendingRestartFrame = frame;
                _eventStreamCancel?.Cancel();
                _eventChannel?.Writer.TryComplete();
                return;
            }

            StartStreamingEventsLocked(frame);
        }
    }

    public void QueueLogEvents(string id, LogStreamEvent[] events, LogStreamInitialization resynchronization)
    {
        if (events == null || events.Length == 0)
            return;

        lock (_eventStreamLock)
        {
            if (!string.Equals(_activeProcessId, id, StringComparison.Ordinal) || _eventStreamCancel?.IsCancellationRequested == true)
                return;
            if (_eventChannel?.Writer.TryWrite(LogStreamFrame.ForEvents(id, events)) == true)
                return;

            ReplacePendingFramesLocked(LogStreamFrame.ForInitialization(id, resynchronization));
        }
    }

    public void StopStreamingEvents()
    {
        //Debug.WriteLine($"########## WebClientHandler.StopStreamingEvents {ConnectionId}");
        lock (_eventStreamLock)
        {
            _pendingRestartFrame = null;
            _eventStreamCancel?.Cancel();
            _eventChannel?.Writer.TryComplete();
        }
    }

    private void StartStreamingEventsLocked(LogStreamFrame initialization)
    {
        Channel<LogStreamFrame> eventChannel = Channel.CreateBounded<LogStreamFrame>(
            new BoundedChannelOptions(OutboundQueueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true,
            }
        );
        CancellationTokenSource eventStreamCancel = new();
        eventChannel.Writer.TryWrite(initialization);
        Task eventStreamTask = StreamEvents(eventChannel, eventStreamCancel.Token);
        _activeProcessId = initialization.ProcessId;
        _eventChannel = eventChannel;
        _eventStreamCancel = eventStreamCancel;
        _eventStreamTask = eventStreamTask;
        _ = ObserveEventStream(eventStreamTask, eventStreamCancel, eventChannel);
    }

    private void ReplacePendingFramesLocked(LogStreamFrame initialization)
    {
        if (_eventChannel == null)
            return;

        while (_eventChannel.Reader.TryRead(out _)) { }
        if (!_eventChannel.Writer.TryWrite(initialization))
        {
            _pendingRestartFrame = initialization;
            _eventStreamCancel?.Cancel();
            _eventChannel.Writer.TryComplete();
        }
    }

    private async Task StreamEvents(Channel<LogStreamFrame> eventChannel, CancellationToken cancel)
    {
        try
        {
            while (await eventChannel.Reader.WaitToReadAsync(cancel))
            {
                while (eventChannel.Reader.TryRead(out LogStreamFrame frame))
                {
                    if (frame.Initialization != null)
                        await _client.InitializeLogStream(frame.ProcessId, frame.Initialization);
                    else
                        await _client.StreamLogEvents(frame.ProcessId, frame.Events!);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Trace.WriteLine($"WebClientHandler stream failed: {ex.Message}");
        }
    }

    private async Task ObserveEventStream(Task eventStreamTask, CancellationTokenSource eventStreamCancel, Channel<LogStreamFrame> eventChannel)
    {
        try
        {
            await eventStreamTask;
        }
        finally
        {
            lock (_eventStreamLock)
            {
                if (ReferenceEquals(_eventStreamTask, eventStreamTask))
                {
                    _eventStreamTask = null;
                    _eventStreamCancel = null;
                    _eventChannel = null;
                    _activeProcessId = null;
                    if (_pendingRestartFrame is LogStreamFrame pendingRestartFrame)
                    {
                        _pendingRestartFrame = null;
                        StartStreamingEventsLocked(pendingRestartFrame);
                    }
                }
            }
            eventStreamCancel.Dispose();
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
