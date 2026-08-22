using System;
using System.IO;
using DiagnosticExplorer.Extensions.Logging;
using DiagnosticExplorer.Logging;
using DiagnosticExplorer.NLog;
using DiagnosticExplorer.Serilog;
using log4net;
using log4net.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NLog.Config;
using Serilog;
using Serilog.Events;
using MicrosoftLogger = Microsoft.Extensions.Logging.ILogger;
using MicrosoftLoggerFactory = Microsoft.Extensions.Logging.ILoggerFactory;
using NLogLogger = global::NLog.Logger;
using SerilogLogger = global::Serilog.ILogger;

namespace WidgetSample;

internal enum SampleLogLevel
{
    Trace,
    Debug,
    Information,
    Notice,
    Warning,
    Error,
    Critical,
}

internal interface ISampleLogger
{
    void Log(SampleLogLevel level, string message, Exception exception = null);
}

internal static class SampleLoggerExtensions
{
    public static void Trace(this ISampleLogger logger, string message, Exception exception = null)
    {
        logger.Log(SampleLogLevel.Trace, message, exception);
    }

    public static void Debug(this ISampleLogger logger, string message, Exception exception = null)
    {
        logger.Log(SampleLogLevel.Debug, message, exception);
    }

    public static void Info(this ISampleLogger logger, string message, Exception exception = null)
    {
        logger.Log(SampleLogLevel.Information, message, exception);
    }

    public static void Notice(this ISampleLogger logger, string message, Exception exception = null)
    {
        logger.Log(SampleLogLevel.Notice, message, exception);
    }

    public static void Warn(this ISampleLogger logger, string message, Exception exception = null)
    {
        logger.Log(SampleLogLevel.Warning, message, exception);
    }

    public static void Error(this ISampleLogger logger, string message, Exception exception = null)
    {
        logger.Log(SampleLogLevel.Error, message, exception);
    }

    public static void Error(this ISampleLogger logger, Exception exception)
    {
        logger.Log(SampleLogLevel.Error, exception.Message, exception);
    }
}

internal static class SampleLogging
{
    private static Func<string, ISampleLogger> _createLogger;
    private static IDisposable _lifetime;

    public static string ProviderName { get; private set; }

    public static void Configure()
    {
        if (_createLogger != null)
            return;

        IConfiguration configuration = LoadConfiguration();
        ProviderName = configuration["WidgetSample:Logging:Provider"] ?? "Log4Net";
        EventSinkRouteOptions routes = ConfigurationBinder.Get<EventSinkRouteOptions>(
            configuration.GetSection("DiagnosticExplorer:Routing")
        );
        if (routes == null)
            throw new InvalidOperationException("DiagnosticExplorer:Routing must be configured.");

        switch (ProviderName.Trim().ToUpperInvariant())
        {
            case "LOG4NET":
                ConfigureLog4Net();
                _createLogger = category => new Log4NetSampleLogger(LogManager.GetLogger(category));
                break;

            case "MEL":
                MicrosoftLoggerFactory melFactory = LoggerFactory.Create(builder =>
                {
                    builder.SetMinimumLevel(LogLevel.Trace);
                    builder.AddDiagnosticExplorer(routes);
                });
                _lifetime = melFactory;
                _createLogger = category => new MelSampleLogger(melFactory.CreateLogger(category));
                break;

            case "SERILOG":
                SerilogLogger serilogLogger = new LoggerConfiguration()
                    .MinimumLevel.Verbose()
                    .WriteTo.DiagnosticExplorer(routes)
                    .CreateLogger();
                _lifetime = (IDisposable)serilogLogger;
                _createLogger = category => new SerilogSampleLogger(
                    serilogLogger.ForContext("SourceContext", category)
                );
                break;

            case "NLOG":
                LoggingConfiguration nlogConfiguration = new();
                nlogConfiguration.AddTarget(
                    "diagnosticExplorer",
                    new DiagnosticExplorerTarget(routes)
                );
                nlogConfiguration.AddRuleForAllLevels("diagnosticExplorer");
                global::NLog.LogManager.Configuration = nlogConfiguration;
                _lifetime = new NLogLifetime();
                _createLogger = category => new NLogSampleLogger(
                    global::NLog.LogManager.GetLogger(category)
                );
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported WidgetSample:Logging:Provider '{ProviderName}'. "
                        + "Use Log4Net, MEL, Serilog, or NLog."
                );
        }
    }

    public static ISampleLogger GetLogger(string category)
    {
        if (_createLogger == null)
            throw new InvalidOperationException(
                "SampleLogging.Configure must be called before creating loggers."
            );

        return _createLogger(category);
    }

    public static void Shutdown()
    {
        _lifetime?.Dispose();
        _lifetime = null;
        _createLogger = null;
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

    private static void ConfigureLog4Net()
    {
        XmlConfigurator.ConfigureAndWatch(
            new FileInfo(Path.Combine(AppContext.BaseDirectory, "log4net.config"))
        );
    }

    private sealed class NLogLifetime : IDisposable
    {
        public void Dispose()
        {
            global::NLog.LogManager.Shutdown();
        }
    }
}

internal sealed class Log4NetSampleLogger : ISampleLogger
{
    private readonly ILog _logger;

    public Log4NetSampleLogger(ILog logger)
    {
        _logger = logger;
    }

    public void Log(SampleLogLevel level, string message, Exception exception = null)
    {
        switch (level)
        {
            case SampleLogLevel.Trace:
                _logger.Logger.Log(
                    typeof(Log4NetSampleLogger),
                    log4net.Core.Level.Trace,
                    message,
                    exception
                );
                break;
            case SampleLogLevel.Debug:
                _logger.Debug(message, exception);
                break;
            case SampleLogLevel.Information:
                _logger.Info(message, exception);
                break;
            case SampleLogLevel.Notice:
                _logger.Logger.Log(
                    typeof(Log4NetSampleLogger),
                    log4net.Core.Level.Notice,
                    message,
                    exception
                );
                break;
            case SampleLogLevel.Warning:
                _logger.Warn(message, exception);
                break;
            case SampleLogLevel.Error:
                _logger.Error(message, exception);
                break;
            case SampleLogLevel.Critical:
                _logger.Fatal(message, exception);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(level));
        }
    }
}

internal sealed class MelSampleLogger : ISampleLogger
{
    private readonly MicrosoftLogger _logger;

    public MelSampleLogger(MicrosoftLogger logger)
    {
        _logger = logger;
    }

    public void Log(SampleLogLevel level, string message, Exception exception = null)
    {
        _logger.Log(ToMicrosoftLevel(level), exception, "{Message}", message);
    }

    private static LogLevel ToMicrosoftLevel(SampleLogLevel level)
    {
        return level switch
        {
            SampleLogLevel.Trace => LogLevel.Trace,
            SampleLogLevel.Debug => LogLevel.Debug,
            SampleLogLevel.Information or SampleLogLevel.Notice => LogLevel.Information,
            SampleLogLevel.Warning => LogLevel.Warning,
            SampleLogLevel.Error => LogLevel.Error,
            SampleLogLevel.Critical => LogLevel.Critical,
            _ => throw new ArgumentOutOfRangeException(nameof(level)),
        };
    }
}

internal sealed class SerilogSampleLogger : ISampleLogger
{
    private readonly SerilogLogger _logger;

    public SerilogSampleLogger(SerilogLogger logger)
    {
        _logger = logger;
    }

    public void Log(SampleLogLevel level, string message, Exception exception = null)
    {
        _logger.Write(ToSerilogLevel(level), exception, "{Message}", message);
    }

    private static LogEventLevel ToSerilogLevel(SampleLogLevel level)
    {
        return level switch
        {
            SampleLogLevel.Trace => LogEventLevel.Verbose,
            SampleLogLevel.Debug => LogEventLevel.Debug,
            SampleLogLevel.Information or SampleLogLevel.Notice => LogEventLevel.Information,
            SampleLogLevel.Warning => LogEventLevel.Warning,
            SampleLogLevel.Error => LogEventLevel.Error,
            SampleLogLevel.Critical => LogEventLevel.Fatal,
            _ => throw new ArgumentOutOfRangeException(nameof(level)),
        };
    }
}

internal sealed class NLogSampleLogger : ISampleLogger
{
    private readonly NLogLogger _logger;

    public NLogSampleLogger(NLogLogger logger)
    {
        _logger = logger;
    }

    public void Log(SampleLogLevel level, string message, Exception exception = null)
    {
        _logger.Log(ToNLogLevel(level), exception, message);
    }

    private static global::NLog.LogLevel ToNLogLevel(SampleLogLevel level)
    {
        return level switch
        {
            SampleLogLevel.Trace => global::NLog.LogLevel.Trace,
            SampleLogLevel.Debug => global::NLog.LogLevel.Debug,
            SampleLogLevel.Information or SampleLogLevel.Notice => global::NLog.LogLevel.Info,
            SampleLogLevel.Warning => global::NLog.LogLevel.Warn,
            SampleLogLevel.Error => global::NLog.LogLevel.Error,
            SampleLogLevel.Critical => global::NLog.LogLevel.Fatal,
            _ => throw new ArgumentOutOfRangeException(nameof(level)),
        };
    }
}
