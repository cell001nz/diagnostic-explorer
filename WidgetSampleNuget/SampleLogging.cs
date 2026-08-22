using System;
using System.IO;
using DiagnosticExplorer.Extensions.Logging;
using DiagnosticExplorer.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WidgetSampleNuget;

internal static class SampleLogging
{
    private static ILoggerFactory _factory;

    public static void Configure()
    {
        if (_factory != null)
            return;

        IConfiguration configuration = LoadConfiguration();
        EventSinkRouteOptions routes = ConfigurationBinder.Get<EventSinkRouteOptions>(configuration.GetSection("DiagnosticExplorer:Routing"));
        if (routes == null)
            throw new InvalidOperationException("DiagnosticExplorer:Routing must be configured.");

        _factory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddConsole();
            builder.AddDiagnosticExplorer(routes);
        });
    }

    public static ILogger GetLogger(string category)
    {
        if (_factory == null)
            throw new InvalidOperationException("SampleLogging.Configure must be called before creating loggers.");

        return _factory.CreateLogger(category);
    }

    public static void Shutdown()
    {
        _factory?.Dispose();
        _factory = null;
    }

    private static IConfiguration LoadConfiguration()
    {
        IConfigurationBuilder builder = new ConfigurationBuilder();
        JsonConfigurationExtensions.AddJsonFile(
            builder,
            Path.Combine(AppContext.BaseDirectory, "config.json"),
            optional: false,
            reloadOnChange: false
        );
        return builder.Build();
    }
}
