using System;
using DiagnosticExplorer.Logging;
using log4net;
using log4net.Core;
using Microsoft.Extensions.Logging;
using Xunit;
using RoutingDiagnosticAppender = global::DiagnosticExplorer.Log4Net.RoutingDiagnosticAppender;

namespace DiagnosticExplorer.Logging.Tests;

public class Log4NetRoutingAppenderTests
{
    [Fact]
    public void FluentConfigurationDefinesAllMigratedRoutes()
    {
        EventSinkRouteOptions routes = new();
        routes
            .UseMatchMode(EventSinkRouteMatchMode.AllMatches)
            .Route("Widgets", route => route.AtLeast(LogLevel.Information).To("Widgets", "Widgets Events"))
            .Route("Gadgets", route => route.AtLeast(LogLevel.Information).To("Gadgets", "Gadget Events"))
            .Route("WidgetSample.Form1", route => route.AtLeast(LogLevel.Trace).To("Form 1", "Form1 Events Only"))
            .Route("*", route => route.AtLeast(LogLevel.Warning).AtMost(LogLevel.Warning).To("System", "Warnings"))
            .Route("*", route => route.AtLeast(LogLevel.Error).To("System", "Errors"));

        EventSinkRepo sinkRepo = new();
        TestRoutingDiagnosticAppender appender = new(sinkRepo) { RoutingOptions = routes };

        appender.ActivateOptions();
        appender.AppendForTest(CreateEvent("Widgets.Component", Level.Warn, "Paint failed"));

        Assert.Single(sinkRepo.GetSink("Widgets Events", "Widgets").Events);
        Assert.Single(sinkRepo.GetSink("Warnings", "System").Events);
    }

    [Fact]
    public void RoutesLoggerNameThroughSharedRules()
    {
        EventSinkRepo sinkRepo = new();
        TestRoutingDiagnosticAppender appender = new(sinkRepo)
        {
            RoutingOptions = new EventSinkRouteOptions
            {
                Routes =
                {
                    new EventSinkRoute
                    {
                        CategoryPattern = "Widgets",
                        Destinations =
                        {
                            new EventSinkDestination { SinkName = "Widget Events", SinkCategory = "Widgets" },
                        },
                    },
                    new EventSinkRoute
                    {
                        CategoryPattern = "*",
                        MinLevel = LogLevel.Warning,
                        MaxLevel = LogLevel.Warning,
                        Destinations =
                        {
                            new EventSinkDestination { SinkName = "Warnings", SinkCategory = "System" },
                        },
                    },
                },
            },
        };
        appender.ActivateOptions();

        string repositoryName = Guid.NewGuid().ToString("N");
        var repository = LogManager.CreateRepository(repositoryName);

        try
        {
            appender.AppendForTest(CreateEvent("Widgets.Component", Level.Warn, "Paint failed", repository));

            Assert.Single(sinkRepo.GetSink("Widget Events", "Widgets").Events);
            Assert.Single(sinkRepo.GetSink("Warnings", "System").Events);
        }
        finally
        {
            LogManager.ShutdownRepository(repositoryName);
        }
    }

    private sealed class TestRoutingDiagnosticAppender : RoutingDiagnosticAppender
    {
        public TestRoutingDiagnosticAppender(EventSinkRepo sinkRepo)
            : base(sinkRepo) { }

        public void AppendForTest(LoggingEvent loggingEvent)
        {
            Append(loggingEvent);
        }
    }

    private static LoggingEvent CreateEvent(string loggerName, Level level, string message, log4net.Repository.ILoggerRepository repository = null)
    {
        return new LoggingEvent(typeof(Log4NetRoutingAppenderTests), repository ?? LogManager.GetRepository(), loggerName, level, message, null);
    }
}
