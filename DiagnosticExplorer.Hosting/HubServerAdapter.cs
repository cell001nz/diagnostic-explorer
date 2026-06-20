using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DiagnosticExplorer;
using DiagnosticExplorer.Util;
using log4net;
using Microsoft.AspNetCore.SignalR.Client;

namespace DiagWebService.Hubs;

internal class HubServerAdapter : IDiagnosticHubClient
{
    private static readonly ILog _log = LogManager.GetLogger(typeof(HubServerAdapter));
    private Task _writeEventTask;
    private CancellationTokenSource _writeEventCancel;

    private readonly HubConnection _hubConn;

    public HubServerAdapter(HubConnection hubConn)
    {
        _hubConn = hubConn;

        _hubConn.On<byte[]>(nameof(IDiagnosticHubClient.GetDiagnostics), GetDiagnostics);

        _hubConn.On<string, string, string, OperationResponse>(nameof(IDiagnosticHubClient.SetProperty),
            (requestId, path, value) => SetProperty(requestId, path, value));

        _hubConn.On<string, string, string, string[], OperationResponse>(nameof(IDiagnosticHubClient.ExecuteOperation),
            (requestId, path, operation, args) => ExecuteOperation(requestId, path, operation, args));

        _hubConn.On(nameof(IDiagnosticHubClient.SubscribeEvents),
            async () => await SubscribeEvents());

        _hubConn.On(nameof(IDiagnosticHubClient.UnsubscribeEvents),
            async () => await UnsubscribeEvents());
    }

    public Task SubscribeEvents()
    {
        _writeEventCancel = new CancellationTokenSource();
        _writeEventTask = Task.Run(() => SendEventStream(_writeEventCancel.Token), _writeEventCancel.Token);
        return Task.CompletedTask;
    }

    public Task UnsubscribeEvents()
    {
        _writeEventCancel?.Cancel();
        _writeEventCancel = null;
        _writeEventTask = null;
        return Task.CompletedTask;
    }

    private async Task SendEventStream(CancellationToken cancel)
    {
        using EventSinkStream stream = EventSinkRepo.Default.CreateSinkStream(TimeSpan.FromMilliseconds(50), 100);

        try
        {
            SystemEvent[] initial = stream.InitialEvents;
            await _hubConn.InvokeCoreAsync<string>(nameof(IDiagnosticHubServer.SetEvents), new object[] { initial }, cancel);

            while (await stream.EventChannel.Reader.WaitToReadAsync(cancel))
            {
                IList<SystemEvent> item = await stream.EventChannel.Reader.ReadAsync(cancel);
                await _hubConn.InvokeCoreAsync<string>(nameof(IDiagnosticHubServer.StreamEvents), new object[] { item }, cancel);
            }
        }
        catch (OperationCanceledException)
        {
            Trace.WriteLine("HubServerAdapter.SendEventStream cancelled");
        }
    }

    public void Dispose()
    {
        UnsubscribeEvents();
    }


    public Task<byte[]> GetDiagnostics()
    {
        return Task.Run(() =>
        {
            try
            {
                DiagnosticResponse response = DiagnosticManager.GetDiagnostics();
                return ProtobufUtil.Compress(response, 1024);
            }
            catch (Exception ex)
            {
                _log.Error(ex);
                throw;
            }
        });
    }

    public Task<OperationResponse> SetProperty(string requestId, string path, string value)
    {
        return Task.Run(() =>
        {
            try
            {
                return DiagnosticManager.SetProperty(path, value);
            }
            catch (Exception ex)
            {
                _log.Error(ex);
                return OperationResponse.Error(ex.Message, ex.ToString());
            }
        });
    }

    public async Task<OperationResponse> ExecuteOperation(string requestId, string path, string operation, string[] args)
    {
        return await Task.Run(async () =>
        {
            try
            {
                return await DiagnosticManager.ExecuteOperation(path, operation, args);
            }
            catch (Exception ex)
            {
                _log.Error(ex);
                return OperationResponse.Error(ex.Message, ex.ToString());
            }
        });
    }

    public async Task<RegistrationResponse> Register(Registration registration)
    {
        RpcResult<RegistrationResponse> response = await _hubConn.InvokeCoreAsync<RpcResult<RegistrationResponse>>(nameof(IDiagnosticHubServer.Register), new object[] { registration });
        if (!response.IsSuccess)
            throw new ApplicationException(response.Message);

        return response.Response;
    }

    public async Task Deregister(Registration registration)
    {
        if (_hubConn != null)
        {
            RpcResult response = await _hubConn.InvokeCoreAsync<RpcResult>(nameof(IDiagnosticHubServer.Deregister), new object[] { registration });
            if (!response.IsSuccess)
                throw new ApplicationException(response.Message);
        }
    }

    public async Task LogEvents(byte[] eventData)
    {
        RpcResult response = await _hubConn.InvokeCoreAsync<RpcResult>(nameof(IDiagnosticHubServer.LogEvents), new object[] { eventData });

        if (!response.IsSuccess)
            throw new ApplicationException(response.Message);
    }
}
