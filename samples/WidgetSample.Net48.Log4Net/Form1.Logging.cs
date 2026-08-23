using System;
using log4net;
using log4net.Core;

namespace WidgetSample.Harness;

public partial class Form1
{
    private static partial bool RemoteDiagnosticsAreHostManaged() => false;

    private static ILog _gadgetLog;
    private static ILog _widgetLog;
    private static ILog _formLog;

    internal static void InitializeLoggers()
    {
        _gadgetLog = global::log4net.LogManager.GetLogger("Gadgets");
        _widgetLog = global::log4net.LogManager.GetLogger("Widgets");
        _formLog = global::log4net.LogManager.GetLogger(typeof(Form1));
    }
}

internal static class Log4NetFormLoggerExtensions
{
    public static void Trace(this ILog logger, string message, Exception exception = null) =>
        logger.Logger.Log(typeof(Form1), Level.Trace, message, exception);

    public static void Debug(this ILog logger, string message, Exception exception = null) => logger.Debug(message, exception);

    public static void Info(this ILog logger, string message, Exception exception = null) => logger.Info(message, exception);

    public static void Notice(this ILog logger, string message, Exception exception = null) =>
        logger.Logger.Log(typeof(Form1), Level.Notice, message, exception);

    public static void Warn(this ILog logger, string message, Exception exception = null) => logger.Warn(message, exception);

    public static void Error(this ILog logger, string message, Exception exception = null) => logger.Error(message, exception);

    public static void Error(this ILog logger, Exception exception) => logger.Error(exception.Message, exception);

    public static void Log(this ILog logger, SampleLogLevel level, string message) =>
        logger.Logger.Log(typeof(Form1), ToLogLevel(level), message, null);

    private static Level ToLogLevel(SampleLogLevel level) =>
        level switch
        {
            SampleLogLevel.Trace => Level.Trace,
            SampleLogLevel.Debug => Level.Debug,
            SampleLogLevel.Information => Level.Info,
            SampleLogLevel.Notice => Level.Notice,
            SampleLogLevel.Warning => Level.Warn,
            SampleLogLevel.Error => Level.Error,
            SampleLogLevel.Critical => Level.Fatal,
            _ => throw new ArgumentOutOfRangeException(nameof(level)),
        };
}
