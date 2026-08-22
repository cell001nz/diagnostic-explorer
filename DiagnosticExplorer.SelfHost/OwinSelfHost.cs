#if NET48
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Microsoft.Owin;
using Microsoft.Owin.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Owin;

namespace DiagnosticExplorer.SelfHost;

internal static class DiagnosticSelfHostFactory
{
    internal static Task<DiagnosticSelfHost> StartAsync(string url, SelfHostOptions options, System.Threading.CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(options.GetNormalizedPathBase()))
            throw new NotSupportedException("PathBase is not supported by the standalone net48 OWIN host.");

        SelfHostManager manager = new();
        IDisposable server = WebApp.Start(url, app => Configure(app, manager, options));
        DiagnosticSelfHost host = new(url + options.GetNormalizedPathBase(), () =>
        {
            manager.Dispose();
            server.Dispose();
            return Task.CompletedTask;
        });
        return Task.FromResult(host);
    }

    private static void Configure(IAppBuilder app, SelfHostManager manager, SelfHostOptions options)
    {
        JsonSerializer serializer = GlobalHost.DependencyResolver.Resolve<JsonSerializer>();
        serializer.ContractResolver = new SignalRProtocolContractResolver();
        GlobalHost.DependencyResolver.Register(typeof(SelfHostManager), () => manager);
        app.MapSignalR("/hub", new HubConfiguration { EnableDetailedErrors = options.EnableDetailedErrors });
        app.Run(context => WriteAssetAsync(context));
    }

    private static async Task WriteAssetAsync(IOwinContext context)
    {
        if (!SelfHostAssetStore.TryOpen(context.Request.Path.Value, out Stream stream, out string contentType, out bool isIndex))
        {
            context.Response.StatusCode = 404;
            return;
        }

        using (stream)
        {
            context.Response.ContentType = contentType;
            context.Response.Headers.Set("Cache-Control", isIndex ? "no-cache" : "public,max-age=31536000,immutable");
            await stream.CopyToAsync(context.Response.Body);
        }
    }
}

internal sealed class SignalRProtocolContractResolver : CamelCasePropertyNamesContractResolver
{
    private static readonly HashSet<string> SignalRProtocolProperties = new(StringComparer.Ordinal)
    {
        "Url",
        "ConnectionToken",
        "ConnectionId",
        "KeepAliveTimeout",
        "DisconnectTimeout",
        "ConnectionTimeout",
        "TryWebSockets",
        "ProtocolVersion",
        "TransportConnectTimeout",
        "LongPollDelay",
        "A",
        "C",
        "D",
        "E",
        "G",
        "H",
        "I",
        "L",
        "M",
        "R",
        "S",
        "T"
    };

    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        JsonProperty property = base.CreateProperty(member, memberSerialization);
        JsonPropertyAttribute explicitName = member.GetCustomAttribute<JsonPropertyAttribute>();
        if (!string.IsNullOrEmpty(explicitName?.PropertyName))
            property.PropertyName = explicitName.PropertyName;
        else if (SignalRProtocolProperties.Contains(member.Name))
            property.PropertyName = member.Name;

        return property;
    }
}

public sealed class SelfHostWebHub : Hub, ISelfHostHub
{
    private SelfHostManager Manager => GlobalHost.DependencyResolver.Resolve<SelfHostManager>();

    public override Task OnConnected()
    {
        Manager.AddClient(Context.ConnectionId, new OwinSelfHostClient(Clients.Caller));
        return base.OnConnected();
    }

    public override Task OnDisconnected(bool stopCalled)
    {
        Manager.RemoveClient(Context.ConnectionId);
        return base.OnDisconnected(stopCalled);
    }

    public Task<SelfHostProcessInfo> GetProcessInfo() => Task.FromResult(Manager.GetProcessInfo());

    public Task Subscribe(string processId) => Manager.SubscribeAsync(Context.ConnectionId, processId);

    public Task Unsubscribe(string processId)
    {
        Manager.Unsubscribe(Context.ConnectionId, processId);
        return Task.CompletedTask;
    }

    public Task<OperationResponse> SetProperty(string processId, SetPropertyRequest request) => Manager.SetPropertyAsync(processId, request);

    public Task<OperationResponse> ExecuteOperation(string processId, OperationRequest request) => Manager.ExecuteOperationAsync(processId, request);
}

internal sealed class OwinSelfHostClient : ISelfHostClient
{
    private readonly dynamic _client;

    public OwinSelfHostClient(dynamic client) => _client = client;

    public Task ShowDiagnostics(string processId, DiagnosticResponse response) => _client.ShowDiagnostics(processId, response);
    public Task ShowDiagnosticsError(string processId, string message) => _client.ShowDiagnosticsError(processId, message);
    public Task SetEvents(string processId, SystemEvent[] events) => _client.SetEvents(processId, events);
    public Task StreamEvents(string processId, SystemEvent[] events) => _client.StreamEvents(processId, events);
}
#endif