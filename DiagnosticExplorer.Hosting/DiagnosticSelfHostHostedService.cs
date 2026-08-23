#if NET5_0_OR_GREATER
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DiagnosticExplorer;

internal sealed class DiagnosticSelfHostHostedService : IHostedService
{
    private readonly DiagExplorerOptions _options;
    private DiagnosticSelfHost[] _selfHosts = Array.Empty<DiagnosticSelfHost>();

    public DiagnosticSelfHostHostedService(IOptions<DiagExplorerOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return;

        DiagnosticHostOptions[] hosts = _options
            .Hosts.Where(host => host.Type == DiagnosticHostType.SelfHost && !string.IsNullOrWhiteSpace(host.Url))
            .ToArray();
        _selfHosts = await Task.WhenAll(
            hosts.Select(host =>
                DiagnosticSelfHostingService.StartAsync(
                    host.Url,
                    new SelfHostOptions { Enabled = _options.Enabled, Url = host.Url },
                    cancellationToken
                )
            )
        );
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.WhenAll(_selfHosts.Select(host => host.StopAsync()));
}
#endif
