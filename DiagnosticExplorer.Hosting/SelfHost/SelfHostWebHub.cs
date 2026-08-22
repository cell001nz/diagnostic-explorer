#if NET6_0_OR_GREATER
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace DiagnosticExplorer.SelfHost;

/// <summary>
/// Local equivalent of the service's WebHub. Implements only the subset of methods the
/// Angular app invokes in self-host (realtime-only) mode; property/operation calls go
/// straight to the in-process <see cref="DiagnosticManager"/>. Retro and multi-process
/// management are intentionally absent — the UI hides them in self-host mode.
/// </summary>
public class SelfHostWebHub : Hub<ISelfHostClient>
{
    private readonly SelfHostManager _manager;

    public SelfHostWebHub(SelfHostManager manager) => _manager = manager;

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        _manager.AddClient(Context.ConnectionId, Clients.Caller);
    }

    public override Task OnDisconnectedAsync(Exception exception)
    {
        _manager.RemoveClient(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    public Task Subscribe(string processId)
    {
        _manager.Subscribe(Context.ConnectionId, processId);
        return Task.CompletedTask;
    }

    public Task Unsubscribe(string processId)
    {
        _manager.Unsubscribe(Context.ConnectionId, processId);
        return Task.CompletedTask;
    }

    public Task<OperationResponse> SetProperty(string processId, SetPropertyRequest request)
    {
        try
        {
            return Task.FromResult(DiagnosticManager.SetProperty(request.Path, request.Value));
        }
        catch (Exception ex)
        {
            // Mirror the service's behaviour: surface bad-path / conversion failures as a
            // friendly OperationResponse rather than a hub-level exception.
            return Task.FromResult(OperationResponse.Error(ex.Message));
        }
    }

    public async Task<OperationResponse> ExecuteOperation(string processId, OperationRequest request)
    {
        try
        {
            return await DiagnosticManager.ExecuteOperation(request.Path, request.Operation, request.Arguments);
        }
        catch (Exception ex)
        {
            return OperationResponse.Error(ex.Message);
        }
    }
}
#endif
