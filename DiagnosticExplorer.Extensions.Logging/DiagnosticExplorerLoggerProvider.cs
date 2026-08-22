using System;
using System.Collections.Concurrent;
using DiagnosticExplorer.Logging;
using Microsoft.Extensions.Logging;

namespace DiagnosticExplorer.Extensions.Logging;

public sealed class DiagnosticExplorerLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentDictionary<string, DiagnosticExplorerLogger> _loggers = new(
        StringComparer.Ordinal
    );
    private IExternalScopeProvider _scopeProvider;

    public DiagnosticExplorerLoggerProvider(
        EventSinkRouteOptions options,
        EventSinkRepo sinkRepo = null
    )
    {
        Router = new EventSinkRouter(options, sinkRepo);
    }

    public EventSinkRouter Router { get; }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(
            categoryName ?? string.Empty,
            category => new DiagnosticExplorerLogger(category, this)
        );
    }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider;
    }

    public void Dispose()
    {
        _loggers.Clear();
    }

    internal bool IsEnabled(string category, LogLevel logLevel)
    {
        return logLevel != LogLevel.None && Router.IsEnabled(category, logLevel);
    }

    internal IExternalScopeProvider ScopeProvider => _scopeProvider;
}
