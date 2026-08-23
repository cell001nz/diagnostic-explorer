#if NET5_0_OR_GREATER

using System;
using System.Linq;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DiagnosticExplorer
{
    public static class DiagnosticHostingExtensions
    {
        public static IServiceCollection ConfigureDiagnosticExplorer(this IServiceCollection services, Action<IDiagConfigurator> configureDiagnostics)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            if (configureDiagnostics == null)
                throw new ArgumentNullException(nameof(configureDiagnostics));

            DiagnosticConfiguration diagnosticConfiguration = new();
            configureDiagnostics(diagnosticConfiguration);
            return services.AddDiagnosticExplorer(diagnosticConfiguration);
        }

        public static IServiceCollection AddDiagnosticExplorer(
            this IServiceCollection services,
            IConfiguration config,
            Action<HttpConnectionOptions> configureHttp = null
        )
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            DiagExplorerOptions options = config.GetSection(DiagExplorerOptions.ConfigurationSectionName).Get<DiagExplorerOptions>() ?? new();
            DiagnosticManager.Enabled = options.Enabled;
            services.Configure<DiagExplorerOptions>(config.GetSection(DiagExplorerOptions.ConfigurationSectionName));
            AddConfiguredHostedServices(services, options, configureHttp);
            return services;
        }

        public static IServiceCollection AddDiagnosticExplorer(
            this IServiceCollection services,
            DiagnosticConfiguration configuration,
            Action<HttpConnectionOptions> configureHttp = null
        )
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            DiagnosticManager.UseConfiguration(configuration);
            DiagnosticRuntimeOptions runtime = configuration.RuntimeOptions;
            services.Configure<DiagExplorerOptions>(options =>
            {
                options.Enabled = runtime.Enabled;
                options.Hosts = runtime.Hosts.Select(host => new DiagnosticHostOptions { Type = host.Type, Url = host.Url }).ToList();
                options.EventRetention = new EventRetentionOptions
                {
                    MaxEventsPerSink = runtime.EventRetention.MaxEventsPerSink,
                    MaxAgeMinutes = runtime.EventRetention.MaxAgeMinutes,
                };
            });
            AddConfiguredHostedServices(
                services,
                new DiagExplorerOptions
                {
                    Enabled = runtime.Enabled,
                    Hosts = runtime.Hosts.Select(host => new DiagnosticHostOptions { Type = host.Type, Url = host.Url }).ToList(),
                },
                configureHttp
            );
            return services;
        }

        private static void AddConfiguredHostedServices(
            IServiceCollection services,
            DiagExplorerOptions options,
            Action<HttpConnectionOptions> configureHttp
        )
        {
            if (!options.Enabled)
                return;

            if (options.Hosts.Any(host => host.Type == DiagnosticHostType.Remote && !string.IsNullOrWhiteSpace(host.Url)))
                services.AddHostedService(sp => new DiagnosticHostingService(sp.GetRequiredService<IOptions<DiagExplorerOptions>>(), configureHttp));
            if (options.Hosts.Any(host => host.Type == DiagnosticHostType.SelfHost && !string.IsNullOrWhiteSpace(host.Url)))
                services.AddHostedService<DiagnosticSelfHostHostedService>();
        }
    }
}

#endif
