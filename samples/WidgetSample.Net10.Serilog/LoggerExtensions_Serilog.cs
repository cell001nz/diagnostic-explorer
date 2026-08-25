using System;

namespace WidgetSample.Harness;

internal static class LoggerExtensions_Serilog
{
    public static void Trace(this global::Serilog.ILogger logger, string message, Exception exception = null) => logger.Verbose(exception, message);

    public static void Debug(this global::Serilog.ILogger logger, string message, Exception exception = null) => logger.Debug(exception, message);

    public static void Info(this global::Serilog.ILogger logger, string message, Exception exception = null) =>
        logger.Information(exception, message);

    public static void Notice(this global::Serilog.ILogger logger, string message, Exception exception = null) =>
        logger.Information(exception, message);

    public static void Warn(this global::Serilog.ILogger logger, string message, Exception exception = null) => logger.Warning(exception, message);

    public static void Error(this global::Serilog.ILogger logger, string message, Exception exception = null) => logger.Error(exception, message);

    public static void Error(this global::Serilog.ILogger logger, Exception exception) => logger.Error(exception, exception.Message);

    public static void Log(this global::Serilog.ILogger logger, SampleLogLevel level, string message) => logger.Write(ToLogLevel(level), message);

    private static global::Serilog.Events.LogEventLevel ToLogLevel(SampleLogLevel level) =>
        level switch
        {
            SampleLogLevel.Trace => global::Serilog.Events.LogEventLevel.Verbose,
            SampleLogLevel.Debug => global::Serilog.Events.LogEventLevel.Debug,
            SampleLogLevel.Information or SampleLogLevel.Notice => global::Serilog.Events.LogEventLevel.Information,
            SampleLogLevel.Warning => global::Serilog.Events.LogEventLevel.Warning,
            SampleLogLevel.Error => global::Serilog.Events.LogEventLevel.Error,
            SampleLogLevel.Critical => global::Serilog.Events.LogEventLevel.Fatal,
            _ => throw new ArgumentOutOfRangeException(nameof(level)),
        };
}
