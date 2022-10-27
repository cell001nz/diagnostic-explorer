#region Copyright

// Diagnostic Explorer, a .Net diagnostic toolset
// Copyright (C) 2010 Cameron Elliot
// 
// This file is part of Diagnostic Explorer.
// 
// Diagnostic Explorer is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Diagnostic Explorer is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public License
// along with Diagnostic Explorer.  If not, see <http://www.gnu.org/licenses/>.
// 
// http://diagexplorer.sourceforge.net/

#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Subjects;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading;
using System.Threading.Tasks;
using DiagnosticExplorer.Util;
using log4net.Core;

namespace DiagnosticExplorer;

public class SingleUseDiagnosticClient : ClientBase<IDiagnosticService>, IDiagnosticService
{
    public SingleUseDiagnosticClient(Binding binding, EndpointAddress remoteAddress) : base(binding, remoteAddress)
    {
    }

    public DiagnosticResponse GetDiagnostics(string context)
    {
        return Channel.GetDiagnostics(context);
    }

    public OperationResponse ExecuteOperation(string path, string operation, string[] arguments)
    {
        return Channel.ExecuteOperation(path, operation, arguments);
    }

    public OperationResponse SetProperty(string path, string value)
    {
        return Channel.SetProperty(path, value);
    }
}

public class DiagnosticClient : IDiagnosticClient
{
    private string _uri;
    private string? _eventContext = null;
    

    public Subject<SystemEvent[]> EventsSet { get; } = new();
    public Subject<SystemEvent[]> EventsStreamed { get; } = new();

		
    public DiagnosticClient(string uri)
    {
        _uri = uri;
    }

    private SingleUseDiagnosticClient CreateDiagnosticClient()
    {
        NetTcpBinding binding = new NetTcpBinding(SecurityMode.None);
        binding.MaxReceivedMessageSize = 1_024_000_000;
        binding.OpenTimeout = TimeSpan.FromSeconds(10);
        binding.ReaderQuotas.MaxStringContentLength = 1_024_000_000;

        return new SingleUseDiagnosticClient(binding, new EndpointAddress(new Uri(_uri)));
    }


    public DiagnosticResponse GetDiagnostics(string context)
    {
        SingleUseDiagnosticClient client = CreateDiagnosticClient();
        try
        {
            DiagnosticResponse response = client.GetDiagnostics(context);
            if (response.ProtoResponse != null)
                response = ProtobufUtil.Decompress<DiagnosticResponse>(response.ProtoResponse);

            var sinks = response.Events.Where(sink => sink.Events != null).ToArray();

            foreach (EventResponse sink in sinks)
            {
                foreach (SystemEvent? evt in sink.Events)
                {
                    evt.Level = SeverityToLevel(evt.Severity);
                    evt.SinkCategory = sink.Category;
                    evt.SinkName = sink.Name;
                }
            }

            if (_eventContext == null)
                EventsSet.OnNext(sinks.SelectMany(er => er.Events).ToArray());
            else
                EventsStreamed.OnNext(sinks.SelectMany(er => er.Events).ToArray());

            _eventContext = response.Context;

            return response;
        }
        finally
        {
            CloseAndDispose(client);
        }
    }

    private void CloseAndDispose(SingleUseDiagnosticClient client)
    {
        try
        {
            client.Close();
        }
        catch (Exception ex) { Trace.WriteLine(ex);}
        try
        {
            if (client is IDisposable disposable)
                disposable.Dispose();
        }
        catch (Exception ex) { Trace.WriteLine(ex);}
    }

    private int SeverityToLevel(EventSeverity severity)
    {
        return severity == EventSeverity.Low ? Level.Info.Value
            : severity == EventSeverity.Medium ? Level.Warn.Value
            : Level.Error.Value;
    }

    Task<OperationResponse> IDiagnosticClient.SetProperty(string path, string value)
    {
        return Task.FromResult(SetProperty(path, value));
    }

    Task<OperationResponse> IDiagnosticClient.ExecuteOperation(string path, string operation, string[] arguments)
    {
        return Task.FromResult(ExecuteOperation(path, operation, arguments));
    }

    public Task SubscribeEvents()
    {
        return Task.CompletedTask;
    }

    public Task UnsubscribeEvents()
    {
        _eventContext = null;
        return Task.CompletedTask;
    }


    public OperationResponse ExecuteOperation(string path, string operation, string[] arguments)
    {
        SingleUseDiagnosticClient client = CreateDiagnosticClient();

        try
        {
            return client.ExecuteOperation(path, operation, arguments);
        }
        finally
        {
            CloseAndDispose(client);
        }
    }

    Task<DiagnosticResponse> IDiagnosticClient.GetDiagnostics(CancellationToken cancel)
    {
        return Task.FromResult(GetDiagnostics(_eventContext));
    }

    public OperationResponse SetProperty(string path, string value)
    {
        SingleUseDiagnosticClient client = CreateDiagnosticClient();
        try
        {
            return client.SetProperty(path, value);
        }
        finally
        {
            CloseAndDispose(client);
        }
    }
}