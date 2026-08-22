using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace DiagnosticExplorer.SelfHost;

/// <summary>Starts and stops a self-hosted diagnostics viewer.</summary>
public static class DiagnosticSelfHostingService
{
    private static readonly ConcurrentDictionary<string, DiagnosticSelfHost> Hosts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Starts a standalone listener from the <c>DiagnosticExplorer:SelfHostUrl</c> configuration key.</summary>
    public static Task<DiagnosticSelfHost> StartAsync(IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        SelfHostOptions options = new()
        {
            Enabled = configuration.GetValue<bool?>(DiagnosticManager.EnabledConfigurationKey) ?? true,
            Url = configuration[SelfHostOptions.SelfHostUrlConfigurationKey] ?? SelfHostOptions.DefaultUrl,
        };
        DiagnosticManager.Enabled = options.Enabled;
        return StartAsync(options.Url, options, cancellationToken);
    }

    /// <summary>Starts a standalone self-host listener.</summary>
    public static async Task<DiagnosticSelfHost> StartAsync(
        string url = null,
        SelfHostOptions options = null,
        CancellationToken cancellationToken = default
    )
    {
        SelfHostOptions resolvedOptions = options ?? new SelfHostOptions();
        if (!DiagnosticManager.Enabled || !resolvedOptions.Enabled)
            return DiagnosticSelfHost.Disabled();

        string listenerUrl = string.IsNullOrWhiteSpace(url) ? resolvedOptions.Url : url;
        if (string.IsNullOrWhiteSpace(listenerUrl))
            listenerUrl = SelfHostOptions.DefaultUrl;
        if (Hosts.ContainsKey(listenerUrl))
            throw new InvalidOperationException($"A self-host listener is already running at '{listenerUrl}'.");

        DiagnosticSelfHost host = await DiagnosticSelfHostFactory.StartAsync(listenerUrl, resolvedOptions, cancellationToken).ConfigureAwait(false);
        if (!Hosts.TryAdd(listenerUrl, host))
        {
            await host.StopAsync().ConfigureAwait(false);
            throw new InvalidOperationException($"A self-host listener is already running at '{listenerUrl}'.");
        }

        host.Stopped += (_, _) => Hosts.TryRemove(listenerUrl, out _);
        return host;
    }
}

/// <summary>Represents a running standalone self-host listener.</summary>
public sealed class DiagnosticSelfHost : IDisposable
{
    private readonly Func<Task> _stop;
    private int _stopped;

    internal DiagnosticSelfHost(string url, Func<Task> stop, bool isEnabled = true)
    {
        Url = url;
        _stop = stop;
        IsEnabled = isEnabled;
    }

    internal static DiagnosticSelfHost Disabled() => new(null, () => Task.CompletedTask, false);

    /// <summary>Raised once the host has stopped.</summary>
    public event EventHandler Stopped;

    /// <summary>Gets the listener URL.</summary>
    public string Url { get; }

    /// <summary>Gets whether a local listener was started.</summary>
    public bool IsEnabled { get; }

    /// <summary>Stops the listener and releases its diagnostic subscriptions.</summary>
    public async Task StopAsync()
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0)
            return;

        await _stop().ConfigureAwait(false);
        Stopped?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Initiates shutdown without blocking the calling thread.</summary>
    public void Dispose()
    {
        _ = ObserveStopAsync(StopAsync());
    }

    private static async Task ObserveStopAsync(Task stopTask)
    {
        try
        {
            await stopTask.ConfigureAwait(false);
        }
        catch { }
    }
}
