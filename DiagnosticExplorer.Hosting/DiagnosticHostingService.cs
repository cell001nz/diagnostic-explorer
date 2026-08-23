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

public class DiagnosticHostingService
#if NET5_0_OR_GREATER
    : IHostedService
#endif
{
    private static DiagnosticHostingService _instance;
    private DiagExplorerOptions _options;

    private RegistrationHandler[] _registrationHandlers;

    private Action<HttpConnectionOptions> _configureHttp;

    private DiagnosticHostingService(DiagExplorerOptions options, Action<HttpConnectionOptions> configureHttp = null)
    {
        _options = options;
        _configureHttp = configureHttp;
    }

#if NET5_0_OR_GREATER

    public DiagnosticHostingService(IOptions<DiagExplorerOptions> options, Action<HttpConnectionOptions> configureHttp = null)
        : this(options.Value, configureHttp)
    {
        Debug.WriteLine($"DiagnosticHostingService constructed {_options.Enabled} RemoteUrl [{_options.RemoteUrl}");
    }

    public async Task StartAsync(CancellationToken cancel)
    {
        Debug.WriteLine($"DiagnosticHostingService starting {_options.Enabled} RemoteUrl [{_options.RemoteUrl}");
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
#endif

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

            _registrationHandlers = Regex
                .Split(_options.RemoteUrl, @"\s|;|,")
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
            DiagExplorerOptions options = new() { RemoteUrl = url };
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
        if (string.IsNullOrWhiteSpace(configuration.RuntimeOptions.RemoteUrl))
            throw new InvalidOperationException("A remote diagnostics URL has not been configured.");

        Start(configuration.RuntimeOptions.RemoteUrl, configureHttp);
    }

    public static async Task Stop()
    {
        if (_instance != null)
        {
            await _instance.StopHosting();
            _instance = null;
        }
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
