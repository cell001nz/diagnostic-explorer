using System;
using DiagnosticExplorer.Logging;
using log4net.Core;
using log4net.Repository;
using log4net.Repository.Hierarchy;

namespace DiagnosticExplorer.Log4Net;

public static class LoggingRepositoryExtensions
{
    public static void ConfigureDiagnosticExplorer(this ILoggerRepository repository)
    {
        if (repository == null)
            throw new ArgumentNullException(nameof(repository));
        if (repository is not Hierarchy hierarchy)
            throw new NotSupportedException("DiagnosticExplorer requires a log4net hierarchy repository.");

        hierarchy.ResetConfiguration();
        hierarchy.Root.Level = Level.All;

        RoutingDiagnosticAppender appender = new()
        {
            Name = "DiagnosticExplorer",
            RoutingOptions = DiagnosticManager.CurrentConfiguration.RuntimeOptions.Routing,
        };
        appender.ActivateOptions();
        hierarchy.Root.AddAppender(appender);
        hierarchy.Configured = true;
    }
}
