using System;
using DiagnosticExplorer.Logging;
using NLog.Config;

namespace DiagnosticExplorer.NLog;

public static class LoggingConfigurationExtensions
{
    public static DiagnosticExplorerTarget AddDiagnosticExplorer(
        this LoggingConfiguration configuration,
        string targetName,
        EventSinkRouteOptions options
    )
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        if (string.IsNullOrWhiteSpace(targetName))
            throw new ArgumentException("A target name is required.", nameof(targetName));

        DiagnosticExplorerTarget target = new(options);
        configuration.AddTarget(targetName, target);
        return target;
    }
}
