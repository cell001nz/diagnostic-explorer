using System;

namespace DiagnosticExplorer;

public interface ITraceScope : IDisposable
{
    void SetTraceAction(Action<string> traceMethod);
    void SetTraceAction(int timeMillis, Action<string> traceMethod);
    TimeSpan? SuppressDetailThreshold { get; set; }
    void StartAutoTraceTimer(TimeSpan time);
}