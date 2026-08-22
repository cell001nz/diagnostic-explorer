using System;
using DiagnosticExplorer.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DiagnosticExplorer.Extensions.Logging;

public static class LoggingBuilderExtensions
{
    public static ILoggingBuilder AddDiagnosticExplorer(
        this ILoggingBuilder builder,
        EventSinkRouteOptions options
    )
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));

        builder.AddProvider(new DiagnosticExplorerLoggerProvider(options));
        return builder;
    }

    public static ILoggingBuilder AddDiagnosticExplorer(
        this ILoggingBuilder builder,
        Action<EventSinkRouteOptions> configure
    )
    {
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        EventSinkRouteOptions options = new();
        configure(options);
        return builder.AddDiagnosticExplorer(options);
    }

    public static ILoggingBuilder AddDiagnosticExplorer(
        this ILoggingBuilder builder,
        IConfiguration configuration
    )
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        EventSinkRouteOptions options =
            configuration.Get<EventSinkRouteOptions>() ?? new EventSinkRouteOptions();
        return builder.AddDiagnosticExplorer(options);
    }
}
