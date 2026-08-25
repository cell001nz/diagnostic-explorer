using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DiagnosticExplorer.Log4Net;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DiagnosticExplorer;

public class DiagnosticHostingService : IHostedService
{
    private static DiagnosticHostingService _instance;
    private static DiagnosticSelfHost[] _selfHosts = Array.Empty<DiagnosticSelfHost>();
    private DiagExplorerOptions _options;

    private RegistrationHandler[] _registrationHandlers;

    private Action<HttpConnectionOptions> _configureHttp;

    private DiagnosticHostingService(DiagExplorerOptions options, Action<HttpConnectionOptions> configureHttp = null)
    {
        _options = options;
        _configureHttp = configureHttp;
    }

    public DiagnosticHostingService(IOptions<DiagExplorerOptions> options, Action<HttpConnectionOptions> configureHttp = null)
        : this(options.Value, configureHttp)
    {
        Debug.WriteLine($"DiagnosticHostingService constructed {_options.Enabled}");
    }

    public async Task StartAsync(CancellationToken cancel)
    {
        Debug.WriteLine($"DiagnosticHostingService starting {_options.Enabled}");
        if (_options.Enabled)
        {
            _instance = this;
            StartHosting();
        }
    }

    public Task StopAsync(CancellationToken cancel)
    {
        return StopHosting();
    }

    private void StartHosting()
    {
        if (!DiagnosticManager.Enabled)
            return;

        try
        {
            DiagnosticRetroAppender.SetLoggingAction(LogEvent);
            SystemStatus.Register();

            Registration registration = new()
            {
                ProcessId = Process.GetCurrentProcess().Id,
                InstanceId = Guid.NewGuid().ToString("N"),
                UserDomain = Environment.UserDomainName,
                UserName = Environment.UserName,
                MachineName = Environment.MachineName,
                ProcessName = ResolveProcessName(),
            };

            _registrationHandlers = _options
                .Hosts.Where(host => host.Type == DiagnosticHostType.Remote)
                .SelectMany(host => Regex.Split(host.Url ?? string.Empty, @"\s|;|,"))
                .Select(hubUrl => hubUrl.Trim())
                .Where(hubUrl => !string.IsNullOrWhiteSpace(hubUrl))
                .Select(hubUrl => new RegistrationHandler(hubUrl, registration))
                .ToArray();

            foreach (RegistrationHandler handler in _registrationHandlers)
                handler.Start(_configureHttp);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    // The entry assembly name is a stabler identifier than the OS process
    // name for .NET apps: `dotnet MyApp.dll` reports "dotnet" as the
    // process, but the entry assembly is still "MyApp". Falls back to the
    // process name when there is no managed entry assembly (rare -- mostly
    // unmanaged hosts).
    private static string ResolveProcessName()
    {
        string entryAssemblyName = Assembly.GetEntryAssembly()?.GetName().Name;
        if (!string.IsNullOrEmpty(entryAssemblyName))
            return entryAssemblyName;

        return Process.GetCurrentProcess().ProcessName.Replace(".vshost", "");
    }

    public async Task StopHosting()
    {
        try
        {
            DiagnosticRetroAppender.SetLoggingAction(null);
            await Task.WhenAll(_registrationHandlers.Select(handler => handler.Stop()).ToArray());
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }

        _registrationHandlers = null;
    }

    public static void Start(string url, Action<HttpConnectionOptions> configureHttp = null)
    {
        if (!DiagnosticManager.Enabled)
            return;

        if (_instance == null)
        {
            DiagExplorerOptions options = new()
            {
                Hosts =
                {
                    new DiagnosticHostOptions { Type = DiagnosticHostType.Remote, Url = url },
                },
            };
            _instance = new DiagnosticHostingService(options, configureHttp);
            _instance.StartHosting();
        }
    }

    public static void Start(DiagnosticConfiguration configuration, Action<HttpConnectionOptions> configureHttp = null)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        DiagnosticManager.UseConfiguration(configuration);
        if (!configuration.RuntimeOptions.Enabled)
            return;
        string remoteUrl = configuration.RuntimeOptions.Hosts.FirstOrDefault(host => host.Type == DiagnosticHostType.Remote)?.Url;
        if (string.IsNullOrWhiteSpace(remoteUrl))
            throw new InvalidOperationException("A remote diagnostics URL has not been configured.");

        Start(remoteUrl, configureHttp);
    }

    public static async Task StartAsync(DiagnosticConfiguration configuration, Action<HttpConnectionOptions> configureHttp = null)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        DiagnosticManager.UseConfiguration(configuration);
        DiagnosticRuntimeOptions runtime = configuration.RuntimeOptions;
        if (!runtime.Enabled)
            return;

        if (_instance == null && runtime.Hosts.Any(host => host.Type == DiagnosticHostType.Remote))
        {
            _instance = new DiagnosticHostingService(
                new DiagExplorerOptions { Enabled = runtime.Enabled, Hosts = runtime.Hosts.ToList() },
                configureHttp
            );
            _instance.StartHosting();
        }

        if (_selfHosts.Length == 0)
        {
            DiagnosticHostOptions[] selfHostOptions = runtime
                .Hosts.Where(host => host.Type == DiagnosticHostType.SelfHost && !string.IsNullOrWhiteSpace(host.Url))
                .ToArray();
            _selfHosts = await Task.WhenAll(
                selfHostOptions.Select(host =>
                    DiagnosticSelfHostingService.StartAsync(host.Url, new SelfHostOptions { Enabled = runtime.Enabled, Url = host.Url })
                )
            );
        }
    }

    public static async Task Stop()
    {
        if (_instance != null)
        {
            await _instance.StopHosting();
            _instance = null;
        }

        DiagnosticSelfHost[] selfHosts = _selfHosts;
        _selfHosts = Array.Empty<DiagnosticSelfHost>();
        await Task.WhenAll(selfHosts.Select(host => host.StopAsync()));
    }

    public static void LogEvent(DiagnosticMsg evt)
    {
        DiagnosticHostingService instance = _instance;
        if (instance != null)
            // Debug.WriteLine($"Sending to {instance._registrationHandlers?.Length} registration handlers");
            foreach (RegistrationHandler handler in instance._registrationHandlers ?? Array.Empty<RegistrationHandler>())
                handler.LogEvent(evt);
    }
}
