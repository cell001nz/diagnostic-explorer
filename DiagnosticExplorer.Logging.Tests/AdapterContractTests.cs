using System;
using DiagnosticExplorer.Extensions.Logging;
using DiagnosticExplorer.Logging;
using DiagnosticExplorer.NLog;
using DiagnosticExplorer.Serilog;
using Microsoft.Extensions.Logging;
using NLog.Config;
using Serilog;
using Xunit;
using MicrosoftLogger = Microsoft.Extensions.Logging.ILogger;
using NLogManager = global::NLog.LogManager;
using SerilogLogger = global::Serilog.ILogger;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace DiagnosticExplorer.Logging.Tests;

public class AdapterContractTests
{
    [Fact]
    public void MicrosoftExtensionsLoggingRoutesCategoryAndException()
    {
        EventSinkRepo repo = new();
        using DiagnosticExplorerLoggerProvider provider = new(CreateOptions(), repo);
        MicrosoftLogger logger = provider.CreateLogger("Widgets.Component");

        logger.LogError(
            new EventId(42, "Save"),
            new InvalidOperationException("write failed"),
            "Could not save {WidgetId}",
            7
        );

        SystemEvent loggedEvent = Assert.Single(repo.GetSink("Widget Events", "Widgets").Events);
        Assert.Equal((int)LogLevel.Error, loggedEvent.Level);
        Assert.Equal("Could not save 7", loggedEvent.Message);
        Assert.Contains("write failed", loggedEvent.Detail);
        Assert.Contains("EventId: 42 Save", loggedEvent.Detail);
    }

    [Fact]
    public void SerilogRoutesSourceContext()
    {
        EventSinkRepo repo = new();
        using var logger = new LoggerConfiguration()
            .WriteTo.Sink(new DiagnosticExplorerSink(CreateOptions(), sinkRepo: repo))
            .CreateLogger();

        logger
            .ForContext("SourceContext", "Widgets.Component")
            .Information("Created {WidgetId}", 7);

        SystemEvent loggedEvent = Assert.Single(repo.GetSink("Widget Events", "Widgets").Events);
        Assert.Equal((int)LogLevel.Information, loggedEvent.Level);
        Assert.Equal("Created 7", loggedEvent.Message);
        Assert.Contains("Property.WidgetId", loggedEvent.Detail);
    }

    [Fact]
    public void NLogRoutesLoggerName()
    {
        EventSinkRepo repo = new();
        LoggingConfiguration configuration = new();
        configuration.AddTarget(
            "diagnosticExplorer",
            new DiagnosticExplorerTarget(CreateOptions(), repo)
        );
        configuration.AddRuleForAllLevels("diagnosticExplorer");
        NLogManager.Configuration = configuration;

        try
        {
            NLogManager.GetLogger("Widgets.Component").Info("Created {0}", 7);
            NLogManager.Flush();

            SystemEvent loggedEvent = Assert.Single(
                repo.GetSink("Widget Events", "Widgets").Events
            );
            Assert.Equal((int)LogLevel.Information, loggedEvent.Level);
            Assert.Equal("Created 7", loggedEvent.Message);
        }
        finally
        {
            NLogManager.Shutdown();
        }
    }

    private static EventSinkRouteOptions CreateOptions()
    {
        return new EventSinkRouteOptions
        {
            Routes =
            {
                new EventSinkRoute
                {
                    CategoryPattern = "Widgets",
                    Destinations =
                    {
                        new EventSinkDestination
                        {
                            SinkName = "Widget Events",
                            SinkCategory = "Widgets",
                        },
                    },
                },
            },
        };
    }
}
