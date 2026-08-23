using System;
using DiagnosticExplorer.Logging;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace DiagnosticExplorer.Serilog;

public static class LoggerSinkConfigurationExtensions
{
    public static LoggerConfiguration DiagnosticExplorer(
        this LoggerSinkConfiguration sinkConfiguration,
        string fallbackCategory = "Application",
        LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
        LoggingLevelSwitch levelSwitch = null
    )
    {
        return sinkConfiguration.DiagnosticExplorer(
            DiagnosticManager.CurrentConfiguration.RuntimeOptions.Routing,
            fallbackCategory,
            restrictedToMinimumLevel,
            levelSwitch
        );
    }

    public static LoggerConfiguration DiagnosticExplorer(
        this LoggerSinkConfiguration sinkConfiguration,
        EventSinkRouteOptions options,
        string fallbackCategory = "Application",
        LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
        LoggingLevelSwitch levelSwitch = null
    )
    {
        if (sinkConfiguration == null)
            throw new ArgumentNullException(nameof(sinkConfiguration));

        return sinkConfiguration.Sink(new DiagnosticExplorerSink(options, fallbackCategory), restrictedToMinimumLevel, levelSwitch);
    }
}
