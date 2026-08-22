using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;

namespace DiagnosticExplorer.Extensions.Logging;

internal static class DiagnosticExplorerLogDetail
{
    public static string GetHeadline(string message)
    {
        if (string.IsNullOrEmpty(message))
            return message;

        int newLine = message.IndexOfAny(new[] { '\r', '\n' });
        return newLine < 0 ? message : message.Substring(0, newLine);
    }

    public static string Create<TState>(
        string message,
        Exception exception,
        EventId eventId,
        TState state,
        IExternalScopeProvider scopeProvider
    )
    {
        StringBuilder detail = new();
        if (!string.IsNullOrEmpty(message) && message.Length != GetHeadline(message).Length)
            detail.AppendLine(message);
        if (exception != null)
            detail.AppendLine(exception.ToString());
        if (eventId.Id != 0 || !string.IsNullOrEmpty(eventId.Name))
            detail.AppendLine($"EventId: {eventId.Id} {eventId.Name}".TrimEnd());

        AppendState(detail, state, "State");
        scopeProvider?.ForEachScope(
            (scope, builder) => AppendState(builder, scope, "Scope"),
            detail
        );
        return detail.Length == 0 ? null : detail.ToString().TrimEnd();
    }

    private static void AppendState<TState>(StringBuilder detail, TState state, string prefix)
    {
        if (!(state is IEnumerable<KeyValuePair<string, object>> properties))
            return;

        foreach (KeyValuePair<string, object> property in properties)
        {
            if (property.Key == "{OriginalFormat}")
                continue;

            detail
                .Append(prefix)
                .Append('.')
                .Append(property.Key)
                .Append(": ")
                .AppendLine(property.Value?.ToString());
        }
    }
}
