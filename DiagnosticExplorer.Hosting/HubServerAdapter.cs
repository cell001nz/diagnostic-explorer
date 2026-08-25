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
using TypedSignalR.Client;

namespace DiagWebService.Hubs;

internal class HubServerAdapter : IDiagnosticHubClient
{
    private static readonly ILog _log = LogManager.GetLogger(typeof(HubServerAdapter));
    private Task _writeEventTask;
    private CancellationTokenSource _writeEventCancel;

    private readonly IDiagnosticHubServer _hubServer;
    private readonly IDisposable _clientRegistration;
    private readonly IServiceProvider _serviceProvider;

    public HubServerAdapter(HubConnection hubConn, IServiceProvider serviceProvider = null)
    {
        _hubServer = hubConn.CreateHubProxy<IDiagnosticHubServer>();
        _clientRegistration = hubConn.Register<IDiagnosticHubClient>(this);
        _serviceProvider = serviceProvider;
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
            await _hubServer.SetEvents(initial);

            while (await stream.EventChannel.Reader.WaitToReadAsync(cancel))
            {
                IList<SystemEvent> item = await stream.EventChannel.Reader.ReadAsync(cancel);
                await _hubServer.StreamEvents(item.ToArray());
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
        _clientRegistration.Dispose();
    }

    public Task<byte[]> GetDiagnostics()
    {
        return Task.Run(() =>
        {
            try
            {
                DiagnosticResponse response = DiagnosticManager.GetDiagnostics(_serviceProvider);
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
                return DiagnosticManager.SetProperty(_serviceProvider, path, value);
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
                return await DiagnosticManager.ExecuteOperation(_serviceProvider, path, operation, args);
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
        RpcResult<RegistrationResponse> response = await _hubServer.Register(registration);
        if (!response.IsSuccess)
            throw new ApplicationException(response.Message);

        return response.Response;
    }

    public async Task Deregister(Registration registration)
    {
        RpcResult response = await _hubServer.Deregister(registration);
        if (!response.IsSuccess)
            throw new ApplicationException(response.Message);
    }

    public async Task LogEvents(byte[] eventData)
    {
        RpcResult response = await _hubServer.LogEvents(eventData);

        if (!response.IsSuccess)
            throw new ApplicationException(response.Message);
    }
}
