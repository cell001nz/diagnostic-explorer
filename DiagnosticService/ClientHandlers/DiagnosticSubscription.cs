using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using DiagnosticExplorer;
using DiagnosticExplorer.Common;
using DiagnosticExplorer.Logging;

namespace DiagWebService.ClientHandlers;

public class DiagnosticSubscription
{
    private static int _instanceCounter = 0;
    private int _instance;
    public DiagProcess Process { get; set; }
    public IDiagnosticClient? DiagnosticClient { get; private set; }
    private readonly ConcurrentDictionary<string, WebClientHandler> _webClients = new();
    private Task? _requestLoop;
    private CancellationTokenSource? _requestLoopCancelSource;
    private DiagnosticResponse? _lastResponse;
    public string ProcessId => Process.Id;
    private IDiagnosticClient? _eventSubscriptionOwnerClient;
    private IDisposable? _logStreamInitializationSubscription;
    private IDisposable? _logStreamEventSubscription;
    private readonly LogEventRelayStore _eventStore = new();
    private readonly object _startStopLock = new();
    private bool _streamingStarted = false;
    private bool _eventSubscriptionStopInProgress = false;
    private IDiagnosticClient? _eventSubscriptionStopClient;
    private bool _eventSubscriptionRestartBlocked = false;

    public DiagnosticSubscription(DiagProcess process)
    {
        _instance = Interlocked.Increment(ref _instanceCounter);
        Process = process;
    }

    public void SetDiagnosticClient(IDiagnosticClient? diagClient)
    {
        if (DiagnosticClient != diagClient)
        {
            IDiagnosticClient? previousClient = DiagnosticClient;
            lock (_startStopLock)
            {
                DiagnosticClient = diagClient;
                _eventSubscriptionRestartBlocked = true;
            }

            StopRequestLoop();
            StopDiagClientEvents(previousClient);
            StopWebClientEvents();
            lock (_startStopLock)
            {
                _eventSubscriptionRestartBlocked = false;
            }
            string isNull = diagClient == null ? "NULL" : "NOT NULL";
            //Debug.WriteLine($"@@@@@@@@@@ DiagnosticSubscription {Process.Id} client set to {isNull}");
            StartIfRequired();
        }
    }

    private void StopWebClientEvents()
    {
        WebClientHandler[] handlers;
        lock (_startStopLock)
        {
            _streamingStarted = false;
            handlers = _webClients.Values.ToArray();
        }

        foreach (WebClientHandler handler in handlers)
        {
            //Debug.WriteLine($"@@@@@@@@@@ StopWebClientEventStreaming handler {handler.ConnectionId} stop streaming events");
            handler.StopStreamingEvents();
        }
    }

    public async Task AddWebClient(WebClientHandler webClient)
    {
        //Debug.WriteLine($"@@@@@@@@@@ DiagnosticSubscription.AddWebClient {webClient.ConnectionId} now there are {_webClients.Count} _streamingStarted: {_streamingStarted}");

        if (_lastResponse != null)
            await TrySend(webClient, _lastResponse);

        lock (_startStopLock)
        {
            bool added = _webClients.TryAdd(webClient.ConnectionId, webClient);

            if (added)
                webClient.InitializeLogStream(ProcessId, _eventStore.CreateInitialization());

            StartIfRequired();
        }
    }

    public void RemoveWebClient(WebClientHandler webClient)
    {
        if (_webClients.ContainsKey(webClient.ConnectionId))
        {
            //Debug.WriteLine($"@@@@@@@@@@ DiagnosticSubscription.RemoveWebClient {webClient.ConnectionId} currently {_webClients.Count} clients before removal");

            webClient.StopStreamingEvents();

            lock (_startStopLock)
            {
                _webClients.TryRemove(webClient.ConnectionId, out _);
            }

            StopIfRequired();
        }
    }

    private void StartIfRequired()
    {
        lock (_startStopLock)
        {
            if (
                _webClients.Any()
                && DiagnosticClient != null
                && _logStreamEventSubscription == null
                && !_eventSubscriptionStopInProgress
                && !_eventSubscriptionRestartBlocked
            )
                StartDiagClientEvents();

            if (_webClients.Any() && DiagnosticClient != null && _requestLoop == null)
                StartRequestLoop();
        }
    }

    private void StartRequestLoop()
    {
        _requestLoopCancelSource = new CancellationTokenSource();
        _requestLoop = RunLoop(DiagnosticClient!, _requestLoopCancelSource.Token);
    }

    private void StopRequestLoop()
    {
        _requestLoopCancelSource?.Cancel();
        _requestLoop = null;
    }

    private void StartDiagClientEvents()
    {
        IDiagnosticClient diagnosticClient = DiagnosticClient!;
        IDisposable? initializationSubscription = null;
        IDisposable? eventSubscription = null;
        initializationSubscription = diagnosticClient.LogStreamInitialized.Subscribe(initialization =>
            HandleInitialEventsArrived(diagnosticClient, initializationSubscription!, eventSubscription!, initialization)
        );
        eventSubscription = diagnosticClient.LogStreamEvents.Subscribe(events =>
            HandleStreamedEventsArrived(diagnosticClient, initializationSubscription!, eventSubscription!, events)
        );
        _eventSubscriptionOwnerClient = diagnosticClient;
        _logStreamInitializationSubscription = initializationSubscription;
        _logStreamEventSubscription = eventSubscription;
        RunDetached(
            () => diagnosticClient.SubscribeEvents(),
            ex => HandleSubscribeEventsFailure(diagnosticClient, initializationSubscription, eventSubscription, ex)
        );
    }

    private void StopDiagClientEvents(IDiagnosticClient? diagnosticClientToUnsubscribe = null)
    {
        IDisposable? initializationSubscription;
        IDisposable? eventSubscription;
        IDiagnosticClient? diagnosticClient;
        lock (_startStopLock)
        {
            if (_logStreamInitializationSubscription == null && _logStreamEventSubscription == null)
                return;

            initializationSubscription = _logStreamInitializationSubscription;
            eventSubscription = _logStreamEventSubscription;
            diagnosticClient = diagnosticClientToUnsubscribe ?? _eventSubscriptionOwnerClient;
            _streamingStarted = false;
            _eventSubscriptionOwnerClient = null;
            _logStreamInitializationSubscription = null;
            _logStreamEventSubscription = null;
            _eventSubscriptionStopInProgress = diagnosticClient != null;
            _eventSubscriptionStopClient = diagnosticClient;
        }

        initializationSubscription?.Dispose();
        eventSubscription?.Dispose();

        if (diagnosticClient != null)
            RunDetached(
                () => diagnosticClient.UnsubscribeEvents(),
                ex => HandleUnsubscribeEventsCompletion(diagnosticClient, ex),
                () => HandleUnsubscribeEventsCompletion(diagnosticClient, null)
            );
    }

    private void RunDetached(Func<Task> action, Action<Exception>? onError = null, Action? onSuccess = null)
    {
        try
        {
            Task task = action();
            if (task.IsCompletedSuccessfully)
            {
                onSuccess?.Invoke();
                return;
            }

            _ = ObserveDetachedTask(task, onError, onSuccess);
        }
        catch (Exception ex)
        {
            onError?.Invoke(ex);
        }
    }

    private async Task ObserveDetachedTask(Task task, Action<Exception>? onError, Action? onSuccess)
    {
        try
        {
            await task;
            onSuccess?.Invoke();
        }
        catch (Exception ex)
        {
            onError?.Invoke(ex);
        }
    }

    private void HandleSubscribeEventsFailure(
        IDiagnosticClient diagnosticClient,
        IDisposable eventSetSubscription,
        IDisposable eventStreamSubscription,
        Exception ex
    )
    {
        lock (_startStopLock)
        {
            if (!MatchesCurrentEventSubscriptions(diagnosticClient, eventSetSubscription, eventStreamSubscription))
                return;

            eventSetSubscription.Dispose();
            eventStreamSubscription.Dispose();
            _eventSubscriptionOwnerClient = null;
            _logStreamInitializationSubscription = null;
            _logStreamEventSubscription = null;
            _streamingStarted = false;
        }

        Trace.WriteLine($"DiagnosticSubscription {Process.Id} failed to subscribe events: {ex.Message}");
    }

    private void HandleUnsubscribeEventsCompletion(IDiagnosticClient diagnosticClient, Exception? ex)
    {
        lock (_startStopLock)
        {
            if (!_eventSubscriptionStopInProgress || !ReferenceEquals(_eventSubscriptionStopClient, diagnosticClient))
                return;

            _eventSubscriptionStopInProgress = false;
            _eventSubscriptionStopClient = null;

            if (!_eventSubscriptionRestartBlocked && _webClients.Any() && DiagnosticClient != null && _logStreamEventSubscription == null)
                StartDiagClientEvents();
        }

        if (ex != null)
            Trace.WriteLine($"DiagnosticSubscription {Process.Id} failed to unsubscribe events: {ex.Message}");
    }

    private bool MatchesCurrentEventSubscriptions(
        IDiagnosticClient diagnosticClient,
        IDisposable eventSetSubscription,
        IDisposable eventStreamSubscription
    )
    {
        return ReferenceEquals(DiagnosticClient, diagnosticClient)
            && ReferenceEquals(_logStreamInitializationSubscription, eventSetSubscription)
            && ReferenceEquals(_logStreamEventSubscription, eventStreamSubscription);
    }

    private void HandleInitialEventsArrived(
        IDiagnosticClient diagnosticClient,
        IDisposable eventSetSubscription,
        IDisposable eventStreamSubscription,
        LogStreamInitialization initialization
    )
    {
        lock (_startStopLock)
        {
            if (!MatchesCurrentEventSubscriptions(diagnosticClient, eventSetSubscription, eventStreamSubscription))
                return;

            if (_streamingStarted)
                return;

            _eventStore.MergeInitialization(initialization);
            _streamingStarted = true;

            LogStreamInitialization currentInitialization = _eventStore.CreateInitialization();
            foreach (WebClientHandler handler in _webClients.Values)
                handler.InitializeLogStream(Process.Id, currentInitialization);
        }
    }

    private void HandleStreamedEventsArrived(
        IDiagnosticClient diagnosticClient,
        IDisposable eventSetSubscription,
        IDisposable eventStreamSubscription,
        LogStreamEvent[] events
    )
    {
        lock (_startStopLock)
        {
            if (!MatchesCurrentEventSubscriptions(diagnosticClient, eventSetSubscription, eventStreamSubscription))
                return;

            LogStreamEvent[] acceptedEvents = _eventStore.Append(events);
            if (acceptedEvents.Length == 0)
                return;

            LogStreamInitialization resynchronization = _eventStore.CreateInitialization();
            foreach (WebClientHandler handler in _webClients.Values)
                handler.QueueLogEvents(Process.Id, acceptedEvents, resynchronization);
        }
    }

    private void StopIfRequired()
    {
        lock (_startStopLock)
        {
            if (_webClients.Count == 0 && _requestLoop != null)
                StopRequestLoop();

            if (_webClients.Count == 0 && _logStreamEventSubscription != null)
                StopDiagClientEvents();
        }
    }

    private async Task RunLoop(IDiagnosticClient client, CancellationToken cancelToken)
    {
        //Debug.WriteLine($"@@@@@@@@@@ RunLoop {Process.Id} enter");
        try
        {
            while (!cancelToken.IsCancellationRequested)
            {
                try
                {
                    if (client != null)
                    {
                        DiagnosticResponse diags = await client.GetDiagnostics(cancelToken);
                        _lastResponse = diags;
                        //Debug.WriteLine($"@@@@@@@@@@ RunLoop got diags {Process.Id} {diags}");
                        await Task.WhenAll(_webClients.Values.Select(client => TrySend(client, diags)));
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    await Task.WhenAll(_webClients.Values.Select(client => TrySendError(client, ex.Message)));
                    //Debug.WriteLine($"@@@@@@@@@@ RunLoop {Process.Id} exception {ex.Message}");
                }

                await Task.Delay(2000, cancelToken);
            }
        }
        catch (TaskCanceledException) { }
        catch (OperationCanceledException) { }

        //Debug.WriteLine($"@@@@@@@@@@ RunLoop {Process.Id} exit");
    }

    private async Task TrySend(WebClientHandler client, DiagnosticResponse diags)
    {
        try
        {
            await client.ShowDiagnostics(Process.Id, diags);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"@@@@@@@@@@ RunLoop {Process.Id} TrySend fail {ex.Message}");
        }
    }

    private async Task TrySendError(WebClientHandler client, string message)
    {
        try
        {
            await client.ShowDiagnosticsError(Process.Id, message);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"@@@@@@@@@@ RunLoop {Process.Id} TrySendError fail {ex.Message}");
        }
    }
}
