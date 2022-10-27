using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DiagnosticExplorer;
using DiagnosticExplorer.Common;
using Diagnostics.Service.Common.Hubs;

namespace DiagWebService.ClientHandlers;

public class WebClientHandler
{
    private IWebHubClient _client;
    private IDisposable? _processSubscription;
    private IDisposable? _processRemoveSubscription;
    private Task? _eventStreamTask;
    private CancellationTokenSource? _eventStreamCancel;

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

    public async Task SetEvents(string id, SystemEvent[] events)
    {
        await _client.SetEvents(id, events);
    }

    public async Task ShowDiagnosticsError(string id, string message)
    {
        await _client.ShowDiagnosticsError(id, message);
    }

    public void StartStreamingEvents(string id, EventSinkRepo sinkRepo)
    {
        //Debug.WriteLine($"########## WebClientHandler.StartStreamingEvents connection {ConnectionId}");
        _eventStreamCancel = new CancellationTokenSource();
        _eventStreamTask = StreamEvents(id, sinkRepo, _eventStreamCancel.Token);
    }

    public void StopStreamingEvents()
    {
        //Debug.WriteLine($"########## WebClientHandler.StopStreamingEvents {ConnectionId}");
        _eventStreamCancel?.Cancel();
        _eventStreamTask = null;
    }

    private async Task StreamEvents(string id, EventSinkRepo sinkRepo, CancellationToken cancel)
    {
        using EventSinkStream? stream = sinkRepo.CreateSinkStream(TimeSpan.FromMilliseconds(25), 100);
        try
        {
            //Debug.WriteLine($"########## WebClientHandler calling _client.SetEvents({id}, {stream.InitialEvents.Length} events)");
            await _client.SetEvents(id, stream.InitialEvents);

            while (!cancel.IsCancellationRequested)
            {
                IList<SystemEvent>? evts = await stream.EventChannel.Reader.ReadAsync(cancel);
                if (evts != null)
                {
                    await _client.StreamEvents(id, evts);
                    //Debug.WriteLine($"########## WebClientHandler calling _client.StreamEvent({id}, 1 event)");
                }
            }
        }
        catch (OperationCanceledException)
        {
            //Debug.WriteLine("########## Stream event task cancelled");
        }
        catch (Exception ex)
        {
            //Debug.WriteLine($"########## Stream event exception {ex}");
        }
    }

}