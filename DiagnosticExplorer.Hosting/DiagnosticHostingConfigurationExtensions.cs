using System;
using Microsoft.Extensions.Configuration;

namespace DiagnosticExplorer;

public static class DiagnosticHostingConfigurationExtensions
{
    public static void ConfigureHosting(this IDiagConfigurator config, IConfiguration configuration)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        DiagExplorerOptions options = configuration.GetSection(DiagExplorerOptions.ConfigurationSectionName).Get<DiagExplorerOptions>() ?? new();
        config.ConfigureHosting(options);
    }

    public static void ConfigureHosting(this IDiagConfigurator config, DiagExplorerOptions options)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        config.ConfigureHosting(configure =>
        {
            configure.Enabled(options.Enabled);

            foreach (DiagnosticHostOptions host in options.Hosts ?? new())
            {
                if (host == null || string.IsNullOrWhiteSpace(host.Url))
                    continue;

                configure.AddHost(host.Type, host.Url);
            }

            configure.EventRetention(retention =>
            {
                retention.MaxEventsPerSink = options.EventRetention.MaxEventsPerSink;
                retention.MaxAgeMinutes = options.EventRetention.MaxAgeMinutes;
            });
            config.ConfigureLogEventRetention(retention =>
            {
                retention.MaxEvents = options.LogEventRetention.MaxEvents;
                retention.MaxAgeMinutes = options.LogEventRetention.MaxAgeMinutes;
            });
        });
    }
}
