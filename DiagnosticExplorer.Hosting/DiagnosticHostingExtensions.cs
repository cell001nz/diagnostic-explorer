#if NET5_0_OR_GREATER

using System;
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
            services.AddHostedService(sp => new DiagnosticHostingService(sp.GetRequiredService<IOptions<DiagExplorerOptions>>(), configureHttp));
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
                options.RemoteUrl = runtime.RemoteUrl;
                options.SelfHostUrl = runtime.SelfHostUrl;
                options.EventRetention = new EventRetentionOptions
                {
                    MaxEventsPerSink = runtime.EventRetention.MaxEventsPerSink,
                    MaxAgeMinutes = runtime.EventRetention.MaxAgeMinutes,
                };
            });
            services.AddHostedService(sp => new DiagnosticHostingService(sp.GetRequiredService<IOptions<DiagExplorerOptions>>(), configureHttp));
            return services;
        }
    }
}

#endif
