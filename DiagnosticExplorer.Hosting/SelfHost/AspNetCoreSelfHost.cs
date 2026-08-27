#if NET6_0_OR_GREATER
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiagnosticExplorer;

/// <summary>Registers self-host diagnostics services in an ASP.NET Core application.</summary>
public static class DiagnosticSelfHostServiceCollectionExtensions
{
    public static IServiceCollection AddDiagnosticSelfHost(
        this IServiceCollection services,
        DiagnosticConfiguration configuration,
        Action<SelfHostOptions> configure = null
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        DiagnosticManager.UseConfiguration(configuration);
        DiagnosticRuntimeOptions runtime = configuration.RuntimeOptions;
        string selfHostUrl = runtime.Hosts.FirstOrDefault(host => host.Type == DiagnosticHostType.SelfHost)?.Url;
        return services.AddDiagnosticSelfHost(options =>
        {
            options.Enabled = runtime.Enabled;
            options.Url = selfHostUrl ?? options.Url;
            configure?.Invoke(options);
        });
    }

    public static IServiceCollection AddDiagnosticSelfHost(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<SelfHostOptions> configure = null
    )
    {
        ArgumentNullException.ThrowIfNull(configuration);
        DiagExplorerOptions hostOptions = configuration.GetSection(DiagExplorerOptions.ConfigurationSectionName).Get<DiagExplorerOptions>() ?? new();
        return services.AddDiagnosticSelfHost(options =>
        {
            options.Enabled = hostOptions.Enabled;
            options.Url = hostOptions.Hosts.FirstOrDefault(host => host.Type == DiagnosticHostType.SelfHost)?.Url ?? options.Url;
            configure?.Invoke(options);
            DiagnosticManager.Enabled = options.Enabled;
        });
    }

    public static IServiceCollection AddDiagnosticSelfHost(this IServiceCollection services, Action<SelfHostOptions> configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        SelfHostOptions options = new();
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.AddSingleton<SelfHostManager>();
        services
            .AddSignalR()
            .AddHubOptions<SelfHostWebHub>(hub => hub.EnableDetailedErrors = options.EnableDetailedErrors)
            .AddJsonProtocol(json => json.PayloadSerializerOptions.PropertyNameCaseInsensitive = true);
        return services;
    }
}

/// <summary>Maps the local diagnostics hub and its embedded viewer assets.</summary>
public static class DiagnosticSelfHostEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapDiagnosticSelfHost(this IEndpointRouteBuilder endpoints, string pathBase = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        SelfHostOptions options = endpoints.ServiceProvider.GetRequiredService<SelfHostOptions>();
        if (!DiagnosticManager.Enabled || !options.Enabled)
            return endpoints;

        string basePath = pathBase ?? options.GetNormalizedPathBase();
        if (string.IsNullOrWhiteSpace(basePath))
            basePath = string.Empty;
        else
            basePath = "/" + basePath.Trim('/');

        endpoints.MapHub<SelfHostWebHub>($"{basePath}/hub");
        endpoints.MapGet($"{basePath}/{{**assetPath}}", async context => await WriteAssetAsync(context));
        return endpoints;
    }

    private static async Task WriteAssetAsync(HttpContext context)
    {
        string assetPath = context.Request.RouteValues["assetPath"]?.ToString();
        if (!SelfHostAssetStore.TryOpen(assetPath, out Stream stream, out string contentType, out bool isIndex))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await using (stream)
        {
            context.Response.ContentType = contentType;
            context.Response.Headers.CacheControl = isIndex ? "no-cache" : "public,max-age=31536000,immutable";
            await stream.CopyToAsync(context.Response.Body, context.RequestAborted);
        }
    }
}

/// <summary>ASP.NET Core SignalR hub for the local diagnostic process.</summary>
public sealed class SelfHostWebHub : Microsoft.AspNetCore.SignalR.Hub<ISelfHostClient>, ISelfHostHub
{
    private readonly SelfHostManager _manager;

    public SelfHostWebHub(SelfHostManager manager) => _manager = manager;

    public override Task OnConnectedAsync()
    {
        _manager.AddClient(Context.ConnectionId, Clients.Caller);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception exception)
    {
        _manager.RemoveClient(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    public Task<SelfHostProcessInfo> GetProcessInfo() => Task.FromResult(_manager.GetProcessInfo());

    public Task<int> GetDiagnosticsRefreshInterval() => Task.FromResult(_manager.GetDiagnosticsRefreshInterval());

    public Task<int> SetDiagnosticsRefreshInterval(int seconds) => Task.FromResult(_manager.SetDiagnosticsRefreshInterval(seconds));

    public Task Subscribe(string processId) => _manager.SubscribeAsync(Context.ConnectionId, processId);

    public Task Unsubscribe(string processId)
    {
        _manager.Unsubscribe(Context.ConnectionId, processId);
        return Task.CompletedTask;
    }

    public Task<DrillDownResponse> GetDrillDown(string processId, DrillDownRequest request) => _manager.GetDrillDownAsync(processId, request);

    public Task<OperationResponse> SetProperty(string processId, SetPropertyRequest request) => _manager.SetPropertyAsync(processId, request);

    public Task<OperationResponse> ExecuteOperation(string processId, OperationRequest request) => _manager.ExecuteOperationAsync(processId, request);
}

internal static class DiagnosticSelfHostFactory
{
    internal static async Task<DiagnosticSelfHost> StartAsync(
        string url,
        SelfHostOptions options,
        CancellationToken cancellationToken,
        IServiceProvider serviceProvider
    )
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(url);
        builder.Services.AddDiagnosticSelfHost(configure =>
        {
            configure.PathBase = options.PathBase;
            configure.EnableDetailedErrors = options.EnableDetailedErrors;
        });
        builder.Services.AddSingleton(new SelfHostManager(serviceProvider));

        WebApplication app = builder.Build();
        app.MapDiagnosticSelfHost();
        await app.StartAsync(cancellationToken).ConfigureAwait(false);

        SelfHostManager manager = app.Services.GetRequiredService<SelfHostManager>();
        return new DiagnosticSelfHost(
            url + options.GetNormalizedPathBase(),
            async () =>
            {
                manager.Dispose();
                using CancellationTokenSource stopToken = new(TimeSpan.FromSeconds(2));
                try
                {
                    await app.StopAsync(stopToken.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stopToken.IsCancellationRequested) { }
                await app.DisposeAsync().ConfigureAwait(false);
            }
        );
    }
}
#endif
