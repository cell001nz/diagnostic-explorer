using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DiagnosticExplorer;

/// <summary>Bridges the in-process diagnostic manager to connected browser clients.</summary>
public sealed class SelfHostManager : IDisposable
{
    public const string LocalProcessId = "self";

    private readonly ConcurrentDictionary<string, ISelfHostClient> _clients = new();
    private readonly CancellationTokenSource _stopToken = new();
    private readonly EventSinkStream _eventStream;
    private readonly Task _eventTask;
    private readonly object _diagnosticsLock = new();
    private CancellationTokenSource _diagnosticsStopToken;
    private Task _diagnosticsTask;
    private int _disposed;
    private readonly IServiceProvider _serviceProvider;

    public SelfHostManager(IServiceProvider serviceProvider = null)
    {
        _serviceProvider = serviceProvider;
        _eventStream = EventSinkRepo.Default.CreateSinkStream(TimeSpan.FromMilliseconds(50), 100);
        _eventTask = Task.Run(StreamEventsAsync);
    }

    public string ProcessId => LocalProcessId;

    public string ProcessName => Process.GetCurrentProcess().ProcessName;

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
        _clients[connectionId] = client;
        StartDiagnosticsLoop();
    }

    public void RemoveClient(string connectionId)
    {
        if (connectionId != null && _clients.TryRemove(connectionId, out _))
            StopDiagnosticsLoopIfIdle();
    }

    public async Task SubscribeAsync(string connectionId, string processId)
    {
        ISelfHostClient client = GetClient(connectionId);
        if (!IsLocalProcess(processId))
        {
            await client.ShowDiagnosticsError(processId, "This self-host viewer exposes only its owning process.");
            return;
        }

        try
        {
            await client.ShowDiagnostics(LocalProcessId, DiagnosticManager.GetDiagnostics(_serviceProvider));
            await client.SetEvents(LocalProcessId, EventSinkRepo.Default.GetEvents());
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
            while (await _eventStream.EventChannel.Reader.WaitToReadAsync(_stopToken.Token))
            {
                IList<SystemEvent> events = await _eventStream.EventChannel.Reader.ReadAsync(_stopToken.Token);
                await BroadcastEventsAsync(events.ToArray());
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

                await Task.Delay(TimeSpan.FromSeconds(2), stopToken);
            }
        }
        catch (OperationCanceledException) { }
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

    private async Task TrySendDiagnostics(ISelfHostClient client, DiagnosticResponse diagnostics)
    {
        try
        {
            await client.ShowDiagnostics(LocalProcessId, diagnostics);
        }
        catch { }
    }

    private async Task TrySendDiagnosticsError(ISelfHostClient client, string message)
    {
        try
        {
            await client.ShowDiagnosticsError(LocalProcessId, message);
        }
        catch { }
    }

    private async Task BroadcastEventsAsync(SystemEvent[] events)
    {
        foreach (KeyValuePair<string, ISelfHostClient> pair in _clients.ToArray())
        {
            try
            {
                await pair.Value.StreamEvents(LocalProcessId, events);
            }
            catch
            {
                if (_clients.TryRemove(pair.Key, out _))
                    StopDiagnosticsLoopIfIdle();
            }
        }
    }

    private ISelfHostClient GetClient(string connectionId)
    {
        if (connectionId == null || !_clients.TryGetValue(connectionId, out ISelfHostClient client))
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
        }

        diagnosticsStopToken?.Cancel();
        if (diagnosticsStopToken != null)
            _ = DisposeDiagnosticsStopTokenAsync(diagnosticsTask, diagnosticsStopToken);

        _stopToken.Cancel();
        _eventStream.Dispose();
        _clients.Clear();
        _stopToken.Dispose();
    }
}
