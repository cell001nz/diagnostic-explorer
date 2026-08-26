using System;
using System.Collections.Generic;
using System.Text;
using DiagnosticExplorer.Logging;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;

namespace DiagnosticExplorer.Serilog;

public sealed class DiagnosticExplorerSink : ILogEventSink
{
    private const string SourceContextProperty = "SourceContext";
    private readonly string _fallbackCategory;

    public DiagnosticExplorerSink(EventSinkRouteOptions options, string fallbackCategory = "Application", LogEventStore eventStore = null)
    {
        if (string.IsNullOrWhiteSpace(fallbackCategory))
            throw new ArgumentException("A fallback category is required.", nameof(fallbackCategory));

        _fallbackCategory = fallbackCategory;
        Router = new EventSinkRouter(options, eventStore);
    }

    public EventSinkRouter Router { get; }

    public void Emit(LogEvent logEvent)
    {
        if (logEvent == null)
            throw new ArgumentNullException(nameof(logEvent));

        string category = GetCategory(logEvent);
        LogLevel level = ToLogLevel(logEvent.Level);
        if (!Router.IsEnabled(category, level))
            return;

        string renderedMessage = logEvent.RenderMessage();
        Router.Route(new EventSinkLogEvent(category, level, GetHeadline(renderedMessage), CreateDetail(logEvent, renderedMessage)));
    }

    private string GetCategory(LogEvent logEvent)
    {
        if (
            logEvent.Properties.TryGetValue(SourceContextProperty, out LogEventPropertyValue value)
            && value is ScalarValue scalar
            && scalar.Value is string sourceContext
            && !string.IsNullOrWhiteSpace(sourceContext)
        )
        {
            return sourceContext;
        }

        return _fallbackCategory;
    }

    private static LogLevel ToLogLevel(LogEventLevel level)
    {
        switch (level)
        {
            case LogEventLevel.Verbose:
                return LogLevel.Trace;
            case LogEventLevel.Debug:
                return LogLevel.Debug;
            case LogEventLevel.Information:
                return LogLevel.Information;
            case LogEventLevel.Warning:
                return LogLevel.Warning;
            case LogEventLevel.Error:
                return LogLevel.Error;
            case LogEventLevel.Fatal:
                return LogLevel.Critical;
            default:
                throw new ArgumentOutOfRangeException(nameof(level));
        }
    }

    private static string GetHeadline(string message)
    {
        if (string.IsNullOrEmpty(message))
            return message;

        int newLine = message.IndexOfAny(new[] { '\r', '\n' });
        return newLine < 0 ? message : message.Substring(0, newLine);
    }

    private static string CreateDetail(LogEvent logEvent, string renderedMessage)
    {
        StringBuilder detail = new();
        if (!string.IsNullOrEmpty(renderedMessage) && renderedMessage.Length != GetHeadline(renderedMessage).Length)
            detail.AppendLine(renderedMessage);
        if (logEvent.Exception != null)
            detail.AppendLine(logEvent.Exception.ToString());

        foreach (KeyValuePair<string, LogEventPropertyValue> property in logEvent.Properties)
        {
            if (property.Key == SourceContextProperty)
                continue;

            detail.Append("Property.").Append(property.Key).Append(": ").AppendLine(property.Value.ToString());
        }

        return detail.Length == 0 ? null : detail.ToString().TrimEnd();
    }
}
