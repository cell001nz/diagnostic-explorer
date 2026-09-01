using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DiagnosticExplorer;

internal sealed class DiagnosticSelfHostHostedService : IHostedService
{
    private readonly DiagExplorerOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private DiagnosticSelfHost[] _selfHosts = Array.Empty<DiagnosticSelfHost>();

    public DiagnosticSelfHostHostedService(IOptions<DiagExplorerOptions> options, IServiceProvider serviceProvider)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return;

        DiagnosticHostOptions[] hosts = _options
            .Hosts.Where(host => host.Type == DiagnosticHostType.SelfHost && !string.IsNullOrWhiteSpace(host.Url))
            .ToArray();
        List<DiagnosticSelfHost> startedHosts = new();
        foreach (DiagnosticHostOptions host in hosts)
        {
            try
            {
                startedHosts.Add(
                    await DiagnosticSelfHostingService.StartAsync(
                        host.Url,
                        new SelfHostOptions { Enabled = _options.Enabled, Url = host.Url },
                        cancellationToken,
                        _serviceProvider
                    )
                );
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                Trace.TraceError($"Diagnostic Explorer self-host at '{host.Url}' failed to start: {exception}");
            }
        }

        _selfHosts = startedHosts.ToArray();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.WhenAll(_selfHosts.Select(host => host.StopAsync()));
}
