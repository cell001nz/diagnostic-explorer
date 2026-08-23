using System;
using NLog;

namespace WidgetSample.Harness;

public partial class Form1
{
    private static partial bool RemoteDiagnosticsAreHostManaged() => true;

    private static Logger _gadgetLog;
    private static Logger _widgetLog;
    private static Logger _formLog;

    internal static void InitializeLoggers()
    {
        _gadgetLog = LogManager.GetLogger("Gadgets");
        _widgetLog = LogManager.GetLogger("Widgets");
        _formLog = LogManager.GetLogger(typeof(Form1).FullName);
    }
}

internal static class NLogFormLoggerExtensions
{
    public static void Trace(this Logger logger, string message, Exception exception = null) => logger.Log(LogLevel.Trace, exception, message);

    public static void Debug(this Logger logger, string message, Exception exception = null) => logger.Log(LogLevel.Debug, exception, message);

    public static void Info(this Logger logger, string message, Exception exception = null) => logger.Log(LogLevel.Info, exception, message);

    public static void Notice(this Logger logger, string message, Exception exception = null) => logger.Log(LogLevel.Info, exception, message);

    public static void Warn(this Logger logger, string message, Exception exception = null) => logger.Log(LogLevel.Warn, exception, message);

    public static void Error(this Logger logger, string message, Exception exception = null) => logger.Log(LogLevel.Error, exception, message);

    public static void Error(this Logger logger, Exception exception) => logger.Log(LogLevel.Error, exception, exception.Message);

    public static void Log(this Logger logger, SampleLogLevel level, string message) => logger.Log(ToLogLevel(level), message);

    private static LogLevel ToLogLevel(SampleLogLevel level) =>
        level switch
        {
            SampleLogLevel.Trace => LogLevel.Trace,
            SampleLogLevel.Debug => LogLevel.Debug,
            SampleLogLevel.Information or SampleLogLevel.Notice => LogLevel.Info,
            SampleLogLevel.Warning => LogLevel.Warn,
            SampleLogLevel.Error => LogLevel.Error,
            SampleLogLevel.Critical => LogLevel.Fatal,
            _ => throw new ArgumentOutOfRangeException(nameof(level)),
        };
}
