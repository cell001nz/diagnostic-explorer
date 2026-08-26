using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using DiagnosticExplorer;
using DiagnosticExplorer.Logging;
using DiagnosticExplorer.Util;
using Diagnostics.Service.Common.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace DiagWebService.ClientHandlers;

public class DiagnosticClientHandler : IDiagnosticClient
{
    private readonly IHubContext<DiagnosticHub, IDiagnosticHubClient> _hubContext;
    private readonly HubCallerContext _callerContext;
    public event EventHandler Disconnected;
    public Subject<LogStreamInitialization> LogStreamInitialized { get; } = new();
    public Subject<LogStreamEvent[]> LogStreamEvents { get; } = new();

    public DiagnosticClientHandler(HubCallerContext callerContext, IHubContext<DiagnosticHub, IDiagnosticHubClient> hubContext)
    {
        _hubContext = hubContext;
        _callerContext = callerContext;
        ConnectionId = callerContext.ConnectionId;
        _callerContext.ConnectionAborted.Register(() => Disconnected?.Invoke(this, EventArgs.Empty));
    }

    public string ConnectionId { get; }

    /// <summary>
    /// Resolves a fresh strongly-typed proxy for this connection on every call.
    /// We must never cache <c>Clients.Caller</c>: while a connection runs
    /// <c>OnConnectedAsync</c> it is a "NotAllowed" proxy, and invoking a client
    /// result on it later throws "Client results inside OnConnectedAsync Hub methods
    /// are not allowed". Resolving via <see cref="IHubContext{THub,T}"/> always yields
    /// an invoke-allowed proxy.
    /// </summary>
    private IDiagnosticHubClient Client => _hubContext.Clients.Client(ConnectionId);

    public async Task<DiagnosticResponse> GetDiagnostics(CancellationToken cancel)
    {
        byte[] data = await Client.GetDiagnostics();
        return ProtobufUtil.Decompress<DiagnosticResponse>(data);
    }

    public async Task<DrillDownResponse> GetDrillDown(DrillDownRequest request)
    {
        byte[] data = await Client.GetDrillDown(Guid.NewGuid().ToString("N"), request);
        return ProtobufUtil.Decompress<DrillDownResponse>(data);
    }

    public Task<OperationResponse> SetProperty(SetPropertyRequest request)
    {
        string requestId = Guid.NewGuid().ToString("N");
        return request.ObjectPaths == null || request.ObjectPaths.Length == 0
            ? Client.SetProperty(requestId, request.Path, request.Value)
            : Client.SetPropertyWithContext(requestId, request);
    }

    public Task<OperationResponse> ExecuteOperation(OperationRequest request)
    {
        string requestId = Guid.NewGuid().ToString("N");
        return request.ObjectPaths == null || request.ObjectPaths.Length == 0
            ? Client.ExecuteOperation(requestId, request.Path, request.Operation, request.Arguments)
            : Client.ExecuteOperationWithContext(requestId, request);
    }

    public async Task SubscribeEvents()
    {
        await Client.SubscribeEvents();
    }

    public async Task UnsubscribeEvents()
    {
        await Client.UnsubscribeEvents();
    }

    public void InitializeLogStream(LogStreamInitialization initialization)
    {
        LogStreamInitialized.OnNext(initialization);
    }

    public void StreamLogEvents(LogStreamEvent[] events)
    {
        LogStreamEvents.OnNext(events);
    }

    public void CloseConnection()
    {
        _callerContext.Abort();
    }
}
