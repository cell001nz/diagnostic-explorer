using System;
using Microsoft.Extensions.Logging;

namespace WidgetSample.Harness;

internal static class LoggerExtensions_Mel
{
    public static void Trace(this ILogger logger, string message, Exception exception = null) =>
        logger.Log(LogLevel.Trace, exception, "{Message}", message);

    public static void Debug(this ILogger logger, string message, Exception exception = null) =>
        logger.Log(LogLevel.Debug, exception, "{Message}", message);

    public static void Info(this ILogger logger, string message, Exception exception = null) =>
        logger.Log(LogLevel.Information, exception, "{Message}", message);

    public static void Info(this ILogger logger, string messageTemplate, params object[] propertyValues) =>
        logger.LogInformation(messageTemplate, propertyValues);

    public static void Notice(this ILogger logger, string message, Exception exception = null) =>
        logger.Log(LogLevel.Information, exception, "{Message}", message);

    public static void Warn(this ILogger logger, string message, Exception exception = null) =>
        logger.Log(LogLevel.Warning, exception, "{Message}", message);

    public static void Error(this ILogger logger, string message, Exception exception = null) =>
        logger.Log(LogLevel.Error, exception, "{Message}", message);

    public static void Error(this ILogger logger, Exception exception) => logger.Log(LogLevel.Error, exception, "{Message}", exception.Message);

    public static void Log(this ILogger logger, SampleLogLevel level, string message) => logger.Log(ToLogLevel(level), "{Message}", message);

    private static LogLevel ToLogLevel(SampleLogLevel level) =>
        level switch
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
