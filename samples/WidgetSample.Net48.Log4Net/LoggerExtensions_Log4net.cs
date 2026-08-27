using System;
using System.Globalization;
using log4net;
using log4net.Core;

namespace WidgetSample.Harness;

internal static class LoggerExtensions_Log4net
{
    public static void Trace(this ILog logger, string message, Exception exception = null) =>
        logger.Logger.Log(typeof(Form1), Level.Trace, message, exception);

    public static void Debug(this ILog logger, string message, Exception exception = null) => logger.Debug(message, exception);

    public static void Info(this ILog logger, string message, Exception exception = null) => logger.Info(message, exception);

    public static void Info(this ILog logger, string messageTemplate, params object[] propertyValues) =>
        logger.Info(RenderMessageTemplate(messageTemplate, propertyValues));

    public static void Notice(this ILog logger, string message, Exception exception = null) =>
        logger.Logger.Log(typeof(Form1), Level.Notice, message, exception);

    public static void Warn(this ILog logger, string message, Exception exception = null) => logger.Warn(message, exception);

    public static void Error(this ILog logger, string message, Exception exception = null) => logger.Error(message, exception);

    public static void Error(this ILog logger, Exception exception) => logger.Error(exception.Message, exception);

    public static void Log(this ILog logger, SampleLogLevel level, string message) =>
        logger.Logger.Log(typeof(Form1), ToLogLevel(level), message, null);

    private static string RenderMessageTemplate(string messageTemplate, object[] propertyValues)
    {
        int valueIndex = 0;
        return string.Format(CultureInfo.CurrentCulture, ReplaceNamedPlaceholders(messageTemplate, propertyValues, ref valueIndex), propertyValues);
    }

    private static string ReplaceNamedPlaceholders(string messageTemplate, object[] propertyValues, ref int valueIndex)
    {
        System.Text.StringBuilder result = new();
        for (int index = 0; index < messageTemplate.Length; index++)
        {
            if (messageTemplate[index] != '{' || index + 1 >= messageTemplate.Length || messageTemplate[index + 1] == '{')
            {
                result.Append(messageTemplate[index]);
                continue;
            }

            int closingBrace = messageTemplate.IndexOf('}', index + 1);
            if (closingBrace < 0 || valueIndex >= propertyValues.Length)
            {
                result.Append(messageTemplate[index]);
                continue;
            }

            string placeholder = messageTemplate.Substring(index + 1, closingBrace - index - 1);
            int formatIndex = placeholder.IndexOf(':');
            string format = formatIndex < 0 ? string.Empty : placeholder.Substring(formatIndex);
            result.Append('{').Append(valueIndex++).Append(format).Append('}');
            index = closingBrace;
        }

        return result.ToString();
    }

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
