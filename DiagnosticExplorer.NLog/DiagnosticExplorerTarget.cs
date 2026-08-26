using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DiagnosticExplorer.Logging;
using NLog;
using NLog.Layouts;
using NLog.Targets;
using MicrosoftLogLevel = Microsoft.Extensions.Logging.LogLevel;
using NLogLevel = global::NLog.LogLevel;

namespace DiagnosticExplorer.NLog;

[Target("DiagnosticExplorer")]
public sealed class DiagnosticExplorerTarget : TargetWithLayout
{
    private readonly LogEventStore _eventStore;
    private EventSinkRouter _router;

    public DiagnosticExplorerTarget()
        : this(new EventSinkRouteOptions()) { }

    public DiagnosticExplorerTarget(EventSinkRouteOptions options, LogEventStore eventStore = null)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        _eventStore = eventStore ?? DiagnosticManager.LogEventStore;
        Layout = new SimpleLayout("${message}");
    }

    public EventSinkRouteOptions Options { get; set; }

    public string FallbackCategory { get; set; } = "Application";

    protected override void InitializeTarget()
    {
        base.InitializeTarget();
        _router = new EventSinkRouter(Options, _eventStore);
    }

    protected override void Write(LogEventInfo logEvent)
    {
        if (logEvent == null)
            throw new ArgumentNullException(nameof(logEvent));

        EventSinkRouter router = _router ?? new EventSinkRouter(Options, _eventStore);
        string category = string.IsNullOrWhiteSpace(logEvent.LoggerName) ? FallbackCategory : logEvent.LoggerName;
        MicrosoftLogLevel level = ToLogLevel(logEvent.Level);
        if (!router.IsEnabled(category, level))
            return;

        string renderedMessage = RenderLogEvent(Layout, logEvent);
        router.Route(new EventSinkLogEvent(category, level, GetHeadline(renderedMessage), CreateDetail(logEvent, renderedMessage)));
    }

    private static MicrosoftLogLevel ToLogLevel(NLogLevel level)
    {
        if (level == NLogLevel.Trace)
            return MicrosoftLogLevel.Trace;
        if (level == NLogLevel.Debug)
            return MicrosoftLogLevel.Debug;
        if (level == NLogLevel.Info)
            return MicrosoftLogLevel.Information;
        if (level == NLogLevel.Warn)
            return MicrosoftLogLevel.Warning;
        if (level == NLogLevel.Error)
            return MicrosoftLogLevel.Error;
        if (level == NLogLevel.Fatal)
            return MicrosoftLogLevel.Critical;

        throw new ArgumentOutOfRangeException(nameof(level));
    }

    private static string GetHeadline(string message)
    {
        if (string.IsNullOrEmpty(message))
            return message;

        int newLine = message.IndexOfAny(new[] { '\r', '\n' });
        return newLine < 0 ? message : message.Substring(0, newLine);
    }

    private static string CreateDetail(LogEventInfo logEvent, string renderedMessage)
    {
        StringBuilder detail = new();
        if (!string.IsNullOrEmpty(renderedMessage) && renderedMessage.Length != GetHeadline(renderedMessage).Length)
            detail.AppendLine(renderedMessage);
        if (logEvent.Exception != null)
            detail.AppendLine(logEvent.Exception.ToString());

        if (logEvent.Properties is IDictionary properties)
        {
            foreach (DictionaryEntry property in properties)
            {
                detail.Append("Property.").Append(property.Key).Append(": ").AppendLine(property.Value?.ToString());
            }
        }

        return detail.Length == 0 ? null : detail.ToString().TrimEnd();
    }
}
